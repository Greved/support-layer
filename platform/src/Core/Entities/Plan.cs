namespace Core.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public PlanLimit? Limit { get; set; }
    public ICollection<Tenant> Tenants { get; set; } = [];
}
