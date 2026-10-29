"""Package the verified Map13 battle closure for the shared offline host."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/map13-battles.json"
SOURCE_RECEIPT = ROOT / "unity/Assets/Official/World/Map/13/battle-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map13/battle-runtime.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-battle-runtime-source-manifest.json"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value) -> None:
    raw = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == raw:
        return
    path.write_bytes(raw)


def write_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    meta = path.with_name(path.name + ".meta")
    raw = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
           "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    if meta.exists() and meta.read_bytes() != raw:
        raise ValueError("Refusing to replace unrelated Unity metadata: " + str(meta))
    if not meta.exists():
        meta.write_bytes(raw)


def build() -> dict:
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    source_receipt = json.loads(SOURCE_RECEIPT.read_text(encoding="utf-8"))
    if source.get("manifest") != "854_156383" or source.get("mapClassId") != 13:
        raise ValueError("Map13 battle source is not the pinned official closure")
    if source_receipt.get("manifest") != "854_156383" or source_receipt.get("mapClassId") != 13:
        raise ValueError("Map13 battle source receipt is not pinned")
    if source_receipt.get("output", {}).get("sha256") != sha(SOURCE):
        raise ValueError("Map13 battle source differs from its receipt")
    stages = source.get("stages")
    rosters = source.get("monsterRosters")
    scenes = source.get("battleScenes")
    arrays = source.get("monsterArrays")
    if not isinstance(stages, list) or len(stages) != 158:
        raise ValueError("Map13 battle stage closure must contain 158 official stages")
    if not isinstance(rosters, list) or len(rosters) != len(stages):
        raise ValueError("Map13 battle roster closure differs from stages")
    if not isinstance(scenes, dict) or not scenes or not isinstance(arrays, dict) or not arrays:
        raise ValueError("Map13 battle scenes or monster arrays are missing")
    stage_ids = {int(value["Id"]) for value in stages}
    roster_ids = {int(value["StageId"]) for value in rosters}
    if stage_ids != roster_ids:
        raise ValueError("Map13 battle stages and rosters are not one-to-one")
    payload = {
        "manifest": source["manifest"],
        "mapClassId": 13,
        "sourceSha256": sha(SOURCE),
        "stages": stages,
        "monsterRosters": rosters,
        "battleScenes": [dict(GameId=int(key), **value)
                         for key, value in sorted(scenes.items(), key=lambda item: int(item[0]))],
    }
    write_json(OUTPUT, payload)
    write_meta(OUTPUT)
    receipt = {
        "format": 1,
        "manifest": source["manifest"],
        "mapClassId": 13,
        "operation": "Package the verified Map13 official stage, roster, and battle-scene closure for the shared offline host; no battle values are synthesized.",
        "inputs": [
            {"path": SOURCE.relative_to(ROOT).as_posix(), "sha256": sha(SOURCE)},
            {"path": SOURCE_RECEIPT.relative_to(ROOT).as_posix(), "sha256": sha(SOURCE_RECEIPT)},
            {"path": Path(__file__).relative_to(ROOT).as_posix(), "sha256": sha(Path(__file__))},
        ],
        "outputs": [
            {"path": OUTPUT.relative_to(ROOT).as_posix(), "sha256": sha(OUTPUT)},
            {"path": (OUTPUT.with_name(OUTPUT.name + ".meta")).relative_to(ROOT).as_posix(),
             "sha256": sha(OUTPUT.with_name(OUTPUT.name + ".meta"))},
        ],
        "counts": {"stages": len(stages), "rosters": len(rosters),
                   "battleScenes": len(scenes), "monsterArrays": len(arrays)},
    }
    write_json(RECEIPT, receipt)
    print(json.dumps(receipt["counts"], ensure_ascii=False, sort_keys=True))
    return receipt


if __name__ == "__main__":
    build()
