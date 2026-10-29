"""Convert pinned official dungeon and arena UI sprites into local PNG assets."""
from __future__ import annotations

import hashlib
from io import BytesIO
import json
from pathlib import Path
import re

import UnityPy

ROOT = Path(__file__).resolve().parents[1]
VERSION = "871_156598"
EXTRACTED = ROOT / ".tools/source-packages/extracted"
ASSETS = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI/Sprites"
MANIFEST = ROOT / ".tools/source-packages/manifest-assets.json"
BUNDLE_SOURCES = EXTRACTED / "bundle-sources.json"
RECEIPT = ROOT / "assets/reference/dungeon-ui/sprite-source-manifest.json"
MAP_CLASSES = (11, 12, 13, 14, 17, 19, 21, 23)
BUNDLES = (
    "defaultpackage_assets_image_ui_arena.bundle",
    "defaultpackage_assets_image_ui_notes_travelnotes.bundle",
    "defaultpackage_assets_image_ui_notes_dailydungeon_dailydungeon_atlas.bundle",
    *(f"defaultpackage_assets_image_ui_notes_dailydungeon_map_{value}.bundle"
      for value in MAP_CLASSES),
)
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_meta(asset_guid: str) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {asset_guid}\nTextureImporter:\n"
            "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n"
            "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n  sRGBTexture: 1\n"
            "  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n"
            "  alphaIsTransparency: 1\n  spriteMode: 1\n  spritePixelsToUnits: 100\n"
            "  spriteMeshType: 1\n  textureCompression: 1\n  platformSettings: []\n"
            "  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n"
            "    physicsShape: []\n    bones: []\n    spriteID: \n    internalID: 0\n"
            "    vertices: []\n    indices: \n    edges: []\n    weights: []\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def ensure_folder_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    meta = path.with_name(path.name + ".meta")
    folder_guid = hashlib.sha256(("zjcs-dungeon-ui-folder:" + relative).encode()).hexdigest()[:32]
    if meta.exists():
        existing = META_GUID.search(meta.read_text(encoding="ascii"))
        if existing is None:
            raise ValueError("UI sprite folder metadata is malformed: " + str(meta))
        return
    meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {folder_guid}\n"
                    "folderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="ascii")


def ensure_output_folders() -> None:
    assets_root = ROOT / "unity/Assets"
    folders = [ASSETS]
    while folders[-1] != assets_root:
        folders.append(folders[-1].parent)
    for folder in reversed(folders[:-1]):
        ensure_folder_meta(folder)


def main() -> int:
    ensure_output_folders()
    assets = json.loads(MANIFEST.read_text(encoding="utf-8"))
    source_records = json.loads(BUNDLE_SOURCES.read_text(encoding="utf-8"))
    by_bundle_name = {}
    for record in assets:
        if record.get("bundle") in BUNDLES:
            by_bundle_name.setdefault(record["bundle"], {})[
                Path(record.get("path", "")).stem] = record

    outputs = []
    for bundle in BUNDLES:
        path = EXTRACTED / bundle
        source = source_records.get(bundle)
        if source is None or source.get("manifest") != VERSION:
            raise ValueError("UI sprite bundle is not verified for " + VERSION + ": " + bundle)
        bundle_bytes = path.read_bytes()
        bundle_hash = sha256(bundle_bytes)
        if bundle_hash != source.get("bundle_sha256"):
            raise ValueError("UI sprite bundle hash changed: " + bundle)

        environment = UnityPy.load(str(path))
        sprite_records = by_bundle_name.get(bundle, {})
        for reader in environment.objects:
            if reader.type.name != "Sprite":
                continue
            sprite = reader.read()
            sprite_name = reader.read_typetree(check_read=False).get("m_Name", "")
            record = sprite_records.get(sprite_name)
            if record is None:
                raise ValueError("Official sprite has no source manifest path: "
                                 + bundle + ":" + sprite_name)
            image = sprite.image
            if image is None or image.width <= 0 or image.height <= 0:
                raise ValueError("Official sprite has no decoded pixels: " + sprite_name)
            relative = Path(bundle.removesuffix(".bundle")) / (sprite_name + ".png")
            target = ASSETS / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            image_buffer = BytesIO()
            image.save(image_buffer, format="PNG", optimize=False)
            image_bytes = image_buffer.getvalue()
            if target.exists() and target.read_bytes() != image_bytes:
                raise ValueError("Refusing to overwrite changed official UI sprite: " + str(target))
            if not target.exists():
                target.write_bytes(image_bytes)
            asset_guid = hashlib.sha256(
                ("zjcs-dungeon-ui-sprite:" + record["guid"]).encode("utf-8")).hexdigest()[:32]
            meta = target.with_name(target.name + ".meta")
            expected_meta = asset_meta(asset_guid)
            if meta.exists():
                existing_guid = META_GUID.search(meta.read_text(encoding="ascii"))
                if existing_guid is None or existing_guid.group(1) != asset_guid:
                    raise ValueError("UI sprite metadata GUID changed: " + str(meta))
            else:
                meta.write_bytes(expected_meta)
            outputs.append({
                "path": target.relative_to(ROOT).as_posix(),
                "bundle": bundle,
                "bundle_sha256": bundle_hash,
                "source_path": record["path"],
                "source_guid": record["guid"],
                "asset_guid": asset_guid,
                "sprite_name": sprite_name,
                "width": image.width,
                "height": image.height,
                "sha256": sha256(target.read_bytes()),
                "meta_sha256": sha256(meta.read_bytes()),
            })

    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    receipt = {"manifest": VERSION, "bundles": list(BUNDLES), "outputs": outputs}
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": VERSION, "sprites": len(outputs),
                      "receipt": RECEIPT.relative_to(ROOT).as_posix()}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
