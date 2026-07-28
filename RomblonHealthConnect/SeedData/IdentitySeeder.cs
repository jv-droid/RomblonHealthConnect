using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Models.Identity;

namespace RomblonHealthConnect.SeedData;

/// <summary>
/// Seeds Identity roles and, in development, an initial administrator.
///
/// Idempotent: every insert is guarded by an existence check, so repeated
/// startups never duplicate a role or user. Healthcare data is never touched.
/// </summary>
public static class IdentitySeeder
{
    public const string DevAdminUserName = "provincial.admin";
    public const string DevAdminEmail = "admin@romblonhealthconnect.local";

    /// <summary>Configuration key holding the initial administrator password.</summary>
    public const string SeedPasswordKey = "Seed:AdminPassword";

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        await SeedRolesAsync(roleManager, logger, cancellationToken);
        await SeedAdministratorAsync(userManager, context, configuration, environment, logger, cancellationToken);
        await SeedFacilityUsersAsync(userManager, context, configuration, environment, logger, cancellationToken);
    }

    /// <summary>
    /// Development-only coordinator accounts, one per facility, so hospital-level
    /// data scoping can actually be exercised. Uses the same configured password
    /// as the administrator and is skipped entirely when that is unset or when
    /// the environment is not Development.
    /// </summary>
    private static async Task SeedFacilityUsersAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var password = configuration[SeedPasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        // One coordinator for each of the two busiest facilities is enough to
        // demonstrate and test cross-hospital isolation.
        var targets = await context.Hospitals
            .Where(h => h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.Id)
            .Select(h => new { h.Id, h.Code, h.Name })
            .Take(2)
            .ToListAsync(cancellationToken);

        var created = 0;

        foreach (var hospital in targets)
        {
            var userName = $"{hospital.Code}.coord";

            if (await userManager.FindByNameAsync(userName) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = $"{hospital.Code}.coord@romblonhealthconnect.local",
                EmailConfirmed = true,
                FirstName = hospital.Name.Split(' ')[0],
                LastName = "Coordinator",
                DisplayName = $"{hospital.Name} Coordinator",
                PositionTitle = "Referral Coordinator",
                HospitalId = hospital.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system-seed"
            };

            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, Roles.ReferralCoordinator);
                created++;
            }
            else
            {
                logger.LogError("Could not create facility user {UserName}: {Errors}",
                    userName, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (created > 0)
        {
            logger.LogInformation("Created {Count} development facility coordinator account(s).", created);
        }
    }

    /* ------------------------------------------------------------------ */

    private static async Task SeedRolesAsync(
        RoleManager<ApplicationRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var created = 0;

        foreach (var (name, description) in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Existence check keeps repeated runs from duplicating roles.
            if (await roleManager.RoleExistsAsync(name))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new ApplicationRole(name, description));

            if (result.Succeeded)
            {
                created++;
            }
            else
            {
                logger.LogError("Could not create role {Role}: {Errors}",
                    name, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        logger.LogInformation("Role seed complete. {Created} created, {Total} defined.",
            created, Roles.All.Count);
    }

    private static async Task SeedAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Never auto-create a privileged account outside development.
        if (!environment.IsDevelopment())
        {
            logger.LogInformation(
                "Administrator seeding is skipped outside Development. Create the first account manually.");
            return;
        }

        var existing = await userManager.FindByNameAsync(DevAdminUserName);
        if (existing is not null)
        {
            logger.LogInformation("Initial administrator already present; skipping.");
            return;
        }

        var password = configuration[SeedPasswordKey];

        if (string.IsNullOrWhiteSpace(password))
        {
            // A hardcoded fallback would be a credential in source control, so
            // the seed is skipped with instructions instead.
            logger.LogWarning(
                "No initial administrator was created because '{Key}' is not configured. " +
                "Set it with:  dotnet user-secrets set \"{Key}\" \"<a strong password>\"  " +
                "then restart. No default password is generated by design.",
                SeedPasswordKey, SeedPasswordKey);
            return;
        }

        // Provincial hospital is the natural home facility, but the provincial
        // administrator is province-wide, so HospitalId stays null.
        var admin = new ApplicationUser
        {
            UserName = DevAdminUserName,
            Email = DevAdminEmail,
            EmailConfirmed = true,
            FirstName = "Provincial",
            LastName = "Administrator",
            DisplayName = "Provincial Administrator",
            PositionTitle = "Provincial Health Office",
            HospitalId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system-seed"
        };

        var result = await userManager.CreateAsync(admin, password);

        if (!result.Succeeded)
        {
            logger.LogError("Could not create the initial administrator: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.ProvincialAdministrator);

        logger.LogInformation(
            "Initial administrator '{UserName}' created and assigned {Role}.",
            DevAdminUserName, Roles.ProvincialAdministrator);
    }
}
