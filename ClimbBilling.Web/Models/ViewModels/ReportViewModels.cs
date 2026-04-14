namespace ClimbBilling.Web.Models.ViewModels;

public class MonthlyReportViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    // Summary totals
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal PlatformFeesCharged { get; set; }

    // Breakdown by type
    public decimal InstructionRevenue { get; set; }
    public decimal AircraftRentalRevenue { get; set; }
    public decimal OtherRevenue { get; set; }

    // By payment method
    public decimal CollectedByCard { get; set; }
    public decimal CollectedByACH { get; set; }
    public decimal CollectedByManual { get; set; }  // cash, Venmo, Zelle, etc.

    public int InvoiceCount { get; set; }
    public int PaidInvoiceCount { get; set; }
    public int StudentCount { get; set; }

    public List<MonthlyInvoiceRow> Invoices { get; set; } = new();

    // Navigation
    public List<int> AvailableYears { get; set; } = new();
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
}

public class MonthlyInvoiceRow
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
}
