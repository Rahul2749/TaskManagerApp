using System.Text.Json;
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
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            ApplicationDbContext context,
            ITenantService tenant,
            IEntitlementService entitlements,
            IBillingProvider provider,
            IOptions<RazorpayOptions> razorpay,
            ILogger<BillingController> logger)
        {
            _context = context;
            _tenant = tenant;
            _entitlements = entitlements;
            _provider = provider;
            _razorpay = razorpay.Value;
            _logger = logger;
        }

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
                IsActive = sub is null || SubscriptionStatus.GrantsAccess(sub.Status),
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
                    "Billing is not configured yet. Add the payment provider keys to enable checkout.");

            if (_tenant.OrganizationId is null)
                return BadRequest("Only organization users can subscribe.");

            var def = PlanCatalog.GetByCode(request.PlanCode);
            if (def is null || def.Code == PlanCodes.Free)
                return BadRequest("Invalid plan for checkout.");

            var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Code == request.PlanCode, ct);
            var providerPlanId = request.BillingInterval == BillingIntervals.Annual
                ? plan?.ProviderAnnualPlanId
                : plan?.ProviderMonthlyPlanId;

            if (string.IsNullOrWhiteSpace(providerPlanId))
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "This plan is not linked to the payment provider yet.");

            var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == _tenant.OrganizationId, ct);
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == _tenant.UserId, ct);

            var existing = await _context.Subscriptions.FirstOrDefaultAsync(s => s.OrganizationId == _tenant.OrganizationId, ct);

            var customerId = await _provider.EnsureCustomerAsync(
                new BillingCustomer(existing?.ProviderCustomerId, org?.Name ?? "Customer", admin?.Email ?? "", null), ct);

            // total_count: number of billing cycles (12 for annual view of monthly, etc.). Simplified.
            var totalCount = request.BillingInterval == BillingIntervals.Annual ? 5 : 60;

            var providerSub = await _provider.CreateSubscriptionAsync(
                new CreateSubscriptionRequest(customerId, providerPlanId!, request.Seats, totalCount,
                    $"org:{_tenant.OrganizationId}"), ct);

            // Persist a pending subscription; webhook will activate it.
            if (existing is null)
            {
                _context.Subscriptions.Add(new Subscription
                {
                    OrganizationId = _tenant.OrganizationId.Value,
                    PlanId = plan!.Id,
                    Status = SubscriptionStatus.Incomplete,
                    BillingInterval = request.BillingInterval,
                    Seats = request.Seats,
                    Provider = _provider.Name,
                    ProviderCustomerId = customerId,
                    ProviderSubscriptionId = providerSub.ProviderSubscriptionId
                });
            }
            else
            {
                existing.PlanId = plan!.Id;
                existing.BillingInterval = request.BillingInterval;
                existing.Seats = request.Seats;
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

            sub.Status = eventType switch
            {
                "subscription.activated" or "subscription.charged" or "subscription.resumed" => SubscriptionStatus.Active,
                "subscription.pending" or "subscription.halted" => SubscriptionStatus.PastDue,
                "subscription.cancelled" or "subscription.completed" => SubscriptionStatus.Canceled,
                _ => sub.Status
            };

            if (entity.TryGetProperty("current_end", out var ce) && ce.ValueKind == JsonValueKind.Number)
                sub.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(ce.GetInt64()).UtcDateTime;

            sub.UpdatedAt = DateTime.UtcNow;
            _entitlements.Invalidate(sub.OrganizationId);
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
