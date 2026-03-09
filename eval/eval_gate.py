from __future__ import annotations

import argparse
import json
import logging
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path

DEFAULT_THRESHOLDS: dict[str, float] = {
    "faithfulness": 0.05,
    "answer_relevancy": 0.05,
    "context_precision": 0.08,
    "context_recall": 0.08,
    "hallucination_rate": 0.03,
}

RELATIVE_DROP_METRICS = {
    "faithfulness",
    "answer_relevancy",
    "context_precision",
    "context_recall",
}
ABSOLUTE_INCREASE_METRICS = {"hallucination_rate"}
logger = logging.getLogger("eval_gate")


@dataclass(frozen=True)
class MetricCheck:
    metric: str
    baseline: float
    current: float
    threshold: float
    mode: str
    failed: bool

    @property
    def delta(self) -> float:
        return self.current - self.baseline

    @property
    def relative_drop(self) -> float:
        if self.baseline <= 0:
            return 0.0 if self.current >= self.baseline else 1.0
        return max(0.0, (self.baseline - self.current) / self.baseline)


@dataclass(frozen=True)
class IntegrationCheck:
    name: str
    expected: str
    actual: str
    failed: bool


def _parse_metrics_file(path: Path) -> dict[str, float]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    data = raw.get("metrics", raw) if isinstance(raw, dict) else raw
    if not isinstance(data, dict):
        raise ValueError(f"Unsupported metrics format in {path}")

    parsed: dict[str, float] = {}
    missing = []
    for metric in DEFAULT_THRESHOLDS:
        value = data.get(metric)
        if value is None:
            missing.append(metric)
            continue
        try:
            parsed[metric] = float(value)
        except (TypeError, ValueError) as exc:
            raise ValueError(f"Metric '{metric}' in {path} must be numeric") from exc

    if missing:
        missing_str = ", ".join(missing)
        raise ValueError(f"Missing metrics in {path}: {missing_str}")
    return parsed


def _parse_run_result_file(path: Path) -> dict:
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError(f"Unsupported run-result format in {path}")
    return raw


def evaluate_integration_integrity(run_result: dict) -> list[IntegrationCheck]:
    timings = run_result.get("timings")
    integrations = run_result.get("integrations")
    integration_errors = run_result.get("integration_errors")

    if not isinstance(timings, dict):
        raise ValueError("run-result missing timings object")
    if not isinstance(integrations, dict):
        raise ValueError("run-result missing integrations object")
    if not isinstance(integration_errors, dict):
        raise ValueError("run-result missing integration_errors object")

    rows_count = int(timings.get("rows_count") or 0)
    rows_with_ragas = int(timings.get("rows_with_ragas") or 0)
    rows_with_deepeval = int(timings.get("rows_with_deepeval") or 0)
    rows_with_fallback = int(timings.get("rows_with_fallback") or 0)
    rows_with_explicit_answer = int(timings.get("rows_with_explicit_answer") or 0)
    rows_with_explicit_context = int(timings.get("rows_with_explicit_context") or 0)
    rows_answer_equals_ground_truth = int(timings.get("rows_answer_equals_ground_truth") or 0)
    ragas_error_count = int(timings.get("ragas_error_count") or 0)
    deepeval_error_count = int(timings.get("deepeval_error_count") or 0)
    ragas_errors = integration_errors.get("ragas") or []
    deepeval_errors = integration_errors.get("deepeval") or []

    checks = [
        IntegrationCheck(
            name="rows_count",
            expected="> 0",
            actual=str(rows_count),
            failed=rows_count <= 0,
        ),
        IntegrationCheck(
            name="ragas_required",
            expected="true",
            actual=str(bool(integrations.get("ragas_required"))).lower(),
            failed=not bool(integrations.get("ragas_required")),
        ),
        IntegrationCheck(
            name="deepeval_required",
            expected="true",
            actual=str(bool(integrations.get("deepeval_required"))).lower(),
            failed=not bool(integrations.get("deepeval_required")),
        ),
        IntegrationCheck(
            name="rows_with_ragas",
            expected=f"== {rows_count}",
            actual=str(rows_with_ragas),
            failed=rows_with_ragas != rows_count,
        ),
        IntegrationCheck(
            name="rows_with_deepeval",
            expected=f"== {rows_count}",
            actual=str(rows_with_deepeval),
            failed=rows_with_deepeval != rows_count,
        ),
        IntegrationCheck(
            name="rows_with_fallback",
            expected="== 0",
            actual=str(rows_with_fallback),
            failed=rows_with_fallback != 0,
        ),
        IntegrationCheck(
            name="rows_with_explicit_answer",
            expected=f"== {rows_count}",
            actual=str(rows_with_explicit_answer),
            failed=rows_with_explicit_answer != rows_count,
        ),
        IntegrationCheck(
            name="rows_with_explicit_context",
            expected=f"== {rows_count}",
            actual=str(rows_with_explicit_context),
            failed=rows_with_explicit_context != rows_count,
        ),
        IntegrationCheck(
            name="rows_answer_equals_ground_truth",
            expected=f"< {rows_count}",
            actual=str(rows_answer_equals_ground_truth),
            failed=rows_answer_equals_ground_truth >= rows_count,
        ),
        IntegrationCheck(
            name="ragas_error_count",
            expected="== 0",
            actual=str(ragas_error_count),
            failed=ragas_error_count != 0 or bool(ragas_errors),
        ),
        IntegrationCheck(
            name="deepeval_error_count",
            expected="== 0",
            actual=str(deepeval_error_count),
            failed=deepeval_error_count != 0 or bool(deepeval_errors),
        ),
    ]
    return checks


def evaluate_regression(
    baseline: dict[str, float],
    current: dict[str, float],
    thresholds: dict[str, float] | None = None,
) -> list[MetricCheck]:
    rules = thresholds or DEFAULT_THRESHOLDS
    checks: list[MetricCheck] = []
    for metric, threshold in rules.items():
        b = float(baseline[metric])
        c = float(current[metric])
        if metric in RELATIVE_DROP_METRICS:
            drop = 0.0 if b <= 0 else max(0.0, (b - c) / b)
            failed = drop > threshold
            checks.append(
                MetricCheck(
                    metric=metric,
                    baseline=b,
                    current=c,
                    threshold=threshold,
                    mode="relative_drop",
                    failed=failed,
                )
            )
        elif metric in ABSOLUTE_INCREASE_METRICS:
            increase = max(0.0, c - b)
            failed = increase > threshold
            checks.append(
                MetricCheck(
                    metric=metric,
                    baseline=b,
                    current=c,
                    threshold=threshold,
                    mode="absolute_increase",
                    failed=failed,
                )
            )
        else:
            raise ValueError(f"Unsupported metric '{metric}'")
    return checks


def _format_delta(check: MetricCheck) -> str:
    if check.mode == "relative_drop":
        return f"{check.relative_drop * 100:.2f}% drop"
    return f"{max(0.0, check.current - check.baseline):.4f} increase"


def build_markdown_report(
    checks: list[MetricCheck],
    baseline_run_id: str,
    current_run_id: str,
    baseline_metrics: dict[str, float],
    current_metrics: dict[str, float],
    integration_checks: list[IntegrationCheck] | None = None,
    current_run_result: dict | None = None,
) -> str:
    generated_at = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    lines = [
        "# Eval Gate Summary",
        "",
        f"- Generated at: `{generated_at}`",
        f"- Baseline run: `{baseline_run_id}`",
        f"- Current run: `{current_run_id}`",
        "",
        "| Metric | Baseline | Current | Delta | Threshold | Rule | Result |",
        "|---|---:|---:|---|---:|---|---|",
    ]
    for check in checks:
        result = "FAIL" if check.failed else "PASS"
        lines.append(
            f"| {check.metric} | {check.baseline:.4f} | {check.current:.4f} | "
            f"{_format_delta(check)} | {check.threshold:.4f} | {check.mode} | {result} |"
        )
    failed = [c.metric for c in checks if c.failed]
    failed_integrations = [c.name for c in (integration_checks or []) if c.failed]
    lines.extend(
        [
            "",
            f"- Gate result: **{'FAILED' if (failed or failed_integrations) else 'PASSED'}**",
        ]
    )
    if failed:
        lines.append(f"- Regressed metrics: {', '.join(failed)}")
    if integration_checks:
        lines.append(
            f"- Integration integrity: **{'FAILED' if failed_integrations else 'PASSED'}**"
        )
        if failed_integrations:
            lines.append(f"- Integration failures: {', '.join(failed_integrations)}")
        lines.extend(
            [
                "",
                "## Integration Integrity Checks",
                "",
                "| Check | Expected | Actual | Result |",
                "|---|---|---|---|",
            ]
        )
        for check in integration_checks:
            lines.append(
                f"| {check.name} | {check.expected} | {check.actual} | "
                f"{'FAIL' if check.failed else 'PASS'} |"
            )

    if current_run_result is not None:
        timings = current_run_result.get("timings")
        if isinstance(timings, dict):
            lines.extend(
                [
                    "",
                    "## Timing Snapshot (Current Run)",
                    "",
                    "```json",
                    json.dumps(timings, indent=2, sort_keys=True),
                    "```",
                ]
            )

    lines.extend(
        [
            "",
            "## Exact Metric Values",
            "",
            "### Baseline (raw)",
            "```json",
            json.dumps(baseline_metrics, indent=2, sort_keys=True),
            "```",
            "",
            "### Current (raw)",
            "```json",
            json.dumps(current_metrics, indent=2, sort_keys=True),
            "```",
        ]
    )
    return "\n".join(lines) + "\n"


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Phase 6 eval regression gate")
    parser.add_argument("--baseline-run-id", required=True, help="Reference baseline run id")
    parser.add_argument("--current-run-id", required=True, help="Current run id to evaluate")
    parser.add_argument("--baseline-file", required=True, type=Path, help="Baseline metrics JSON")
    parser.add_argument("--current-file", required=True, type=Path, help="Current metrics JSON")
    parser.add_argument(
        "--current-run-result-file",
        type=Path,
        help="Optional current run-result JSON from eval.run_eval",
    )
    parser.add_argument(
        "--require-real-integrations",
        action="store_true",
        help=(
            "Fail gate when current run did not use real RAGAS/DeepEval integrations "
            "for every row (no fallback allowed)."
        ),
    )
    parser.add_argument("--output-md", type=Path, help="Optional markdown report output path")
    parser.add_argument("--log-file", type=Path, help="Optional eval gate log file path")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    handlers: list[logging.Handler] = [logging.StreamHandler()]
    if args.log_file:
        args.log_file.parent.mkdir(parents=True, exist_ok=True)
        handlers.append(logging.FileHandler(args.log_file, encoding="utf-8"))
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s eval_gate %(message)s",
        handlers=handlers,
        force=True,
    )
    baseline = _parse_metrics_file(args.baseline_file)
    current = _parse_metrics_file(args.current_file)
    logger.info(
        "Starting eval gate baseline_run_id=%s current_run_id=%s",
        args.baseline_run_id,
        args.current_run_id,
    )
    logger.info("Baseline metrics raw=%s", json.dumps(baseline, sort_keys=True))
    logger.info("Current metrics raw=%s", json.dumps(current, sort_keys=True))

    checks = evaluate_regression(baseline, current)
    integration_checks: list[IntegrationCheck] | None = None
    current_run_result: dict | None = None
    if args.current_run_result_file:
        current_run_result = _parse_run_result_file(args.current_run_result_file)
    if args.require_real_integrations:
        if current_run_result is None:
            raise ValueError("--require-real-integrations requires --current-run-result-file")
        integration_checks = evaluate_integration_integrity(current_run_result)
        for check in integration_checks:
            logger.info(
                "Integration check name=%s expected=%s actual=%s failed=%s",
                check.name,
                check.expected,
                check.actual,
                check.failed,
            )

    for check in checks:
        logger.info(
            ("Metric=%s baseline=%.10f current=%.10f threshold=%.10f " "mode=%s failed=%s"),
            check.metric,
            check.baseline,
            check.current,
            check.threshold,
            check.mode,
            check.failed,
        )

    report = build_markdown_report(
        checks,
        args.baseline_run_id,
        args.current_run_id,
        baseline_metrics=baseline,
        current_metrics=current,
        integration_checks=integration_checks,
        current_run_result=current_run_result,
    )

    if args.output_md:
        args.output_md.parent.mkdir(parents=True, exist_ok=True)
        args.output_md.write_text(report, encoding="utf-8")

    print(report, end="")
    failed = any(check.failed for check in checks) or any(
        check.failed for check in (integration_checks or [])
    )
    logger.info("Eval gate decision=%s", "FAILED" if failed else "PASSED")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
