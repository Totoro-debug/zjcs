from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path


WORKSPACE = Path(__file__).resolve().parents[1]
sys.dont_write_bytecode = True
sys.path.insert(0, str(WORKSPACE / "scripts"))

from fetch_official_assets import APK, SHA256 as APK_SHA256  # noqa: E402
from official_apk import Apk  # noqa: E402
from official_tables import Constructors  # noqa: E402


VERSION = "854_156383"
STAGE_TABLE = "stage_battle_stele"
SETTINGS_TABLE = "game_settings"
STAGE_SHA256 = "34aa50255e338873f47935e76713eaf22b8e2a5d11fc0683befaed739858b61d"
SETTINGS_SHA256 = "8832aa02061ada1c2dad9cc190fbe6fe54a25be1e30596513b9f8c1a1ef22b27"
BUNDLE_NAME = "defaultpackage_assets_config_luban.bundle"
BUNDLE_MD5 = "e0b314ac00f9a6425dac35792b472695"
STAGE_PATH_ID = "5102655057011860899"
SETTINGS_PATH_ID = "-3869022733471997335"
TABLE_PATTERN = re.compile(r'new (Tb\w+)\(loader\("([^"]+)"\)\)')
DECODER_FILES = [
    "scripts/fetch_official_assets.py",
    "scripts/official_apk.py",
    "scripts/official_cloud_cache.py",
    "scripts/official_manifest.py",
    "scripts/official_metadata.py",
    "scripts/official_tables.py",
]


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_record(path: Path, base: Path) -> dict:
    raw = path.read_bytes()
    return {"path": path.relative_to(base).as_posix(), "bytes": len(raw), "sha256": sha256(raw)}


def read_text_asset(apk: Apk, name: str, expected_path_id: str) -> tuple[bytes, dict]:
    obj = apk.asset("config_luban", f"/{name}.g.bytes")
    asset = obj.read()
    if asset.m_Name != f"{name}.g":
        raise ValueError(f"Unexpected Luban TextAsset name: {asset.m_Name}")
    if str(obj.path_id) != expected_path_id:
        raise ValueError(f"Unexpected object path ID for {name}: {obj.path_id}")
    raw = asset.m_Script.encode("utf8", "surrogateescape")
    provenance = apk.provenance(obj)
    if provenance["bundle"] != BUNDLE_NAME or provenance["bundle_hash"] != BUNDLE_MD5:
        raise ValueError(f"Unexpected original bundle for {name}: {provenance}")
    return raw, provenance


def verify_bundle(apk: Apk) -> dict:
    record = apk.bundle_records[BUNDLE_NAME]
    if record["hash"] != BUNDLE_MD5:
        raise ValueError("Pinned config_luban bundle MD5 changed")
    apk_entry = f"assets/yoo/DefaultPackage/{record['file']}"
    raw = apk.archive.read(apk_entry)
    if len(raw) != int(record["size"]) or hashlib.md5(raw).hexdigest() != BUNDLE_MD5:
        raise ValueError("config_luban bundle differs from the pinned APK manifest")
    return {
        "name": BUNDLE_NAME,
        "apk_entry": apk_entry,
        "bytes": len(raw),
        "md5": BUNDLE_MD5,
        "sha256": sha256(raw),
    }


def write_package_manifest(output_dir: Path, entrypoint: Path) -> None:
    files = [
        output_dir / "data/battle-stele.json",
        output_dir / "assets/reference/official-map/battle-stele-source-manifest.json",
    ]
    readme = output_dir / "README.md"
    if readme.is_file():
        files.append(readme)
    records = sorted((file_record(path, output_dir) for path in files), key=lambda item: item["path"])
    canonical = json.dumps(records, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("utf-8")
    manifest = {
        "schema": 1,
        "status": "official_apk_luban_conversion_verified",
        "version": VERSION,
        "package_sha256": sha256(canonical),
        "package_sha256_definition": "SHA256 of UTF-8 compact sorted-key JSON for the files array; the manifest itself is excluded",
        "entrypoint": file_record(entrypoint, WORKSPACE),
        "files": records,
    }
    (output_dir / "SUCCESSOR_MANIFEST.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def build(output_dir: Path, make_package_manifest: bool = False) -> tuple[dict, dict]:
    apk = Apk()
    constructors = Constructors()
    tables_source = constructors.source("Tables")
    mapping = {match[2]: match[1] for match in TABLE_PATTERN.finditer(tables_source)}
    if STAGE_TABLE not in mapping or SETTINGS_TABLE not in mapping:
        raise ValueError("Pinned Tables.cs does not map the required Luban tables")

    stage_raw, stage_source = read_text_asset(apk, STAGE_TABLE, STAGE_PATH_ID)
    if sha256(stage_raw) != STAGE_SHA256:
        raise ValueError(f"Original BattleStele table SHA256 mismatch: {sha256(stage_raw)}")
    rows = constructors.table(stage_raw, mapping[STAGE_TABLE])
    if not isinstance(rows, list) or len(rows) != 65:
        raise ValueError(f"Expected 65 original BattleStele rows; got {len(rows) if hasattr(rows, '__len__') else type(rows).__name__}")
    if len({int(row["Id"]) for row in rows}) != 65:
        raise ValueError("BattleStele rows contain missing or duplicate stage IDs")
    if any(len(row.get("CombatRatingLevelValues", [])) != 4 for row in rows):
        raise ValueError("BattleStele rows must contain four original rating values")

    settings_raw, settings_source = read_text_asset(apk, SETTINGS_TABLE, SETTINGS_PATH_ID)
    if sha256(settings_raw) != SETTINGS_SHA256:
        raise ValueError("Original game_settings table SHA256 mismatch")
    settings = constructors.table(settings_raw, mapping[SETTINGS_TABLE])
    recommend_index = settings.get("RecommendCombatRatingIndex")
    if recommend_index != 2:
        raise ValueError(f"Pinned 854_156383 recommendation index changed: {recommend_index!r}")

    payload = {
        "version": VERSION,
        "source_sha256": sha256(stage_raw),
        "recommend_index": recommend_index,
        "rows": rows,
    }
    data_path = output_dir / "data/battle-stele.json"
    data_bytes = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    data_path.parent.mkdir(parents=True, exist_ok=True)
    data_path.write_bytes(data_bytes)

    bundle = verify_bundle(apk)
    code_root = WORKSPACE / ".tools/source-packages/official-code"
    constructor_paths = [
        code_root / "global-metadata.dat",
        code_root / "common-source/Common/Tables.cs",
        code_root / "common-source/Common/TbStagePartInfoBattleStele.cs",
        code_root / "common-source/Common/TbGameSettings.cs",
    ]
    decoder_sources = [file_record(WORKSPACE / path, WORKSPACE) for path in DECODER_FILES]
    code_manifest_path = WORKSPACE / "assets/reference/official-city/code-manifest.json"
    code_manifest = file_record(code_manifest_path, WORKSPACE) if code_manifest_path.is_file() else None
    source = {
        "schema": 1,
        "status": "official_apk_luban_conversion_verified",
        "version": VERSION,
        "apk": {"path": APK.relative_to(WORKSPACE).as_posix(), "sha256": APK_SHA256},
        "bundle": bundle,
        "tables": {
            STAGE_TABLE: {
                "constructor": mapping[STAGE_TABLE],
                "source": stage_source,
                "raw_bytes": len(stage_raw),
                "raw_sha256": sha256(stage_raw),
                "rows": len(rows),
            },
            SETTINGS_TABLE: {
                "constructor": mapping[SETTINGS_TABLE],
                "source": settings_source,
                "raw_bytes": len(settings_raw),
                "raw_sha256": sha256(settings_raw),
                "recommend_index_key": "RecommendCombatRatingIndex",
                "recommend_index": recommend_index,
            },
        },
        "decoder_sources": decoder_sources,
        "official_constructor_sources": [file_record(path, WORKSPACE) for path in constructor_paths],
        "official_code_generation_manifest": code_manifest,
        "output": {
            "data_path": "data/battle-stele.json",
            "provenance_path": "assets/reference/official-map/battle-stele-source-manifest.json",
            "bytes": len(data_bytes),
            "sha256": sha256(data_bytes),
        },
    }
    source_path = output_dir / "assets/reference/official-map/battle-stele-source-manifest.json"
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    if make_package_manifest:
        write_package_manifest(output_dir, Path(__file__).resolve())
    return payload, source


def main() -> None:
    parser = argparse.ArgumentParser(description="Convert the pinned original BattleStele Luban tables offline.")
    parser.add_argument("--output-dir", type=Path, default=WORKSPACE, help="Root for the two generated artifacts")
    parser.add_argument(
        "--write-package-manifest",
        action="store_true",
        help="Also write SUCCESSOR_MANIFEST.json in the output directory",
    )
    args = parser.parse_args()
    cab_index = WORKSPACE / ".tools/source-packages/cab-index.json"
    if not cab_index.is_file():
        raise FileNotFoundError(
            "Verified local CAB index is required to avoid creating cache files: " + str(cab_index)
        )
    output_dir = args.output_dir.resolve()
    payload, source = build(output_dir, args.write_package_manifest)
    print(
        f"Generated {output_dir / 'data/battle-stele.json'} and "
        f"{output_dir / 'assets/reference/official-map/battle-stele-source-manifest.json'}: "
        f"{len(payload['rows'])} rows, recommend_index={payload['recommend_index']}, "
        f"raw_sha256={source['tables'][STAGE_TABLE]['raw_sha256']}"
    )


if __name__ == "__main__":
    main()
