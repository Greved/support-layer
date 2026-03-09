namespace Core.Entities;

public class EvalResult
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid? DatasetItemId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string RetrievedChunksJson { get; set; } = "[]";
    public string ContextSnapshotJson { get; set; } = "{}";
    public double? Faithfulness { get; set; }
    public double? AnswerRelevancy { get; set; }
    public double? ContextPrecision { get; set; }
    public double? ContextRecall { get; set; }
    public double? HallucinationScore { get; set; }
    public double? AnswerCompleteness { get; set; }
    public int LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }

    public EvalRun Run { get; set; } = null!;
    public EvalDataset? DatasetItem { get; set; }
}
