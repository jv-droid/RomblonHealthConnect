using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LicenseNumber).HasMaxLength(50).IsRequired();
        builder.Property(d => d.ContactNumber).HasMaxLength(50);
        builder.Property(d => d.Email).HasMaxLength(200);

        builder.Property(d => d.Availability).HasConversion<int>();

        builder.HasIndex(d => d.LicenseNumber).IsUnique();

        builder.HasOne(d => d.Hospital)
            .WithMany(h => h.Doctors)
            .HasForeignKey(d => d.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.PrimarySpecialization)
            .WithMany(s => s.Doctors)
            .HasForeignKey(d => d.PrimarySpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(d => d.FullName);
        builder.Ignore(d => d.IsAcceptingReferrals);
    }
}

public class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
{
    public void Configure(EntityTypeBuilder<Specialization> builder)
    {
        builder.ToTable("Specializations");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(400);

        builder.HasIndex(s => s.Name).IsUnique();
    }
}

public class DoctorSpecializationConfiguration : IEntityTypeConfiguration<DoctorSpecialization>
{
    public void Configure(EntityTypeBuilder<DoctorSpecialization> builder)
    {
        builder.ToTable("DoctorSpecializations");

        builder.HasKey(ds => new { ds.DoctorId, ds.SpecializationId });

        builder.HasOne(ds => ds.Doctor)
            .WithMany(d => d.DoctorSpecializations)
            .HasForeignKey(ds => ds.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ds => ds.Specialization)
            .WithMany(s => s.DoctorSpecializations)
            .HasForeignKey(ds => ds.SpecializationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
