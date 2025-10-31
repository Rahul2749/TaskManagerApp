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
                // Apply any pending migrations
                await context.Database.MigrateAsync();

                // Check if we already have users
                if (await context.Users.AnyAsync())
                {
                    return; // DB has been seeded
                }

                // Create default admin user
                var adminUser = new User
                {
                    Username = "admin",
                    Email = "admin@taskmanager.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    FirstName = "System",
                    LastName = "Administrator",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                Console.WriteLine("========================================");
                Console.WriteLine("Database initialized successfully!");
                Console.WriteLine("========================================");
                Console.WriteLine("Default Admin Account:");
                Console.WriteLine("Username: admin");
                Console.WriteLine("Password: Admin@123");
                Console.WriteLine("========================================");
                Console.WriteLine("⚠️  IMPORTANT: Change this password after first login!");
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
