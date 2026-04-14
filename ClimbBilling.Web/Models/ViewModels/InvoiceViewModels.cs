using System.ComponentModel.DataAnnotations;
using ClimbBilling.Web.Models.Entities;

namespace ClimbBilling.Web.Models.ViewModels;

public class InvoiceListViewModel
{
    public List<InvoiceSummaryRow> Invoices { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? StudentFilter { get; set; }
    public int? YearFilter { get; set; }
    public List<int> AvailableYears { get; set; } = new();
}

public class CreateInvoiceViewModel
{
    [Required]
    public int StudentId { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; } = DateTime.Today.AddDays(30);

    [StringLength(2000)]
    public string? Notes { get; set; }

    public List<LineItemInputModel> LineItems { get; set; } = new()
    {
        new LineItemInputModel()
    };

    // Lookups
    public List<StudentSelectItem> Students { get; set; } = new();
    public List<RateSelectItem> Rates { get; set; } = new();
}

public class LineItemInputModel
{
    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, Range(0.01, 9999)]
    public decimal Quantity { get; set; } = 1;

    [Required, Range(0.01, 99999)]
    public decimal UnitPrice { get; set; }

    public LineItemType Type { get; set; } = LineItemType.Instruction;

    [DataType(DataType.Date)]
    public DateTime? ServiceDate { get; set; }

    public string? TmsReservationId { get; set; }
}

public class InvoiceDetailViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string? InstructorBusinessName { get; set; }
    public string InstructorEmail { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? PaymentLinkUrl { get; set; }
    public bool StripeConnected { get; set; }
    public List<LineItemDetailRow> LineItems { get; set; } = new();
    public List<PaymentDetailRow> Payments { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.Today && BalanceDue > 0;
}

public class LineItemDetailRow
{
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? ServiceDate { get; set; }
}

public class PaymentDetailRow
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public bool IsManual { get; set; }
    public string? Notes { get; set; }
}

public class StudentSelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class RateSelectItem
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class LogManualPaymentViewModel
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal BalanceDue { get; set; }

    [Required, Range(0.01, 999999)]
    public decimal Amount { get; set; }

    [Required]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Required, DataType(DataType.Date)]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [StringLength(500)]
    public string? Notes { get; set; }
}
