"""Recover the official player skin selection sprites from the pinned APK bundle."""
from __future__ import annotations

import hashlib
import io
import json
import sys
from pathlib import Path
from official_source import OfficialSource
from official_bundle_reader import OfficialBundleReader

ROOT = Path(__file__).resolve().parents[1]
BUNDLE_NAME = "defaultpackage_assets_image_ui_playerskin.bundle"
SOURCE = OfficialSource()
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered/PlayerSkinIcons"
PAYLOAD_MANIFEST = PAYLOAD / "source-manifest.json"
REFERENCE_MANIFEST = ROOT / "assets/reference/character-creation/player-skin-icons-source-manifest.json"
APPEARANCE_OPTIONS = ROOT / "unity/Assets/Official/CharacterCreation/appearance-options.json"
SPRITE_FIELDS = (
    "hairstyle_id", "eyebrow_id", "eye_id", "mouth_id", "makeup_id", "feature_id",
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_guid(path: str) -> str:
    return sha256(("zjcs:" + path).encode("utf-8"))[:32]


def write_checked(path: Path, data: bytes, previous_hash: str | None) -> None:
    if path.exists():
        current_hash = sha256(path.read_bytes())
        output_hash = sha256(data)
        if current_hash != output_hash and current_hash != previous_hash:
            raise ValueError("Refusing to overwrite changed character skin icon payload: " + str(path))
        if current_hash == output_hash:
            return
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)


def folder_meta(relative_path: str) -> bytes:
    return ("fileFormatVersion: 2\n"
            "guid: " + stable_guid(relative_path) + "\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("utf-8")


def text_meta(relative_path: str) -> bytes:
    return ("fileFormatVersion: 2\n"
            "guid: " + stable_guid(relative_path) + "\n"
            "TextScriptImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("utf-8")


def sprite_meta(relative_path: str) -> bytes:
    guid = stable_guid(relative_path)
    return ("fileFormatVersion: 2\n"
            "guid: " + guid + "\n"
            "TextureImporter:\n"
            "  serializedVersion: 13\n"
            "  internalIDToNameTable: []\n"
            "  externalObjects: {}\n"
            "  mipmaps:\n"
            "    mipMapMode: 0\n"
            "    enableMipMap: 0\n"
            "    sRGBTexture: 1\n"
            "    linearTexture: 0\n"
            "    fadeOut: 0\n"
            "    borderMipMap: 0\n"
            "    mipMapsPreserveCoverage: 0\n"
            "    alphaTestReferenceValue: 0.5\n"
            "    mipMapFadeDistanceStart: 1\n"
            "    mipMapFadeDistanceEnd: 3\n"
            "  bumpmap:\n"
            "    convertToNormalMap: 0\n"
            "    externalNormalMap: 0\n"
            "    heightScale: 0.25\n"
            "    normalMapFilter: 0\n"
            "    flipGreenChannel: 0\n"
            "  isReadable: 0\n"
            "  streamingMipmaps: 0\n"
            "  streamingMipmapsPriority: 0\n"
            "  vTOnly: 0\n"
            "  ignoreMipmapLimit: 0\n"
            "  grayScaleToAlpha: 0\n"
            "  generateCubemap: 6\n"
            "  cubemapConvolution: 0\n"
            "  seamlessCubemap: 0\n"
            "  textureFormat: 3\n"
            "  maxTextureSize: 2048\n"
            "  textureSettings:\n"
            "    serializedVersion: 2\n"
            "    filterMode: 1\n"
            "    aniso: 1\n"
            "    mipBias: 0\n"
            "    wrapU: 1\n"
            "    wrapV: 1\n"
            "    wrapW: 1\n"
            "  nPOTScale: 0\n"
            "  lightmap: 0\n"
            "  compressionQuality: 50\n"
            "  spriteMode: 1\n"
            "  spriteExtrude: 1\n"
            "  spriteMeshType: 1\n"
            "  alignment: 0\n"
            "  spritePivot: {x: 0.5, y: 0.5}\n"
            "  spritePixelsToUnits: 100\n"
            "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n"
            "  spriteGenerateFallbackPhysicsShape: 0\n"
            "  alphaUsage: 1\n"
            "  alphaIsTransparency: 1\n"
            "  spriteTessellationDetail: -1\n"
            "  textureType: 8\n"
            "  textureShape: 1\n"
            "  singleChannelComponent: 0\n"
            "  flipbookRows: 0\n"
            "  flipbookColumns: 0\n"
            "  maxTextureSizeSet: 0\n"
            "  compressionQualitySet: 0\n"
            "  textureFormatSet: 0\n"
            "  ignorePngGamma: 0\n"
            "  applyGammaDecoding: 0\n"
            "  swizzle: 50462976\n"
            "  platformSettings: []\n"
            "  spriteSheet:\n"
            "    serializedVersion: 2\n"
            "    sprites: []\n"
            "    outline: []\n"
            "    physicsShape: []\n"
            "    bones: []\n"
            "    spriteID: " + guid + "\n"
            "    internalID: 0\n"
            "    vertices: []\n"
            "    indices: \n"
            "    edges: []\n"
            "    weights: []\n"
            "    secondaryTextures: []\n"
            "    nameFileIdTable: {}\n"
            "  mipmapLimitGroupName: \n"
            "  pSDRemoveMatte: 0\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("utf-8")


def source_records() -> tuple[bytes, dict, dict]:
    with OfficialBundleReader(SOURCE) as reader:
        verified = reader.read('DefaultPackage', BUNDLE_NAME)
    data, source = verified.data, dict(verified.provenance)
    bundle = {
        "name": BUNDLE_NAME,
        "origin": source["origin"],
        "bundle_md5": hashlib.md5(data).hexdigest(),
        "bundle_sha256": sha256(data),
        "bytes": len(data),
    }
    if source['origin'] == 'apk_zip':
        bundle.update(apk_entry=source['apk_entry'], apk_sha256=source['apk_sha256'])
    else:
        bundle['url'] = source['url']
    return data, source, bundle


def required_option_icons() -> set[str]:
    data = json.loads(APPEARANCE_OPTIONS.read_text(encoding="utf-8"))
    required = set()
    for sections in data["bodies"].values():
        for field in SPRITE_FIELDS:
            for option in sections.get(field, []):
                skin = data["skins"].get(str(option["skin_id"]))
                if not skin or not skin.get("SkinName"):
                    raise ValueError("Official appearance option has no skin icon name: " + str(option))
                required.add(skin["SkinName"])
    return required


def collect_sprites(bundle_data: bytes, UnityPy) -> dict[str, bytes]:
    from official_apk import Apk
    environment = UnityPy.load(Apk.decode(bundle_data))
    output = {}
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        name = sprite.m_Name
        if not name or name in output:
            raise ValueError("Official player skin sprite name is empty or duplicated: " + str(name))
        stream = io.BytesIO()
        sprite.image.save(stream, format="PNG")
        output[name] = stream.getvalue()
    if not output:
        raise ValueError("Pinned player skin icon bundle contains no Sprite assets.")
    return output


def build() -> dict:
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    try:
        import UnityPy
    except ImportError as error:
        raise RuntimeError("Install UnityPy==1.25.4 in .tools/character-art-python") from error

    bundle_path, source, bundle = source_records()
    previous_data = REFERENCE_MANIFEST.read_bytes() if REFERENCE_MANIFEST.is_file() else None
    previous = json.loads(previous_data.decode("utf-8")) if previous_data else None
    previous_hashes = {}
    if previous is not None:
        previous_hashes = {item["path"]: item["sha256"] for item in previous.get("sprites", [])}
        if PAYLOAD_MANIFEST.is_file() and PAYLOAD_MANIFEST.read_bytes() != previous_data:
            raise ValueError("Player skin icon payload and reference manifests disagree.")

    sprites = collect_sprites(bundle_path, UnityPy)
    required = required_option_icons()
    missing = sorted(required - sprites.keys())
    if missing:
        raise ValueError("Official character creation option icons are missing: " + ", ".join(missing[:20]))

    outputs = []
    payload_files = []
    for name, data in sorted(sprites.items(), key=lambda item: item[0]):
        relative = "Assets/Official/CharacterCreation/Recovered/PlayerSkinIcons/" + name + ".png"
        outputs.append({"name": name, "path": relative, "bytes": len(data), "sha256": sha256(data)})
        payload_files.append((relative, data))

    manifest = {
        "manifest": SOURCE.version,
        "apk_sha256": SOURCE.apk_sha256,
        "source_bundle": bundle,
        "required_option_icon_count": len(required),
        "sprites": outputs,
    }
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    for relative, data in payload_files:
        target = ROOT / "unity" / relative
        write_checked(target, data, previous_hashes.get(relative))
        relative_to_unity = relative + ".meta"
        meta = sprite_meta(relative_to_unity)
        meta_target = ROOT / "unity" / relative_to_unity
        if meta_target.exists() and meta_target.read_bytes() != meta:
            raise ValueError("Refusing to overwrite changed player skin icon metadata: " + str(meta_target))
        if not meta_target.exists():
            meta_target.write_bytes(meta)

    folder = PAYLOAD
    folder.mkdir(parents=True, exist_ok=True)
    folder_meta_path = folder.with_suffix(".meta")
    folder_meta_data = folder_meta("Assets/Official/CharacterCreation/Recovered/PlayerSkinIcons")
    if folder_meta_path.exists() and folder_meta_path.read_bytes() != folder_meta_data:
        raise ValueError("Refusing to overwrite changed player skin icon folder metadata.")
    if not folder_meta_path.exists():
        folder_meta_path.write_bytes(folder_meta_data)

    write_checked(PAYLOAD_MANIFEST, encoded,
                  sha256(previous_data) if previous_data else None)
    manifest_meta = PAYLOAD_MANIFEST.with_suffix(PAYLOAD_MANIFEST.suffix + ".meta")
    manifest_meta_data = text_meta("Assets/Official/CharacterCreation/Recovered/PlayerSkinIcons/source-manifest.json")
    if manifest_meta.exists() and manifest_meta.read_bytes() != manifest_meta_data:
        raise ValueError("Refusing to overwrite changed player skin icon manifest metadata.")
    if not manifest_meta.exists():
        manifest_meta.write_bytes(manifest_meta_data)

    REFERENCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    if REFERENCE_MANIFEST.exists() and REFERENCE_MANIFEST.read_bytes() != encoded:
        if previous_data != REFERENCE_MANIFEST.read_bytes():
            raise ValueError("Refusing to overwrite changed player skin icon source manifest.")
    REFERENCE_MANIFEST.write_bytes(encoded)
    return {"sprite_count": len(outputs), "required_option_icon_count": len(required),
            "missing_option_icons": len(missing), "manifest": REFERENCE_MANIFEST.relative_to(ROOT).as_posix()}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
