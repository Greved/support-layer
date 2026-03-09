using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Core.Evals;

public interface IEvalScoringService
{
    Task<EvalScoringBatchResult> ScoreAsync(
        string tenantSlug,
        string runId,
        IReadOnlyList<EvalDataset> datasetRows,
        CancellationToken cancellationToken = default);
}

public sealed record EvalScoredRow(
    string Question,
    string GroundTruth,
    string Answer,
    IReadOnlyList<string> RetrievedChunks,
    double Faithfulness,
    double AnswerRelevancy,
    double ContextPrecision,
    double ContextRecall,
    double HallucinationScore,
    double AnswerCompleteness,
    int LatencyMs);

public sealed record EvalScoringBatchResult(
    IReadOnlyList<EvalScoredRow> Rows,
    IReadOnlyDictionary<string, double> Metrics,
    string Engine,
    bool UsedFallback,
    string? Diagnostics,
    IReadOnlyDictionary<string, double>? Timings = null);

public sealed class PythonEvalScoringService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<PythonEvalScoringService> logger) : IEvalScoringService
{
    public async Task<EvalScoringBatchResult> ScoreAsync(
        string tenantSlug,
        string runId,
        IReadOnlyList<EvalDataset> datasetRows,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        if (datasetRows.Count == 0)
        {
            return new EvalScoringBatchResult(
                [],
                new Dictionary<string, double>(),
                "none",
                false,
                null,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["rows_count"] = 0,
                    ["total_service_ms"] = totalStopwatch.Elapsed.TotalMilliseconds,
                });
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "supportlayer-eval", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var datasetFile = Path.Combine(tempDir, "dataset.json");
        var outputFile = Path.Combine(tempDir, "run-result.json");
        var metricsFile = Path.Combine(tempDir, "metrics.json");

        try
        {
            var preferredPython = configuration["Phase6:EvalRunner:Command"];
            var configuredArgs = configuration["Phase6:EvalRunner:CommandPrefixArgs"];
            var timeoutSeconds = Math.Clamp(configuration.GetValue("Phase6:EvalRunner:TimeoutSeconds", 300), 30, 3600);
            var disableRagas = configuration.GetValue("Phase6:EvalRunner:DisableRagas", false);
            var disableDeepEval = configuration.GetValue("Phase6:EvalRunner:DisableDeepEval", false);
            var requireRagas = configuration.GetValue("Phase6:EvalRunner:RequireRagas", false);
            var requireDeepEval = configuration.GetValue("Phase6:EvalRunner:RequireDeepEval", false);
            var useLiveRagQuery = configuration.GetValue("Phase6:EvalRunner:UseLiveRagQuery", true);
            var requireLiveRagQuery = configuration.GetValue(
                "Phase6:EvalRunner:RequireLiveRagQuery",
                requireRagas || requireDeepEval);
            var inputPreparation = await BuildEvalInputPayloadAsync(
                tenantSlug,
                datasetRows,
                useLiveRagQuery,
                requireLiveRagQuery,
                cancellationToken);
            var payload = inputPreparation.Payload;

            var writeDatasetStopwatch = Stopwatch.StartNew();
            await File.WriteAllTextAsync(
                datasetFile,
                JsonSerializer.Serialize(payload),
                cancellationToken);
            writeDatasetStopwatch.Stop();

            var workingDirectory = ResolveWorkingDirectory();
            var candidates = BuildCommandCandidates(preferredPython, configuredArgs);
            logger.LogInformation(
                "Phase6 eval scoring start tenant={TenantSlug} runId={RunId} rows={Rows} timeoutSeconds={TimeoutSeconds} disableRagas={DisableRagas} disableDeepEval={DisableDeepEval} requireRagas={RequireRagas} requireDeepEval={RequireDeepEval} useLiveRagQuery={UseLiveRagQuery} requireLiveRagQuery={RequireLiveRagQuery} writeDatasetMs={WriteDatasetMs} inputPreparationTimings={InputPreparationTimings}",
                tenantSlug,
                runId,
                datasetRows.Count,
                timeoutSeconds,
                disableRagas,
                disableDeepEval,
                requireRagas,
                requireDeepEval,
                useLiveRagQuery,
                requireLiveRagQuery,
                writeDatasetStopwatch.ElapsedMilliseconds,
                JsonSerializer.Serialize(inputPreparation.Timings));

            string? lastError = inputPreparation.Diagnostics;
            foreach (var candidate in candidates)
            {
                try
                {
                    logger.LogInformation(
                        "Phase6 eval scoring candidate start tenant={TenantSlug} runId={RunId} command={Command}",
                        tenantSlug,
                        runId,
                        candidate.FileName);
                    var result = await TryRunEvalAsync(
                        candidate,
                        tenantSlug,
                        runId,
                        datasetFile,
                        outputFile,
                        metricsFile,
                        workingDirectory,
                        disableRagas,
                        disableDeepEval,
                        requireRagas,
                        requireDeepEval,
                        timeoutSeconds,
                        cancellationToken);

                    if (!result.Success)
                    {
                        var failedDiagnostics = AppendDiagnostics(
                            result.Diagnostics,
                            inputPreparation.Diagnostics);
                        if (TryRecoverResultFromFailedProcess(
                                outputFile,
                                failedDiagnostics,
                                out var recovered))
                        {
                            var recoveredTimings = MergeTimings(
                                recovered.Timings,
                                inputPreparation.Timings,
                                ("dataset_write_ms", writeDatasetStopwatch.Elapsed.TotalMilliseconds),
                                ("process_duration_ms", result.DurationMs),
                                ("total_service_ms", totalStopwatch.Elapsed.TotalMilliseconds));
                            logger.LogWarning(
                                "Phase6 eval scoring candidate exited non-zero but produced usable output tenant={TenantSlug} runId={RunId} command={Command} durationMs={DurationMs}",
                                tenantSlug,
                                runId,
                                candidate.FileName,
                                result.DurationMs);
                            return recovered with { Timings = recoveredTimings };
                        }

                        lastError = failedDiagnostics;
                        logger.LogWarning(
                            "Phase6 eval scoring candidate failed tenant={TenantSlug} runId={RunId} command={Command} durationMs={DurationMs} details={Details}",
                            tenantSlug,
                            runId,
                            candidate.FileName,
                            result.DurationMs,
                            result.Diagnostics);
                        continue;
                    }
                    var parsed = ParseRunResult(
                        outputFile,
                        AppendDiagnostics(result.Diagnostics, inputPreparation.Diagnostics));
                    var timings = MergeTimings(
                        parsed.Timings,
                        inputPreparation.Timings,
                        ("dataset_write_ms", writeDatasetStopwatch.Elapsed.TotalMilliseconds),
                        ("process_duration_ms", result.DurationMs),
                        ("total_service_ms", totalStopwatch.Elapsed.TotalMilliseconds));
                    logger.LogInformation(
                        "Phase6 eval scoring completed tenant={TenantSlug} runId={RunId} engine={Engine} usedFallback={UsedFallback} rows={Rows} timings={Timings}",
                        tenantSlug,
                        runId,
                        parsed.Engine,
                        parsed.UsedFallback,
                        parsed.Rows.Count,
                        JsonSerializer.Serialize(timings));
                    return parsed with { Timings = timings };
                }
                catch (Exception ex)
                {
                    lastError = AppendDiagnostics(
                        $"{candidate.FileName}: {ex.GetType().Name} {ex.Message}",
                        inputPreparation.Diagnostics);
                    logger.LogWarning(
                        ex,
                        "Phase6 eval scoring process failed for candidate {Command}",
                        candidate.FileName);
                }
            }

            var failureDiagnostics = AppendDiagnostics(
                "Real eval scoring failed: no python candidate produced a valid non-fallback result.",
                AppendDiagnostics(lastError, inputPreparation.Diagnostics));
            logger.LogError(
                "Phase6 eval scoring failed hard tenant={TenantSlug} runId={RunId} details={Details}",
                tenantSlug,
                runId,
                failureDiagnostics);
            throw new InvalidOperationException(failureDiagnostics);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private EvalScoringBatchResult ParseRunResult(string outputFile, string diagnostics)
    {
        if (!File.Exists(outputFile))
        {
            return new EvalScoringBatchResult(
                [],
                new Dictionary<string, double>(),
                "python-eval",
                true,
                diagnostics,
                new Dictionary<string, double>(StringComparer.Ordinal));
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(outputFile));
        var root = doc.RootElement;

        var rows = new List<EvalScoredRow>();
        if (root.TryGetProperty("rows", out var rowsElement) && rowsElement.ValueKind == JsonValueKind.Array)
        {
            var rowIndex = 0;
            foreach (var rowElement in rowsElement.EnumerateArray())
            {
                var question = GetString(rowElement, "question");
                var groundTruth = GetString(rowElement, "ground_truth");
                var answer = GetString(rowElement, "answer");
                var retrieved = GetStringList(rowElement, "retrieved_context");

                rows.Add(new EvalScoredRow(
                    question,
                    groundTruth,
                    answer,
                    retrieved,
                    GetRequiredDouble(rowElement, "faithfulness", rowIndex),
                    GetRequiredDouble(rowElement, "answer_relevancy", rowIndex),
                    GetRequiredDouble(rowElement, "context_precision", rowIndex),
                    GetRequiredDouble(rowElement, "context_recall", rowIndex),
                    GetRequiredDouble(rowElement, "hallucination_rate", rowIndex),
                    GetRequiredDouble(rowElement, "answer_completeness", rowIndex),
                    GetRequiredInt(rowElement, "latency_ms", rowIndex)));
                rowIndex++;
            }
        }

        var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
        if (root.TryGetProperty("metrics", out var metricsElement) && metricsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in metricsElement.EnumerateObject())
            {
                var value = GetDouble(metricsElement, prop.Name);
                if (value.HasValue)
                    metrics[prop.Name] = value.Value;
            }
        }

        var timings = ParseTimings(root);
        if (timings.TryGetValue("rows_with_fallback", out var rowsWithFallback) && rowsWithFallback > 0)
            throw new InvalidDataException(
                $"Eval output contains fallback-scored rows (rows_with_fallback={rowsWithFallback}).");
        if (rows.Count == 0)
            throw new InvalidDataException("Eval output did not contain any scored rows.");

        return new EvalScoringBatchResult(rows, metrics, "python-eval", false, diagnostics, timings);
    }

    private bool TryRecoverResultFromFailedProcess(
        string outputFile,
        string diagnostics,
        out EvalScoringBatchResult recovered)
    {
        recovered = default!;
        try
        {
            var parsed = ParseRunResult(outputFile, diagnostics);
            if (parsed.UsedFallback || parsed.Rows.Count == 0)
                return false;

            recovered = parsed;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Phase6 eval scoring failed to recover run-result after non-zero python exit. file={OutputFile}",
                outputFile);
            return false;
        }
    }

    private async Task<EvalInputPreparationResult> BuildEvalInputPayloadAsync(
        string tenantSlug,
        IReadOnlyList<EvalDataset> datasetRows,
        bool useLiveRagQuery,
        bool requireLiveRagQuery,
        CancellationToken cancellationToken)
    {
        var payload = new List<EvalInputRowPayload>(datasetRows.Count);
        var queryDurationsMs = new List<double>(datasetRows.Count);
        var queryErrors = new List<string>();
        var rowsWithLiveQuery = 0;
        var rowsWithLiveFailures = 0;

        if (!useLiveRagQuery)
        {
            foreach (var row in datasetRows)
            {
                var sourceChunks = EvalContextSnapshotBuilder.ParseSourceChunkIds(row.SourceChunkIdsJson);
                var fallbackRetrieved = sourceChunks.Count > 0 ? sourceChunks : [row.GroundTruth];
                payload.Add(new EvalInputRowPayload(
                    row.Question,
                    row.GroundTruth,
                    sourceChunks,
                    row.GroundTruth,
                    fallbackRetrieved,
                    180));
            }

            return new EvalInputPreparationResult(
                payload,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["rag_query_enabled"] = 0,
                    ["rag_query_required"] = requireLiveRagQuery ? 1 : 0,
                    ["rag_query_rows_attempted"] = 0,
                    ["rag_query_rows_live"] = 0,
                    ["rag_query_rows_failed"] = 0,
                },
                null);
        }

        if (!TryGetRagCoreConfig(out var ragBaseUrl, out var internalSecret, out var configError))
        {
            if (requireLiveRagQuery)
            {
                throw new InvalidOperationException(
                    $"Live RAG query preparation is required but configuration is missing: {configError}");
            }

            logger.LogWarning(
                "Live RAG query preparation disabled because configuration is missing. details={Details}",
                configError);
            foreach (var row in datasetRows)
            {
                var sourceChunks = EvalContextSnapshotBuilder.ParseSourceChunkIds(row.SourceChunkIdsJson);
                var fallbackRetrieved = sourceChunks.Count > 0 ? sourceChunks : [row.GroundTruth];
                payload.Add(new EvalInputRowPayload(
                    row.Question,
                    row.GroundTruth,
                    sourceChunks,
                    row.GroundTruth,
                    fallbackRetrieved,
                    180));
            }

            return new EvalInputPreparationResult(
                payload,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["rag_query_enabled"] = 1,
                    ["rag_query_required"] = requireLiveRagQuery ? 1 : 0,
                    ["rag_query_rows_attempted"] = datasetRows.Count,
                    ["rag_query_rows_live"] = 0,
                    ["rag_query_rows_failed"] = datasetRows.Count,
                    ["rag_query_error_count"] = 1,
                },
                configError);
        }

        foreach (var row in datasetRows)
        {
            var sourceChunks = EvalContextSnapshotBuilder.ParseSourceChunkIds(row.SourceChunkIdsJson);
            var fallbackRetrieved = sourceChunks.Count > 0 ? sourceChunks : [row.GroundTruth];
            var answer = row.GroundTruth;
            var retrievedContext = fallbackRetrieved;
            var latencyMs = 180;

            var queryStopwatch = Stopwatch.StartNew();
            try
            {
                var queryResult = await QueryRagAsync(
                    ragBaseUrl,
                    internalSecret,
                    tenantSlug,
                    row.Question,
                    cancellationToken);
                queryStopwatch.Stop();
                queryDurationsMs.Add(queryStopwatch.Elapsed.TotalMilliseconds);
                latencyMs = (int)Math.Round(Math.Max(0, queryStopwatch.Elapsed.TotalMilliseconds));

                if (!string.IsNullOrWhiteSpace(queryResult.Answer))
                    answer = queryResult.Answer;

                if (queryResult.RetrievedContext.Count > 0)
                    retrievedContext = queryResult.RetrievedContext;

                var liveRow = !string.IsNullOrWhiteSpace(answer) && retrievedContext.Count > 0;
                if (liveRow)
                    rowsWithLiveQuery++;
                else if (requireLiveRagQuery)
                    throw new InvalidOperationException(
                        "RAG query returned incomplete data (missing answer or retrieved context).");
            }
            catch (Exception ex)
            {
                queryStopwatch.Stop();
                queryDurationsMs.Add(queryStopwatch.Elapsed.TotalMilliseconds);
                rowsWithLiveFailures++;
                var compact = $"{row.Id}:{ex.GetType().Name}:{ex.Message}";
                queryErrors.Add(compact.Length <= 300 ? compact : compact[..300]);
                if (requireLiveRagQuery)
                    throw;
            }

            payload.Add(new EvalInputRowPayload(
                row.Question,
                row.GroundTruth,
                sourceChunks,
                answer,
                retrievedContext,
                latencyMs));
        }

        var timings = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["rag_query_enabled"] = 1,
            ["rag_query_required"] = requireLiveRagQuery ? 1 : 0,
            ["rag_query_rows_attempted"] = datasetRows.Count,
            ["rag_query_rows_live"] = rowsWithLiveQuery,
            ["rag_query_rows_failed"] = rowsWithLiveFailures,
            ["rag_query_error_count"] = queryErrors.Count,
        };
        if (queryDurationsMs.Count > 0)
        {
            timings["rag_query_total_ms"] = queryDurationsMs.Sum();
            timings["rag_query_avg_ms"] = queryDurationsMs.Average();
            timings["rag_query_p50_ms"] = Percentile(queryDurationsMs, 0.50);
            timings["rag_query_p95_ms"] = Percentile(queryDurationsMs, 0.95);
            timings["rag_query_max_ms"] = queryDurationsMs.Max();
        }

        var diagnostics = queryErrors.Count == 0
            ? null
            : $"rag_query_errors={string.Join(" | ", queryErrors.Take(5))}";
        return new EvalInputPreparationResult(payload, timings, diagnostics);
    }

    private bool TryGetRagCoreConfig(
        out string baseUrl,
        out string internalSecret,
        out string? error)
    {
        baseUrl = (configuration["RagCore:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
        internalSecret = configuration["RagCore:InternalSecret"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            error = "RagCore:BaseUrl is missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(internalSecret))
        {
            error = "RagCore:InternalSecret is missing";
            return false;
        }

        error = null;
        return true;
    }

    private async Task<RagEvalQueryResult> QueryRagAsync(
        string ragBaseUrl,
        string internalSecret,
        string tenantSlug,
        string query,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(PythonEvalScoringService));
        var body = JsonSerializer.Serialize(new { tenant_id = tenantSlug, query });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ragBaseUrl}/internal/query")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Internal-Secret", internalSecret);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var answer = root.TryGetProperty("answer", out var answerEl)
            ? answerEl.GetString() ?? string.Empty
            : string.Empty;
        var contexts = new List<string>();
        if (root.TryGetProperty("sources", out var sourcesEl) && sourcesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sourcesEl.EnumerateArray())
            {
                var brief = source.TryGetProperty("brief_content", out var briefEl)
                    ? briefEl.GetString() ?? string.Empty
                    : string.Empty;
                var fallback = BuildSourceFallbackText(source);
                var value = !string.IsNullOrWhiteSpace(brief) ? brief : fallback;
                if (!string.IsNullOrWhiteSpace(value))
                    contexts.Add(value.Trim());
            }
        }

        var dedupedContexts = contexts
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToList();

        return new RagEvalQueryResult(answer.Trim(), dedupedContexts);
    }

    private static string BuildSourceFallbackText(JsonElement source)
    {
        var file = source.TryGetProperty("file", out var fileEl) ? fileEl.GetString() ?? string.Empty : string.Empty;
        var page = source.TryGetProperty("page", out var pageEl) && pageEl.ValueKind == JsonValueKind.Number
            ? pageEl.GetInt32().ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        var offset = source.TryGetProperty("offset", out var offsetEl) && offsetEl.ValueKind == JsonValueKind.Number
            ? offsetEl.GetInt32().ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(file))
            parts.Add(file.Trim());
        if (!string.IsNullOrWhiteSpace(page))
            parts.Add($"page:{page}");
        if (!string.IsNullOrWhiteSpace(offset))
            parts.Add($"offset:{offset}");
        return string.Join(" ", parts);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 1)
            return sorted[0];

        var rank = (sorted.Length - 1) * percentile;
        var low = (int)Math.Floor(rank);
        var high = Math.Min(sorted.Length - 1, low + 1);
        var fraction = rank - low;
        return sorted[low] + (sorted[high] - sorted[low]) * fraction;
    }

    private string ResolveWorkingDirectory()
    {
        var configured = configuration["Phase6:EvalRunner:WorkingDirectory"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "eval", "run_eval.py")))
            return cwd;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "eval", "run_eval.py")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return cwd;
    }

    private static IReadOnlyList<CommandCandidate> BuildCommandCandidates(string? configuredCommand, string? configuredPrefixArgs)
    {
        if (!string.IsNullOrWhiteSpace(configuredCommand))
        {
            var prefix = string.IsNullOrWhiteSpace(configuredPrefixArgs)
                ? Array.Empty<string>()
                : configuredPrefixArgs
                    .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return [new CommandCandidate(configuredCommand.Trim(), prefix)];
        }

        if (OperatingSystem.IsWindows())
        {
            return
            [
                new CommandCandidate("python", []),
                new CommandCandidate("py", ["-3.12"]),
            ];
        }

        return
        [
            new CommandCandidate("python3", []),
            new CommandCandidate("python", []),
        ];
    }

    private async Task<ProcessExecutionResult> TryRunEvalAsync(
        CommandCandidate candidate,
        string tenantSlug,
        string runId,
        string datasetFile,
        string outputFile,
        string metricsFile,
        string workingDirectory,
        bool disableRagas,
        bool disableDeepEval,
        bool requireRagas,
        bool requireDeepEval,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = candidate.FileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var prefixArg in candidate.PrefixArgs)
            startInfo.ArgumentList.Add(prefixArg);

        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("eval.run_eval");
        startInfo.ArgumentList.Add("--tenant");
        startInfo.ArgumentList.Add(tenantSlug);
        startInfo.ArgumentList.Add("--dataset-file");
        startInfo.ArgumentList.Add(datasetFile);
        startInfo.ArgumentList.Add("--run-id");
        startInfo.ArgumentList.Add(runId);
        startInfo.ArgumentList.Add("--output-file");
        startInfo.ArgumentList.Add(outputFile);
        startInfo.ArgumentList.Add("--metrics-file");
        startInfo.ArgumentList.Add(metricsFile);

        if (disableRagas)
            startInfo.ArgumentList.Add("--disable-ragas");
        if (disableDeepEval)
            startInfo.ArgumentList.Add("--disable-deepeval");
        if (requireRagas)
            startInfo.ArgumentList.Add("--require-ragas");
        if (requireDeepEval)
            startInfo.ArgumentList.Add("--require-deepeval");

        using var process = new Process { StartInfo = startInfo };
        var processStopwatch = Stopwatch.StartNew();
        if (!process.Start())
        {
            processStopwatch.Stop();
            return new ProcessExecutionResult(
                false,
                $"Failed to start process {candidate.FileName}",
                processStopwatch.Elapsed.TotalMilliseconds);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            var diagnostics = $"cmd={candidate.FileName} exit={process.ExitCode} stdout={Trim(stdOut)} stderr={Trim(stdErr)}";
            if (process.ExitCode != 0)
            {
                processStopwatch.Stop();
                return new ProcessExecutionResult(false, diagnostics, processStopwatch.Elapsed.TotalMilliseconds);
            }

            processStopwatch.Stop();
            return new ProcessExecutionResult(true, diagnostics, processStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore cleanup errors.
            }

            processStopwatch.Stop();
            return new ProcessExecutionResult(
                false,
                $"cmd={candidate.FileName} timeout_after={timeoutSeconds}s",
                processStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static string Trim(string value)
    {
        const int maxLen = 1_000;
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }

    private static string AppendDiagnostics(string? primary, string? secondary)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(primary))
            parts.Add(primary.Trim());
        if (!string.IsNullOrWhiteSpace(secondary))
            parts.Add(secondary.Trim());
        return string.Join(" | ", parts.Distinct(StringComparer.Ordinal));
    }

    private static string GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText(),
        };
    }

    private static IReadOnlyList<string> GetStringList(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return [];

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var single = value.GetString();
            if (string.IsNullOrWhiteSpace(single))
                return [];
            return [single.Trim()];
        }

        return [value.GetRawText()];
    }

    private static double? GetDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static int? GetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static double GetRequiredDouble(JsonElement element, string property, int rowIndex)
    {
        var value = GetDouble(element, property);
        if (!value.HasValue)
            throw new InvalidDataException(
                $"Missing or invalid '{property}' in eval output row {rowIndex}.");
        return value.Value;
    }

    private static int GetRequiredInt(JsonElement element, string property, int rowIndex)
    {
        var value = GetInt(element, property);
        if (!value.HasValue)
            throw new InvalidDataException(
                $"Missing or invalid '{property}' in eval output row {rowIndex}.");
        return value.Value;
    }

    private static Dictionary<string, double> ParseTimings(JsonElement root)
    {
        var timings = new Dictionary<string, double>(StringComparer.Ordinal);
        if (!root.TryGetProperty("timings", out var timingsElement) || timingsElement.ValueKind != JsonValueKind.Object)
            return timings;

        foreach (var property in timingsElement.EnumerateObject())
        {
            var value = GetDouble(timingsElement, property.Name);
            if (value.HasValue)
                timings[property.Name] = value.Value;
        }

        return timings;
    }

    private static Dictionary<string, double> MergeTimings(
        IReadOnlyDictionary<string, double>? baseTimings,
        IReadOnlyDictionary<string, double>? additionalTimings,
        params (string Key, double Value)[] extras)
    {
        var merged = baseTimings is null
            ? new Dictionary<string, double>(StringComparer.Ordinal)
            : new Dictionary<string, double>(baseTimings, StringComparer.Ordinal);

        if (additionalTimings is not null)
        {
            foreach (var pair in additionalTimings)
                merged[pair.Key] = pair.Value;
        }

        foreach (var extra in extras)
            merged[extra.Key] = extra.Value;

        return merged;
    }

    private readonly record struct CommandCandidate(string FileName, string[] PrefixArgs);

    private readonly record struct ProcessExecutionResult(bool Success, string Diagnostics, double DurationMs);

    private sealed record EvalInputPreparationResult(
        IReadOnlyList<EvalInputRowPayload> Payload,
        IReadOnlyDictionary<string, double> Timings,
        string? Diagnostics);

    private sealed record EvalInputRowPayload(
        [property: JsonPropertyName("question")] string Question,
        [property: JsonPropertyName("ground_truth")] string GroundTruth,
        [property: JsonPropertyName("source_chunks")] IReadOnlyList<string> SourceChunks,
        [property: JsonPropertyName("answer")] string Answer,
        [property: JsonPropertyName("retrieved_context")] IReadOnlyList<string> RetrievedContext,
        [property: JsonPropertyName("latency_ms")] int LatencyMs);

    private sealed record RagEvalQueryResult(
        string Answer,
        IReadOnlyList<string> RetrievedContext);

}
