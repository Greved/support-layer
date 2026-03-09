from __future__ import annotations

import argparse
import json
import uuid
from datetime import UTC, datetime
from pathlib import Path
from time import perf_counter
from typing import Any

from eval.score import aggregate_scores, score_row_with_trace


def _read_dataset(path: Path) -> list[dict[str, Any]]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(raw, dict) and isinstance(raw.get("rows"), list):
        return [dict(row) for row in raw["rows"]]
    if isinstance(raw, list):
        return [dict(row) for row in raw]
    raise ValueError(f"Unsupported dataset format in {path}")


def _percentile(values: list[float], percentile: float) -> float:
    if not values:
        return 0.0
    if len(values) == 1:
        return round(values[0], 3)
    rank = (len(values) - 1) * percentile
    low = int(rank)
    high = min(len(values) - 1, low + 1)
    fraction = rank - low
    return round(values[low] + (values[high] - values[low]) * fraction, 3)


def _timing_stats(values: list[float], key_prefix: str) -> dict[str, float]:
    if not values:
        return {
            f"{key_prefix}_sum_ms": 0.0,
            f"{key_prefix}_avg_ms": 0.0,
            f"{key_prefix}_p50_ms": 0.0,
            f"{key_prefix}_p95_ms": 0.0,
            f"{key_prefix}_max_ms": 0.0,
        }
    sorted_values = sorted(values)
    return {
        f"{key_prefix}_sum_ms": round(sum(sorted_values), 3),
        f"{key_prefix}_avg_ms": round(sum(sorted_values) / len(sorted_values), 3),
        f"{key_prefix}_p50_ms": _percentile(sorted_values, 0.50),
        f"{key_prefix}_p95_ms": _percentile(sorted_values, 0.95),
        f"{key_prefix}_max_ms": round(sorted_values[-1], 3),
    }


def _build_eval_rows(dataset_rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    eval_rows: list[dict[str, Any]] = []
    for row in dataset_rows:
        question = str(row.get("question") or "")
        ground_truth = str(row.get("ground_truth") or "")
        explicit_answer = bool(str(row.get("answer") or "").strip())
        explicit_context = bool(row.get("retrieved_context") or row.get("source_chunks"))
        answer = str(row.get("answer") or ground_truth)
        retrieved_context = (
            row.get("retrieved_context") or row.get("source_chunks") or [ground_truth]
        )
        eval_rows.append(
            {
                "question": question,
                "ground_truth": ground_truth,
                "answer": answer,
                "retrieved_context": retrieved_context,
                "latency_ms": int(row.get("latency_ms") or 180),
                "__input_meta": {
                    "explicit_answer": explicit_answer,
                    "explicit_context": explicit_context,
                },
            }
        )
    return eval_rows


def run_eval(
    tenant: str,
    dataset_rows: list[dict[str, Any]],
    run_id: str | None = None,
    use_ragas: bool = True,
    use_deepeval: bool = True,
    require_ragas: bool = False,
    require_deepeval: bool = False,
) -> dict[str, Any]:
    total_started = perf_counter()
    if not tenant.strip():
        raise ValueError("tenant must be non-empty")

    run_identifier = run_id or f"run-{uuid.uuid4()}"
    started_at = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")

    build_started = perf_counter()
    eval_rows = _build_eval_rows(dataset_rows)
    build_eval_rows_ms = round((perf_counter() - build_started) * 1000.0, 3)
    rows_with_explicit_answer = sum(
        1 for row in eval_rows if bool(row.get("__input_meta", {}).get("explicit_answer"))
    )
    rows_with_explicit_context = sum(
        1 for row in eval_rows if bool(row.get("__input_meta", {}).get("explicit_context"))
    )
    rows_answer_equals_ground_truth = sum(
        1
        for row in eval_rows
        if str(row.get("answer") or "").strip().casefold()
        == str(row.get("ground_truth") or "").strip().casefold()
    )
    scored_rows = []
    aggregate_input = []
    score_loop_started = perf_counter()

    for index, eval_row in enumerate(eval_rows):
        scores, trace = score_row_with_trace(
            eval_row,
            prefer_ragas=use_ragas,
            prefer_deepeval=use_deepeval,
        )
        aggregate_input.append(scores)
        scored_rows.append(
            {
                "index": index,
                "question": eval_row["question"],
                "ground_truth": eval_row["ground_truth"],
                "answer": eval_row["answer"],
                "retrieved_context": eval_row["retrieved_context"],
                "faithfulness": scores.faithfulness,
                "answer_relevancy": scores.answer_relevancy,
                "context_precision": scores.context_precision,
                "context_recall": scores.context_recall,
                "hallucination_rate": scores.hallucination_rate,
                "answer_completeness": scores.answer_completeness,
                "latency_ms": scores.latency_ms,
                "timings": trace,
            }
        )
    scoring_total_ms = round((perf_counter() - score_loop_started) * 1000.0, 3)

    aggregate_started = perf_counter()
    metrics = aggregate_scores(aggregate_input)
    aggregate_metrics_ms = round((perf_counter() - aggregate_started) * 1000.0, 3)

    ragas_duration_ms = round(
        sum(float(row["timings"]["ragas"]["duration_ms"]) for row in scored_rows),
        3,
    )
    deepeval_duration_ms = round(
        sum(float(row["timings"]["deepeval"]["duration_ms"]) for row in scored_rows),
        3,
    )
    rows_with_ragas = sum(1 for row in scored_rows if bool(row["timings"]["ragas"]["used"]))
    rows_with_deepeval = sum(1 for row in scored_rows if bool(row["timings"]["deepeval"]["used"]))
    rows_with_fallback = sum(1 for row in scored_rows if bool(row["timings"]["fallback_used"]))

    ragas_step_names = ["import_ms", "dataset_build_ms", "evaluate_ms", "parse_metrics_ms"]
    deepeval_step_names = [
        "import_ms",
        "model_init_ms",
        "test_case_build_ms",
        "metrics_init_ms",
        "measure_ms",
        "assert_ms",
        "parse_metrics_ms",
    ]
    ragas_step_totals = {
        f"ragas_{step}": round(
            sum(float(row["timings"]["ragas"]["steps"].get(step, 0.0)) for row in scored_rows),
            3,
        )
        for step in ragas_step_names
    }
    deepeval_step_totals = {
        f"deepeval_{step}": round(
            sum(float(row["timings"]["deepeval"]["steps"].get(step, 0.0)) for row in scored_rows),
            3,
        )
        for step in deepeval_step_names
    }

    row_total_ms = [float(row["timings"]["total_ms"]) for row in scored_rows]
    row_total_sorted = sorted(row_total_ms)

    ragas_errors = sorted(
        {
            str(row["timings"]["ragas"].get("error") or "")
            for row in scored_rows
            if not bool(row["timings"]["ragas"]["used"])
            and str(row["timings"]["ragas"].get("error") or "")
        }
    )
    deepeval_errors = sorted(
        {
            str(row["timings"]["deepeval"].get("error") or "")
            for row in scored_rows
            if not bool(row["timings"]["deepeval"]["used"])
            and str(row["timings"]["deepeval"].get("error") or "")
        }
    )

    ragas_duration_values = [float(row["timings"]["ragas"]["duration_ms"]) for row in scored_rows]
    deepeval_duration_values = [
        float(row["timings"]["deepeval"]["duration_ms"]) for row in scored_rows
    ]
    ragas_step_breakdown: dict[str, float] = {}
    for step in ragas_step_names:
        values = [float(row["timings"]["ragas"]["steps"].get(step, 0.0)) for row in scored_rows]
        step_prefix = step[:-3] if step.endswith("_ms") else step
        ragas_step_breakdown.update(_timing_stats(values, f"ragas_{step_prefix}"))
    deepeval_step_breakdown: dict[str, float] = {}
    for step in deepeval_step_names:
        values = [float(row["timings"]["deepeval"]["steps"].get(step, 0.0)) for row in scored_rows]
        step_prefix = step[:-3] if step.endswith("_ms") else step
        deepeval_step_breakdown.update(_timing_stats(values, f"deepeval_{step_prefix}"))

    if require_ragas and rows_with_ragas < len(scored_rows):
        raise RuntimeError(
            "RAGAS was required but not used for all rows. "
            f"rows_with_ragas={rows_with_ragas} rows_count={len(scored_rows)} "
            f"errors={ragas_errors}"
        )

    if require_deepeval and rows_with_deepeval < len(scored_rows):
        raise RuntimeError(
            "DeepEval was required but not used for all rows. "
            f"rows_with_deepeval={rows_with_deepeval} rows_count={len(scored_rows)} "
            f"errors={deepeval_errors}"
        )

    if (require_ragas or require_deepeval) and rows_with_explicit_answer < len(scored_rows):
        raise RuntimeError(
            "Strict eval requires explicit answers for every row. "
            f"rows_with_explicit_answer={rows_with_explicit_answer} rows_count={len(scored_rows)}"
        )

    if (require_ragas or require_deepeval) and rows_with_explicit_context < len(scored_rows):
        raise RuntimeError(
            "Strict eval requires explicit retrieved_context/source_chunks for every row. "
            f"rows_with_explicit_context={rows_with_explicit_context} rows_count={len(scored_rows)}"
        )

    if (require_ragas or require_deepeval) and rows_answer_equals_ground_truth >= len(scored_rows):
        raise RuntimeError(
            "Strict eval requires non-trivial candidate answers. "
            "All answers are identical to ground_truth."
        )

    finished_at = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    return {
        "tenant": tenant,
        "run_id": run_identifier,
        "started_at": started_at,
        "finished_at": finished_at,
        "status": "completed",
        "integrations": {
            "ragas_enabled": use_ragas,
            "deepeval_enabled": use_deepeval,
            "ragas_required": require_ragas,
            "deepeval_required": require_deepeval,
        },
        "timings": {
            "build_eval_rows_ms": build_eval_rows_ms,
            "scoring_total_ms": scoring_total_ms,
            "aggregate_metrics_ms": aggregate_metrics_ms,
            "ragas_duration_ms": ragas_duration_ms,
            "deepeval_duration_ms": deepeval_duration_ms,
            **_timing_stats(row_total_ms, "row_total"),
            **_timing_stats(ragas_duration_values, "ragas_duration"),
            **_timing_stats(deepeval_duration_values, "deepeval_duration"),
            "rows_with_ragas": rows_with_ragas,
            "rows_with_deepeval": rows_with_deepeval,
            "rows_with_fallback": rows_with_fallback,
            "rows_with_explicit_answer": rows_with_explicit_answer,
            "rows_with_explicit_context": rows_with_explicit_context,
            "rows_answer_equals_ground_truth": rows_answer_equals_ground_truth,
            "rows_count": len(scored_rows),
            "row_total_ms_p50": _percentile(row_total_sorted, 0.50),
            "row_total_ms_p95": _percentile(row_total_sorted, 0.95),
            "ragas_error_count": len(ragas_errors),
            "deepeval_error_count": len(deepeval_errors),
            "total_ms": round((perf_counter() - total_started) * 1000.0, 3),
            **ragas_step_totals,
            **deepeval_step_totals,
            **ragas_step_breakdown,
            **deepeval_step_breakdown,
        },
        "metrics": metrics,
        "rows": scored_rows,
        "integration_errors": {
            "ragas": ragas_errors,
            "deepeval": deepeval_errors,
        },
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run Phase 6 eval scoring on a dataset")
    parser.add_argument("--tenant", required=True, help="Tenant slug")
    parser.add_argument("--dataset-file", required=True, type=Path, help="Input dataset JSON")
    parser.add_argument("--run-id", help="Optional explicit run id")
    parser.add_argument(
        "--disable-ragas",
        action="store_true",
        help="Disable RAGAS scoring integration and use fallback scoring only",
    )
    parser.add_argument(
        "--disable-deepeval",
        action="store_true",
        help="Disable DeepEval scoring integration and use fallback scoring only",
    )
    parser.add_argument(
        "--require-ragas",
        action="store_true",
        help="Fail run when RAGAS is not used for every row.",
    )
    parser.add_argument(
        "--require-deepeval",
        action="store_true",
        help="Fail run when DeepEval is not used for every row.",
    )
    parser.add_argument(
        "--output-file",
        type=Path,
        default=Path("artifacts/eval/run-result.json"),
        help="Output JSON with row-level eval scores",
    )
    parser.add_argument(
        "--metrics-file",
        type=Path,
        default=Path("artifacts/eval/current-metrics.json"),
        help="Output JSON with aggregate metrics",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    read_started = perf_counter()
    dataset_rows = _read_dataset(args.dataset_file)
    read_dataset_ms = round((perf_counter() - read_started) * 1000.0, 3)
    result = run_eval(
        args.tenant,
        dataset_rows,
        args.run_id,
        use_ragas=not args.disable_ragas,
        use_deepeval=not args.disable_deepeval,
        require_ragas=args.require_ragas,
        require_deepeval=args.require_deepeval,
    )
    result.setdefault("timings", {})
    result["timings"]["read_dataset_ms"] = read_dataset_ms

    write_result_started = perf_counter()
    args.output_file.parent.mkdir(parents=True, exist_ok=True)
    args.output_file.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    write_output_ms = round((perf_counter() - write_result_started) * 1000.0, 3)

    write_metrics_started = perf_counter()
    args.metrics_file.parent.mkdir(parents=True, exist_ok=True)
    args.metrics_file.write_text(json.dumps(result["metrics"], indent=2) + "\n", encoding="utf-8")
    write_metrics_ms = round((perf_counter() - write_metrics_started) * 1000.0, 3)
    result["timings"]["write_output_ms"] = write_output_ms
    result["timings"]["write_metrics_ms"] = write_metrics_ms

    args.output_file.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")

    print(
        json.dumps(
            {
                "tenant": result["tenant"],
                "run_id": result["run_id"],
                "rows": len(result["rows"]),
                "timings": result.get("timings", {}),
                "metrics_file": str(args.metrics_file),
                "output_file": str(args.output_file),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
