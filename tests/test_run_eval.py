from __future__ import annotations

import pytest

import eval.run_eval as run_eval_module
from eval.run_eval import run_eval
from eval.score import EvalScoreRow


def test_run_eval_produces_metrics_and_rows() -> None:
    dataset = [
        {
            "question": "How do I reset password?",
            "ground_truth": "Use the reset link from account settings.",
            "source_chunks": ["Use the reset link from account settings."],
        },
        {
            "question": "What to do on SLA breach?",
            "ground_truth": "Escalate to incident lead and issue credit workflow.",
            "source_chunks": ["Escalate to incident lead and issue credit workflow."],
        },
    ]

    result = run_eval(
        "tenant-eval",
        dataset,
        run_id="run-123",
        use_ragas=False,
        use_deepeval=False,
    )
    assert result["tenant"] == "tenant-eval"
    assert result["run_id"] == "run-123"
    assert result["status"] == "completed"
    assert result["integrations"]["ragas_enabled"] is False
    assert result["integrations"]["deepeval_enabled"] is False
    assert len(result["rows"]) == 2

    metrics = result["metrics"]
    assert set(metrics.keys()) == {
        "faithfulness",
        "answer_relevancy",
        "context_precision",
        "context_recall",
        "hallucination_rate",
        "answer_completeness",
        "latency_ms",
    }
    assert metrics["faithfulness"] >= 0
    assert metrics["faithfulness"] <= 1
    timings = result["timings"]
    assert timings["rows_count"] == 2
    assert "read_dataset_ms" not in timings
    assert "row_total_sum_ms" in timings
    assert "row_total_avg_ms" in timings
    assert "row_total_p50_ms" in timings
    assert "row_total_p95_ms" in timings
    assert "row_total_max_ms" in timings
    assert "ragas_duration_avg_ms" in timings
    assert "deepeval_duration_avg_ms" in timings
    assert "ragas_import_sum_ms" in timings
    assert "deepeval_import_sum_ms" in timings
    assert timings["rows_with_explicit_answer"] == 0
    assert timings["rows_with_explicit_context"] == 2
    assert timings["rows_answer_equals_ground_truth"] == 2


def test_run_eval_can_disable_integrations() -> None:
    dataset = [
        {
            "question": "Q",
            "ground_truth": "G",
            "source_chunks": ["G"],
        }
    ]

    result = run_eval("tenant-eval", dataset, run_id="run-124", use_ragas=False, use_deepeval=False)
    assert result["integrations"]["ragas_enabled"] is False
    assert result["integrations"]["deepeval_enabled"] is False


def test_run_eval_requirements_reject_trivial_inputs(monkeypatch) -> None:
    def _score_stub(row, prefer_ragas=True, prefer_deepeval=True):
        return (
            EvalScoreRow(
                faithfulness=0.5,
                answer_relevancy=0.5,
                context_precision=0.5,
                context_recall=0.5,
                hallucination_rate=0.5,
                answer_completeness=0.5,
                latency_ms=180,
            ),
            {
                "total_ms": 1.0,
                "fallback_used": False,
                "ragas": {
                    "enabled": True,
                    "used": True,
                    "duration_ms": 1.0,
                    "steps": {},
                    "error": None,
                },
                "deepeval": {
                    "enabled": True,
                    "used": True,
                    "duration_ms": 1.0,
                    "steps": {},
                    "error": None,
                },
            },
        )

    monkeypatch.setattr(run_eval_module, "score_row_with_trace", _score_stub)

    dataset = [
        {
            "question": "Q",
            "ground_truth": "G",
        }
    ]

    with pytest.raises(RuntimeError, match="explicit answers"):
        run_eval_module.run_eval(
            "tenant-eval",
            dataset,
            run_id="run-strict",
            require_ragas=True,
            require_deepeval=True,
        )
