import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, RefreshCw, ShieldCheck, Zap } from 'lucide-react';
import { evals } from '@/lib/api';
import type {
  PortalEvalResultItem,
  PortalEvalRunDetail,
  PortalEvalRunItem,
  PortalEvalSummary,
} from '@/types';

const DISMISSED_KEY = 'sl_quality_dismissed_rows';

function formatPercent(value: number | null): string {
  if (value === null || Number.isNaN(value)) return 'N/A';
  return `${(value * 100).toFixed(1)}%`;
}

function formatMetricDelta(
  current: number | null,
  previous: number | null
): { text: string; className: string } {
  if (current === null || previous === null) {
    return { text: 'no baseline', className: 'text-gray-400' };
  }

  const delta = current - previous;
  const sign = delta > 0 ? '+' : '';
  if (Math.abs(delta) < 0.0001) {
    return { text: '0.0 pp', className: 'text-gray-500' };
  }

  return {
    text: `${sign}${(delta * 100).toFixed(1)} pp`,
    className: delta > 0 ? 'text-green-600' : 'text-red-600',
  };
}

function metricLabel(metric: number): string {
  if (metric >= 0.9) return 'Excellent';
  if (metric >= 0.75) return 'Healthy';
  if (metric >= 0.6) return 'Watch';
  return 'Critical';
}

function scoreStyle(score: number): string {
  if (score >= 0.8) return 'text-green-700 bg-green-50 border-green-200';
  if (score >= 0.65) return 'text-amber-700 bg-amber-50 border-amber-200';
  return 'text-red-700 bg-red-50 border-red-200';
}

function metricAverage(values: Array<number | null>): number {
  const present = values.filter((v): v is number => v !== null);
  if (present.length === 0) return 0;
  return present.reduce((sum, value) => sum + value, 0) / present.length;
}

function runQualityScore(run: PortalEvalRunItem): number | null {
  const values = [
    run.metrics.faithfulness,
    run.metrics.answerRelevancy,
    run.metrics.contextPrecision,
    run.metrics.contextRecall,
    run.metrics.answerCompleteness,
  ].filter((value): value is number => value !== null);

  if (values.length === 0) {
    return null;
  }

  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function formatRunChartLabel(startedAt: string): string {
  const date = new Date(startedAt);
  return `${date.getMonth() + 1}/${date.getDate()}`;
}

function lowConfidenceScore(row: PortalEvalResultItem): number {
  const positive = metricAverage([
    row.faithfulness,
    row.answerRelevancy,
    row.contextPrecision,
    row.contextRecall,
    row.answerCompleteness,
  ]);
  const hallucinationPenalty = row.hallucinationScore ?? 0;
  return Math.max(0, Math.min(1, positive - hallucinationPenalty * 0.5));
}

function DonutMetric({
  title,
  value,
  colorClass,
}: {
  title: string;
  value: number | null;
  colorClass: string;
}) {
  const safeValue = Math.max(0, Math.min(1, value ?? 0));
  const angle = safeValue * 360;

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">{title}</p>
      <div className="mt-4 flex items-center gap-4">
        <div
          className="relative h-24 w-24 rounded-full"
          style={{
            background: `conic-gradient(var(--metric-color) ${angle}deg, #e5e7eb ${angle}deg)`,
            ['--metric-color' as string]: colorClass,
          }}
        >
          <div className="absolute inset-[9px] flex items-center justify-center rounded-full bg-white">
            <span className="text-lg font-semibold text-gray-900">{formatPercent(value)}</span>
          </div>
        </div>
        <div>
          <p className="text-sm text-gray-500">Current run</p>
          <p className="text-sm font-medium text-gray-900">
            {value === null ? 'No data yet' : metricLabel(safeValue)}
          </p>
        </div>
      </div>
    </div>
  );
}

function qualityReasoning(result: PortalEvalResultItem): string {
  const parts: string[] = [];
  if (result.faithfulness !== null) parts.push(`faithfulness ${formatPercent(result.faithfulness)}`);
  if (result.answerRelevancy !== null)
    parts.push(`relevancy ${formatPercent(result.answerRelevancy)}`);
  if (result.contextPrecision !== null)
    parts.push(`precision ${formatPercent(result.contextPrecision)}`);
  if (result.contextRecall !== null) parts.push(`recall ${formatPercent(result.contextRecall)}`);
  if (result.hallucinationScore !== null)
    parts.push(`hallucination ${formatPercent(result.hallucinationScore)}`);
  if (parts.length === 0) return 'No metric trace available for this row.';
  return `Model quality signals: ${parts.join(', ')}.`;
}

function loadDismissed(): Set<string> {
  try {
    const raw = localStorage.getItem(DISMISSED_KEY);
    if (!raw) return new Set<string>();
    const parsed = JSON.parse(raw) as string[];
    return new Set(parsed);
  } catch {
    return new Set<string>();
  }
}

function saveDismissed(values: Set<string>): void {
  localStorage.setItem(DISMISSED_KEY, JSON.stringify([...values]));
}

function formatRunDate(value: string | null): string {
  if (!value) return 'In progress';
  return new Date(value).toLocaleString();
}

function metricCard(
  title: string,
  current: number | null,
  previous: number | null,
  icon: React.ReactNode,
  testId: string
) {
  const delta = formatMetricDelta(current, previous);

  return (
    <div
      data-testid={testId}
      className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm"
    >
      <div className="mb-3 flex items-center justify-between">
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">{title}</p>
        {icon}
      </div>
      <p className="text-2xl font-semibold text-gray-900">{formatPercent(current)}</p>
      <p className={`mt-1 text-xs font-medium ${delta.className}`}>{delta.text}</p>
    </div>
  );
}

export default function QualityPage() {
  const navigate = useNavigate();
  const [summary, setSummary] = useState<PortalEvalSummary | null>(null);
  const [runs, setRuns] = useState<PortalEvalRunItem[]>([]);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [detail, setDetail] = useState<PortalEvalRunDetail | null>(null);
  const [selectedResultId, setSelectedResultId] = useState<string | null>(null);
  const [dismissedRows, setDismissedRows] = useState<Set<string>>(() => loadDismissed());
  const [loading, setLoading] = useState(true);
  const [runLoading, setRunLoading] = useState(false);
  const [error, setError] = useState('');

  const refresh = async (preferredRunId?: string | null) => {
    setLoading(true);
    setError('');

    try {
      const [summaryRes, runsRes] = await Promise.all([evals.summary(), evals.runs(1, 30)]);
      const receivedSummary = summaryRes.data;
      const receivedRuns = runsRes.data.items;
      setSummary(receivedSummary);
      setRuns(receivedRuns);

      const targetRun =
        preferredRunId ??
        selectedRunId ??
        receivedSummary.currentRunId ??
        (receivedRuns.length > 0 ? receivedRuns[0].runId : null);
      setSelectedRunId(targetRun);
    } catch {
      setError('Failed to load quality data.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!selectedRunId) {
      setDetail(null);
      return;
    }

    let cancelled = false;
    const load = async () => {
      try {
        const response = await evals.runDetail(selectedRunId);
        if (cancelled) return;
        setDetail(response.data);
      } catch {
        if (cancelled) return;
        setError('Failed to load selected run details.');
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [selectedRunId]);

  const lowConfidenceRows = useMemo(() => {
    const results = detail?.results ?? [];
    return results
      .filter((row) => !dismissedRows.has(row.resultId))
      .map((row) => ({ row, score: lowConfidenceScore(row) }))
      .sort((a, b) => a.score - b.score)
      .slice(0, 25);
  }, [detail, dismissedRows]);

  useEffect(() => {
    if (lowConfidenceRows.length === 0) {
      setSelectedResultId(null);
      return;
    }

    const exists = selectedResultId
      ? lowConfidenceRows.some((entry) => entry.row.resultId === selectedResultId)
      : false;
    if (!exists) {
      setSelectedResultId(lowConfidenceRows[0].row.resultId);
    }
  }, [lowConfidenceRows, selectedResultId]);

  const selectedResult = useMemo(() => {
    if (!selectedResultId) return null;
    return lowConfidenceRows.find((entry) => entry.row.resultId === selectedResultId)?.row ?? null;
  }, [lowConfidenceRows, selectedResultId]);

  const handleTriggerRun = async () => {
    setRunLoading(true);
    setError('');

    try {
      const response = await evals.triggerRun();
      await refresh(response.data.runId);
    } catch {
      setError('Failed to trigger an eval run.');
    } finally {
      setRunLoading(false);
    }
  };

  const handleMarkCorrect = (resultId: string) => {
    const next = new Set(dismissedRows);
    next.add(resultId);
    setDismissedRows(next);
    saveDismissed(next);
  };

  const current = summary?.currentScores ?? null;
  const previous = summary?.previousScores ?? null;

  const topMetrics: Array<{
    title: string;
    currentValue: number | null;
    previousValue: number | null;
    icon: React.ReactNode;
    testId: string;
  }> = [
    {
      title: 'Faithfulness',
      currentValue: current?.faithfulness ?? null,
      previousValue: previous?.faithfulness ?? null,
      icon: <ShieldCheck size={16} className="text-blue-600" />,
      testId: 'quality-metric-faithfulness',
    },
    {
      title: 'Answer Relevancy',
      currentValue: current?.answerRelevancy ?? null,
      previousValue: previous?.answerRelevancy ?? null,
      icon: <CheckCircle2 size={16} className="text-green-600" />,
      testId: 'quality-metric-answer-relevancy',
    },
    {
      title: 'Context Precision',
      currentValue: current?.contextPrecision ?? null,
      previousValue: previous?.contextPrecision ?? null,
      icon: <Zap size={16} className="text-amber-600" />,
      testId: 'quality-metric-context-precision',
    },
    {
      title: 'Context Recall',
      currentValue: current?.contextRecall ?? null,
      previousValue: previous?.contextRecall ?? null,
      icon: <AlertTriangle size={16} className="text-purple-600" />,
      testId: 'quality-metric-context-recall',
    },
  ];

  const runHistory = useMemo(() => {
    return runs
      .filter((run) => run.status === 'completed')
      .slice(0, 10)
      .reverse()
      .map((run) => ({
        runId: run.runId,
        label: formatRunChartLabel(run.startedAt),
        score: runQualityScore(run),
      }));
  }, [runs]);

  const traceSteps = selectedResult
    ? [
        {
          title: 'USER QUERY',
          body: selectedResult.question,
        },
        {
          title: 'RETRIEVED DOCS',
          body:
            selectedResult.groundTruth ||
            'Retrieved document snippets are not exposed by this API response yet.',
        },
        {
          title: 'LLM REASONING',
          body: qualityReasoning(selectedResult),
        },
        {
          title: 'FINAL ANSWER',
          body: selectedResult.answer,
        },
      ]
    : [];

  return (
    <div className="space-y-6 p-6" data-testid="quality-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Quality</h1>
          <p className="mt-1 text-sm text-gray-500">
            Evaluate grounding, relevancy, and low-confidence responses for your tenant.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => void refresh()}
            data-testid="quality-refresh-btn"
            className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50"
          >
            <RefreshCw size={14} />
            Refresh
          </button>
          <button
            onClick={handleTriggerRun}
            disabled={runLoading}
            data-testid="quality-run-eval-btn"
            className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {runLoading ? 'Running...' : 'Run Evaluation'}
          </button>
        </div>
      </div>

      {error && (
        <div
          data-testid="quality-error-banner"
          className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-16">
          <div className="h-8 w-8 animate-spin rounded-full border-b-2 border-blue-600" />
        </div>
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            {topMetrics.map((metric) => (
              <div key={metric.title}>
                {metricCard(
                  metric.title,
                  metric.currentValue,
                  metric.previousValue,
                  metric.icon,
                  metric.testId
                )}
              </div>
            ))}
          </div>

          <div className="grid gap-6 xl:grid-cols-[2fr,1fr]">
            <div className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2">
                <div data-testid="quality-donut-groundedness">
                  <DonutMetric
                    title="Groundedness Score"
                    value={current?.faithfulness ?? null}
                    colorClass="#2563eb"
                  />
                </div>
                <div data-testid="quality-donut-answer-relevancy">
                  <DonutMetric
                    title="Answer Relevancy Score"
                    value={current?.answerRelevancy ?? null}
                    colorClass="#16a34a"
                  />
                </div>
              </div>

              <div
                data-testid="quality-run-history-chart"
                className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm"
              >
                <div className="mb-3 flex items-center justify-between">
                  <p className="text-sm font-semibold text-gray-900">Run History (Quality Score)</p>
                  <span className="text-xs text-gray-500">Last 10 completed runs</span>
                </div>
                {runHistory.length === 0 ? (
                  <p className="py-6 text-sm text-gray-500">No completed runs yet.</p>
                ) : (
                  <>
                    <div className="flex h-20 items-end gap-1">
                      {runHistory.map((entry) => (
                        <div
                          key={entry.runId}
                          data-testid="quality-run-history-bar"
                          className="group relative flex-1"
                        >
                          <div
                            className={`w-full rounded-t ${
                              entry.score === null
                                ? 'bg-gray-200'
                                : entry.score >= 0.8
                                  ? 'bg-green-500'
                                  : entry.score >= 0.65
                                    ? 'bg-amber-500'
                                    : 'bg-red-500'
                            }`}
                            style={{
                              height: `${Math.max(8, Math.round((entry.score ?? 0) * 100))}%`,
                            }}
                            title={`${entry.label}: ${formatPercent(entry.score)}`}
                          />
                        </div>
                      ))}
                    </div>
                    <div className="mt-2 flex items-center justify-between text-[11px] text-gray-500">
                      <span>{runHistory[0].label}</span>
                      <span>{runHistory[runHistory.length - 1].label}</span>
                    </div>
                  </>
                )}
              </div>

              <div
                data-testid="quality-low-confidence-table"
                className="rounded-xl border border-gray-200 bg-white shadow-sm"
              >
                <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
                  <div>
                    <p className="text-sm font-semibold text-gray-900">Low-Confidence Queries</p>
                    <p className="text-xs text-gray-500">
                      {summary?.currentRunResultCount ?? 0} rows in current run
                    </p>
                  </div>
                  <span className="text-xs text-gray-500">
                    Run finished: {formatRunDate(summary?.currentRunFinishedAt ?? null)}
                  </span>
                </div>
                {lowConfidenceRows.length === 0 ? (
                  <div className="px-4 py-8 text-sm text-gray-500">
                    No low-confidence rows available.
                  </div>
                ) : (
                  <div className="max-h-[420px] overflow-auto">
                    <table className="w-full text-sm">
                      <thead className="sticky top-0 bg-gray-50">
                        <tr className="border-b border-gray-200 text-left text-xs uppercase tracking-wide text-gray-500">
                          <th className="px-4 py-2.5">Query</th>
                          <th className="px-4 py-2.5">Generated Answer</th>
                          <th className="px-4 py-2.5">Score</th>
                          <th className="px-4 py-2.5 text-right">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {lowConfidenceRows.map(({ row, score }) => (
                          <tr
                            key={row.resultId}
                            data-testid="quality-low-confidence-row"
                            className={`cursor-pointer hover:bg-gray-50 ${
                              selectedResultId === row.resultId ? 'bg-blue-50/60' : ''
                            }`}
                            onClick={() => setSelectedResultId(row.resultId)}
                          >
                            <td className="max-w-[320px] truncate px-4 py-3 font-medium text-gray-900">
                              {row.question}
                            </td>
                            <td className="max-w-[420px] truncate px-4 py-3 text-gray-600">
                              {row.answer}
                            </td>
                            <td className="px-4 py-3">
                              <span
                                className={`inline-flex rounded-full border px-2 py-0.5 text-xs font-medium ${scoreStyle(
                                  score
                                )}`}
                              >
                                {formatPercent(score)}
                              </span>
                            </td>
                            <td className="px-4 py-3 text-right">
                              <button
                                data-testid="quality-row-mark-correct-btn"
                                onClick={(event) => {
                                  event.stopPropagation();
                                  handleMarkCorrect(row.resultId);
                                }}
                                className="text-xs font-medium text-blue-600 hover:text-blue-700"
                              >
                                Mark Correct
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              <div
                data-testid="quality-recent-runs-table"
                className="rounded-xl border border-gray-200 bg-white shadow-sm"
              >
                <div className="border-b border-gray-200 px-4 py-3">
                  <p className="text-sm font-semibold text-gray-900">Recent Eval Runs</p>
                </div>
                {runs.length === 0 ? (
                  <div className="px-4 py-6 text-sm text-gray-500">No eval runs yet.</div>
                ) : (
                  <div className="max-h-64 overflow-auto">
                    <table className="w-full text-sm">
                      <thead className="sticky top-0 bg-gray-50">
                        <tr className="border-b border-gray-200 text-left text-xs uppercase tracking-wide text-gray-500">
                          <th className="px-4 py-2.5">Started</th>
                          <th className="px-4 py-2.5">Type</th>
                          <th className="px-4 py-2.5">Status</th>
                          <th className="px-4 py-2.5">Rows</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {runs.map((run) => (
                          <tr
                            key={run.runId}
                            data-testid="quality-recent-run-row"
                            onClick={() => setSelectedRunId(run.runId)}
                            className={`cursor-pointer hover:bg-gray-50 ${
                              selectedRunId === run.runId ? 'bg-blue-50/60' : ''
                            }`}
                          >
                            <td className="px-4 py-3 text-gray-700">{formatRunDate(run.startedAt)}</td>
                            <td className="px-4 py-3 text-gray-700">{run.runType}</td>
                            <td className="px-4 py-3">
                              <span
                                className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                                  run.status === 'completed'
                                    ? 'bg-green-100 text-green-700'
                                    : run.status === 'running'
                                      ? 'bg-blue-100 text-blue-700'
                                      : 'bg-red-100 text-red-700'
                                }`}
                              >
                                {run.status}
                              </span>
                            </td>
                            <td className="px-4 py-3 text-gray-700">{run.resultCount}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>

            <div
              data-testid="quality-trace-view"
              className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm xl:sticky xl:top-4 xl:self-start"
            >
              <div className="mb-3 flex items-center justify-between">
                <p className="text-sm font-semibold text-gray-900">Trace View</p>
                {selectedResult && (
                  <div className="flex items-center gap-2">
                    <button
                      data-testid="quality-trace-mark-correct-btn"
                      onClick={() => handleMarkCorrect(selectedResult.resultId)}
                      className="rounded-md border border-gray-300 px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                    >
                      Mark Correct
                    </button>
                    <button
                      data-testid="quality-fix-kb-btn"
                      onClick={() =>
                        navigate(`/documents?focus=${encodeURIComponent(selectedResult.question)}`)
                      }
                      className="rounded-md bg-blue-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-blue-700"
                    >
                      Fix in Knowledge Base
                    </button>
                  </div>
                )}
              </div>

              {!selectedResult ? (
                <p className="py-8 text-sm text-gray-500">Select a low-confidence row to inspect.</p>
              ) : (
                <div className="space-y-3">
                  {traceSteps.map((step) => (
                    <div
                      key={step.title}
                      data-testid="quality-trace-step"
                      className="rounded-md border border-gray-200"
                    >
                      <div className="border-b border-gray-200 bg-gray-50 px-3 py-2">
                        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">
                          {step.title}
                        </p>
                      </div>
                      <div className="max-h-40 overflow-auto px-3 py-2 text-sm text-gray-700">
                        {step.body}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
