namespace Core.Entities;

public class PlanLimit
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public int MaxDocuments { get; set; }
    public long MaxStorageBytes { get; set; }
    public int MaxQueriesPerMonth { get; set; }
    public int MaxUsers { get; set; }

    public Plan Plan { get; set; } = null!;
}
