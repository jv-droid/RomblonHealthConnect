using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Configurations;

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable("Referrals");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReferralNumber).HasMaxLength(32).IsRequired();
        builder.Property(r => r.ReasonForReferral).HasMaxLength(500).IsRequired();
        builder.Property(r => r.Diagnosis).HasMaxLength(500);
        builder.Property(r => r.ClinicalNotes).HasMaxLength(4000);
        builder.Property(r => r.ResponseNotes).HasMaxLength(2000);

        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.Priority).HasConversion<int>();

        builder.HasIndex(r => r.ReferralNumber).IsUnique();
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CreatedUtc);

        // Composite indexes matching the incoming and outgoing queue queries.
        builder.HasIndex(r => new { r.DestinationHospitalId, r.Status });
        builder.HasIndex(r => new { r.OriginHospitalId, r.Status });

        builder.HasOne(r => r.Patient)
            .WithMany(p => p.Referrals)
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two FKs to Hospitals, so both must be Restrict to avoid multiple cascade paths.
        builder.HasOne(r => r.OriginHospital)
            .WithMany(h => h.OutgoingReferrals)
            .HasForeignKey(r => r.OriginHospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.DestinationHospital)
            .WithMany(h => h.IncomingReferrals)
            .HasForeignKey(r => r.DestinationHospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RequestedSpecialization)
            .WithMany(s => s.Referrals)
            .HasForeignKey(r => r.RequestedSpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two optional FKs to Doctors. SET NULL on both would give SQL Server two
        // cascade paths from the same table (error 1785), so deletes are restricted.
        // Doctors are retired with IsActive rather than removed.
        builder.HasOne(r => r.AssignedDoctor)
            .WithMany()
            .HasForeignKey(r => r.AssignedDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReferringDoctor)
            .WithMany()
            .HasForeignKey(r => r.ReferringDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(r => r.IsOpen);
        builder.Ignore(r => r.IsTerminal);
    }
}

public class ReferralHistoryConfiguration : IEntityTypeConfiguration<ReferralHistory>
{
    public void Configure(EntityTypeBuilder<ReferralHistory> builder)
    {
        builder.ToTable("ReferralHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Notes).HasMaxLength(2000);
        builder.Property(h => h.PerformedBy).HasMaxLength(200).IsRequired();

        builder.Property(h => h.Action).HasConversion<int>();
        builder.Property(h => h.FromStatus).HasConversion<int>();
        builder.Property(h => h.ToStatus).HasConversion<int>();

        builder.HasIndex(h => new { h.ReferralId, h.PerformedAtUtc });

        builder.HasOne(h => h.Referral)
            .WithMany(r => r.History)
            .HasForeignKey(h => h.ReferralId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(h => h.ActionLabel);
    }
}

public class ReferralAttachmentConfiguration : IEntityTypeConfiguration<ReferralAttachment>
{
    public void Configure(EntityTypeBuilder<ReferralAttachment> builder)
    {
        builder.ToTable("ReferralAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.StoredFileName).HasMaxLength(120).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(a => a.UploadedBy).HasMaxLength(200).IsRequired();

        builder.Property(a => a.Category).HasConversion<int>();

        builder.HasIndex(a => a.ReferralId);

        builder.HasOne(a => a.Referral)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.ReferralId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(a => a.IsPreviewable);
        builder.Ignore(a => a.Extension);
        builder.Ignore(a => a.DisplaySize);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();

        builder.Property(n => n.Type).HasConversion<int>();

        builder.HasIndex(n => new { n.HospitalId, n.IsRead, n.CreatedUtc });

        builder.HasOne(n => n.Hospital)
            .WithMany()
            .HasForeignKey(n => n.HospitalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Referral)
            .WithMany()
            .HasForeignKey(n => n.ReferralId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Ignore(n => n.Icon);
    }
}
