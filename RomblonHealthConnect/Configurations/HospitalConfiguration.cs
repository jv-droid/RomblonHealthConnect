using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Configurations;

public class HospitalConfiguration : IEntityTypeConfiguration<Hospital>
{
    public void Configure(EntityTypeBuilder<Hospital> builder)
    {
        builder.ToTable("Hospitals");

        builder.HasKey(h => h.Id);

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

        // Computed properties are display-only.
        builder.Ignore(h => h.FacilityTypeLabel);
    }
}
