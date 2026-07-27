# Romblon Health Connect

A web-based health referral and appointment system for hospitals and health facilities in the province of Romblon. The goal is to connect patients, doctors, and hospitals across the island municipalities so referrals, schedules, and records can be handled in one place instead of over phone calls and paper forms.

## Status

Early development. The project structure and dependencies are in place; feature modules are being built out one at a time.

## Tech Stack

- ASP.NET Core MVC (.NET 10)
- Entity Framework Core with SQL Server
- ASP.NET Core Identity for authentication and roles
- SignalR for real-time notifications
- Serilog for logging (console and file)
- Mapster for object mapping
- QuestPDF for report generation
- Swashbuckle for API documentation
- Bootstrap 5, jQuery

## Project Structure

```
RomblonHealthConnect/
├── Configurations/   EF Core entity configurations
├── Constants/        Shared constant values
├── Controllers/      MVC controllers
├── Data/             DbContext and migrations
├── Extensions/       Extension methods and service registration
├── Features/         Feature modules
│   ├── AI/
│   ├── Administration/
│   ├── Appointments/
│   ├── Dashboard/
│   ├── Doctors/
│   ├── Hospitals/
│   ├── Maps/
│   ├── Notifications/
│   ├── Patients/
│   ├── Referrals/
│   ├── Reports/
│   ├── Schedules/
│   └── Specializations/
├── Helpers/          Utility classes
├── Hubs/             SignalR hubs
├── Interfaces/       Service and repository contracts
├── Logs/             Serilog file output
├── Mapping/          Mapster configuration
├── Middleware/       Custom middleware
├── Models/           Domain entities
├── Repositories/     Data access implementations
├── SeedData/         Initial database seeding
├── Services/         Business logic
├── ViewModels/       View-specific models
├── Views/            Razor views
└── wwwroot/          Static assets
```

## Planned Features

- Patient registration and medical records
- Doctor profiles, specializations, and availability schedules
- Hospital and health facility directory
- Referral creation and tracking between facilities
- Appointment booking and management
- Real-time notifications for referral and appointment updates
- Map view of facilities across Romblon
- Reports exportable to PDF
- Role-based dashboards for administrators, doctors, and staff

## Getting Started

### Requirements

- .NET 10 SDK
- SQL Server (LocalDB, Express, or full instance)

### Setup

Clone the repository:

```bash
git clone https://github.com/jv-droid/RomblonHealthConnect.git
cd RomblonHealthConnect/RomblonHealthConnect
```

Set the database connection string in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RomblonHealthConnect;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

Restore packages and apply migrations:

```bash
dotnet restore
dotnet ef database update
```

Run the application:

```bash
dotnet run
```

The app will be available at the URL shown in the console output (see `Properties/launchSettings.json`).

## Author

Jayvee Molino
