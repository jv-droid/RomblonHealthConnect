using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PatientNumber).HasMaxLength(32).IsRequired();
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.MiddleName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ContactNumber).HasMaxLength(50);
        builder.Property(p => p.Address).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Municipality).HasMaxLength(100).IsRequired();
        builder.Property(p => p.BloodType).HasMaxLength(8);

        builder.Property(p => p.Sex).HasConversion<int>();

        builder.HasIndex(p => p.PatientNumber).IsUnique();
        builder.HasIndex(p => new { p.LastName, p.FirstName });

        builder.Ignore(p => p.FullName);
        builder.Ignore(p => p.Age);
    }
}
