"""Add the pinned official action mutex table to the shared battle rules payload."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from official_tables import Constructors  # noqa: E402

VERSION = "854_156383"
PAYLOAD = Path("assets/official/battle-rules.json")
MANIFEST = Path("assets/reference/battle-rules/source-manifest.json")
TABLE = "state_mutex"
SOURCE_CODE = (
    "StateMutex",
    "StateMutexParser",
    "TbStateMutex",
    "ActionBehaviour",
    "ActionType",
    "ActionAgentComponentImplement",
    "BattleAISetting",
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def decode_state_mutex(source_root: Path) -> tuple[bytes, str, list[dict]]:
    data_root = source_root / "official-data"
    code_root = source_root / "official-code/common-source/Common"
    tables_source = (code_root / "Tables.cs").read_text(encoding="utf-8-sig")
    mapping = {
        match[2]: match[1]
        for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', tables_source)
    }
    table_type = mapping.get(TABLE)
    if table_type != "TbStateMutex":
        raise ValueError("Pinned Tables.cs does not map state_mutex to TbStateMutex")
    raw = (data_root / (TABLE + ".g.bytes")).read_bytes()
    rows = Constructors(source_root / "official-code").table(raw, table_type)
    if not isinstance(rows, list) or len(rows) != 48:
        raise ValueError("Pinned state_mutex table did not decode to its expected 48 rows")
    for row in rows:
        if not isinstance(row, dict) or not isinstance(row.get("From"), int):
            raise ValueError("Pinned state_mutex row is malformed")
        behaviours = row.get("Behaviours")
        if not isinstance(behaviours, dict) or any(
            not isinstance(key, int) or value not in (0, 1, 2)
            for key, value in behaviours.items()
        ):
            raise ValueError("Pinned state_mutex behaviour map is malformed")
    return raw, table_type, rows


def build(source_root: Path = ROOT / ".tools/source-packages") -> tuple[bytes, bytes, dict]:
    payload_path = ROOT / PAYLOAD
    manifest_path = ROOT / MANIFEST
    payload_bytes = payload_path.read_bytes()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("manifest") != VERSION:
        raise ValueError("Battle rules manifest is not pinned to " + VERSION)

    raw, table_type, rows = decode_state_mutex(source_root)
    payload = json.loads(payload_bytes)
    tables = payload.get("tables")
    if not isinstance(tables, dict):
        raise ValueError("Battle rules payload has no table object")
    baseline_payload_path = ROOT / "unity/Assets/Official/Combat/battle-rules.json"
    baseline_sidecar_path = ROOT / "unity/Assets/Official/Combat/battle-rules-source-manifest.json"
    module_manifest_path = ROOT / "unity/Assets/Official/Combat/source-manifest.json"
    baseline_payload = json.loads(baseline_payload_path.read_bytes())
    baseline_sidecar = json.loads(baseline_sidecar_path.read_text(encoding="utf-8"))
    module_manifest = json.loads(module_manifest_path.read_text(encoding="utf-8"))
    entries = {entry.get("path"): entry for entry in module_manifest.get("assets", [])}
    battle_entry = entries.get("Assets/Official/Combat/battle-rules.json")
    if (module_manifest.get("manifest") != VERSION or battle_entry is None
            or battle_entry.get("source_manifest") != str(MANIFEST).replace("\\", "/")
            or battle_entry.get("output_sha256") != baseline_sidecar.get("output_sha256")
            or baseline_sidecar.get("manifest") != VERSION):
        raise ValueError("Unity combat baseline does not identify the pinned battle rules")

    baseline_source = copy.deepcopy(manifest)
    baseline_source["tables"].pop(TABLE, None)
    for name in SOURCE_CODE:
        source_path = source_root / "official-code/common-source/Common" / (name + ".cs")
        baseline_source["original_code"].pop(str(source_path.relative_to(ROOT)), None)
    baseline_source["output_sha256"] = baseline_sidecar.get("output_sha256")
    if baseline_source != baseline_sidecar:
        raise ValueError("Unity battle-rules source sidecar is not the verified pre-mutex manifest")

    baseline_current = copy.deepcopy(payload)
    baseline_current.get("tables", {}).pop(TABLE, None)
    if baseline_current != baseline_payload:
        raise ValueError("Battle rules changed beyond the pinned state_mutex addition")
    existing = tables.get(TABLE)
    if existing is not None and json.dumps(existing, ensure_ascii=False, separators=(",", ":")) \
            != json.dumps(rows, ensure_ascii=False, separators=(",", ":")):
        raise ValueError("Existing state_mutex table differs from the pinned table")

    table_record = manifest.get("tables", {}).get(TABLE, {})
    if table_record and (table_record.get("sha256") != sha256(raw)
            or table_record.get("constructor") != table_type
            or table_record.get("bytes") != len(raw)):
        raise ValueError("Battle rules manifest state_mutex provenance differs from the pinned source")
    tables[TABLE] = rows

    manifest.setdefault("tables", {})[TABLE] = {
        "sha256": sha256(raw),
        "constructor": table_type,
        "bytes": len(raw),
        "source": ".tools/source-packages/official-data/state_mutex.g.bytes",
    }
    original_code = manifest.setdefault("original_code", {})
    for name in SOURCE_CODE:
        path = source_root / "official-code/common-source/Common" / (name + ".cs")
        original_code[str(path.relative_to(ROOT))] = sha256(path.read_bytes())

    result_bytes = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    manifest["output_sha256"] = sha256(result_bytes)
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return result_bytes, manifest_bytes, {
        "rows": len(rows),
        "input_sha256": sha256(raw),
        "output_sha256": manifest["output_sha256"],
    }


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-action-rules-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as output:
            output.write(data)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="rebuild and compare without writing")
    parser.add_argument("--source-root", type=Path, default=ROOT / ".tools/source-packages")
    args = parser.parse_args()
    payload, manifest, record = build(args.source_root)
    payload_path = ROOT / PAYLOAD
    manifest_path = ROOT / MANIFEST
    if args.verify:
        if payload_path.read_bytes() != payload or manifest_path.read_bytes() != manifest:
            raise SystemExit("battle action rules differ from the pinned source table")
    else:
        atomic_write(payload_path, payload)
        atomic_write(manifest_path, manifest)
    print(json.dumps(record, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
