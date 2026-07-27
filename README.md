# Romblon Health Connect

A web-based health referral system for hospitals and health facilities in the province of Romblon. The goal is to connect patients, doctors, and hospitals across the island municipalities so referrals, schedules, and records can be handled in one place instead of over phone calls and paper forms.

## Status

Phase 3 complete. The GIS dashboard and the Smart Referral Engine are both working against a seeded database.

| Phase | Module | State |
|-------|--------|-------|
| 1 | Project structure and dependencies | Done |
| 2 | GIS dashboard (map, facilities, overview) | Done |
| 3 | Smart Referral Engine | Done |
| 4 | Authentication and role-based access | Next |

### What works today

- Provincial GIS dashboard with 15 mapped facilities, filters, and a facility drawer
- Referral dashboard with metric cards, search, filters, and paging
- Seven-step create-referral wizard with live hospital capability lookup and map integration
- Full referral workflow: draft, submit, accept, reject, request information, complete, cancel
- Append-only audit timeline on every referral
- File attachments (PDF, JPEG, PNG, DOCX) with inline image preview
- Real-time updates and a notification centre over SignalR
- Seeded demo data for the whole province

### Known gaps

- **No authentication yet.** Every session acts as Romblon Provincial Hospital, resolved by `ICurrentFacilityProvider` from session state. Incoming and Outgoing are always from that facility's perspective, so you cannot yet watch a transfer from both sides.
- The GIS dashboard still renders from client-side sample data in `dashboard.js` rather than the database. The referral module uses the same facility codes, so the two can be joined up when convenient.
- Expired referrals are seeded but nothing sweeps overdue ones automatically; a background job is needed.
- Serilog, Mapster, QuestPDF, and Swashbuckle are referenced in the project but not wired up yet.

## Tech Stack

- ASP.NET Core MVC (.NET 10), Razor Views
- Entity Framework Core 10 with SQL Server
- SignalR for real-time referral updates
- MapLibre GL JS with CARTO/OpenStreetMap tiles
- Vanilla JavaScript, no frontend framework
- Bootstrap 5 for layout utilities, Font Awesome for icons
- Microsoft Fluent 2 inspired design system driven by CSS variables

Referenced but not yet in use: ASP.NET Core Identity, Serilog, Mapster, QuestPDF, Swashbuckle.

## Getting Started

### Requirements

- .NET 10 SDK
- SQL Server (LocalDB, Express, or full instance)
- Internet access on first run (MapLibre, Font Awesome, and the SignalR client load from CDN)

### Run

```bash
git clone https://github.com/jv-droid/RomblonHealthConnect.git
cd RomblonHealthConnect/RomblonHealthConnect
dotnet run
```

The connection string lives in `appsettings.json` and points at LocalDB by default. On startup the app applies any pending migrations and seeds demo data if the database is empty, so no manual migration step is needed.

Open the URL shown in the console (see `Properties/launchSettings.json`; currently `http://localhost:5022`).

### Reset the database

```bash
sqlcmd -S "(localdb)\mssqllocaldb" -Q "DROP DATABASE RomblonHealthConnect;"
dotnet run
```

### Working with migrations

The EF CLI must match EF Core 10:

```bash
dotnet tool update --global dotnet-ef --version "10.*"
dotnet ef migrations add <Name> --output-dir Data/Migrations
```

If the app is running, its Debug output is locked. Add `--configuration Release` to work around it without stopping the app.

## Project Structure

```
RomblonHealthConnect/
├── Configurations/   EF Core entity configurations (Fluent API)
├── Constants/        Shared constant values
├── Controllers/      HomeController, ReferralsController
├── Data/             ApplicationDbContext and Migrations/
├── Extensions/       Service registration helpers
├── Features/         Reserved for future feature modules
├── Helpers/          Utility classes
├── Hubs/             ReferralHub (SignalR)
├── Interfaces/       Repository and service contracts
├── Logs/             Reserved for Serilog file output
├── Mapping/          Reserved for Mapster configuration
├── Middleware/       Custom middleware
├── Models/           Domain entities and Enums/
├── Repositories/     EF Core data access
├── SeedData/         DatabaseSeeder
├── Services/         Referral, notification, file storage, current facility
├── ViewModels/       Referrals/ view models
├── Views/            Home/, Referrals/, Shared/
└── wwwroot/          css/, js/, lib/, uploads/
```

## Data Model

Nine tables, created by the `InitialReferralEngine` migration.

| Entity | Notes |
|--------|-------|
| `Hospital` | Facility with coordinates, status, beds, services. `Code` matches the GIS dashboard. |
| `Doctor` | Belongs to a hospital, has a primary specialization and an availability state |
| `Specialization` | Clinical specialty; primary-care ones are flagged |
| `DoctorSpecialization` | Join table for secondary specialties |
| `Patient` | Demographics and patient number |
| `Referral` | Aggregate root. Two FKs to `Hospital` (origin, destination) and two to `Doctor` (assigned, referring). |
| `ReferralHistory` | Append-only audit trail; drives the timeline |
| `ReferralAttachment` | File metadata; files live under `wwwroot/uploads/referrals` |
| `Notification` | Targeted at a facility, surfaced in the notification centre |

**Referral states:** Draft, Submitted, Accepted, Rejected, Cancelled, Completed, Expired.
**Priorities:** Routine, Urgent, Emergency — these set the response deadline (72h, 12h, 2h).

### Design decisions worth remembering

- `ReferralStatus` is a C# enum stored as `int`, not a lookup table. Promote it to a table if statuses ever need admin-editable metadata.
- Both `Doctor` foreign keys on `Referral` use `DeleteBehavior.Restrict`. Two `SET NULL` paths from the same table trigger SQL Server error 1785 (multiple cascade paths). Doctors are retired with `IsActive` rather than deleted.
- `GetByIdAsync` uses `AsSplitQuery()` because it includes two collections.
- The create-referral wizard is client-side; the whole form posts once from step 7 as multipart.

## Routes

| Route | Purpose |
|-------|---------|
| `/` | GIS dashboard |
| `/Referrals` | Referral dashboard |
| `/Referrals/Create` | Seven-step wizard |
| `/Referrals/Incoming` `/Outgoing` `/Pending` `/Completed` `/Archive` | Queues |
| `/Referrals/Details/{id}` | Record, attachments, timeline, actions |
| `/hubs/referrals` | SignalR hub |

JSON endpoints used by the client: `HospitalCapability`, `AvailableDoctors`, `SearchPatients`, `Notifications`, `CurrentFacility`.

## Front-end Assets

| File | Role |
|------|------|
| `css/site.css` | Design tokens, app shell, sidebar, header |
| `css/dashboard.css` | GIS dashboard components |
| `css/referrals.css` | Referral module components |
| `js/dashboard.js` | GIS dashboard sample data and rendering |
| `js/health-map.js` | MapLibre map, shared by the dashboard and the wizard |
| `js/referrals.js` | Notification centre, toasts, filters |
| `js/referral-wizard.js` | Wizard state machine and map integration |
| `js/referral-realtime.js` | SignalR client |

`health-map.js` is shared unchanged between Phase 2 and the wizard. Both pages publish the same small contract (`RHC.data.facilities`, `RHC.getFacility`, `RHC.openFacility`) and let that module own the map.

## Next Steps

1. **Authentication and roles** — ASP.NET Core Identity, then replace `ICurrentFacilityProvider` with a claim so each user acts for their real facility
2. Point the GIS dashboard at the database instead of `dashboard.js` sample data
3. Background job to expire overdue referrals
4. Wire up Serilog, then Mapster for the hand-written view model mapping
5. Reports and PDF export with QuestPDF
6. Appointments and schedules module

## Author

Jayvee Molino
