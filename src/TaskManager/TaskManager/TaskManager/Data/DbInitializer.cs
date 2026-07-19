using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Models;

namespace TaskManager.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            ApplicationDbContext context,
            SeedOptions options,
            ILogger logger)
        {
            await context.Database.MigrateAsync();
            await SeedPlansAsync(context, logger);

            if (options.CreateSuperAdmin)
            {
                await SeedSuperAdminAsync(context, options, logger);
            }

            if (options.SeedDemoData)
            {
                await SeedDemoOrganizationAsync(context, logger);
            }

            logger.LogInformation("Database initialization completed");
        }

        private static async Task SeedSuperAdminAsync(
            ApplicationDbContext context,
            SeedOptions options,
            ILogger logger)
        {
            if (await context.Users.IgnoreQueryFilters().AnyAsync(user => user.Role == Roles.SuperAdmin))
            {
                logger.LogInformation("A platform SuperAdmin already exists; skipping account creation");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.SuperAdminPassword))
            {
                throw new InvalidOperationException(
                    "Seed:SuperAdminPassword is required when Seed:CreateSuperAdmin is enabled.");
            }

            if (options.SuperAdminPassword.Length < 12 ||
                !options.SuperAdminPassword.Any(char.IsUpper) ||
                !options.SuperAdminPassword.Any(char.IsLower) ||
                !options.SuperAdminPassword.Any(char.IsDigit))
            {
                throw new InvalidOperationException(
                    "Seed:SuperAdminPassword must be at least 12 characters and contain upper, lower, and numeric characters.");
            }

            context.Users.Add(new User
            {
                Username = options.SuperAdminUsername.Trim(),
                Email = options.SuperAdminEmail.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.SuperAdminPassword),
                FirstName = "Platform",
                LastName = "Administrator",
                Role = Roles.SuperAdmin,
                OrganizationId = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Created platform SuperAdmin account {Username}; password was not logged",
                options.SuperAdminUsername);
        }

        private static async Task SeedDemoOrganizationAsync(
            ApplicationDbContext context,
            ILogger logger)
        {
            if (await context.Organizations.AnyAsync(organization => organization.Slug == "acme"))
            {
                logger.LogInformation("Demo organization already exists; skipping demo seed");
                return;
            }

            var org = new Organization
            {
                Name = "Acme Corporation",
                Slug = "acme",
                Description = "Demo organization seeded for local development.",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Organizations.Add(org);
            await context.SaveChangesAsync();

            var orgAdmin = new User
            {
                Username = "orgadmin",
                Email = "orgadmin@acme.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FirstName = "Acme",
                LastName = "Administrator",
                Role = Roles.OrganizationAdmin,
                OrganizationId = org.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var manager = new User
            {
                Username = "manager",
                Email = "manager@acme.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                FirstName = "Project",
                LastName = "Manager",
                Role = Roles.Manager,
                OrganizationId = org.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var user = new User
            {
                Username = "user",
                Email = "user@acme.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                FirstName = "Team",
                LastName = "Member",
                Role = Roles.User,
                OrganizationId = org.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.AddRange(orgAdmin, manager, user);
            await context.SaveChangesAsync();

            context.OrganizationMembers.AddRange(
                new OrganizationMember
                {
                    OrganizationId = org.Id,
                    UserId = orgAdmin.Id,
                    Role = Roles.OrganizationAdmin
                },
                new OrganizationMember
                {
                    OrganizationId = org.Id,
                    UserId = manager.Id,
                    Role = Roles.Manager
                },
                new OrganizationMember
                {
                    OrganizationId = org.Id,
                    UserId = user.Id,
                    Role = Roles.User
                });
            await context.SaveChangesAsync();

            logger.LogWarning(
                "Seeded local demo accounts. Seed:SeedDemoData must remain disabled outside development.");
        }

        /// <summary>
        /// Upserts the plan catalog (Plans + PlanFeatures) from <see cref="PlanCatalog"/>.
        /// Idempotent and fault-tolerant: logs and continues if billing tables don't exist yet.
        /// </summary>
        private static async Task SeedPlansAsync(ApplicationDbContext context, ILogger logger)
        {
            try
            {
                foreach (var def in PlanCatalog.Plans)
                {
                    var plan = await context.Plans
                        .Include(p => p.Features)
                        .FirstOrDefaultAsync(p => p.Code == def.Code);

                    if (plan is null)
                    {
                        plan = new Plan { Code = def.Code, CreatedAt = DateTime.UtcNow };
                        context.Plans.Add(plan);
                    }

                    plan.Name = def.Name;
                    plan.Description = def.Description;
                    plan.SortOrder = def.SortOrder;
                    plan.TrialDays = def.TrialDays;
                    plan.IsCustomPricing = def.IsCustomPricing;
                    plan.Currency = def.Currency;
                    plan.MonthlyPricePerSeat = def.MonthlyPricePerSeat;
                    plan.AnnualPricePerSeat = def.AnnualPricePerSeat;
                    plan.IsActive = true;
                    plan.UpdatedAt = DateTime.UtcNow;

                    var desiredEntitlements = new Dictionary<string, (bool IsEnabled, long? Limit)>();
                    foreach (var key in FeatureKeys.All)
                        desiredEntitlements[key] = (def.HasFeature(key), null);
                    foreach (var key in LimitKeys.All)
                        desiredEntitlements[key] = (true, def.GetLimit(key));

                    foreach (var obsolete in plan.Features
                        .Where(feature => !desiredEntitlements.ContainsKey(feature.Key))
                        .ToList())
                    {
                        plan.Features.Remove(obsolete);
                    }

                    foreach (var (key, entitlement) in desiredEntitlements)
                    {
                        var feature = plan.Features.FirstOrDefault(existing => existing.Key == key);
                        if (feature is null)
                        {
                            feature = new PlanFeature { Key = key };
                            plan.Features.Add(feature);
                        }

                        feature.IsEnabled = entitlement.IsEnabled;
                        feature.Limit = entitlement.Limit;
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {PlanCount} subscription plans", PlanCatalog.Plans.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipped plan seeding; billing tables might not exist yet");
            }
        }
    }
}
