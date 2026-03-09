from __future__ import annotations

from eval.score import score_row


def test_score_row_prefers_integration_metrics(monkeypatch) -> None:
    monkeypatch.setattr(
        "eval.score._try_score_with_ragas",
        lambda row: (
            {
                "faithfulness": 0.81,
                "answer_relevancy": 0.82,
                "context_precision": 0.83,
                "context_recall": 0.84,
            },
            {"enabled": True, "used": True, "duration_ms": 1.0, "steps": {}, "error": None},
        ),
    )
    monkeypatch.setattr(
        "eval.score._try_score_with_deepeval",
        lambda row: (
            {
                "hallucination_rate": 0.12,
                "answer_completeness": 0.91,
            },
            {"enabled": True, "used": True, "duration_ms": 1.0, "steps": {}, "error": None},
        ),
    )

    row = {
        "question": "Q",
        "ground_truth": "G",
        "answer": "A",
        "retrieved_context": ["ctx"],
        "latency_ms": 220,
    }
    score = score_row(row)

    assert score.faithfulness == 0.81
    assert score.answer_relevancy == 0.82
    assert score.context_precision == 0.83
    assert score.context_recall == 0.84
    assert score.hallucination_rate == 0.12
    assert score.answer_completeness == 0.91
    assert score.latency_ms == 220


def test_score_row_can_disable_integrations(monkeypatch) -> None:
    monkeypatch.setattr(
        "eval.score._try_score_with_ragas",
        lambda row: (
            {"faithfulness": 0.0},
            {"enabled": True, "used": True, "duration_ms": 1.0, "steps": {}, "error": None},
        ),
    )
    monkeypatch.setattr(
        "eval.score._try_score_with_deepeval",
        lambda row: (
            {"hallucination_rate": 1.0},
            {"enabled": True, "used": True, "duration_ms": 1.0, "steps": {}, "error": None},
        ),
    )

    row = {
        "question": "How reset password?",
        "ground_truth": "Use reset link.",
        "answer": "Use reset link.",
        "retrieved_context": ["Use reset link from settings page."],
        "latency_ms": 180,
    }
    score = score_row(row, prefer_ragas=False, prefer_deepeval=False)

    assert score.faithfulness > 0
    assert score.answer_relevancy > 0
    assert score.context_precision > 0
    assert score.context_recall > 0
    assert score.hallucination_rate < 1
