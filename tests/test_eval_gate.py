from __future__ import annotations

import json
from pathlib import Path

from eval.eval_gate import build_markdown_report, evaluate_regression, main


def _fixture(name: str) -> Path:
    return Path(__file__).parent / "fixtures" / "eval" / name


def test_eval_gate_passes_within_thresholds(tmp_path: Path) -> None:
    output = tmp_path / "gate.md"
    rc = main(
        [
            "--baseline-run-id",
            "baseline-1",
            "--current-run-id",
            "current-1",
            "--baseline-file",
            str(_fixture("baseline.json")),
            "--current-file",
            str(_fixture("current-pass.json")),
            "--output-md",
            str(output),
        ]
    )

    assert rc == 0
    assert output.exists()
    text = output.read_text(encoding="utf-8")
    assert "Gate result: **PASSED**" in text


def test_eval_gate_fails_on_regression() -> None:
    rc = main(
        [
            "--baseline-run-id",
            "baseline-1",
            "--current-run-id",
            "current-2",
            "--baseline-file",
            str(_fixture("baseline.json")),
            "--current-file",
            str(_fixture("current-fail.json")),
        ]
    )

    assert rc == 1


def test_markdown_report_includes_failed_metric() -> None:
    baseline = {
        "faithfulness": 0.9,
        "answer_relevancy": 0.9,
        "context_precision": 0.9,
        "context_recall": 0.9,
        "hallucination_rate": 0.05,
    }
    current = {
        "faithfulness": 0.7,
        "answer_relevancy": 0.9,
        "context_precision": 0.9,
        "context_recall": 0.9,
        "hallucination_rate": 0.06,
    }
    checks = evaluate_regression(
        baseline=baseline,
        current=current,
    )
    report = build_markdown_report(
        checks, "b", "c", baseline_metrics=baseline, current_metrics=current
    )
    assert "faithfulness" in report
    assert "FAIL" in report
    assert "Exact Metric Values" in report
    assert '"faithfulness": 0.9' in report


def test_eval_gate_real_integration_check_passes(tmp_path: Path) -> None:
    output = tmp_path / "gate-real.md"
    run_result = tmp_path / "current-run-result.json"
    run_result.write_text(
        json.dumps(
            {
                "integrations": {
                    "ragas_enabled": True,
                    "deepeval_enabled": True,
                    "ragas_required": True,
                    "deepeval_required": True,
                },
                "timings": {
                    "rows_count": 2,
                    "rows_with_ragas": 2,
                    "rows_with_deepeval": 2,
                    "rows_with_fallback": 0,
                    "rows_with_explicit_answer": 2,
                    "rows_with_explicit_context": 2,
                    "rows_answer_equals_ground_truth": 1,
                    "ragas_error_count": 0,
                    "deepeval_error_count": 0,
                },
                "integration_errors": {
                    "ragas": [],
                    "deepeval": [],
                },
            }
        ),
        encoding="utf-8",
    )
    rc = main(
        [
            "--baseline-run-id",
            "baseline-1",
            "--current-run-id",
            "current-real",
            "--baseline-file",
            str(_fixture("baseline.json")),
            "--current-file",
            str(_fixture("current-pass.json")),
            "--current-run-result-file",
            str(run_result),
            "--require-real-integrations",
            "--output-md",
            str(output),
        ]
    )

    assert rc == 0
    assert "Integration integrity: **PASSED**" in output.read_text(encoding="utf-8")


def test_eval_gate_real_integration_check_fails_on_fallback(tmp_path: Path) -> None:
    run_result = tmp_path / "current-run-result.json"
    run_result.write_text(
        json.dumps(
            {
                "integrations": {
                    "ragas_enabled": True,
                    "deepeval_enabled": True,
                    "ragas_required": True,
                    "deepeval_required": True,
                },
                "timings": {
                    "rows_count": 2,
                    "rows_with_ragas": 2,
                    "rows_with_deepeval": 1,
                    "rows_with_fallback": 1,
                    "rows_with_explicit_answer": 1,
                    "rows_with_explicit_context": 1,
                    "rows_answer_equals_ground_truth": 2,
                    "ragas_error_count": 0,
                    "deepeval_error_count": 1,
                },
                "integration_errors": {
                    "ragas": [],
                    "deepeval": ["import_failed:ModuleNotFoundError"],
                },
            }
        ),
        encoding="utf-8",
    )
    rc = main(
        [
            "--baseline-run-id",
            "baseline-1",
            "--current-run-id",
            "current-fallback",
            "--baseline-file",
            str(_fixture("baseline.json")),
            "--current-file",
            str(_fixture("current-pass.json")),
            "--current-run-result-file",
            str(run_result),
            "--require-real-integrations",
        ]
    )
    assert rc == 1
