from __future__ import annotations

from eval.run_eval import run_eval


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

    result = run_eval("tenant-eval", dataset, run_id="run-123")
    assert result["tenant"] == "tenant-eval"
    assert result["run_id"] == "run-123"
    assert result["status"] == "completed"
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
