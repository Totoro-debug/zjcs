"""Extract the pinned profession full-body sprites and bind them to the character catalog."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools" / "unitypy-work"))
sys.path.insert(0, str(ROOT / "scripts"))

from official_apk import Apk
from official_manifest import read_asset_manifest

VERSION = "854_156383"
SETTINGS = ROOT / "assets" / "official" / "professions.json"
SOURCE = ROOT / "assets" / "reference" / "profession-ui" / "portraits-source-manifest.json"
TARGET = ROOT / "unity" / "Assets" / "Official" / "Character" / "Art" / "sprites"
CATALOG = ROOT / "unity" / "Assets" / "Game" / "Resources" / "Character" / "OfficialCharacterAssetCatalog.asset"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def unity_guid(path: str) -> str:
    return sha(("zjcs-unity:" + path).encode("utf-8"))[:32]


def png_meta(path: str, pixels_per_unit: float, pivot, border) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {unity_guid(path)}\nTextureImporter:\n"
            "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n"
            "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n  sRGBTexture: 1\n"
            "  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n"
            "  alphaIsTransparency: 1\n  spriteMode: 1\n"
            f"  spritePixelsToUnits: {pixels_per_unit:g}\n"
            f"  spriteAlignment: 9\n  spritePivot: {{x: {pivot.x:.9g}, y: {pivot.y:.9g}}}\n"
            f"  spriteBorder: {{x: {border.x:.9g}, y: {border.y:.9g}, z: {border.z:.9g}, w: {border.w:.9g}}}\n"
            "  spriteMeshType: 1\n  textureCompression: 1\n  platformSettings: []\n"
            "  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n"
            "    physicsShape: []\n    bones: []\n    spriteID: \n    internalID: 0\n"
            "    vertices: []\n    indices: \n    edges: []\n    weights: []\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def profession_sprites(settings: dict) -> dict[int, dict[str, str]]:
    sprites = {}
    for style in settings["settings"]["mColors"]:
        for profession in style["Professions"]:
            profession = int(profession)
            if profession <= 0:
                continue
            sprites[profession] = {
                "0": style["ManRoleFullBodyImg"]["mAssetGuid"],
                "1": style["WomanRoleFullBodyImg"]["mAssetGuid"],
            }
    return sprites


def load_bundle(apk, name: str):
    if name in apk.loaded:
        return
    expected = apk.bundle_records[name]
    apk_path = "assets/yoo/DefaultPackage/" + expected["file"]
    if apk_path in apk.available:
        raw = apk.archive.read(apk_path)
        source = {"origin": "apk_zip", "apk_sha256": apk.source.apk_sha256, "apk_entry": apk_path}
    else:
        cache = ROOT / ".tools" / "source-packages" / "extracted"
        raw = (cache / name).read_bytes()
        sources = json.loads((cache / "bundle-sources.json").read_text(encoding="utf-8"))
        source = sources[name]
    if len(raw) != expected["size"] or hashlib.md5(raw).hexdigest() != expected["hash"]:
        raise ValueError("A profession portrait bundle failed its pinned size/MD5 check: " + name)
    bundle = apk.env.load_file(apk.decode(raw), name=name)
    apk.loaded[name] = bundle
    for asset in bundle.files.values():
        if hasattr(asset, "objects"):
            apk.cabs[asset.name.lower()] = dict({"bundle": name, "hash": expected["hash"]}, **source)


def build() -> None:
    settings_bytes = SETTINGS.read_bytes()
    settings = json.loads(settings_bytes.decode("utf-8"))
    by_profession = profession_sprites(settings)
    guids = sorted({guid for values in by_profession.values() for guid in values.values()})
    apk = Apk()
    manifest_name = "assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes"
    manifest_bytes = apk.archive.read(manifest_name)
    if apk.version != VERSION:
        raise ValueError("The pinned profession portrait manifest is not 854_156383.")
    asset_rows = {row["guid"]: row for row in read_asset_manifest(manifest_bytes) if row["guid"] in guids}
    if set(asset_rows) != set(guids):
        raise ValueError("A profession portrait GUID is absent from the pinned APK asset manifest.")

    portrait_rows = {}
    source_rows = {}
    for profession in sorted(by_profession):
        portrait_rows[str(profession)] = {}
        for sex, asset_guid in sorted(by_profession[profession].items()):
            asset = asset_rows[asset_guid]
            if not asset["path"].startswith("Assets/Image/UI/Role/") or not asset["path"].endswith(".png"):
                raise ValueError("Profession portrait GUID does not point to an original role PNG: " + asset["path"])
            load_bundle(apk, asset["bundle"])
            pointer = next((ptr for path, ptr in apk.container(asset["bundle"])
                            if path.lower() == asset["path"].lower() and ptr.type.name == "Sprite"), None)
            if pointer is None:
                raise ValueError("The referenced profession portrait sprite is missing from its verified bundle: " + asset["path"])
            sprite = pointer.read()
            raw = sprite.image
            from io import BytesIO
            stream = BytesIO()
            raw.save(stream, format="PNG")
            png = stream.getvalue()
            filename = "ProfessionPortrait_" + asset_guid[:12] + "-" + sha(png)[:10] + ".png"
            relative = "sprites/" + filename
            target = TARGET / filename
            meta_path = Path(str(target) + ".meta")
            meta = png_meta(relative, float(sprite.m_PixelsToUnits), sprite.m_Pivot, sprite.m_Border)
            for path, content in ((target, png), (meta_path, meta)):
                if path.exists() and path.read_bytes() != content:
                    if path == meta_path:
                        existing_meta = path.read_text(encoding="utf-8")
                        expected_guid = unity_guid(relative)
                        if (f"guid: {expected_guid}" not in existing_meta
                                or "spriteMode: 1" not in existing_meta
                                or f"spritePixelsToUnits: {float(sprite.m_PixelsToUnits):g}" not in existing_meta):
                            raise ValueError("Refusing to replace a profession portrait with mismatched Unity sprite settings: " + str(path.relative_to(ROOT)))
                    else:
                        raise ValueError("Refusing to replace a modified profession portrait output: " + str(path.relative_to(ROOT)))
                if not path.exists():
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(content)
            portrait_rows[str(profession)][sex] = relative
            source_rows[asset_guid] = {
                "path": asset["path"], "bundle": asset["bundle"],
                "bundle_hash": apk.index[asset["bundle"]], "path_id": str(pointer.path_id),
                "png_sha256": sha(png), "asset_sha256": sha(pointer.deref().get_raw_data()),
                "pixels_per_unit": float(sprite.m_PixelsToUnits), "width": raw.width, "height": raw.height,
                "pivot": dict(x=sprite.m_Pivot.x, y=sprite.m_Pivot.y),
                "border": dict(x=sprite.m_Border.x, y=sprite.m_Border.y, z=sprite.m_Border.z, w=sprite.m_Border.w),
                "unity_path": "unity/Assets/Official/Character/Art/" + relative,
                "meta_sha256": sha(meta_path.read_bytes()),
            }

    catalog_bytes = CATALOG.read_bytes()
    newline = "\r\n" if b"\r\n" in catalog_bytes else "\n"
    catalog_text = catalog_bytes.decode("utf-8").replace("\r\n", "\n")
    valid_paths = {path for variants in portrait_rows.values() for path in variants.values()}
    catalog_text = re.sub(r"(?m)^  - sourcePath: (sprites/ProfessionPortrait_[^\n]+)\n    sprite: [^\n]+\n?",
                          lambda match: match.group(0) if match.group(1) in valid_paths else "", catalog_text)
    existing = set(re.findall(r"^  - sourcePath: (.+)$", catalog_text, re.M))
    additions = []
    for relative in sorted({path for sexes in portrait_rows.values() for path in sexes.values()}):
        if relative in existing:
            continue
        filename = relative.rsplit("/", 1)[-1]
        meta_guid = unity_guid(relative)
        if not (TARGET / filename).with_suffix(".png.meta").exists():
            raise ValueError("A profession portrait sprite meta file is missing: " + filename)
        additions.append("  - sourcePath: " + relative + "\n"
                         "    sprite: {fileID: 21300000, guid: " + meta_guid + ", type: 3}\n")
    catalog_text = catalog_text.rstrip() + "\n" + "".join(additions)
    converted_catalog = catalog_text.replace("\n", newline).encode("utf-8")
    if catalog_bytes != converted_catalog:
        CATALOG.write_bytes(converted_catalog)

    payload = {
        "manifest": VERSION,
        "operation": "Extract pinned ProfessionSettings full-body role sprites and bind them to the Unity character catalog.",
        "settings_sha256": sha(settings_bytes),
        "apk_sha256": apk.source.apk_sha256,
        "package_manifest_sha256": sha(manifest_bytes),
        "asset_guids": source_rows,
        "profession_portraits": portrait_rows,
        "catalog": "unity/Assets/Game/Resources/Character/OfficialCharacterAssetCatalog.asset",
        "catalog_sha256": sha(CATALOG.read_bytes()),
        "outputs": {
            "unity/Assets/Official/Character/Art/" + relative: sha((ROOT / "unity" / "Assets" / "Official" / "Character" / "Art" / relative).read_bytes())
            for relative in sorted({path for variants in portrait_rows.values() for path in variants.values()})
        },
    }
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    SOURCE.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Extracted profession full-body sprites:", len(source_rows), "source assets for", len(portrait_rows), "profession IDs")


if __name__ == "__main__":
    build()
