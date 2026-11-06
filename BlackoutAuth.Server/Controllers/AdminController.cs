using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using BlackoutAuth.Server.Data;
using BlackoutAuth.Server.Models;
using BlackoutAuth.Server.Services;

namespace BlackoutAuth.Server.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly AuthDbContext _db;

        // Maps token -> (username, expiry)
        private static readonly Dictionary<string, (string Username, DateTime Expiry)> _adminSessions = new();

        // Rate limiting: maps IP -> block-until time
        private static readonly Dictionary<string, DateTime> _failedAdminLogins = new();

        // Maps username -> active token (for 1-session-per-user enforcement)
        private static readonly Dictionary<string, string> _userActiveToken = new();

        public AdminController(AuthDbContext db)
        {
            _db = db;
        }

        public record AdminLoginRequest(string Username, string Password);
        public record AppCreateRequest(string Name, string OwnerId, string Version, bool HwidLockEnabled, string? DownloadUrl, string? DiscordWebhookUrl);
        public record AppUpdateRequest(string Name, string Version, bool IsEnabled, bool HwidLockEnabled, string? DownloadUrl, string? DiscordWebhookUrl);
        public record KeyGenerateRequest(string AppId, int Count, int DurationDays, int Level, string? Prefix, string? Note, int MaxUses);
        public record UserCreateRequest(string AppId, string Username, string Password, int DurationDays, int Level, int MaxLogins);
        public record UserBanRequest(bool Banned, string? Reason);
        public record UserExtendRequest(int Days);
        public record VariableRequest(string AppId, string Name, string Value, int MinLevel);
        public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

        /// <summary>
        /// Resolves the admin username for the current request from the X-Admin-Token header.
        /// Returns null if the token is missing or expired.
        /// </summary>
        private string? GetCurrentAdminUsername()
        {
            string? token = Request.Headers["X-Admin-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token)) return null;

            lock (_adminSessions)
            {
                if (_adminSessions.TryGetValue(token, out var entry))
                {
                    if (DateTime.UtcNow < entry.Expiry)
                        return entry.Username;

                    // Expired — clean up
                    _adminSessions.Remove(token);
                }
            }
            return null;
        }

        private bool IsAdminAuthenticated() => GetCurrentAdminUsername() != null;

        private IQueryable<Application> ApplyAdminFilter(IQueryable<Application> query)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (!string.IsNullOrEmpty(adminUser))
                return query.Where(a => a.AdminUsername == adminUser);
            return query;
        }

        private IQueryable<LicenseKey> ApplyAdminFilter(IQueryable<LicenseKey> query)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (!string.IsNullOrEmpty(adminUser))
            {
                var ownedAppIds = _db.Applications
                    .Where(a => a.AdminUsername == adminUser)
                    .Select(a => a.Id);
                return query.Where(k => ownedAppIds.Contains(k.AppId));
            }
            return query;
        }

        private IQueryable<AppUser> ApplyAdminFilter(IQueryable<AppUser> query)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (!string.IsNullOrEmpty(adminUser))
            {
                var ownedAppIds = _db.Applications
                    .Where(a => a.AdminUsername == adminUser)
                    .Select(a => a.Id);
                return query.Where(u => ownedAppIds.Contains(u.AppId));
            }
            return query;
        }

        private IQueryable<CloudVariable> ApplyAdminFilter(IQueryable<CloudVariable> query)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (!string.IsNullOrEmpty(adminUser))
            {
                var ownedAppIds = _db.Applications
                    .Where(a => a.AdminUsername == adminUser)
                    .Select(a => a.Id);
                return query.Where(v => ownedAppIds.Contains(v.AppId));
            }
            return query;
        }

        private IQueryable<ProtectedFile> ApplyAdminFilter(IQueryable<ProtectedFile> query)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (!string.IsNullOrEmpty(adminUser))
            {
                var ownedAppIds = _db.Applications
                    .Where(a => a.AdminUsername == adminUser)
                    .Select(a => a.Id);
                return query.Where(f => ownedAppIds.Contains(f.AppId));
            }
            return query;
        }

        private IQueryable<SecurityLog> ApplyAdminFilter(IQueryable<SecurityLog> query)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (!string.IsNullOrEmpty(adminUser))
            {
                var ownedAppIds = _db.Applications
                    .Where(a => a.AdminUsername == adminUser)
                    .Select(a => a.Id);
                return query.Where(l => ownedAppIds.Contains(l.AppId));
            }
            return query;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AdminLoginRequest req)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Rate limiting: block IP after 5 failed attempts for 15 minutes
            lock (_failedAdminLogins)
            {
                if (_failedAdminLogins.TryGetValue(ipAddress, out var blockTime))
                {
                    if (DateTime.UtcNow < blockTime)
                    {
                        int remainingMinutes = (int)Math.Ceiling((blockTime - DateTime.UtcNow).TotalMinutes);
                        return Unauthorized(new { success = false, message = $"Too many failed attempts. Try again in {remainingMinutes} minute(s)." });
                    }
                    else
                    {
                        _failedAdminLogins.Remove(ipAddress);
                    }
                }
            }

            var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username.ToLower() == req.Username.ToLower());
            if (admin == null)
            {
                lock (_failedAdminLogins)
                {
                    _failedAdminLogins[ipAddress] = DateTime.UtcNow.AddMinutes(15);
                }
                return Unauthorized(new { success = false, message = "Invalid credentials." });
            }

            string hash = AuthDbContext.HashPassword(req.Password, admin.Salt);
            if (hash != admin.PasswordHash)
            {
                lock (_failedAdminLogins)
                {
                    _failedAdminLogins[ipAddress] = DateTime.UtcNow.AddMinutes(15);
                }
                return Unauthorized(new { success = false, message = "Invalid credentials." });
            }

            // Clear failed attempts on successful login
            lock (_failedAdminLogins)
            {
                _failedAdminLogins.Remove(ipAddress);
            }

            string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            lock (_adminSessions)
            {
                // Enforce max 1 active session per user: revoke any existing session for this admin
                if (_userActiveToken.TryGetValue(admin.Username, out string? oldToken))
                {
                    _adminSessions.Remove(oldToken);
                }

                _adminSessions[token] = (admin.Username, DateTime.UtcNow.AddHours(12));
                _userActiveToken[admin.Username] = token;
            }

            admin.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, token, username = admin.Username });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == adminUser);
            if (admin == null) return NotFound();

            string currentHash = AuthDbContext.HashPassword(req.CurrentPassword, admin.Salt);
            if (currentHash != admin.PasswordHash)
                return BadRequest(new { success = false, message = "Current password is wrong." });

            string newSalt = Guid.NewGuid().ToString("N");
            admin.Salt = newSalt;
            admin.PasswordHash = AuthDbContext.HashPassword(req.NewPassword, newSalt);

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Password updated successfully." });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats([FromQuery] string? appId)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var userQuery = ApplyAdminFilter(_db.Users.AsQueryable());
            var keyQuery = ApplyAdminFilter(_db.LicenseKeys.AsQueryable());
            var logQuery = ApplyAdminFilter(_db.SecurityLogs.AsQueryable());

            if (!string.IsNullOrEmpty(appId))
            {
                userQuery = userQuery.Where(u => u.AppId == appId);
                keyQuery = keyQuery.Where(k => k.AppId == appId);
                logQuery = logQuery.Where(l => l.AppId == appId);
            }

            int totalUsers = await userQuery.CountAsync();
            int totalKeys = await keyQuery.CountAsync();
            int unusedKeys = await keyQuery.CountAsync(k => k.Status == "Unused");
            int bannedUsers = await userQuery.CountAsync(u => u.IsBanned);
            int totalApps = await ApplyAdminFilter(_db.Applications.AsQueryable()).CountAsync();

            var todayUtc = DateTime.UtcNow.Date;
            int loginsToday = await logQuery.CountAsync(l => l.Action == "LOGIN_SUCCESS" && l.Timestamp >= todayUtc);
            int securityAlerts = await logQuery.CountAsync(l => l.Severity == "DANGER" || l.Severity == "WARN");

            // 7-day auth activity for chart
            var chartData = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var day = todayUtc.AddDays(-i);
                var nextDay = day.AddDays(1);

                int successCount = await logQuery.CountAsync(l => l.Action == "LOGIN_SUCCESS" && l.Timestamp >= day && l.Timestamp < nextDay);
                int failCount = await logQuery.CountAsync(l => (l.Action == "LOGIN_FAIL" || l.Action == "HWID_MISMATCH") && l.Timestamp >= day && l.Timestamp < nextDay);

                chartData.Add(new
                {
                    date = day.ToString("MMM dd"),
                    success = successCount,
                    failed = failCount
                });
            }

            return Ok(new
            {
                success = true,
                stats = new
                {
                    totalUsers,
                    totalKeys,
                    unusedKeys,
                    bannedUsers,
                    totalApps,
                    loginsToday,
                    securityAlerts
                },
                chart = chartData
            });
        }

        // ================= APPLICATIONS =================
        [HttpGet("apps")]
        public async Task<IActionResult> GetApps()
        {
            if (!IsAdminAuthenticated()) return Unauthorized();
            var apps = await ApplyAdminFilter(_db.Applications).OrderByDescending(a => a.CreatedAt).ToListAsync();
            return Ok(new { success = true, apps });
        }

        [HttpPost("apps")]
        public async Task<IActionResult> CreateApp([FromBody] AppCreateRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var app = new Application
            {
                Name = req.Name,
                OwnerId = string.IsNullOrWhiteSpace(req.OwnerId) ? "OWNER-" + CryptoService.GenerateRandomKey(4).ToUpperInvariant() : req.OwnerId,
                Version = string.IsNullOrWhiteSpace(req.Version) ? "1.0" : req.Version,
                HwidLockEnabled = req.HwidLockEnabled,
                DownloadUrl = req.DownloadUrl,
                DiscordWebhookUrl = req.DiscordWebhookUrl,
                AdminUsername = adminUser // Assign app to the currently authenticated admin
            };

            _db.Applications.Add(app);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, app, message = "Application created successfully." });
        }

        [HttpPut("apps/{id}")]
        public async Task<IActionResult> UpdateApp(string id, [FromBody] AppUpdateRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            app.Name = req.Name;
            app.Version = req.Version;
            app.IsEnabled = req.IsEnabled;
            app.HwidLockEnabled = req.HwidLockEnabled;
            app.DownloadUrl = req.DownloadUrl;
            app.DiscordWebhookUrl = req.DiscordWebhookUrl;

            await _db.SaveChangesAsync();
            return Ok(new { success = true, app, message = "Application updated." });
        }

        [HttpPost("apps/{id}/regen-secret")]
        public async Task<IActionResult> RegenerateSecret(string id)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            app.Secret = CryptoService.GenerateRandomKey(32);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, new_secret = app.Secret });
        }

        [HttpDelete("apps/{id}")]
        public async Task<IActionResult> DeleteApp(string id)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            // Cascade delete app artifacts
            var keys = await _db.LicenseKeys.Where(k => k.AppId == id).ToListAsync();
            var users = await _db.Users.Where(u => u.AppId == id).ToListAsync();
            var vars = await _db.CloudVariables.Where(v => v.AppId == id).ToListAsync();
            var logs = await _db.SecurityLogs.Where(l => l.AppId == id).ToListAsync();

            _db.LicenseKeys.RemoveRange(keys);
            _db.Users.RemoveRange(users);
            _db.CloudVariables.RemoveRange(vars);
            _db.SecurityLogs.RemoveRange(logs);
            _db.Applications.Remove(app);

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Application and all associated data deleted." });
        }

        // ================= LICENSE KEYS =================
        [HttpGet("keys")]
        public async Task<IActionResult> GetKeys([FromQuery] string? appId, [FromQuery] string? search)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var query = ApplyAdminFilter(_db.LicenseKeys.AsQueryable());
            if (!string.IsNullOrEmpty(appId))
                query = query.Where(k => k.AppId == appId);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(k => k.Key.Contains(search) || (k.UsedByUsername != null && k.UsedByUsername.Contains(search)));

            var keys = await query.OrderByDescending(k => k.CreatedAt).Take(200).ToListAsync();
            return Ok(new { success = true, keys });
        }

        [HttpPost("keys/generate")]
        public async Task<IActionResult> GenerateKeys([FromBody] KeyGenerateRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            // Verify app exists and belongs to current admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == req.AppId && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            int count = Math.Clamp(req.Count, 1, 100);
            var generatedKeys = new List<LicenseKey>();
            string prefix = string.IsNullOrWhiteSpace(req.Prefix) ? "BLACKOUT" : req.Prefix.Trim().ToUpperInvariant();

            for (int i = 0; i < count; i++)
            {
                var license = new LicenseKey
                {
                    Key = CryptoService.GenerateLicenseKey(prefix),
                    AppId = req.AppId,
                    DurationDays = req.DurationDays <= 0 ? 30 : req.DurationDays,
                    Level = req.Level <= 0 ? 1 : req.Level,
                    MaxUses = req.MaxUses <= 0 ? 1 : req.MaxUses,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = req.Note,
                    CreatedAt = DateTime.UtcNow
                };
                generatedKeys.Add(license);
            }

            _db.LicenseKeys.AddRange(generatedKeys);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, keys = generatedKeys.Select(k => k.Key).ToList(), count });
        }

        [HttpDelete("keys/{key}")]
        public async Task<IActionResult> DeleteKey(string key)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var item = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key);
            if (item == null) return NotFound();

            // Ensure the key belongs to an app owned by this admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == item.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            _db.LicenseKeys.Remove(item);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Key deleted." });
        }

        [HttpPost("keys/{key}/toggle-status")]
        public async Task<IActionResult> ToggleKeyStatus(string key)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var item = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key);
            if (item == null) return NotFound();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == item.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            item.Status = item.Status == "Paused" ? "Unused" : "Paused";
            await _db.SaveChangesAsync();

            return Ok(new { success = true, status = item.Status });
        }

        // ================= USERS =================
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? appId, [FromQuery] string? search)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var userQuery = ApplyAdminFilter(_db.Users.AsQueryable());
            if (!string.IsNullOrEmpty(appId))
                userQuery = userQuery.Where(u => u.AppId == appId);

            if (!string.IsNullOrEmpty(search))
                userQuery = userQuery.Where(u => u.Username.Contains(search) || (u.Hwid != null && u.Hwid.Contains(search)));

            var users = await userQuery.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync();

            var sanitized = users.Select(u => new
            {
                u.Id,
                u.AppId,
                u.Username,
                u.Hwid,
                u.IpAddress,
                u.ExpiryDate,
                u.Level,
                u.MaxLogins,
                u.IsBanned,
                u.BanReason,
                u.CreatedAt,
                u.LastLogin,
                isExpired = DateTime.UtcNow > u.ExpiryDate
            });

            return Ok(new { success = true, users = sanitized });
        }

        [HttpPost("users/create")]
        public async Task<IActionResult> CreateUserDirect([FromBody] UserCreateRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            // Verify app exists and belongs to current admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == req.AppId && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            var existing = await _db.Users.FirstOrDefaultAsync(u => u.AppId == req.AppId && u.Username.ToLower() == req.Username.ToLower());
            if (existing != null)
                return BadRequest(new { success = false, message = "Username already exists for this application." });

            DateTime expiry = req.DurationDays >= 99999 ? DateTime.UtcNow.AddYears(100) : DateTime.UtcNow.AddDays(req.DurationDays <= 0 ? 30 : req.DurationDays);
            string salt = Guid.NewGuid().ToString("N");
            string hash = AuthDbContext.HashPassword(req.Password, salt);

            var newUser = new AppUser
            {
                AppId = req.AppId,
                Username = req.Username,
                PasswordHash = hash + ":" + salt,
                ExpiryDate = expiry,
                Level = req.Level <= 0 ? 1 : req.Level,
                MaxLogins = req.MaxLogins <= 0 ? 1 : req.MaxLogins,
                IsBanned = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            _db.SecurityLogs.Add(new SecurityLog
            {
                AppId = req.AppId,
                Username = req.Username,
                Action = "ADMIN_CREATE_USER",
                Details = $"Admin created user account. Expiry: {expiry:yyyy-MM-dd}, MaxLogins: {newUser.MaxLogins}",
                Severity = "INFO"
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = $"User {req.Username} created successfully!", user = newUser });
        }

        [HttpPost("users/{id}/reset-hwid")]
        public async Task<IActionResult> ResetHwid(int id)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Verify the user belongs to an app owned by this admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            string oldHwid = user.Hwid ?? "None";
            user.Hwid = null;

            _db.SecurityLogs.Add(new SecurityLog
            {
                AppId = user.AppId,
                Username = user.Username,
                Action = "HWID_RESET",
                Details = $"Admin reset HWID from {oldHwid}",
                Severity = "WARN",
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = $"HWID reset for user {user.Username}." });
        }

        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> BanUser(int id, [FromBody] UserBanRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            user.IsBanned = req.Banned;
            user.BanReason = req.Reason ?? (req.Banned ? "Banned by administrator" : null);

            _db.SecurityLogs.Add(new SecurityLog
            {
                AppId = user.AppId,
                Username = user.Username,
                Action = req.Banned ? "USER_BANNED" : "USER_UNBANNED",
                Details = req.Reason ?? (req.Banned ? "Banned" : "Unbanned"),
                Severity = req.Banned ? "DANGER" : "INFO",
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = req.Banned ? "User banned." : "User unbanned." });
        }

        [HttpPost("users/{id}/extend")]
        public async Task<IActionResult> ExtendUser(int id, [FromBody] UserExtendRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            var baseDate = user.ExpiryDate > DateTime.UtcNow ? user.ExpiryDate : DateTime.UtcNow;
            user.ExpiryDate = baseDate.AddDays(req.Days);

            await _db.SaveChangesAsync();
            return Ok(new { success = true, new_expiry = user.ExpiryDate, message = $"Extended by {req.Days} days." });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "User deleted." });
        }

        // ================= CLOUD VARIABLES =================
        [HttpGet("vars")]
        public async Task<IActionResult> GetVariables([FromQuery] string? appId)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var query = ApplyAdminFilter(_db.CloudVariables.AsQueryable());
            if (!string.IsNullOrEmpty(appId))
                query = query.Where(v => v.AppId == appId);

            var list = await query.ToListAsync();
            return Ok(new { success = true, variables = list });
        }

        [HttpPost("vars")]
        public async Task<IActionResult> SaveVariable([FromBody] VariableRequest req)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            // Verify app exists and belongs to current admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == req.AppId && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            var item = await _db.CloudVariables.FirstOrDefaultAsync(v => v.AppId == req.AppId && v.Name == req.Name);
            if (item == null)
            {
                item = new CloudVariable
                {
                    AppId = req.AppId,
                    Name = req.Name,
                    Value = req.Value,
                    MinLevel = req.MinLevel,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.CloudVariables.Add(item);
            }
            else
            {
                item.Value = req.Value;
                item.MinLevel = req.MinLevel;
                item.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, variable = item, message = "Variable saved." });
        }

        [HttpDelete("vars/{id}")]
        public async Task<IActionResult> DeleteVariable(int id)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var item = await _db.CloudVariables.FindAsync(id);
            if (item == null) return NotFound();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == item.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            _db.CloudVariables.Remove(item);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Variable deleted." });
        }

        // ================= PROTECTED FILES =================
        [HttpGet("files")]
        public async Task<IActionResult> GetFiles([FromQuery] string? appId)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var query = ApplyAdminFilter(_db.ProtectedFiles.AsQueryable());
            if (!string.IsNullOrEmpty(appId))
                query = query.Where(f => f.AppId == appId);

            var files = await query.Select(f => new
            {
                f.Id,
                f.AppId,
                f.FileName,
                f.FileSize,
                f.MinLevel,
                f.UploadedAt
            }).ToListAsync();

            return Ok(new { success = true, files });
        }

        [HttpPost("files/upload")]
        [RequestSizeLimit(50_000_000)] // 50MB
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string appId, [FromForm] int minLevel = 1)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            // Verify app exists and belongs to current admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == appId && a.AdminUsername == adminUser);
            if (app == null) return NotFound(new { success = false, message = "Application not found or access denied." });

            if (file == null || file.Length == 0) return BadRequest("File is empty.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var protectedFile = new ProtectedFile
            {
                AppId = appId,
                FileName = Path.GetFileName(file.FileName),
                FileContent = ms.ToArray(),
                FileSize = file.Length,
                MinLevel = minLevel,
                UploadedAt = DateTime.UtcNow
            };

            _db.ProtectedFiles.Add(protectedFile);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, fileId = protectedFile.Id, fileName = protectedFile.FileName });
        }

        [HttpDelete("files/{id}")]
        public async Task<IActionResult> DeleteFile(string id)
        {
            string? adminUser = GetCurrentAdminUsername();
            if (adminUser == null) return Unauthorized();

            var file = await _db.ProtectedFiles.FindAsync(id);
            if (file == null) return NotFound();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == file.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();

            _db.ProtectedFiles.Remove(file);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "File deleted." });
        }

        // ================= SECURITY LOGS =================
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs([FromQuery] string? appId, [FromQuery] string? severity)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var query = ApplyAdminFilter(_db.SecurityLogs.AsQueryable());
            if (!string.IsNullOrEmpty(appId))
                query = query.Where(l => l.AppId == appId);

            if (!string.IsNullOrEmpty(severity))
                query = query.Where(l => l.Severity == severity);

            var logs = await query.OrderByDescending(l => l.Timestamp).Take(100).ToListAsync();
            return Ok(new { success = true, logs });
        }

        [HttpDelete("logs/clear")]
        public async Task<IActionResult> ClearLogs([FromQuery] string? appId)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            var query = ApplyAdminFilter(_db.SecurityLogs.AsQueryable());
            if (!string.IsNullOrEmpty(appId))
                query = query.Where(l => l.AppId == appId);

            var logs = await query.ToListAsync();
            _db.SecurityLogs.RemoveRange(logs);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Logs cleared." });
        }

        [HttpPost("test-webhook")]
        public async Task<IActionResult> TestWebhook([FromBody] Dictionary<string, string> body)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            string url = body.GetValueOrDefault("url", "");
            await DiscordService.SendAlertAsync(url, "Webhook Test", "Blackout Auth Discord notification is working successfully!", 0x8B5CF6);
            return Ok(new { success = true, message = "Test notification sent." });
        }

        [HttpGet("download-sdk/{fileType}")]
        public IActionResult DownloadSdk(string fileType)
        {
            if (!IsAdminAuthenticated()) return Unauthorized();

            string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "downloads");
            string filePath = "";
            string contentType = "application/octet-stream";
            string downloadFileName = "";

            switch (fileType.ToLowerInvariant())
            {
                case "cpp":
                case "hpp":
                    filePath = Path.Combine(webRoot, "BlackoutAuth.hpp");
                    contentType = "text/plain";
                    downloadFileName = "BlackoutAuth.hpp";
                    break;
                case "csharp":
                case "cs":
                    filePath = Path.Combine(webRoot, "BlackoutApp.cs");
                    contentType = "text/plain";
                    downloadFileName = "BlackoutApp.cs";
                    break;
                case "dll":
                    filePath = Path.Combine(webRoot, "BlackoutAuth.SDK.CSharp.dll");
                    contentType = "application/octet-stream";
                    downloadFileName = "BlackoutAuth.SDK.CSharp.dll";
                    break;
                default:
                    return BadRequest("Invalid SDK file requested.");
            }

            if (!System.IO.File.Exists(filePath))
            {
                // Fallback search relative to solution root
                string altPath = fileType.ToLowerInvariant() switch
                {
                    "cpp" or "hpp" => Path.Combine(Directory.GetCurrentDirectory(), "..", "BlackoutAuth.SDK.Cpp", "include", "BlackoutAuth.hpp"),
                    "csharp" or "cs" => Path.Combine(Directory.GetCurrentDirectory(), "..", "BlackoutAuth.SDK.CSharp", "BlackoutApp.cs"),
                    "dll" => Path.Combine(Directory.GetCurrentDirectory(), "..", "BlackoutAuth.SDK.CSharp", "bin", "Release", "net9.0", "BlackoutAuth.SDK.CSharp.dll"),
                    _ => ""
                };

                if (!string.IsNullOrEmpty(altPath) && System.IO.File.Exists(altPath))
                {
                    filePath = altPath;
                }
                else
                {
                    return NotFound(new { success = false, message = "SDK file not found on server." });
                }
            }

            var bytes = System.IO.File.ReadAllBytes(filePath);
            return File(bytes, contentType, downloadFileName);
        }
    }
}
