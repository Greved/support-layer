from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

QUESTION_TYPES = (
    "synthetic_simple",
    "synthetic_multihop",
    "synthetic_adversarial",
)


@dataclass(frozen=True)
class SourceDocument:
    doc_id: str
    title: str
    excerpt: str


def _default_documents(tenant: str) -> list[SourceDocument]:
    return [
        SourceDocument(
            doc_id=f"{tenant}-doc-1",
            title="Support Playbook",
            excerpt="Escalate unresolved cases after two failed remediation attempts.",
        ),
        SourceDocument(
            doc_id=f"{tenant}-doc-2",
            title="Billing FAQ",
            excerpt="Refunds are approved when the outage exceeds SLA and evidence is attached.",
        ),
        SourceDocument(
            doc_id=f"{tenant}-doc-3",
            title="Incident SOP",
            excerpt=(
                "For priority incidents, acknowledge in 15 minutes and update status every hour."
            ),
        ),
    ]


def _parse_source_document(row: Any) -> SourceDocument:
    if isinstance(row, str):
        value = row.strip()
        return SourceDocument(doc_id=value or "doc", title=value or "Document", excerpt=value or "")
    if isinstance(row, dict):
        return SourceDocument(
            doc_id=str(row.get("id") or row.get("doc_id") or row.get("title") or "doc"),
            title=str(row.get("title") or row.get("id") or "Document"),
            excerpt=str(row.get("excerpt") or row.get("content") or ""),
        )
    raise ValueError("Document rows must be strings or objects")


def load_documents(path: Path | None, tenant: str) -> list[SourceDocument]:
    if path is None:
        return _default_documents(tenant)

    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, list):
        raise ValueError(f"Documents file {path} must contain a JSON array")
    docs = [_parse_source_document(row) for row in raw]
    return docs or _default_documents(tenant)


def _build_question(doc: SourceDocument, question_type: str) -> tuple[str, str]:
    if question_type == "synthetic_simple":
        return (
            f"What does '{doc.title}' say about this issue?",
            doc.excerpt or f"Refer to {doc.title}.",
        )
    if question_type == "synthetic_multihop":
        return (
            f"How should an agent combine '{doc.title}' guidance with another policy?",
            f"Start with {doc.title} and cross-check related policy documents before finalizing.",
        )
    return (
        f"What should happen if '{doc.title}' does not fully answer an edge case?",
        f"Flag low confidence, cite {doc.title}, and escalate for manual review.",
    )


def generate_dataset(
    tenant: str,
    count: int,
    documents: list[SourceDocument],
    dataset_version: str | None = None,
) -> list[dict[str, Any]]:
    if count <= 0:
        raise ValueError("count must be greater than zero")
    if not tenant.strip():
        raise ValueError("tenant must be non-empty")
    if not documents:
        raise ValueError("at least one document is required")

    version = dataset_version or datetime.now(UTC).strftime("%Y%m%d%H%M%S")
    created_at = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    rows: list[dict[str, Any]] = []

    for index in range(count):
        question_type = QUESTION_TYPES[index % len(QUESTION_TYPES)]
        doc = documents[index % len(documents)]
        question, ground_truth = _build_question(doc, question_type)
        rows.append(
            {
                "tenant": tenant,
                "dataset_version": version,
                "created_at": created_at,
                "question_type": question_type,
                "question": question,
                "ground_truth": ground_truth,
                "source_chunk_ids": [f"doc:{doc.doc_id}"],
            }
        )
    return rows


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate synthetic eval dataset rows")
    parser.add_argument("--tenant", required=True, help="Tenant slug")
    parser.add_argument("--count", type=int, default=50, help="Number of rows to generate")
    parser.add_argument(
        "--documents-file",
        type=Path,
        help="Optional JSON array with source documents",
    )
    parser.add_argument(
        "--output-file",
        type=Path,
        default=Path("artifacts/eval/generated-dataset.json"),
        help="Output JSON file path",
    )
    parser.add_argument("--dataset-version", help="Optional explicit dataset version")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    documents = load_documents(args.documents_file, args.tenant)
    rows = generate_dataset(args.tenant, args.count, documents, args.dataset_version)

    args.output_file.parent.mkdir(parents=True, exist_ok=True)
    args.output_file.write_text(json.dumps({"rows": rows}, indent=2) + "\n", encoding="utf-8")
    print(
        json.dumps(
            {
                "tenant": args.tenant,
                "dataset_version": rows[0]["dataset_version"],
                "rows": len(rows),
                "output_file": str(args.output_file),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
