namespace ClimbBilling.Web.Models.Entities;

public class Student
{
    public int Id { get; set; }

    /// <summary>Links to the shared Climb TMS user identity (optional).</summary>
    public string? TmsUserId { get; set; }

    public int InstructorId { get; set; }
    public Instructor Instructor { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
