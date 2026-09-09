using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(TrustedTransitDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Staff of the caller's facility. Empty when the caller isn't linked to one.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var me = await CurrentUserAsync(_context);
            if (me?.FacilityId == null)
                return Ok(Array.Empty<UserDto>());

            var users = await _context.Users
                .Where(u => u.FacilityId == me.FacilityId)
                .OrderBy(u => u.Email)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDetailDto>> GetUser(Guid id)
        {
            var me = await CurrentUserAsync(_context);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            // Only yourself, or an admin of the same facility.
            var sameFacilityAdmin = me?.Role == Roles.Admin && me.FacilityId != null && me.FacilityId == user.FacilityId;
            if (me?.Id != user.Id && !sameFacilityAdmin)
                return Forbid();

            return Ok(new UserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                Auth0Id = user.Auth0Id,
                Role = user.Role,
                Status = user.Status,
                FacilityId = user.FacilityId,
                CreatedAt = user.CreatedAt
            });
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDetailDto>> GetCurrentUser()
        {
            // Resolves by Auth0 "sub" (not a Guid), creating the row on first sight and
            // matching a facility by email domain.
            var user = await GetOrCreateCurrentUserAsync(_context);
            if (user == null)
                return Unauthorized();

            return Ok(new UserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                Auth0Id = user.Auth0Id,
                Role = user.Role,
                Status = user.Status,
                FacilityId = user.FacilityId,
                CreatedAt = user.CreatedAt
            });
        }

        /// <summary>
        /// Self-serve link for a user whose email domain doesn't auto-match: attaches the
        /// caller to a facility that has no members yet. Established facilities require an
        /// admin (or an email-domain match). One-time — can't be used to switch facilities.
        /// </summary>
        [HttpPost("me/facility")]
        public async Task<IActionResult> LinkMyFacility([FromBody] LinkFacilityRequest request)
        {
            var user = await GetOrCreateCurrentUserAsync(_context);
            if (user == null)
                return Unauthorized();

            if (user.FacilityId != null)
                return BadRequest("Your account is already linked to a facility.");

            if (!await _context.Facilities.AnyAsync(f => f.Id == request.FacilityId))
                return NotFound("Facility not found.");

            if (await _context.Users.AnyAsync(u => u.FacilityId == request.FacilityId))
                return BadRequest("That facility already has members. Ask an admin to add you, or sign in with your work email.");

            await AssignFacilityAsync(_context, user, request.FacilityId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} linked to facility {FacilityId} as {Role}", user.Id, request.FacilityId, user.Role);
            return NoContent();
        }

        // Admin: change a facility member's role or status.
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            var isAdminHere = me.Role == Roles.Admin && me.FacilityId != null && me.FacilityId == user.FacilityId;
            if (!isAdminHere)
                return Forbid();

            if (request.Role != null)
            {
                if (!Roles.IsValid(request.Role))
                    return BadRequest($"Invalid role. Use: {Roles.Admin}, {Roles.User}, {Roles.Driver}.");

                // Don't let an admin demote the facility's last admin (including themselves).
                if (user.Role == Roles.Admin && request.Role != Roles.Admin)
                {
                    var otherAdmins = await _context.Users
                        .CountAsync(u => u.FacilityId == user.FacilityId && u.Role == Roles.Admin && u.Id != user.Id);
                    if (otherAdmins == 0)
                        return BadRequest("A facility must keep at least one admin.");
                }
                user.Role = request.Role;
            }

            user.Status = request.Status ?? user.Status;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} updated by admin {AdminId}", id, me.Id);
            return NoContent();
        }

        // Admin: remove a member from the facility (keeps the user row, unlinks it).
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveUser(Guid id)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            var isAdminHere = me.Role == Roles.Admin && me.FacilityId != null && me.FacilityId == user.FacilityId;
            if (!isAdminHere)
                return Forbid();
            if (user.Id == me.Id)
                return BadRequest("You can't remove yourself.");

            user.FacilityId = null;
            user.Role = Roles.User;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} removed from facility by admin {AdminId}", id, me.Id);

            return NoContent();
        }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
    }

    public class UserDetailDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string Auth0Id { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public Guid? FacilityId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? Role { get; set; }
        public string? Status { get; set; }
    }

    public class LinkFacilityRequest
    {
        public Guid FacilityId { get; set; }
    }
}