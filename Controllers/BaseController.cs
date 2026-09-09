using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
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

        protected string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not found");
        }

        protected string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "user";
        }

        protected bool IsAdmin()
        {
            return GetUserRole() == "admin";
        }

        /// <summary>The Auth0 subject ("sub") of the caller, or null if unauthenticated.</summary>
        protected string? GetAuth0Id() =>
            User.Identity?.IsAuthenticated == true
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : null;

        /// <summary>
        /// Resolves the current caller to a <see cref="User"/> row, creating it on first sight
        /// (get-or-create keyed by Auth0 "sub"). New or still-unlinked users are matched to a
        /// <see cref="Facility"/> by their verified email domain. Returns null if the request
        /// is unauthenticated.
        /// </summary>
        protected async Task<User?> GetOrCreateCurrentUserAsync(TrustedTransitDbContext db)
        {
            var auth0Id = GetAuth0Id();
            if (string.IsNullOrEmpty(auth0Id))
                return null;

            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "";
            var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);

            if (user == null)
            {
                user = new User { Auth0Id = auth0Id, Email = email, Role = "user", Status = "active" };
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
                {
                    user.FacilityId = facilityId;
                    user.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Facility that owns the caller's email domain, or null when there's no match, the
        /// email is unverified, the domain is a consumer provider, or more than one facility
        /// claims it.
        /// </summary>
        private async Task<Guid?> MatchFacilityByEmailDomainAsync(TrustedTransitDbContext db, string email)
        {
            // Treat a missing email_verified claim as "not explicitly false" for now; tighten
            // to require "true" once the Auth0 access token is confirmed to carry the claim.
            if (string.Equals(User.FindFirst("email_verified")?.Value, "false", StringComparison.OrdinalIgnoreCase))
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

        /// <summary>
        /// The facility a write should be scoped to: the authenticated user's facility when set,
        /// otherwise <paramref name="requestedFacilityId"/> (the pre-auth fallback the frontend
        /// still relies on). Null means "couldn't determine one".
        /// </summary>
        protected async Task<Guid?> ResolveFacilityIdAsync(TrustedTransitDbContext db, Guid? requestedFacilityId)
        {
            var user = await GetOrCreateCurrentUserAsync(db);
            return user?.FacilityId ?? requestedFacilityId;
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
