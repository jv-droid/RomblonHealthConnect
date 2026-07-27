using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.SeedData;

/// <summary>
/// Populates the database with illustrative Romblon data for design review and demos.
/// Facility codes and coordinates match the Phase 2 GIS dashboard so both modules
/// describe the same network. Safe to call on every startup — it exits if data exists.
/// </summary>
public static class DatabaseSeeder
{
    // Fixed seed keeps demo data identical between rebuilds.
    private static readonly Random Random = new(20260728);

    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await context.Hospitals.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed data already present; skipping.");
            return;
        }

        logger.LogInformation("Seeding Romblon HealthConnect demo data...");

        var specializations = BuildSpecializations();
        await context.Specializations.AddRangeAsync(specializations, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var hospitals = BuildHospitals();
        await context.Hospitals.AddRangeAsync(hospitals, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var doctors = BuildDoctors(hospitals, specializations);
        await context.Doctors.AddRangeAsync(doctors, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var patients = BuildPatients();
        await context.Patients.AddRangeAsync(patients, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var referrals = BuildReferrals(hospitals, doctors, patients, specializations);
        await context.Referrals.AddRangeAsync(referrals, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var notifications = BuildNotifications(referrals);
        await context.Notifications.AddRangeAsync(notifications, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Hospitals} hospitals, {Doctors} doctors, {Patients} patients, {Referrals} referrals.",
            hospitals.Count, doctors.Count, patients.Count, referrals.Count);
    }

    /* ------------------------------------------------------------------ */

    private static List<Specialization> BuildSpecializations() =>
    [
        new() { Name = "Internal Medicine", Description = "Adult general medicine" },
        new() { Name = "General Surgery", Description = "Operative management of surgical conditions" },
        new() { Name = "Pediatrics", Description = "Infant, child, and adolescent care" },
        new() { Name = "Obstetrics and Gynecology", Description = "Pregnancy and women's health" },
        new() { Name = "Anesthesiology", Description = "Peri-operative anesthesia" },
        new() { Name = "Radiology", Description = "Diagnostic imaging" },
        new() { Name = "Cardiology", Description = "Heart and vascular conditions" },
        new() { Name = "Orthopedics", Description = "Musculoskeletal injuries" },
        new() { Name = "Dermatology", Description = "Skin conditions" },
        new() { Name = "Ophthalmology", Description = "Eye care" },
        new() { Name = "Emergency Medicine", Description = "Acute and trauma care" },
        new() { Name = "General Practice", Description = "Primary care", IsPrimaryCare = true },
        new() { Name = "Maternal Health", Description = "Prenatal and postnatal services", IsPrimaryCare = true },
        new() { Name = "Immunization", Description = "Vaccination programmes", IsPrimaryCare = true },
        new() { Name = "Laboratory", Description = "Diagnostic laboratory services", IsPrimaryCare = true }
    ];

    private static List<Hospital> BuildHospitals()
    {
        var now = DateTime.UtcNow;

        return
        [
            New("rph-romblon", "Romblon Provincial Hospital", FacilityType.Public, "Romblon",
                "Barangay Capaclan, Romblon, Romblon", "(042) 567 1234", 12.5764, 122.2708,
                FacilityStatus.Online, true, 120, 32,
                "Emergency, Surgery, Laboratory, Radiology, Blood Bank, Intensive Care", now.AddMinutes(-3)),

            New("tidh-odiongan", "Tablas Island District Hospital", FacilityType.District, "Odiongan",
                "Barangay Dapawan, Odiongan, Romblon", "(042) 567 5580", 12.4003, 121.9889,
                FacilityStatus.Online, true, 75, 18,
                "Emergency, Surgery, Laboratory, Maternity, X-Ray", now.AddMinutes(-6)),

            New("rdh-romblon", "Romblon District Hospital", FacilityType.District, "Romblon",
                "Barangay Bagacay, Romblon, Romblon", "(042) 567 2210", 12.5698, 122.2609,
                FacilityStatus.Online, true, 50, 11,
                "Emergency, Laboratory, Maternity, Out-patient", now.AddMinutes(-11)),

            New("adh-alcantara", "Alcantara District Hospital", FacilityType.District, "Alcantara",
                "Poblacion, Alcantara, Romblon", "(042) 567 3341", 12.2333, 122.0667,
                FacilityStatus.Online, true, 40, 4,
                "Emergency, Laboratory, Maternity", now.AddMinutes(-8)),

            New("cdh-cajidiocan", "Cajidiocan District Hospital", FacilityType.District, "Cajidiocan",
                "Poblacion, Cajidiocan, Sibuyan Island", "(042) 567 4419", 12.4394, 122.5308,
                FacilityStatus.Limited, true, 35, 7,
                "Emergency, Laboratory, Out-patient", now.AddMinutes(-24)),

            New("sfdh-sanfernando", "San Fernando District Hospital", FacilityType.District, "San Fernando",
                "Poblacion, San Fernando, Sibuyan Island", "(042) 567 4802", 12.3175, 122.5461,
                FacilityStatus.Online, true, 30, 9,
                "Emergency, Laboratory, Maternity", now.AddMinutes(-15)),

            New("rhu-sanagustin", "San Agustin Rural Health Unit", FacilityType.RuralHealthUnit, "San Agustin",
                "Poblacion, San Agustin, Tablas Island", "(042) 567 6120", 12.6167, 122.1333,
                FacilityStatus.Online, false, 12, 6,
                "Out-patient, Maternal Health, Immunization", now.AddMinutes(-19)),

            New("rhu-sanandres", "San Andres Rural Health Unit", FacilityType.RuralHealthUnit, "San Andres",
                "Poblacion, San Andres, Tablas Island", "(042) 567 6255", 12.5167, 122.0333,
                FacilityStatus.Online, false, 10, 5,
                "Out-patient, Immunization", now.AddMinutes(-27)),

            New("rhu-odiongan", "Odiongan Rural Health Unit", FacilityType.RuralHealthUnit, "Odiongan",
                "Poblacion, Odiongan, Tablas Island", "(042) 567 5612", 12.4118, 121.9975,
                FacilityStatus.Online, false, 15, 8,
                "Out-patient, Maternal Health, Immunization, Laboratory", now.AddMinutes(-5)),

            New("rhu-magdiwang", "Magdiwang Rural Health Unit", FacilityType.RuralHealthUnit, "Magdiwang",
                "Poblacion, Magdiwang, Sibuyan Island", "(042) 567 4703", 12.4972, 122.5217,
                FacilityStatus.Online, false, 10, 4,
                "Out-patient, Maternal Health", now.AddMinutes(-31)),

            New("rhu-looc", "Looc Rural Health Unit", FacilityType.RuralHealthUnit, "Looc",
                "Poblacion, Looc, Tablas Island", "(042) 567 5934", 12.2611, 121.9944,
                FacilityStatus.Online, false, 10, 3,
                "Out-patient, Immunization", now.AddMinutes(-22)),

            New("rhu-santafe", "Santa Fe Rural Health Unit", FacilityType.RuralHealthUnit, "Santa Fe",
                "Poblacion, Santa Fe, Tablas Island", "(042) 567 5177", 12.1500, 122.0333,
                FacilityStatus.Offline, false, 8, 2,
                "Out-patient", now.AddMinutes(-96)),

            New("rhu-corcuera", "Corcuera Rural Health Unit", FacilityType.RuralHealthUnit, "Corcuera",
                "Poblacion, Corcuera, Simara Island", "(042) 567 6488", 12.6333, 122.1667,
                FacilityStatus.Limited, false, 8, 3,
                "Out-patient", now.AddMinutes(-44)),

            New("tmc-odiongan", "Tablas Medical Center", FacilityType.Private, "Odiongan",
                "Barangay Liwayway, Odiongan, Romblon", "(042) 567 5900", 12.3946, 121.9820,
                FacilityStatus.Online, true, 45, 14,
                "Emergency, Surgery, Cardiology, Radiology, Laboratory, Dialysis", now.AddMinutes(-9)),

            New("shmc-romblon", "Sacred Heart Medical Clinic", FacilityType.Private, "Romblon",
                "Barangay Ilauran, Romblon, Romblon", "(042) 567 2077", 12.5821, 122.2782,
                FacilityStatus.Online, false, 12, 5,
                "Out-patient, Dermatology, Laboratory", now.AddMinutes(-13))
        ];

        static Hospital New(string code, string name, FacilityType type, string municipality, string address,
            string contact, double lat, double lng, FacilityStatus status, bool emergency,
            int totalBeds, int availableBeds, string services, DateTime updated) => new()
            {
                Code = code,
                Name = name,
                FacilityType = type,
                Municipality = municipality,
                Address = address,
                ContactNumber = contact,
                Email = $"{code}@romblonhealth.gov.ph",
                Latitude = lat,
                Longitude = lng,
                Status = status,
                HasEmergency = emergency,
                TotalBeds = totalBeds,
                AvailableBeds = availableBeds,
                Services = services,
                LastUpdatedUtc = updated,
                IsActive = true
            };
    }

    private static List<Doctor> BuildDoctors(List<Hospital> hospitals, List<Specialization> specializations)
    {
        var doctors = new List<Doctor>();
        var licence = 100_000;

        void Add(string first, string last, string hospitalCode, string specialty, DoctorAvailability availability)
        {
            var hospital = hospitals.First(h => h.Code == hospitalCode);
            var specialization = specializations.First(s => s.Name == specialty);

            doctors.Add(new Doctor
            {
                FirstName = first,
                LastName = last,
                LicenseNumber = $"PRC-{++licence}",
                Hospital = hospital,
                PrimarySpecialization = specialization,
                Availability = availability,
                ContactNumber = $"0917 {Random.Next(100, 999)} {Random.Next(1000, 9999)}",
                Email = $"{first[0]}.{last}@romblonhealth.gov.ph".ToLowerInvariant(),
                IsActive = true
            });
        }

        // Romblon Provincial Hospital — the referral hub for the province.
        Add("Marisol", "Fabreag", "rph-romblon", "Internal Medicine", DoctorAvailability.Available);
        Add("Ramon", "Fadri", "rph-romblon", "General Surgery", DoctorAvailability.InSurgery);
        Add("Elena", "Solidum", "rph-romblon", "Anesthesiology", DoctorAvailability.OnCall);
        Add("Teresa", "Madrid", "rph-romblon", "Pediatrics", DoctorAvailability.Available);
        Add("Ignacio", "Mayor", "rph-romblon", "Obstetrics and Gynecology", DoctorAvailability.Available);
        Add("Beatriz", "Servañez", "rph-romblon", "Radiology", DoctorAvailability.Available);
        Add("Alfonso", "Riano", "rph-romblon", "Orthopedics", DoctorAvailability.OnCall);
        Add("Cristina", "Molina", "rph-romblon", "Emergency Medicine", DoctorAvailability.Available);

        // Tablas Island District Hospital
        Add("Lourdes", "Mindoro", "tidh-odiongan", "Pediatrics", DoctorAvailability.Available);
        Add("Alberto", "Gaa", "tidh-odiongan", "Obstetrics and Gynecology", DoctorAvailability.Available);
        Add("Nestor", "Fabella", "tidh-odiongan", "General Surgery", DoctorAvailability.OnCall);
        Add("Imelda", "Rosales", "tidh-odiongan", "Internal Medicine", DoctorAvailability.Available);
        Add("Danilo", "Fabon", "tidh-odiongan", "Emergency Medicine", DoctorAvailability.Available);

        // Romblon District Hospital
        Add("Corazon", "Malacas", "rdh-romblon", "Internal Medicine", DoctorAvailability.Available);
        Add("Federico", "Mortel", "rdh-romblon", "Pediatrics", DoctorAvailability.OffDuty);
        Add("Salome", "Rondael", "rdh-romblon", "General Practice", DoctorAvailability.Available);

        // Alcantara District Hospital
        Add("Perlita", "Musico", "adh-alcantara", "General Practice", DoctorAvailability.Available);
        Add("Rogelio", "Manzo", "adh-alcantara", "Internal Medicine", DoctorAvailability.Available);
        Add("Amparo", "Fajilan", "adh-alcantara", "Obstetrics and Gynecology", DoctorAvailability.OnCall);

        // Cajidiocan District Hospital
        Add("Virgilio", "Rufo", "cdh-cajidiocan", "General Practice", DoctorAvailability.Available);
        Add("Norma", "Fabroa", "cdh-cajidiocan", "Pediatrics", DoctorAvailability.OffDuty);

        // San Fernando District Hospital
        Add("Vicente", "Faigao", "sfdh-sanfernando", "General Practice", DoctorAvailability.Available);
        Add("Estrella", "Fetalvero", "sfdh-sanfernando", "Internal Medicine", DoctorAvailability.Available);

        // Rural health units
        Add("Josefina", "Fadrilan", "rhu-sanagustin", "General Practice", DoctorAvailability.Available);
        Add("Manuel", "Galicia", "rhu-sanagustin", "Maternal Health", DoctorAvailability.Available);
        Add("Rosario", "Mendoza", "rhu-sanandres", "General Practice", DoctorAvailability.Available);
        Add("Antonio", "Fetalino", "rhu-odiongan", "General Practice", DoctorAvailability.Available);
        Add("Milagros", "Fernandez", "rhu-odiongan", "Maternal Health", DoctorAvailability.Available);
        Add("Eduardo", "Faminial", "rhu-magdiwang", "General Practice", DoctorAvailability.Available);
        Add("Purificacion", "Falcutila", "rhu-looc", "General Practice", DoctorAvailability.Available);
        Add("Bernardo", "Fabricante", "rhu-corcuera", "General Practice", DoctorAvailability.OnCall);
        Add("Leonora", "Fabiala", "rhu-santafe", "General Practice", DoctorAvailability.OffDuty);

        // Private facilities
        Add("Carlos", "Rioflorido", "tmc-odiongan", "Cardiology", DoctorAvailability.OnCall);
        Add("Angelica", "Sarmiento", "tmc-odiongan", "Internal Medicine", DoctorAvailability.Available);
        Add("Bienvenido", "Tansiongco", "tmc-odiongan", "General Surgery", DoctorAvailability.Available);
        Add("Remedios", "Solis", "tmc-odiongan", "Radiology", DoctorAvailability.Available);
        Add("Gloria", "Villanueva", "shmc-romblon", "Dermatology", DoctorAvailability.Available);
        Add("Rafael", "Montojo", "shmc-romblon", "General Practice", DoctorAvailability.Available);

        return doctors;
    }

    private static List<Patient> BuildPatients()
    {
        var seeds = new (string First, string? Middle, string Last, int Year, int Month, int Day, Sex Sex,
            string Municipality, string Blood)[]
        {
            ("Juanito", "Rivera", "Fadriquela", 1958, 3, 14, Sex.Male, "Romblon", "O+"),
            ("Maria Elena", "Sales", "Gadon", 1972, 7, 2, Sex.Female, "Odiongan", "A+"),
            ("Rodrigo", null, "Mateo", 1965, 11, 23, Sex.Male, "Looc", "B+"),
            ("Anabelle", "Cruz", "Fetil", 1990, 1, 30, Sex.Female, "San Agustin", "O-"),
            ("Ernesto", "Lim", "Rebueno", 1948, 5, 8, Sex.Male, "Cajidiocan", "AB+"),
            ("Divina", null, "Morales", 1985, 9, 17, Sex.Female, "Magdiwang", "A-"),
            ("Feliciano", "Ramos", "Sacapaño", 1979, 12, 4, Sex.Male, "San Fernando", "O+"),
            ("Marilou", "Dela Cruz", "Fabregas", 1994, 4, 21, Sex.Female, "Alcantara", "B-"),
            ("Nicanor", null, "Rodriguez", 1961, 8, 12, Sex.Male, "Santa Fe", "O+"),
            ("Editha", "Vega", "Fallarme", 1976, 2, 27, Sex.Female, "Odiongan", "A+"),
            ("Bonifacio", "Reyes", "Manzano", 1955, 6, 19, Sex.Male, "Romblon", "B+"),
            ("Consolacion", null, "Fajardo", 1988, 10, 6, Sex.Female, "San Andres", "O+"),
            ("Arturo", "Santos", "Fadera", 1970, 3, 25, Sex.Male, "Corcuera", "AB-"),
            ("Lolita", "Buenaflor", "Ruedas", 1963, 7, 11, Sex.Female, "Romblon", "A+"),
            ("Gregorio", null, "Fabiaña", 2011, 5, 3, Sex.Male, "Odiongan", "O+"),
            ("Norma", "Aguilar", "Festin", 1982, 11, 15, Sex.Female, "Looc", "B+"),
            ("Wilfredo", "Torres", "Fabon", 1996, 1, 9, Sex.Male, "Alcantara", "O-"),
            ("Salvacion", null, "Mirafuentes", 1959, 9, 29, Sex.Female, "San Agustin", "A+"),
            ("Nelson", "Ilagan", "Fabile", 2018, 8, 22, Sex.Male, "Romblon", "O+"),
            ("Aurora", "Del Rosario", "Fadrigo", 1974, 12, 13, Sex.Female, "Cajidiocan", "B+")
        };

        var patients = new List<Patient>();
        var sequence = 0;

        foreach (var s in seeds)
        {
            patients.Add(new Patient
            {
                PatientNumber = $"PT-2026-{++sequence:D5}",
                FirstName = s.First,
                MiddleName = s.Middle,
                LastName = s.Last,
                DateOfBirth = new DateOnly(s.Year, s.Month, s.Day),
                Sex = s.Sex,
                ContactNumber = $"0918 {Random.Next(100, 999)} {Random.Next(1000, 9999)}",
                Address = $"Barangay Poblacion, {s.Municipality}, Romblon",
                Municipality = s.Municipality,
                BloodType = s.Blood,
                IsActive = true
            });
        }

        return patients;
    }

    private static List<Referral> BuildReferrals(
        List<Hospital> hospitals,
        List<Doctor> doctors,
        List<Patient> patients,
        List<Specialization> specializations)
    {
        var referrals = new List<Referral>();
        var now = DateTime.UtcNow;
        var sequence = 0;

        // (patientIndex, originCode, destinationCode, specialty, status, priority, hoursAgo, reason)
        var plan = new (int Patient, string From, string To, string Specialty, ReferralStatus Status,
            ReferralPriority Priority, int HoursAgo, string Reason)[]
        {
            (0, "rhu-looc", "tidh-odiongan", "Internal Medicine", ReferralStatus.Accepted,
                ReferralPriority.Urgent, 2, "Uncontrolled hypertension with chest discomfort."),
            (1, "rhu-magdiwang", "cdh-cajidiocan", "General Practice", ReferralStatus.Submitted,
                ReferralPriority.Routine, 3, "Persistent cough unresponsive to outpatient treatment."),
            (2, "adh-alcantara", "rph-romblon", "Orthopedics", ReferralStatus.Submitted,
                ReferralPriority.Emergency, 1, "Closed fracture of the left femur after a fall."),
            (3, "rhu-sanandres", "tidh-odiongan", "Obstetrics and Gynecology", ReferralStatus.Accepted,
                ReferralPriority.Urgent, 5, "Pre-eclampsia at 34 weeks gestation."),
            (4, "rhu-corcuera", "rph-romblon", "Internal Medicine", ReferralStatus.Completed,
                ReferralPriority.Urgent, 30, "Decompensated heart failure requiring admission."),
            (5, "sfdh-sanfernando", "rph-romblon", "General Surgery", ReferralStatus.Completed,
                ReferralPriority.Routine, 52, "Elective cholecystectomy for symptomatic gallstones."),
            (6, "rhu-odiongan", "tmc-odiongan", "Cardiology", ReferralStatus.Accepted,
                ReferralPriority.Routine, 8, "Abnormal ECG requiring specialist review."),
            (7, "rhu-sanagustin", "rdh-romblon", "Pediatrics", ReferralStatus.Rejected,
                ReferralPriority.Routine, 26, "Recurrent febrile seizures in a four-year-old."),
            (8, "rhu-santafe", "adh-alcantara", "General Practice", ReferralStatus.Expired,
                ReferralPriority.Routine, 120, "Chronic wound requiring debridement."),
            (9, "rhu-odiongan", "tidh-odiongan", "General Surgery", ReferralStatus.Submitted,
                ReferralPriority.Urgent, 4, "Acute appendicitis suspected on examination."),
            (10, "rdh-romblon", "rph-romblon", "Radiology", ReferralStatus.Completed,
                ReferralPriority.Routine, 74, "CT imaging unavailable at origin facility."),
            (11, "rhu-sanandres", "rph-romblon", "Emergency Medicine", ReferralStatus.Cancelled,
                ReferralPriority.Emergency, 44, "Patient transferred privately before dispatch."),
            (12, "rhu-corcuera", "rdh-romblon", "Internal Medicine", ReferralStatus.Submitted,
                ReferralPriority.Routine, 6, "Poorly controlled diabetes with foot ulcer."),
            (13, "shmc-romblon", "rph-romblon", "General Surgery", ReferralStatus.Accepted,
                ReferralPriority.Routine, 12, "Breast mass requiring excision biopsy."),
            (14, "rhu-odiongan", "tidh-odiongan", "Pediatrics", ReferralStatus.Completed,
                ReferralPriority.Urgent, 96, "Severe dehydration secondary to gastroenteritis."),
            (15, "rhu-looc", "adh-alcantara", "Obstetrics and Gynecology", ReferralStatus.Accepted,
                ReferralPriority.Urgent, 9, "Prolonged labour requiring assisted delivery."),
            (16, "adh-alcantara", "tmc-odiongan", "Radiology", ReferralStatus.Submitted,
                ReferralPriority.Routine, 7, "Ultrasound required for abdominal mass."),
            (17, "rhu-sanagustin", "rph-romblon", "Ophthalmology", ReferralStatus.Draft,
                ReferralPriority.Routine, 1, "Progressive vision loss over three months."),
            (18, "rdh-romblon", "rph-romblon", "Pediatrics", ReferralStatus.Accepted,
                ReferralPriority.Emergency, 3, "Neonatal jaundice requiring phototherapy."),
            (19, "cdh-cajidiocan", "rph-romblon", "Internal Medicine", ReferralStatus.Completed,
                ReferralPriority.Urgent, 140, "Suspected stroke requiring neuro-imaging.")
        };

        foreach (var item in plan)
        {
            var origin = hospitals.First(h => h.Code == item.From);
            var destination = hospitals.First(h => h.Code == item.To);
            var specialization = specializations.First(s => s.Name == item.Specialty);
            var createdUtc = now.AddHours(-item.HoursAgo);

            var referral = new Referral
            {
                ReferralNumber = $"RF-2026-{++sequence:D4}",
                Patient = patients[item.Patient],
                OriginHospital = origin,
                DestinationHospital = destination,
                RequestedSpecialization = specialization,
                ReferringDoctor = doctors.FirstOrDefault(d => d.Hospital.Code == item.From),
                Status = item.Status,
                Priority = item.Priority,
                ReasonForReferral = item.Reason,
                Diagnosis = item.Reason.Split('.')[0],
                ClinicalNotes = "Vital signs stable on transfer. Referral prepared from the origin facility record.",
                CreatedUtc = createdUtc,
                // Everything older than five days is filed away.
                IsArchived = item.HoursAgo > 120
            };

            BuildTimeline(referral, item.Status, createdUtc, doctors, destination);
            referrals.Add(referral);
        }

        return referrals;
    }

    /// <summary>Replays the workflow so each seeded referral has a believable audit trail.</summary>
    private static void BuildTimeline(
        Referral referral,
        ReferralStatus target,
        DateTime createdUtc,
        List<Doctor> doctors,
        Hospital destination)
    {
        void Log(ReferralAction action, ReferralStatus? from, ReferralStatus? to, string notes, DateTime at) =>
            referral.History.Add(new ReferralHistory
            {
                Action = action,
                FromStatus = from,
                ToStatus = to,
                Notes = notes,
                PerformedBy = "Provincial Administrator",
                PerformedAtUtc = at
            });

        Log(ReferralAction.Created, null, ReferralStatus.Draft, "Referral drafted.", createdUtc);

        if (target == ReferralStatus.Draft)
        {
            return;
        }

        var submittedUtc = createdUtc.AddMinutes(3);
        referral.SubmittedUtc = submittedUtc;
        referral.ExpiresUtc = submittedUtc.AddHours(target == ReferralStatus.Expired ? 2 : 72);
        Log(ReferralAction.Submitted, ReferralStatus.Draft, ReferralStatus.Submitted,
            "Referral sent to the receiving facility.", submittedUtc);

        switch (target)
        {
            case ReferralStatus.Submitted:
                return;

            case ReferralStatus.Rejected:
                referral.RespondedUtc = submittedUtc.AddMinutes(41);
                referral.ResponseNotes = "No pediatric bed available; please route to the provincial hospital.";
                Log(ReferralAction.Rejected, ReferralStatus.Submitted, ReferralStatus.Rejected,
                    referral.ResponseNotes, referral.RespondedUtc.Value);
                return;

            case ReferralStatus.Cancelled:
                Log(ReferralAction.Cancelled, ReferralStatus.Submitted, ReferralStatus.Cancelled,
                    "Cancelled by the referring facility.", submittedUtc.AddMinutes(22));
                return;

            case ReferralStatus.Expired:
                Log(ReferralAction.Expired, ReferralStatus.Submitted, ReferralStatus.Expired,
                    "No response received within the required window.", referral.ExpiresUtc.Value);
                return;
        }

        // Accepted and Completed both pass through acceptance and doctor assignment.
        var acceptedUtc = submittedUtc.AddMinutes(9);
        referral.RespondedUtc = acceptedUtc;
        referral.ResponseNotes = "Bed reserved. Please send the patient with the referral packet.";
        Log(ReferralAction.Accepted, ReferralStatus.Submitted, ReferralStatus.Accepted,
            referral.ResponseNotes, acceptedUtc);

        var doctor = doctors.FirstOrDefault(d =>
            d.Hospital.Code == destination.Code
            && d.PrimarySpecialization.Name == referral.RequestedSpecialization.Name)
            ?? doctors.FirstOrDefault(d => d.Hospital.Code == destination.Code);

        if (doctor is not null)
        {
            referral.AssignedDoctor = doctor;
            Log(ReferralAction.DoctorAssigned, ReferralStatus.Accepted, ReferralStatus.Accepted,
                $"{doctor.FullName} assigned to the patient.", acceptedUtc.AddMinutes(4));
        }

        var scheduledUtc = acceptedUtc.AddMinutes(14);
        referral.ScheduledUtc = scheduledUtc;
        Log(ReferralAction.PatientScheduled, ReferralStatus.Accepted, ReferralStatus.Accepted,
            "Patient scheduled for admission.", scheduledUtc);

        if (target == ReferralStatus.Completed)
        {
            var completedUtc = scheduledUtc.AddHours(2);
            referral.CompletedUtc = completedUtc;
            Log(ReferralAction.Completed, ReferralStatus.Accepted, ReferralStatus.Completed,
                "Patient seen and referral closed.", completedUtc);
        }
    }

    private static List<Notification> BuildNotifications(List<Referral> referrals)
    {
        var notifications = new List<Notification>();

        foreach (var referral in referrals.Where(r => r.Status != ReferralStatus.Draft).Take(12))
        {
            var (type, title, message, target) = referral.Status switch
            {
                ReferralStatus.Accepted => (NotificationType.ReferralAccepted, "Referral accepted",
                    $"{referral.DestinationHospital.Name} accepted {referral.ReferralNumber}.",
                    referral.OriginHospital),

                ReferralStatus.Rejected => (NotificationType.ReferralRejected, "Referral rejected",
                    $"{referral.DestinationHospital.Name} rejected {referral.ReferralNumber}.",
                    referral.OriginHospital),

                ReferralStatus.Completed => (NotificationType.ReferralCompleted, "Referral completed",
                    $"{referral.ReferralNumber} has been completed.",
                    referral.OriginHospital),

                _ => (NotificationType.ReferralReceived, "New referral received",
                    $"{referral.OriginHospital.Name} referred {referral.Patient.FullName} " +
                    $"({referral.ReferralNumber}).",
                    referral.DestinationHospital)
            };

            notifications.Add(new Notification
            {
                Type = type,
                Title = title,
                Message = message,
                Hospital = target,
                Referral = referral,
                CreatedUtc = referral.SubmittedUtc ?? referral.CreatedUtc,
                IsRead = referral.Status is ReferralStatus.Completed
            });
        }

        return notifications;
    }
}
