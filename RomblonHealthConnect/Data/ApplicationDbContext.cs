using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Identity;

namespace RomblonHealthConnect.Data;

/// <summary>
/// Entity Framework context for Romblon HealthConnect.
///
/// Phase 1 changed the base class from DbContext to IdentityDbContext. That adds
/// the AspNet* tables only; every pre-existing healthcare DbSet and its Fluent
/// configuration is untouched, so no existing table is renamed or rebuilt.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /* -- existing healthcare sets, unchanged ---------------------------- */

    public DbSet<Hospital> Hospitals => Set<Hospital>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Specialization> Specializations => Set<Specialization>();

    public DbSet<DoctorSpecialization> DoctorSpecializations => Set<DoctorSpecialization>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Referral> Referrals => Set<Referral>();

    public DbSet<ReferralHistory> ReferralHistories => Set<ReferralHistory>();

    public DbSet<ReferralAttachment> ReferralAttachments => Set<ReferralAttachment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    /* -- added in Phase 1 ----------------------------------------------- */

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own mappings must be applied before ours so that our
        // configurations can refine them rather than be overwritten.
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration in the Configurations folder,
        // including the untouched healthcare entity configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
