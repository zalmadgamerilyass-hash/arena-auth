using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlackoutAuth.Server.Services
{
    public class DiscordService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task SendAlertAsync(string? webhookUrl, string title, string description, int colorHex = 0x8B5CF6)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || !webhookUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = "🛡️ [BLACKOUT AUTH] " + title,
                            description = description,
                            color = colorHex,
                            timestamp = DateTime.UtcNow.ToString("o"),
                            footer = new
                            {
                                text = "Blackout Auth Security Engine"
                            }
                        }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(webhookUrl, content);
            }
            catch
            {
                // Silently ignore webhook network errors
            }
        }
    }
}
