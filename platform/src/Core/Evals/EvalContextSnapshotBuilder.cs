using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Entities;

namespace Core.Evals;

public static class EvalContextSnapshotBuilder
{
    private const int MaxSnapshotChars = 128_000;
    private const int MaxPreviewChars = 2_000;
    private const int MaxTextChars = 8_000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] SensitiveConfigTokens =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "connectionstring",
        "connection_string",
        "privatekey",
        "private_key",
        "credential",
        "key"
    ];

    public static string BuildRunSnapshot(
        Guid tenantId,
        string tenantSlug,
        string runType,
        string triggeredBy,
        DateTime startedAtUtc,
        IReadOnlyCollection<EvalDataset> datasetRows,
        IReadOnlyCollection<TenantConfig> tenantConfigs,
        string source,
        object? triggerContext = null)
    {
        var questionTypeCounts = datasetRows
            .GroupBy(d => string.IsNullOrWhiteSpace(d.QuestionType) ? "unknown" : d.QuestionType.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var datasetVersions = datasetRows
            .Select(d => d.DatasetVersion)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        var snapshot = new
        {
            schema = "phase6.eval-run-context.v1",
            capturedAtUtc = DateTime.UtcNow,
            source,
            tenant = new
            {
                id = tenantId,
                slug = tenantSlug,
            },
            run = new
            {
                runType = TrimText(runType),
                triggeredBy = TrimText(triggeredBy),
                startedAtUtc,
            },
            dataset = new
            {
                rowCount = datasetRows.Count,
                questionTypeCounts,
                versions = datasetVersions,
                sampleDatasetItemIds = datasetRows.Select(d => d.Id).Take(20).ToArray(),
            },
            tenantConfig = tenantConfigs
                .OrderBy(c => c.Key, StringComparer.Ordinal)
                .Select(c => new
                {
                    key = TrimText(c.Key),
                    value = RedactConfigValue(c.Key, c.Value),
                })
                .ToArray(),
            triggerContext,
        };

        return SerializeAndHarden(snapshot);
    }

    public static IReadOnlyList<string> ParseSourceChunkIds(string? sourceChunkIdsJson)
    {
        if (string.IsNullOrWhiteSpace(sourceChunkIdsJson))
            return [];

        var parsed = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(sourceChunkIdsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            parsed.Add(TrimText(value));
                    }
                    else
                    {
                        parsed.Add(TrimText(item.GetRawText()));
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var value = doc.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    parsed.Add(TrimText(value));
            }
            else
            {
                parsed.Add(TrimText(doc.RootElement.GetRawText()));
            }
        }
        catch
        {
            parsed.Add(TrimText(sourceChunkIdsJson));
        }

        return parsed
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(1_000)
            .ToArray();
    }

    public static string BuildRetrievedChunksSnapshot(IReadOnlyList<string> chunks)
    {
        var chunkObjects = ToChunkObjects(chunks);
        return SerializeAndHarden(chunkObjects);
    }

    public static string BuildResultSnapshot(
        EvalDataset dataset,
        IReadOnlyList<string> sourceChunkIds,
        IReadOnlyList<string> retrievedChunks,
        string answer,
        int latencyMs,
        double? faithfulness,
        double? answerRelevancy,
        double? contextPrecision,
        double? contextRecall,
        double? hallucinationScore,
        double? answerCompleteness)
    {
        var snapshot = new
        {
            schema = "phase6.eval-result-context.v1",
            capturedAtUtc = DateTime.UtcNow,
            dataset = new
            {
                id = dataset.Id,
                question = TrimText(dataset.Question),
                groundTruth = TrimText(dataset.GroundTruth),
                questionType = TrimText(dataset.QuestionType),
                datasetVersion = TrimText(dataset.DatasetVersion),
                sourceChunkIds,
                sourceChunkCount = sourceChunkIds.Count,
                retrievedChunkCount = retrievedChunks.Count,
            },
            retrievedChunks = ToChunkObjects(retrievedChunks),
            output = new
            {
                answer = TrimText(answer),
                latencyMs = Math.Max(0, latencyMs),
                metrics = new
                {
                    faithfulness,
                    answerRelevancy,
                    contextPrecision,
                    contextRecall,
                    hallucinationScore,
                    answerCompleteness,
                },
            },
        };

        return SerializeAndHarden(snapshot);
    }

    private static object[] ToChunkObjects(IReadOnlyList<string> chunkIds)
    {
        return chunkIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select((chunkId, index) => new
            {
                rank = index + 1,
                chunkId = TrimText(chunkId),
            })
            .ToArray<object>();
    }

    private static string RedactConfigValue(string key, string value)
    {
        if (IsSensitiveConfigKey(key))
            return "[REDACTED]";

        return TrimText(value);
    }

    private static bool IsSensitiveConfigKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalized = key.Trim().ToLowerInvariant();
        return SensitiveConfigTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static string TrimText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim();
        if (normalized.Length <= MaxTextChars)
            return normalized;

        return normalized[..MaxTextChars];
    }

    private static string SerializeAndHarden(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, SerializerOptions);
            if (json.Length <= MaxSnapshotChars)
                return json;

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            var truncated = new
            {
                schema = "phase6.eval-context-truncated.v1",
                truncated = true,
                originalLength = json.Length,
                maxLength = MaxSnapshotChars,
                sha256 = Convert.ToHexString(hashBytes),
                preview = json[..Math.Min(MaxPreviewChars, json.Length)],
            };
            return JsonSerializer.Serialize(truncated, SerializerOptions);
        }
        catch (Exception ex)
        {
            var fallback = new
            {
                schema = "phase6.eval-context-error.v1",
                error = ex.GetType().Name,
                message = TrimText(ex.Message),
                capturedAtUtc = DateTime.UtcNow,
            };
            return JsonSerializer.Serialize(fallback, SerializerOptions);
        }
    }
}
