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
    }
}