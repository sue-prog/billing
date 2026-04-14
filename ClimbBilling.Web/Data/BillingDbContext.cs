using ClimbBilling.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClimbBilling.Web.Data;

public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PlatformConfig> PlatformConfigs => Set<PlatformConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Instructor
        modelBuilder.Entity<Instructor>(e =>
        {
            e.HasIndex(i => i.Email).IsUnique();
            e.HasIndex(i => i.TmsUserId);
            e.Property(i => i.DisplayName).HasMaxLength(200);
            e.Property(i => i.Email).HasMaxLength(200);
            e.Property(i => i.Phone).HasMaxLength(20);
            e.Property(i => i.BusinessName).HasMaxLength(200);
            e.Property(i => i.StripeConnectAccountId).HasMaxLength(100);
            e.Property(i => i.StripeSubscriptionId).HasMaxLength(100);
        });

        // Student
        modelBuilder.Entity<Student>(e =>
        {
            e.HasIndex(s => new { s.InstructorId, s.Email });
            e.Property(s => s.Name).HasMaxLength(200);
            e.Property(s => s.Email).HasMaxLength(200);
            e.Property(s => s.Phone).HasMaxLength(20);
            e.HasOne(s => s.Instructor)
                .WithMany(i => i.Students)
                .HasForeignKey(s => s.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Rate
        modelBuilder.Entity<Rate>(e =>
        {
            e.Property(r => r.Label).HasMaxLength(200);
            e.Property(r => r.HourlyRate).HasColumnType("decimal(10,2)");
            e.HasOne(r => r.Instructor)
                .WithMany(i => i.Rates)
                .HasForeignKey(r => r.InstructorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasIndex(inv => inv.InvoiceNumber).IsUnique();
            e.Property(inv => inv.InvoiceNumber).HasMaxLength(30);
            e.Property(inv => inv.Notes).HasMaxLength(2000);
            e.Property(inv => inv.StripePaymentLinkId).HasMaxLength(200);
            e.Property(inv => inv.StripePaymentLinkUrl).HasMaxLength(500);
            e.Ignore(inv => inv.TotalAmount);
            e.Ignore(inv => inv.AmountPaid);
            e.Ignore(inv => inv.BalanceDue);
            e.HasOne(inv => inv.Instructor)
                .WithMany(i => i.Invoices)
                .HasForeignKey(inv => inv.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(inv => inv.Student)
                .WithMany(s => s.Invoices)
                .HasForeignKey(inv => inv.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InvoiceLineItem
        modelBuilder.Entity<InvoiceLineItem>(e =>
        {
            e.Property(li => li.Description).HasMaxLength(500);
            e.Property(li => li.Quantity).HasColumnType("decimal(10,2)");
            e.Property(li => li.UnitPrice).HasColumnType("decimal(10,2)");
            e.Property(li => li.TmsReservationId).HasMaxLength(100);
            e.Ignore(li => li.LineTotal);
            e.HasOne(li => li.Invoice)
                .WithMany(inv => inv.LineItems)
                .HasForeignKey(li => li.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasColumnType("decimal(10,2)");
            e.Property(p => p.PlatformFeeAmount).HasColumnType("decimal(10,2)");
            e.Property(p => p.StripePaymentIntentId).HasMaxLength(200);
            e.Property(p => p.StripeChargeId).HasMaxLength(200);
            e.Property(p => p.StripeTransferId).HasMaxLength(200);
            e.Property(p => p.Notes).HasMaxLength(500);
            e.HasOne(p => p.Invoice)
                .WithMany(inv => inv.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlatformConfig
        modelBuilder.Entity<PlatformConfig>(e =>
        {
            e.Property(pc => pc.SubscriptionMonthlyPrice).HasColumnType("decimal(10,2)");
            e.Property(pc => pc.PerTransactionFeePercent).HasColumnType("decimal(6,4)");
            e.Property(pc => pc.PerTransactionFeeFixed).HasColumnType("decimal(10,2)");
            e.Property(pc => pc.DefaultInvoiceFooterText).HasMaxLength(1000);
        });

        // Seed default platform config
        modelBuilder.Entity<PlatformConfig>().HasData(new PlatformConfig
        {
            Id = 1,
            SubscriptionEnabled = false,
            SubscriptionMonthlyPrice = 9.99m,
            PerTransactionFeeEnabled = true,
            PerTransactionFeePercent = 0.005m,
            PerTransactionFeeFixed = 0.00m,
            DefaultPaymentDueDays = 30,
            DefaultInvoiceFooterText = "Thank you for your business. Payment due upon receipt.",
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
