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
    lines.extend(["", f"- Gate result: **{'FAILED' if failed else 'PASSED'}**"])
    if failed:
        lines.append(f"- Regressed metrics: {', '.join(failed)}")
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
    )

    if args.output_md:
        args.output_md.parent.mkdir(parents=True, exist_ok=True)
        args.output_md.write_text(report, encoding="utf-8")

    print(report, end="")
    failed = any(check.failed for check in checks)
    logger.info("Eval gate decision=%s", "FAILED" if failed else "PASSED")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
