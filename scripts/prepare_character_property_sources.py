"""Extract the pinned character property tables carried in official Unity bundles."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import tempfile
import zipfile

from official_apk import ROOT
from official_tables import Constructors

APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
APK_PATH = ROOT / ".tools/source-packages/zjcs_M509085.apk"
OUTPUT = ROOT / "assets/official/character-property-sources.json"
MANIFEST = ROOT / "assets/reference/character-property-sources/source-manifest.json"
TABLES = (
    ("defaultpackage_assets_config_luban_map.bundle", "assets/yoo/DefaultPackage/71fe29d82df52227b7bfa2a6e52afe14.bundle",
     "71fe29d82df52227b7bfa2a6e52afe14", "map_region.g", "TbMapRegionInfo", "map_region"),
    ("defaultpackage_assets_config_luban_npctalenttree.bundle", "assets/yoo/DefaultPackage/e63847e752d716216b75f4d5715ed964.bundle",
     "e63847e752d716216b75f4d5715ed964", "npc_talent_tree_npc_map_prop.g",
     "TbNpcTalentTreeMapPropInfo", "npc_talent_tree_prop"),
    ("defaultpackage_assets_config_luban_npctalenttree.bundle", "assets/yoo/DefaultPackage/e63847e752d716216b75f4d5715ed964.bundle",
     "e63847e752d716216b75f4d5715ed964", "npc_talent_tree_npc_map.g",
     "TbNpcTalentTreeNpcMapInfo", "npc_talent_tree_map"),
    ("defaultpackage_assets_config_luban_npctalenttree.bundle", "assets/yoo/DefaultPackage/e63847e752d716216b75f4d5715ed964.bundle",
     "e63847e752d716216b75f4d5715ed964", "npc_talent_item.g",
     "TbNpcTalentItemInfo", "npc_talent_item"),
)
_APK_HASH_VERIFIED = False


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def md5(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def write_atomic(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=".character-props-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def payload_bytes(value: str | bytes) -> bytes:
    if isinstance(value, bytes):
        return value
    chunks = []
    for character in value:
        codepoint = ord(character)
        if 0xDC80 <= codepoint <= 0xDCFF:
            chunks.append(bytes((codepoint - 0xDC00,)))
        else:
            chunks.append(character.encode("utf-8"))
    return b"".join(chunks)


def pinned_bundle(bundle_name: str, archive_path: str, expected_md5: str) -> tuple[bytes, str]:
    global _APK_HASH_VERIFIED
    if not APK_PATH.is_file():
        raise FileNotFoundError("Pinned 854_156383 APK is missing: " + str(APK_PATH))
    manifest = json.loads((ROOT / "assets/reference/male-remodel/source-manifest.json")
                          .read_text(encoding="utf-8"))
    if manifest.get("manifest") != "854_156383" or manifest.get("sha256") != APK_SHA256:
        raise ValueError("Pinned APK manifest does not match 854_156383.")
    if not _APK_HASH_VERIFIED:
        digest = hashlib.sha256()
        with APK_PATH.open("rb") as stream:
            while block := stream.read(16 * 1024 * 1024):
                digest.update(block)
        if digest.hexdigest() != APK_SHA256:
            raise ValueError("Pinned APK SHA-256 differs from its 854_156383 manifest.")
        _APK_HASH_VERIFIED = True
    with zipfile.ZipFile(APK_PATH) as archive:
        raw = archive.read(archive_path)
    if md5(raw) != expected_md5:
        raise ValueError("Bundle bytes differ from the pinned 854_156383 APK entry: " + bundle_name)
    return raw, sha256(raw)


def decode_bundle_table(bundle_name: str, archive_path: str, expected_md5: str,
                        asset_name: str, constructor: str):
    import UnityPy

    raw, bundle_sha256 = pinned_bundle(bundle_name, archive_path, expected_md5)
    decoded = bytes(value ^ 0x40 for value in raw) if raw[:7] != b"UnityFS" else raw
    if decoded[:7] != b"UnityFS":
        raise ValueError("Property bundle has an unexpected UnityFS header: " + bundle_name)
    environment = UnityPy.load(decoded)
    asset = next((value for value in environment.objects if value.type.name == "TextAsset"
                  and getattr(value.read(), "m_Name", "") == asset_name), None)
    if asset is None:
        raise ValueError("Pinned property TextAsset is missing: " + asset_name)
    encoded = payload_bytes(asset.read().m_Script)
    rows = Constructors().table(encoded, constructor)
    return [dict(row) for row in rows], {
        "path": archive_path,
        "container": ".tools/source-packages/zjcs_M509085.apk",
        "container_sha256": APK_SHA256,
        "sha256": bundle_sha256,
        "md5": expected_md5,
        "apk_sha256": APK_SHA256,
        "asset": asset_name,
        "constructor": constructor,
        "rows": len(rows),
    }


def main() -> dict:
    tables = {}
    inputs = []
    for bundle, archive_path, expected_md5, asset, constructor, table_name in TABLES:
        rows, source = decode_bundle_table(bundle, archive_path, expected_md5, asset, constructor)
        if table_name == "map_region":
            rows = [
                {"ClassId": row["ClassId"], "Region": row["Region"],
                 "GoddessActivePropAwardDict": row["GoddessActivePropAwardDict"]}
                for row in rows
            ]
        tables[table_name] = rows
        inputs.append(source)
    if not tables["map_region"] or not tables["npc_talent_tree_prop"]:
        raise ValueError("Pinned character property tables are incomplete.")
    payload = {"manifest": "854_156383", "tables": tables}
    data = (json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    write_atomic(OUTPUT, data)
    manifest = {
        "manifest": "854_156383",
        "operation": "Extract pinned map goddess and NPC talent property tables for the offline character catalog.",
        "inputs": inputs,
        "outputs": {"assets/official/character-property-sources.json": sha256(data)},
    }
    encoded_manifest = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    write_atomic(MANIFEST, encoded_manifest)
    return {"output": str(OUTPUT), "tables": {key: len(value) for key, value in tables.items()},
            "sha256": sha256(data)}


if __name__ == "__main__":
    print(json.dumps(main(), ensure_ascii=False, indent=2))
