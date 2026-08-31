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

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _context.Users
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
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            return Ok(new UserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                Auth0Id = user.Auth0Id,
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt
            });
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDetailDto>> GetCurrentUser()
        {
            var userId = GetUserId();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

            if (user == null)
                return NotFound();

            return Ok(new UserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
        {
            // Check if user already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
                return BadRequest("User already exists");

            var user = new User
            {
                Email = request.Email,
                Auth0Id = request.Auth0Id,
                Role = request.Role ?? "user",
                Status = "active"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} created with email {Email}", user.Id, user.Email);

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status
            });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            // Only allow admin or self to update
            if (id.ToString() != GetUserId() && !IsAdmin())
                return Forbid();

            user.Role = request.Role ?? user.Role;
            user.Status = request.Status ?? user.Status;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} updated", id);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            // Only admin can delete users
            if (!IsAdmin())
                return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} deleted", id);

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
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; }
        public string Auth0Id { get; set; }
        public string Role { get; set; }
    }

    public class UpdateUserRequest
    {
        public string Role { get; set; }
        public string Status { get; set; }
    }
}