using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResidentsController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<ResidentsController> _logger;

        public ResidentsController(TrustedTransitDbContext context, ILogger<ResidentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResidentDto>>> GetResidents()
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return Ok(Array.Empty<ResidentDto>());

            var residents = await _context.Residents
                .Where(r => r.FacilityId == facilityId)
                .Select(r => new ResidentDto
                {
                    Id = r.Id,
                    FacilityId = r.FacilityId,
                    FirstName = r.FirstName,
                    LastName = r.LastName,
                    Phone = r.Phone,
                    MobilityRequirements = r.MobilityRequirements,
                    Status = r.Status
                })
                .ToListAsync();

            return Ok(residents);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ResidentDetailDto>> GetResident(Guid id)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == facilityId);

            if (resident == null)
                return NotFound();

            return Ok(new ResidentDetailDto
            {
                Id = resident.Id,
                FacilityId = resident.FacilityId,
                FirstName = resident.FirstName,
                LastName = resident.LastName,
                Phone = resident.Phone,
                DateOfBirth = resident.DateOfBirth,
                MobilityRequirements = resident.MobilityRequirements,
                MedicalInfo = resident.MedicalInfo,
                Notes = resident.Notes,
                FamilyEmail = resident.FamilyEmail,
                FamilyPhone = resident.FamilyPhone,
                Status = resident.Status
            });
        }
        
        [HttpPost]
        public async Task<ActionResult<ResidentDto>> CreateResident([FromBody] CreateResidentRequest request)
        {
            var (facility, error) = await RequireWritableFacilityAsync(_context);
            if (error != null) return error;

            var plan = Plans.Resolve(facility!.SubscriptionTier);
            if (!plan.UnlimitedResidents)
            {
                var count = await _context.Residents.CountAsync(r => r.FacilityId == facility.Id);
                if (count >= plan.ResidentLimit)
                    return StatusCode(StatusCodes.Status402PaymentRequired,
                        $"The {plan.Name} plan is limited to {plan.ResidentLimit} residents. Upgrade to add more.");
            }

            var resident = new Resident
            {
                FacilityId = facility.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                DateOfBirth = ToUtc(request.DateOfBirth),
                MobilityRequirements = request.MobilityRequirements ?? string.Empty,
                MedicalInfo = request.MedicalInfo ?? string.Empty,
                Notes = request.Notes ?? string.Empty,
                FamilyEmail = request.FamilyEmail ?? string.Empty,
                FamilyPhone = request.FamilyPhone ?? string.Empty,
                Status = "active"
            };

            _context.Residents.Add(resident);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Resident {ResidentId} created", resident.Id);

            return CreatedAtAction(nameof(GetResident), new { id = resident.Id }, new ResidentDto
            {
                Id = resident.Id,
                FirstName = resident.FirstName,
                LastName = resident.LastName
            });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateResident(Guid id, [FromBody] UpdateResidentRequest request)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == facilityId);
            if (resident == null)
                return NotFound();

            resident.FirstName = request.FirstName ?? resident.FirstName;
            resident.LastName = request.LastName ?? resident.LastName;
            resident.Phone = request.Phone ?? resident.Phone;
            if (request.DateOfBirth.HasValue)
                resident.DateOfBirth = ToUtc(request.DateOfBirth.Value);
            resident.MobilityRequirements = request.MobilityRequirements ?? resident.MobilityRequirements;
            resident.MedicalInfo = request.MedicalInfo ?? resident.MedicalInfo;
            resident.Notes = request.Notes ?? resident.Notes;
            resident.FamilyEmail = request.FamilyEmail ?? resident.FamilyEmail;
            resident.FamilyPhone = request.FamilyPhone ?? resident.FamilyPhone;
            resident.Status = request.Status ?? resident.Status;
            resident.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Resident {ResidentId} updated", id);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResident(Guid id)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == facilityId);
            if (resident == null)
                return NotFound();

            _context.Residents.Remove(resident);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Resident {ResidentId} deleted", id);

            return NoContent();
        }
    }

    public class ResidentDto
    {
        public Guid Id { get; set; }
        public Guid FacilityId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string MobilityRequirements { get; set; }
        public string Status { get; set; }
    }

    public class ResidentDetailDto
    {
        public Guid Id { get; set; }
        public Guid FacilityId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string MobilityRequirements { get; set; }
        public string MedicalInfo { get; set; }
        public string Notes { get; set; }
        public string FamilyEmail { get; set; }
        public string FamilyPhone { get; set; }
        public string Status { get; set; }
    }

    public class CreateResidentRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? MobilityRequirements { get; set; }
        public string? MedicalInfo { get; set; }
        public string? Notes { get; set; }
        public string? FamilyEmail { get; set; }
        public string? FamilyPhone { get; set; }
    }

    public class UpdateResidentRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? MobilityRequirements { get; set; }
        public string? MedicalInfo { get; set; }
        public string? Notes { get; set; }
        public string? FamilyEmail { get; set; }
        public string? FamilyPhone { get; set; }
        public string? Status { get; set; }
    }
}