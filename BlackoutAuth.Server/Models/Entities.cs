using System;
using System.ComponentModel.DataAnnotations;

namespace BlackoutAuth.Server.Models
{
    public class AdminUser
    {
        [Key]
        public int Id { get; set; }
        public string Username { get; set; } = "admin";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
    }

    public class Application
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string Secret { get; set; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        public string Version { get; set; } = "1.0";
        public bool IsEnabled { get; set; } = true;
        public bool HwidLockEnabled { get; set; } = true;
        public string? DownloadUrl { get; set; }
        public string? DiscordWebhookUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LicenseKey
    {
        [Key]
        public string Key { get; set; } = "";
        public string AppId { get; set; } = "";
        public int DurationDays { get; set; } = 30; // 99999 = Lifetime
        public int Level { get; set; } = 1;
        public int MaxUses { get; set; } = 1; // Number of times this key can be redeemed
        public int CurrentUses { get; set; } = 0;
        public string Status { get; set; } = "Unused"; // Unused, Used, Banned, Paused
        public string? UsedByUsername { get; set; }
        public DateTime? UsedAt { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AppUser
    {
        [Key]
        public int Id { get; set; }
        public string AppId { get; set; } = "";
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string? Hwid { get; set; }
        public string? IpAddress { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int Level { get; set; } = 1;
        public int MaxLogins { get; set; } = 1; // How many users / devices can log in with this user
        public bool IsBanned { get; set; } = false;
        public string? BanReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
    }

    public class CloudVariable
    {
        [Key]
        public int Id { get; set; }
        public string AppId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public int MinLevel { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProtectedFile
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string AppId { get; set; } = "";
        public string FileName { get; set; } = "";
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public long FileSize { get; set; }
        public int MinLevel { get; set; } = 1;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    public class AppSession
    {
        [Key]
        public string SessionToken { get; set; } = Guid.NewGuid().ToString("N");
        public string AppId { get; set; } = "";
        public string? Username { get; set; }
        public string? Hwid { get; set; }
        public string? IpAddress { get; set; }
        public string EncryptionKey { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
        public bool IsValid { get; set; } = true;
    }

    public class SecurityLog
    {
        [Key]
        public int Id { get; set; }
        public string AppId { get; set; } = "";
        public string? Username { get; set; }
        public string? IpAddress { get; set; }
        public string Action { get; set; } = ""; // INIT, LOGIN_SUCCESS, LOGIN_FAIL, HWID_MISMATCH, BAN, TAMPER
        public string Details { get; set; } = "";
        public string Severity { get; set; } = "INFO"; // INFO, WARN, DANGER
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
