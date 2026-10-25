#!/usr/bin/env python3
"""Restore the pinned official icons used by non-default skill appearances."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BUNDLE_NAME = "defaultpackage_assets_image_itemicon_skill_colorful_noatlas.bundle"
BUNDLE_INVENTORY = ROOT / ".tools" / "source-packages" / "cloud-research" / "bundle-inventory.json"
BUNDLE_ROOT = ROOT / ".tools" / "source-packages" / "cloud" / "854_156383" / "DefaultPackage"
RULES_PATH = ROOT / "assets" / "official" / "rules.json"
RULES_MANIFEST = ROOT / "assets" / "reference" / "official-city" / "source-manifest.json"
ICONS_PATH = ROOT / "assets" / "official" / "icons.json"
SPRITE_SOURCE = ROOT / "assets" / "official" / "sprites"
OUTPUT_ROOT = ROOT / "unity" / "Assets" / "Game" / "Resources" / "Character" / "SkillSkinIcons"
SOURCE_MANIFEST = ROOT / "assets" / "reference" / "character-appearance" / "skill-skin-icons.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_guid(relative_path: str) -> str:
    return sha256(("zjcs-unity:" + relative_path).encode("utf-8"))[:32]


def sprite_meta(relative_path: str) -> bytes:
    guid = stable_guid(relative_path)
    return (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
            "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n"
            "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n"
            "  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n"
            "  alphaIsTransparency: 1\n  spriteMode: 1\n  spritePixelsToUnits: 100\n"
            "  spriteMeshType: 1\n  textureCompression: 1\n  platformSettings: []\n"
            "  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n"
            "    physicsShape: []\n    bones: []\n    spriteID: \n    internalID: 0\n"
            "    vertices: []\n    indices: \n    edges: []\n    weights: []\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def folder_meta(relative_path: str) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {stable_guid(relative_path)}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def checked_inputs() -> tuple[dict, dict[str, str], Path, bytes]:
    manifest = json.loads(RULES_MANIFEST.read_text(encoding="utf-8"))
    rule_bytes = RULES_PATH.read_bytes()
    if manifest.get("manifest") != "854_156383" or sha256(rule_bytes) != manifest.get("data_sha256", {}).get("rules.json"):
        raise ValueError("Official character rules are not pinned to the 854_156383 source manifest.")
    rules = json.loads(rule_bytes)
    skin_rows = rules.get("skill_skin")
    items = {int(row["ClassId"]): row for row in rules.get("item", [])}
    if not isinstance(skin_rows, list) or not skin_rows:
        raise ValueError("The pinned official skill_skin table is missing.")
    icons = json.loads(ICONS_PATH.read_text(encoding="utf-8"))
    packaged_icons = {value.get("name") for value in icons.values() if isinstance(value, dict)}
    existing_icons = {path.name.split("-", 1)[0] for path in SPRITE_SOURCE.glob("skill_*.png")}
    needed = set()
    for skin in skin_rows:
        if int(skin.get("SkinType", 0)) == 0:
            continue
        item = items.get(int(skin["ClassId"]))
        if item is None or int(item.get("ItemType", 0)) != 425:
            raise ValueError("A non-default skill skin has no official ItemType 425 row.")
        raw_icon = str(item.get("RawIcon", "")).rsplit("/", 1)[-1]
        if not raw_icon.startswith("skill_"):
            raise ValueError("A skill skin has an unexpected RawIcon: " + raw_icon)
        if raw_icon not in packaged_icons and raw_icon not in existing_icons:
            needed.add(raw_icon)
    if not needed:
        raise ValueError("No missing official skill skin icons were found; check whether this preparation is stale.")

    inventory = json.loads(BUNDLE_INVENTORY.read_text(encoding="utf-8"))
    bundles = inventory.get("bundles", [])
    bundle = next((row for row in bundles if row.get("name") == BUNDLE_NAME), None)
    if bundle is None or bundle.get("manifest") != "854_156383":
        raise ValueError("The skill skin icon bundle is not recorded in the pinned 854_156383 inventory.")
    path = BUNDLE_ROOT / (bundle["hash"] + ".bundle")
    raw = path.read_bytes()
    if len(raw) != int(bundle["size"]) or hashlib.md5(raw).hexdigest() != bundle["hash"]:
        raise ValueError("The cached official skill skin icon bundle differs from its APK manifest record.")
    return bundle, {name: name for name in sorted(needed)}, path, raw


def extract(bundle_path: Path, expected: set[str]) -> dict[str, bytes]:
    sys.path.insert(0, str(ROOT / ".tools" / "character-art-python"))
    try:
        import UnityPy
    except ImportError as error:
        raise RuntimeError("Install UnityPy==1.25.4 in .tools/character-art-python.") from error

    environment = UnityPy.load(str(bundle_path))
    output = {}
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        if sprite.m_Name not in expected:
            continue
        if sprite.m_Name in output:
            raise ValueError("The pinned bundle contains a duplicate skill skin icon: " + sprite.m_Name)
        stream = io.BytesIO()
        sprite.image.save(stream, format="PNG")
        output[sprite.m_Name] = stream.getvalue()
    missing = expected - output.keys()
    if missing:
        raise ValueError("The pinned bundle is missing official skill skin icons: " + ", ".join(sorted(missing)))
    return output


def build(check: bool) -> dict:
    bundle, names, bundle_path, raw_bundle = checked_inputs()
    sprites = extract(bundle_path, set(names))
    bundle_sha = sha256(raw_bundle)
    outputs = []
    sprite_records = []
    for name, data in sorted(sprites.items()):
        relative = f"Character/SkillSkinIcons/{name}.png"
        output = OUTPUT_ROOT / f"{name}.png"
        meta_path = Path(str(output) + ".meta")
        meta = sprite_meta(f"Assets/Game/Resources/{relative}")
        record = {"name": name, "path": relative, "bytes": len(data), "sha256": sha256(data),
                  "meta_sha256": sha256(meta)}
        outputs.extend(((output, data, record["sha256"]), (meta_path, meta, record["meta_sha256"])))
        sprite_records.append(record)

    manifest = {
        "manifest": "854_156383",
        "source_bundle": BUNDLE_NAME,
        "source_bundle_md5": bundle["hash"],
        "source_bundle_sha256": bundle_sha,
        "source_bundle_bytes": len(raw_bundle),
        "source_path": ".tools/source-packages/cloud/854_156383/DefaultPackage/" + bundle_path.name,
        "sprites": sprite_records,
    }
    previous = None
    if SOURCE_MANIFEST.is_file():
        previous = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
        if (previous.get("manifest") != manifest["manifest"]
                or previous.get("source_bundle_md5") != manifest["source_bundle_md5"]):
            raise ValueError("Existing skill skin icon provenance uses another official source bundle.")
    previous_hashes = {}
    if previous is not None:
        for entry in previous.get("sprites", []):
            previous_hashes[entry.get("path")] = (entry.get("sha256"), entry.get("meta_sha256"))

    for path, data, expected_hash in outputs:
        if check:
            if not path.is_file() or sha256(path.read_bytes()) != expected_hash:
                raise ValueError("A restored official skill skin icon is missing or changed: " + str(path))
        elif path.exists() and sha256(path.read_bytes()) != expected_hash:
            relative = path.relative_to(OUTPUT_ROOT).as_posix()
            old = previous_hashes.get("Character/SkillSkinIcons/" + relative.removesuffix(".meta"))
            if old is None or sha256(path.read_bytes()) not in old:
                raise ValueError("Refusing to overwrite changed skill skin icon content: " + str(path))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        elif not path.exists():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)

    folder = OUTPUT_ROOT
    meta = Path(str(folder) + ".meta")
    if check:
        if not meta.is_file() or meta.read_bytes() != folder_meta("Assets/Game/Resources/Character/SkillSkinIcons"):
            raise ValueError("The skill skin icon resource folder metadata is missing or changed.")
    else:
        folder.mkdir(parents=True, exist_ok=True)
        expected_meta = folder_meta("Assets/Game/Resources/Character/SkillSkinIcons")
        if not meta.exists():
            meta.write_bytes(expected_meta)
        elif meta.read_bytes() != expected_meta:
            raise ValueError("Refusing to overwrite changed skill skin icon folder metadata.")

    encoded = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    if check:
        if not SOURCE_MANIFEST.is_file() or SOURCE_MANIFEST.read_text(encoding="utf-8") != encoded:
            raise ValueError("The skill skin icon provenance differs from the extracted original bundle.")
    else:
        SOURCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
        SOURCE_MANIFEST.write_text(encoded, encoding="utf-8")
    return {"sprites": len(sprite_records), "source_bundle_md5": bundle["hash"]}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify restored icons and provenance without writing.")
    args = parser.parse_args()
    result = build(args.check)
    print(f"Verified manifest 854_156383: {result['sprites']} skill skin icons from {result['source_bundle_md5']}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
