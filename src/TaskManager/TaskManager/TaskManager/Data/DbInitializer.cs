using Microsoft.EntityFrameworkCore;
using TaskManager.Models;

namespace TaskManager.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            try
            {
                // Apply any pending migrations.
                await context.Database.MigrateAsync();

                // Seeding writes cross-tenant records (platform admin + demo org), so it must
                // bypass the tenant query filter on the existence checks.
                if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Role == Roles.SuperAdmin))
                {
                    return; // DB has been seeded
                }

                // ── Platform-wide SuperAdmin (no organization) ─────────────
                context.Users.Add(new User
                {
                    Username = "superadmin",
                    Email = "superadmin@taskmanager.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    FirstName = "Platform",
                    LastName = "Administrator",
                    Role = Roles.SuperAdmin,
                    OrganizationId = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();

                // ── Demo organization + its admin + manager + user ──────────
                var org = new Organization
                {
                    Name = "Acme Corporation",
                    Slug = "acme",
                    Description = "Demo organization seeded for first run.",
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

                // Organization memberships
                context.OrganizationMembers.AddRange(
                    new OrganizationMember { OrganizationId = org.Id, UserId = orgAdmin.Id, Role = Roles.OrganizationAdmin },
                    new OrganizationMember { OrganizationId = org.Id, UserId = manager.Id, Role = Roles.Manager },
                    new OrganizationMember { OrganizationId = org.Id, UserId = user.Id, Role = Roles.User }
                );
                await context.SaveChangesAsync();

                Console.WriteLine("========================================");
                Console.WriteLine("Database initialized (multi-tenant)!");
                Console.WriteLine("========================================");
                Console.WriteLine("Platform SuperAdmin: superadmin / Admin@123");
                Console.WriteLine("Acme OrgAdmin:       orgadmin / Admin@123");
                Console.WriteLine("Acme Manager:        manager / Manager@123");
                Console.WriteLine("Acme User:           user / User@123");
                Console.WriteLine("========================================");
                Console.WriteLine("WARNING: change these passwords after first login!");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
                throw;
            }
        }
    }
}
