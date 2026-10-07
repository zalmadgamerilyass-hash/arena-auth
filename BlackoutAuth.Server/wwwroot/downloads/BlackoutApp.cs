using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlackoutAuth.SDK.CSharp.Models;
using BlackoutAuth.SDK.CSharp.Security;

namespace BlackoutAuth.SDK.CSharp
{
    public class BlackoutApp
    {
        public string Name { get; }
        public string OwnerId { get; }
        private readonly string _secret;
        public string Version { get; }
        public string ApiUrl { get; }
        public bool EnableAntiDebug { get; set; }

        public bool IsInitialized { get; private set; }
        public bool IsLoggedIn { get; private set; }
        public string LastError { get; private set; } = "";
        public UserData? User { get; private set; }
        public string HwidString { get; }

        private string? _sessionToken;
        private string? _encryptionKey;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public BlackoutApp(string name, string ownerId, string secret, string version, string apiUrl = "http://localhost:5000", bool enableAntiDebug = false)
        {
            Name = name;
            OwnerId = ownerId;
            _secret = secret;
            Version = version;
            ApiUrl = apiUrl.TrimEnd('/');
            EnableAntiDebug = enableAntiDebug;
            HwidString = Hwid.Get();
        }

        public bool Init()
        {
            try
            {
                if (EnableAntiDebug)
                {
                    if (AntiDebug.CheckDebugger() || AntiDebug.CheckBlacklistedProcesses())
                    {
                        LastError = "Security violation: Debugger or reverse engineering tool detected.";
                        return false;
                    }
                }

                var payload = new
                {
                    name = Name,
                    ownerId = OwnerId,
                    secret = _secret,
                    version = Version
                };

                // PERMANENT DEXTER DEVELOPMENT CREDIT
                Console.WriteLine("\n=======================================================");
                Console.WriteLine("  [*] BLACKOUT AUTH FRAMEWORK");
                Console.WriteLine("  [*] Developed & Engineered exclusively by: DEXTER");
                Console.WriteLine("=======================================================\n");

                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/init", payload);
                if (res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    _sessionToken = res.RootElement.GetProperty("session_token").GetString();
                    _encryptionKey = res.RootElement.GetProperty("encryption_key").GetString();
                    IsInitialized = true;
                    return true;
                }

                if (res.RootElement.TryGetProperty("update_required", out var upd) && upd.GetBoolean())
                {
                    string dl = res.RootElement.TryGetProperty("download_url", out var dlProp) ? dlProp.GetString() ?? "" : "";
                    LastError = $"Update required! Download from: {dl}";
                    return false;
                }

                LastError = res.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Init failed.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = "Connection to authentication server failed: " + ex.Message;
                return false;
            }
        }

        public bool Register(string username, string password, string licenseKey)
        {
            if (!EnsureInit()) return false;

            try
            {
                var payload = new
                {
                    sessionToken = _sessionToken,
                    username,
                    password,
                    licenseKey,
                    hwid = HwidString
                };

                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/register", payload);
                if (res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    PopulateUser(res.RootElement);
                    IsLoggedIn = true;
                    return true;
                }

                LastError = res.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Registration failed." : "Registration failed.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool Login(string username, string password)
        {
            if (!EnsureInit()) return false;

            try
            {
                var payload = new
                {
                    sessionToken = _sessionToken,
                    username,
                    password,
                    hwid = HwidString
                };

                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/login", payload);
                if (res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    PopulateUser(res.RootElement);
                    IsLoggedIn = true;
                    return true;
                }

                LastError = res.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Login failed." : "Login failed.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool LicenseOnly(string licenseKey)
        {
            if (!EnsureInit()) return false;

            try
            {
                var payload = new
                {
                    sessionToken = _sessionToken,
                    licenseKey,
                    hwid = HwidString
                };

                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/license", payload);
                if (res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    PopulateUser(res.RootElement);
                    IsLoggedIn = true;
                    return true;
                }

                LastError = res.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? "License auth failed." : "License auth failed.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool Upgrade(string username, string licenseKey)
        {
            if (!EnsureInit()) return false;

            try
            {
                var payload = new
                {
                    sessionToken = _sessionToken,
                    username,
                    licenseKey
                };

                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/upgrade", payload);
                if (res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    if (User != null && res.RootElement.TryGetProperty("new_expiry", out var exp))
                    {
                        User.Expiry = exp.GetString() ?? User.Expiry;
                    }
                    return true;
                }

                LastError = res.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Upgrade failed." : "Upgrade failed.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public string? GetVar(string varName)
        {
            if (!EnsureInit() || !IsLoggedIn)
            {
                LastError = "Must be logged in to fetch variables.";
                return null;
            }

            try
            {
                var payload = new { sessionToken = _sessionToken, varName };
                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/var", payload);
                if (res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    string cipherBase64 = res.RootElement.GetProperty("value").GetString() ?? "";
                    if (res.RootElement.TryGetProperty("encrypted", out var isEnc) && isEnc.GetBoolean())
                    {
                        return Crypto.DecryptAes256(cipherBase64, _encryptionKey ?? "");
                    }
                    return cipherBase64;
                }

                LastError = res.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Var fetch failed." : "Failed.";
                return null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        public byte[]? DownloadFile(string fileId)
        {
            if (!EnsureInit() || !IsLoggedIn)
            {
                LastError = "Must be logged in to download protected files.";
                return null;
            }

            try
            {
                string url = $"{ApiUrl}/api/v1/auth/file/{fileId}?sessionToken={_sessionToken}";
                var task = _http.GetByteArrayAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception ex)
            {
                LastError = "File download failed: " + ex.Message;
                return null;
            }
        }

        public bool Check()
        {
            if (!EnsureInit()) return false;

            try
            {
                var payload = new { sessionToken = _sessionToken };
                var res = PostJsonSync($"{ApiUrl}/api/v1/auth/check", payload);
                return res.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean();
            }
            catch
            {
                return false;
            }
        }

        public void Log(string message, string severity = "INFO")
        {
            if (!EnsureInit()) return;
            try
            {
                var payload = new { sessionToken = _sessionToken, message, severity };
                _ = PostJsonSync($"{ApiUrl}/api/v1/auth/log", payload);
            }
            catch { }
        }

        private bool EnsureInit()
        {
            if (!IsInitialized)
            {
                LastError = "SDK not initialized. Call Init() first.";
                return false;
            }
            return true;
        }

        private void PopulateUser(JsonElement root)
        {
            if (root.TryGetProperty("user", out var u))
            {
                User = new UserData
                {
                    Username = u.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "",
                    Expiry = u.TryGetProperty("expiry", out var ex) ? ex.GetString() ?? "" : "",
                    Level = u.TryGetProperty("level", out var lv) ? lv.GetInt32() : 1,
                    Hwid = u.TryGetProperty("hwid", out var hw) ? hw.GetString() : null
                };
            }
        }

        private static JsonDocument PostJsonSync(string url, object body)
        {
            string json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var task = _http.PostAsync(url, content);
            task.Wait();

            var response = task.Result;
            var readTask = response.Content.ReadAsStringAsync();
            readTask.Wait();

            return JsonDocument.Parse(readTask.Result);
        }
    }
}
