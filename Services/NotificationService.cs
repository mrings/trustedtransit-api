using System.Text;
using System.Text.Json;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Services
{
    public record RideUpdate(
        Guid RideId, Guid FacilityId, string Event,
        string ResidentFirstName, string FamilyEmail, string FamilyPhone,
        DateTime ScheduledPickupTime, string DestinationAddress,
        string? DriverName);

    /// <summary>
    /// Sends ride-status notifications to residents' families. Email via Resend, SMS via Twilio.
    /// A channel with no configured keys is simply skipped.
    /// </summary>
    public class NotificationService
    {
        private readonly IHttpClientFactory _http;
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IHttpClientFactory http, IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationService> logger)
        {
            _http = http;
            _scopes = scopes;
            _config = config;
            _logger = logger;
        }

        public bool EmailEnabled => !string.IsNullOrWhiteSpace(_config["Resend:ApiKey"])
            && !string.IsNullOrWhiteSpace(_config["Notifications:FromEmail"]);

        public bool SmsEnabled => !string.IsNullOrWhiteSpace(_config["Twilio:AccountSid"])
            && !string.IsNullOrWhiteSpace(_config["Twilio:AuthToken"])
            && !string.IsNullOrWhiteSpace(_config["Twilio:FromNumber"]);

        /// <summary>Fire-and-forget. Failures are logged, never surfaced to the caller.</summary>
        public void Enqueue(RideUpdate update) => _ = Task.Run(() => SendAsync(update));

        private async Task SendAsync(RideUpdate u)
        {
            try
            {
                var (subject, body) = Compose(u);
                var records = new List<RideNotification>();

                if (EmailEnabled && LooksLikeEmail(u.FamilyEmail))
                    records.Add(await SendEmailAsync(u, subject, body));
                if (SmsEnabled && LooksLikePhone(u.FamilyPhone))
                    records.Add(await SendSmsAsync(u, body));

                if (records.Count == 0)
                    return;

                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TrustedTransitDbContext>();
                db.RideNotifications.AddRange(records);
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Notification dispatch failed for ride {RideId}", u.RideId);
            }
        }

        private static (string subject, string body) Compose(RideUpdate u)
        {
            var when = u.ScheduledPickupTime.ToString("MMM d 'at' h:mm tt") + " UTC";
            var name = u.ResidentFirstName;
            var body = u.Event switch
            {
                "assigned" => $"{name}'s ride to {u.DestinationAddress} on {when} has a driver" +
                              (string.IsNullOrWhiteSpace(u.DriverName) ? "." : $": {u.DriverName}."),
                "en_route" => $"{name} has been picked up for their appointment ({when}).",
                "completed" => $"{name}'s ride is complete. They've arrived safely.",
                "cancelled" => $"{name}'s ride scheduled for {when} has been cancelled.",
                _ => $"Update on {name}'s ride ({when}).",
            };
            return ($"TrustedTransit: ride update for {name}", body + "\n\n— TrustedTransit");
        }

        private async Task<RideNotification> SendEmailAsync(RideUpdate u, string subject, string body)
        {
            var rec = NewRecord(u, "email", u.FamilyEmail);
            try
            {
                var client = _http.CreateClient();
                var payload = JsonSerializer.Serialize(new
                {
                    from = _config["Notifications:FromEmail"],
                    to = new[] { u.FamilyEmail },
                    subject,
                    text = body,
                });
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                req.Headers.Add("Authorization", $"Bearer {_config["Resend:ApiKey"]}");
                var res = await client.SendAsync(req);
                rec.Success = res.IsSuccessStatusCode;
                if (!rec.Success)
                    rec.Error = $"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}";
            }
            catch (Exception e) { rec.Error = e.Message; }
            return rec;
        }

        private async Task<RideNotification> SendSmsAsync(RideUpdate u, string body)
        {
            var rec = NewRecord(u, "sms", u.FamilyPhone);
            try
            {
                var sid = _config["Twilio:AccountSid"]!;
                var token = _config["Twilio:AuthToken"]!;
                var client = _http.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["To"] = u.FamilyPhone,
                        ["From"] = _config["Twilio:FromNumber"]!,
                        ["Body"] = body,
                    }),
                };
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sid}:{token}"));
                req.Headers.Add("Authorization", $"Basic {basic}");
                var res = await client.SendAsync(req);
                rec.Success = res.IsSuccessStatusCode;
                if (!rec.Success)
                    rec.Error = $"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}";
            }
            catch (Exception e) { rec.Error = e.Message; }
            return rec;
        }

        private static RideNotification NewRecord(RideUpdate u, string channel, string recipient) => new()
        {
            RideId = u.RideId,
            FacilityId = u.FacilityId,
            Event = u.Event,
            Channel = channel,
            Recipient = recipient,
        };

        private static bool LooksLikeEmail(string s) => s.Contains('@') && s.Contains('.');
        private static bool LooksLikePhone(string s) => s.Count(char.IsDigit) >= 10;
    }
}
