from __future__ import annotations

import json
from pathlib import Path

from eval.generate_dataset import generate_dataset, load_documents


def test_generate_dataset_creates_requested_count() -> None:
    docs = load_documents(path=None, tenant="tenant-a")
    rows = generate_dataset("tenant-a", 7, docs, dataset_version="v1")
    assert len(rows) == 7
    assert {row["question_type"] for row in rows} == {
        "synthetic_simple",
        "synthetic_multihop",
        "synthetic_adversarial",
    }
    assert all(row["tenant"] == "tenant-a" for row in rows)
    assert all(row["dataset_version"] == "v1" for row in rows)


def test_load_documents_from_json_array(tmp_path: Path) -> None:
    payload = [
        {"id": "doc-1", "title": "Guide", "excerpt": "Step-by-step guidance"},
        "Fallback doc text",
    ]
    file_path = tmp_path / "docs.json"
    file_path.write_text(json.dumps(payload), encoding="utf-8")

    docs = load_documents(file_path, tenant="tenant-b")
    assert len(docs) == 2
    assert docs[0].doc_id == "doc-1"
    assert docs[1].title == "Fallback doc text"
