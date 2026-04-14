using ClimbBilling.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClimbBilling.Web.Data;

/// <summary>
/// Seeds realistic demo data in Development so you can explore the app
/// without entering everything manually. Only runs when the DB is empty.
/// </summary>
public static class DevSeedData
{
    public static async Task SeedAsync(BillingDbContext db)
    {
        if (await db.Instructors.AnyAsync()) return; // already seeded

        // ── Instructor ──────────────────────────────────────────────
        var instructor = new Instructor
        {
            TmsUserId    = "demo@climbaviation.com",
            DisplayName  = "Sarah Mitchell",
            BusinessName = "Mitchell Flight Training",
            Email        = "sarah@mitchellflight.com",
            Phone        = "555-867-5309",
            IsActive     = true,
            // Simulate Stripe connected so payment-link button shows
            StripeConnectAccountId  = "acct_demo_placeholder",
            StripeOnboardingComplete = false,   // true would require real Stripe
            SubscriptionStatus       = SubscriptionStatus.None,
        };
        db.Instructors.Add(instructor);
        await db.SaveChangesAsync();

        // ── Rates ────────────────────────────────────────────────────
        var rates = new[]
        {
            new Rate { InstructorId = instructor.Id, Type = RateType.Instruction,      Label = "Dual Instruction (CFI)",        HourlyRate = 65.00m,  IsActive = true },
            new Rate { InstructorId = instructor.Id, Type = RateType.GroundInstruction, Label = "Ground Instruction",            HourlyRate = 45.00m,  IsActive = true },
            new Rate { InstructorId = instructor.Id, Type = RateType.AircraftRental,    Label = "Cessna 172 (N12345) — Wet",     HourlyRate = 130.00m, IsActive = true },
            new Rate { InstructorId = instructor.Id, Type = RateType.AircraftRental,    Label = "Piper Archer (N98765) — Wet",   HourlyRate = 145.00m, IsActive = true },
            new Rate { InstructorId = instructor.Id, Type = RateType.SimulatorRental,   Label = "Redbird TD2 Simulator",         HourlyRate = 55.00m,  IsActive = true },
            new Rate { InstructorId = instructor.Id, Type = RateType.Instruction,      Label = "Instrument Dual (CFII)",        HourlyRate = 70.00m,  IsActive = true },
        };
        db.Rates.AddRange(rates);
        await db.SaveChangesAsync();

        // ── Students ─────────────────────────────────────────────────
        var jake   = new Student { InstructorId = instructor.Id, Name = "Jake Thornton",    Email = "jake.thornton@email.com",  Phone = "555-201-4411", Notes = "PPL student, ~30 hrs TT, working on solo XC",        IsActive = true };
        var priya  = new Student { InstructorId = instructor.Id, Name = "Priya Nair",       Email = "priya.nair@gmail.com",     Phone = "555-334-8820", Notes = "Instrument rating, already holds PPL",                IsActive = true };
        var marcus = new Student { InstructorId = instructor.Id, Name = "Marcus Webb",      Email = "mwebb@outlook.com",        Phone = "555-779-0033", Notes = "Discovery flight → now pursuing PPL",                 IsActive = true };
        var linda  = new Student { InstructorId = instructor.Id, Name = "Linda Espinoza",   Email = "linda.e@yahoo.com",        Phone = "555-556-1192", Notes = "Commercial checkride prep",                          IsActive = true };
        db.Students.AddRange(jake, priya, marcus, linda);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // ── Invoices ─────────────────────────────────────────────────

        // 1. Jake — paid invoice (last month)
        var inv1 = new Invoice
        {
            InvoiceNumber = "INV-2026-0001",
            InstructorId  = instructor.Id,
            StudentId     = jake.Id,
            InvoiceDate   = now.AddMonths(-1).AddDays(-5),
            DueDate       = now.AddMonths(-1).AddDays(25),
            Status        = InvoiceStatus.Paid,
            PaidAt        = now.AddMonths(-1).AddDays(3),
            Notes         = "N12345 — dual XC to KBDR and back. Great crosswind work.",
            LineItems = new List<InvoiceLineItem>
            {
                new() { Type = LineItemType.Instruction,   Description = "Dual Instruction (CFI)",      Quantity = 2.4m, UnitPrice = 65.00m,  ServiceDate = now.AddMonths(-1).AddDays(-6) },
                new() { Type = LineItemType.AircraftRental, Description = "Cessna 172 (N12345) — Wet",  Quantity = 2.4m, UnitPrice = 130.00m, ServiceDate = now.AddMonths(-1).AddDays(-6) },
            },
            Payments = new List<Payment>
            {
                new() { Amount = 468.00m, Method = PaymentMethod.ACH, Status = PaymentStatus.Completed, PaymentDate = now.AddMonths(-1).AddDays(3), IsManual = false, StripePaymentIntentId = "pi_demo_001" }
            }
        };

        // 2. Jake — another paid invoice (last month)
        var inv2 = new Invoice
        {
            InvoiceNumber = "INV-2026-0002",
            InstructorId  = instructor.Id,
            StudentId     = jake.Id,
            InvoiceDate   = now.AddMonths(-1).AddDays(-12),
            DueDate       = now.AddMonths(-1).AddDays(18),
            Status        = InvoiceStatus.Paid,
            PaidAt        = now.AddMonths(-1).AddDays(-10),
            Notes         = "Pattern work + ground briefing on weather decision-making.",
            LineItems = new List<InvoiceLineItem>
            {
                new() { Type = LineItemType.Instruction,    Description = "Dual Instruction (CFI)",      Quantity = 1.5m, UnitPrice = 65.00m,  ServiceDate = now.AddMonths(-1).AddDays(-13) },
                new() { Type = LineItemType.AircraftRental, Description = "Cessna 172 (N12345) — Wet",   Quantity = 1.5m, UnitPrice = 130.00m, ServiceDate = now.AddMonths(-1).AddDays(-13) },
                new() { Type = LineItemType.GroundInstruction, Description = "Ground Instruction",        Quantity = 1.0m, UnitPrice = 45.00m,  ServiceDate = now.AddMonths(-1).AddDays(-13) },
            },
            Payments = new List<Payment>
            {
                new() { Amount = 337.50m, Method = PaymentMethod.Venmo, Status = PaymentStatus.Completed, PaymentDate = now.AddMonths(-1).AddDays(-10), IsManual = true, Notes = "Venmo @jake-thornton-42" }
            }
        };

        // 3. Priya — sent, unpaid, due soon (this month)
        var inv3 = new Invoice
        {
            InvoiceNumber    = "INV-2026-0003",
            InstructorId     = instructor.Id,
            StudentId        = priya.Id,
            InvoiceDate      = now.AddDays(-8),
            DueDate          = now.AddDays(7),
            Status           = InvoiceStatus.Sent,
            Notes            = "IFR sim session + actual IMC flight, great approach work.",
            StripePaymentLinkUrl = null,   // no real Stripe in demo
            LineItems = new List<InvoiceLineItem>
            {
                new() { Type = LineItemType.Instruction,    Description = "Instrument Dual (CFII)",      Quantity = 2.0m, UnitPrice = 70.00m,  ServiceDate = now.AddDays(-9) },
                new() { Type = LineItemType.SimulatorRental, Description = "Redbird TD2 Simulator",      Quantity = 1.5m, UnitPrice = 55.00m,  ServiceDate = now.AddDays(-10) },
                new() { Type = LineItemType.AircraftRental, Description = "Piper Archer (N98765) — Wet", Quantity = 1.8m, UnitPrice = 145.00m, ServiceDate = now.AddDays(-9) },
            }
        };

        // 4. Priya — partially paid (this month)
        var inv4 = new Invoice
        {
            InvoiceNumber = "INV-2026-0004",
            InstructorId  = instructor.Id,
            StudentId     = priya.Id,
            InvoiceDate   = now.AddDays(-15),
            DueDate       = now.AddDays(15),
            Status        = InvoiceStatus.PartiallyPaid,
            Notes         = "Sim session — holds and approaches.",
            LineItems = new List<InvoiceLineItem>
            {
                new() { Type = LineItemType.Instruction,    Description = "Instrument Dual (CFII)",  Quantity = 1.0m, UnitPrice = 70.00m, ServiceDate = now.AddDays(-16) },
                new() { Type = LineItemType.SimulatorRental, Description = "Redbird TD2 Simulator",  Quantity = 2.0m, UnitPrice = 55.00m, ServiceDate = now.AddDays(-16) },
            },
            Payments = new List<Payment>
            {
                new() { Amount = 100.00m, Method = PaymentMethod.Cash, Status = PaymentStatus.Completed, PaymentDate = now.AddDays(-14), IsManual = true, Notes = "Cash deposit, balance on next session" }
            }
        };

        // 5. Marcus — overdue (sent 5 weeks ago, due 2 weeks ago)
        var inv5 = new Invoice
        {
            InvoiceNumber = "INV-2026-0005",
            InstructorId  = instructor.Id,
            StudentId     = marcus.Id,
            InvoiceDate   = now.AddDays(-35),
            DueDate       = now.AddDays(-14),
            Status        = InvoiceStatus.Sent,
            Notes         = "Intro flight + 3 lessons. Working toward solo.",
            LineItems = new List<InvoiceLineItem>
            {
                new() { Type = LineItemType.Instruction,    Description = "Dual Instruction (CFI)",     Quantity = 4.5m, UnitPrice = 65.00m,  ServiceDate = now.AddDays(-35) },
                new() { Type = LineItemType.AircraftRental, Description = "Cessna 172 (N12345) — Wet",  Quantity = 4.5m, UnitPrice = 130.00m, ServiceDate = now.AddDays(-35) },
            }
        };

        // 6. Linda — draft (not sent yet, created today)
        var inv6 = new Invoice
        {
            InvoiceNumber = "INV-2026-0006",
            InstructorId  = instructor.Id,
            StudentId     = linda.Id,
            InvoiceDate   = now,
            DueDate       = now.AddDays(30),
            Status        = InvoiceStatus.Draft,
            Notes         = "Commercial maneuvers — chandelles, lazy eights, eights on pylons.",
            LineItems = new List<InvoiceLineItem>
            {
                new() { Type = LineItemType.Instruction,    Description = "Dual Instruction (CFI)",     Quantity = 2.0m, UnitPrice = 65.00m,  ServiceDate = now.AddDays(-1) },
                new() { Type = LineItemType.AircraftRental, Description = "Piper Archer (N98765) — Wet", Quantity = 2.0m, UnitPrice = 145.00m, ServiceDate = now.AddDays(-1) },
            }
        };

        db.Invoices.AddRange(inv1, inv2, inv3, inv4, inv5, inv6);
        await db.SaveChangesAsync();
    }
}
