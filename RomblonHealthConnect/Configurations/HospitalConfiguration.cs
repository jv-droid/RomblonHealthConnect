using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Configurations;

public class HospitalConfiguration : IEntityTypeConfiguration<Hospital>
{
    public void Configure(EntityTypeBuilder<Hospital> builder)
    {
        builder.ToTable("Hospitals");

        builder.HasKey(h => h.Id);

        /* -- pre-existing columns, mappings unchanged -------------------- */

        builder.Property(h => h.Code).HasMaxLength(64).IsRequired();
        builder.Property(h => h.Name).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Municipality).HasMaxLength(100).IsRequired();
        builder.Property(h => h.Address).HasMaxLength(300).IsRequired();
        builder.Property(h => h.ContactNumber).HasMaxLength(50);
        builder.Property(h => h.Email).HasMaxLength(200);
        builder.Property(h => h.Services).HasMaxLength(1000);

        builder.Property(h => h.FacilityType).HasConversion<int>();
        builder.Property(h => h.Status).HasConversion<int>();

        builder.HasIndex(h => h.Code).IsUnique();
        builder.HasIndex(h => h.Municipality);

        /* -- Phase 1 additions ------------------------------------------ */

        builder.Property(h => h.ShortName).HasMaxLength(100);
        builder.Property(h => h.Barangay).HasMaxLength(150);

        // Defaults let the columns be NOT NULL without a separate backfill pass:
        // SQL Server applies the default to every existing row when the column
        // is added.
        builder.Property(h => h.Province).HasMaxLength(100).IsRequired().HasDefaultValue("Romblon");
        builder.Property(h => h.Region).HasMaxLength(100).IsRequired().HasDefaultValue("MIMAROPA");

        builder.Property(h => h.WebsiteUrl).HasMaxLength(300);
        builder.Property(h => h.LicenseNumber).HasMaxLength(100);
        builder.Property(h => h.PhilHealthAccreditationNumber).HasMaxLength(100);

        // Column default only; existing rows are backfilled from FacilityType by
        // the migration so nothing is left at an arbitrary value.
        builder.Property(h => h.OwnershipType)
            .HasConversion<int>()
            .HasDefaultValue(HospitalOwnershipType.ProvincialGovernment);

        builder.Property(h => h.HasOperatingRoom).HasDefaultValue(false);
        builder.Property(h => h.HasLaboratory).HasDefaultValue(false);
        builder.Property(h => h.HasPharmacy).HasDefaultValue(false);
        builder.Property(h => h.HasAmbulance).HasDefaultValue(false);
        builder.Property(h => h.IsReferralReceivingFacility).HasDefaultValue(true);

        builder.Property(h => h.IsDeleted).HasDefaultValue(false);
        builder.Property(h => h.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(h => h.CreatedBy).HasMaxLength(450);
        builder.Property(h => h.UpdatedBy).HasMaxLength(450);

        builder.HasIndex(h => h.IsDeleted);
        builder.HasIndex(h => new { h.IsActive, h.IsDeleted });

        /* -- computed / alias members are display-only ------------------- */

        builder.Ignore(h => h.FacilityTypeLabel);
        builder.Ignore(h => h.OwnershipLabel);
        builder.Ignore(h => h.FacilityCode);
        builder.Ignore(h => h.FullAddress);
        builder.Ignore(h => h.BedCapacity);
        builder.Ignore(h => h.LastReportedAt);
        builder.Ignore(h => h.NetworkStatus);
        builder.Ignore(h => h.IsEmergencyCapable);
    }
}
