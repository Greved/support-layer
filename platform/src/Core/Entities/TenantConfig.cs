namespace Core.Entities;

public class TenantConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
