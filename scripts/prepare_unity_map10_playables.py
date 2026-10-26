"""Prepare the three official Map10 NPC playables as native Unity resources."""
from __future__ import annotations

import json
from pathlib import Path

from prepare_unity_map10_rendering import (
    ROOT,
    UNITY_ASSETS,
    _directory_meta,
    _write_identical,
    importer_meta,
    sha256,
)


PLAYABLES_PATH = ROOT / "assets/official/map-playables.json"
PLAYABLES_MANIFEST = ROOT / "assets/reference/official-map/playables-source-manifest.json"
PARTICLES_PATH = ROOT / "assets/official/map-particles.json"
PARTICLES_ROOT = ROOT / "assets/official/map-particles"
PARTICLES_MANIFEST = ROOT / "assets/reference/official-map/particles-source-manifest.json"
OUTPUT_MANIFEST = ROOT / "assets/reference/official-map/map10-playables-source-manifest.json"
PLAYABLE_TARGET = "Resources/Map10/Playables"
WINDOWS_SHADER_PATH = Path("unity/Assets/Game/Runtime/Presentation/World/OfficialMap10PlayableView.shader")
PLAYABLES = {
    "91ab22ab272abaf4eb63daaea8bb0d29": (
        "Assets/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/playable_FeiLe_appear.prefab",
        "317cab5f75107697871e.json",
    ),
    "5fd5bec6c4f91f54799a4b823888f49a": (
        "Assets/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/playable_FeiLe_disappear.prefab",
        "07209cc2278201440538.json",
    ),
    "9b248cde91299ee4287533b25b7dd468": (
        "Assets/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/playable_npc_disappear.prefab",
        "2be7d16f0ed565786b62.json",
    ),
}


def _curve(value):
    if not isinstance(value, dict) or not {"mode", "min", "max"}.issubset(value):
        raise ValueError("Map10 particle curve is malformed")
    result = {key: value[key] for key in ("mode", "min", "max")}
    for side in ("low", "high"):
        keys = value.get(side, [])
        result[side] = [
            {"time": key[0], "value": key[1], "inSlope": key[2], "outSlope": key[3]}
            for key in keys
        ]
    return result


def _gradient(value):
    if not isinstance(value, dict):
        raise ValueError("Map10 particle gradient is malformed")
    result = {"rgb": [], "alpha": []}
    for key in value.get("rgb", []):
        if len(key) != 4:
            raise ValueError("Map10 particle color key must contain time and RGB")
        result["rgb"].append({"time": key[0], "r": key[1], "g": key[2], "b": key[3]})
    for key in value.get("alpha", []):
        if len(key) != 2:
            raise ValueError("Map10 particle alpha key must contain time and alpha")
        result["alpha"].append({"time": key[0], "alpha": key[1]})
    return result


def _color(value):
    if not isinstance(value, list) or len(value) != 4:
        raise ValueError("Map10 particle color is malformed")
    return {"r": value[0], "g": value[1], "b": value[2], "a": value[3]}


def _vector3(value):
    return {"x": value[0], "y": value[1], "z": value[2]}


def _vector2(value):
    return {"x": value[0], "y": value[1]}


def _prepare_mesh(source):
    mesh = json.loads(source.read_text(encoding="utf-8"))
    return {
        "name": source.stem,
        "vertices": [_vector3(value) for value in mesh["vertices"]],
        "uv": [_vector2(value) for value in mesh["uv"]],
        "colors": [_color(value) for value in mesh.get("colors", [])],
        "triangles": mesh["triangles"],
    }


def _write_generated(destination, relative_path, content, previous_outputs):
    path = destination / relative_path
    if path.exists() and path.read_bytes() != content:
        previous_hash = previous_outputs.get(relative_path)
        if not previous_hash or sha256(path.read_bytes()) != previous_hash:
            raise ValueError("Refusing to overwrite an untracked Map10 playable asset: " + str(path))
        path.write_bytes(content)
        return
    _write_identical(path, content)


def _remove_stale_outputs(destination, previous_outputs, current_outputs):
    for relative_path, expected_hash in previous_outputs.items():
        if not relative_path.startswith(PLAYABLE_TARGET + "/") or relative_path in current_outputs:
            continue
        path = destination / relative_path
        if not path.exists():
            continue
        if sha256(path.read_bytes()) != expected_hash:
            raise ValueError("Refusing to remove a modified Map10 playable asset: " + str(path))
        path.unlink()
        meta_path = Path(str(path) + ".meta")
        expected_meta = importer_meta(relative_path, texture=path.suffix.lower() == ".png")
        if meta_path.exists() and meta_path.read_bytes() != expected_meta:
            raise ValueError("Refusing to remove a modified Map10 playable asset meta: " + str(meta_path))
        if meta_path.exists():
            meta_path.unlink()


def _prepare_system(source):
    record = dict(source)
    matrix = source.get("matrix")
    if not isinstance(matrix, list) or len(matrix) != 4 or any(len(row) != 4 for row in matrix):
        raise ValueError("Map10 particle system has no 4x4 transform: " + source.get("path", ""))
    record["matrix"] = [value for row in matrix for value in row]
    for key in ("delay", "life", "velocity", "size", "size_y", "rotation", "rotation_x", "rotation_y", "rate"):
        record[key] = _curve(source[key])
    record["color"] = {
        "mode": source["color"]["mode"],
        "min": _color(source["color"]["min"]),
        "max": _color(source["color"]["max"]),
        "low": _gradient(source["color"]["low"]),
        "high": _gradient(source["color"]["high"]),
    }
    modules = {}
    for name, module in source["modules"].items():
        converted = dict(module)
        for key, value in module.items():
            if isinstance(value, dict) and {"mode", "min", "max"}.issubset(value):
                converted[key] = _curve(value)
        if name == "ColorModule":
            converted["low"] = _gradient(module["low"])
            converted["high"] = _gradient(module["high"])
            converted["min"] = _color(module["min"])
            converted["max"] = _color(module["max"])
        modules[name] = converted
    record["modules"] = modules
    return record


def _verify_source_manifest():
    playable_hash = sha256(PLAYABLES_PATH.read_bytes())
    playable_manifest = json.loads(PLAYABLES_MANIFEST.read_text(encoding="utf-8"))
    if playable_manifest.get("manifest") != "854_156383" \
            or playable_manifest.get("outputs", {}).get("map-playables.json") != playable_hash:
        raise ValueError("Pinned official Map10 playables do not match their source manifest")

    particle_hash = sha256(PARTICLES_PATH.read_bytes())
    particle_manifest = json.loads(PARTICLES_MANIFEST.read_text(encoding="utf-8"))
    if particle_manifest.get("manifest") != "854_156383" \
            or particle_manifest.get("outputs", {}).get("map-particles.json") != particle_hash:
        raise ValueError("Pinned official Map10 particle catalog does not match its source manifest")
    return playable_hash, playable_manifest, particle_hash, particle_manifest


def build(destination: Path = UNITY_ASSETS, manifest_path: Path = OUTPUT_MANIFEST) -> dict:
    destination = Path(destination).resolve()
    manifest_path = Path(manifest_path).resolve()
    playable_hash, playable_manifest, particle_hash, particle_manifest = _verify_source_manifest()
    previous_outputs = {}
    if manifest_path.is_file():
        previous = json.loads(manifest_path.read_text(encoding="utf-8"))
        if previous.get("manifest") != "854_156383" or previous.get("mapClassId") != 10:
            raise ValueError("Refusing to replace a different Map10 playable manifest")
        previous_outputs = previous.get("outputs", {})
    playable_data = json.loads(PLAYABLES_PATH.read_text(encoding="utf-8"))
    particle_data = json.loads(PARTICLES_PATH.read_text(encoding="utf-8"))
    scenes = playable_data.get("scenes", {})
    prefab_particles = particle_data.get("prefabs", {})
    material_catalog = particle_data.get("materials", {})
    manifest_outputs = particle_manifest.get("outputs", {})
    source_resources = particle_manifest.get("resources", {})

    _directory_meta(destination, "Resources")
    _directory_meta(destination, "Resources/Map10")
    _directory_meta(destination, PLAYABLE_TARGET)
    _directory_meta(destination, PLAYABLE_TARGET + "/Textures")
    _directory_meta(destination, PLAYABLE_TARGET + "/Meshes")

    generated_resources = {}
    texture_records = {}
    mesh_records = {}
    material_sources = {}
    playable_records = []

    for guid, (prefab_path, particle_filename) in PLAYABLES.items():
        scene = scenes.get(guid)
        particle_uri = prefab_particles.get(prefab_path)
        if scene is None or not particle_uri or not particle_uri.startswith("res://assets/official/map-particles/"):
            raise ValueError("Official Map10 playable or particle payload is missing: " + guid)

        particle_relative = particle_uri.removeprefix("res://assets/official/")
        particle_source = ROOT / "assets/official" / particle_relative
        expected_particle_hash = manifest_outputs.get(particle_relative)
        if not particle_source.is_file() or expected_particle_hash != sha256(particle_source.read_bytes()):
            raise ValueError("Official Map10 particle systems differ from their source manifest: " + guid)
        source_systems = json.loads(particle_source.read_text(encoding="utf-8"))
        if not source_systems or any(item.get("unconverted_modules") for item in source_systems):
            raise ValueError("Map10 playable has no converted particle systems: " + guid)
        systems = [_prepare_system(item) for item in source_systems]

        used_material_ids = sorted({item["material_id"] for item in source_systems})
        materials = []
        for material_id in used_material_ids:
            material = material_catalog.get(material_id)
            if not isinstance(material, dict):
                raise ValueError("Map10 particle material is missing: " + material_id)
            textures = []
            for property_name, texture in sorted(material.get("textures", {}).items()):
                texture_uri = texture.get("file", "")
                if not texture_uri.startswith("res://assets/official/sprites/"):
                    raise ValueError("Map10 playable references a non-official texture: " + texture_uri)
                texture_source = ROOT / texture_uri.removeprefix("res://")
                texture_record = source_resources.get(texture_uri)
                if not texture_source.is_file() or not isinstance(texture_record, dict) \
                        or texture_record.get("sha256") != sha256(texture_source.read_bytes()):
                    raise ValueError("Map10 playable texture differs from its source manifest: " + texture_uri)
                texture_name = texture_source.stem
                texture_path = f"{PLAYABLE_TARGET}/Textures/{texture_name}.png"
                texture_bytes = texture_source.read_bytes()
                _write_generated(destination, texture_path, texture_bytes, previous_outputs)
                _write_generated(destination, texture_path + ".meta",
                                 importer_meta(texture_path, texture=True), previous_outputs)
                generated_resources[texture_path] = sha256(texture_bytes)
                texture_record_out = texture_records.setdefault(texture_uri, {
                    "sourceUri": texture_uri,
                    "sourceSha256": texture_record["sha256"],
                    "path": texture_path,
                    "sha256": sha256(texture_bytes),
                    "materialIds": [],
                })
                if material_id not in texture_record_out["materialIds"]:
                    texture_record_out["materialIds"].append(material_id)
                textures.append({
                    "property": property_name,
                    "resource": "Map10/Playables/Textures/" + texture_name,
                    "scale": texture.get("scale", [1, 1]),
                    "offset": texture.get("offset", [0, 0]),
                })

            material_source = material.get("source", {})
            material_sources[material_id] = {
                "materialId": material_id,
                "name": material.get("name", ""),
                **material_source,
            }
            materials.append({
                "id": material_id,
                "name": material.get("name", ""),
                "source": material_source,
                "floats": [{"name": name, "value": value}
                           for name, value in sorted(material.get("floats", {}).items())],
                "colors": [{"name": name, "value": _color(value)}
                           for name, value in sorted(material.get("colors", {}).items())],
                "textures": textures,
            })

        mesh_resources = {}
        mesh_payloads = {}
        for item in source_systems:
            for mesh_uri in item.get("meshes", []):
                if not mesh_uri.startswith("res://assets/official/map-particles/"):
                    raise ValueError("Map10 particle mesh is not from the official resource set")
                mesh_source = ROOT / mesh_uri.removeprefix("res://")
                mesh_relative = mesh_source.relative_to(ROOT / "assets/official").as_posix()
                expected_mesh_hash = manifest_outputs.get(mesh_relative)
                if not mesh_source.is_file() or expected_mesh_hash != sha256(mesh_source.read_bytes()):
                    raise ValueError("Map10 particle mesh differs from its source manifest: " + mesh_relative)
                mesh_name = mesh_source.stem
                mesh_path = f"{PLAYABLE_TARGET}/Meshes/{mesh_name}.json"
                mesh_bytes = mesh_source.read_bytes()
                _write_generated(destination, mesh_path, mesh_bytes, previous_outputs)
                _write_generated(destination, mesh_path + ".meta",
                                 importer_meta(mesh_path, texture=False), previous_outputs)
                generated_resources[mesh_path] = sha256(mesh_bytes)
                mesh_records[mesh_name] = {"sourceUri": mesh_uri, "sourceSha256": expected_mesh_hash,
                                           "path": mesh_path, "sha256": sha256(mesh_bytes)}
                mesh_resources[mesh_uri] = "Map10/Playables/Meshes/" + mesh_name
                mesh_payloads[mesh_name] = _prepare_mesh(mesh_source)

        for system in systems:
            system["meshResources"] = [mesh_resources[uri] for uri in system.get("meshes", [])]
        payload = {
            "format": 1,
            "mapClassId": 10,
            "assetGuid": guid,
            "prefabPath": prefab_path,
            "duration": scene["duration"],
            "systems": systems,
            "materials": materials,
            "meshes": list(mesh_payloads.values()),
        }
        payload_bytes = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        payload_path = f"{PLAYABLE_TARGET}/{guid}.json"
        _write_generated(destination, payload_path, payload_bytes, previous_outputs)
        _write_generated(destination, payload_path + ".meta",
                         importer_meta(payload_path, texture=False), previous_outputs)
        generated_resources[payload_path] = sha256(payload_bytes)
        playable_records.append({
            "assetGuid": guid,
            "prefabPath": prefab_path,
            "duration": scene["duration"],
            "particleSource": particle_relative,
            "particleSourceSha256": expected_particle_hash,
            "particleSystemCount": len(systems),
            "materialIds": used_material_ids,
            "meshUris": sorted(set(mesh_resources)),
            "payloadPath": payload_path,
            "payloadSha256": sha256(payload_bytes),
        })

    _remove_stale_outputs(destination, previous_outputs, generated_resources)
    record = {
        "manifest": "854_156383",
        "mapClassId": 10,
        "operation": "Official MyPlayable prefab timing/transform and particle source parameters; native Unity ParticleSystem modules; custom Windows shader rebuilt from converted official material values and textures; source shader is Android GLES bytecode and output is not pixel-equivalent",
        "inputs": {
            "map-playables.json": {
                "path": PLAYABLES_PATH.relative_to(ROOT).as_posix(),
                "sha256": playable_hash,
                "sourceManifest": PLAYABLES_MANIFEST.relative_to(ROOT).as_posix(),
                "sourceManifestSha256": sha256(PLAYABLES_MANIFEST.read_bytes()),
            },
            "map-particles.json": {
                "path": PARTICLES_PATH.relative_to(ROOT).as_posix(),
                "sha256": particle_hash,
                "sourceManifest": PARTICLES_MANIFEST.relative_to(ROOT).as_posix(),
                "sourceManifestSha256": sha256(PARTICLES_MANIFEST.read_bytes()),
            },
        },
        "playables": playable_records,
        "materialSources": [material_sources[key] for key in sorted(material_sources)],
        "textures": list(texture_records.values()),
        "meshes": list(mesh_records.values()),
        "windowsShader": {
            "path": WINDOWS_SHADER_PATH.as_posix(),
            "sha256": sha256((ROOT / WINDOWS_SHADER_PATH).read_bytes()),
            "sourceKind": "custom Windows reconstruction; original fixed APK contains GLES bytecode only",
        },
        "outputs": {path: digest for path, digest in sorted(generated_resources.items())},
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(record, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
                             encoding="utf-8")
    return record


if __name__ == "__main__":
    result = build()
    print("Prepared Map10 playable particle resources:",
          [(item["assetGuid"], item["particleSystemCount"]) for item in result["playables"]], flush=True)
