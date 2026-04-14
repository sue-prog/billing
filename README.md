# Climb Billing

A billing module for freelance flight instructors and small flight schools. Part of the [Climb TMS](https://github.com/sue-prog) ecosystem.

Instructors create invoices for flight instruction and aircraft rental, send payment links to students, and track all payments in one place. A monthly report provides everything needed for bookkeeping.

---

## Table of Contents

- [Payment Methods](#payment-methods)
- [Platform Funding Models](#platform-funding-models)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Configuration Reference](#configuration-reference)
- [Stripe Setup Guide](#stripe-setup-guide)
- [Project Structure](#project-structure)
- [Integration with Climb TMS](#integration-with-climb-tms)
- [Roadmap](#roadmap)

---

## Payment Methods

| Method | How | Stripe Fee | Platform Fee | Notes |
|--------|-----|-----------|--------------|-------|
| **Credit / Debit Card** | Online via Stripe Payment Link | 2.9% + 30¢ | Configurable | Students pay from any device — no app required |
| **ACH Bank Transfer** | Online via Stripe Payment Link | 0.8% (max $5) | Configurable | Best option for large invoices ($150+) |
| **Cash** | Manual log by instructor | None | None | Recorded for bookkeeping only |
| **Check** | Manual log by instructor | None | None | Recorded for bookkeeping only |
| **Venmo** | Manual log by instructor | None | None | Instructor collects via Venmo app; logs it here for records |
| **Zelle** | Manual log by instructor | None | None | Instructor collects via Zelle; logs it here for records |
| **Other** | Manual log by instructor | None | None | Any other method |

### How online payments work

1. Instructor creates an invoice and clicks **Send Invoice**
2. A **Stripe Payment Link** is generated automatically — a URL the instructor can text, email, or share any way they like
3. Student opens the link in a browser, chooses card or ACH, and pays
4. Funds are deposited directly into the instructor's connected bank account (typically 2 business days)
5. The invoice is automatically marked **Paid**

### How manual payments work (cash, Venmo, Zelle, check)

The instructor collects the payment however they normally do (Venmo app, cash in hand, etc.), then clicks **Log Payment** on the invoice to record it. The invoice status updates to Paid and the amount appears in monthly reports. No Stripe account or fees involved.

### ACH vs. Card — Why ACH is better for flight invoices

A typical flight invoice is $200–$500. At that amount:

| Invoice | Card fee | ACH fee | Savings |
|---------|----------|---------|---------|
| $200 | $6.10 | $1.60 | $4.50 |
| $350 | $10.45 | $2.80 | $7.65 |
| $500 | $14.80 | $4.00 | $10.80 |

Both options are presented to the student on the Stripe payment page — they choose.

---

## Platform Funding Models

Two options are available and can be enabled independently or together.

### Option A — Subscription

Instructors pay a monthly subscription fee to use the platform. Implemented via Stripe Billing — the instructor enters their card once and is charged automatically each month.

- Configured in the `PlatformConfig` database table: `SubscriptionEnabled = true`, `SubscriptionMonthlyPrice`, `StripeSubscriptionPriceId`
- Requires creating a Product + Price in the Stripe Dashboard and saving the Price ID in config

### Option B — Per-Transaction Fee

The platform collects a small percentage on every Stripe payment. This is deducted automatically before the instructor receives their payout — no separate charge, no invoice to the instructor.

- Configured in `PlatformConfig`: `PerTransactionFeeEnabled = true`, `PerTransactionFeePercent` (e.g. `0.005` = 0.5%), `PerTransactionFeeFixed` (optional flat amount)
- **Default:** 0.5% per transaction, enabled out of the box
- Only applies to online Stripe payments — manual payments (cash/Venmo/Zelle) have no platform fee

**Example:** On a $300 invoice with a 0.5% platform fee:
- Student pays $300
- Stripe deducts its fee (~$8.70 for card, ~$2.40 for ACH)
- Platform receives $1.50
- Instructor receives the remainder

---

## Features

### Dashboard
- Outstanding balance (total unpaid)
- Amount collected this month and year-to-date
- Overdue invoice count
- Recent invoices list with quick status view
- Stripe connection status banner

### Rates Management
- Define any number of hourly rates
- Types: Flight Instruction, Aircraft Rental, Ground Instruction, Simulator Rental, Other
- Rates are a snapshot — changing a rate does not affect existing invoices
- Active/inactive toggle (inactive rates are hidden from invoice creation)

### Student Management
- Maintain a student roster per instructor
- Email and phone stored for contact
- Invoice history and total billed visible per student
- Optional link to Climb TMS user identity (`TmsUserId`)

### Invoice Creation
- Pick a student, set date and due date
- Add line items: description, hours (quantity), rate, service date
- "Add from Rate" dropdown auto-fills description and rate from saved rates
- Running total updates live as you add/edit items
- Notes field for lesson details, aircraft N-number, etc.

### Invoice Lifecycle
| Status | Meaning |
|--------|---------|
| **Draft** | Created but not yet sent |
| **Sent** | Sent to student; Stripe Payment Link active |
| **Partially Paid** | Some payment received, balance remains |
| **Paid** | Fully paid |
| **Void** | Cancelled |

### Payment Tracking
- Stripe payments recorded automatically via webhook
- Manual payments (cash, Venmo, Zelle, check) logged by instructor
- Full payment history on each invoice with method, date, and notes

### Monthly Report
- Total invoiced, collected, and outstanding
- Revenue breakdown by type (instruction vs. rental)
- Collections breakdown by method (card, ACH, manual)
- Invoice-by-invoice detail table for bookkeeping
- Navigate month-by-month with forward/back arrows

### Stripe Connect Onboarding
- Instructor clicks "Connect Stripe Account"
- Redirected to Stripe's hosted onboarding (KYC, bank account, identity)
- On return, status is refreshed automatically
- Stripe holds and routes all funds — the platform never touches instructor money

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8 MVC |
| Database | Azure SQL / SQL Server via Entity Framework Core 8 |
| Payments | Stripe.net v46 (Stripe Connect Express, Payment Links, Webhooks) |
| UI | Bootstrap 5 + Bootstrap Icons |
| Auth | Cookie auth (dev) / OIDC shared with Climb TMS (production) |
| Hosting | Azure App Service (planned) |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server LocalDB](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio) or any SQL Server instance
- A [Stripe account](https://stripe.com) (free to create; use test mode for development)
- [Stripe CLI](https://stripe.com/docs/stripe-cli) for local webhook testing

### Run locally

```bash
# Clone the repo
git clone https://github.com/sue-prog/billing
cd billing/ClimbBilling.Web

# Add your Stripe test keys to appsettings.Development.json
# (see Configuration Reference below)

# Run — database is auto-created on first launch
dotnet run
```

Browse to `https://localhost:{port}`. Sign in with any email address (dev mode — no real auth required).

### Forward Stripe webhooks locally

```bash
stripe login
stripe listen --forward-to https://localhost:{port}/stripe/webhook
```

Copy the webhook signing secret printed by the CLI into `appsettings.Development.json` as `Stripe:WebhookSecret`.

---

## Configuration Reference

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "BillingDb": "Server=(localdb)\\mssqllocaldb;Database=ClimbBilling_Dev;Trusted_Connection=True;"
  },
  "Stripe": {
    "PublishableKey": "pk_test_...",
    "SecretKey":      "sk_test_...",
    "WebhookSecret":  "whsec_..."
  }
}
```

Get your test keys from [dashboard.stripe.com/test/apikeys](https://dashboard.stripe.com/test/apikeys).

### appsettings.json (production)

```json
{
  "ConnectionStrings": {
    "BillingDb": "Server=...;Database=ClimbBilling;..."
  },
  "Stripe": {
    "PublishableKey": "pk_live_...",
    "SecretKey":      "sk_live_...",
    "WebhookSecret":  "whsec_..."
  },
  "Authentication": {
    "Oidc": {
      "Authority":     "https://login.microsoftonline.com/{tenant}/v2.0",
      "ClientId":      "...",
      "ClientSecret":  "..."
    }
  }
}
```

> **Never commit live Stripe keys.** Use Azure App Service environment variables or Azure Key Vault in production.

---

## Stripe Setup Guide

### For development (test mode)

1. Create a free Stripe account at [stripe.com](https://stripe.com)
2. In the Stripe Dashboard, switch to **Test mode**
3. Copy `pk_test_...` and `sk_test_...` keys into `appsettings.Development.json`
4. Run the app, create an instructor profile, and click **Connect Stripe Account**
5. Complete Stripe's Express onboarding using test data
6. Use Stripe CLI to forward webhooks (see above)

Test card numbers: `4242 4242 4242 4242` (any future expiry, any CVC)

### For production

1. In Stripe Dashboard, complete your platform's own account verification
2. Enable **Connect** in your Stripe Dashboard
3. Create a webhook endpoint pointing to `https://yourdomain.com/stripe/webhook` with these events:
   - `payment_intent.succeeded`
   - `payment_intent.payment_failed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
4. Copy the webhook signing secret into your production config

### Setting up the subscription product (Option A funding)

1. In Stripe Dashboard → Products → Add Product
2. Set a monthly recurring price (e.g. $9.99/month)
3. Copy the **Price ID** (`price_...`)
4. In your database, update `PlatformConfig`: set `SubscriptionEnabled = 1` and `StripeSubscriptionPriceId = 'price_...'`

---

## Project Structure

```
billing/
├── ClimbBilling.sln
└── ClimbBilling.Web/
    ├── Controllers/
    │   ├── DashboardController.cs       # Home screen with financial summary
    │   ├── InstructorsController.cs     # Profile, Stripe onboarding, subscriptions
    │   ├── StudentsController.cs        # Student roster management
    │   ├── RatesController.cs           # Hourly rate management
    │   ├── InvoicesController.cs        # Invoice CRUD, send, void, log payment
    │   ├── ReportsController.cs         # Monthly bookkeeping report
    │   ├── StripeWebhookController.cs   # Stripe webhook endpoint
    │   └── AccountController.cs        # Dev-only login stub
    ├── Models/
    │   ├── Entities/
    │   │   ├── Instructor.cs            # Instructor + Stripe Connect + subscription
    │   │   ├── Student.cs               # Student roster entry
    │   │   ├── Rate.cs                  # Hourly rate definition
    │   │   ├── Invoice.cs               # Invoice header + status
    │   │   ├── InvoiceLineItem.cs       # Line item (hours × rate)
    │   │   ├── Payment.cs               # Payment record (Stripe or manual)
    │   │   └── PlatformConfig.cs        # Platform-wide fee configuration
    │   └── ViewModels/
    │       ├── DashboardViewModel.cs
    │       ├── InvoiceViewModels.cs
    │       └── ReportViewModels.cs
    ├── Services/
    │   ├── StripeService.cs             # All Stripe API calls
    │   └── InvoiceService.cs            # Invoice creation and status logic
    ├── Data/
    │   └── BillingDbContext.cs          # EF Core DbContext + model config
    ├── Views/                           # Razor views (Bootstrap 5)
    ├── Migrations/                      # EF Core migrations
    ├── Program.cs                       # App startup, DI, middleware
    ├── appsettings.json
    └── appsettings.Development.json
```

---

## Integration with Climb TMS

This module is designed to integrate into the broader Climb TMS:

- **Authentication:** When integrated, `Instructor.TmsUserId` links to the shared TMS identity. The OIDC config in `appsettings.json` connects to the same Azure AD tenant.
- **Scheduling → Billing:** `InvoiceLineItem.TmsReservationId` stores a reference to a scheduling reservation. Once integrated, completed flights can auto-populate invoice line items with hours and aircraft.
- **Student identity:** `Student.TmsUserId` links to the TMS student record so a single identity spans scheduling and billing.

---

## Roadmap

- [ ] Email invoices directly to students from the app
- [ ] PDF invoice generation / download
- [ ] Recurring invoice templates
- [ ] Multi-aircraft rate lookup from TMS aircraft registry
- [ ] Auto-populate invoices from completed scheduling reservations
- [ ] Stripe tax calculation (for instructors who charge sales tax)
- [ ] Admin dashboard (platform-wide revenue, instructor management)
- [ ] Student-facing portal (view and pay invoices without instructor sharing a link)
