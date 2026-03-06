from __future__ import annotations

import argparse
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


def _read_registry(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {"tenants": {}}
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError(f"Invalid baseline registry format in {path}")
    raw.setdefault("tenants", {})
    if not isinstance(raw["tenants"], dict):
        raise ValueError(f"Invalid baseline registry format in {path}: 'tenants' must be object")
    return raw


def set_baseline(
    tenant: str,
    run_id: str,
    registry_file: Path,
    triggered_by: str = "manual",
) -> dict[str, Any]:
    if not tenant.strip():
        raise ValueError("tenant must be non-empty")
    if not run_id.strip():
        raise ValueError("run_id must be non-empty")

    registry = _read_registry(registry_file)
    now = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    tenants = registry["tenants"]
    tenants[tenant] = {
        "run_id": run_id,
        "set_at": now,
        "triggered_by": triggered_by,
    }

    registry_file.parent.mkdir(parents=True, exist_ok=True)
    registry_file.write_text(json.dumps(registry, indent=2) + "\n", encoding="utf-8")
    return tenants[tenant]


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Pin eval baseline run for a tenant")
    parser.add_argument("--tenant", required=True, help="Tenant slug")
    parser.add_argument("--run-id", required=True, help="Eval run id to pin as baseline")
    parser.add_argument(
        "--registry-file",
        type=Path,
        default=Path("artifacts/eval/baselines.json"),
        help="Baseline registry JSON path",
    )
    parser.add_argument(
        "--triggered-by",
        default="manual",
        help="Who triggered baseline pinning (manual/ci/system)",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    row = set_baseline(
        tenant=args.tenant,
        run_id=args.run_id,
        registry_file=args.registry_file,
        triggered_by=args.triggered_by,
    )
    print(json.dumps({"tenant": args.tenant, **row}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
