using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Extensions;
using RomblonHealthConnect.Hubs;
using RomblonHealthConnect.SeedData;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging to console and rolling file.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/rhc-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

// Every endpoint requires an authenticated user unless it opts out with
// [AllowAnonymous]. Securing by default avoids a new controller shipping open.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();

// Session still carries lightweight UI state (the acting facility fallback).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RomblonHealthConnect.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddApplicationDatabase(builder.Configuration);
builder.Services.AddApplicationIdentity(builder.Environment);
builder.Services.AddApplicationAuthorization();
builder.Services.AddApplicationRepositories();
builder.Services.AddApplicationServices();

var app = builder.Build();

// Apply migrations, then seed roles and demo data.
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await context.Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(context, logger);
        await IdentitySeeder.SeedAsync(
            scope.ServiceProvider, app.Configuration, app.Environment, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration or seeding failed. The application may be unusable.");
    }
}

/* -- middleware pipeline, in required order --------------------------- */

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Renders friendly pages for 404 and other status-only responses.
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllers();

app.MapHub<ReferralHub>("/hubs/referrals");

app.Run();

/// <summary>Exposed so the integration test project can build a host.</summary>
public partial class Program;
