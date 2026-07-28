using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Identity;

namespace RomblonHealthConnect.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.MiddleName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PositionTitle).HasMaxLength(150);
        builder.Property(u => u.CreatedBy).HasMaxLength(450);
        builder.Property(u => u.UpdatedBy).HasMaxLength(450);

        builder.HasIndex(u => u.HospitalId);
        builder.HasIndex(u => u.IsActive);

        // Optional assignment to an existing facility. Restrict so a facility
        // with staff cannot be hard-deleted out from under its user accounts.
        builder.HasOne(u => u.Hospital)
            .WithMany()
            .HasForeignKey(u => u.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(u => u.FullName);
    }
}

public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(r => r.Description).HasMaxLength(400);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).HasMaxLength(450);
        builder.Property(a => a.UserDisplayName).HasMaxLength(200);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.Description).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(400);

        // Audit queries are almost always "recent activity" or "by entity".
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.UserId);
    }
}
