namespace Core.Entities;

public class EvalBaseline
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public DateTime SetAt { get; set; }
    public string SetBy { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;
    public EvalRun Run { get; set; } = null!;
}
