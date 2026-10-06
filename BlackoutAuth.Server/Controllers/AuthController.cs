using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using BlackoutAuth.Server.Data;
using BlackoutAuth.Server.Models;
using BlackoutAuth.Server.Services;

namespace BlackoutAuth.Server.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthDbContext _db;

        public AuthController(AuthDbContext db)
        {
            _db = db;
        }

        public record InitRequest(string Name, string OwnerId, string Secret, string Version);
        public record RegisterRequest(string SessionToken, string Username, string Password, string LicenseKey, string? Hwid);
        public record LoginRequest(string SessionToken, string Username, string Password, string? Hwid);
        public record LicenseRequest(string SessionToken, string LicenseKey, string? Hwid);
        public record UpgradeRequest(string SessionToken, string Username, string LicenseKey);
        public record VarRequest(string SessionToken, string VarName);
        public record CheckRequest(string SessionToken);
        public record LogRequest(string SessionToken, string Message, string? Severity);

        [HttpPost("init")]
        public async Task<IActionResult> Init([FromBody] InitRequest req)
        {
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Name == req.Name && a.OwnerId == req.OwnerId && a.Secret == req.Secret);
            if (app == null)
            {
                return Ok(new { success = false, message = "Application not found or invalid credentials." });
            }

            if (!app.IsEnabled)
            {
                return Ok(new { success = false, message = "Application is currently disabled by administrator." });
            }

            if (app.Version != req.Version)
            {
                return Ok(new
                {
                    success = false,
                    update_required = true,
                    download_url = app.DownloadUrl,
                    message = $"Update required! Your version: {req.Version}, latest version: {app.Version}"
                });
            }

            // Create new session
            string sessionToken = Guid.NewGuid().ToString("N");
            string encryptionKey = CryptoService.GenerateRandomKey(16);

            var session = new AppSession
            {
                SessionToken = sessionToken,
                AppId = app.Id,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                EncryptionKey = encryptionKey,
                CreatedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                IsValid = true
            };

            _db.Sessions.Add(session);
            _db.SecurityLogs.Add(new SecurityLog
            {
                AppId = app.Id,
                Action = "INIT",
                Details = $"Client initialized session. Version: {req.Version}",
                Severity = "INFO",
                IpAddress = session.IpAddress,
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                session_token = sessionToken,
                encryption_key = encryptionKey,
                app_name = app.Name,
                app_version = app.Version,
                message = "Initialization successful."
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Invalid or expired session. Please re-initialize." });

            var app = await _db.Applications.FindAsync(session.AppId);
            if (app == null)
                return Ok(new { success = false, message = "Application context missing." });

            // Check if username already exists for this app
            var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.AppId == session.AppId && u.Username.ToLower() == req.Username.ToLower());
            if (existingUser != null)
                return Ok(new { success = false, message = "Username already taken." });

            // Validate license key
            var key = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.AppId == session.AppId && k.Key == req.LicenseKey);
            if (key == null)
                return Ok(new { success = false, message = "Invalid license key." });

            if (key.Status != "Unused")
                return Ok(new { success = false, message = $"License key is {key.Status.ToLowerInvariant()}." });

            // Calculate expiration
            DateTime expiry = key.DurationDays >= 99999 ? DateTime.UtcNow.AddYears(100) : DateTime.UtcNow.AddDays(key.DurationDays);

            string salt = Guid.NewGuid().ToString("N");
            string hash = AuthDbContext.HashPassword(req.Password, salt);

            var newUser = new AppUser
            {
                AppId = session.AppId,
                Username = req.Username,
                PasswordHash = hash + ":" + salt,
                Hwid = req.Hwid,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ExpiryDate = expiry,
                Level = key.Level,
                IsBanned = false,
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };

            key.CurrentUses++;
            if (key.CurrentUses >= (key.MaxUses <= 0 ? 1 : key.MaxUses))
            {
                key.Status = "Used";
            }
            key.UsedByUsername = string.IsNullOrEmpty(key.UsedByUsername) ? req.Username : $"{key.UsedByUsername}, {req.Username}";
            key.UsedAt = DateTime.UtcNow;

            session.Username = req.Username;
            session.Hwid = req.Hwid;

            _db.Users.Add(newUser);
            _db.SecurityLogs.Add(new SecurityLog
            {
                AppId = session.AppId,
                Username = req.Username,
                IpAddress = session.IpAddress,
                Action = "REGISTER",
                Details = $"New user registered with key {req.LicenseKey}. Expiry: {expiry:yyyy-MM-dd}",
                Severity = "INFO"
            });

            await _db.SaveChangesAsync();

            _ = DiscordService.SendAlertAsync(app.DiscordWebhookUrl, "New User Registered",
                $"**User:** `{req.Username}`\n**App:** `{app.Name}`\n**License:** `{req.LicenseKey}`\n**Duration:** `{key.DurationDays} days`\n**HWID:** `{req.Hwid ?? "N/A"}`", 0x10B981);

            return Ok(new
            {
                success = true,
                message = "Registration successful!",
                user = new
                {
                    username = newUser.Username,
                    expiry = newUser.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    level = newUser.Level,
                    hwid = newUser.Hwid
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Invalid or expired session." });

            var app = await _db.Applications.FindAsync(session.AppId);
            if (app == null)
                return Ok(new { success = false, message = "Application context missing." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.AppId == session.AppId && u.Username.ToLower() == req.Username.ToLower());
            if (user == null)
            {
                await LogSecurityAsync(session.AppId, req.Username, "LOGIN_FAIL", "Invalid username", "WARN");
                return Ok(new { success = false, message = "Invalid username or password." });
            }

            if (user.IsBanned)
            {
                await LogSecurityAsync(session.AppId, req.Username, "BANNED_LOGIN", $"Banned user attempted login: {user.BanReason}", "DANGER");
                return Ok(new { success = false, message = $"Account is banned. Reason: {user.BanReason ?? "Security violation"}" });
            }

            // Verify password
            var parts = user.PasswordHash.Split(':');
            if (parts.Length != 2 || AuthDbContext.HashPassword(req.Password, parts[1]) != parts[0])
            {
                await LogSecurityAsync(session.AppId, req.Username, "LOGIN_FAIL", "Invalid password", "WARN");
                return Ok(new { success = false, message = "Invalid username or password." });
            }

            // Expiry check
            if (DateTime.UtcNow > user.ExpiryDate)
            {
                return Ok(new { success = false, message = $"Your subscription expired on {user.ExpiryDate:yyyy-MM-dd}." });
            }

            // HWID Lock Check with multi-device / MaxLogins support
            if (app.HwidLockEnabled && !string.IsNullOrEmpty(req.Hwid))
            {
                var boundHwids = string.IsNullOrEmpty(user.Hwid)
                    ? new List<string>()
                    : user.Hwid.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                if (!boundHwids.Contains(req.Hwid, StringComparer.OrdinalIgnoreCase))
                {
                    if (boundHwids.Count < (user.MaxLogins <= 0 ? 1 : user.MaxLogins))
                    {
                        boundHwids.Add(req.Hwid);
                        user.Hwid = string.Join(",", boundHwids);
                    }
                    else
                    {
                        await LogSecurityAsync(session.AppId, req.Username, "HWID_MISMATCH", $"Device limit reached ({boundHwids.Count}/{user.MaxLogins}). Current HWID: {req.Hwid}", "DANGER");
                        _ = DiscordService.SendAlertAsync(app.DiscordWebhookUrl, "Device Limit Exceeded",
                            $"**User:** `{user.Username}`\n**Allowed Devices:** `{user.MaxLogins}`\n**Blocked HWID:** `{req.Hwid}`", 0xEF4444);

                        return Ok(new { success = false, message = $"Device limit reached! Allowed devices: {user.MaxLogins}. Contact admin to reset." });
                    }
                }
            }

            user.LastLogin = DateTime.UtcNow;
            user.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            session.Username = user.Username;
            session.Hwid = req.Hwid;

            await _db.SaveChangesAsync();

            await LogSecurityAsync(session.AppId, req.Username, "LOGIN_SUCCESS", "User successfully authenticated.", "INFO");

            return Ok(new
            {
                success = true,
                message = "Login successful!",
                user = new
                {
                    username = user.Username,
                    expiry = user.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    level = user.Level,
                    hwid = user.Hwid
                }
            });
        }

        [HttpPost("license")]
        public async Task<IActionResult> License([FromBody] LicenseRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Invalid or expired session." });

            var app = await _db.Applications.FindAsync(session.AppId);
            if (app == null)
                return Ok(new { success = false, message = "Application context missing." });

            var key = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.AppId == session.AppId && k.Key == req.LicenseKey);
            if (key == null)
                return Ok(new { success = false, message = "Invalid license key." });

            if (key.Status == "Banned")
                return Ok(new { success = false, message = "This license key is banned." });

            if (key.Status == "Paused")
                return Ok(new { success = false, message = "This license key is currently paused." });

            // If key is Unused, bind it to a synthesized key user
            if (key.Status == "Unused")
            {
                DateTime expiry = key.DurationDays >= 99999 ? DateTime.UtcNow.AddYears(100) : DateTime.UtcNow.AddDays(key.DurationDays);

                var keyUser = new AppUser
                {
                    AppId = session.AppId,
                    Username = key.Key,
                    PasswordHash = "KEY_ONLY:" + Guid.NewGuid().ToString("N"),
                    Hwid = req.Hwid,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ExpiryDate = expiry,
                    Level = key.Level,
                    IsBanned = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };

                key.Status = "Used";
                key.UsedByUsername = key.Key;
                key.UsedAt = DateTime.UtcNow;

                session.Username = key.Key;
                session.Hwid = req.Hwid;

                _db.Users.Add(keyUser);
                await _db.SaveChangesAsync();

                _ = DiscordService.SendAlertAsync(app.DiscordWebhookUrl, "License Activated (Key-Only)",
                    $"**License:** `{key.Key}`\n**Duration:** `{key.DurationDays} days`\n**HWID:** `{req.Hwid ?? "N/A"}`", 0x10B981);

                return Ok(new
                {
                    success = true,
                    message = "License successfully activated!",
                    user = new
                    {
                        username = keyUser.Username,
                        expiry = keyUser.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        level = keyUser.Level,
                        hwid = keyUser.Hwid
                    }
                });
            }

            // If already used, find bound user
            var user = await _db.Users.FirstOrDefaultAsync(u => u.AppId == session.AppId && u.Username == key.UsedByUsername);
            if (user == null)
            {
                return Ok(new { success = false, message = "User bound to license not found." });
            }

            if (user.IsBanned)
                return Ok(new { success = false, message = "Associated account is banned." });

            if (DateTime.UtcNow > user.ExpiryDate)
                return Ok(new { success = false, message = $"Subscription expired on {user.ExpiryDate:yyyy-MM-dd}." });

            if (app.HwidLockEnabled)
            {
                if (string.IsNullOrEmpty(user.Hwid))
                {
                    user.Hwid = req.Hwid;
                }
                else if (!string.IsNullOrEmpty(req.Hwid) && !string.Equals(user.Hwid, req.Hwid, StringComparison.OrdinalIgnoreCase))
                {
                    await LogSecurityAsync(session.AppId, user.Username, "HWID_MISMATCH", $"HWID mismatch for license {key.Key}", "DANGER");
                    return Ok(new { success = false, message = "HWID mismatch for this license." });
                }
            }

            user.LastLogin = DateTime.UtcNow;
            session.Username = user.Username;
            session.Hwid = req.Hwid;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "License authenticated successfully!",
                user = new
                {
                    username = user.Username,
                    expiry = user.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    level = user.Level,
                    hwid = user.Hwid
                }
            });
        }

        [HttpPost("upgrade")]
        public async Task<IActionResult> Upgrade([FromBody] UpgradeRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Invalid or expired session." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.AppId == session.AppId && u.Username.ToLower() == req.Username.ToLower());
            if (user == null)
                return Ok(new { success = false, message = "User not found." });

            var key = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.AppId == session.AppId && k.Key == req.LicenseKey);
            if (key == null)
                return Ok(new { success = false, message = "Invalid license key." });

            if (key.Status != "Unused")
                return Ok(new { success = false, message = "License key already used." });

            // Extend expiry
            DateTime baseDate = user.ExpiryDate > DateTime.UtcNow ? user.ExpiryDate : DateTime.UtcNow;
            user.ExpiryDate = key.DurationDays >= 99999 ? DateTime.UtcNow.AddYears(100) : baseDate.AddDays(key.DurationDays);

            if (key.Level > user.Level)
                user.Level = key.Level;

            key.Status = "Used";
            key.UsedByUsername = user.Username;
            key.UsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Successfully extended subscription by {key.DurationDays} days!",
                new_expiry = user.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"),
                level = user.Level
            });
        }

        [HttpPost("var")]
        public async Task<IActionResult> GetVar([FromBody] VarRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Invalid session." });

            if (string.IsNullOrEmpty(session.Username))
                return Ok(new { success = false, message = "You must be logged in to fetch variables." });

            var variable = await _db.CloudVariables.FirstOrDefaultAsync(v => v.AppId == session.AppId && v.Name == req.VarName);
            if (variable == null)
                return Ok(new { success = false, message = "Variable not found." });

            // Encrypt response value with session encryption key for tamper resistance
            string encryptedValue = CryptoService.EncryptAes256(variable.Value, session.EncryptionKey);

            return Ok(new
            {
                success = true,
                name = variable.Name,
                encrypted = true,
                value = encryptedValue
            });
        }

        [HttpGet("file/{fileId}")]
        public async Task<IActionResult> DownloadFile(string fileId, [FromQuery] string sessionToken)
        {
            var session = await ValidateSessionAsync(sessionToken);
            if (session == null || string.IsNullOrEmpty(session.Username))
                return Unauthorized(new { success = false, message = "Valid authenticated session required." });

            var file = await _db.ProtectedFiles.FirstOrDefaultAsync(f => f.AppId == session.AppId && f.Id == fileId);
            if (file == null)
                return NotFound(new { success = false, message = "File not found." });

            return File(file.FileContent, "application/octet-stream", file.FileName);
        }

        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] CheckRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Session invalid or expired." });

            session.LastHeartbeat = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Session alive." });
        }

        [HttpPost("log")]
        public async Task<IActionResult> LogEvent([FromBody] LogRequest req)
        {
            var session = await ValidateSessionAsync(req.SessionToken);
            if (session == null)
                return Ok(new { success = false, message = "Invalid session." });

            await LogSecurityAsync(session.AppId, session.Username, "CLIENT_LOG", req.Message, req.Severity ?? "INFO");

            return Ok(new { success = true });
        }

        private async Task<AppSession?> ValidateSessionAsync(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.SessionToken == token && s.IsValid);
            if (session == null) return null;

            // Session timeout after 6 hours of inactivity
            if (DateTime.UtcNow - session.LastHeartbeat > TimeSpan.FromHours(6))
            {
                session.IsValid = false;
                await _db.SaveChangesAsync();
                return null;
            }

            return session;
        }

        private async Task LogSecurityAsync(string appId, string? username, string action, string details, string severity)
        {
            _db.SecurityLogs.Add(new SecurityLog
            {
                AppId = appId,
                Username = username,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Action = action,
                Details = details,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
