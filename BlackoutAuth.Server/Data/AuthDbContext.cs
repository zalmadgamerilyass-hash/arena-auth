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

            // --- Seed 3 Admin Users ---
            // SINZO (password: 111)
            if (!context.Admins.Any(a => a.Username == "SINZO"))
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword("111", salt);
                context.Admins.Add(new AdminUser
                {
                    Username = "SINZO",
                    PasswordHash = hash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // MARCHAL (password: 222)
            if (!context.Admins.Any(a => a.Username == "MARCHAL"))
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword("222", salt);
                context.Admins.Add(new AdminUser
                {
                    Username = "MARCHAL",
                    PasswordHash = hash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // VINI (password: 1949) - Master Admin
            if (!context.Admins.Any(a => a.Username == "VINI"))
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword("1949", salt);
                context.Admins.Add(new AdminUser
                {
                    Username = "VINI",
                    PasswordHash = hash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // DEXTER (password: 2000)
            if (!context.Admins.Any(a => a.Username == "DEXTER"))
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword("2000", salt);
                context.Admins.Add(new AdminUser
                {
                    Username = "DEXTER",
                    PasswordHash = hash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow
                });
            }


            // DEXTER (password: 2000)
            if (!context.Admins.Any(a => a.Username == "KENZO"))
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword("121", salt);
                context.Admins.Add(new AdminUser
                {
                    Username = "DEXTER",
                    PasswordHash = hash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow
                });
            }
            context.SaveChanges(); // Save admins first to get their IDs/names

            // --- Seed Separate Applications for Each Admin ---
            // 1. DEXTER's Application
            if (!context.Applications.Any(a => a.AdminUsername == "DEXTER"))
            {
                var dexterApp = new Application
                {
                    Name = "BlackoutApp",
                    OwnerId = "OWNER-DEXTER",
                    AdminUsername = "DEXTER",
                    Secret = "blackout_secret_9999a8b7c6d5e4f321_Dexter", // Unique secret per admin
                    Version = "1.0",
                    IsEnabled = true,
                    HwidLockEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Applications.Add(dexterApp);
                context.SaveChanges(); // Save to get App.Id

                // Dexters demo license key
                context.LicenseKeys.Add(new LicenseKey
                {
                    Key = "BLACKOUT-DEXTER-30DAYS-1337",
                    AppId = dexterApp.Id,
                    DurationDays = 30,
                    Level = 1,
                    MaxUses = 1,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = "Demo license key for DEXTER",
                    CreatedAt = DateTime.UtcNow
                });

                // Dexters demo user
                string userSalt = Guid.NewGuid().ToString("N");
                string userHash = HashPassword("demo1234", userSalt);
                context.Users.Add(new AppUser
                {
                    AppId = dexterApp.Id,
                    Username = "demo",
                    PasswordHash = userHash + ":" + userSalt,
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    Level = 1,
                    MaxLogins = 1,
                    IsBanned = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                });

                // Dexters cloud variable
                context.CloudVariables.Add(new CloudVariable
                {
                    AppId = dexterApp.Id,
                    Name = "client_greeting",
                    Value = "Welcome DEXTER! Your app is isolated.",
                    MinLevel = 1,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // 2. SINZO's Application
            if (!context.Applications.Any(a => a.AdminUsername == "SINZO"))
            {
                var sinzoApp = new Application
                {
                    Name = "BlackoutApp",
                    OwnerId = "OWNER-SINZO",
                    AdminUsername = "SINZO",
                    Secret = "blackout_secret_9999a8b7c6d5e4f321_Sinzo", // Unique secret per admin
                    Version = "1.0",
                    IsEnabled = true,
                    HwidLockEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Applications.Add(sinzoApp);
                context.SaveChanges();

                // Sinzo's demo license key
                context.LicenseKeys.Add(new LicenseKey
                {
                    Key = "BLACKOUT-SINZO-30DAYS-1337",
                    AppId = sinzoApp.Id,
                    DurationDays = 30,
                    Level = 1,
                    MaxUses = 1,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = "Demo license key for SINZO",
                    CreatedAt = DateTime.UtcNow
                });

                // Sinzo's demo user
                string userSalt = Guid.NewGuid().ToString("N");
                string userHash = HashPassword("demo1234", userSalt);
                context.Users.Add(new AppUser
                {
                    AppId = sinzoApp.Id,
                    Username = "demo",
                    PasswordHash = userHash + ":" + userSalt,
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    Level = 1,
                    MaxLogins = 1,
                    IsBanned = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                });

                // Sinzo's cloud variable
                context.CloudVariables.Add(new CloudVariable
                {
                    AppId = sinzoApp.Id,
                    Name = "client_greeting",
                    Value = "Welcome SINZOR! Your app is isolated.",
                    MinLevel = 1,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // 4. VINI's Application (Master Admin)
            if (!context.Applications.Any(a => a.AdminUsername == "VINI"))
            {
                var viniApp = new Application
                {
                    Name = "ArenaApp",
                    OwnerId = "OWNER-VINI",
                    AdminUsername = "VINI",
                    Secret = "vini_secret_9999a8b7c6d5e4f321_Vini",
                    Version = "1.0",
                    IsEnabled = true,
                    HwidLockEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Applications.Add(viniApp);
                context.SaveChanges();

                context.LicenseKeys.Add(new LicenseKey
                {
                    Key = "ARENA-VINI-LIFETIME-1949",
                    AppId = viniApp.Id,
                    DurationDays = 99999,
                    Level = 1,
                    MaxUses = 1,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = "Master Key for VINI",
                    CreatedAt = DateTime.UtcNow
                });

                string userSalt = Guid.NewGuid().ToString("N");
                string userHash = HashPassword("1949", userSalt);
                context.Users.Add(new AppUser
                {
                    AppId = viniApp.Id,
                    Username = "vini",
                    PasswordHash = userHash + ":" + userSalt,
                    ExpiryDate = DateTime.UtcNow.AddYears(10),
                    Level = 1,
                    MaxLogins = 5,
                    IsBanned = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                });

                context.CloudVariables.Add(new CloudVariable
                {
                    AppId = viniApp.Id,
                    Name = "client_greeting",
                    Value = "Welcome VINI! Full SuperAdmin access enabled.",
                    MinLevel = 1,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // 3. MARCHAL's Application
            if (!context.Applications.Any(a => a.AdminUsername == "MARCHAL"))
            {
                var marchalApp = new Application
                {
                    Name = "BlackoutApp",
                    OwnerId = "OWNER-MARCHAL",
                    AdminUsername = "MARCHAL",
                    Secret = "blackout_secret_9999a8b7c6d5e4f321_Marchal", // Unique secret per admin
                    Version = "1.0",
                    IsEnabled = true,
                    HwidLockEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Applications.Add(marchalApp);
                context.SaveChanges();

                // Marchal's demo license key
                context.LicenseKeys.Add(new LicenseKey
                {
                    Key = "BLACKOUT-MARCHAL-30DAYS-1337",
                    AppId = marchalApp.Id,
                    DurationDays = 30,
                    Level = 1,
                    MaxUses = 1,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = "Demo license key for MARCHAL",
                    CreatedAt = DateTime.UtcNow
                });

                // Marchal's demo user
                string userSalt = Guid.NewGuid().ToString("N");
                string userHash = HashPassword("demo1234", userSalt);
                context.Users.Add(new AppUser
                {
                    AppId = marchalApp.Id,
                    Username = "demo",
                    PasswordHash = userHash + ":" + userSalt,
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    Level = 1,
                    MaxLogins = 1,
                    IsBanned = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                });

                // Marchal's cloud variable
                context.CloudVariables.Add(new CloudVariable
                {
                    AppId = marchalApp.Id,
                    Name = "client_greeting",
                    Value = "Welcome MARCHAL! Your app is isolated.",
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
