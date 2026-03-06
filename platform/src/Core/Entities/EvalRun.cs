namespace Core.Entities;

public class EvalRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RunType { get; set; } = string.Empty;
    public string ConfigSnapshotJson { get; set; } = "{}";
    public string TriggeredBy { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<EvalResult> Results { get; set; } = [];
    public ICollection<EvalBaseline> Baselines { get; set; } = [];
}
