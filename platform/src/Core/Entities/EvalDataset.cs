namespace Core.Entities;

public class EvalDataset
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SourceFeedbackId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string SourceChunkIdsJson { get; set; } = "[]";
    public string QuestionType { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ChatMessageFeedback? SourceFeedback { get; set; }
    public ICollection<EvalResult> EvalResults { get; set; } = [];
}
