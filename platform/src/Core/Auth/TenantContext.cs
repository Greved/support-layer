namespace Core.Auth;

public class TenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
}
