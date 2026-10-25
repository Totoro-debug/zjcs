"""Restore verified official battle-scene geometry and textures as Unity Resources."""
import argparse
import hashlib
import json
import os
import re
import struct
import tempfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
SCENE_SOURCE_ROOT = Path("assets/official/battle-scenes")
BATTLE_SCENES_SOURCE = Path("assets/official/battle-scenes.json")
BATTLE_SCENES_MANIFEST = Path("assets/reference/official-map/battle-scenes-source-manifest.json")
BATTLE_STAGES_SOURCE = Path("assets/official/battle-stages.json")
BATTLE_STAGES_MANIFEST = Path("assets/reference/official-map/battle-stages-source-manifest.json")
UNITY_SCENE_ROOT = Path("Assets/Official/Combat/Scenes")
RESOURCE_ROOT = UNITY_SCENE_ROOT / "Resources/BattleScenes"
CATALOG_PATH = RESOURCE_ROOT / "catalog.json"
MANIFEST_PATH = UNITY_SCENE_ROOT / "scene-assets-source-manifest.json"
KNOWN_CAMERA_POSITIONS = {
    "game_1000": [10.5, 9.75, -20.0],
    "game_1004": [10.5, 8.75, -20.0],
    "game_1008": [10.4990234375, 9.750007629394531, -20.0],
}
KNOWN_CAMERA_ORTHOGRAPHIC_SIZE = 5.0


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def stable_guid(relative_path):
    identity = f"{VERSION}:{relative_path.as_posix()}".encode("utf-8")
    return hashlib.sha256(identity).hexdigest()[:32]


def importer_meta(relative_path):
    guid = stable_guid(relative_path)
    if relative_path.suffix.lower() == ".png":
        importer = """TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQuality: 50
  textureCompression: 1
  compressionPolicy: 0
  crunchedCompression: 0
  allowsAlphaSplitting: 0
  androidETC2FallbackOverride: 0
  forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    else:
        importer = """TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    return f"fileFormatVersion: 2\nguid: {guid}\n{importer}"


def atomic_write(target, content, expected_hash=None, accepted_previous=None):
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        if expected_hash is not None and sha256(target) == expected_hash:
            return False
        if target.read_bytes() == content:
            return False
        if accepted_previous is not None and target.read_bytes() == accepted_previous:
            pass
        else:
            raise ValueError("Refusing to overwrite changed battle scene asset: " + str(target))
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-scene-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        if expected_hash is not None and sha256(temporary) != expected_hash:
            raise ValueError("Copied battle scene asset hash differs: " + str(target))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def atomic_copy(source, target, expected_hash):
    if target.exists():
        if sha256(target) != expected_hash:
            raise ValueError("Refusing to overwrite changed battle scene asset: " + str(target))
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-scene-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with source.open("rb") as input_stream, os.fdopen(handle, "wb") as output_stream:
            while block := input_stream.read(1024 * 1024):
                output_stream.write(block)
        if sha256(temporary) != expected_hash:
            raise ValueError("Copied battle scene asset hash differs: " + str(source))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def ensure_meta(target, relative_path):
    meta_path = target.with_name(target.name + ".meta")
    content = importer_meta(relative_path).encode("utf-8")
    if meta_path.exists() and meta_path.read_bytes() != content:
        raise ValueError("Refusing to overwrite changed battle scene metadata: " + str(meta_path))
    if not meta_path.exists():
        atomic_write(meta_path, content)


def ensure_folder_meta(target, relative_path):
    target.mkdir(parents=True, exist_ok=True)
    meta_path = target.with_name(target.name + ".meta")
    content = (
        f"fileFormatVersion: 2\nguid: {stable_guid(relative_path)}\n"
        "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n"
        "  assetBundleName: \n  assetBundleVariant: \n"
    ).encode("utf-8")
    if meta_path.exists() and meta_path.read_bytes() != content:
        current = meta_path.read_text(encoding="utf-8")
        if ("folderAsset: yes" not in current or "DefaultImporter:" not in current
                or len(re.findall(r"^guid: [0-9a-f]{32}$", current, re.MULTILINE)) != 1):
            raise ValueError("Refusing to replace non-folder metadata: " + str(meta_path))
        handle, temporary_name = tempfile.mkstemp(prefix=".battle-scene-meta-", dir=meta_path.parent)
        temporary = Path(temporary_name)
        try:
            with os.fdopen(handle, "wb") as stream:
                stream.write(content)
            temporary.replace(meta_path)
        finally:
            temporary.unlink(missing_ok=True)
    elif not meta_path.exists():
        atomic_write(meta_path, content)


def read_json_object(data, offset):
    if offset >= len(data) or data[offset] != ord("{"):
        raise ValueError("Battle geometry metadata does not start with an object.")
    start = offset
    depth = 0
    quoted = False
    escaped = False
    while offset < len(data):
        char = data[offset]
        if quoted:
            if escaped:
                escaped = False
            elif char == ord("\\"):
                escaped = True
            elif char == ord('"'):
                quoted = False
        elif char == ord('"'):
            quoted = True
        elif char == ord("{"):
            depth += 1
        elif char == ord("}"):
            depth -= 1
            if depth == 0:
                offset += 1
                return json.loads(data[start:offset]), offset
        offset += 1
    raise ValueError("Battle geometry metadata is truncated.")


def read_u32(data, offset):
    if offset < 0 or offset + 4 > len(data):
        raise ValueError("Battle geometry payload is truncated.")
    return struct.unpack_from("<I", data, offset)[0], offset + 4


def geometry_textures(data):
    if len(data) < 16 or data[:8] != b"ZJMAP001":
        raise ValueError("Official battle geometry header is invalid.")
    batch_count = struct.unpack_from("<I", data, 8)[0]
    first_metadata_size = struct.unpack_from("<I", data, 12)[0]
    if batch_count <= 0 or batch_count > 100000:
        raise ValueError("Official battle geometry batch count is invalid.")
    offset = 16
    textures = set()
    for index in range(batch_count):
        metadata_start = offset
        metadata, offset = read_json_object(data, offset)
        if index == 0 and offset - metadata_start != first_metadata_size:
            raise ValueError("Official battle geometry metadata length is invalid.")
        texture = metadata.get("texture")
        if not isinstance(texture, str) or not texture.startswith("res://assets/official/sprites/"):
            raise ValueError("Battle geometry has an unsupported texture reference.")
        textures.add(texture)
        vertices, offset = read_u32(data, offset)
        if vertices <= 0 or vertices > 1000000:
            raise ValueError("Battle geometry vertex count is invalid.")
        offset += vertices * 32
        if offset > len(data):
            raise ValueError("Battle geometry vertex payload is truncated.")
        indices, offset = read_u32(data, offset)
        if indices <= 0 or indices > 3000000:
            raise ValueError("Battle geometry index count is invalid.")
        offset += indices * 4
        if offset > len(data):
            raise ValueError("Battle geometry index payload is truncated.")
        if index + 1 < batch_count:
            if offset + 5 > len(data) or data[offset + 4] != ord("{"):
                raise ValueError("Battle geometry batch separator is invalid.")
            offset += 4
    if offset != len(data):
        raise ValueError("Official battle geometry has trailing or unparsed data.")
    return batch_count, sorted(textures)


def safe_source(root, relative):
    root = root.resolve()
    path = root.joinpath(*PurePosixPath(relative).parts)
    if path.is_symlink() or not path.resolve().is_relative_to(root):
        raise ValueError("Battle scene source escapes its root: " + str(relative))
    return path


def scene_metadata(source_root, game_id):
    source_root = Path(source_root).resolve()
    inventory = load_source_inventory(source_root)
    scene_name = inventory["game_scene_names"].get(int(game_id))
    if scene_name is None:
        raise ValueError("Official battle stage has no scene name for GameId " + str(game_id))
    return _scene_metadata(source_root, int(game_id), scene_name, inventory)


def load_source_inventory(source_root):
    source_root = Path(source_root).resolve()
    scene_manifest_path = safe_source(source_root, BATTLE_SCENES_MANIFEST)
    stage_manifest_path = safe_source(source_root, BATTLE_STAGES_MANIFEST)
    scene_manifest = json.loads(scene_manifest_path.read_text(encoding="utf-8"))
    stage_manifest = json.loads(stage_manifest_path.read_text(encoding="utf-8"))
    scene_path = safe_source(source_root, BATTLE_SCENES_SOURCE)
    stage_path = safe_source(source_root, BATTLE_STAGES_SOURCE)
    if (scene_manifest.get("manifest") != VERSION
            or scene_manifest.get("outputs", {}).get("battle-scenes.json") != sha256(scene_path)
            or stage_manifest.get("manifest") != VERSION
            or stage_manifest.get("output", {}).get("sha256") != sha256(stage_path)):
        raise ValueError("Battle scene or stage index is not pinned to manifest " + VERSION)

    scenes = json.loads(scene_path.read_text(encoding="utf-8")).get("scenes")
    stages = json.loads(stage_path.read_text(encoding="utf-8")).get("stages")
    if not isinstance(scenes, dict) or len(scenes) != 229 or not isinstance(stages, dict):
        raise ValueError("Official battle scene index is incomplete.")
    game_scene_names = {}
    for raw_game_id, stage in stages.items():
        try:
            game_id = int(raw_game_id)
            scene_name = stage["FightComponent"]["SceneName"]
        except (KeyError, TypeError, ValueError) as error:
            raise ValueError("Official fight-stage scene binding is malformed: " + str(raw_game_id)) from error
        if not isinstance(scene_name, str) or scene_name not in scenes:
            raise ValueError("Official fight-stage scene is missing: " + str(scene_name))
        if game_id in game_scene_names:
            raise ValueError("Official fight-stage GameId is duplicated: " + str(game_id))
        game_scene_names[game_id] = scene_name
    if len(game_scene_names) != 256 or set(game_scene_names.values()) != set(scenes):
        raise ValueError("Official fight-stage scene bindings do not cover the pinned scene index.")
    return {
        "scene_manifest": scene_manifest,
        "stage_manifest": stage_manifest,
        "scene_catalog": scenes,
        "game_scene_names": game_scene_names,
    }


def _verified_output(source_root, relative, scene_manifest):
    path = safe_source(source_root, relative)
    asset_root = source_root / "assets/official"
    key = path.relative_to(asset_root).as_posix()
    expected = scene_manifest.get("outputs", {}).get(key)
    if not isinstance(expected, str) or sha256(path) != expected:
        raise ValueError("Battle scene output differs from its pinned source manifest: " + key)
    return path


def _scene_metadata(source_root, game_id, scene_name, inventory):
    source_root = Path(source_root).resolve()
    scene_index = inventory["scene_catalog"][scene_name]
    scene_manifest = inventory["scene_manifest"]
    base_uri = scene_index.get("base")
    if not isinstance(base_uri, str):
        raise ValueError("Official battle scene has no base layer: " + scene_name)

    texture_uris = set()
    geometry_sources = []
    layer_records = {"day": [], "night": []}
    all_layer_metadata = []

    def load_layer(metadata_uri):
        metadata_relative = PurePosixPath(metadata_uri.removeprefix("res://"))
        metadata_path = _verified_output(source_root, metadata_relative, scene_manifest)
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        geometry_uri = metadata.get("geometry")
        if not isinstance(geometry_uri, str):
            raise ValueError("Battle scene layer has no geometry binding: " + str(metadata_path))
        geometry_relative = PurePosixPath(geometry_uri.removeprefix("res://"))
        geometry_path = _verified_output(source_root, geometry_relative, scene_manifest)
        geometry_data = geometry_path.read_bytes()
        all_layer_metadata.append(metadata)
        if len(geometry_data) == 12 and geometry_data[:8] == b"ZJMAP001" \
                and struct.unpack_from("<I", geometry_data, 8)[0] == 0:
            return None
        batch_count, layer_textures = geometry_textures(geometry_data)
        texture_uris.update(layer_textures)
        resource = "BattleScenes/" + geometry_path.stem
        geometry_sources.append({"path": geometry_path, "data": geometry_data, "resource": resource})
        return {"geometryResource": resource, "batchCount": batch_count}

    base_metadata_path = _verified_output(
        source_root, PurePosixPath(base_uri.removeprefix("res://")), scene_manifest)
    base_metadata = json.loads(base_metadata_path.read_text(encoding="utf-8"))
    base_layer = load_layer(base_uri)
    if base_layer is None:
        raise ValueError("Official battle scene base layer is empty: " + scene_name)

    for time_name in ("day", "night"):
        uris = scene_index.get(time_name, [])
        if not isinstance(uris, list):
            raise ValueError("Official battle scene time layers are malformed: " + scene_name)
        for uri in uris:
            if not isinstance(uri, str):
                raise ValueError("Official battle scene time-layer URI is malformed: " + scene_name)
            layer = load_layer(uri)
            if layer is not None:
                layer_records[time_name].append(layer)

    root_matrix = scene_index.get("root_matrix")
    if not isinstance(root_matrix, list) or len(root_matrix) != 4 \
            or any(not isinstance(row, list) or len(row) != 4 for row in root_matrix):
        raise ValueError("Official battle scene root transform is malformed: " + scene_name)
    expected_matrix = [[1.0, 0.0, 0.0, root_matrix[0][3]],
                       [0.0, 1.0, 0.0, root_matrix[1][3]],
                       [0.0, 0.0, 1.0, root_matrix[2][3]],
                       [0.0, 0.0, 0.0, 1.0]]
    if any(abs(float(root_matrix[row][column]) - expected_matrix[row][column]) > 0.0001
           for row in range(4) for column in range(4)):
        raise ValueError("Official battle scene uses an unsupported root matrix: " + scene_name)

    camera_position = KNOWN_CAMERA_POSITIONS.get(scene_name)
    record = {
        "gameId": game_id,
        "sceneName": scene_name,
        "geometryResource": base_layer["geometryResource"],
        "batchCount": base_layer["batchCount"],
        "rootOffset": [root_matrix[0][3], root_matrix[1][3], root_matrix[2][3]],
        "cameraPosition": camera_position,
        "cameraOrthographicSize": KNOWN_CAMERA_ORTHOGRAPHIC_SIZE if camera_position else 0.0,
        "defaultTime": int(scene_index.get("default_time", 1)),
        "dayLayers": layer_records["day"],
        "nightLayers": layer_records["night"],
        "sourceParticleCount": len(base_metadata.get("particles", [])),
        "sourceEnvironmentParticleCount": sum(len(item.get("particles", []))
                                               for item in all_layer_metadata[1:]),
        "sourceSkeletonCount": sum(len(item.get("skeletons", [])) for item in all_layer_metadata),
        "textures": [],
    }
    if record["defaultTime"] not in (0, 1):
        raise ValueError("Official battle scene has an unknown default time type: " + scene_name)
    return {
        "game_id": game_id,
        "scene_id": PurePosixPath(base_uri.removeprefix("res://")).stem,
        "scene_name": scene_name,
        "metadata_path": base_metadata_path,
        "metadata": base_metadata,
        "source": base_metadata.get("source", {}),
        "geometry_sources": geometry_sources,
        "texture_uris": sorted(texture_uris),
        "record": record,
    }


def catalog_bytes(scenes):
    catalog = {"manifest": VERSION, "scenes": [scene["record"] for scene in scenes]}
    return (json.dumps(catalog, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def _verified_previous_outputs(unity_root):
    catalog_path = unity_root.joinpath(*CATALOG_PATH.parts)
    manifest_path = unity_root.joinpath(*MANIFEST_PATH.parts)
    if not catalog_path.exists() and not manifest_path.exists():
        return None, None
    if not catalog_path.is_file() or not manifest_path.is_file():
        raise ValueError("Existing generated battle-scene catalog and manifest are incomplete.")
    previous_manifest = manifest_path.read_bytes()
    parsed = json.loads(previous_manifest.decode("utf-8"))
    if parsed.get("manifest") != VERSION or not isinstance(parsed.get("assets"), list):
        raise ValueError("Existing generated battle-scene manifest is not a supported migration source.")
    catalog_expected = None
    for entry in parsed["assets"]:
        relative = PurePosixPath(entry.get("path", ""))
        if not relative.parts or relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Existing battle-scene manifest contains an unsafe asset path.")
        target = unity_root.joinpath(*relative.parts)
        if target.is_symlink() or not target.resolve().is_relative_to(unity_root):
            raise ValueError("Existing battle-scene output escapes the Unity project.")
        if not target.is_file() or sha256(target) != entry.get("sha256"):
            raise ValueError("Existing generated battle-scene asset differs from its manifest: " + str(relative))
        if relative.as_posix() == CATALOG_PATH.as_posix():
            catalog_expected = entry["sha256"]
    if catalog_expected is None or sha256(catalog_path) != catalog_expected:
        raise ValueError("Existing battle-scene catalog is not covered by its source manifest.")
    return catalog_path.read_bytes(), previous_manifest


def restore(source_root=ROOT, unity_root=ROOT / "unity", game_ids=None):
    source_root = Path(source_root).resolve()
    unity_root = Path(unity_root).resolve()
    previous_catalog, previous_manifest = _verified_previous_outputs(unity_root)
    inventory = load_source_inventory(source_root)
    selected_ids = sorted(inventory["game_scene_names"] if game_ids is None
                          else {int(value) for value in game_ids})
    missing_ids = set(selected_ids) - set(inventory["game_scene_names"])
    if missing_ids:
        raise ValueError("Unknown official battle scene GameIds: " + ", ".join(map(str, sorted(missing_ids))))
    scene_by_name = {}
    for game_id in selected_ids:
        scene_name = inventory["game_scene_names"][game_id]
        if scene_name not in scene_by_name:
            scene_by_name[scene_name] = _scene_metadata(source_root, game_id, scene_name, inventory)
    game_scene_pairs = [(game_id, inventory["game_scene_names"][game_id]) for game_id in selected_ids]
    for directory in (UNITY_SCENE_ROOT, UNITY_SCENE_ROOT / "Resources",
                      RESOURCE_ROOT, RESOURCE_ROOT / "Textures"):
        ensure_folder_meta(unity_root.joinpath(*directory.parts), directory)

    entries = {}
    for scene in scene_by_name.values():
        for geometry in scene["geometry_sources"]:
            geometry_relative = RESOURCE_ROOT / (PurePosixPath(geometry["resource"]).name + ".bytes")
            geometry_target = unity_root.joinpath(*geometry_relative.parts)
            geometry_hash = hashlib.sha256(geometry["data"]).hexdigest()
            atomic_copy(geometry["path"], geometry_target, geometry_hash)
            ensure_meta(geometry_target, geometry_relative)
            entries[geometry_relative.as_posix()] = geometry_hash

        for uri in scene["texture_uris"]:
            relative = PurePosixPath(uri.removeprefix("res://"))
            source_texture = safe_source(source_root, relative)
            expected = inventory["scene_manifest"].get("resources", {}).get(uri, {}).get("sha256")
            resource_relative = RESOURCE_ROOT / "Textures" / relative.name
            texture_target = unity_root.joinpath(*resource_relative.parts)
            texture_hash = sha256(source_texture)
            if not isinstance(expected, str) or texture_hash != expected:
                raise ValueError("Official battle-scene texture differs from its source manifest: " + uri)
            previous_hash = entries.get(resource_relative.as_posix())
            if previous_hash is not None and previous_hash != texture_hash:
                raise ValueError("Official battle scenes bind conflicting textures to one resource name: " + uri)
            atomic_copy(source_texture, texture_target, texture_hash)
            ensure_meta(texture_target, resource_relative)
            entries[resource_relative.as_posix()] = texture_hash
        scene["record"]["textures"] = [{
                "uri": uri,
                "resource": "BattleScenes/Textures/" + PurePosixPath(uri.removeprefix("res://")).stem,
            } for uri in scene["texture_uris"]]

    records = []
    for game_id, scene_name in game_scene_pairs:
        template = scene_by_name[scene_name]["record"]
        record = dict(template)
        record["gameId"] = game_id
        records.append(record)

    catalog_target = unity_root.joinpath(*CATALOG_PATH.parts)
    catalog_content = catalog_bytes([{"record": record} for record in records])
    catalog_hash = hashlib.sha256(catalog_content).hexdigest()
    atomic_write(catalog_target, catalog_content, catalog_hash, accepted_previous=previous_catalog)
    ensure_meta(catalog_target, CATALOG_PATH)
    entries[CATALOG_PATH.as_posix()] = catalog_hash

    manifest_scenes = [{
        "game_ids": [game_id for game_id, scene_name in game_scene_pairs if scene_name == name],
        "scene": scene["scene_id"],
        "scene_name": name,
        "source_metadata_path": (SCENE_SOURCE_ROOT / (scene["scene_id"] + ".json")).as_posix(),
        "source_metadata_sha256": sha256(scene["metadata_path"]),
        "source_geometry_path": (SCENE_SOURCE_ROOT / (scene["scene_id"] + ".geom")).as_posix(),
        "source_geometry_sha256": hashlib.sha256(
            next(value["data"] for value in scene["geometry_sources"]
                 if value["resource"] == scene["record"]["geometryResource"])).hexdigest(),
        "original_bundle": scene["source"],
        "batch_count": scene["record"]["batchCount"],
        "texture_count": len(scene["texture_uris"]),
        "source_particle_count": scene["record"]["sourceParticleCount"],
        "source_environment_particle_count": scene["record"]["sourceEnvironmentParticleCount"],
        "source_skeleton_count": scene["record"]["sourceSkeletonCount"],
        "day_layers": scene["record"]["dayLayers"],
        "night_layers": scene["record"]["nightLayers"],
    } for name, scene in sorted(scene_by_name.items())]
    manifest = {
        "manifest": VERSION,
        "scenes": manifest_scenes,
        "assets": [{"path": path, "sha256": digest}
                   for path, digest in sorted(entries.items())],
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    manifest_target = unity_root.joinpath(*MANIFEST_PATH.parts)
    manifest_hash = hashlib.sha256(manifest_bytes).hexdigest()
    atomic_write(manifest_target, manifest_bytes, manifest_hash, accepted_previous=previous_manifest)
    ensure_meta(manifest_target, MANIFEST_PATH)
    return {
        "scenes": [{"game_id": game_id, "scene_name": scene_name,
                    "batch_count": scene_by_name[scene_name]["record"]["batchCount"],
                    "texture_count": len(scene_by_name[scene_name]["texture_uris"])}
                   for game_id, scene_name in game_scene_pairs],
        "unique_scenes": len(scene_by_name),
        "assets": len(entries),
        "manifest": MANIFEST_PATH.as_posix(),
        "manifest_sha256": manifest_hash,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=ROOT)
    parser.add_argument("--unity-root", type=Path, default=ROOT / "unity")
    args = parser.parse_args()
    print(json.dumps(restore(args.source_root, args.unity_root), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
