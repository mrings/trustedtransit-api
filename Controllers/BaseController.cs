using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TrustedTransit.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BaseController : ControllerBase
    {
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