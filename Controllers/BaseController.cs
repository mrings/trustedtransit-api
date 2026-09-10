using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    /// <summary>Known values for <see cref="Models.User.Role"/>.</summary>
    public static class Roles
    {
        public const string Admin = "admin";   // manages the facility, its staff, drivers, billing
        public const string User = "user";      // facility staff: residents + rides
        public const string Driver = "driver";  // driver app (not built yet)

        public static bool IsValid(string? r) => r is Admin or User or Driver;
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BaseController : ControllerBase
    {
        // Consumer email providers that must never auto-join a user to a facility by domain.
        private static readonly HashSet<string> ConsumerEmailDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "msn.com",
            "yahoo.com", "ymail.com", "aol.com", "icloud.com", "me.com", "mac.com",
            "proton.me", "protonmail.com", "gmx.com", "zoho.com", "yandex.com", "mail.com"
        };

        private User? _currentUser;
        private bool _currentUserLoaded;

        protected string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not found");
        }

        /// <summary>The Auth0 subject ("sub") of the caller, or null if unauthenticated.</summary>
        protected string? GetAuth0Id() =>
            User.Identity?.IsAuthenticated == true
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : null;

        // Auth0 access tokens don't carry email by default; a Login-flow Action adds it,
        // and custom claims must be namespaced. Check the namespaced claim and the plain one.
        private const string ClaimNamespace = "https://trustedtransit.app/";

        private string GetEmail() =>
            User.FindFirst(ClaimNamespace + "email")?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? "";

        private string? GetEmailVerified() =>
            User.FindFirst(ClaimNamespace + "email_verified")?.Value
            ?? User.FindFirst("email_verified")?.Value;

        private string? _userInfoEmail;
        private bool? _userInfoVerified;
        private bool _userInfoFetched;

        /// <summary>
        /// Calls Auth0's /userinfo with the caller's access token to get their verified email
        /// when the token itself doesn't carry it (no custom-claim Action configured).
        /// </summary>
        private async Task EnsureUserInfoAsync()
        {
            if (_userInfoFetched) return;
            _userInfoFetched = true;

            var bearer = Request.Headers.Authorization.ToString();
            if (!bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return;
            var domain = HttpContext.RequestServices.GetService<IConfiguration>()?["Auth0:Domain"];
            var factory = HttpContext.RequestServices.GetService<IHttpClientFactory>();
            if (string.IsNullOrEmpty(domain) || factory == null) return;

            try
            {
                var client = factory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://{domain}/userinfo");
                req.Headers.Add("Authorization", bearer);
                var res = await client.SendAsync(req);
                if (!res.IsSuccessStatusCode) return;
                using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("email", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String)
                    _userInfoEmail = e.GetString();
                if (doc.RootElement.TryGetProperty("email_verified", out var v))
                    _userInfoVerified = v.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Resolves the current caller to a <see cref="User"/> row, creating it on first sight
        /// (get-or-create keyed by Auth0 "sub"). New or still-unlinked users are matched to a
        /// <see cref="Facility"/> by their verified email domain. Returns null if the request
        /// is unauthenticated.
        /// </summary>
        protected async Task<User?> GetOrCreateCurrentUserAsync(TrustedTransitDbContext db)
        {
            if (_currentUserLoaded)
                return _currentUser;
            _currentUserLoaded = true;

            var auth0Id = GetAuth0Id();
            if (string.IsNullOrEmpty(auth0Id))
                return _currentUser = null;

            var email = GetEmail();
            if (string.IsNullOrEmpty(email))
            {
                await EnsureUserInfoAsync();
                email = _userInfoEmail ?? "";
            }
            var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);

            if (user == null)
            {
                user = new User { Auth0Id = auth0Id, Email = email, Role = Roles.User, Status = "active" };
                db.Users.Add(user);
            }
            else if (!string.IsNullOrEmpty(email) && user.Email != email)
            {
                user.Email = email;
                user.UpdatedAt = DateTime.UtcNow;
            }

            if (user.FacilityId == null)
            {
                var facilityId = await MatchFacilityByEmailDomainAsync(db, email);
                if (facilityId != null)
                    await AssignFacilityAsync(db, user, facilityId.Value);
            }
            else if (user.Role != Roles.Admin)
            {
                // Invariant: a populated facility always has an admin. If it lost/never had one,
                // its earliest member is promoted. (Also heals users linked before roles existed.)
                var hasAdmin = await db.Users.AnyAsync(u => u.FacilityId == user.FacilityId && u.Role == Roles.Admin);
                if (!hasAdmin)
                {
                    var earliestMemberId = await db.Users
                        .Where(u => u.FacilityId == user.FacilityId)
                        .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync();
                    if (earliestMemberId == user.Id)
                    {
                        user.Role = Roles.Admin;
                        user.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await db.SaveChangesAsync();
            return _currentUser = user;
        }

        /// <summary>
        /// Links a user to a facility. The first member of a facility becomes its
        /// <see cref="Roles.Admin"/>; everyone after keeps their current role (default
        /// <see cref="Roles.User"/>).
        /// </summary>
        protected async Task AssignFacilityAsync(TrustedTransitDbContext db, User user, Guid facilityId)
        {
            user.FacilityId = facilityId;
            var isFirstMember = !await db.Users.AnyAsync(u => u.FacilityId == facilityId && u.Id != user.Id);
            if (isFirstMember)
                user.Role = Roles.Admin;
            user.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>The resolved current user, or null if unauthenticated.</summary>
        protected Task<User?> CurrentUserAsync(TrustedTransitDbContext db) => GetOrCreateCurrentUserAsync(db);

        /// <summary>True when the caller is an admin of their facility.</summary>
        protected async Task<bool> IsAdminAsync(TrustedTransitDbContext db) =>
            (await CurrentUserAsync(db))?.Role == Roles.Admin;

        /// <summary>Null when the caller is an admin (of any facility); otherwise the error result to return.</summary>
        protected async Task<ActionResult?> CheckAdminAsync(TrustedTransitDbContext db)
        {
            var me = await CurrentUserAsync(db);
            if (me == null) return Unauthorized();
            if (me.Role != Roles.Admin) return Forbid();
            return null;
        }

        /// <summary>
        /// Facility that owns the caller's email domain, or null when there's no match, the
        /// email is unverified, the domain is a consumer provider, or more than one facility
        /// claims it.
        /// </summary>
        private async Task<Guid?> MatchFacilityByEmailDomainAsync(TrustedTransitDbContext db, string email)
        {
            // Block only when we positively know the email is unverified.
            if (string.Equals(GetEmailVerified(), "false", StringComparison.OrdinalIgnoreCase) || _userInfoVerified == false)
                return null;

            var at = email.LastIndexOf('@');
            if (at < 0 || at == email.Length - 1)
                return null;

            var domain = email[(at + 1)..].Trim().ToLowerInvariant();
            if (domain.Length == 0 || ConsumerEmailDomains.Contains(domain))
                return null;

            var matches = await db.Facilities
                .Where(f => f.EmailDomain == domain)
                .Select(f => f.Id)
                .Take(2)
                .ToListAsync();

            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>The authenticated caller's facility, or null if they aren't linked to one.</summary>
        protected async Task<Guid?> CurrentFacilityIdAsync(TrustedTransitDbContext db) =>
            (await GetOrCreateCurrentUserAsync(db))?.FacilityId;

        /// <summary>
        /// The Driver record for a driver-role caller, get-or-created and linked by UserId.
        /// Null if the caller isn't a driver.
        /// </summary>
        protected async Task<Driver?> GetOrCreateCurrentDriverAsync(TrustedTransitDbContext db)
        {
            var user = await GetOrCreateCurrentUserAsync(db);
            if (user == null || user.Role != Roles.Driver)
                return null;

            var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (driver == null)
            {
                driver = new Driver { UserId = user.Id, Status = "active" };
                db.Drivers.Add(driver);
                await db.SaveChangesAsync();
            }
            return driver;
        }

        /// <summary>The caller's facility with its trial end backfilled, or null.</summary>
        protected async Task<Facility?> CurrentFacilityAsync(TrustedTransitDbContext db)
        {
            var id = await CurrentFacilityIdAsync(db);
            if (id == null) return null;
            var facility = await db.Facilities.FirstAsync(f => f.Id == id);
            if (facility.SubscriptionStatus == "trial" && facility.TrialEndsAt == null)
            {
                facility.TrialEndsAt = facility.CreatedAt.AddDays(Plans.TrialDays);
                await db.SaveChangesAsync();
            }
            return facility;
        }

        /// <summary>
        /// The caller's facility, or a 400/402 result: 400 if unlinked, 402 (Payment Required)
        /// if the plan/subscription state doesn't allow creating new records.
        /// </summary>
        protected async Task<(Facility? facility, ActionResult? error)> RequireWritableFacilityAsync(TrustedTransitDbContext db)
        {
            var facility = await CurrentFacilityAsync(db);
            if (facility == null)
                return (null, BadRequest("Your account isn't linked to a facility yet."));
            if (!Entitlements.CanWrite(facility))
                return (null, StatusCode(StatusCodes.Status402PaymentRequired,
                    $"{Entitlements.BlockedReason(facility)} Subscribe to keep adding residents and rides."));
            return (facility, null);
        }

        /// <summary>Lowercases and strips a leading "@"/whitespace from an email domain; null/empty -> null.</summary>
        protected static string? NormalizeEmailDomain(string? domain)
        {
            var d = domain?.Trim().TrimStart('@').Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(d) ? null : d;
        }

        /// <summary>
        /// Normalizes a client-supplied DateTime to UTC so it can be written to a
        /// PostgreSQL 'timestamp with time zone' column. A value with no timezone
        /// (Kind=Unspecified, e.g. "1950-01-01" from a form) is treated as UTC.
        /// </summary>
        protected static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
