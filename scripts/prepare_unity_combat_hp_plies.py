"""Restore the fixed battle catalog's official monster HP plies table."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors


VERSION = "854_156383"
TABLE_NAME = "monster_hp_plies"
CONSTRUCTOR = "TbMonsterHpPliesInfo"
OBJECT_PATH = "Assets/Config/Luban/monster_hp_plies.g.bytes"
OUTPUT = Path("Assets/Official/Combat/monster-hp-plies.json")
OUTPUT_MANIFEST = Path("Assets/Official/Combat/monster-hp-plies-source-manifest.json")
REFERENCE_MANIFEST = Path("assets/reference/official-map/monster-hp-plies-source-manifest.json")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def asset_meta(path: Path, unity_root: Path) -> bytes:
    relative = path.relative_to(unity_root).as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    return (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def write_verified(path: Path, content: bytes) -> None:
    if path.exists():
        if path.read_bytes() == content:
            return
        raise ValueError("Refusing to overwrite changed official HP plies output: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def generate(apk: Apk | None = None, unity_root: Path = ROOT / "unity") -> dict:
    apk = apk or Apk()
    if apk.version != VERSION:
        raise ValueError("HP plies must come from the pinned original battle manifest " + VERSION)
    asset = apk.asset("config_luban", "monster_hp_plies.g.bytes")
    source_bytes = asset.read().m_Script.encode("utf-8", "surrogateescape")
    source_digest = sha256(source_bytes)
    rows = Constructors().table(source_bytes, CONSTRUCTOR)
    normalized_rows = [
        {"Id": int(row["Id"]), "LowLevel": int(row["LowLevel"]), "Plies": int(row["Plies"])}
        for row in rows
    ]
    if not normalized_rows or any(row["Id"] <= 0 or row["LowLevel"] <= 0 or row["Plies"] <= 0
                                  for row in normalized_rows):
        raise ValueError("Official monster HP plies table contains invalid rows")

    output_bytes = json_bytes({"manifest": VERSION, "rows": normalized_rows})
    output_digest = sha256(output_bytes)
    reference = {
        "format": 1,
        "manifest": VERSION,
        "source": {
            "object": OBJECT_PATH,
            "constructor": CONSTRUCTOR,
            "table": TABLE_NAME,
            "provenance": apk.provenance(asset),
            "payload_bytes": len(source_bytes),
            "payload_sha256": source_digest,
        },
        "output": {
            "path": OUTPUT.as_posix(),
            "rows": len(normalized_rows),
            "sha256": output_digest,
        },
    }
    reference_bytes = json_bytes(reference)
    unity_manifest_bytes = json_bytes({
        "manifest": VERSION,
        "output_sha256": output_digest,
        "source_manifest": REFERENCE_MANIFEST.as_posix(),
        "source_manifest_sha256": sha256(reference_bytes),
    })

    source_root = ROOT
    unity_root = unity_root.resolve()
    output_path = unity_root / OUTPUT
    output_manifest_path = unity_root / OUTPUT_MANIFEST
    output_meta_path = Path(str(output_path) + ".meta")
    manifest_meta_path = Path(str(output_manifest_path) + ".meta")
    reference_path = source_root / REFERENCE_MANIFEST

    write_verified(output_path, output_bytes)
    write_verified(output_manifest_path, unity_manifest_bytes)
    write_verified(output_meta_path, asset_meta(output_path, unity_root))
    write_verified(manifest_meta_path, asset_meta(output_manifest_path, unity_root))
    write_verified(reference_path, reference_bytes)
    return {"rows": len(normalized_rows), "output": output_path.as_posix(),
            "output_sha256": output_digest, "source_sha256": source_digest}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-root", type=Path, default=ROOT / "unity",
                        help="Unity project receiving the pinned official HP plies table")
    args = parser.parse_args()
    print(json.dumps(generate(unity_root=args.unity_root), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
