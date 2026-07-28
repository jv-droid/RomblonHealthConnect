using Microsoft.AspNetCore.Identity;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Models.Identity;
using RomblonHealthConnect.Services;

namespace RomblonHealthConnect.Extensions;

/// <summary>Identity, cookie, and authorization-policy registration.</summary>
public static class IdentityServiceExtensions
{
    public static IServiceCollection AddApplicationIdentity(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Strong password policy.
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 4;

            // Lockout after repeated failures.
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddClaimsPrincipalFactory<ApplicationClaimsPrincipalFactory>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = ".RomblonHealthConnect.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;

            // Plain HTTP is used for local development; everywhere else the
            // cookie must never travel unencrypted.
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ReturnUrlParameter = "returnUrl";
        });

        return services;
    }

    /// <summary>
    /// Policy definitions. Controllers reference these by name so permission
    /// changes happen in one place rather than scattered role literals.
    /// </summary>
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanManageHospitals, policy =>
                policy.RequireRole(Roles.ProvincialAdministrator, Roles.PHOAdministrator));

            options.AddPolicy(Policies.CanManageUsers, policy =>
                policy.RequireRole(Roles.ProvincialAdministrator));

            options.AddPolicy(Policies.CanViewProvinceWideData, policy =>
                policy.RequireRole(
                    Roles.ProvincialAdministrator, Roles.PHOAdministrator,
                    Roles.ExecutiveViewer, Roles.SystemAuditor));

            // Hospital administrators manage their own facility; the record-level
            // check still happens server-side in the controller.
            options.AddPolicy(Policies.CanManageHospitalData, policy =>
                policy.RequireRole(
                    Roles.ProvincialAdministrator, Roles.PHOAdministrator,
                    Roles.HospitalAdministrator));

            options.AddPolicy(Policies.CanCreateReferral, policy =>
                policy.RequireRole(
                    Roles.ProvincialAdministrator, Roles.PHOAdministrator,
                    Roles.HospitalAdministrator, Roles.ReferralCoordinator,
                    Roles.Doctor, Roles.Nurse));

            options.AddPolicy(Policies.CanReviewReferral, policy =>
                policy.RequireRole(
                    Roles.ProvincialAdministrator, Roles.PHOAdministrator,
                    Roles.HospitalAdministrator, Roles.ReferralCoordinator,
                    Roles.Doctor));

            options.AddPolicy(Policies.CanViewExecutiveDashboard, policy =>
                policy.RequireRole(
                    Roles.ProvincialAdministrator, Roles.PHOAdministrator,
                    Roles.ExecutiveViewer, Roles.HospitalAdministrator,
                    Roles.ReferralCoordinator, Roles.Doctor, Roles.Nurse,
                    Roles.RecordsOfficer, Roles.SystemAuditor));

            options.AddPolicy(Policies.CanViewAuditLogs, policy =>
                policy.RequireRole(Roles.ProvincialAdministrator, Roles.SystemAuditor));
        });

        return services;
    }
}
