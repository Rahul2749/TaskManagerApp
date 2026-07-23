using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BillingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;
        private readonly IEntitlementService _entitlements;
        private readonly IBillingProvider _provider;
        private readonly RazorpayOptions _razorpay;
        private readonly AppOptions _app;
        private readonly IBackgroundJobClient _jobs;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            ApplicationDbContext context,
            ITenantService tenant,
            IEntitlementService entitlements,
            IBillingProvider provider,
            IOptions<RazorpayOptions> razorpay,
            IOptions<AppOptions> app,
            IBackgroundJobClient jobs,
            ILogger<BillingController> logger)
        {
            _context = context;
            _tenant = tenant;
            _entitlements = entitlements;
            _provider = provider;
            _razorpay = razorpay.Value;
            _app = app.Value;
            _jobs = jobs;
            _logger = logger;
        }

        /// <summary>Whether the payment provider credentials are configured.</summary>
        [AllowAnonymous]
        [HttpGet("status")]
        public ActionResult<object> GetStatus() =>
            Ok(new
            {
                provider = _provider.Name,
                configured = _provider.IsConfigured,
                webhookConfigured = !string.IsNullOrWhiteSpace(_razorpay.WebhookSecret)
            });

        /// <summary>Public pricing catalog.</summary>
        [AllowAnonymous]
        [HttpGet("plans")]
        public ActionResult<IEnumerable<PlanDto>> GetPlans() =>
            Ok(PlanCatalog.Plans.OrderBy(p => p.SortOrder).Select(ToDto).ToList());

        /// <summary>The current organization's subscription and resolved entitlements.</summary>
        [Authorize]
        [HttpGet("subscription")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscription(CancellationToken ct)
        {
            var orgId = _tenant.OrganizationId;
            if (orgId is null)
                return Forbid();

            var plan = await _entitlements.GetPlanAsync(orgId, ct);

            var sub = await _context.Subscriptions.Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);

            var graceDays = Math.Max(1, _app.BillingGracePeriodDays);
            var softLimited = sub?.Status == SubscriptionStatus.PastDue
                              && sub.PastDueSince is not null
                              && DateTime.UtcNow >= sub.PastDueSince.Value.AddDays(graceDays);
            var graceEndsAt = sub?.Status == SubscriptionStatus.PastDue && sub.PastDueSince is not null
                ? sub.PastDueSince.Value.AddDays(graceDays)
                : (DateTime?)null;
            var graceDaysRemaining = graceEndsAt is null
                ? 0
                : Math.Max(0, (int)Math.Ceiling((graceEndsAt.Value - DateTime.UtcNow).TotalDays));

            return Ok(new SubscriptionDto
            {
                PlanCode = plan.Code,
                PlanName = plan.Name,
                Status = sub?.Status ?? SubscriptionStatus.Active,
                BillingInterval = sub?.BillingInterval ?? BillingIntervals.Monthly,
                Seats = sub?.Seats ?? 0,
                CurrentPeriodEnd = sub?.CurrentPeriodEnd,
                TrialEndsAt = sub?.TrialEndsAt,
                CancelAtPeriodEnd = sub?.CancelAtPeriodEnd ?? false,
                PastDueSince = sub?.PastDueSince,
                GraceEndsAt = graceEndsAt,
                GraceDaysRemaining = graceDaysRemaining,
                IsSoftLimited = softLimited,
                IsActive = !softLimited && (sub is null || SubscriptionStatus.GrantsAccess(sub.Status)),
                Features = plan.Features.ToList(),
                Limits = new Dictionary<string, long?>(plan.Limits)
            });
        }

        /// <summary>Invoices for the current organization.</summary>
        [Authorize(Roles = "OrganizationAdmin")]
        [HttpGet("invoices")]
        public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetInvoices(CancellationToken ct)
        {
            var invoices = await _context.Invoices
                .OrderByDescending(i => i.IssuedAt)
                .Select(i => new InvoiceDto
                {
                    Id = i.Id,
                    Number = i.Number,
                    Amount = i.Amount,
                    Currency = i.Currency,
                    Status = i.Status,
                    PdfUrl = i.PdfUrl,
                    IssuedAt = i.IssuedAt,
                    PaidAt = i.PaidAt
                })
                .ToListAsync(ct);

            return Ok(invoices);
        }

        /// <summary>Starts a subscription checkout for the current organization.</summary>
        [Authorize(Roles = "OrganizationAdmin")]
        [HttpPost("checkout")]
        public async Task<ActionResult<CheckoutSessionDto>> Checkout([FromBody] CheckoutRequestDto request, CancellationToken ct)
        {
            if (!_provider.IsConfigured)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Billing is not configured yet. Add Razorpay:KeyId and Razorpay:KeySecret.");

            if (_tenant.OrganizationId is null)
                return BadRequest("Only organization users can subscribe.");

            var def = PlanCatalog.GetByCode(request.PlanCode);
            if (def is null || def.Code == PlanCodes.Free || def.IsCustomPricing)
                return BadRequest("Invalid plan for checkout.");

            if (request.Seats < 1)
                return BadRequest("Seats must be at least 1.");

            var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Code == request.PlanCode, ct);
            if (plan is null)
                return BadRequest("Plan is not available.");

            var providerPlanId = request.BillingInterval == BillingIntervals.Annual
                ? plan.ProviderAnnualPlanId
                : plan.ProviderMonthlyPlanId;

            if (string.IsNullOrWhiteSpace(providerPlanId))
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "This plan is not linked to Razorpay yet. Restart the app after configuring keys so plans can sync.");

            var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == _tenant.OrganizationId, ct);
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == _tenant.UserId, ct);

            var existing = await _context.Subscriptions.FirstOrDefaultAsync(s => s.OrganizationId == _tenant.OrganizationId, ct);

            var customerId = await _provider.EnsureCustomerAsync(
                new BillingCustomer(existing?.ProviderCustomerId, org?.Name ?? "Customer", admin?.Email ?? "", null), ct);

            // When changing plans, cancel the previous provider subscription so we don't leave orphans.
            if (existing is not null
                && !string.IsNullOrWhiteSpace(existing.ProviderSubscriptionId)
                && existing.Status is not (SubscriptionStatus.Canceled or SubscriptionStatus.Incomplete))
            {
                try
                {
                    await _provider.CancelSubscriptionAsync(existing.ProviderSubscriptionId!, atPeriodEnd: false, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not cancel previous provider subscription {SubId} before plan change",
                        existing.ProviderSubscriptionId);
                }
            }

            // total_count: billed cycles before the subscription ends (Razorpay requirement).
            var totalCount = request.BillingInterval == BillingIntervals.Annual ? 10 : 120;

            var providerSub = await _provider.CreateSubscriptionAsync(
                new CreateSubscriptionRequest(
                    customerId,
                    providerPlanId!,
                    request.Seats,
                    totalCount,
                    $"org:{_tenant.OrganizationId}",
                    def.TrialDays), ct);

            var trialEndsAt = def.TrialDays > 0
                ? DateTime.UtcNow.AddDays(def.TrialDays)
                : (DateTime?)null;

            // Persist a pending subscription; webhook will activate it.
            if (existing is null)
            {
                _context.Subscriptions.Add(new Subscription
                {
                    OrganizationId = _tenant.OrganizationId.Value,
                    PlanId = plan.Id,
                    Status = SubscriptionStatus.Incomplete,
                    BillingInterval = request.BillingInterval,
                    Seats = request.Seats,
                    TrialEndsAt = trialEndsAt,
                    Provider = _provider.Name,
                    ProviderCustomerId = customerId,
                    ProviderSubscriptionId = providerSub.ProviderSubscriptionId
                });
            }
            else
            {
                existing.PlanId = plan.Id;
                existing.BillingInterval = request.BillingInterval;
                existing.Seats = request.Seats;
                existing.TrialEndsAt = trialEndsAt;
                existing.CancelAtPeriodEnd = false;
                existing.PastDueSince = null;
                existing.ProviderCustomerId = customerId;
                existing.ProviderSubscriptionId = providerSub.ProviderSubscriptionId;
                existing.Status = SubscriptionStatus.Incomplete;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            _entitlements.Invalidate(_tenant.OrganizationId.Value);

            return Ok(new CheckoutSessionDto
            {
                Provider = _provider.Name,
                ProviderKeyId = _razorpay.KeyId,
                ProviderSubscriptionId = providerSub.ProviderSubscriptionId,
                CheckoutUrl = providerSub.ShortUrl,
                PlanCode = def.Code,
                Currency = def.Currency,
                CustomerName = org?.Name,
                CustomerEmail = admin?.Email
            });
        }

        /// <summary>Cancels the current organization's subscription at period end.</summary>
        [Authorize(Roles = "OrganizationAdmin")]
        [HttpPost("cancel")]
        public async Task<ActionResult> Cancel(CancellationToken ct)
        {
            var sub = await _context.Subscriptions.FirstOrDefaultAsync(s => s.OrganizationId == _tenant.OrganizationId, ct);
            if (sub is null || string.IsNullOrWhiteSpace(sub.ProviderSubscriptionId))
                return NotFound("No active subscription.");

            await _provider.CancelSubscriptionAsync(sub.ProviderSubscriptionId!, atPeriodEnd: true, ct);
            sub.CancelAtPeriodEnd = true;
            sub.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>Inbound webhook from the payment provider. Verifies signature and syncs state.</summary>
        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<ActionResult> Webhook(CancellationToken ct)
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(ct);

            var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? string.Empty;
            if (!_provider.VerifyWebhookSignature(payload, signature))
                return Unauthorized();

            JsonElement root;
            try { root = JsonSerializer.Deserialize<JsonElement>(payload); }
            catch { return BadRequest(); }

            var eventType = root.TryGetProperty("event", out var e) ? e.GetString() ?? "" : "";
            var eventId = Request.Headers["X-Razorpay-Event-Id"].FirstOrDefault()
                          ?? $"{eventType}:{Guid.NewGuid()}";

            // Idempotency: skip if we've already recorded this event.
            if (await _context.BillingEvents.AnyAsync(b => b.Provider == _provider.Name && b.EventId == eventId, ct))
                return Ok();

            var record = new BillingEvent
            {
                Provider = _provider.Name,
                EventId = eventId,
                EventType = eventType,
                Payload = payload
            };
            _context.BillingEvents.Add(record);

            try
            {
                await HandleEventAsync(eventType, root, ct);
                record.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                record.Error = ex.Message[..Math.Min(ex.Message.Length, 480)];
                _logger.LogError(ex, "Failed handling billing webhook {EventType}", eventType);
            }

            await _context.SaveChangesAsync(ct);
            return Ok();
        }

        private async Task HandleEventAsync(string eventType, JsonElement root, CancellationToken ct)
        {
            if (eventType.StartsWith("invoice.", StringComparison.Ordinal))
            {
                await HandleInvoiceEventAsync(eventType, root, ct);
                return;
            }

            // Razorpay nests the subscription entity under payload.subscription.entity.
            if (!root.TryGetProperty("payload", out var payloadEl) ||
                !payloadEl.TryGetProperty("subscription", out var subEl) ||
                !subEl.TryGetProperty("entity", out var entity))
                return;

            var providerSubId = entity.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(providerSubId))
                return;

            var sub = await _context.Subscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == providerSubId, ct);
            if (sub is null)
                return;

            var previousStatus = sub.Status;
            sub.Status = eventType switch
            {
                "subscription.activated" or "subscription.charged" or "subscription.resumed" => SubscriptionStatus.Active,
                "subscription.authenticated" => sub.TrialEndsAt is not null && sub.TrialEndsAt > DateTime.UtcNow
                    ? SubscriptionStatus.Trialing
                    : SubscriptionStatus.Active,
                "subscription.pending" or "subscription.halted" => SubscriptionStatus.PastDue,
                "subscription.cancelled" or "subscription.completed" or "subscription.expired" => SubscriptionStatus.Canceled,
                _ => sub.Status
            };

            if (eventType is "subscription.activated" or "subscription.charged" or "subscription.resumed"
                && sub.TrialEndsAt is not null && sub.TrialEndsAt > DateTime.UtcNow)
            {
                sub.Status = SubscriptionStatus.Trialing;
            }

            if (entity.TryGetProperty("current_end", out var ce) && ce.ValueKind == JsonValueKind.Number)
                sub.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(ce.GetInt64()).UtcDateTime;

            if (eventType is "subscription.cancelled" or "subscription.completed")
                sub.CancelAtPeriodEnd = false;

            if (sub.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Canceled)
                sub.PastDueSince = null;
            else if (sub.Status == SubscriptionStatus.PastDue && sub.PastDueSince is null)
                sub.PastDueSince = DateTime.UtcNow;

            sub.UpdatedAt = DateTime.UtcNow;
            _entitlements.Invalidate(sub.OrganizationId);

            if (sub.Status == SubscriptionStatus.PastDue && previousStatus != SubscriptionStatus.PastDue)
                await EnqueueDunningAsync(sub, ct);
        }

        private async Task HandleInvoiceEventAsync(string eventType, JsonElement root, CancellationToken ct)
        {
            if (!root.TryGetProperty("payload", out var payloadEl) ||
                !payloadEl.TryGetProperty("invoice", out var invoiceEl) ||
                !invoiceEl.TryGetProperty("entity", out var entity))
                return;

            var providerInvoiceId = entity.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(providerInvoiceId))
                return;

            string? providerSubId = null;
            if (entity.TryGetProperty("subscription_id", out var subIdEl) && subIdEl.ValueKind == JsonValueKind.String)
                providerSubId = subIdEl.GetString();

            Subscription? sub = null;
            if (!string.IsNullOrWhiteSpace(providerSubId))
            {
                sub = await _context.Subscriptions
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == providerSubId, ct);
            }

            if (sub is null)
                return;

            var invoice = await _context.Invoices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.ProviderInvoiceId == providerInvoiceId, ct);

            var amountPaise = entity.TryGetProperty("amount", out var amountEl) && amountEl.ValueKind == JsonValueKind.Number
                ? amountEl.GetInt64()
                : 0L;
            var currency = entity.TryGetProperty("currency", out var currencyEl)
                ? currencyEl.GetString() ?? "INR"
                : "INR";
            var status = eventType switch
            {
                "invoice.paid" => "paid",
                "invoice.partially_paid" => "due",
                "invoice.expired" => "failed",
                _ => entity.TryGetProperty("status", out var st) ? st.GetString() ?? "due" : "due"
            };

            var issuedAt = DateTime.UtcNow;
            if (entity.TryGetProperty("issued_at", out var issuedEl) && issuedEl.ValueKind == JsonValueKind.Number)
                issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedEl.GetInt64()).UtcDateTime;

            if (invoice is null)
            {
                invoice = new Invoice
                {
                    OrganizationId = sub.OrganizationId,
                    Number = entity.TryGetProperty("receipt", out var receiptEl) && receiptEl.ValueKind == JsonValueKind.String
                        ? receiptEl.GetString() ?? providerInvoiceId
                        : providerInvoiceId,
                    Amount = amountPaise / 100m,
                    Currency = currency,
                    Status = status,
                    ProviderInvoiceId = providerInvoiceId,
                    PdfUrl = entity.TryGetProperty("short_url", out var urlEl) ? urlEl.GetString() : null,
                    IssuedAt = issuedAt,
                    PaidAt = status == "paid" ? DateTime.UtcNow : null
                };
                _context.Invoices.Add(invoice);
            }
            else
            {
                invoice.Amount = amountPaise / 100m;
                invoice.Currency = currency;
                invoice.Status = status;
                invoice.PdfUrl = entity.TryGetProperty("short_url", out var urlEl) ? urlEl.GetString() : invoice.PdfUrl;
                if (status == "paid" && invoice.PaidAt is null)
                    invoice.PaidAt = DateTime.UtcNow;
            }

            if (status == "paid" && sub.Status is SubscriptionStatus.Incomplete or SubscriptionStatus.PastDue)
            {
                sub.Status = SubscriptionStatus.Active;
                sub.PastDueSince = null;
                sub.UpdatedAt = DateTime.UtcNow;
                _entitlements.Invalidate(sub.OrganizationId);
            }

            if (status == "failed" && sub.Status is not (SubscriptionStatus.Canceled or SubscriptionStatus.Incomplete))
            {
                var wasPastDue = sub.Status == SubscriptionStatus.PastDue;
                sub.Status = SubscriptionStatus.PastDue;
                sub.PastDueSince ??= DateTime.UtcNow;
                sub.UpdatedAt = DateTime.UtcNow;
                _entitlements.Invalidate(sub.OrganizationId);
                if (!wasPastDue)
                    await EnqueueDunningAsync(sub, ct);
            }

            if (status == "paid")
            {
                var admin = await _context.Users.IgnoreQueryFilters()
                    .Where(u => u.OrganizationId == sub.OrganizationId
                                && u.Role == Roles.OrganizationAdmin
                                && u.IsActive)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync(ct);

                if (admin is not null)
                {
                    _jobs.Enqueue<EmailJobs>(j =>
                        j.SendReceipt(admin.Email, invoice.Number, invoice.Amount, invoice.Currency));
                }
            }
        }

        private async Task EnqueueDunningAsync(Subscription sub, CancellationToken ct)
        {
            var admin = await _context.Users.IgnoreQueryFilters()
                .Where(u => u.OrganizationId == sub.OrganizationId
                            && u.Role == Roles.OrganizationAdmin
                            && u.IsActive)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(ct);
            if (admin is null) return;

            var org = await _context.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == sub.OrganizationId, ct);
            var billingUrl = $"{_app.PublicBaseUrl.TrimEnd('/')}/billing";
            var graceDays = Math.Max(1, _app.BillingGracePeriodDays);
            var remaining = sub.PastDueSince is null
                ? graceDays
                : Math.Max(0, (int)Math.Ceiling((sub.PastDueSince.Value.AddDays(graceDays) - DateTime.UtcNow).TotalDays));

            var email = admin.Email;
            var firstName = string.IsNullOrWhiteSpace(admin.FirstName) ? admin.Username : admin.FirstName;
            var workspaceName = string.IsNullOrWhiteSpace(org?.Name) ? "your workspace" : org!.Name;

            _jobs.Enqueue<EmailJobs>(j =>
                j.SendPaymentFailed(email, firstName, workspaceName, billingUrl, remaining));
        }

        private static PlanDto ToDto(PlanDefinition d) => new()
        {
            Code = d.Code,
            Name = d.Name,
            Description = d.Description,
            SortOrder = d.SortOrder,
            TrialDays = d.TrialDays,
            IsCustomPricing = d.IsCustomPricing,
            Currency = d.Currency,
            MonthlyPricePerSeat = d.MonthlyPricePerSeat,
            AnnualPricePerSeat = d.AnnualPricePerSeat,
            Features = d.Features.ToList(),
            Limits = new Dictionary<string, long?>(d.Limits)
        };
    }
}
