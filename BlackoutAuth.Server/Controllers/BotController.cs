using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using BlackoutAuth.Server.Data;
using BlackoutAuth.Server.Models;
using BlackoutAuth.Server.Services;

namespace BlackoutAuth.Server.Controllers
{
    [ApiController]
    [Route("api/bot")]
    public class BotController : ControllerBase
    {
        private readonly AuthDbContext _db;
        private const string BotSecret = "zyrex-bot-9f4c2e7a1b8d6f3e5c0a7b4d2f8e1c6a";

        public BotController(AuthDbContext db)
        {
            _db = db;
        }

        private bool IsAuthorized()
        {
            if (Request.Headers.TryGetValue("x-bot-secret", out var secret) && secret == BotSecret)
                return true;

            var ip = HttpContext.Connection.RemoteIpAddress;
            if (ip != null && System.Net.IPAddress.IsLoopback(ip))
                return true;

            return false;
        }

        public record BotKeyGenRequest(
            string? AppId,
            int Count,
            float? DurationDays,
            string? Level,
            string? OwnerDiscordId,
            string? OwnerDiscordName,
            string? ClientId,
            List<string>? Products
        );

        public record BotStatusRequest(string? Status);
        public record BotExtendRequest(float? ExtraDays);
        public record BotPurgeRequest(string? Mode);

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            if (!IsAuthorized()) return Unauthorized();
            int totalKeys = await _db.LicenseKeys.CountAsync();
            int totalApps = await _db.Applications.CountAsync();
            int totalUsers = await _db.Users.CountAsync();

            return Ok(new
            {
                stats = new
                {
                    total = totalKeys,
                    apps = totalApps,
                    users = totalUsers
                }
            });
        }

        [HttpGet("apps")]
        public async Task<IActionResult> GetApps()
        {
            if (!IsAuthorized()) return Unauthorized();
            var apps = await _db.Applications.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                secret = a.Secret,
                ownerId = a.OwnerId,
                version = a.Version,
                isEnabled = a.IsEnabled,
                adminUsername = a.AdminUsername
            }).ToListAsync();

            return Ok(new { apps });
        }

        [HttpPost("keys/generate")]
        public async Task<IActionResult> GenerateKeys([FromBody] BotKeyGenRequest req)
        {
            if (!IsAuthorized()) return Unauthorized();

            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == req.AppId);
            if (app == null)
            {
                app = await _db.Applications.FirstOrDefaultAsync();
            }

            if (app == null)
            {
                app = new Application
                {
                    Name = "ArenaApp",
                    OwnerId = "OWNER-ARENA",
                    AdminUsername = "ADMIN",
                    Version = "1.0",
                    IsEnabled = true,
                    HwidLockEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Applications.Add(app);
                await _db.SaveChangesAsync();
            }

            int count = Math.Clamp(req.Count, 1, 100);
            int durationDays = (int)Math.Max(1, req.DurationDays ?? 30.0);

            var createdList = new List<LicenseKey>();
            for (int i = 0; i < count; i++)
            {
                var license = new LicenseKey
                {
                    Key = CryptoService.GenerateLicenseKey("ARENA"),
                    AppId = app.Id,
                    DurationDays = durationDays,
                    Level = 1,
                    MaxUses = 1,
                    CurrentUses = 0,
                    Status = "Unused",
                    Note = req.OwnerDiscordName != null ? $"Discord: {req.OwnerDiscordName} ({req.OwnerDiscordId})" : "Bot Generated",
                    CreatedAt = DateTime.UtcNow
                };
                createdList.Add(license);
            }

            _db.LicenseKeys.AddRange(createdList);
            await _db.SaveChangesAsync();

            var result = createdList.Select(k => new
            {
                id = k.Key,
                key = k.Key,
                appId = k.AppId,
                durationDays = k.DurationDays,
                status = "available",
                created = k.CreatedAt.ToString("o"),
                ownerDiscordId = req.OwnerDiscordId,
                ownerDiscordName = req.OwnerDiscordName
            }).ToList();

            return Ok(new { keys = result });
        }

        [HttpGet("keys")]
        public async Task<IActionResult> GetKeys([FromQuery] string? appId, [FromQuery] string? discordId)
        {
            if (!IsAuthorized()) return Unauthorized();
            var query = _db.LicenseKeys.AsQueryable();
            if (!string.IsNullOrEmpty(appId))
            {
                query = query.Where(k => k.AppId == appId);
            }

            var keys = await query.OrderByDescending(k => k.CreatedAt).Take(200).ToListAsync();
            var result = keys.Select(k => new
            {
                id = k.Key,
                key = k.Key,
                appId = k.AppId,
                durationDays = k.DurationDays,
                status = k.Status == "Unused" ? "available" : k.Status == "Used" ? "active" : k.Status.ToLowerInvariant(),
                created = k.CreatedAt.ToString("o"),
                lastUsed = k.UsedAt?.ToString("o")
            }).ToList();

            return Ok(new { keys = result });
        }

        [HttpGet("keys/{key}")]
        public async Task<IActionResult> GetSingleKey(string key)
        {
            if (!IsAuthorized()) return Unauthorized();
            var k = await _db.LicenseKeys.FirstOrDefaultAsync(x => x.Key == key);
            if (k == null) return NotFound(new { message = "Key not found" });

            return Ok(new
            {
                key = new
                {
                    id = k.Key,
                    key = k.Key,
                    appId = k.AppId,
                    durationDays = k.DurationDays,
                    status = k.Status == "Unused" ? "available" : k.Status == "Used" ? "active" : k.Status.ToLowerInvariant(),
                    created = k.CreatedAt.ToString("o"),
                    lastUsed = k.UsedAt?.ToString("o")
                }
            });
        }

        [HttpDelete("keys/{key}")]
        public async Task<IActionResult> DeleteKey(string key)
        {
            if (!IsAuthorized()) return Unauthorized();
            var item = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key);
            if (item != null)
            {
                _db.LicenseKeys.Remove(item);
                await _db.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpPost("keys/{key}/status")]
        public async Task<IActionResult> UpdateKeyStatus(string key, [FromBody] BotStatusRequest req)
        {
            if (!IsAuthorized()) return Unauthorized();
            var item = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key);
            if (item == null) return NotFound();

            if (req.Status != null)
            {
                if (req.Status.Equals("banned", StringComparison.OrdinalIgnoreCase))
                    item.Status = "Banned";
                else if (req.Status.Equals("available", StringComparison.OrdinalIgnoreCase) || req.Status.Equals("unused", StringComparison.OrdinalIgnoreCase))
                    item.Status = "Unused";
                else if (req.Status.Equals("active", StringComparison.OrdinalIgnoreCase) || req.Status.Equals("used", StringComparison.OrdinalIgnoreCase))
                    item.Status = "Used";
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, status = item.Status });
        }

        [HttpPost("keys/{key}/reset-hwid")]
        public async Task<IActionResult> ResetHwid(string key)
        {
            if (!IsAuthorized()) return Unauthorized();
            var item = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key);
            if (item != null && !string.IsNullOrEmpty(item.UsedByUsername))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.AppId == item.AppId && u.Username == item.UsedByUsername);
                if (user != null)
                {
                    user.Hwid = null;
                    await _db.SaveChangesAsync();
                }
            }
            return Ok(new { success = true });
        }

        [HttpPost("keys/{key}/extend")]
        public async Task<IActionResult> ExtendKey(string key, [FromBody] BotExtendRequest req)
        {
            if (!IsAuthorized()) return Unauthorized();
            var item = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key);
            if (item == null) return NotFound();

            item.DurationDays += (int)Math.Max(1, req.ExtraDays ?? 1);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, durationDays = item.DurationDays });
        }

        [HttpPost("keys/purge")]
        public async Task<IActionResult> PurgeKeys([FromBody] BotPurgeRequest req)
        {
            if (!IsAuthorized()) return Unauthorized();
            var items = await _db.LicenseKeys.ToListAsync();
            _db.LicenseKeys.RemoveRange(items);
            await _db.SaveChangesAsync();
            return Ok(new { removed = items.Count });
        }
    }
}
