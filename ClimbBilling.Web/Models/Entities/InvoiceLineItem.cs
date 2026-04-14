namespace ClimbBilling.Web.Models.Entities;

public class InvoiceLineItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public LineItemType Type { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Hours (or units) for this line item.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Rate at time of invoicing (snapshot — rate changes don't affect old invoices).</summary>
    public decimal UnitPrice { get; set; }

    public DateTime? ServiceDate { get; set; }

    /// <summary>Optional: link back to a scheduling record in the TMS.</summary>
    public string? TmsReservationId { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

public enum LineItemType
{
    Instruction,
    AircraftRental,
    GroundInstruction,
    SimulatorRental,
    Discount,
    Other
}
