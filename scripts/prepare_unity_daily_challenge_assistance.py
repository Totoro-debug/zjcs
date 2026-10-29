"""Export the verified current official tables needed by daily challenge NPC assistance."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource


TABLES = (
    "conditions",
    "npc",
    "npc_level_detail",
    "npc_profession_detail",
    "player_robot_invite",
    "skill",
)
OUTPUT = ROOT / "unity/Assets/Game/Resources/DailyChallenges/npc-assistance-rules.json"
RUNTIME_RECEIPT = ROOT / "unity/Assets/Game/Resources/DailyChallenges/npc-assistance-rules-source-manifest.json"
RECEIPT = ROOT / "assets/reference/daily-challenges/npc-assistance-source-manifest.json"
TABLE_RECEIPT = ROOT / "assets/reference/official-map/current-table-source-manifest.json"


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def main() -> None:
    version = OfficialSource().version
    table_receipt_raw = TABLE_RECEIPT.read_bytes()
    table_receipt = json.loads(table_receipt_raw)
    if table_receipt.get("manifest") != version:
        raise ValueError("Current official table receipt does not match the active source version")
    table_cache = ROOT / ".tools/source-packages/current" / version / "official-data/tables.json"
    table_cache_raw = table_cache.read_bytes()
    proof_path = table_cache.parent.parent / "table-proof-source-manifest.json"
    proof_raw = proof_path.read_bytes()
    proof = json.loads(proof_raw)
    if proof.get("manifest") != version or proof.get("table_sha256") != sha(table_cache_raw):
        raise ValueError("Current decoded tables do not match their constructor proof")
    tables = json.loads(table_cache_raw.decode("utf-8-sig"))
    source_records = {row["table"]: row for row in table_receipt.get("tables", [])}
    proof_records = {row["table"]: row for row in proof.get("tables", [])}
    if any(name not in tables or name not in source_records for name in TABLES):
        raise ValueError("A required official NPC assistance table is missing")
    if any(name not in proof_records or source_records[name].get("sha256") != proof_records[name].get("sha256")
           or source_records[name].get("constructor_sha256") != proof_records[name].get("constructor_sha256")
           for name in TABLES):
        raise ValueError("An NPC assistance table is not covered by the active constructor proof")
    payload = {"manifest": version, **{name: tables[name] for name in TABLES}}
    encoded = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encoded)
    runtime_manifest = {"manifest": version, "output_sha256": sha(encoded)}
    runtime_manifest_raw = (json.dumps(runtime_manifest, separators=(",", ":")) + "\n").encode("utf-8")
    RUNTIME_RECEIPT.write_bytes(runtime_manifest_raw)
    outputs = {}
    for path, content in ((OUTPUT, encoded), (RUNTIME_RECEIPT, runtime_manifest_raw)):
        guid = hashlib.sha256(path.relative_to(ROOT).as_posix().encode("utf-8")).hexdigest()[:32]
        meta = path.with_name(path.name + ".meta")
        meta.write_text("fileFormatVersion: 2\nguid: " + guid + "\n", encoding="utf-8")
        outputs[path.relative_to(ROOT).as_posix()] = {"bytes": len(content), "sha256": sha(content)}
        outputs[meta.relative_to(ROOT).as_posix()] = {"sha256": sha(meta.read_bytes())}
    receipt = {
        "manifest": version,
        "inputs": {
            table_cache.relative_to(ROOT).as_posix(): {
                "sha256": sha(table_cache_raw),
                "tables": {name: source_records[name] for name in TABLES},
            },
            proof_path.relative_to(ROOT).as_posix(): {"sha256": sha(proof_raw)},
            TABLE_RECEIPT.relative_to(ROOT).as_posix(): {"sha256": sha(table_receipt_raw)},
        },
        "outputs": outputs,
        "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(),
                      "sha256": sha(Path(__file__).read_bytes())},
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Exported current official NPC assistance tables:", ", ".join(TABLES))


if __name__ == "__main__":
    main()
