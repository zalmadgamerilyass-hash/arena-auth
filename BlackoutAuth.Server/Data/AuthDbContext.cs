using Microsoft.EntityFrameworkCore;
using BlackoutAuth.Server.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlackoutAuth.Server.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<AdminUser> Admins => Set<AdminUser>();
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<LicenseKey> LicenseKeys => Set<LicenseKey>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<CloudVariable> CloudVariables => Set<CloudVariable>();
        public DbSet<ProtectedFile> ProtectedFiles => Set<ProtectedFile>();
        public DbSet<AppSession> Sessions => Set<AppSession>();
        public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Application>()
                .HasIndex(a => a.Name);

            modelBuilder.Entity<LicenseKey>()
                .HasIndex(k => k.AppId);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => new { u.AppId, u.Username })
                .IsUnique();

            modelBuilder.Entity<AppSession>()
                .HasIndex(s => s.SessionToken)
                .IsUnique();

            modelBuilder.Entity<SecurityLog>()
                .HasIndex(l => l.AppId);
        }

        public static void InitializeSeedData(AuthDbContext context)
        {
            context.Database.EnsureCreated();

            // Seed default admin user
            if (!context.Admins.Any())
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword("blackout1337", salt);

                context.Admins.Add(new AdminUser
                {
                    Username = "admin",
                    PasswordHash = hash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Seed default application (BlackoutApp) that SDKs connect to
            if (!context.Applications.Any(a => a.Name == "BlackoutApp"))
            {
                var app = new Application
                {
                    Name = "BlackoutApp",
                    OwnerId = "OWNER-001",
                    Secret = "blackout_secret_9999a8b7c6d5e4f321",
                    Version = "1.0",
                    IsEnabled = true,
                    HwidLockEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Applications.Add(app);
                context.SaveChanges(); // Save to get the App.Id

                // Seed a demo license key
                context.LicenseKeys.Add(new LicenseKey
                {
                    Key = "BLACKOUT-DEMO-30DAYS-1337",
                    AppId = app.Id,
                    DurationDays = 30,
                    Level = 1,
                    MaxUses = 1,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = "Demo license key for testing",
                    CreatedAt = DateTime.UtcNow
                });

                // Seed a demo user (username: demo, password: demo1234)
                string userSalt = Guid.NewGuid().ToString("N");
                string userHash = HashPassword("demo1234", userSalt);
                context.Users.Add(new AppUser
                {
                    AppId = app.Id,
                    Username = "demo",
                    PasswordHash = userHash + ":" + userSalt,
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    Level = 1,
                    MaxLogins = 1,
                    IsBanned = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                });

                // Seed a demo cloud variable
                context.CloudVariables.Add(new CloudVariable
                {
                    AppId = app.Id,
                    Name = "client_greeting",
                    Value = "Welcome to BLACKOUT AUTH! You are now authenticated.",
                    MinLevel = 1,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            context.SaveChanges();
        }

        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt + "BLACKOUT_SALT_KEY"));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
