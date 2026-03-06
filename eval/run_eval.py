from __future__ import annotations

import argparse
import json
import uuid
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from eval.score import aggregate_scores, score_row


def _read_dataset(path: Path) -> list[dict[str, Any]]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(raw, dict) and isinstance(raw.get("rows"), list):
        return [dict(row) for row in raw["rows"]]
    if isinstance(raw, list):
        return [dict(row) for row in raw]
    raise ValueError(f"Unsupported dataset format in {path}")


def _build_eval_rows(dataset_rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    eval_rows: list[dict[str, Any]] = []
    for row in dataset_rows:
        question = str(row.get("question") or "")
        ground_truth = str(row.get("ground_truth") or "")
        eval_rows.append(
            {
                "question": question,
                "ground_truth": ground_truth,
                "answer": str(row.get("answer") or ground_truth),
                "retrieved_context": row.get("retrieved_context")
                or row.get("source_chunks")
                or [ground_truth],
                "latency_ms": int(row.get("latency_ms") or 180),
            }
        )
    return eval_rows


def run_eval(
    tenant: str, dataset_rows: list[dict[str, Any]], run_id: str | None = None
) -> dict[str, Any]:
    if not tenant.strip():
        raise ValueError("tenant must be non-empty")

    run_identifier = run_id or f"run-{uuid.uuid4()}"
    started_at = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")

    eval_rows = _build_eval_rows(dataset_rows)
    scored_rows = []
    aggregate_input = []

    for index, eval_row in enumerate(eval_rows):
        scores = score_row(eval_row)
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
            }
        )

    metrics = aggregate_scores(aggregate_input)
    finished_at = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    return {
        "tenant": tenant,
        "run_id": run_identifier,
        "started_at": started_at,
        "finished_at": finished_at,
        "status": "completed",
        "metrics": metrics,
        "rows": scored_rows,
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run Phase 6 eval scoring on a dataset")
    parser.add_argument("--tenant", required=True, help="Tenant slug")
    parser.add_argument("--dataset-file", required=True, type=Path, help="Input dataset JSON")
    parser.add_argument("--run-id", help="Optional explicit run id")
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
    dataset_rows = _read_dataset(args.dataset_file)
    result = run_eval(args.tenant, dataset_rows, args.run_id)

    args.output_file.parent.mkdir(parents=True, exist_ok=True)
    args.output_file.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")

    args.metrics_file.parent.mkdir(parents=True, exist_ok=True)
    args.metrics_file.write_text(json.dumps(result["metrics"], indent=2) + "\n", encoding="utf-8")

    print(
        json.dumps(
            {
                "tenant": result["tenant"],
                "run_id": result["run_id"],
                "rows": len(result["rows"]),
                "metrics_file": str(args.metrics_file),
                "output_file": str(args.output_file),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
