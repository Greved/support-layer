from __future__ import annotations

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
