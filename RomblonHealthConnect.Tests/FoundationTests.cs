using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Hospitals;

namespace RomblonHealthConnect.Tests;

/// <summary>
/// Phase 1 foundation tests. These use an in-memory store so they never touch
/// the provincial database.
/// </summary>
public class FoundationTests
{
    private static ApplicationDbContext NewContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Hospital NewHospital(int id, string code, string name, int beds = 20, int free = 10) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        Municipality = "Romblon",
        Address = "Poblacion",
        Latitude = 12.5771,
        Longitude = 122.2711,
        TotalBeds = beds,
        AvailableBeds = free,
        IsActive = true,
        IsDeleted = false,
        Services = string.Empty,
        LastUpdatedUtc = DateTime.UtcNow
    };

    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        // IValidatableObject rules are not run by TryValidateObject on their own.
        if (model is IValidatableObject validatable)
        {
            results.AddRange(validatable.Validate(new ValidationContext(model)));
        }

        return results;
    }

    /* -- roles ---------------------------------------------------------- */

    [Fact]
    public void RoleCatalogue_HasNoDuplicates_SoSeedingIsIdempotent()
    {
        var names = Roles.All.Select(r => r.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(9, names.Count);
    }

    [Fact]
    public void ProvinceWideRoles_AreAllRealRoles()
    {
        var defined = Roles.All.Select(r => r.Name).ToHashSet();

        Assert.All(Roles.ProvinceWide, role => Assert.Contains(role, defined));
    }

    /* -- hospital validation -------------------------------------------- */

    [Fact]
    public void AvailableBeds_CannotExceedCapacity()
    {
        var form = new HospitalFormViewModel
        {
            Name = "Test Facility",
            Municipality = "Romblon",
            Address = "Poblacion",
            Latitude = 12.5,
            Longitude = 122.2,
            TotalBeds = 10,
            AvailableBeds = 25
        };

        var errors = Validate(form);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("cannot exceed total beds"));
    }

    [Fact]
    public void Coordinates_OutsideRomblon_AreRejected()
    {
        var form = new HospitalFormViewModel
        {
            Name = "Somewhere Else",
            Municipality = "Romblon",
            Address = "Elsewhere",
            Latitude = 48.8566,   // Paris
            Longitude = 2.3522,
            TotalBeds = 10,
            AvailableBeds = 5
        };

        var errors = Validate(form);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("outside Romblon"));
    }

    [Fact]
    public void Municipality_OutsideProvince_IsRejected()
    {
        var form = new HospitalFormViewModel
        {
            Name = "Test",
            Municipality = "Quezon City",
            Address = "Somewhere",
            Latitude = 12.5,
            Longitude = 122.2,
            TotalBeds = 5,
            AvailableBeds = 1
        };

        var errors = Validate(form);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("municipality within Romblon"));
    }

    [Fact]
    public void ValidFacility_PassesValidation()
    {
        var form = new HospitalFormViewModel
        {
            Name = "Romblon Provincial Hospital",
            Municipality = "Romblon",
            Address = "Barangay Capaclan",
            Latitude = 12.5764,
            Longitude = 122.2708,
            TotalBeds = 120,
            AvailableBeds = 32
        };

        Assert.Empty(Validate(form));
    }

    /* -- GIS endpoint filter -------------------------------------------- */

    [Fact]
    public async Task MapQuery_ExcludesInactiveDeletedAndUnpositionedFacilities()
    {
        await using var context = NewContext(nameof(MapQuery_ExcludesInactiveDeletedAndUnpositionedFacilities));

        var good = NewHospital(1, "good", "Mapped Facility");

        var inactive = NewHospital(2, "inactive", "Inactive Facility");
        inactive.IsActive = false;

        var deleted = NewHospital(3, "deleted", "Deleted Facility");
        deleted.IsDeleted = true;

        var noCoords = NewHospital(4, "nocoords", "Unpositioned Facility");
        noCoords.Latitude = 0;
        noCoords.Longitude = 0;

        context.Hospitals.AddRange(good, inactive, deleted, noCoords);
        await context.SaveChangesAsync();

        // Mirrors the filter in HospitalsApiController.Map.
        var mapped = await context.Hospitals
            .AsNoTracking()
            .Where(h => h.IsActive && !h.IsDeleted && h.Latitude != 0 && h.Longitude != 0)
            .ToListAsync();

        Assert.Single(mapped);
        Assert.Equal("good", mapped[0].Code);
    }

    /* -- soft delete ----------------------------------------------------- */

    [Fact]
    public async Task SoftDeletedFacility_IsHiddenFromActiveQueries()
    {
        await using var context = NewContext(nameof(SoftDeletedFacility_IsHiddenFromActiveQueries));

        var kept = NewHospital(1, "kept", "Kept Facility");
        var removed = NewHospital(2, "removed", "Removed Facility");
        removed.IsDeleted = true;
        removed.DeletedAt = DateTime.UtcNow;

        context.Hospitals.AddRange(kept, removed);
        await context.SaveChangesAsync();

        var active = await context.Hospitals.Where(h => h.IsActive && !h.IsDeleted).ToListAsync();

        Assert.Single(active);
        Assert.Equal("kept", active[0].Code);

        // The row still exists, so referral history keeps its foreign key.
        Assert.Equal(2, await context.Hospitals.CountAsync());
    }

    /* -- enum stability -------------------------------------------------- */

    [Fact]
    public void ExistingEnumValues_KeepTheirStoredNumbers()
    {
        // These integers are already persisted; changing them would silently
        // reclassify every existing row.
        Assert.Equal(0, (int)FacilityType.Public);
        Assert.Equal(1, (int)FacilityType.District);
        Assert.Equal(2, (int)FacilityType.RuralHealthUnit);
        Assert.Equal(3, (int)FacilityType.Private);

        Assert.Equal(0, (int)FacilityStatus.Online);
        Assert.Equal(1, (int)FacilityStatus.Limited);
        Assert.Equal(2, (int)FacilityStatus.Offline);
    }

    /* -- audit redaction -------------------------------------------------- */

    [Fact]
    public void AuditActionNames_AreDistinct()
    {
        var fields = typeof(AuditActions)
            .GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(fields.Count, fields.Distinct().Count());
    }
}
