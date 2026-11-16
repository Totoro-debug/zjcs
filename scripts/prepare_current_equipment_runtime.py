from __future__ import annotations

import hashlib
import json
import os
import re
import sys
import tempfile
from pathlib import Path

from official_apk import ROOT
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props


MANIFEST = "884_156692"
SOURCE_ROOT = ROOT / ".tools/source-packages/current" / MANIFEST
CODE_ROOT = SOURCE_ROOT / "official-code"
TABLE_ROOT = SOURCE_ROOT / "official-data"
LEVEL_PROP_PATH = SOURCE_ROOT / "native-conversion/inputs/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile"
RUNTIME_PATH = Path("unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json")
RUNTIME_ASSET_PATH = "Assets/Official/Equipment/Runtime/equipment-runtime-rules.json"
RUNTIME_RECEIPT_PATH = Path("unity/Assets/Official/Equipment/Runtime/source-manifest.json")
REFERENCE_RECEIPT_PATH = Path("assets/reference/equipment-runtime-source-manifest.json")

RULE_TABLES = (
    "conditions", "equip", "equip_attributes", "equip_attributes_props", "equip_piece_merge",
    "equip_rank", "equip_type", "equip_upgrade_bless", "equip_upgrade_material", "function_limit",
    "game_settings", "gem", "item", "item_piece_merge", "level_prop_default_value",
    "profession_base", "prop_cfg",
)
EXTRA_TABLES = (
    "equip_attribute_bless_scale", "equip_attributes_passive_skill", "equip_drop_bless_level",
    "equip_inherit_cost", "equip_inlay_site", "equip_purify_cost", "equip_resonance", "equip_suit",
    "source_operate_group", "system_level_gap_limit", "system_level_limit",
)
TABLE_COUNTS = {
    "conditions": 4496,
    "equip": 1223,
    "equip_attributes": 381,
    "equip_attributes_props": 228,
    "equip_piece_merge": 73,
    "equip_rank": 8,
    "equip_type": 9,
    "equip_upgrade_bless": 1800,
    "equip_upgrade_material": 250,
    "function_limit": 422,
    "gem": 360,
    "item": 12725,
    "item_piece_merge": 3,
    "level_prop_default_value": 73,
    "profession_base": 31,
    "prop_cfg": 352,
    "equip_attribute_bless_scale": 1753,
    "equip_attributes_passive_skill": 153,
    "equip_drop_bless_level": 840,
    "equip_inherit_cost": 4,
    "equip_inlay_site": 25,
    "equip_purify_cost": 4,
    "equip_resonance": 240,
    "equip_suit": 64,
    "source_operate_group": 2,
    "system_level_gap_limit": 276,
    "system_level_limit": 1124,
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_input(path: Path, kind: str, **details: object) -> dict[str, object]:
    data = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(data), "kind": kind, **details}


def build() -> tuple[dict[str, object], dict[str, object]]:
    source_selection_path = ROOT / "official-source.json"
    selection = json.loads(source_selection_path.read_bytes())
    if selection.get("active") != MANIFEST:
        raise ValueError("Equipment runtime generation requires the selected current 884_156692 source.")
    profile = selection.get("profiles", {}).get(MANIFEST)
    if (not profile or not profile.get("manifests")
            or any(row.get("version") != MANIFEST for row in profile["manifests"])):
        raise ValueError("The selected current equipment resource manifests are incomplete or mixed.")

    tables_source = CODE_ROOT / "common-source/Common/Tables.cs"
    source_text = tables_source.read_text(encoding="utf-8-sig")
    table_sources: dict[str, list[str]] = {}
    for constructor, source in re.findall(r'new\s+(Tb\w+)\s*\(loader\("([^"]+)"\)\)', source_text):
        table_sources.setdefault(Path(source).name, []).append(constructor)

    constructors = Constructors(CODE_ROOT)
    all_tables = RULE_TABLES + EXTRA_TABLES
    decoded: dict[str, object] = {}
    inputs = [file_input(source_selection_path, "selection")]
    inputs.append(file_input(tables_source, "table-index"))
    metadata_path = CODE_ROOT / "global-metadata.dat"
    inputs.append(file_input(metadata_path, "decoder-metadata"))

    for table in all_tables:
        candidates = table_sources.get(table, [])
        if len(candidates) != 1:
            raise ValueError("Current table constructor is missing or ambiguous: " + table)
        constructor = candidates[0]
        raw_path = TABLE_ROOT / (table + ".g.bytes")
        if not raw_path.is_file():
            raise FileNotFoundError("Current equipment table payload is missing: " + str(raw_path))
        raw = raw_path.read_bytes()
        value = constructors.table(raw, constructor)
        if table == "game_settings":
            if not isinstance(value, dict) or len(value) < 500:
                raise ValueError("Current GameSettings table is incomplete.")
        elif not isinstance(value, list) or len(value) != TABLE_COUNTS[table]:
            raise ValueError("Current equipment table row count differs: " + table)
        decoded[table] = value
        inputs.append(file_input(raw_path, "luban-table", table=table, constructor=constructor,
            entries=len(value)))

    constructor_source_root = constructors.source_root
    for name in sorted(constructors.sources):
        path = constructor_source_root / (name + ".cs")
        if not path.is_file():
            path = constructor_source_root.parent / "Common.LubanGen" / (name + ".cs")
        if not path.is_file():
            raise FileNotFoundError("Current table decoder source is missing: " + name)
        inputs.append(file_input(path, "table-constructor", constructor=name))

    wanted = {
        int(value)
        for row in decoded["equip"]
        for key, value in row.items()
        if key.endswith("PropId") and isinstance(value, int) and value > 0
    }
    level_prop_raw = LEVEL_PROP_PATH.read_bytes()
    props = decode_level_props(level_prop_raw, wanted)
    inputs.append(file_input(LEVEL_PROP_PATH, "level-properties", property_classes=len(props)))

    for path in (
        ROOT / "scripts/official_apk.py",
        ROOT / "scripts/official_metadata.py",
        ROOT / "scripts/official_tables.py",
        ROOT / "scripts/prepare_unity_character_catalog.py",
        Path(__file__).resolve(),
    ):
        inputs.append(file_input(path, "converter"))

    inputs.sort(key=lambda row: str(row["path"]))
    if len({row["path"] for row in inputs}) != len(inputs):
        raise ValueError("Current equipment provenance contains duplicate source paths.")
    input_hashes = {str(row["path"]): row["sha256"] for row in inputs}
    payload = {
        "input_sha256": input_hashes,
        "manifest": MANIFEST,
        "props": props,
        "rules": {name: decoded[name] for name in RULE_TABLES},
        "tables": {name: decoded[name] for name in EXTRA_TABLES},
    }
    encoded_payload = json.dumps(payload, ensure_ascii=False, indent=2, allow_nan=False).encode("utf-8") + b"\n"
    receipt = {
        "manifest": MANIFEST,
        "inputs": inputs,
        "tables": list(EXTRA_TABLES),
        "table_rows": {name: len(decoded[name]) for name in all_tables if isinstance(decoded[name], list)},
        "output": {
            "path": RUNTIME_ASSET_PATH,
            "bytes": len(encoded_payload),
            "sha256": sha256(encoded_payload),
        },
    }
    return payload, receipt


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, name = tempfile.mkstemp(prefix=".equipment-runtime-", dir=path.parent)
    temporary = Path(name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(data)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def write_owned(path: Path, data: bytes, previous_sha256: str | None = None) -> None:
    if path.exists() and path.read_bytes() != data:
        if previous_sha256 is None or sha256(path.read_bytes()) != previous_sha256:
            raise ValueError("Refusing to overwrite changed equipment runtime content: " + str(path))
        atomic_write(path, data)
    elif not path.exists():
        atomic_write(path, data)


def main() -> None:
    payload, receipt = build()
    runtime_path = ROOT / RUNTIME_PATH
    runtime_receipt_path = ROOT / RUNTIME_RECEIPT_PATH
    reference_receipt_path = ROOT / REFERENCE_RECEIPT_PATH
    previous_runtime_receipt_data = runtime_receipt_path.read_bytes() if runtime_receipt_path.exists() else None
    previous_reference_receipt_data = reference_receipt_path.read_bytes() if reference_receipt_path.exists() else None
    previous_receipt = json.loads(previous_runtime_receipt_data) if previous_runtime_receipt_data else None
    if (previous_reference_receipt_data is not None and previous_runtime_receipt_data is not None
            and previous_reference_receipt_data != previous_runtime_receipt_data):
        raise ValueError("Equipment runtime source receipts differ; refusing to replace either one.")
    previous_output_sha = None
    if runtime_path.exists():
        if (not previous_receipt or previous_receipt.get("output", {}).get("path") != RUNTIME_ASSET_PATH
                or sha256(runtime_path.read_bytes()) != previous_receipt.get("output", {}).get("sha256")):
            raise ValueError("Existing equipment runtime output has no matching generated receipt.")
        previous_output_sha = previous_receipt["output"]["sha256"]

    encoded_payload = json.dumps(payload, ensure_ascii=False, indent=2, allow_nan=False).encode("utf-8") + b"\n"
    encoded_receipt = json.dumps(receipt, ensure_ascii=False, indent=2).encode("utf-8") + b"\n"
    write_owned(runtime_path, encoded_payload, previous_output_sha)
    write_owned(runtime_receipt_path, encoded_receipt,
        sha256(previous_runtime_receipt_data) if previous_runtime_receipt_data is not None else None)
    write_owned(reference_receipt_path, encoded_receipt,
        sha256(previous_reference_receipt_data) if previous_reference_receipt_data is not None else None)
    print("Current equipment runtime: " + str(RUNTIME_PATH))
    print("Manifest: " + MANIFEST + "; bytes: " + str(len(encoded_payload))
        + "; sha256: " + sha256(encoded_payload) + "; classes: " + str(len(payload["props"])))


if __name__ == "__main__":
    main()
