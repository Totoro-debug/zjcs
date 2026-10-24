"""Prepare the verified battle-presentation effects used by the opening record.

Effect JSON, sprites, Wwise clips, and source hashes are checked against their
pinned manifests before the native runtime payload is written.
"""
from __future__ import annotations

import gzip
import hashlib
import json
import math
import re
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
RECORD = ROOT / "unity/Assets/StreamingAssets/Opening/opening-record.bytes"
EFFECTS = ROOT / "assets/official/battle-presentation/effects.json"
EFFECT_ROOT = ROOT / "assets/official/battle-presentation/effects"
MANIFEST = ROOT / "assets/reference/official-map/battle-effects-source-manifest.json"
BATTLE_AUDIO = ROOT / "assets/official/battle-audio/catalog.json"
BATTLE_AUDIO_MANIFEST = ROOT / "assets/reference/battle-audio/source-manifest.json"
SPRITES = ROOT / "assets/official/sprites"
TARGET = ROOT / "unity/Assets/StreamingAssets/Opening/skill-effects.json"
TEXTURES = ROOT / "unity/Assets/Official/Opening/SkillEffects/Resources/OpeningSkillEffects/Textures"
AUDIO = ROOT / "unity/Assets/Official/Opening/SkillEffects/Resources/OpeningSkillEffects/Audio"
MESHES = ROOT / "unity/Assets/StreamingAssets/Opening/Meshes"
REFERENCE = ROOT / "assets/reference/character-creation/opening-skill-effects-source-manifest.json"

TEXTURE_META = """fileFormatVersion: 2
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
  textureType: 0
  textureShape: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    wrapU: 0
    wrapV: 0
    wrapW: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

AUDIO_META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def guid(path: str) -> str:
    return hashlib.md5(("zjcs-official:" + path).encode("utf-8")).hexdigest()


def ensure_folder_meta(directory: Path) -> None:
    relative = directory.relative_to(ROOT / "unity").as_posix()
    meta = directory.with_name(directory.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\nguid: " + guid(relative) + "\nfolderAsset: yes\nDefaultImporter:\n"
        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8")


def ensure_payload_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\nguid: " + guid(relative) + "\nDefaultImporter:\n"
        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8")


def main() -> int:
    for directory in (TEXTURES, TEXTURES.parent, TEXTURES.parent.parent, TEXTURES.parent.parent.parent,
                      AUDIO, MESHES):
        directory.mkdir(parents=True, exist_ok=True)
        ensure_folder_meta(directory)
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    effects_bytes = EFFECTS.read_bytes()
    expected = manifest["outputs"].get("battle-presentation/effects.json")
    if manifest.get("manifest") != VERSION or not expected or sha(effects_bytes) != expected:
        raise ValueError("Battle effect catalog differs from its pinned source manifest")
    catalog = json.loads(effects_bytes.decode("utf-8"))
    audio_manifest = json.loads(BATTLE_AUDIO_MANIFEST.read_text(encoding="utf-8"))
    audio_catalog_bytes = BATTLE_AUDIO.read_bytes()
    audio_catalog_hash = audio_manifest.get("outputs", {}).get("res://assets/official/battle-audio/catalog.json")
    if audio_manifest.get("manifest") != VERSION or not audio_catalog_hash \
            or sha(audio_catalog_bytes) != audio_catalog_hash:
        raise ValueError("Battle audio catalog differs from its pinned source manifest")
    audio_catalog = json.loads(audio_catalog_bytes.decode("utf-8"))
    record = json.loads(gzip.decompress(RECORD.read_bytes()).decode("utf-8", "replace"))
    skill_classes = set()
    damage_classes = set()
    status_classes = set()
    for round_data in record.get("RoundMsgList", []):
        for step in round_data.get("StepList", []):
            if step.get("MsgType") != "GAME_ENTITY_ADD": continue
            entity = step.get("Data", {}).get("Entity", {})
            class_id = entity.get("ClassId")
            if class_id is None: continue
            class_id = int(class_id)
            component_types = {c.get("$type", "").split(".")[-1].split(",")[0]
                               for c in entity.get("ComponentList", [])}
            if "FightSkillComponent" in component_types: skill_classes.add(class_id)
            if "FightDamageComponent" in component_types: damage_classes.add(class_id)
            if "FightStatusComponent" in component_types: status_classes.add(class_id)
    if 11150 not in skill_classes:
        raise ValueError("Opening record does not contain cast entity ClassId 11150")
    selected = {}
    for effect_type, class_ids in (("cast", skill_classes), ("damage", damage_classes),
                                   ("status", status_classes)):
        for class_id in sorted(class_ids):
            key = effect_type + ":" + str(class_id)
            if key in catalog: selected[key] = catalog[key]
    if "cast:11150" not in selected:
        raise ValueError("Pinned battle effect catalog has no cast:11150 entry")

    resources = manifest["resources"]
    texture_uri_by_source = {
        (str(value.get("bundle", "")), str(value.get("bundle_hash", "")), str(value.get("path_id", ""))): uri
        for uri, value in resources.items()
    }
    output_textures = {}
    output_sounds = {}
    output_meshes = {}
    output_effects = []
    effect_inputs = {}
    apk = None
    particle_objects = {}
    material_objects = {}
    material_shader_data = {}
    restored_pinned_payloads = []

    def load_apk():
        nonlocal apk
        if apk is None:
            from official_apk import Apk
            apk = Apk()
        return apk

    def load_pinned_effect_source(manifest_path: str, source: Path) -> tuple[str, bytes]:
        expected_hash = manifest["outputs"].get(manifest_path)
        source_bytes = source.read_bytes()
        conversion = manifest.get("current_output_conversion") or {}
        if conversion.get("manifest") != "871_156598":
            if expected_hash and sha(source_bytes) == expected_hash:
                return expected_hash, source_bytes
            raise ValueError("Effect payload hash differs from its pinned source manifest: " + manifest_path)
        conversion_source = next((item for item in conversion.get("sources", [])
                                  if item.get("path") == "assets/reference/official-map/current-material-source-manifest.json"),
                                 None)
        if conversion_source is None:
            raise ValueError("Current material conversion has no verified source receipt: " + manifest_path)
        conversion_manifest_path = ROOT / PurePosixPath(conversion_source["path"])
        conversion_manifest_bytes = conversion_manifest_path.read_bytes()
        if sha(conversion_manifest_bytes) != conversion_source.get("sha256"):
            raise ValueError("Current material conversion receipt changed: " + manifest_path)
        conversion_manifest = json.loads(conversion_manifest_bytes.decode("utf-8"))
        if conversion_manifest.get("manifest") != conversion.get("manifest"):
            raise ValueError("Current material conversion version differs from its receipt: " + manifest_path)

        output_path = "assets/official/" + manifest_path
        converted_output = next((item for item in conversion_manifest.get("outputs", [])
                                 if item.get("path") == output_path), None)
        if converted_output is None:
            if expected_hash and sha(source_bytes) == expected_hash:
                return expected_hash, source_bytes
            raise ValueError("Effect payload hash differs from its pinned source manifest: " + manifest_path)
        if converted_output.get("sha256") != expected_hash or sha(source_bytes) != expected_hash:
            raise ValueError("Current effect payload is not identified by its verified conversion: " + manifest_path)
        previous = converted_output.get("previous") or {}
        previous_hash = converted_output.get("previous_sha256", "")
        if previous.get("path") != output_path or previous.get("sha256") != previous_hash:
            raise ValueError("Current effect payload has no pinned predecessor record: " + manifest_path)
        preserved_path = previous.get("preserved", "")
        if not preserved_path:
            raise ValueError("Current effect payload has no preserved pinned predecessor: " + manifest_path)
        preserved = ROOT / PurePosixPath(preserved_path)
        pinned_bytes = preserved.read_bytes()
        if not previous_hash or sha(pinned_bytes) != previous_hash:
            raise ValueError("Preserved effect payload differs from its pinned predecessor hash: " + manifest_path)
        restored_pinned_payloads.append({"path": manifest_path, "sha256": previous_hash,
                                         "preserved": preserved_path})
        return previous_hash, pinned_bytes

    def register_texture(texture_uri: str) -> str:
        if not texture_uri:
            return ""
        resource = resources.get(texture_uri)
        if resource is None:
            raise ValueError("Effect texture has no verified resource record: " + texture_uri)
        if texture_uri == "res://assets/official/map-particles/white.tres":
            source = ROOT / PurePosixPath(texture_uri.removeprefix("res://"))
            if sha(source.read_bytes()) != resource.get("sha256"):
                raise ValueError("Official white texture source hash differs from manifest")
            output_textures[PurePosixPath(texture_uri).name] = {
                "path": "builtin/white", "builtin": "white", "sha256": resource["sha256"],
                "source": texture_uri, "bundle": resource["bundle"],
                "bundle_hash": resource.get("bundle_hash", ""),
                "path_id": resource.get("path_id", ""),
                "object": resource.get("object", ""),
            }
            return "white"
        sprite_name = PurePosixPath(texture_uri).name
        sprite_source = SPRITES / sprite_name
        payload = sprite_source.read_bytes()
        if sha(payload) != resource.get("sha256"):
            raise ValueError("Effect texture hash differs from source manifest: " + sprite_name)
        relative_target = "Assets/Official/Opening/SkillEffects/Resources/OpeningSkillEffects/Textures/" + sprite_name
        target = TEXTURES / sprite_name
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or sha(target.read_bytes()) != resource["sha256"]:
            target.write_bytes(payload)
        meta = target.with_name(target.name + ".meta")
        if not meta.exists():
            meta.write_text(TEXTURE_META.format(guid=guid(relative_target)), encoding="utf-8")
        output_textures[sprite_name] = {
            "path": relative_target, "sha256": resource["sha256"], "source": texture_uri,
            "bundle": resource["bundle"], "bundle_hash": resource.get("bundle_hash", ""),
            "path_id": resource.get("path_id", ""), "object": resource.get("object", ""),
        }
        return sprite_source.stem

    def register_mesh_file(filename: str, payload: bytes, source: dict) -> str:
        target = MESHES / filename
        target.parent.mkdir(parents=True, exist_ok=True)
        payload_hash = sha(payload)
        if not target.exists() or sha(target.read_bytes()) != payload_hash:
            target.write_bytes(payload)
        ensure_payload_meta(target)
        output_meshes[filename] = {
            "path": "Opening/Meshes/" + filename,
            "sha256": payload_hash,
            "source": source,
        }
        return filename

    def register_mesh_uri(mesh_uri: str) -> str:
        prefix = "res://assets/official/map-particles/"
        if not mesh_uri.startswith(prefix) or not mesh_uri.endswith(".mesh.json"):
            raise ValueError("Unexpected opening particle mesh URI: " + mesh_uri)
        source = ROOT / PurePosixPath(mesh_uri.removeprefix("res://"))
        payload = source.read_bytes()
        return register_mesh_file(source.name, payload, {"uri": mesh_uri})

    def load_particle_object(source: dict):
        bundle = source.get("bundle", "")
        path_id = int(source.get("path_id", 0))
        cache_key = (bundle, path_id)
        particle_object = particle_objects.get(cache_key)
        if particle_object is None:
            particle_object = next((item for item in load_apk().objects(bundle)
                                    if item.type.name == "ParticleSystem" and item.path_id == path_id), None)
            if particle_object is None:
                raise ValueError("Opening particle source is missing: " + bundle + "/" + str(path_id))
            particle_objects[cache_key] = particle_object
        actual_bundle_hash = load_apk().index.get(bundle)
        if not actual_bundle_hash or source.get("bundle_hash") != actual_bundle_hash:
            raise ValueError("Opening particle bundle hash differs from the pinned APK: " + bundle)
        return particle_object

    def require_close(actual: float, expected: float, label: str) -> None:
        if not math.isclose(float(actual), float(expected), rel_tol=1e-6, abs_tol=1e-6):
            raise ValueError("Opening particle value differs from the pinned APK: " + label)

    def validate_curve(value: dict, source_curve, label: str) -> None:
        mode = int(value.get("mode", -1))
        if mode != int(source_curve.minMaxState):
            raise ValueError("Opening particle curve mode differs from the pinned APK: " + label)
        if mode == 0:
            active_value = float(source_curve.scalar)
            if not any(math.isclose(float(value.get(key, 0)), active_value,
                                    rel_tol=1e-6, abs_tol=1e-6) for key in ("min", "max")):
                raise ValueError("Opening particle constant differs from the pinned APK: " + label)
            value["min"] = active_value
        elif mode == 1:
            active_value = float(source_curve.scalar)
            if not any(math.isclose(float(value.get(key, 0)), active_value,
                                    rel_tol=1e-6, abs_tol=1e-6) for key in ("min", "max")):
                raise ValueError("Opening particle curve scale differs from the pinned APK: " + label)
            value["max"] = active_value
        elif mode == 2:
            require_close(value.get("max", 0), source_curve.scalar, label + "/curve-scale")
        else:
            require_close(value.get("min", 0), source_curve.minScalar, label + "/min")
            require_close(value.get("max", 0), source_curve.scalar, label + "/max")
        for key, source_key in (("low", "minCurve"), ("high", "maxCurve")):
            actual_keys = value.get(key, [])
            source_keys = getattr(getattr(source_curve, source_key), "m_Curve", []) or []
            if len(actual_keys) != len(source_keys):
                raise ValueError("Opening particle curve keys differ from the pinned APK: " + label + "/" + key)
            for index, (actual_key, source_keyframe) in enumerate(zip(actual_keys, source_keys)):
                expected_key = (source_keyframe.time, source_keyframe.value,
                                source_keyframe.inSlope, source_keyframe.outSlope)
                if len(actual_key) < 4:
                    raise ValueError("Opening particle curve key is incomplete: " + label + "/" + key)
                for axis, expected in enumerate(expected_key):
                    require_close(actual_key[axis], expected, label + "/" + key + "/" + str(index))

    def validate_particle_source(particle: dict) -> None:
        source = particle.get("source") or {}
        particle_object = load_particle_object(source)
        system = particle_object.read()
        require_close(particle.get("duration", 0), system.lengthInSec, source.get("path_id", "") + "/duration")
        if bool(particle.get("loop")) != bool(system.looping):
            raise ValueError("Opening particle loop flag differs from the pinned APK: " + source.get("path_id", ""))
        if bool(particle.get("prewarm")) != bool(system.prewarm):
            raise ValueError("Opening particle prewarm flag differs from the pinned APK: " + source.get("path_id", ""))
        if bool(particle.get("play_on_awake")) != bool(system.playOnAwake):
            raise ValueError("Opening particle play-on-awake differs from the pinned APK: " + source.get("path_id", ""))
        if int(particle.get("scaling_mode", -1)) != int(system.scalingMode):
            raise ValueError("Opening particle scaling mode differs from the pinned APK: " + source.get("path_id", ""))
        require_close(particle.get("speed", 0), system.simulationSpeed, source.get("path_id", "") + "/speed")
        initial = system.InitialModule
        for key, source_curve in (("delay", system.startDelay), ("life", initial.startLifetime),
                                  ("velocity", initial.startSpeed), ("size", initial.startSize),
                                  ("rotation", initial.startRotation),
                                  ("rate", system.EmissionModule.rateOverTime)):
            validate_curve(particle[key], source_curve, source.get("path_id", "") + "/" + key)
        color = particle["color"]
        source_color = initial.startColor
        if int(color.get("mode", -1)) != int(source_color.minMaxState):
            raise ValueError("Opening particle color mode differs from the pinned APK: " + source.get("path_id", ""))
        for field, source_color_value in (("min", source_color.minColor), ("max", source_color.maxColor)):
            actual_color = color.get(field, [])
            expected_color = [source_color_value.r, source_color_value.g,
                              source_color_value.b, source_color_value.a]
            if len(actual_color) < 4:
                raise ValueError("Opening particle color is incomplete: " + source.get("path_id", ""))
            for actual, expected in zip(actual_color, expected_color):
                require_close(actual, expected, source.get("path_id", "") + "/color/" + field)
        game_object = system.m_GameObject.deref().read()
        renderer = next((component.component.deref().read() for component in game_object.m_Component
                         if component.component.deref().type.name == "ParticleSystemRenderer"), None)
        material = particle.get("material") or {}
        material_source = material.get("source") or {}
        if renderer is None or not any(pointer.path_id == int(material_source.get("path_id", 0))
                                       for pointer in renderer.m_Materials or []):
            raise ValueError("Opening particle material does not match its pinned renderer: "
                             + source.get("path_id", ""))

    def register_shape_mesh(particle: dict) -> str:
        shape = particle.get("shape") or {}
        pointer = shape.get("m_Mesh") or {}
        path_id = int(pointer.get("m_PathID", 0))
        if not path_id:
            return ""
        source = particle.get("source") or {}
        bundle = source.get("bundle", "")
        particle_path_id = int(source.get("path_id", 0))
        particle_object = load_particle_object(source)
        particle_system = particle_object.read()
        mesh_pointer = particle_system.ShapeModule.m_Mesh
        if mesh_pointer is None or mesh_pointer.path_id != path_id:
            raise ValueError("Opening particle shape mesh reference differs from its source object")
        mesh_object = mesh_pointer.deref().read()
        from UnityPy.helpers.MeshHelper import MeshHandler
        handler = MeshHandler(mesh_object)
        handler.process()
        geometry = {
            "vertices": [[float(value) for value in row] for row in (handler.m_Vertices or [])],
            "uv": [[float(value) for value in row] for row in (handler.m_UV0 or [])],
            "colors": [[float(value) for value in row] for row in (handler.m_Colors or [])],
            "triangles": [[[int(index) for index in face] for face in faces]
                          for faces in handler.get_triangles()],
        }
        payload = (json.dumps(geometry, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        filename = "opening-shape-" + sha(payload)[:24] + ".mesh.json"
        mesh_source = {
            "bundle": bundle,
            "bundle_hash": source.get("bundle_hash", ""),
            "particle_path_id": str(particle_path_id),
            "mesh_path_id": str(mesh_pointer.path_id),
            "mesh_name": mesh_object.m_Name,
        }
        return register_mesh_file(filename, payload, mesh_source)

    def curve_data(curve: dict) -> dict:
        def keys(name: str) -> list:
            source = curve.get(name) or {}
            return [[frame.get("time", 0), frame.get("value", 0), frame.get("inSlope", 0),
                     frame.get("outSlope", 0)] for frame in source.get("m_Curve", [])]

        return {"mode": int(curve.get("minMaxState", 0)), "min": float(curve.get("minScalar", 0)),
                "max": float(curve.get("scalar", 0)), "low": keys("minCurve"), "high": keys("maxCurve")}

    def gradient_data(gradient: dict) -> dict:
        colors = []
        alphas = []
        for index in range(int(gradient.get("m_NumColorKeys", 0))):
            key = gradient["key" + str(index)]
            colors.append([float(gradient.get("ctime" + str(index), 0)) / 65535.0,
                           float(key["r"]), float(key["g"]), float(key["b"])])
        for index in range(int(gradient.get("m_NumAlphaKeys", 0))):
            key = gradient["key" + str(index)]
            alphas.append([float(gradient.get("atime" + str(index), 0)) / 65535.0, float(key["a"])])
        return {"mode": int(gradient.get("m_Mode", 0)), "rgb": colors, "alpha": alphas}

    def color_data(color: dict) -> dict:
        def rgba(value: dict) -> list:
            return [float(value.get(channel, 0)) for channel in ("r", "g", "b", "a")]

        return {"mode": int(color.get("minMaxState", 0)), "min": rgba(color.get("minColor") or {}),
                "max": rgba(color.get("maxColor") or {}),
                "low": gradient_data(color["minGradient"]) if color.get("minGradient") else {},
                "high": gradient_data(color["maxGradient"]) if color.get("maxGradient") else {}}

    def vector_data(value: dict) -> dict:
        return {axis: float(value.get(axis, 0)) for axis in ("x", "y", "z")}

    def texture_uri_for(pointer) -> str:
        if pointer is None or pointer.path_id == 0:
            return ""
        source_object = pointer.deref()
        source = load_apk().provenance(source_object)
        source_key = (str(source["bundle"]), str(source["bundle_hash"]), str(source["path_id"]))
        texture_uri = texture_uri_by_source.get(source_key)
        if texture_uri:
            return texture_uri
        texture = source_object.read()
        image = texture.image.convert("RGBA")
        pixel_bytes = image.tobytes()
        pixel_hash = sha(pixel_bytes)
        matching = []
        from PIL import Image
        for candidate in SPRITES.glob(texture.m_Name + "-*.png"):
            try:
                with Image.open(candidate) as exported:
                    if exported.convert("RGBA").size == image.size \
                            and exported.convert("RGBA").tobytes() == pixel_bytes:
                        matching.append(candidate)
            except OSError:
                continue
        if len(matching) > 1:
            raise ValueError("Follow-spine texture has ambiguous verified exports: " + texture.m_Name)
        if matching:
            sprite_source = matching[0]
        else:
            sprite_name = texture.m_Name + "-" + pixel_hash[:10] + ".png"
            sprite_source = SPRITES / sprite_name
            SPRITES.mkdir(parents=True, exist_ok=True)
            image.save(sprite_source, format="PNG", optimize=False)
        texture_uri = "res://assets/official/sprites/" + sprite_source.name
        resource = {"sha256": sha(sprite_source.read_bytes()), "object": texture.m_Name, **source}
        resources[texture_uri] = resource
        texture_uri_by_source[source_key] = texture_uri
        return texture_uri

    def material_data_for_renderer(renderer, particle_source: dict) -> dict:
        material_pointers = list(renderer.m_Materials or [])
        if len(material_pointers) != 1 or material_pointers[0].path_id == 0:
            raise ValueError("Follow-spine particle renderer does not have exactly one verified material: "
                             + str(particle_source.get("path_id", "")))
        material_object = material_pointers[0].deref()
        material = material_object.read()
        source = load_apk().provenance(material_object)
        saved = material.m_SavedProperties
        textures = {}
        texture_resources = {}
        for property_name, env in saved.m_TexEnvs:
            uri = texture_uri_for(env.m_Texture)
            if not uri:
                continue
            textures[str(property_name)] = {
                "file": uri,
                "scale": [float(env.m_Scale.x), float(env.m_Scale.y)],
                "offset": [float(env.m_Offset.x), float(env.m_Offset.y)],
            }
            texture_resources[str(property_name)] = register_texture(uri)
        result = {
            "name": material.m_Name,
            "source": source,
            "textures": textures,
            "textureResources": texture_resources,
            "floats": {str(name): float(value) for name, value in saved.m_Floats},
            "colors": {str(name): [float(getattr(value, channel)) for channel in ("r", "g", "b", "a")]
                       for name, value in saved.m_Colors},
            "shaderKeywords": list(material.m_ValidKeywords or []),
        }
        result.update(read_material_shader_data(result))
        return result

    def mesh_resource(mesh_pointer, particle_source: dict, label: str) -> str:
        if mesh_pointer is None or mesh_pointer.path_id == 0:
            return ""
        mesh_object = mesh_pointer.deref()
        mesh = mesh_object.read()
        from UnityPy.helpers.MeshHelper import MeshHandler
        handler = MeshHandler(mesh)
        handler.process()
        geometry = {
            "vertices": [[float(value) for value in row] for row in (handler.m_Vertices or [])],
            "uv": [[float(value) for value in row] for row in (handler.m_UV0 or [])],
            "colors": [[float(value) for value in row] for row in (handler.m_Colors or [])],
            "triangles": [[[int(index) for index in face] for face in faces]
                          for faces in handler.get_triangles()],
        }
        payload = (json.dumps(geometry, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        filename = "opening-shape-" + sha(payload)[:24] + ".mesh.json"
        mesh_source = load_apk().provenance(mesh_object)
        mesh_source.update(particle_path_id=str(particle_source.get("path_id", "")), mesh_role=label,
                           mesh_name=mesh.m_Name)
        return register_mesh_file(filename, payload, mesh_source)

    def convert_particle_object(particle_object, node_name: str, source_path: str) -> dict:
        source = load_apk().provenance(particle_object)
        raw = particle_object.read_typetree()
        system = particle_object.read()
        initial = raw["InitialModule"]
        emission = raw["EmissionModule"]
        shape_raw = raw["ShapeModule"]
        game_object = system.m_GameObject.deref().read()
        renderer = next((component.component.deref().read() for component in game_object.m_Component
                         if component.component.deref().type.name == "ParticleSystemRenderer"), None)
        if renderer is None:
            raise ValueError("Follow-spine particle has no ParticleSystemRenderer: " + str(source["path_id"]))
        material = material_data_for_renderer(renderer, source)

        shape = {
            "enabled": bool(shape_raw.get("enabled")), "type": int(shape_raw.get("type", 0)),
            "angle": float(shape_raw.get("angle", 0)), "length": float(shape_raw.get("length", 0)),
            "boxThickness": shape_raw.get("boxThickness", {"x": 0, "y": 0, "z": 0}),
            "radiusThickness": float(shape_raw.get("radiusThickness", 1)),
            "donutRadius": float(shape_raw.get("donutRadius", 0.2)),
            "m_Position": vector_data(shape_raw.get("m_Position") or {}),
            "m_Rotation": vector_data(shape_raw.get("m_Rotation") or {}),
            "m_Scale": vector_data(shape_raw.get("m_Scale") or {"x": 1, "y": 1, "z": 1}),
            "placementMode": int(shape_raw.get("placementMode", 0)),
            "m_MeshMaterialIndex": int(shape_raw.get("m_MeshMaterialIndex", 0)),
            "m_MeshNormalOffset": float(shape_raw.get("m_MeshNormalOffset", 0)),
            "m_MeshSpawn": shape_raw.get("m_MeshSpawn", {}),
            "m_Mesh": shape_raw.get("m_Mesh", {"m_FileID": 0, "m_PathID": 0}),
            "m_UseMeshMaterialIndex": bool(shape_raw.get("m_UseMeshMaterialIndex", False)),
            "m_UseMeshColors": bool(shape_raw.get("m_UseMeshColors", True)),
            "alignToDirection": bool(shape_raw.get("alignToDirection", False)),
            "randomDirectionAmount": float(shape_raw.get("randomDirectionAmount", 0)),
            "sphericalDirectionAmount": float(shape_raw.get("sphericalDirectionAmount", 0)),
            "randomPositionAmount": float(shape_raw.get("randomPositionAmount", 0)),
            "radius": shape_raw.get("radius", {}), "arc": shape_raw.get("arc", {}),
        }
        bursts = []
        for burst in emission.get("m_Bursts", [])[:int(emission.get("m_BurstCount", 0))]:
            bursts.append({"time": float(burst.get("time", 0)), "count": curve_data(burst["countCurve"]),
                           "cycles": int(burst.get("cycleCount", 1)),
                           "interval": float(burst.get("repeatInterval", 0.01)),
                           "probability": float(burst.get("probability", 1))})
        modules = {}
        if raw["SizeModule"].get("enabled"):
            modules["SizeModule"] = {"curve": curve_data(raw["SizeModule"]["curve"]),
                                      "y": curve_data(raw["SizeModule"]["y"]),
                                      "separateAxes": bool(raw["SizeModule"].get("separateAxes"))}
        if raw["RotationModule"].get("enabled"):
            modules["RotationModule"] = {"curve": curve_data(raw["RotationModule"]["curve"]),
                                          "x": curve_data(raw["RotationModule"]["x"]),
                                          "y": curve_data(raw["RotationModule"]["y"]),
                                          "separateAxes": bool(raw["RotationModule"].get("separateAxes"))}
        if raw["ColorModule"].get("enabled"):
            modules["ColorModule"] = color_data(raw["ColorModule"]["gradient"])
        if raw["UVModule"].get("enabled"):
            uv = raw["UVModule"]
            modules["UVModule"] = {"animationType": int(uv.get("animationType", 0)),
                                    "tilesX": int(uv.get("tilesX", 1)), "tilesY": int(uv.get("tilesY", 1)),
                                    "cycles": float(uv.get("cycles", 1)), "rowIndex": int(uv.get("rowIndex", 0)),
                                    "rowMode": int(uv.get("rowMode", 0)),
                                    "frame": curve_data(uv["frameOverTime"]),
                                    "start": curve_data(uv["startFrame"]),
                                    "timeMode": int(uv.get("timeMode", 0)), "fps": float(uv.get("fps", 30)),
                                    "speedRange": uv.get("speedRange", {"x": 0, "y": 1}),
                                    "flipU": float(uv.get("flipU", 0)), "flipV": float(uv.get("flipV", 0))}
        if raw["VelocityModule"].get("enabled"):
            velocity = raw["VelocityModule"]
            modules["VelocityModule"] = {key: curve_data(velocity[key]) for key in
                ("x", "y", "z", "orbitalX", "orbitalY", "orbitalZ", "orbitalOffsetX",
                 "orbitalOffsetY", "orbitalOffsetZ", "radial", "speedModifier")}
            modules["VelocityModule"]["inWorldSpace"] = bool(velocity.get("inWorldSpace"))
        if raw["ClampVelocityModule"].get("enabled"):
            clamp = raw["ClampVelocityModule"]
            modules["ClampVelocityModule"] = {"magnitude": curve_data(clamp["magnitude"]),
                                               "x": curve_data(clamp["x"]), "y": curve_data(clamp["y"]),
                                               "z": curve_data(clamp["z"]),
                                               "separateAxis": bool(clamp.get("separateAxis")),
                                               "inWorldSpace": bool(clamp.get("inWorldSpace")),
                                               "dampen": float(clamp.get("dampen", 0)),
                                               "drag": curve_data(clamp["drag"])}
        if raw["NoiseModule"].get("enabled"):
            noise = raw["NoiseModule"]
            modules["NoiseModule"] = {key: curve_data(noise[key]) for key in
                ("strength", "strengthY", "strengthZ", "scrollSpeed", "remap", "remapY", "remapZ",
                 "positionAmount", "rotationAmount", "sizeAmount")}
            modules["NoiseModule"].update({key: noise.get(key) for key in
                ("separateAxes", "frequency", "damping", "octaves", "octaveMultiplier", "octaveScale",
                 "quality", "remapEnabled")})

        supported = {"InitialModule", "ShapeModule", "EmissionModule", "SizeModule", "RotationModule",
                     "ColorModule", "UVModule", "VelocityModule", "ClampVelocityModule", "NoiseModule"}
        unconverted = [name for name, value in raw.items() if name.endswith("Module")
                       and isinstance(value, dict) and value.get("enabled") and name not in supported]
        if unconverted:
            raise ValueError("Follow-spine particle has unsupported enabled modules: "
                             + str(source["path_id"]) + " / " + ",".join(unconverted))

        particle = {
            "path": source_path + "/" + node_name, "source": source,
            "matrix": [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]],
            "active": bool(game_object.m_IsActive), "duration": float(raw.get("lengthInSec", 1)),
            "loop": bool(raw.get("looping")), "prewarm": bool(raw.get("prewarm")),
            "play_on_awake": bool(raw.get("playOnAwake")), "scaling_mode": int(raw.get("scalingMode", 0)),
            "speed": float(raw.get("simulationSpeed", 1)), "max_particles": int(initial.get("maxNumParticles", 1000)),
            "delay": curve_data(raw["startDelay"]), "life": curve_data(initial["startLifetime"]),
            "velocity": curve_data(initial["startSpeed"]), "size": curve_data(initial["startSize"]),
            "size_y": curve_data(initial["startSizeY"]), "size3d": bool(initial.get("size3D")),
            "rotation": curve_data(initial["startRotation"]),
            "rotation_x": curve_data(initial["startRotationX"]),
            "rotation_y": curve_data(initial["startRotationY"]),
            "rotation3d": bool(initial.get("rotation3D")),
            "gravity_modifier": curve_data(initial["gravityModifier"]),
            "color": color_data(initial["startColor"]),
            "simulation_space": 2 if raw.get("moveWithCustomTransform", {}).get("m_PathID", 0)
                else int(bool(raw.get("moveWithTransform"))), "rate": curve_data(emission["rateOverTime"]),
            "bursts": bursts, "shape": shape, "modules": modules, "unconverted_modules": unconverted,
            "render_mode": int(renderer.m_RenderMode), "alignment": int(renderer.m_RenderAlignment),
            "sorting_layer": int(renderer.m_SortingLayer), "sorting_order": int(renderer.m_SortingOrder),
            "renderer_enabled": bool(renderer.m_Enabled), "material": material,
            "textureResources": material["textureResources"],
        }
        if shape.get("m_Mesh", {}).get("m_PathID", 0):
            shape_mesh = register_shape_mesh(particle)
            if shape_mesh:
                particle["shapeMeshResource"] = shape_mesh
        mesh_resources = []
        for mesh_name in ("m_Mesh", "m_Mesh1", "m_Mesh2", "m_Mesh3"):
            mesh_pointer = getattr(renderer, mesh_name, None)
            resource = mesh_resource(mesh_pointer, source, mesh_name)
            if resource and resource not in mesh_resources:
                mesh_resources.append(resource)
        particle["meshResources"] = mesh_resources
        particle["material"].update(read_material_shader_data(material))
        texture_resources = {name: register_texture(value["file"])
                             for name, value in material["textures"].items()}
        particle["material"]["textureResources"] = texture_resources
        particle["textureResources"] = texture_resources
        particle["textureResource"] = texture_resources.get("_MainTex", "")
        validate_particle_source(particle)
        return particle

    def export_follow_prefab(bundle: str, root_id: int, source_path: str) -> dict:
        objects = {obj.path_id: obj for obj in load_apk().objects(bundle)}
        root_object = objects.get(root_id)
        if root_object is None or root_object.type.name != "GameObject":
            raise ValueError("Follow-spine prefab root is missing: " + bundle + "/" + str(root_id))
        root_reader = root_object.read()
        root_transform = next(component.component.deref() for component in root_reader.m_Component
                              if component.component.deref().type.name in ("Transform", "RectTransform"))
        transform_objects = [obj for obj in objects.values() if obj.type.name in ("Transform", "RectTransform")]
        children = {}
        for transform_object in transform_objects:
            transform = transform_object.read()
            father_id = transform.m_Father.path_id if transform.m_Father is not None else 0
            children.setdefault(father_id, []).append(transform_object)

        nodes = []

        def visit(game_object, transform_object, parent_index: int, path: str) -> None:
            transform = transform_object.read()
            transform_data = transform_object.read_typetree()
            source_path_for_node = path + "/" + game_object.m_Name
            node = {"name": game_object.m_Name, "parent": parent_index,
                    "active": bool(game_object.m_IsActive),
                    "position": vector_data(transform_data.get("m_LocalPosition") or {}),
                    "rotation": {axis: float((transform_data.get("m_LocalRotation") or {}).get(axis, 0))
                                 for axis in ("x", "y", "z", "w")},
                    "scale": vector_data(transform_data.get("m_LocalScale") or {"x": 1, "y": 1, "z": 1}),
                    "particles": []}
            node_index = len(nodes)
            nodes.append(node)
            particle_readers = []
            for component in game_object.m_Component:
                component_object = component.component.deref()
                if component_object.type.name == "ParticleSystem":
                    particle_readers.append(component_object)
            for particle_reader in particle_readers:
                node["particles"].append(convert_particle_object(particle_reader, game_object.m_Name,
                                                                  source_path_for_node))
            for child_transform_object in children.get(transform_object.path_id, []):
                child_transform = child_transform_object.read()
                child_game_object = child_transform.m_GameObject.deref().read()
                visit(child_game_object, child_transform_object, node_index, source_path_for_node)

        visit(root_reader, root_transform, -1, "")
        if sum(len(node["particles"]) for node in nodes) == 0:
            raise ValueError("Follow-spine prefab has no particle systems: " + bundle + "/" + str(root_id))
        return {"source": load_apk().provenance(root_object), "rootName": root_reader.m_Name, "nodes": nodes}

    def read_material_shader_data(material: dict) -> dict:
        source = material.get("source") or {}
        bundle = source.get("bundle", "")
        path_id = int(source.get("path_id", 0))
        cache_key = (bundle, path_id)
        if not bundle or not path_id:
            raise ValueError("Opening particle material has no pinned source reference: "
                             + material.get("name", ""))
        if cache_key not in material_shader_data:
            material_object = next((item for item in load_apk().objects(bundle)
                                    if item.type.name == "Material" and item.path_id == path_id), None)
            if material_object is None:
                raise ValueError("Opening particle material source is missing: " + bundle + "/" + str(path_id))
            material_objects[cache_key] = material_object
            source_material = material_object.read()
            if material.get("name") != source_material.m_Name:
                raise ValueError("Opening particle material name differs from the pinned APK: "
                                 + material.get("name", ""))
            actual_bundle_hash = load_apk().index.get(bundle)
            if not actual_bundle_hash or source.get("bundle_hash") != actual_bundle_hash:
                raise ValueError("Opening particle material bundle hash differs from the pinned APK: " + bundle)
            saved = source_material.m_SavedProperties
            source_floats = {str(name): float(value) for name, value in saved.m_Floats}
            payload_floats = {str(name): float(value) for name, value in (material.get("floats") or {}).items()}
            if set(source_floats) != set(payload_floats) or any(
                    not math.isclose(source_floats[name], payload_floats[name], rel_tol=1e-6, abs_tol=1e-6)
                    for name in source_floats):
                raise ValueError("Opening particle material floats differ from the pinned APK: "
                                 + material.get("name", ""))
            source_colors = {str(name): [float(getattr(value, channel)) for channel in ("r", "g", "b", "a")]
                             for name, value in saved.m_Colors}
            payload_colors = material.get("colors") or {}
            colors_match = set(source_colors) == set(payload_colors)
            if colors_match:
                for name, source_values in source_colors.items():
                    payload_values = payload_colors[name]
                    if len(payload_values) != len(source_values):
                        colors_match = False
                        break
                    for source_value, payload_value in zip(source_values, payload_values):
                        if math.isfinite(source_value):
                            if payload_value is None or not math.isclose(
                                    source_value, float(payload_value), rel_tol=1e-6, abs_tol=1e-6):
                                colors_match = False
                                break
                        elif payload_value is not None:
                            try:
                                if math.isfinite(float(payload_value)):
                                    colors_match = False
                                    break
                            except (TypeError, ValueError):
                                colors_match = False
                                break
                    if not colors_match:
                        break
            if not colors_match:
                raise ValueError("Opening particle material colors differ from the pinned APK: "
                                 + material.get("name", ""))
            source_textures = {str(name): env for name, env in saved.m_TexEnvs
                               if env.m_Texture is not None and env.m_Texture.path_id}
            payload_textures = material.get("textures") or {}
            if not set(source_textures).issubset(payload_textures):
                raise ValueError("Opening particle material texture slots differ from the pinned APK: "
                                 + material.get("name", ""))
            shader_object = source_material.m_Shader.deref()
            shader = shader_object.read()
            shader_name = shader.m_ParsedForm.m_Name
            if shader_name not in ("Effect/Noise_Mask_Pro", "Universal Render Pipeline/2D/Sprite-Lit-Default"):
                raise ValueError("Opening particle material uses an unexpected shader: "
                                 + material.get("name", "") + " / " + shader_name)
            shader_properties = ({prop.m_Name: prop for prop in shader.m_ParsedForm.m_PropInfo.m_Props}
                                 if shader.m_ParsedForm is not None else {})
            for property_name, env in source_textures.items():
                texture_info = payload_textures[property_name]
                transforms = texture_info.get("scale", []) + texture_info.get("offset", [])
                if len(transforms) != 4:
                    raise ValueError("Opening particle material texture transform is incomplete: "
                                     + material.get("name", "") + "/" + property_name)
                if not all(math.isclose(float(actual), float(expected), rel_tol=1e-6, abs_tol=1e-6)
                           for actual, expected in zip(
                               (env.m_Scale.x, env.m_Scale.y, env.m_Offset.x, env.m_Offset.y),
                               transforms)):
                    raise ValueError("Opening particle material texture transform differs from the pinned APK: "
                                     + material.get("name", "") + "/" + property_name)
                texture_object = env.m_Texture.deref()
                texture_source = load_apk().provenance(texture_object)
                expected_source = resources.get(texture_info.get("file", ""))
                if not expected_source or any(str(expected_source.get(field, "")) != str(texture_source.get(field, ""))
                                              for field in ("bundle", "bundle_hash", "path_id")):
                    raise ValueError("Opening particle material texture source differs from the pinned APK: "
                                     + material.get("name", "") + "/" + property_name)
            for property_name in set(payload_textures) - set(source_textures):
                if shader_name == "Universal Render Pipeline/2D/Sprite-Lit-Default":
                    raise ValueError("Sprite-Lit particle material uses an unbound default texture: "
                                     + material.get("name", "") + "/" + property_name)
                texture_property = shader_properties.get(property_name)
                texture_info = payload_textures[property_name]
                expected_source = resources.get(texture_info.get("file", ""))
                default_name = (texture_property.m_DefTexture.m_DefaultName
                               if texture_property is not None and texture_property.m_DefTexture is not None
                               else "")
                shader_source = load_apk().provenance(shader_object)
                default_uri = "res://assets/official/map-particles/" + default_name + ".tres"
                transforms = texture_info.get("scale", []) + texture_info.get("offset", [])
                if (texture_property is None or texture_property.m_Type != 4 or not default_name
                        or texture_info.get("file") != default_uri or not expected_source
                        or any(str(expected_source.get(field, "")) != str(shader_source.get(field, ""))
                               for field in ("bundle", "bundle_hash", "path_id"))
                        or len(transforms) != 4
                        or any(not math.isclose(float(actual), float(expected), rel_tol=1e-6, abs_tol=1e-6)
                               for actual, expected in zip(transforms, (1.0, 1.0, 0.0, 0.0)))):
                    raise ValueError("Opening particle material uses an unverified shader default texture: "
                                     + material.get("name", "") + "/" + property_name)
            shader_keywords = list(source_material.m_ValidKeywords or [])
            shader_disabled_keywords = list(source_material.m_InvalidKeywords or [])
            if material.get("shaderKeywords") is not None \
                    and sorted(material["shaderKeywords"]) != sorted(shader_keywords):
                raise ValueError("Opening particle material keywords differ from the pinned APK: "
                                 + material.get("name", ""))
            material_shader_data[cache_key] = {
                "shader": shader_name,
                "runtimeShader": ("Zjcs/Opening/NoiseMaskPro" if shader_name == "Effect/Noise_Mask_Pro"
                                  else "Sprites/Default"),
                "shaderKeywords": shader_keywords,
                "shaderDisabledKeywords": shader_disabled_keywords,
                "shaderSource": load_apk().provenance(shader_object),
            }
        return material_shader_data[cache_key]

    def iter_particle_hierarchy(particle: dict):
        yield particle
        for child in (particle.get("modules") or {}).get("SubModule", []):
            definition = child.get("definition") if isinstance(child, dict) else None
            if isinstance(definition, dict):
                yield from iter_particle_hierarchy(definition)

    def enrich_nested_particle_materials(particle: dict) -> None:
        for child in (particle.get("modules") or {}).get("SubModule", []):
            definition = child.get("definition") if isinstance(child, dict) else None
            if not isinstance(definition, dict):
                continue
            material = definition.get("material")
            if isinstance(material, dict):
                material.update(read_material_shader_data(material))
                texture_resources = {property_name: register_texture(value.get("file", ""))
                                     for property_name, value in (material.get("textures") or {}).items()}
                material["textureResources"] = texture_resources
                definition["textureResources"] = texture_resources
                definition["textureResource"] = texture_resources.get("_MainTex", "")
            enrich_nested_particle_materials(definition)

    def iter_entry_particles(entry: dict):
        for particle in entry.get("particles", []):
            yield particle
        for prefab in entry.get("source_event", {}).get("follower", {}).get("prefabs", []):
            for node in prefab.get("nodes", []):
                yield from node.get("particles", [])

    grappling_line = None
    if "cast:11830" in selected:
        apk = load_apk()
        line_bundle = "defaultpackage_assets_prefabs_ecentity_fight_fightskill_11830.bundle"
        line_renderer_object = next((item for item in apk.objects(line_bundle)
                                     if item.type.name == "LineRenderer"), None)
        if line_renderer_object is None:
            raise ValueError("Pinned 11830 prefab has no LineRenderer")
        line_renderer = line_renderer_object.read()
        line_material_ptr = line_renderer.m_Materials[0]
        line_material = line_material_ptr.deref().read()
        line_material_textures = {}
        line_texture_resources = {}
        for property_name, env in line_material.m_SavedProperties.m_TexEnvs:
            if env.m_Texture is None or env.m_Texture.path_id == 0:
                continue
            texture = env.m_Texture.deref().read()
            candidates = sorted(SPRITES.glob(texture.m_Name + "-*.png"))
            if len(candidates) != 1:
                raise ValueError("Pinned grapple texture does not have one verified sprite export: " + texture.m_Name)
            sprite_source = candidates[0]
            from PIL import Image
            with Image.open(sprite_source) as exported:
                decoded = texture.image.convert("RGBA")
                if exported.convert("RGBA").size != decoded.size \
                        or exported.convert("RGBA").tobytes() != decoded.tobytes():
                    raise ValueError("Grapple texture pixels differ from pinned Texture2D: " + texture.m_Name)
            texture_uri = "res://assets/official/sprites/" + sprite_source.name
            texture_assets_file = texture.object_reader.assets_file.name.lower()
            texture_source = apk.cabs.get(texture_assets_file)
            if not texture_source or not texture_source.get("bundle"):
                raise ValueError("Grapple texture has no pinned bundle source record: " + texture.m_Name)
            texture_bundle = texture_source["bundle"]
            texture_resource = {
                "sha256": sha(sprite_source.read_bytes()), "bundle": texture_bundle,
                "bundle_hash": apk.index[texture_bundle], "path_id": str(env.m_Texture.path_id),
                "object": texture.m_Name,
            }
            resources[texture_uri] = texture_resource
            line_texture_resources[property_name] = sprite_source.stem
            register_texture(texture_uri)
            line_material_textures[property_name] = {
                "file": texture_uri,
                "scale": [float(env.m_Scale.x), float(env.m_Scale.y)],
                "offset": [float(env.m_Offset.x), float(env.m_Offset.y)],
            }

        def line_color(color):
            return [float(color.r), float(color.g), float(color.b), float(color.a)]

        def line_gradient(gradient):
            color_count = int(gradient.m_NumColorKeys)
            alpha_count = int(gradient.m_NumAlphaKeys)
            colors = []
            alphas = []
            for index in range(color_count):
                key = getattr(gradient, "key" + str(index))
                colors.append([float(getattr(gradient, "ctime" + str(index))) / 65535.0,
                               float(key.r), float(key.g), float(key.b)])
            for index in range(alpha_count):
                key = getattr(gradient, "key" + str(index))
                alphas.append([float(getattr(gradient, "atime" + str(index))) / 65535.0,
                               float(key.a)])
            return {"mode": int(gradient.m_Mode), "rgb": colors, "alpha": alphas}

        def line_curve(curve):
            return {"m_Curve": [{"time": float(key.time), "value": float(key.value),
                                 "inSlope": float(key.inSlope or 0), "outSlope": float(key.outSlope or 0),
                                 "weightedMode": int(key.weightedMode), "inWeight": float(key.inWeight),
                                 "outWeight": float(key.outWeight)} for key in curve.m_Curve],
                    "m_PreInfinity": int(curve.m_PreInfinity),
                    "m_PostInfinity": int(curve.m_PostInfinity)}

        saved = line_material.m_SavedProperties
        material_source_file = line_material.object_reader.assets_file.name.lower()
        material_bundle_record = apk.cabs.get(material_source_file)
        if not material_bundle_record:
            material_bundle_record = {"bundle": line_bundle, "hash": apk.index[line_bundle]}
        params = line_renderer.m_Parameters
        grappling_line = {
            "source": {"bundle": line_bundle, "bundle_hash": apk.index[line_bundle],
                       "line_renderer_path_id": str(line_renderer_object.path_id),
                       "material_path_id": str(line_material_ptr.path_id),
                       "material_bundle": material_bundle_record.get("bundle", line_bundle),
                       "material_bundle_hash": material_bundle_record.get("hash", apk.index[line_bundle])},
            "material": {
                "name": line_material.m_Name,
                "textures": line_material_textures,
                "textureResources": line_texture_resources,
                "floats": {str(name): float(value) for name, value in saved.m_Floats},
                "colors": {str(name): line_color(value) for name, value in saved.m_Colors},
            },
            "line_renderer": {
                "enabled": bool(line_renderer.m_Enabled),
                "useWorldSpace": bool(line_renderer.m_UseWorldSpace),
                "loop": bool(line_renderer.m_Loop),
                "sortingOrder": int(line_renderer.m_SortingOrder),
                "widthMultiplier": float(params.widthMultiplier),
                "widthCurve": line_curve(params.widthCurve),
                "colorGradient": line_gradient(params.colorGradient),
                "textureMode": int(params.textureMode),
                "textureScale": [float(params.textureScale.x), float(params.textureScale.y)],
                "alignment": int(params.alignment),
                "numCapVertices": int(params.numCapVertices),
                "numCornerVertices": int(params.numCornerVertices),
                "generateLightingData": bool(params.generateLightingData),
                "shadowBias": float(params.shadowBias),
            },
        }
        effect_inputs["opening-grapple-line"] = {
            "source": grappling_line["source"],
            "textureResources": {name: value for name, value in line_texture_resources.items()},
        }

    for key, events in selected.items():
        class_id = int(key.split(":", 1)[1])
        for event in events:
            event_kind = event.get("kind", "")
            output_event = {
                "classId": class_id,
                "effectType": key.split(":", 1)[0],
                "key": key,
                "kind": event_kind,
                "eventTime": float(event.get("time", 0.0)),
                "eventSpeed": float(event.get("speed", 1.0)),
                "playable_field": event.get("field", "myPlayablePrefab"),
            }
            if event_kind == "sound":
                event_name = event.get("event", "")
                sound = audio_catalog.get("events", {}).get(event_name)
                if not sound or not sound.get("variants"):
                    raise ValueError("Wwise event has no verified offline variant: " + event_name)
                variants = []
                for variant in sound["variants"]:
                    audio_uri = variant.get("file", "")
                    audio_resource = audio_manifest.get("resources", {}).get(audio_uri)
                    if not audio_resource:
                        raise ValueError("Battle audio event has no source manifest record: " + event_name)
                    audio_path = ROOT / PurePosixPath(audio_uri.removeprefix("res://"))
                    audio_bytes = audio_path.read_bytes()
                    expected_audio_hash = audio_resource.get("sha256", "")
                    if not expected_audio_hash or sha(audio_bytes) != expected_audio_hash:
                        raise ValueError("Battle audio OGG hash differs from source manifest: " + event_name)
                    audio_name = audio_path.name
                    audio_stem = audio_path.stem
                    relative_audio = "Assets/Official/Opening/SkillEffects/Resources/OpeningSkillEffects/Audio/" + audio_name
                    target_audio = AUDIO / audio_name
                    target_audio.parent.mkdir(parents=True, exist_ok=True)
                    if not target_audio.exists() or sha(target_audio.read_bytes()) != expected_audio_hash:
                        target_audio.write_bytes(audio_bytes)
                    audio_meta = target_audio.with_name(target_audio.name + ".meta")
                    if not audio_meta.exists():
                        audio_meta.write_text(AUDIO_META.format(guid=guid(relative_audio)), encoding="utf-8")
                    output_sounds[audio_name] = {"path": relative_audio, "sha256": expected_audio_hash,
                                                 "event": event_name, "event_id": sound.get("id"),
                                                 "duration": variant.get("duration", 0),
                                                 "source_uri": audio_uri}
                    variants.append({"resource": audio_stem, "duration": variant.get("duration", 0),
                                     "loop": bool(variant.get("loop", False)),
                                     "loop_offset": variant.get("loop_offset", 0.0),
                                     "weight": variant.get("weight", variant.get("probability", 1.0))})
                output_event["event"] = event_name
                output_event["event_id"] = sound.get("id")
                output_event["channel"] = sound.get("channel", "")
                output_event["variants"] = variants
                output_effects.append(output_event)
                continue
            if event_kind != "particles":
                output_event["source_event"] = {k: v for k, v in event.items() if k != "file"}
                if event_kind == "special" and event.get("playable") == "MyPlayableFollowSpinePart":
                    source_event = output_event["source_event"]
                    follower = source_event.get("follower") or {}
                    bundle = (source_event.get("source") or {}).get("bundle", "")
                    prefabs = []
                    for pointer in follower.get("Gos", []):
                        root_id = int(pointer.get("m_PathID", 0))
                        if not root_id:
                            continue
                        prefab = export_follow_prefab(bundle, root_id,
                            "follow:" + str(class_id) + ":" + str(root_id))
                        prefabs.append(prefab)
                        source_key = "follow-prefab:" + bundle + ":" + str(root_id)
                        effect_inputs[source_key] = {
                            "source": prefab["source"],
                            "particle_systems": [particle["source"] for node in prefab["nodes"]
                                                 for root_particle in node["particles"]
                                                 for particle in iter_particle_hierarchy(root_particle)],
                            "materials": [particle["material"]["source"] for node in prefab["nodes"]
                                          for root_particle in node["particles"]
                                          for particle in iter_particle_hierarchy(root_particle)],
                            "textures": [texture["file"] for node in prefab["nodes"]
                                         for root_particle in node["particles"]
                                         for particle in iter_particle_hierarchy(root_particle)
                                         for texture in particle["material"]["textures"].values()],
                        }
                    if follower:
                        follower["prefabs"] = prefabs
                    for root_particle in iter_entry_particles(output_event):
                        enrich_nested_particle_materials(root_particle)
                if key == "cast:11830" and "mStartAttachPos" in event.get("fields", {}):
                    output_event["source_event"]["grappling_line"] = grappling_line
                output_effects.append(output_event)
                continue
            uri = event.get("file", "")
            prefix = "res://assets/official/battle-presentation/effects/"
            if not uri.startswith(prefix):
                raise ValueError("Unexpected effect payload URI: " + uri)
            relative = uri[len("res://"):]
            source = ROOT / PurePosixPath(relative)
            manifest_path = relative.removeprefix("assets/official/")
            source_hash, source_bytes = load_pinned_effect_source(manifest_path, source)
            data = json.loads(source_bytes.decode("utf-8"))
            effect_inputs[manifest_path] = {
                "sha256": source_hash,
                "payload_source": next((entry["preserved"] for entry in restored_pinned_payloads
                                         if entry["path"] == manifest_path), relative),
                "systems": [{"path": particle.get("path", ""),
                             "source": particle.get("source", {}),
                             "material": (particle.get("material") or {}).get("source", {}),
                             "meshes": particle.get("meshes", []),
                             "shape_mesh": (particle.get("shape") or {}).get("m_Mesh", {})}
                            for root_particle in data for particle in iter_particle_hierarchy(root_particle)],
            }
            particle_records = []
            for particle in data:
                validate_particle_source(particle)
                material = particle.get("material") or {}
                material_textures = material.get("textures") or {}
                texture_resources = {property_name: register_texture(value.get("file", ""))
                                     for property_name, value in material_textures.items()}
                particle_copy = dict(particle)
                particle_copy["material"] = dict(material)
                particle_copy["material"].update(read_material_shader_data(material))
                particle_copy["textureResources"] = texture_resources
                particle_copy["textureResource"] = texture_resources.get("_MainTex", "")
                enrich_nested_particle_materials(particle_copy)
                particle_copy["meshResources"] = [register_mesh_uri(mesh_uri)
                                                   for mesh_uri in particle.get("meshes", [])]
                shape_mesh = register_shape_mesh(particle)
                if shape_mesh:
                    particle_copy["shapeMeshResource"] = shape_mesh
                particle_records.append(particle_copy)
            output_event["prefab"] = relative
            output_event["prefab_sha256"] = source_hash
            output_event["source_event"] = {k: v for k, v in event.items() if k != "file"}
            output_event["particles"] = particle_records
            output_effects.append(output_event)
    output = {
        "manifest": VERSION,
        "record_sha256": sha(RECORD.read_bytes()),
        "effects_catalog_sha256": expected,
        "effect_manifest_sha256": sha(MANIFEST.read_bytes()),
        "effects": output_effects,
        "textures": output_textures,
        "sounds": output_sounds,
        "meshes": output_meshes,
    }
    data = (json.dumps(output, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_bytes(data)
    ensure_payload_meta(TARGET)
    output_hashes = {TARGET.relative_to(ROOT).as_posix(): sha(data)}
    for entry in output_textures.values():
        if not entry.get("builtin"):
            output_hashes[entry["path"]] = entry["sha256"]
    for entry in output_sounds.values():
        output_hashes[entry["path"]] = entry["sha256"]
    for entry in output_meshes.values():
        output_hashes[(ROOT / "unity/Assets/StreamingAssets" / entry["path"]).relative_to(ROOT).as_posix()] = entry["sha256"]
    reference = {
        "manifest": VERSION,
        "operation": "Pinned opening-record battle playables and Wwise variants; native ParticleSystem/audio "
                     "runtime adapters preserve event timing, source transforms, particle modules including "
                     "recursive sub-emitters, material maps, mesh renderers and verified hashes",
        "inputs": {
            "record": {"path": RECORD.relative_to(ROOT).as_posix(), "sha256": sha(RECORD.read_bytes())},
            "effect_catalog": {"path": EFFECTS.relative_to(ROOT).as_posix(), "sha256": expected},
            "effect_source_manifest": {"path": MANIFEST.relative_to(ROOT).as_posix(),
                                        "sha256": sha(MANIFEST.read_bytes())},
            "audio_catalog": {"path": BATTLE_AUDIO.relative_to(ROOT).as_posix(),
                              "sha256": sha(audio_catalog_bytes)},
            "audio_source_manifest": {"path": BATTLE_AUDIO_MANIFEST.relative_to(ROOT).as_posix(),
                                       "sha256": sha(BATTLE_AUDIO_MANIFEST.read_bytes())},
            "effect_payloads": effect_inputs,
        },
        "effects": [{"key": entry["key"], "effect_type": entry["effectType"],
                     "class_id": entry["classId"], "kind": entry["kind"],
                     "playable_field": entry["playable_field"],
                     "particle_systems": len(entry.get("particles", [])),
                     "follow_particle_systems": sum(len(node.get("particles", []))
                         for prefab in entry.get("source_event", {}).get("follower", {}).get("prefabs", [])
                         for node in prefab.get("nodes", [])),
                     "sub_emitter_systems": sum(1 for root_particle in iter_entry_particles(entry)
                         for particle in iter_particle_hierarchy(root_particle) if particle is not root_particle),
                     "prefab": entry.get("prefab"), "prefab_sha256": entry.get("prefab_sha256"),
                     "event": entry.get("event"),
                     "variants": entry.get("variants", [])}
                    for entry in output_effects],
        "textures": output_textures,
        "sounds": output_sounds,
        "meshes": output_meshes,
        "outputs": output_hashes,
    }
    reference_data = (json.dumps(reference, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_bytes(reference_data)
    print(json.dumps({"effects": len(output_effects),
                      "particleSystems": sum(len(e.get("particles", [])) for e in output_effects),
                      "followParticleSystems": sum(len(node.get("particles", []))
                          for entry in output_effects
                          for prefab in entry.get("source_event", {}).get("follower", {}).get("prefabs", [])
                          for node in prefab.get("nodes", [])),
                      "sounds": sum(e.get("kind") == "sound" for e in output_effects),
                      "textures": len(output_textures), "audioFiles": len(output_sounds), "payloadBytes": len(data),
                      "restoredPinnedPayloads": restored_pinned_payloads,
                      "payloadSha256": sha(data), "manifest": REFERENCE.relative_to(ROOT).as_posix(),
                      "selected": [e["key"] for e in output_effects]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
