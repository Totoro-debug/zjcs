"""Prepare pinned Map10 terrain geometry and its original textures for Unity."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys

from map10_geometry import decode


ROOT = Path(__file__).resolve().parents[1]
UNITY_ASSETS = ROOT / "unity/Assets/Official/World/Map/10"
GEOMETRY_SOURCE = ROOT / "assets/official/regions/10.geom"
REGION_MANIFEST = ROOT / "assets/reference/official-map/regions-source-manifest.json"
MODEL_CATALOG = ROOT / "assets/official/map-models/catalog.json"
MODEL_MANIFEST = ROOT / "assets/reference/official-map/models-source-manifest.json"
SPINE_MANIFEST = ROOT / "assets/reference/official-map/spine-pages-source-manifest.json"
TEXTURE_ROOT = ROOT / "assets/official/sprites"
GEOMETRY_TARGET = "map10.geom.bytes"
TEXTURE_TARGET = "Resources/Map10/Textures"
MONSTER_TARGET = "Resources/Map10/Monsters"
MAP10_MONSTER_IDS = (3204, 3206, 3207, 3254, 3255, 3281, 3282, 3283, 3401, 3411, 3603)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_guid(relative_path: str) -> str:
    return hashlib.md5(("zjcs-official:" + relative_path).encode("utf-8")).hexdigest()


def importer_meta(relative_path: str, texture: bool) -> bytes:
    guid = asset_guid("Assets/Official/World/Map/10/" + relative_path)
    if texture:
        content = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  spriteMode: 0
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    else:
        content = f"""fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    return content.encode("utf-8")


def _write_identical(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if path.read_bytes() != content:
            if path.name.endswith(".meta"):
                existing = path.read_text(encoding="utf-8")
                expected = content.decode("utf-8")
                guid = next((line.split(":", 1)[1].strip() for line in existing.splitlines()
                             if line.startswith("guid:")), "")
                expected_importer = next((line for line in expected.splitlines()
                                          if line and not line[0].isspace() and line.endswith(":")), "")
                if len(guid) == 32 and all(character in "0123456789abcdef" for character in guid.lower()) \
                        and expected_importer in existing \
                        and ("folderAsset: yes" not in expected or "folderAsset: yes" in existing):
                    return
            raise ValueError(f"Refusing to overwrite a different Map10 asset: {path}")
        return
    path.write_bytes(content)


def _directory_meta(destination: Path, relative_path: str) -> None:
    guid = asset_guid("Assets/Official/World/Map/10/" + relative_path)
    content = (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")
    _write_identical(destination / (relative_path + ".meta"), content)


def _write_rendering_manifest(destination: Path, record: dict) -> None:
    path = destination / "rendering-source-manifest.json"
    encoded = (json.dumps(record, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    if path.exists():
        previous = json.loads(path.read_text(encoding="utf-8"))
        if previous == record:
            return
        geometry = previous.get("geometry", {})
        if previous.get("manifest") != record["manifest"] or previous.get("mapClassId") != 10 \
                or geometry.get("sha256") != record["geometry"]["sha256"] \
                or geometry.get("unityPath") != record["geometry"]["unityPath"]:
            raise ValueError("Refusing to replace a changed Map10 rendering source manifest")
        geometry_path = destination / geometry["unityPath"]
        if not geometry_path.is_file() or sha256(geometry_path.read_bytes()) != geometry["sha256"]:
            raise ValueError("Refusing to replace a Map10 manifest whose geometry payload changed")
        for item in previous.get("textures", []):
            texture = destination / item["path"]
            if not texture.is_file() or sha256(texture.read_bytes()) != item["sha256"]:
                raise ValueError("Refusing to replace a Map10 manifest whose texture payload changed")
    path.write_bytes(encoded)


def build(destination: Path = UNITY_ASSETS) -> dict:
    destination = Path(destination).resolve()
    source_bytes = GEOMETRY_SOURCE.read_bytes()
    source_manifest = json.loads(REGION_MANIFEST.read_text(encoding="utf-8"))
    expected_geometry = source_manifest["outputs"].get("10.geom")
    if expected_geometry != sha256(source_bytes):
        raise ValueError("Pinned Map10 geometry differs from its official conversion manifest")

    batches = decode(source_bytes)
    references = sorted({batch["metadata"]["texture"] for batch in batches})
    if not references:
        raise ValueError("Pinned Map10 geometry has no texture references")

    _write_identical(destination / GEOMETRY_TARGET, source_bytes)
    geometry_meta = importer_meta(GEOMETRY_TARGET, texture=False)
    _write_identical(destination / (GEOMETRY_TARGET + ".meta"), geometry_meta)

    texture_directory = destination / TEXTURE_TARGET
    _directory_meta(destination, "Resources")
    _directory_meta(destination, "Resources/Map10")
    _directory_meta(destination, TEXTURE_TARGET)

    textures = []
    for uri in references:
        if not uri.startswith("res://assets/official/sprites/"):
            raise ValueError(f"Unexpected Map10 texture URI: {uri}")
        source = ROOT / uri.removeprefix("res://")
        if not source.is_file():
            raise FileNotFoundError(f"Official Map10 texture is missing: {source}")
        name = Path(uri).name
        data = source.read_bytes()
        relative_path = f"{TEXTURE_TARGET}/{name}"
        target = texture_directory / name
        _write_identical(target, data)
        _write_identical(target.with_name(target.name + ".meta"), importer_meta(relative_path, texture=True))
        textures.append({"uri": uri, "path": relative_path, "sha256": sha256(data), "bytes": len(data)})

    model_catalog = json.loads(MODEL_CATALOG.read_text(encoding="utf-8"))
    model_manifest = json.loads(MODEL_MANIFEST.read_text(encoding="utf-8"))
    spine_manifest = json.loads(SPINE_MANIFEST.read_text(encoding="utf-8"))
    if model_manifest.get("manifest") != "854_156383" or spine_manifest.get("manifest") != "854_156383":
        raise ValueError("Map10 monster animation sources are not pinned to 854_156383")
    model_output_root = destination / MONSTER_TARGET
    _directory_meta(destination, MONSTER_TARGET)

    monsters = []
    for monster_id in MAP10_MONSTER_IDS:
        model_path = f"Appearance/Monster/{monster_id}/MonsterModel_{monster_id}"
        model = model_catalog.get("models", {}).get(model_path)
        if not isinstance(model, dict) or len(model.get("skeleton_instances", [])) != 1:
            raise ValueError(f"Official Map10 monster model is missing or ambiguous: {monster_id}")
        model_geometry = ROOT / "assets/official/map-models" / (model["id"] + ".geom")
        model_geometry_sha256 = sha256(model_geometry.read_bytes())
        if model_manifest.get("outputs", {}).get(model_geometry.name) != model_geometry_sha256:
            raise ValueError(f"Official Map10 monster geometry differs from its source manifest: {monster_id}")
        skeleton_instance = model["skeleton_instances"][0]
        skeleton_uri = model_catalog.get("skeletons", {}).get(skeleton_instance.get("id"))
        if not isinstance(skeleton_uri, str) or not skeleton_uri.startswith("res://assets/official/map-models/spines/"):
            raise ValueError(f"Official Map10 monster skeleton binding is invalid: {monster_id}")
        skeleton_source = ROOT / skeleton_uri.removeprefix("res://")
        skeleton_bytes = skeleton_source.read_bytes()
        skeleton_sha256 = sha256(skeleton_bytes)
        skeleton_relative = skeleton_source.relative_to(ROOT / "assets/official/map-models").as_posix()
        if spine_manifest.get("skeleton_outputs", {}).get(skeleton_uri) != skeleton_sha256:
            raise ValueError(f"Official Map10 monster animation differs from its source manifest: {monster_id}")

        skeleton = json.loads(skeleton_bytes)
        frames = skeleton.get("frames")
        if not isinstance(frames, list) or not frames or skeleton.get("fps", 0) <= 0:
            raise ValueError(f"Official Map10 monster has no sampled idle frames: {monster_id}")
        pages = {attachment.get("page") for frame in frames for attachment in frame}
        if len(pages) != 1:
            raise ValueError(f"Official Map10 monster idle uses unexpected atlas pages: {monster_id}")
        page = pages.pop()
        texture_uri = skeleton.get("textures", {}).get(page)
        texture_record = model_manifest.get("resources", {}).get(texture_uri)
        if not isinstance(texture_uri, str) or not isinstance(texture_record, dict):
            raise ValueError(f"Official Map10 monster texture is absent from its manifest: {monster_id}")
        texture_source = ROOT / texture_uri.removeprefix("res://")
        texture_bytes = texture_source.read_bytes()
        texture_sha256 = sha256(texture_bytes)
        if texture_record.get("sha256") != texture_sha256:
            raise ValueError(f"Official Map10 monster texture differs from its source manifest: {monster_id}")
        texture_name = texture_source.name
        texture_path = f"{TEXTURE_TARGET}/{texture_name}"
        texture_target = destination / texture_path
        _write_identical(texture_target, texture_bytes)
        _write_identical(texture_target.with_name(texture_target.name + ".meta"),
                         importer_meta(texture_path, texture=True))

        matrix = skeleton_instance.get("matrix")
        if not isinstance(matrix, list) or len(matrix) != 4 or any(len(row) != 4 for row in matrix):
            raise ValueError(f"Official Map10 monster transform is invalid: {monster_id}")
        payload = {
            "format": 1,
            "mapClassId": 10,
            "monsterId": monster_id,
            "modelPath": model_path,
            "animation": skeleton["animation"],
            "duration": skeleton["duration"],
            "fps": skeleton["fps"],
            "scaleX": skeleton["scale"] * matrix[0][0],
            "scaleY": skeleton["scale"] * matrix[1][1],
            "offsetX": matrix[0][3],
            "offsetY": matrix[1][3],
            "sortingLayer": skeleton_instance.get("sorting_layer", 0),
            "sortingOrder": skeleton_instance.get("sorting_order", 0),
            "textureResource": "Map10/Textures/" + texture_source.stem,
            "frames": [{"attachments": frame} for frame in frames],
        }
        payload_bytes = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        payload_name = f"{monster_id}.json"
        payload_path = f"{MONSTER_TARGET}/{payload_name}"
        _write_identical(model_output_root / payload_name, payload_bytes)
        _write_identical(model_output_root / (payload_name + ".meta"),
                         importer_meta(payload_path, texture=False))
        monsters.append({
            "monsterId": monster_id,
            "modelPath": model_path,
            "modelGeometryId": model["id"],
            "modelGeometrySha256": model_geometry_sha256,
            "skeletonSource": skeleton_relative,
            "skeletonSha256": skeleton_sha256,
            "payloadPath": payload_path,
            "payloadSha256": sha256(payload_bytes),
            "frameCount": len(frames),
            "fps": skeleton["fps"],
            "animation": skeleton["animation"],
            "textureUri": texture_uri,
            "texturePath": texture_path,
            "textureSha256": texture_sha256,
        })

    record = {
        "manifest": "854_156383",
        "mapClassId": 10,
        "geometry": {
            "source": "assets/official/regions/10.geom",
            "sha256": expected_geometry,
            "bytes": len(source_bytes),
            "unityPath": GEOMETRY_TARGET,
            "batches": len(batches),
            "vertices": sum(len(batch["positions"]) // 2 for batch in batches),
            "indices": sum(len(batch["indices"]) for batch in batches),
        },
        "textures": textures,
        "monsters": monsters,
    }
    _write_rendering_manifest(destination, record)
    _write_identical(destination / "rendering-source-manifest.json.meta",
                     importer_meta("rendering-source-manifest.json", texture=False))
    return record


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, default=UNITY_ASSETS)
    result = build(parser.parse_args().output_dir)
    print("Map10 geometry batches: " + str(result["geometry"]["batches"]))
    print("Map10 terrain textures: " + str(len(result["textures"])))
