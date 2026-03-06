namespace Core.Entities;

public class DriftAlert
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Signal { get; set; } = string.Empty;
    public double BaselineRate { get; set; }
    public double CurrentRate { get; set; }
    public double DropAmount { get; set; }
    public double Threshold { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
