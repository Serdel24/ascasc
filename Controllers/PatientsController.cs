using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models; 
using WebApplication1.DTOs;   

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly MasterContext _context;

    public PatientsController(MasterContext context)
    {
        _context = context;
    }

    // =========================================================================
    // ZADANIE 2: GET /api/patients?search=an (Zagnieżdżony JSON wg wzoru)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetPatients([FromQuery] string? search)
    {
        // Przygotowanie zapytania z dołączeniem powiązanych relacji (Eager Loading)
        var query = _context.Patients
            .Include(p => p.Admissions).ThenInclude(a => a.Ward)
            .Include(p => p.BedAssignments).ThenInclude(ba => ba.Bed).ThenInclude(b => b.BedType)
            .Include(p => p.BedAssignments).ThenInclude(ba => ba.Bed).ThenInclude(b => b.Room).ThenInclude(r => r.Ward)
            .AsQueryable();

        // Filtrowanie po imieniu lub nazwisku
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => 
                EF.Functions.Like(p.FirstName, $"%{search}%") || 
                EF.Functions.Like(p.LastName, $"%{search}%"));
        }

        // Dokładne mapowanie na rekordy DTO z użyciem 'set'
        var result = await query.Select(p => new PatientResponseDto
        {
            Pesel = p.Pesel,
            FirstName = p.FirstName,
            LastName = p.LastName,
            Age = p.Age,
            Sex = p.Sex ? "Male" : "Female", // Mapowanie bool z bazy na tekst "Male"/"Female"
            
            Admissions = p.Admissions.Select(a => new AdmissionDto
            {
                Id = a.Id,
                AdmissionDate = a.AdmissionDate,
                DischargeDate = a.DischargeDate,
                Ward = new WardDto 
                { 
                    Id = a.Ward.Id, 
                    Name = a.Ward.Name, 
                    Description = a.Ward.Description 
                }
            }).ToList(),
            
            BedAssignments = p.BedAssignments.Select(ba => new BedAssignmentDto
            {
                Id = ba.Id,
                From = ba.From,
                To = ba.To,
                Bed = new BedDto
                {
                    Id = ba.Bed.Id,
                    BedType = new BedTypeDto 
                    { 
                        Id = ba.Bed.BedType.Id, 
                        Name = ba.Bed.BedType.Name, 
                        Description = ba.Bed.BedType.Description 
                    },
                    Room = new RoomDto
                    {
                        Id = ba.Bed.Room.Id,
                        HasTv = ba.Bed.Room.HasTv,
                        Ward = new WardDto 
                        { 
                            Id = ba.Bed.Room.Ward.Id, 
                            Name = ba.Bed.Room.Ward.Name, 
                            Description = ba.Bed.Room.Ward.Description 
                        }
                    }
                }
            }).ToList()
        }).ToListAsync();

        return Ok(result);
    }

    // =========================================================================
    // ZADANIE 3: POST /api/patients/{pesel}/bedassignments
    // =========================================================================
    [HttpPost("{pesel}/bedassignments")]
    public async Task<IActionResult> AssignBed(string pesel, [FromBody] AssignBedRequestDto request)
    {
        // 1. Walidacja pacjenta
        var patientExists = await _context.Patients.AnyAsync(p => p.Pesel == pesel);
        if (!patientExists)
        {
            return NotFound($"Pacjent o numerze PESEL {pesel} nie istnieje w bazie.");
        }

        // 2. Walidacja oddziału
        var wardExists = await _context.Wards.AnyAsync(w => w.Id == request.WardId);
        if (!wardExists)
        {
            return NotFound($"Oddział o ID {request.WardId} nie istnieje.");
        }

        // 3. Walidacja typu łóżka
        var bedTypeExists = await _context.BedTypes.AnyAsync(bt => bt.Id == request.BedTypeId);
        if (!bedTypeExists)
        {
            return NotFound($"Typ łóżka o ID {request.BedTypeId} nie istnieje.");
        }

        // 4. Algorytm szukania wolnego łóżka bez nakładania się terminów
        var availableBed = await _context.Beds
            .Where(b => b.Room.WardId == request.WardId && b.BedTypeId == request.BedTypeId)
            .Where(b => !_context.BedAssignments.Any(ba => 
                ba.BedId == b.Id && 
                ba.From < (request.To ?? DateTime.MaxValue) && 
                (ba.To == null || ba.To > request.From)
            ))
            .FirstOrDefaultAsync();

        // 5. Brak wolnych miejsc
        if (availableBed == null)
        {
            return NotFound($"Brak wolnych łóżek typu {request.BedTypeId} na oddziale {request.WardId} w zadanym terminie.");
        }

        // 6. Zapis nowego przypisania do bazy
        var newAssignment = new BedAssignment
        {
            PatientPesel = pesel,
            BedId = availableBed.Id,
            From = request.From,
            To = request.To
        };

        _context.BedAssignments.Add(newAssignment);
        await _context.SaveChangesAsync();

        return StatusCode(201, "Pomyślnie przypisano łóżko do pacjenta.");
    }
}