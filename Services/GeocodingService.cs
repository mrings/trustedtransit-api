using System.Globalization;
using System.Text.Json;

namespace TrustedTransit.Api.Services
{
    /// <summary>
    /// Best-effort street-address geocoding via OpenStreetMap's Nominatim — free, no API key.
    /// Used to resolve a facility's/company's Address/City/State/Zip to a Lat/Lng once, so
    /// "drivers within N miles" has something to measure against. Nominatim's usage policy caps
    /// casual callers at ~1 request/second; callers here only geocode a given row once and
    /// cache the result on it, so at this app's scale that's never a concern.
    /// </summary>
    public class GeocodingService
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<GeocodingService> _logger;

        public GeocodingService(IHttpClientFactory http, ILogger<GeocodingService> logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>Null if the address is blank, can't be resolved, or the request fails — never throws.</summary>
        public async Task<(decimal Lat, decimal Lng)?> GeocodeAsync(string address, string city, string state, string zip)
        {
            var query = string.Join(", ", new[] { address, city, state, zip }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (query.Length == 0)
                return null;

            try
            {
                var client = _http.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TrustedTransit/1.0 (+https://trustedtransit.app)");
                var url = $"https://nominatim.openstreetmap.org/search?format=json&limit=1&q={Uri.EscapeDataString(query)}";
                var res = await client.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                    return null;

                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    return null;

                var first = doc.RootElement[0];
                var lat = decimal.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
                var lng = decimal.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
                return (lat, lng);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Geocoding failed for \"{Query}\"", query);
                return null;
            }
        }

        /// <summary>Great-circle distance in miles between two points.</summary>
        public static double DistanceMiles(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadiusMiles = 3958.8;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusMiles * c;
        }
    }
}
