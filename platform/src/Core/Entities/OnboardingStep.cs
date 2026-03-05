namespace Core.Entities;

public class OnboardingStep
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Step { get; set; }
    public DateTime CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
