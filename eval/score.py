from __future__ import annotations

import logging
import os
import re
from collections.abc import Mapping
from dataclasses import dataclass
from statistics import mean
from time import perf_counter
from typing import Any

TOKEN_RE = re.compile(r"[a-z0-9]+")
logger = logging.getLogger(__name__)

# Avoid outbound telemetry/noise in CI and locked-down environments.
os.environ.setdefault("DEEPEVAL_TELEMETRY_OPT_OUT", "1")
os.environ.setdefault("DEEPEVAL_DISABLE_DOTENV", "1")


@dataclass(frozen=True)
class EvalScoreRow:
    faithfulness: float
    answer_relevancy: float
    context_precision: float
    context_recall: float
    hallucination_rate: float
    answer_completeness: float
    latency_ms: int


def _extract_contexts(row: dict[str, Any]) -> list[str]:
    raw_contexts = (
        row.get("retrieved_context") or row.get("retrieved_chunks") or row.get("source_chunks")
    )
    if raw_contexts is None:
        return []
    if isinstance(raw_contexts, list):
        return [str(item) for item in raw_contexts]
    return [str(raw_contexts)]


def _tokenize(text: str) -> set[str]:
    return set(TOKEN_RE.findall(text.lower()))


def _safe_div(numerator: float, denominator: float) -> float:
    if denominator <= 0:
        return 0.0
    return numerator / denominator


def _clamp01(value: float) -> float:
    return min(1.0, max(0.0, value))


def _to_float(value: Any) -> float | None:
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _normalize_probability(value: float | None) -> float | None:
    if value is None:
        return None
    normalized = value
    if normalized > 1.0:
        if normalized <= 10.0:
            normalized = normalized / 10.0
        elif normalized <= 100.0:
            normalized = normalized / 100.0
    return _clamp01(normalized)


def _token_overlap_score(lhs: str, rhs: str) -> float:
    lhs_tokens = _tokenize(lhs)
    rhs_tokens = _tokenize(rhs)
    if not lhs_tokens or not rhs_tokens:
        return 0.0
    return _safe_div(len(lhs_tokens & rhs_tokens), len(lhs_tokens | rhs_tokens))


def _extract_ragas_metrics(result: Any) -> Mapping[str, Any] | None:
    if isinstance(result, Mapping):
        return result

    if hasattr(result, "to_pandas"):
        try:
            frame = result.to_pandas()
            if getattr(frame, "empty", True):
                return None
            row = frame.iloc[0]
            if hasattr(row, "to_dict"):
                parsed = row.to_dict()
                if isinstance(parsed, Mapping):
                    return parsed
        except Exception:
            pass

    scores_attr = getattr(result, "scores", None)
    if isinstance(scores_attr, Mapping):
        return scores_attr
    if isinstance(scores_attr, list) and scores_attr and isinstance(scores_attr[0], Mapping):
        return scores_attr[0]

    return None


def _pick_metric(metrics: Mapping[str, Any], *keys: str) -> float | None:
    for key in keys:
        if key in metrics:
            normalized = _normalize_probability(_to_float(metrics.get(key)))
            if normalized is not None:
                return normalized
    return None


def _duration_ms(started: float) -> float:
    return round((perf_counter() - started) * 1000.0, 3)


def _try_score_with_ragas(row: dict[str, Any]) -> tuple[dict[str, float] | None, dict[str, Any]]:
    integration_started = perf_counter()
    trace: dict[str, Any] = {
        "enabled": True,
        "used": False,
        "duration_ms": 0.0,
        "steps": {},
        "error": None,
    }
    question = str(row.get("question") or "")
    answer = str(row.get("answer") or "")
    ground_truth = str(row.get("ground_truth") or "")
    contexts = _extract_contexts(row)
    if not question or not answer or not ground_truth or not contexts:
        trace["error"] = "missing_required_fields"
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace

    import_started = perf_counter()
    try:
        from datasets import Dataset
        from langchain_openai import ChatOpenAI, OpenAIEmbeddings
        from ragas import evaluate
        from ragas.embeddings import LangchainEmbeddingsWrapper
        from ragas.llms import LangchainLLMWrapper
        from ragas.metrics import answer_relevancy, context_precision, context_recall, faithfulness
    except Exception as exc:
        trace["error"] = f"import_failed:{type(exc).__name__}:{exc}"
        trace["steps"]["import_ms"] = _duration_ms(import_started)
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace
    trace["steps"]["import_ms"] = _duration_ms(import_started)

    dataset_started = perf_counter()
    try:
        dataset = Dataset.from_dict(
            {
                "question": [question],
                "answer": [answer],
                "contexts": [contexts],
                "ground_truth": [ground_truth],
            }
        )
        metrics = [faithfulness, answer_relevancy, context_precision, context_recall]
        trace["steps"]["dataset_build_ms"] = _duration_ms(dataset_started)

        model_setup_started = perf_counter()
        ragas_model = (
            os.getenv("RAGAS_MODEL")
            or os.getenv("DEEPEVAL_MODEL")
            or os.getenv("LLAMA_LLM_MODEL")
            or "Qwen3-4B-Q4_K_M.gguf"
        )
        ragas_base_url = (
            os.getenv("RAGAS_BASE_URL")
            or os.getenv("DEEPEVAL_BASE_URL")
            or os.getenv("LLAMA_LLM_URL")
            or "http://localhost:8082/v1"
        )
        ragas_api_key = (
            os.getenv("RAGAS_API_KEY")
            or os.getenv("DEEPEVAL_API_KEY")
            or os.getenv("OPENAI_API_KEY")
            or "local-selfhosted"
        )
        ragas_embedding_model = (
            os.getenv("RAGAS_EMBEDDING_MODEL")
            or os.getenv("LLAMA_EMBEDDING_MODEL")
            or "bge-large-en-v1.5-q8_0.gguf"
        )
        ragas_embedding_base_url = (
            os.getenv("RAGAS_EMBEDDING_BASE_URL")
            or os.getenv("LLAMA_EMBEDDING_URL")
            or "http://localhost:8081/v1"
        )
        llm = LangchainLLMWrapper(
            ChatOpenAI(
                model=ragas_model,
                api_key=ragas_api_key,
                base_url=ragas_base_url,
                temperature=0.0,
            )
        )
        embeddings = LangchainEmbeddingsWrapper(
            OpenAIEmbeddings(
                model=ragas_embedding_model,
                api_key=ragas_api_key,
                base_url=ragas_embedding_base_url,
            )
        )
        trace["steps"]["model_setup_ms"] = _duration_ms(model_setup_started)

        evaluate_started = perf_counter()
        try:
            result = evaluate(
                dataset=dataset,
                metrics=metrics,
                llm=llm,
                embeddings=embeddings,
                raise_exceptions=False,
            )
        except TypeError:
            result = evaluate(
                dataset=dataset,
                metrics=metrics,
                llm=llm,
                embeddings=embeddings,
            )
        trace["steps"]["evaluate_ms"] = _duration_ms(evaluate_started)
    except Exception as exc:
        logger.info("RAGAS scoring unavailable for row; using fallback metrics. reason=%s", exc)
        trace["error"] = f"evaluate_failed:{type(exc).__name__}:{exc}"
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace

    parse_started = perf_counter()
    values = _extract_ragas_metrics(result)
    if values is None:
        trace["error"] = "metrics_parse_failed"
        trace["steps"]["parse_metrics_ms"] = _duration_ms(parse_started)
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace

    parsed: dict[str, float] = {}
    faithfulness_score = _pick_metric(values, "faithfulness")
    answer_relevancy_score = _pick_metric(values, "answer_relevancy", "answer_relevance")
    context_precision_score = _pick_metric(values, "context_precision")
    context_recall_score = _pick_metric(values, "context_recall")

    if faithfulness_score is not None:
        parsed["faithfulness"] = faithfulness_score
    if answer_relevancy_score is not None:
        parsed["answer_relevancy"] = answer_relevancy_score
    if context_precision_score is not None:
        parsed["context_precision"] = context_precision_score
    if context_recall_score is not None:
        parsed["context_recall"] = context_recall_score

    trace["steps"]["parse_metrics_ms"] = _duration_ms(parse_started)
    trace["used"] = bool(parsed)
    trace["duration_ms"] = _duration_ms(integration_started)
    return (parsed or None), trace


def _try_score_with_deepeval(row: dict[str, Any]) -> tuple[dict[str, float] | None, dict[str, Any]]:
    integration_started = perf_counter()
    trace: dict[str, Any] = {
        "enabled": True,
        "used": False,
        "duration_ms": 0.0,
        "steps": {},
        "error": None,
    }
    question = str(row.get("question") or "")
    answer = str(row.get("answer") or "")
    ground_truth = str(row.get("ground_truth") or "")
    contexts = _extract_contexts(row)
    if not question or not answer:
        trace["error"] = "missing_required_fields"
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace

    import_started = perf_counter()
    try:
        from deepeval import assert_test
        from deepeval.metrics import GEval, HallucinationMetric
        from deepeval.models.llms.openai_model import GPTModel
        from deepeval.test_case import LLMTestCase, LLMTestCaseParams
    except Exception as exc:
        trace["error"] = f"import_failed:{type(exc).__name__}:{exc}"
        trace["steps"]["import_ms"] = _duration_ms(import_started)
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace
    trace["steps"]["import_ms"] = _duration_ms(import_started)

    model_name = (
        os.getenv("DEEPEVAL_MODEL") or os.getenv("LLAMA_LLM_MODEL") or "Qwen3-4B-Q4_K_M.gguf"
    )
    base_url = (
        os.getenv("DEEPEVAL_BASE_URL") or os.getenv("LLAMA_LLM_URL") or "http://localhost:8082/v1"
    )
    api_key = os.getenv("DEEPEVAL_API_KEY") or os.getenv("OPENAI_API_KEY") or "local-selfhosted"

    model_init_started = perf_counter()
    try:
        judge_model = GPTModel(
            model=model_name,
            api_key=api_key,
            base_url=base_url,
            temperature=0.0,
        )
    except Exception as exc:
        logger.info(
            (
                "DeepEval local model init failed; fallback metrics used. "
                "model=%s base_url=%s reason=%s"
            ),
            model_name,
            base_url,
            exc,
        )
        trace["error"] = f"model_init_failed:{type(exc).__name__}:{exc}"
        trace["steps"]["model_init_ms"] = _duration_ms(model_init_started)
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace
    trace["steps"]["model_init_ms"] = _duration_ms(model_init_started)

    test_case_kwargs: dict[str, Any] = {
        "input": question,
        "actual_output": answer,
    }
    if ground_truth:
        test_case_kwargs["expected_output"] = ground_truth
    if contexts:
        test_case_kwargs["context"] = contexts
        test_case_kwargs["retrieval_context"] = contexts

    test_case_started = perf_counter()
    try:
        test_case = LLMTestCase(**test_case_kwargs)
    except Exception as exc:
        logger.info("DeepEval test case construction failed; fallback metrics used. reason=%s", exc)
        trace["error"] = f"test_case_failed:{type(exc).__name__}:{exc}"
        trace["steps"]["test_case_build_ms"] = _duration_ms(test_case_started)
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace
    trace["steps"]["test_case_build_ms"] = _duration_ms(test_case_started)

    evaluation_params = [LLMTestCaseParams.INPUT, LLMTestCaseParams.ACTUAL_OUTPUT]
    if ground_truth:
        evaluation_params.append(LLMTestCaseParams.EXPECTED_OUTPUT)

    metrics_init_started = perf_counter()
    try:
        completeness_metric = GEval(
            name="answer_completeness",
            criteria=(
                "Evaluate whether the answer covers all essential points from the expected output."
            ),
            evaluation_params=evaluation_params,
            model=judge_model,
            threshold=0.0,
        )
    except TypeError:
        completeness_metric = GEval(
            name="answer_completeness",
            criteria=(
                "Evaluate whether the answer covers all essential points from the expected output."
            ),
            evaluation_params=evaluation_params,
            model=judge_model,
        )

    try:
        hallucination_metric = HallucinationMetric(threshold=1.0, model=judge_model)
    except TypeError:
        hallucination_metric = HallucinationMetric(model=judge_model)
    trace["steps"]["metrics_init_ms"] = _duration_ms(metrics_init_started)

    metrics = [completeness_metric, hallucination_metric]

    measure_started = perf_counter()
    for metric in metrics:
        try:
            metric.measure(test_case)
        except Exception as exc:
            logger.info("DeepEval metric measure failed; fallback metrics used. reason=%s", exc)
            trace["error"] = f"measure_failed:{type(exc).__name__}:{exc}"
            trace["steps"]["measure_ms"] = _duration_ms(measure_started)
            trace["duration_ms"] = _duration_ms(integration_started)
            return None, trace
    trace["steps"]["measure_ms"] = _duration_ms(measure_started)

    assert_started = perf_counter()
    try:
        assert_test(test_case, metrics)
    except AssertionError:
        # We only need metric values here; assertion thresholds are handled by eval-gate.
        pass
    except Exception as exc:
        logger.info("DeepEval assert_test failed; fallback metrics used. reason=%s", exc)
        trace["error"] = f"assert_failed:{type(exc).__name__}:{exc}"
        trace["steps"]["assert_ms"] = _duration_ms(assert_started)
        trace["duration_ms"] = _duration_ms(integration_started)
        return None, trace
    trace["steps"]["assert_ms"] = _duration_ms(assert_started)

    parse_started = perf_counter()
    completeness_score = _normalize_probability(
        _to_float(getattr(completeness_metric, "score", None))
    )
    hallucination_score = _normalize_probability(
        _to_float(getattr(hallucination_metric, "score", None))
    )

    parsed: dict[str, float] = {}
    if completeness_score is not None:
        parsed["answer_completeness"] = completeness_score
    if hallucination_score is not None:
        parsed["hallucination_rate"] = hallucination_score

    trace["steps"]["parse_metrics_ms"] = _duration_ms(parse_started)
    trace["used"] = bool(parsed)
    trace["duration_ms"] = _duration_ms(integration_started)
    return (parsed or None), trace


def score_row_with_trace(
    row: dict[str, Any],
    prefer_ragas: bool = True,
    prefer_deepeval: bool = True,
) -> tuple[EvalScoreRow, dict[str, Any]]:
    started = perf_counter()

    if prefer_ragas:
        ragas_metrics, ragas_trace = _try_score_with_ragas(row)
    else:
        ragas_metrics = None
        ragas_trace = {
            "enabled": False,
            "used": False,
            "duration_ms": 0.0,
            "steps": {},
            "error": "disabled",
        }

    if prefer_deepeval:
        deepeval_metrics, deepeval_trace = _try_score_with_deepeval(row)
    else:
        deepeval_metrics = None
        deepeval_trace = {
            "enabled": False,
            "used": False,
            "duration_ms": 0.0,
            "steps": {},
            "error": "disabled",
        }

    question = str(row.get("question") or "")
    ground_truth = str(row.get("ground_truth") or "")
    answer = str(row.get("answer") or "")
    contexts = _extract_contexts(row)
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

    scored = EvalScoreRow(
        faithfulness=faithfulness,
        answer_relevancy=answer_relevancy,
        context_precision=context_precision,
        context_recall=context_recall,
        hallucination_rate=hallucination_rate,
        answer_completeness=answer_completeness,
        latency_ms=latency_ms,
    )
    trace = {
        "total_ms": round((perf_counter() - started) * 1000.0, 3),
        "fallback_used": ragas_metrics is None or deepeval_metrics is None,
        "ragas": ragas_trace,
        "deepeval": deepeval_trace,
    }
    return scored, trace


def score_row(
    row: dict[str, Any],
    prefer_ragas: bool = True,
    prefer_deepeval: bool = True,
) -> EvalScoreRow:
    scored, _ = score_row_with_trace(
        row,
        prefer_ragas=prefer_ragas,
        prefer_deepeval=prefer_deepeval,
    )
    return scored


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
