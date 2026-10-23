"""Prepare pinned Map10 terrain geometry and its original textures for Unity."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
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
WATER_DATA = ROOT / "assets/official/map-water.json"
WATER_MANIFEST = ROOT / "assets/reference/official-map/water-source-manifest.json"
REGION_MANIFEST = ROOT / "assets/reference/official-map/regions-source-manifest.json"
MATERIALS_MANIFEST = ROOT / "assets/reference/official-map/materials-10-source-manifest.json"
BUTTERFLY_DATA = ROOT / "assets/official/regions/10-butterflies.json"
WATER_MASK = ROOT / "assets/official/map-water-masks/10.png"
WATER_SHADER = ROOT / "unity/Assets/Game/Resources/World/Map10Water.shader"
BUTTERFLY_SHADER = ROOT / "unity/Assets/Game/Resources/World/Map10Butterfly.shader"
TEXTURE_ROOT = ROOT / "assets/official/sprites"
GEOMETRY_TARGET = "map10.geom.bytes"
TEXTURE_TARGET = "Resources/Map10/Textures"
MONSTER_TARGET = "Resources/Map10/Monsters"
ENVIRONMENT_TARGET = "Resources/Map10/Environment"
ENVIRONMENT_TEXTURE_TARGET = ENVIRONMENT_TARGET + "/Textures"
MAP10_MONSTER_IDS = (3204, 3206, 3207, 3254, 3255, 3281, 3282, 3283, 3401, 3411, 3603)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_guid(relative_path: str) -> str:
    return hashlib.md5(("zjcs-official:" + relative_path).encode("utf-8")).hexdigest()


def importer_meta(relative_path: str, texture: bool, *, texture_type: int = 0, srgb: bool = True) -> bytes:
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
  sRGBTexture: {int(srgb)}
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
  textureType: {texture_type}
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


def _prepare_environment(destination: Path) -> dict:
    water_bytes = WATER_DATA.read_bytes()
    butterfly_bytes = BUTTERFLY_DATA.read_bytes()
    materials_manifest_bytes = MATERIALS_MANIFEST.read_bytes()
    water_manifest = json.loads(WATER_MANIFEST.read_text(encoding="utf-8"))
    region_manifest = json.loads(REGION_MANIFEST.read_text(encoding="utf-8"))
    materials_manifest = json.loads(materials_manifest_bytes.decode("utf-8"))
    manifests = (water_manifest, region_manifest, materials_manifest)
    if any(value.get("manifest") != "854_156383" for value in manifests):
        raise ValueError("Map10 environment sources are not pinned to 854_156383")
    if water_manifest.get("outputs", {}).get("map-water.json") != sha256(water_bytes):
        raise ValueError("Pinned Map10 water configuration differs from its source manifest")
    expected = materials_manifest.get("outputs", {})
    if expected.get("assets/official/map-water.json") != sha256(water_bytes):
        raise ValueError("Pinned Map10 water configuration differs from its material manifest")
    if expected.get("assets/official/regions/10-butterflies.json") != sha256(butterfly_bytes):
        raise ValueError("Pinned Map10 butterfly configuration differs from its material manifest")

    def objects(value):
        if isinstance(value, dict):
            yield value
            for child in value.values():
                yield from objects(child)
        elif isinstance(value, list):
            for child in value:
                yield from objects(child)

    material_records = list(objects(materials_manifest))
    water_materials = [record for record in material_records
                       if record.get("name") == "Water2"
                       and record.get("shader") == "Universal Render Pipeline/2D/Sprite-Lit-CustomWater2"]
    butterfly_materials = [record for record in material_records
                           if record.get("name") == "Effect_Butterfly_Material"
                           and record.get("shader") == "Effect/Noise_Mask_Pro"]
    if not water_materials or not butterfly_materials:
        raise ValueError("Pinned Map10 environment materials are missing from their source manifest")
    water_material = water_materials[0]
    butterfly_material = butterfly_materials[0]
    if "USE_MAINTEX" not in butterfly_material.get("keywords", []):
        raise ValueError("Official Map10 butterfly main-texture variant is not enabled")
    if not WATER_SHADER.is_file() or not BUTTERFLY_SHADER.is_file():
        raise FileNotFoundError("Map10 Windows environment shader reconstruction is missing")

    water = json.loads(water_bytes).get("10")
    butterflies = json.loads(butterfly_bytes)
    if not isinstance(water, dict) or len(water.get("renderers", [])) != 4:
        raise ValueError("Pinned Map10 water configuration is incomplete")
    if len(butterflies.get("instances", [])) != 15:
        raise ValueError("Pinned Map10 butterfly configuration is incomplete")

    output_directory = destination / ENVIRONMENT_TARGET
    _directory_meta(destination, ENVIRONMENT_TARGET)
    _directory_meta(destination, ENVIRONMENT_TEXTURE_TARGET)
    water_payload = (json.dumps(water, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    water_path = f"{ENVIRONMENT_TARGET}/map10-water.json"
    butterfly_path = f"{ENVIRONMENT_TARGET}/10-butterflies.json"
    _write_identical(output_directory / "map10-water.json", water_payload)
    _write_identical(output_directory / "map10-water.json.meta", importer_meta("map10-water.json", False))
    _write_identical(output_directory / "10-butterflies.json", butterfly_bytes)
    _write_identical(output_directory / "10-butterflies.json.meta", importer_meta("10-butterflies.json", False))

    texture_records = []
    texture_bindings = {}
    for renderer in water["renderers"]:
        for prop, uri in renderer.get("textures", {}).items():
            texture_bindings.setdefault(uri, set()).add("water:" + prop)
    for skeleton in butterflies.get("skeletons", {}).values():
        for uri in skeleton.get("textures", {}).values():
            texture_bindings.setdefault(uri, set()).add("butterfly")

    for uri, bindings in sorted(texture_bindings.items()):
        if not uri.startswith("res://assets/official/sprites/"):
            raise ValueError(f"Unexpected Map10 environment texture URI: {uri}")
        source = ROOT / uri.removeprefix("res://")
        source_manifest = water_manifest if any(x.startswith("water:") for x in bindings) else region_manifest
        source_record = source_manifest.get("resources", {}).get(uri)
        if not source.is_file() or not isinstance(source_record, dict):
            raise FileNotFoundError(f"Official Map10 environment texture is missing: {source}")
        data = source.read_bytes()
        if source_record.get("sha256") != sha256(data):
            raise ValueError(f"Official Map10 environment texture differs from its source manifest: {uri}")
        texture_path = f"{ENVIRONMENT_TEXTURE_TARGET}/{source.name}"
        resource_path = Path(texture_path.removeprefix("Resources/")).with_suffix("").as_posix()
        texture_type = 1 if "water:_BumpMap" in bindings else 0
        _write_identical(destination / texture_path, data)
        _write_identical(destination / (texture_path + ".meta"), importer_meta(
            texture_path, True, texture_type=texture_type, srgb=texture_type == 0))
        texture_records.append({
            "uri": uri, "path": texture_path, "resource": resource_path,
            "sha256": sha256(data), "bytes": len(data), "bindings": sorted(bindings),
            "textureType": texture_type, "sRGB": texture_type == 0,
        })

    def texture_resource(uri: str) -> str:
        if not uri.startswith("res://assets/official/sprites/"):
            raise ValueError(f"Unexpected Map10 environment texture URI: {uri}")
        return "Map10/Environment/Textures/" + Path(uri).name.removesuffix(".png")

    def water_texture_binding(prop: str, uri: str) -> dict:
        record = water_material.get("textures", {}).get(prop)
        if not isinstance(record, dict):
            raise ValueError("Official Map10 Water2 texture property is missing: " + prop)
        if Path(record.get("planned_path", "")).name != Path(uri).name:
            raise ValueError("Official Map10 Water2 texture differs from its material binding: " + prop)
        scale = record.get("scale", [1, 1])
        offset = record.get("offset", [0, 0])
        if len(scale) != 2 or len(offset) != 2:
            raise ValueError("Official Map10 Water2 texture transform is invalid: " + prop)
        return {"property": prop, "resource": texture_resource(uri), "scale": scale, "offset": offset}

    mask_binding = water.get("native_materials", {}).get("mask", {})
    if mask_binding != water_manifest.get("map10_native_materials", {}).get("mask", {}):
        raise ValueError("Pinned Map10 water mask binding differs from its source manifest")
    mask_uri = mask_binding.get("texture", "")
    if not mask_uri.startswith("res://assets/official/map-water-masks/"):
        raise ValueError("Pinned Map10 water mask URI is invalid")
    mask_source = ROOT / mask_uri.removeprefix("res://")
    if mask_source != WATER_MASK:
        raise ValueError("Pinned Map10 water mask resolved to an unexpected source file")
    if not mask_source.is_file():
        raise FileNotFoundError(f"Official Map10 water mask is missing: {mask_source}")
    mask_bytes = mask_source.read_bytes()
    mask_path = f"{ENVIRONMENT_TARGET}/map10-water-mask.png"
    mask_resource = Path(mask_path.removeprefix("Resources/")).with_suffix("").as_posix()
    _write_identical(destination / mask_path, mask_bytes)
    _write_identical(destination / (mask_path + ".meta"), importer_meta(mask_path, True, srgb=False))

    water_renderers = []
    for renderer in water["renderers"]:
        vertices = renderer.get("vertices") or []
        indices = renderer.get("indices") or []
        if not vertices or not indices or any(len(vertex) != 2 for vertex in vertices):
            raise ValueError("Official Map10 water mesh is malformed: " + str(renderer.get("name")))
        water_renderers.append({
            "name": renderer["name"],
            "material": renderer.get("material", ""),
            "vertices": [coordinate for vertex in vertices for coordinate in vertex],
            "indices": indices,
            "textures": [
                water_texture_binding(prop, uri)
                for prop, uri in sorted(renderer.get("textures", {}).items())
            ],
            "colors": [
                {"property": prop, "value": value}
                for prop, value in sorted(renderer.get("colors", {}).items())
            ],
            "parameters": [
                {"property": prop, "value": value}
                for prop, value in sorted(renderer.get("parameters", {}).items())
            ],
        })
    water_runtime = {
        "format": 1,
        "mapClassId": 10,
        "sortingOrder": 18001,
        "maskResource": mask_resource,
        "maskRect": mask_binding.get("rect"),
        "renderers": water_renderers,
    }
    water_runtime_bytes = (json.dumps(water_runtime, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    water_runtime_path = f"{ENVIRONMENT_TARGET}/map10-water-runtime.json"
    _write_identical(destination / water_runtime_path, water_runtime_bytes)
    _write_identical(destination / (water_runtime_path + ".meta"), importer_meta(water_runtime_path, False))

    animations = {}
    animation_records = []
    for key, skeleton in sorted(butterflies.get("skeletons", {}).items()):
        name = skeleton.get("animation")
        frames = skeleton.get("frames")
        fps = skeleton.get("fps")
        if not isinstance(name, str) or not isinstance(frames, list) or not frames or not isinstance(fps, (int, float)) or fps <= 0:
            raise ValueError("Official Map10 butterfly Spine animation is incomplete: " + str(key))
        pages = {attachment.get("page") for frame in frames for attachment in frame}
        if len(pages) != 1:
            raise ValueError("Official Map10 butterfly Spine uses an ambiguous atlas page: " + name)
        page = pages.pop()
        texture_uri = skeleton.get("textures", {}).get(page)
        if texture_uri not in texture_bindings or "butterfly" not in texture_bindings[texture_uri]:
            raise ValueError("Official Map10 butterfly atlas is missing from its source binding: " + name)
        payload = {
            "format": 1,
            "mapClassId": 10,
            "monsterId": 0,
            "modelPath": "Map/Map_10/EnvEffect/Common/MapEffect_10/" + name,
            "animation": name,
            "duration": skeleton.get("duration", 0),
            "fps": fps,
            "scaleX": skeleton.get("scale", 1),
            "scaleY": skeleton.get("scale", 1),
            "offsetX": 0,
            "offsetY": 0,
            "sortingLayer": 18,
            "sortingOrder": 3,
            "textureResource": texture_resource(texture_uri),
            "frames": [{"attachments": frame} for frame in frames],
        }
        payload_bytes = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        payload_path = f"{ENVIRONMENT_TARGET}/butterfly-{name}.json"
        _write_identical(destination / payload_path, payload_bytes)
        _write_identical(destination / (payload_path + ".meta"), importer_meta(payload_path, False))
        resource = Path(payload_path.removeprefix("Resources/")).with_suffix("").as_posix()
        animations[name] = resource
        animation_records.append({
            "animation": name,
            "sourceKey": key,
            "payloadPath": payload_path,
            "payloadSha256": sha256(payload_bytes),
            "frameCount": len(frames),
            "fps": fps,
            "textureUri": texture_uri,
            "textureResource": payload["textureResource"],
        })

    def parent_transform(matrix: list) -> dict:
        if not isinstance(matrix, list) or len(matrix) != 4 or any(not isinstance(row, list) or len(row) != 4 for row in matrix):
            raise ValueError("Official Map10 butterfly parent transform is malformed")
        scale_x = (matrix[0][0] ** 2 + matrix[1][0] ** 2) ** 0.5
        determinant = matrix[0][0] * matrix[1][1] - matrix[0][1] * matrix[1][0]
        if scale_x <= 0:
            raise ValueError("Official Map10 butterfly parent transform has no scale")
        scale_y = determinant / scale_x
        return {
            "x": matrix[0][3], "y": matrix[1][3], "z": matrix[2][3],
            "rotationZ": math.degrees(math.atan2(matrix[1][0], matrix[0][0])),
            "scaleX": scale_x, "scaleY": scale_y, "scaleZ": matrix[2][2],
        }

    runtime_instances = []
    for instance in butterflies["instances"]:
        animation = instance.get("id", "").rsplit(":", 1)[-1]
        motion = instance.get("motion", {})
        if animation not in animations or not isinstance(motion, dict) or not motion.get("loop"):
            raise ValueError("Official Map10 butterfly instance binding is invalid: " + str(instance.get("path")))
        tracks = []
        for track in motion.get("tracks", []):
            keys = track.get("keys")
            if not isinstance(keys, list) or not keys or any(not isinstance(key, list) or len(key) != 4 for key in keys):
                raise ValueError("Official Map10 butterfly motion curve is malformed: " + str(track.get("property")))
            tracks.append({
                "property": track.get("property", ""),
                "keys": [{"time": key[0], "value": key[1], "inTangent": key[2], "outTangent": key[3]}
                         for key in keys],
            })
        effect = instance.get("effect", {})
        tint = effect.get("tint", [1, 1, 1, 1])
        if len(tint) != 4:
            raise ValueError("Official Map10 butterfly tint is malformed: " + str(instance.get("path")))
        runtime_instances.append({
            "path": instance.get("path", ""),
            "animationResource": animations[animation],
            "parentTransform": parent_transform(motion.get("parent_matrix")),
            "motion": {"duration": motion.get("duration", 0), "loop": True, "tracks": tracks},
            "position": {
                "x": motion.get("position", [0, 0])[0],
                "y": motion.get("position", [0, 0])[1],
                "z": motion.get("position", [0, 0, 0])[2] if len(motion.get("position", [])) > 2 else 0,
            },
            "scale": {
                "x": motion.get("scale", [1, 1])[0],
                "y": motion.get("scale", [1, 1])[1],
                "z": motion.get("scale", [1, 1, 1])[2] if len(motion.get("scale", [])) > 2 else 1,
            },
            "tint": tint,
            "intensity": effect.get("intensity", 1),
            "alphaIntensity": effect.get("alpha_intensity", 0),
            "clipHeight": effect.get("clip_height", -1000000),
            "sortingOrder": int(instance.get("sorting_layer", 0)) * 1000 + int(instance.get("sorting_order", 0)),
        })
    butterflies_runtime = {"format": 1, "mapClassId": 10, "instances": runtime_instances}
    butterflies_runtime_bytes = (json.dumps(butterflies_runtime, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    butterflies_runtime_path = f"{ENVIRONMENT_TARGET}/map10-butterflies-runtime.json"
    _write_identical(destination / butterflies_runtime_path, butterflies_runtime_bytes)
    _write_identical(destination / (butterflies_runtime_path + ".meta"), importer_meta(butterflies_runtime_path, False))

    return {
        "water": {
            "source": "assets/official/map-water.json#10", "sourceSha256": sha256(water_bytes),
            "payloadPath": water_path, "payloadSha256": sha256(water_payload),
            "rendererCount": len(water["renderers"]),
            "runtimePayloadPath": water_runtime_path,
            "runtimePayloadSha256": sha256(water_runtime_bytes),
            "material": {
                "sourceManifest": "assets/reference/official-map/materials-10-source-manifest.json",
                "sourceManifestSha256": sha256(materials_manifest_bytes),
                "name": water_material["name"], "shader": water_material["shader"],
                "source": water_material.get("source"), "shaderSource": water_material.get("shader_source"),
                "reconstructionPath": WATER_SHADER.relative_to(ROOT).as_posix(),
                "reconstructionSha256": sha256(WATER_SHADER.read_bytes()),
            },
            "mask": {
                "source": mask_uri, "path": mask_path, "resource": mask_resource,
                "sha256": sha256(mask_bytes), "rect": mask_binding.get("rect"),
                "pixelsPerUnit": mask_binding.get("pixels_per_unit"),
                "draws": mask_binding.get("draws"), "sRGB": False,
            },
            "textureBindings": {
                uri: sorted(bindings) for uri, bindings in sorted(texture_bindings.items())
                if any(value.startswith("water:") for value in bindings)
            },
        },
        "butterflies": {
            "source": "assets/official/regions/10-butterflies.json",
            "sourceSha256": sha256(butterfly_bytes), "payloadPath": butterfly_path,
            "payloadSha256": sha256(butterfly_bytes), "instanceCount": len(butterflies["instances"]),
            "runtimePayloadPath": butterflies_runtime_path,
            "runtimePayloadSha256": sha256(butterflies_runtime_bytes),
            "animationCount": len(animations), "animations": animation_records,
            "material": {
                "sourceManifest": "assets/reference/official-map/materials-10-source-manifest.json",
                "sourceManifestSha256": sha256(materials_manifest_bytes),
                "name": butterfly_material["name"], "shader": butterfly_material["shader"],
                "source": butterfly_material.get("source"), "shaderSource": butterfly_material.get("shader_source"),
                "keywords": butterfly_material.get("keywords", []),
                "colorIntensity": butterfly_material.get("floats", {}).get("_ColorIntensity"),
                "mainColor": butterfly_material.get("colors", {}).get("_MainColor"),
                "reconstructionPath": BUTTERFLY_SHADER.relative_to(ROOT).as_posix(),
                "reconstructionSha256": sha256(BUTTERFLY_SHADER.read_bytes()),
            },
            "textureBindings": {
                uri: sorted(bindings) for uri, bindings in sorted(texture_bindings.items())
                if "butterfly" in bindings
            },
        },
        "textures": texture_records,
    }


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

    environment = _prepare_environment(destination)

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
        "environment": environment,
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
