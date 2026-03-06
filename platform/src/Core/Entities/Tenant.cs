namespace Core.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Plan Plan { get; set; } = null!;
    public ICollection<User> Users { get; set; } = [];
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public ICollection<TenantConfig> Configs { get; set; } = [];
    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<BillingEvent> BillingEvents { get; set; } = [];
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
    public ICollection<ChatMessageFeedback> ChatMessageFeedbackEntries { get; set; } = [];
    public ICollection<EvalDataset> EvalDatasets { get; set; } = [];
    public ICollection<EvalRun> EvalRuns { get; set; } = [];
    public ICollection<EvalBaseline> EvalBaselines { get; set; } = [];
    public ICollection<DriftAlert> DriftAlerts { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
