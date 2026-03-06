from __future__ import annotations

import re
from dataclasses import dataclass
from statistics import mean
from typing import Any

TOKEN_RE = re.compile(r"[a-z0-9]+")


@dataclass(frozen=True)
class EvalScoreRow:
    faithfulness: float
    answer_relevancy: float
    context_precision: float
    context_recall: float
    hallucination_rate: float
    answer_completeness: float
    latency_ms: int


def _tokenize(text: str) -> set[str]:
    return set(TOKEN_RE.findall(text.lower()))


def _safe_div(numerator: float, denominator: float) -> float:
    if denominator <= 0:
        return 0.0
    return numerator / denominator


def _clamp01(value: float) -> float:
    return min(1.0, max(0.0, value))


def _token_overlap_score(lhs: str, rhs: str) -> float:
    lhs_tokens = _tokenize(lhs)
    rhs_tokens = _tokenize(rhs)
    if not lhs_tokens or not rhs_tokens:
        return 0.0
    return _safe_div(len(lhs_tokens & rhs_tokens), len(lhs_tokens | rhs_tokens))


def _try_score_with_ragas(row: dict[str, Any]) -> dict[str, float] | None:
    try:
        import ragas  # noqa: F401
    except Exception:
        return None
    # Lightweight placeholder hook: keep deterministic local fallback unless real integration
    # is explicitly implemented with dataset adapters/LLM credentials.
    return None


def _try_score_with_deepeval(row: dict[str, Any]) -> dict[str, float] | None:
    try:
        import deepeval  # noqa: F401
    except Exception:
        return None
    # Lightweight placeholder hook: keep deterministic local fallback unless real integration
    # is explicitly implemented with evaluator model credentials.
    return None


def score_row(
    row: dict[str, Any],
    prefer_ragas: bool = True,
    prefer_deepeval: bool = True,
) -> EvalScoreRow:
    ragas_metrics = _try_score_with_ragas(row) if prefer_ragas else None
    deepeval_metrics = _try_score_with_deepeval(row) if prefer_deepeval else None

    question = str(row.get("question") or "")
    ground_truth = str(row.get("ground_truth") or "")
    answer = str(row.get("answer") or "")
    contexts = row.get("retrieved_context") or row.get("retrieved_chunks") or []
    context_text = " ".join(str(chunk) for chunk in contexts)

    relevancy_fallback = _token_overlap_score(question, answer)
    faithfulness_fallback = _token_overlap_score(ground_truth, answer)
    precision_fallback = _token_overlap_score(answer, context_text)
    recall_fallback = _token_overlap_score(context_text, ground_truth)
    completeness_fallback = _token_overlap_score(answer, ground_truth)
    hallucination_fallback = 1.0 - precision_fallback

    faithfulness = _clamp01(float((ragas_metrics or {}).get("faithfulness", faithfulness_fallback)))
    answer_relevancy = _clamp01(
        float((ragas_metrics or {}).get("answer_relevancy", relevancy_fallback))
    )
    context_precision = _clamp01(
        float((ragas_metrics or {}).get("context_precision", precision_fallback))
    )
    context_recall = _clamp01(float((ragas_metrics or {}).get("context_recall", recall_fallback)))
    answer_completeness = _clamp01(
        float((deepeval_metrics or {}).get("answer_completeness", completeness_fallback))
    )
    hallucination_rate = _clamp01(
        float((deepeval_metrics or {}).get("hallucination_rate", hallucination_fallback))
    )

    raw_latency = row.get("latency_ms", 0)
    latency_ms = int(raw_latency) if isinstance(raw_latency, int | float) else 0
    latency_ms = max(0, latency_ms)

    return EvalScoreRow(
        faithfulness=faithfulness,
        answer_relevancy=answer_relevancy,
        context_precision=context_precision,
        context_recall=context_recall,
        hallucination_rate=hallucination_rate,
        answer_completeness=answer_completeness,
        latency_ms=latency_ms,
    )


def aggregate_scores(rows: list[EvalScoreRow]) -> dict[str, float]:
    if not rows:
        return {
            "faithfulness": 0.0,
            "answer_relevancy": 0.0,
            "context_precision": 0.0,
            "context_recall": 0.0,
            "hallucination_rate": 0.0,
            "answer_completeness": 0.0,
            "latency_ms": 0.0,
        }

    return {
        "faithfulness": round(mean(r.faithfulness for r in rows), 6),
        "answer_relevancy": round(mean(r.answer_relevancy for r in rows), 6),
        "context_precision": round(mean(r.context_precision for r in rows), 6),
        "context_recall": round(mean(r.context_recall for r in rows), 6),
        "hallucination_rate": round(mean(r.hallucination_rate for r in rows), 6),
        "answer_completeness": round(mean(r.answer_completeness for r in rows), 6),
        "latency_ms": round(mean(r.latency_ms for r in rows), 3),
    }
