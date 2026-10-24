"""Recover the official character creation appearance group icons."""
from __future__ import annotations

import hashlib
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
EXTRACTED = ROOT / ".tools/source-packages/extracted"
OFFICIAL_DATA = ROOT / ".tools/source-packages/official-data"
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered/AppearanceGroupIcons"
PAYLOAD_MANIFEST = PAYLOAD / "source-manifest.json"
REFERENCE_MANIFEST = ROOT / "assets/reference/character-creation/appearance-group-icons-source-manifest.json"

GROUP_ITEMS = {
    1: ("male", 35506),
    2: ("male", 35599),
    3: ("male", 36008),
    4: ("male", 36001),
    5: ("male", 35891),
    6: ("male", 39815),
    11: ("female", 35556),
    12: ("female", 35600),
    13: ("female", 36108),
    14: ("female", 36101),
    15: ("female", 35893),
    16: ("female", 39816),
}

ITEM_BUNDLES = {
    35506: "defaultpackage_assets_image_itemicon_appearance_default_noatlas.bundle",
    35556: "defaultpackage_assets_image_itemicon_appearance_default_noatlas.bundle",
    35599: "defaultpackage_assets_image_itemicon_appearance_default_noatlas.bundle",
    35600: "defaultpackage_assets_image_itemicon_appearance_default_noatlas.bundle",
    35891: "defaultpackage_assets_image_itemicon_appearance_linkage04_noatlas.bundle",
    35893: "defaultpackage_assets_image_itemicon_appearance_linkage04_noatlas.bundle",
    36001: "defaultpackage_assets_image_itemicon_appearance_accumulatepay01_noatlas.bundle",
    36101: "defaultpackage_assets_image_itemicon_appearance_accumulatepay01_noatlas.bundle",
    36008: "defaultpackage_assets_image_itemicon_appearance_pay01_noatlas.bundle",
    36108: "defaultpackage_assets_image_itemicon_appearance_pay01_noatlas.bundle",
    39815: "defaultpackage_assets_image_itemicon_appearance_linkage10_noatlas.bundle",
    39816: "defaultpackage_assets_image_itemicon_appearance_linkage10_noatlas.bundle",
}

LOCALIZATION_NAME = "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"
LOCALIZATION_MD5 = "9a0b6ab8c5136050fd005ead8f567863"
LOCALIZATION_SHA256 = "738069eafacd7fe33aae970aa5edacd76f48ed02acdd07ededad1c4a499b93b8"
LOCALIZATION_PATH = ROOT / ".tools/source-packages/WwiseBnk" / LOCALIZATION_NAME

XXHASH_PRIMES = (11400714785074694791, 14029467366897019727, 1609587929392839161,
                 9650029242287828579, 2870177450012600261)
MASK64 = (1 << 64) - 1


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _rotate_left_64(value: int, count: int) -> int:
    return ((value << count) | (value >> (64 - count))) & MASK64


def _xxhash_round(accumulator: int, value: int) -> int:
    accumulator = (accumulator + value * XXHASH_PRIMES[1]) & MASK64
    accumulator = _rotate_left_64(accumulator, 31)
    return (accumulator * XXHASH_PRIMES[0]) & MASK64


def xxhash64(data: bytes) -> int:
    offset = 0
    if len(data) >= 32:
        v1 = (XXHASH_PRIMES[0] + XXHASH_PRIMES[1]) & MASK64
        v2 = XXHASH_PRIMES[1]
        v3 = 0
        v4 = (-XXHASH_PRIMES[0]) & MASK64
        limit = len(data) - 32
        while offset <= limit:
            v1 = _xxhash_round(v1, int.from_bytes(data[offset:offset + 8], "little")); offset += 8
            v2 = _xxhash_round(v2, int.from_bytes(data[offset:offset + 8], "little")); offset += 8
            v3 = _xxhash_round(v3, int.from_bytes(data[offset:offset + 8], "little")); offset += 8
            v4 = _xxhash_round(v4, int.from_bytes(data[offset:offset + 8], "little")); offset += 8
        result = (_rotate_left_64(v1, 1) + _rotate_left_64(v2, 7)
                  + _rotate_left_64(v3, 12) + _rotate_left_64(v4, 18)) & MASK64
        for lane in (v1, v2, v3, v4):
            lane = _xxhash_round(0, lane)
            result = ((result ^ lane) * XXHASH_PRIMES[0] + XXHASH_PRIMES[3]) & MASK64
    else:
        result = XXHASH_PRIMES[4]

    result = (result + len(data)) & MASK64
    while offset <= len(data) - 8:
        lane = _xxhash_round(0, int.from_bytes(data[offset:offset + 8], "little"))
        result ^= lane
        result = (_rotate_left_64(result, 27) * XXHASH_PRIMES[0] + XXHASH_PRIMES[3]) & MASK64
        offset += 8
    if offset <= len(data) - 4:
        result ^= (int.from_bytes(data[offset:offset + 4], "little") * XXHASH_PRIMES[0]) & MASK64
        result = (_rotate_left_64(result, 23) * XXHASH_PRIMES[1] + XXHASH_PRIMES[2]) & MASK64
        offset += 4
    while offset < len(data):
        result ^= (data[offset] * XXHASH_PRIMES[4]) & MASK64
        result = (_rotate_left_64(result, 11) * XXHASH_PRIMES[0]) & MASK64
        offset += 1
    result ^= result >> 33
    result = (result * XXHASH_PRIMES[1]) & MASK64
    result ^= result >> 29
    result = (result * XXHASH_PRIMES[2]) & MASK64
    return result ^ (result >> 32)


def load_localized_group_labels() -> tuple[dict[int, str], dict]:
    indexes = json.loads((ROOT / ".tools/source-packages/WwiseBnk/bundle-index.json").read_text(encoding="utf-8"))
    sources = json.loads((ROOT / ".tools/source-packages/WwiseBnk/bundle-sources.json").read_text(encoding="utf-8"))
    data = LOCALIZATION_PATH.read_bytes()
    source = sources.get(LOCALIZATION_NAME)
    if (indexes.get(LOCALIZATION_NAME) != LOCALIZATION_MD5
            or hashlib.md5(data).hexdigest() != LOCALIZATION_MD5
            or len(data) != 5291479 or sha256(data) != LOCALIZATION_SHA256
            or not isinstance(source, dict) or source.get("origin") != "apk_zip"
            or source.get("apk_sha256") != APK_SHA256 or source.get("bundle_hash") != LOCALIZATION_MD5):
        raise ValueError("Pinned official Chinese localization bundle is missing or changed.")

    if len(data) < 4:
        raise ValueError("Pinned official Chinese localization file is truncated.")
    count = int.from_bytes(data[:4], "little")
    text_start = 4 + count * 12
    if text_start > len(data):
        raise ValueError("Pinned official Chinese localization index is invalid.")
    offsets = {}
    for index in range(count):
        entry_offset = 4 + index * 12
        key_hash = int.from_bytes(data[entry_offset:entry_offset + 8], "little")
        text_offset = int.from_bytes(data[entry_offset + 8:entry_offset + 12], "little")
        if key_hash in offsets:
            raise ValueError("Pinned official Chinese localization has a duplicate key hash.")
        offsets[key_hash] = text_offset

    labels = {}
    for group_id in GROUP_ITEMS:
        key = "append_appearance_group_" + str(group_id)
        key_hash = xxhash64(key.encode("utf-8"))
        if key_hash not in offsets:
            raise ValueError("Pinned official Chinese localization is missing: " + key)
        text_offset = text_start + offsets[key_hash]
        if text_offset + 2 > len(data):
            raise ValueError("Pinned official Chinese localization offset is invalid: " + key)
        text_length = int.from_bytes(data[text_offset:text_offset + 2], "little")
        text_end = text_offset + 2 + text_length
        if text_end > len(data):
            raise ValueError("Pinned official Chinese localization text is truncated: " + key)
        labels[group_id] = data[text_offset + 2:text_end].decode("utf-8")

    return labels, {
        "name": LOCALIZATION_NAME,
        "origin": "apk_zip",
        "apk_sha256": APK_SHA256,
        "apk_entry": source["path"],
        "bundle_md5": LOCALIZATION_MD5,
        "bundle_sha256": LOCALIZATION_SHA256,
        "bytes": len(data),
    }


def stable_guid(path: str) -> str:
    return sha256(("zjcs:" + path).encode("utf-8"))[:32]


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


def load_group_records() -> dict[int, dict]:
    tables = json.loads((OFFICIAL_DATA / "tables.json").read_text(encoding="utf-8"))
    rows = tables.get("append_appearance_group")
    appearance_rows = tables.get("append_appearance")
    if not isinstance(rows, list):
        raise ValueError("The decoded pinned append_appearance_group table is missing.")
    if not isinstance(appearance_rows, list):
        raise ValueError("The decoded pinned append_appearance table is missing.")
    appearances = {row["ClassId"]: row for row in appearance_rows}
    options = json.loads((ROOT / "unity/Assets/Official/CharacterCreation/appearance-options.json")
                         .read_text(encoding="utf-8"))
    skin_infos = options.get("skins")
    if not isinstance(skin_infos, dict):
        raise ValueError("Official character appearance skin metadata is missing.")
    result = {}
    for row in rows:
        if row.get("GroupId") in GROUP_ITEMS:
            result[row["GroupId"]] = row
    if set(result) != set(GROUP_ITEMS):
        raise ValueError("Pinned append_appearance_group rows do not match the expected twelve creation groups.")
    for group_id, (sex, item_id) in GROUP_ITEMS.items():
        row = result[group_id]
        if row.get("ItemIdDict", {}).get("0" if sex == "male" else "1") != item_id:
            raise ValueError("Pinned appearance group item mapping changed: " + str(group_id))
        sex_id = "0" if sex == "male" else "1"
        preview_skins = []
        for appearance_id in row.get("AppearanceIdList") or []:
            appearance = appearances.get(appearance_id)
            skin_id = appearance.get("SkinDict", {}).get(sex_id) if appearance else None
            skin = skin_infos.get(str(skin_id)) if skin_id is not None else None
            if skin is None:
                raise ValueError("Official appearance group references an unknown character skin: "
                                 + str(group_id) + "/" + str(appearance_id))
            preview_skins.append({"appearance_id": appearance_id, "skin_id": skin_id,
                                  "skin_type": skin["SkinType"], "skin_name": skin["SkinName"]})
        if not preview_skins:
            raise ValueError("Official appearance group has no preview skins: " + str(group_id))
        row["PreviewSkins"] = preview_skins
    return result


def verify_bundle(name: str, indexes: dict, sources: dict) -> dict:
    path = EXTRACTED / name
    if not path.is_file():
        raise FileNotFoundError("The official appearance icon bundle has not been extracted: " + name)
    data = path.read_bytes()
    expected_md5 = indexes.get(name)
    source = sources.get(name)
    if not expected_md5 or hashlib.md5(data).hexdigest() != expected_md5:
        raise ValueError("Official appearance icon bundle MD5 differs from the pinned index: " + name)
    if not isinstance(source, dict) or source.get("bundle_hash") != expected_md5:
        raise ValueError("Official appearance icon bundle has no matching source receipt: " + name)
    record = {"name": name, "origin": source.get("origin"), "bundle_md5": expected_md5,
              "bundle_sha256": sha256(data), "bytes": len(data)}
    if source.get("origin") == "apk_zip":
        if source.get("apk_sha256") != APK_SHA256:
            raise ValueError("Appearance icon bundle is not from the pinned APK: " + name)
        record["apk_sha256"] = source["apk_sha256"]
        record["apk_entry"] = source["path"]
    elif source.get("origin") == "official_cdn":
        if (source.get("manifest") != "854_156383" or source.get("size") != len(data)
                or source.get("sha256") != record["bundle_sha256"]):
            raise ValueError("Appearance icon bundle does not match its pinned CDN receipt: " + name)
        record["manifest"] = source["manifest"]
        record["url"] = source["url"]
    else:
        raise ValueError("Appearance icon bundle has an unsupported source: " + name)
    return record


def collect_icons(bundle_path: Path, item_ids: set[int], UnityPy) -> dict[int, dict]:
    environment = UnityPy.load(str(bundle_path))
    found = {}
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        name = sprite.m_Name or ""
        if not name.startswith("item_") or not name[5:].isdigit():
            continue
        item_id = int(name[5:])
        if item_id not in item_ids:
            continue
        if item_id in found:
            raise ValueError("Official appearance icon is duplicated: " + name)
        stream = io.BytesIO()
        sprite.image.save(stream, format="PNG")
        found[item_id] = {"path_id": str(obj.path_id), "data": stream.getvalue()}
    return found


def write_checked(path: Path, data: bytes, expected_previous_hash: str | None) -> None:
    if path.is_file():
        current = path.read_bytes()
        if sha256(current) == sha256(data):
            return
        if expected_previous_hash is None or sha256(current) != expected_previous_hash:
            raise ValueError("Refusing to overwrite a changed appearance icon asset: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)


def build() -> dict:
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    try:
        import UnityPy
    except ImportError as error:
        raise RuntimeError("Install UnityPy==1.25.4 in .tools/character-art-python") from error

    indexes = json.loads((EXTRACTED / "bundle-index.json").read_text(encoding="utf-8"))
    sources = json.loads((EXTRACTED / "bundle-sources.json").read_text(encoding="utf-8"))
    groups = load_group_records()
    group_labels, localization_record = load_localized_group_labels()
    bundle_names = sorted(set(ITEM_BUNDLES.values()))
    bundle_records = [verify_bundle(name, indexes, sources) for name in bundle_names]
    icons = {}
    for bundle in bundle_records:
        expected = {item_id for item_id, name in ITEM_BUNDLES.items() if name == bundle["name"]}
        found = collect_icons(EXTRACTED / bundle["name"], expected, UnityPy)
        if set(found) != expected:
            raise ValueError("Official appearance icon bundle is missing items: "
                             + ", ".join(str(item) for item in sorted(expected - found.keys())))
        icons.update(found)

    previous_manifest = None
    previous_hashes = {}
    if REFERENCE_MANIFEST.is_file():
        previous_manifest = json.loads(REFERENCE_MANIFEST.read_text(encoding="utf-8"))
        if previous_manifest.get("manifest") != "854_156383" or previous_manifest.get("apk_sha256") != APK_SHA256:
            raise ValueError("Existing appearance group icon manifest uses another source version.")
        previous_hashes = {entry["item_id"]: entry["sha256"] for entry in previous_manifest.get("groups", [])}
    if PAYLOAD_MANIFEST.is_file() and previous_manifest is not None:
        if PAYLOAD_MANIFEST.read_bytes() != REFERENCE_MANIFEST.read_bytes():
            raise ValueError("Appearance group icon payload and reference manifests disagree.")

    outputs = []
    for group_id, (sex, item_id) in GROUP_ITEMS.items():
        relative = "Assets/Official/CharacterCreation/Recovered/AppearanceGroupIcons/item_" + str(item_id) + ".png"
        data = icons[item_id]["data"]
        outputs.append({"group_id": group_id, "sex": sex, "item_id": item_id,
                        "localized_label": group_labels[group_id],
                        "condition_id": groups[group_id]["TryOnConditionId"],
                        "appearance_ids": groups[group_id]["AppearanceIdList"],
                        "appearance_skins": groups[group_id]["PreviewSkins"],
                        "path_id": icons[item_id]["path_id"],
                        "sprite_name": "item_" + str(item_id), "path": relative,
                        "bytes": len(data), "sha256": sha256(data),
                        "bundle": ITEM_BUNDLES[item_id]})

    manifest = {"manifest": "854_156383", "apk_sha256": APK_SHA256,
                "source_localization": localization_record,
                "source_bundles": bundle_records, "groups": outputs}
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    for entry in outputs:
        item_id = entry["item_id"]
        target = ROOT / "unity" / entry["path"]
        write_checked(target, icons[item_id]["data"], previous_hashes.get(item_id))
        meta_path = target.with_suffix(target.suffix + ".meta")
        meta = sprite_meta(entry["path"] + ".meta")
        if meta_path.exists() and meta_path.read_bytes() != meta:
            raise ValueError("Refusing to overwrite changed appearance icon metadata: " + str(meta_path))
        if not meta_path.exists():
            meta_path.write_bytes(meta)

    PAYLOAD.mkdir(parents=True, exist_ok=True)
    payload_folder_meta = PAYLOAD.with_suffix(".meta")
    expected_folder_meta = folder_meta("Assets/Official/CharacterCreation/Recovered/AppearanceGroupIcons")
    if payload_folder_meta.exists() and payload_folder_meta.read_bytes() != expected_folder_meta:
        raise ValueError("Refusing to overwrite changed appearance group icon folder metadata.")
    if not payload_folder_meta.exists():
        payload_folder_meta.write_bytes(expected_folder_meta)
    previous_manifest_hash = sha256(REFERENCE_MANIFEST.read_bytes()) if REFERENCE_MANIFEST.is_file() else None
    write_checked(PAYLOAD_MANIFEST, encoded, previous_manifest_hash)
    payload_manifest_meta = PAYLOAD_MANIFEST.with_suffix(PAYLOAD_MANIFEST.suffix + ".meta")
    expected_text_meta = text_meta(
        "Assets/Official/CharacterCreation/Recovered/AppearanceGroupIcons/source-manifest.json")
    if payload_manifest_meta.exists() and payload_manifest_meta.read_bytes() != expected_text_meta:
        raise ValueError("Refusing to overwrite changed appearance group icon manifest metadata.")
    if not payload_manifest_meta.exists():
        payload_manifest_meta.write_bytes(expected_text_meta)
    REFERENCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    if REFERENCE_MANIFEST.exists() and REFERENCE_MANIFEST.read_bytes() != encoded:
        if previous_manifest is None:
            raise ValueError("Refusing to overwrite an untracked appearance group icon manifest.")
    REFERENCE_MANIFEST.write_bytes(encoded)
    return {"group_count": len(outputs), "bundle_count": len(bundle_records),
            "manifest": REFERENCE_MANIFEST.relative_to(ROOT).as_posix()}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
