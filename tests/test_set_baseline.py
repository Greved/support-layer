from __future__ import annotations

import json
from pathlib import Path

from eval.set_baseline import main, set_baseline


def test_set_baseline_creates_registry(tmp_path: Path) -> None:
    reg = tmp_path / "baselines.json"
    row = set_baseline("tenant-a", "run-1", reg, triggered_by="ci")

    assert row["run_id"] == "run-1"
    assert row["triggered_by"] == "ci"
    assert "set_at" in row

    payload = json.loads(reg.read_text(encoding="utf-8"))
    assert payload["tenants"]["tenant-a"]["run_id"] == "run-1"


def test_set_baseline_overwrites_existing_tenant(tmp_path: Path) -> None:
    reg = tmp_path / "baselines.json"
    set_baseline("tenant-a", "run-1", reg)
    set_baseline("tenant-a", "run-2", reg)

    payload = json.loads(reg.read_text(encoding="utf-8"))
    assert payload["tenants"]["tenant-a"]["run_id"] == "run-2"


def test_cli_main_writes_default_shape(tmp_path: Path) -> None:
    reg = tmp_path / "x" / "baselines.json"
    rc = main(
        [
            "--tenant",
            "tenant-b",
            "--run-id",
            "run-55",
            "--registry-file",
            str(reg),
        ]
    )
    assert rc == 0
    payload = json.loads(reg.read_text(encoding="utf-8"))
    assert payload["tenants"]["tenant-b"]["run_id"] == "run-55"
