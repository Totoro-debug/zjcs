"""Extract official Map10/Map11 monster portrait sprites and build their runtime catalog.

Sources are the pinned ``map4countries`` and ``mapsp`` no-atlas head-icon bundles. The importer
preserves the original sprite PNG and metadata; the catalog maps the official battle-rules Monster
IconId to the corresponding G_<id> sprite. Existing changed files are never overwritten.
"""
from __future__ import annotations

import hashlib
import io
import json
import re
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
try:
    import UnityPy
except ImportError as error:
    raise SystemExit("UnityPy==1.25.4 is required in .tools/character-art-python") from error

APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
BUNDLES = (
    "defaultpackage_assets_image_itemicon_gameheadicon_monster_map4countries_noatlas.bundle",
    "defaultpackage_assets_image_itemicon_gameheadicon_monster_mapsp_noatlas.bundle",
    "defaultpackage_assets_image_itemicon_gameheadicon_monster_maptherion_noatlas.bundle",
)
INDEX = ROOT / ".tools/source-packages/extracted/bundle-index.json"
RECEIPTS = ROOT / ".tools/source-packages/extracted/bundle-sources.json"
RULES = ROOT / "unity/Assets/Official/Combat/battle-rules.json"
MAP_DATA = ROOT / "unity/Assets/Official/World/Map/10/Resources/Map10/battle-runtime.json"
MAP11_DATA = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11/battle-runtime.json"
OUTPUT_DIR = ROOT / "unity/Assets/Official/Combat/MonsterPortraits"
SCRIPT = ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterPortraitCatalog.cs"
ASSET = ROOT / "unity/Assets/Game/Resources/Combat/OfficialBattleMonsterPortraitCatalog.asset"
MANIFEST = ROOT / "assets/reference/battle-hud/monster-portraits-source-manifest.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def md5(value: str) -> str:
    return hashlib.md5(value.encode("utf-8")).hexdigest()


def source_record(bundle: str, indexes: dict, receipts: dict) -> dict:
    path = ROOT / ".tools/source-packages/extracted" / bundle
    if not path.is_file():
        raise FileNotFoundError("Official monster portrait bundle is not extracted: " + bundle)
    record = receipts.get(bundle)
    expected_md5 = indexes.get(bundle)
    actual_md5 = hashlib.md5(path.read_bytes()).hexdigest()
    if not expected_md5 or actual_md5 != expected_md5 or not isinstance(record, dict):
        raise ValueError("Monster portrait bundle does not match the pinned index: " + bundle)
    if record.get("origin") == "apk_zip":
        if record.get("apk_sha256") != APK_SHA256:
            raise ValueError("Monster portrait bundle is not from the pinned APK: " + bundle)
    elif record.get("origin") == "official_cdn":
        # Only reuse a cached cloud payload whose bytes also match the pinned
        # APK manifest, after proving it is not physically packaged in the APK.
        with zipfile.ZipFile(ROOT / ".tools/source-packages/zjcs_M509085.apk") as apk:
            if any(expected_md5 in name or bundle in name for name in apk.namelist()):
                raise ValueError("Use the packaged APK monster portrait bundle first: " + bundle)
        if record.get("source_manifest") != "854_156383" or record.get("bundle_sha256") != sha(path.read_bytes()):
            raise ValueError("Cloud portrait bytes do not match the pinned source receipt: " + bundle)
    else:
        raise ValueError("Unknown monster portrait bundle source: " + bundle)
    if record.get("bundle_hash") != expected_md5:
        raise ValueError("Monster portrait bundle receipt differs from the pinned index: " + bundle)
    return {"bundle": bundle, "bundle_md5": actual_md5, "bundle_sha256": sha(path.read_bytes()),
            "origin": record["origin"], "apk_sha256": APK_SHA256, "apk_entry": record.get("path"),
            "source_manifest": "854_156383", "cached_manifest": record.get("manifest"), "url": record.get("url")}


def map10_icon_ids() -> list[int]:
    rules = json.loads(RULES.read_text(encoding="utf-8"))
    battles = [json.loads(path.read_text(encoding="utf-8")) for path in (MAP_DATA, MAP11_DATA)]
    monster_by_id = {int(row["Id"]): int(row.get("IconId", 0))
                     for row in rules["tables"]["monster"] if int(row.get("Id", 0)) > 0}
    ids = {monster_by_id[int(unit["MonsterId"])]
           for battle in battles
           for roster in battle["monsterRosters"]
           for wave in roster["Waves"]
           for unit in wave["Units"]
           if int(unit.get("MonsterId", 0)) in monster_by_id and monster_by_id[int(unit["MonsterId"])] > 0}
    return sorted(ids)


def extract_sprites(icon_ids: list[int], sources: list[dict]) -> dict[int, bytes]:
    wanted = {"G_" + str(icon_id): icon_id for icon_id in icon_ids}
    result = {}
    for source in sources:
        bundle_path = ROOT / ".tools/source-packages/extracted" / source["bundle"]
        environment = UnityPy.load(str(bundle_path))
        for obj in environment.objects:
            if obj.type.name != "Sprite":
                continue
            sprite = obj.read()
            name = sprite.m_Name or ""
            icon_id = wanted.get(name)
            if icon_id is None:
                continue
            if icon_id in result:
                continue
            stream = io.BytesIO()
            sprite.image.save(stream, format="PNG")
            result[icon_id] = stream.getvalue()
            source.setdefault("sprites", []).append({
                "iconId": icon_id, "object": name, "pathId": str(obj.path_id),
                "spriteObjectSha256": sha(obj.get_raw_data()),
            })
    missing = sorted(set(icon_ids) - set(result))
    if missing:
        raise ValueError("Official Map10/Map11 monster portraits are missing IconIds: " + str(missing))
    return result


def sprite_meta(path: Path, guid: str, sprite_id: str) -> bytes:
    text = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 9
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 21300000
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
  platformSettings: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    return text.encode("utf-8")


def asset_yaml(class_guid: str, bindings: list[dict]) -> bytes:
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: " + class_guid + ", type: 3}",
        "  m_Name: OfficialBattleMonsterPortraitCatalog", "  m_EditorClassIdentifier: ", "  bindings:",
    ]
    for item in bindings:
        lines.append("  - iconId: " + str(item["iconId"]))
        lines.append("    sprite: {fileID: 21300000, guid: " + item["guid"] + ", type: 3}")
    return ("\n".join(lines) + "\n").encode("utf-8")


def main() -> None:
    indexes = json.loads(INDEX.read_text(encoding="utf-8"))
    receipts = json.loads(RECEIPTS.read_text(encoding="utf-8"))
    source_records = [source_record(bundle, indexes, receipts) for bundle in BUNDLES]
    icon_ids = map10_icon_ids()
    images = extract_sprites(icon_ids, source_records)
    script_guid = next((line.split(":", 1)[1].strip() for line in (Path(str(SCRIPT) + ".meta")).read_text(encoding="utf-8").splitlines() if line.startswith("guid: ")), None)
    if not script_guid:
        raise ValueError("Portrait catalog script meta has no GUID")
    bindings = []
    image_records = []
    for icon_id in icon_ids:
        target = OUTPUT_DIR / f"G_{icon_id}.png"
        png = images[icon_id]
        guid = md5("zjcs-official:" + target.relative_to(ROOT).as_posix())
        sprite_id = md5("zjcs-sprite:" + target.relative_to(ROOT).as_posix())
        meta = Path(str(target) + ".meta")
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.is_file() and target.read_bytes() != png:
            raise ValueError("Refusing to overwrite changed portrait: " + str(target))
        if meta.is_file() and not meta.read_text(encoding="utf-8").startswith("fileFormatVersion: 2\nguid: " + guid):
            raise ValueError("Existing portrait meta has a different GUID: " + str(meta))
        target.write_bytes(png)
        meta.write_bytes(sprite_meta(target, guid, sprite_id))
        bindings.append({"iconId": icon_id, "guid": guid})
        image_records.append({"path": target.relative_to(ROOT).as_posix(), "sha256": sha(png),
                              "metaSha256": sha(meta.read_bytes()), "guid": guid})
    asset_bytes = asset_yaml(script_guid, bindings)
    asset_guid = md5("zjcs-official:" + ASSET.relative_to(ROOT).as_posix())
    asset_meta = Path(str(ASSET) + ".meta")
    ASSET.parent.mkdir(parents=True, exist_ok=True)
    if ASSET.is_file() and ASSET.read_bytes() != asset_bytes:
        previous = json.loads(MANIFEST.read_text(encoding="utf-8"))
        if sha(ASSET.read_bytes().replace(b"\r\n", b"\n")) != previous["catalog"]["sha256"]:
            raise ValueError("Refusing to overwrite changed portrait catalog asset: " + str(ASSET))
    ASSET.write_bytes(asset_bytes)
    if asset_meta.is_file() and not asset_meta.read_text(encoding="utf-8").startswith("fileFormatVersion: 2\nguid: " + asset_guid):
        raise ValueError("Portrait catalog asset meta has a different GUID")
    if not asset_meta.exists():
        asset_meta.write_text("fileFormatVersion: 2\nguid: " + asset_guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    manifest = {
        "format": 1, "manifest": "854_156383", "operation": "Extract official no-atlas monster head Sprite PNGs for Map10/Map11 Monster.IconId.",
        "mapInputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())}
                      for path in (MAP_DATA, MAP11_DATA)],
        "rulesSha256": sha(RULES.read_bytes()),
        "generatorSha256": sha(Path(__file__).read_bytes()),
        "bundles": source_records, "monsterIconIds": icon_ids, "images": image_records,
        "catalog": {"path": ASSET.relative_to(ROOT).as_posix(), "sha256": sha(asset_bytes), "guid": asset_guid},
    }
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    print("Map10/Map11 monster portraits:", len(bindings), "of", len(icon_ids), "IconIds")
    print("Catalog SHA256:", sha(asset_bytes))


if __name__ == "__main__":
    main()
