namespace ClimbBilling.Web.Models.ViewModels;

public class DashboardViewModel
{
    public string InstructorName { get; set; } = string.Empty;
    public bool StripeConnected { get; set; }
    public bool SubscriptionActive { get; set; }

    // Financials
    public decimal OutstandingBalance { get; set; }
    public decimal PaidThisMonth { get; set; }
    public decimal PaidThisYear { get; set; }
    public int OpenInvoiceCount { get; set; }
    public int OverdueInvoiceCount { get; set; }

    // Recent activity
    public List<InvoiceSummaryRow> RecentInvoices { get; set; } = new();
}

public class InvoiceSummaryRow
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.Today && BalanceDue > 0;
}
