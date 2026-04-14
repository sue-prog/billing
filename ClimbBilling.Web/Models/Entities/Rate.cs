namespace ClimbBilling.Web.Models.Entities;

public class Rate
{
    public int Id { get; set; }

    public int InstructorId { get; set; }
    public Instructor Instructor { get; set; } = null!;

    public RateType Type { get; set; }

    /// <summary>Display label, e.g., "Cessna 172 Rental", "Dual Instruction", "Ground Instruction".</summary>
    public string Label { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum RateType
{
    Instruction,
    AircraftRental,
    GroundInstruction,
    SimulatorRental,
    Other
}
