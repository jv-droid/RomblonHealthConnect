using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Data;

/// <summary>
/// Entity Framework context for the Romblon HealthConnect referral engine.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hospital> Hospitals => Set<Hospital>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Specialization> Specializations => Set<Specialization>();

    public DbSet<DoctorSpecialization> DoctorSpecializations => Set<DoctorSpecialization>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Referral> Referrals => Set<Referral>();

    public DbSet<ReferralHistory> ReferralHistories => Set<ReferralHistory>();

    public DbSet<ReferralAttachment> ReferralAttachments => Set<ReferralAttachment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration in the Configurations folder.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
