"""Prepare pinned map entity display geometry, textures, and Spine data."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import re

from map10_geometry import decode, version as geometry_version


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
UNITY_OUTPUT = ROOT / "unity/Assets/Official/World/Map/10"
MAP_DATA = UNITY_OUTPUT / "map10.json"
MAP_COMPONENTS = ROOT / "assets/official/regions/10-entities.json"
MODEL_CATALOG = ROOT / "assets/official/map-models/catalog.json"
ADVENTURE_DATA = ROOT / "assets/official/adventure.json"
MODEL_SOURCE_MANIFEST = ROOT / "assets/reference/official-map/models-source-manifest.json"
EXTRA_MODEL_SOURCE_MANIFEST = ROOT / "assets/reference/official-map/extra-models-source-manifest.json"
SPINE_SOURCE_MANIFEST = ROOT / "assets/reference/official-map/spine-pages-source-manifest.json"
STORY_ACTOR_SOURCE_MANIFEST = ROOT / "assets/reference/official-map/story-actors-source-manifest.json"
BATTLE_DATA = UNITY_OUTPUT / "map10-battles.json"
BATTLE_SOURCE_MANIFEST = UNITY_OUTPUT / "battle-source-manifest.json"
OUTPUT_NAME = "map10-entities.json"
SOURCE_MANIFEST_NAME = "entity-source-manifest.json"
ENTITY_CATALOG_RESOURCE = "Resources/Map10/Entities/catalog.json"
GEOMETRY_RESOURCE_DIR = "Resources/Map10/EntityGeometry"
SPINE_RESOURCE_DIR = "Resources/Map10/Spines"
TEXTURE_RESOURCE_DIR = "Resources/Map10/Textures"
VALIDATED_REFRESH_INPUTS = {"battle_data", "battle_source_manifest"}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def compact_json(value) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)


def recover_map11_state_textures() -> dict:
    """Verify the two grave-building state sprites omitted by the legacy model manifest."""
    from official_apk import Apk
    apk = Apk(reuse_current_cache=True)
    root = apk.asset("prefabs_map_map_11_entitydisplays_worldbuilding", "6330_11_view.prefab")
    names = {"building_naka1": "building_naka1-0c53fcc277.png",
             "building_luzi": "building_luzi-5b63b3348a.png"}
    result = {}

    def visit(game_object):
        for component in game_object.read().m_Component:
            obj = component.component.deref()
            if obj.type.name in ("Transform", "RectTransform"):
                for child in obj.read().m_Children:
                    visit(child.read().m_GameObject.deref())
            elif obj.type.name == "SpriteRenderer":
                pointer = obj.read().m_Sprite
                if not pointer.path_id:
                    continue
                sprite = pointer.read()
                if sprite.m_Name not in names:
                    continue
                target = ROOT / "assets/official/sprites" / names[sprite.m_Name]
                output = io.BytesIO()
                sprite.image.save(output, format="PNG")
                raw = output.getvalue()
                if target.read_bytes() != raw:
                    raise ValueError("Map11 building-state sprite differs from the official Sprite extraction: " + str(target))
                uri = "res://" + target.relative_to(ROOT).as_posix()
                result[uri] = {"sha256": sha256(raw), "source": apk.provenance(pointer.deref()),
                               "prefab": apk.provenance(root), "operation": "UnityPy Sprite.image to PNG"}

    visit(root)
    if len(result) != len(names):
        raise ValueError("Official Map11 building-state Sprite references are incomplete")
    return result


def select_display_path(entity_class_id: int, model_paths: list[str], map_id: int = 10) -> str | None:
    identity = str(entity_class_id)
    candidates = []
    fallback_candidates = []
    for path in model_paths:
        leaf = path.rsplit("/", 1)[-1].lower()
        if re.fullmatch(re.escape(identity) + rf"(?:_{map_id})?(?:_story)?_view", leaf):
            target = candidates
        elif re.fullmatch(re.escape(identity) + r"(?:_[a-z0-9]+)+_view", leaf):
            target = fallback_candidates
        else:
            continue
        if path.startswith(f"Map/Map_{map_id}/"):
            priority = 0
        elif path.startswith("Map/Common/"):
            priority = 1
        else:
            priority = 2
        target.append((priority, path))
    if not candidates:
        candidates = fallback_candidates
    if not candidates:
        return None
    best_priority = min(priority for priority, _ in candidates)
    best = sorted(path for priority, path in candidates if priority == best_priority)
    if len(best) != 1:
        raise ValueError(f"Ambiguous official Map{map_id} entity display for ClassId " + identity + ": " + ", ".join(best))
    return best[0]


def resolve_display_path(entity_class_id: int, model_catalog: dict, map_displays: list[dict],
                         map_id: int) -> str | None:
    model_paths = model_catalog.get("models", {})
    direct = select_display_path(entity_class_id, list(model_paths), map_id)
    if direct:
        return direct
    if map_id != 11:
        return None

    candidates = [row for row in map_displays
                  if int(row.get("EntityClassId", 0)) == entity_class_id
                  and int(row.get("MapClassId", -1)) in (0, map_id)
                  and int(row.get("GroupId", 0)) == 0
                  and int(row.get("GroundType", -1)) == 8888
                  and row.get("AreaTag") == "IgnoreKey"
                  and row.get("DisplayPath") in model_paths]
    if not candidates:
        return None
    candidates.sort(key=lambda row: (int(row.get("MapClassId", 0)) != map_id, row["DisplayPath"]))
    best_priority = (int(candidates[0].get("MapClassId", 0)) != map_id,
                     candidates[0]["DisplayPath"])
    best = sorted({row["DisplayPath"] for row in candidates
                   if (int(row.get("MapClassId", 0)) != map_id, row["DisplayPath"]) == best_priority})
    if len(best) != 1:
        raise ValueError(f"Ambiguous official Map{map_id} entity display table entry for ClassId "
                         + str(entity_class_id) + ": " + ", ".join(best))
    return best[0]


def write_generated(path: Path, content: bytes, previous_hashes: dict[Path, str] | None = None) -> None:
    if path.exists():
        existing = path.read_bytes()
        if existing == content:
            return
        expected_previous = (previous_hashes or {}).get(path.resolve())
        if expected_previous is None or sha256(existing) != expected_previous:
            raise ValueError("Refusing to overwrite changed Map10 entity payload: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def verified_previous_outputs(output: Path, current_inputs: dict, map_id: int = 10, unchanged_placements: bool = False) -> dict[Path, str]:
    source_manifest_name = "entity-source-manifest.json"
    source_path = output / source_manifest_name
    if not source_path.is_file():
        return {}
    previous = json.loads(source_path.read_text(encoding="utf-8"))
    if previous.get("format") != 1 or previous.get("manifest") != VERSION or previous.get("mapClassId") != map_id:
        raise ValueError(f"Existing Map{map_id} entity manifest has an unexpected identity")
    for name, record in previous.get("inputs", {}).items():
        if current_inputs.get(name) != record and name not in VALIDATED_REFRESH_INPUTS and not (name == "map_definition" and unchanged_placements):
            raise ValueError(f"Existing Map{map_id} entity manifest input changed: " + name)

    verified = {}

    def record(path: Path, expected_hash: str) -> None:
        if not path.is_file() or sha256(path.read_bytes()) != expected_hash:
            raise ValueError(f"Existing Map{map_id} entity output differs from its source manifest: " + str(path))
        verified[path.resolve()] = expected_hash

    previous_output = previous.get("output", {})
    record(output / previous_output["path"], previous_output["sha256"])
    for item in previous.get("geometry", {}).values():
        record(output / item["path"], item["sha256"])
    for item in previous.get("spines", {}).values():
        record(output / item["path"], item["sha256"])
    for item in previous.get("textures", []):
        record(output / f"Resources/Map{map_id}/Textures" / Path(item["uri"]).name, item["sha256"])
    if previous_output.get("resourcePath"):
        record(output / ("Resources/Map" + str(map_id) + "/Entities/catalog.json"),
               previous_output["resourceSha256"])
    verified[source_path.resolve()] = sha256(source_path.read_bytes())
    return verified


def write_meta(path: Path, importer: str) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    folder = "folderAsset: yes\n" if importer == "DefaultImporter" else ""
    content = (f"fileFormatVersion: 2\nguid: {guid}\n{folder}{importer}:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_text(encoding="utf-8") != content:
        existing = target.read_text(encoding="utf-8")
        if f"guid: {guid}" not in existing or importer not in existing:
            raise ValueError("Refusing to replace an existing Unity meta file: " + str(target))
    if not target.exists():
        target.write_text(content, encoding="utf-8", newline="\n")


def write_texture_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    content = (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
               "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n"
               "    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n"
               "  isReadable: 0\n  textureSettings:\n    serializedVersion: 2\n"
               "    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 0\n"
               "    wrapV: 0\n    wrapW: 0\n  spriteMode: 0\n"
               "  alphaIsTransparency: 1\n  textureType: 0\n  textureShape: 1\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_text(encoding="utf-8") != content:
        existing = target.read_text(encoding="utf-8")
        # Unity-native imports can already own a stable GUID referenced by
        # materials. Retain that metadata verbatim when adding more map models.
        import re
        if len(re.findall(r'^guid: [0-9a-f]{32}$', existing, re.M)) != 1 or "TextureImporter:" not in existing:
            raise ValueError("Refusing to replace an existing Unity texture meta file: " + str(target))
    if not target.exists():
        target.write_text(content, encoding="utf-8", newline="\n")


def source_digest(path: Path) -> dict:
    payload = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(payload), "sha256": sha256(payload)}


def stage_ids_for_entity(placement: dict, components: dict) -> list[int]:
    source = components.get(str(placement["Id"]), {})
    stage_ids = []
    for component_name in ("LoStageBehStageComponent", "LoStageBehBattleSteleStageComponent",
                           "LoStageBehStoneCastleStageComponent"):
        component = source.get(component_name, {}) or {}
        values = component.get("StageIds") or []
        if component.get("StageId", 0):
            values = list(values) + [component["StageId"]]
        if not isinstance(values, list) or any(not isinstance(value, int) or value <= 0 for value in values):
            raise ValueError("Map entity stage component is malformed: " + str(placement["Id"]))
        stage_ids.extend(values)
    for stage in placement.get("Stages") or []:
        try:
            value = json.loads(stage["ValueJson"])
        except (KeyError, TypeError, json.JSONDecodeError):
            continue
        if isinstance(value, list) and value and isinstance(value[0], list):
            stage_ids.extend(value[0])
    return sorted({int(stage_id) for stage_id in stage_ids})


def stage_behavior_bindings(placement_id: int, components: dict) -> list[dict]:
    source = components.get(str(placement_id), {})
    result = []
    for component_name in ("LoStageBehStageComponent", "LoStageBehBattleSteleStageComponent",
                           "LoStageBehStoneCastleStageComponent"):
        component = source.get(component_name) or {}
        stage_ids = component.get("StageIds") or []
        if component.get("StageId", 0):
            stage_ids = list(stage_ids) + [component["StageId"]]
        if not isinstance(stage_ids, list) or any(not isinstance(value, int) or value <= 0 for value in stage_ids):
            raise ValueError("Map stage behaviour binding is malformed: " + str(placement_id))
        if stage_ids:
            result.append({"Component": component_name, "StageIds": sorted(set(stage_ids))})
    return result


def entity_runtime_rules(placement_id: int, components: dict) -> dict:
    source = components.get(str(placement_id), {})
    lock = source.get("LoEntityLockComponent") or {}
    lock_params = lock.get("Params") or "{}"
    try:
        normalized_lock_params = re.sub(r'([{,]\s*)([A-Za-z_][A-Za-z0-9_]*)(\s*:)', r'\1"\2"\3', lock_params)
        lock_rules = json.loads(normalized_lock_params)
    except (TypeError, json.JSONDecodeError) as error:
        raise ValueError("Map10 entity lock parameters are invalid: " + str(placement_id)) from error
    if not isinstance(lock_rules, dict):
        raise ValueError("Map10 entity lock parameters are not an object: " + str(placement_id))

    groups = lock_rules.get("EntityGroup") or []
    if not isinstance(groups, list) or any(not isinstance(value, int) or value <= 0 for value in groups):
        raise ValueError("Map10 entity lock groups are invalid: " + str(placement_id))
    condition_ids = lock_rules.get("EntityGroup") or lock_rules.get("LoEntityIds") or []
    if not isinstance(condition_ids, list) or any(not isinstance(value, int) or value <= 0 for value in condition_ids):
        raise ValueError("Map10 entity lock condition ids are invalid: " + str(placement_id))

    award_component = source.get("LoStageResultAwardItemComponent") or {}
    awards = award_component.get("AwardItemInfos") or []
    if not isinstance(awards, list):
        raise ValueError("Map10 entity item awards are invalid: " + str(placement_id))
    for award in awards:
        if (not isinstance(award, dict) or not isinstance(award.get("ItemId"), int)
                or award["ItemId"] <= 0 or not isinstance(award.get("Count"), int) or award["Count"] <= 0):
            raise ValueError("Map10 entity item award is malformed: " + str(placement_id))

    change_map = source.get("LoStageResultChangeMapComponent") or {}
    periodic = source.get("LoStageResultPeriodicityInteractionComponent") or {}
    player_periodic = source.get("LoStageConPlayerPeriodicityInteractionComponent") or {}
    periodic_group_ids = periodic.get("PeriodicityGroupIds") or []
    player_periodic_group_ids = player_periodic.get("PeriodicityGroupIds") or []
    if any(not isinstance(value, int) or value <= 0 for value in periodic_group_ids + player_periodic_group_ids):
        raise ValueError("Map entity periodicity interaction groups are invalid: " + str(placement_id))
    return {
        "lockType": int(lock.get("LockType", 0)),
        "lockedByEntityGroups": groups,
        "lockConditionEntityIds": condition_ids,
        "lockParamsJson": compact_json(lock_rules),
        "conditionEntityNeverDestroy": bool(lock_rules.get("ConditionEntityNeverDestroy", False)),
        "conditionEntityInactiveInUnlock": bool(lock_rules.get("ConditionEntityInactiveInUnlock", False)),
        "awardItems": awards,
        "changeMapClassId": int(change_map.get("MapClassId", 0)),
        "periodicityGroupIds": periodic_group_ids,
        "playerPeriodicityGroupIds": player_periodic_group_ids,
    }


def spine_payload(map_id: int, model_path: str, entity_class_id: int, index: int, instance: dict,
                  skeleton: dict, texture_resource: str) -> dict:
    matrix = instance.get("matrix")
    if not isinstance(matrix, list) or len(matrix) != 4 or any(not isinstance(row, list) or len(row) != 4 for row in matrix):
        raise ValueError("Official Map10 entity Spine matrix is invalid: " + model_path)
    frames = skeleton.get("frames")
    fps = skeleton.get("fps")
    if not isinstance(frames, list) or not frames or not isinstance(fps, (int, float)) or fps <= 0:
        raise ValueError("Official Map10 entity Spine frames are missing: " + model_path)
    pages = {attachment.get("page") for frame in frames for attachment in frame}
    if not pages or len(pages) != 1:
        raise ValueError("Official Map10 entity Spine uses an unsupported atlas page set: " + model_path)
    return {
        "format": 1,
        "mapClassId": map_id,
        "monsterId": entity_class_id,
        "entityClassId": entity_class_id,
        "instance": index,
        "modelPath": model_path,
        "animation": skeleton.get("animation", ""),
        "duration": skeleton.get("duration", 0),
        "fps": fps,
        "scaleX": skeleton.get("scale", 1) * matrix[0][0],
        "scaleY": skeleton.get("scale", 1) * matrix[1][1],
        "offsetX": matrix[0][3],
        "offsetY": matrix[1][3],
        "sortingLayer": instance.get("sorting_layer", 0),
        "sortingOrder": instance.get("sorting_order", 0),
        "textureResource": texture_resource,
        "frames": [{"attachments": frame} for frame in frames],
    }


def build(output: Path | None = None, map_id: int = 10) -> dict:
    if map_id not in (10, 11, 12):
        raise ValueError("Only Maps 10, 11 and 12 have pinned entity source manifests")
    map_root = ROOT / "unity/Assets/Official/World/Map" / str(map_id)
    output = Path(output or map_root).resolve()
    map_data_path = map_root / f"map{map_id}.json"
    components_path = ROOT / f"assets/official/regions/{map_id}-entities.json"
    output_name = f"map{map_id}-entities.json"
    entity_catalog_resource = f"Resources/Map{map_id}/Entities/catalog.json"
    geometry_resource_dir = f"Resources/Map{map_id}/EntityGeometry"
    spine_resource_dir = f"Resources/Map{map_id}/Spines"
    texture_resource_dir = f"Resources/Map{map_id}/Textures"
    definition_bytes = map_data_path.read_bytes()
    placements = json.loads(definition_bytes.decode("utf-8"))
    components_bytes = components_path.read_bytes()
    components = json.loads(components_bytes.decode("utf-8"))
    model_catalog_bytes = MODEL_CATALOG.read_bytes()
    model_catalog = json.loads(model_catalog_bytes.decode("utf-8"))
    adventure_bytes = ADVENTURE_DATA.read_bytes()
    adventure = json.loads(adventure_bytes.decode("utf-8"))
    map_displays = adventure.get("tables", {}).get("Map/entity_map_display", [])
    if not isinstance(map_displays, list):
        raise ValueError("Official map entity display table is malformed")
    model_manifest_bytes = MODEL_SOURCE_MANIFEST.read_bytes()
    model_manifest = json.loads(model_manifest_bytes.decode("utf-8"))
    extra_model_manifest_bytes = EXTRA_MODEL_SOURCE_MANIFEST.read_bytes()
    extra_model_manifest = json.loads(extra_model_manifest_bytes.decode("utf-8"))
    spine_manifest_bytes = SPINE_SOURCE_MANIFEST.read_bytes()
    spine_manifest = json.loads(spine_manifest_bytes.decode("utf-8"))
    story_actor_manifest_bytes = STORY_ACTOR_SOURCE_MANIFEST.read_bytes()
    story_actor_manifest = json.loads(story_actor_manifest_bytes.decode("utf-8"))
    battle_bytes = BATTLE_DATA.read_bytes() if map_id == 10 else None
    battle_source_bytes = BATTLE_SOURCE_MANIFEST.read_bytes() if map_id == 10 else None
    battle_data = json.loads(battle_bytes.decode("utf-8")) if battle_bytes else None
    battle_source = json.loads(battle_source_bytes.decode("utf-8")) if battle_source_bytes else None

    if placements.get("ClassId") != map_id or not placements.get("Entities"):
        raise ValueError(f"Pinned Map{map_id} placement source is incomplete")
    if map_id == 10 and len(placements["Entities"]) != 82:
        raise ValueError("Pinned Map10 placement count differs")
    if (model_manifest.get("manifest") != VERSION or extra_model_manifest.get("manifest") != VERSION
            or spine_manifest.get("manifest") != VERSION or story_actor_manifest.get("manifest") != VERSION):
        raise ValueError(f"Map{map_id} model or Spine sources are not pinned to the official APK")
    if map_id == 10 and (battle_data.get("manifest") != VERSION or battle_data.get("mapClassId") != map_id
                         or battle_source.get("output", {}).get("sha256") != sha256(battle_bytes)
                         or battle_source.get("manifest") != VERSION):
        raise ValueError("Map10 battle data differs from its source manifest")

    stage_rows = {int(stage["Id"]): stage for stage in (battle_data or {}).get("stages", [])}
    model_paths = model_catalog.get("models", {})
    model_sources = {name: digest for name, digest in extra_model_manifest.get("outputs", {}).items()
                     if name.endswith(".geom")}
    for name, digest in model_manifest.get("outputs", {}).items():
        if not name.endswith(".geom"):
            continue
        if name in model_sources and model_sources[name] != digest:
            raise ValueError("Official map model manifests disagree for " + name)
        model_sources[name] = digest
    resource_sources = dict(extra_model_manifest.get("resources", {}))
    for manifest_resources in (model_manifest.get("resources", {}), story_actor_manifest.get("resources", {})):
        for uri, record in manifest_resources.items():
            if uri in resource_sources and resource_sources[uri] != record:
                raise ValueError("Official map texture manifests disagree for " + uri)
            resource_sources[uri] = record
    state_texture_sources = recover_map11_state_textures() if map_id == 11 else {}
    resource_sources.update(state_texture_sources)
    skeleton_sources = spine_manifest.get("skeleton_outputs", {})
    models = {}
    entities = []
    textures = {}
    geometry_outputs = {}
    spine_outputs = {}
    spine_payloads = {}
    verified_story_actor_payloads = None

    def export_geometry(key: str, source_uri: str, batch_count: int) -> str | None:
        if not source_uri.startswith("res://assets/official/map-models/"):
            raise ValueError("Official entity geometry source is outside the pinned model catalog: " + source_uri)
        if key in geometry_outputs:
            previous = geometry_outputs[key]
            if previous["source"] != source_uri.removeprefix("res://") or previous["batchCount"] != batch_count:
                raise ValueError("Repeated official entity state has conflicting geometry: " + key)
            return f"Map{map_id}/EntityGeometry/{key}.geom" if batch_count else None
        source = ROOT / source_uri.removeprefix("res://")
        raw = source.read_bytes()
        if model_sources.get(source.name) != sha256(raw):
            raise ValueError("Official entity geometry differs from its source manifest: " + source_uri)
        if batch_count == 0:
            if raw != b"ZJMAP001\x00\x00\x00\x00":
                raise ValueError("Official empty entity geometry header differs: " + source_uri)
            batches = []
        else:
            batches = decode(raw, expected_version=geometry_version(raw))
        if len(batches) != batch_count:
            raise ValueError("Official entity geometry batch count differs: " + source_uri)
        geometry_outputs[key] = {
            "source": source.relative_to(ROOT).as_posix(),
            "sha256": sha256(raw),
            "path": f"{geometry_resource_dir}/{key}.geom.bytes",
            "batchCount": len(batches),
        }
        for batch in batches:
            uri = batch["metadata"]["texture"]
            texture_record = resource_sources.get(uri)
            if not isinstance(texture_record, dict):
                raise ValueError("Official entity texture is absent from the model source manifest: " + uri)
            textures[uri] = texture_record["sha256"]
        return f"Map{map_id}/EntityGeometry/{key}.geom" if batches else None

    story_placements = [{
        "Id": 0,
        "EntityId": 8402,
        "StoryOnly": True,
        "StoryModelPath": "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view",
    }] if map_id in (10, 12) else []
    exploration_path = ROOT / 'unity/Assets/Game/Resources/World/Mainline/exploration.json'
    exploration_placements = [dict(Id=0, EntityId=row['entityClassId'], StoryOnly=True)
        for row in json.loads(exploration_path.read_text(encoding='utf-8'))['entities']] if map_id in (11, 12) else []
    for placement in sorted(placements["Entities"] + story_placements + exploration_placements, key=lambda row: int(row["Id"])):
        entity_class_id = int(placement["EntityId"])
        model_path = placement.get("StoryModelPath") or resolve_display_path(
            entity_class_id, model_catalog, map_displays, map_id)
        model = model_paths.get(model_path) if model_path else None
        model_id = model.get("id") if isinstance(model, dict) else None
        geometry_resource = None
        if model is not None:
            geometry = model.get("geometry") or {}
            if int(geometry.get("batches", 0)) > 0:
                geometry_source = ROOT / "assets/official/map-models" / (model_id + ".geom")
                raw_geometry = geometry_source.read_bytes()
                if model_sources.get(geometry_source.name) != sha256(raw_geometry):
                    raise ValueError("Map10 entity geometry differs from its source manifest: " + model_path)
                batches = decode(raw_geometry, expected_version=geometry_version(raw_geometry))
                if len(batches) != int(geometry["batches"]):
                    raise ValueError("Map10 entity geometry batch count differs: " + model_path)
                relative_geometry = f"{geometry_resource_dir}/{model_id}.geom.bytes"
                geometry_resource = f"Map{map_id}/EntityGeometry/{model_id}.geom"
                geometry_outputs[model_id] = {
                    "source": geometry_source.relative_to(ROOT).as_posix(),
                    "sha256": sha256(raw_geometry),
                    "path": relative_geometry,
                    "batchCount": len(batches),
                }
                for batch in batches:
                    uri = batch["metadata"]["texture"]
                    texture_record = resource_sources.get(uri)
                    if not isinstance(texture_record, dict):
                        raise ValueError("Map10 entity texture is absent from the model source manifest: " + uri)
                    textures[uri] = texture_record["sha256"]

            skeleton_instances = model.get("skeleton_instances") or []
            spine_refs = []
            for index, instance in enumerate(skeleton_instances):
                skeleton_id = instance.get("id")
                skeleton_uri = model_catalog.get("skeletons", {}).get(skeleton_id)
                if not isinstance(skeleton_uri, str) or not skeleton_uri.startswith("res://assets/official/map-models/spines/"):
                    raise ValueError("Map10 entity Spine binding is invalid: " + model_path)
                skeleton_source = ROOT / skeleton_uri.removeprefix("res://")
                skeleton_bytes = skeleton_source.read_bytes()
                skeleton = json.loads(skeleton_bytes.decode("utf-8"))
                skeleton_digest = skeleton_sources.get(skeleton_uri)
                verified_source = {"path": skeleton_uri.removeprefix("res://"), "sha256": sha256(skeleton_bytes)}
                if skeleton_digest is None:
                    relative_skeleton = skeleton_uri.removeprefix("res://assets/official/map-models/")
                    skeleton_digest = extra_model_manifest.get("outputs", {}).get(relative_skeleton)
                if skeleton_digest is None:
                    if verified_story_actor_payloads is None:
                        verified_story_actor_payloads = []
                        for story_path, expected_digest in story_actor_manifest.get("outputs", {}).items():
                            if not story_path.startswith("map-story-actors/") or not story_path.endswith(".json"):
                                continue
                            story_source = ROOT / "assets/official" / story_path
                            story_bytes = story_source.read_bytes()
                            if sha256(story_bytes) != expected_digest:
                                raise ValueError("Official Map10 story actor differs from its source manifest: " + story_path)
                            story_payload = json.loads(story_bytes.decode("utf-8"))
                            verified_story_actor_payloads.append((story_path, expected_digest, story_payload))
                    matches = [(story_path, expected_digest) for story_path, expected_digest, story_payload
                               in verified_story_actor_payloads
                               if {key: value for key, value in story_payload.items() if key != "bone_frames"}
                               == skeleton]
                    if len(matches) != 1:
                        raise ValueError("Map10 entity Spine has no unique verified official frame source: " + model_path)
                    verified_source = {"path": "assets/official/" + matches[0][0], "sha256": matches[0][1],
                                       "equivalentFramePayload": True}
                if skeleton_digest != sha256(skeleton_bytes):
                    if not verified_source.get("equivalentFramePayload"):
                        raise ValueError("Map10 entity Spine differs from its source manifest: " + model_path)
                frame_pages = {attachment.get("page") for frame in skeleton.get("frames", []) for attachment in frame}
                if len(frame_pages) != 1:
                    raise ValueError("Map10 entity Spine page set is incomplete or ambiguous: " + model_path)
                page = next(iter(frame_pages))
                texture_uri = skeleton.get("textures", {}).get(page)
                texture_record = resource_sources.get(texture_uri)
                if not isinstance(texture_uri, str) or not isinstance(texture_record, dict):
                    raise ValueError("Map10 entity Spine texture is absent from the model source manifest: " + model_path)
                texture_source = ROOT / texture_uri.removeprefix("res://")
                if sha256(texture_source.read_bytes()) != texture_record.get("sha256"):
                    raise ValueError("Map10 entity Spine texture differs from its source manifest: " + model_path)
                textures[texture_uri] = texture_record["sha256"]
                texture_resource = f"Map{map_id}/Textures/" + texture_source.stem
                spine_key = f"{entity_class_id}-{index}"
                payload = spine_payload(map_id, model_path, entity_class_id, index, instance, skeleton, texture_resource)
                payload_bytes = json_bytes(payload)
                spine_path = f"{spine_resource_dir}/{spine_key}.json"
                spine_outputs[spine_key] = {
                    "source": skeleton_uri.removeprefix("res://"),
                    "sourceSha256": sha256(skeleton_bytes),
                    "verifiedSource": verified_source,
                    "path": spine_path,
                    "sha256": sha256(payload_bytes),
                    "page": page,
                    "textureUri": texture_uri,
                    "textureSha256": texture_record["sha256"],
                }
                if spine_key in spine_payloads and spine_payloads[spine_key] != payload_bytes:
                    raise ValueError("Repeated Map10 entity Spine key has different payloads: " + spine_key)
                spine_payloads[spine_key] = payload_bytes
                spine_refs.append({"key": spine_key, "resource": f"Map{map_id}/Spines/" + spine_key})

            for interaction_state, rules in (model.get("interactions") or {}).items():
                if not isinstance(rules, list):
                    raise ValueError("Map10 entity interaction rules are invalid: " + model_path + ":" + str(interaction_state))
            state_bindings = []
            if map_id in (11, 12):
                for state, variant in sorted((model.get("states") or {}).items(), key=lambda pair: int(pair[0])):
                    state_spines = variant.get("skeleton_instances") or []
                    if state_spines and state_spines != skeleton_instances:
                        raise ValueError("Official entity state requires distinct Spine bindings: " + model_path + ":" + state)
                    batch_count = int(variant.get("geometry", {}).get("batches", 0))
                    state_bindings.append({
                        "state": int(state),
                        "geometryResource": export_geometry(model_id + "-state-" + state, variant["file"], batch_count),
                        "geometryBatchCount": batch_count,
                        "spines": spine_refs if state_spines else [],
                    })
            models.setdefault(model_path, {
                "modelPath": model_path,
                "modelId": model_id,
                "geometryResource": geometry_resource,
                "geometryBatchCount": int(geometry.get("batches", 0)),
                "spines": spine_refs,
                "interactionsJson": compact_json(model.get("interactions") or {}),
                "monsterSlotsJson": compact_json(model.get("monster_slots") or []),
                "overlapDisplay": bool(model.get("overlap_display", False)),
                **({"states": state_bindings} if state_bindings else {}),
            })
            if models[model_path]["spines"] != spine_refs:
                raise ValueError("Repeated Map10 entity display produced different Spine bindings: " + model_path)

        stage_ids = stage_ids_for_entity(placement, components)
        if map_id == 10 and model_path and "/WorldMonster/" in model_path:
            for stage_id in stage_ids:
                if stage_id not in stage_rows:
                    raise ValueError("Map10 monster entity references a missing official battle stage: " + str(stage_id))
        if placement.get("StoryOnly"):
            continue
        entities.append({
            "placementId": int(placement["Id"]),
            "entityClassId": entity_class_id,
            "typeId": int(placement["TypeId"]),
            "grid": [int(value) for value in placement["Grid"]],
            "region": int(placement["Region"]),
            "direction": int(placement["Direction"]),
            "unique": bool(placement["Unique"]),
            "area": int(placement["Area"]),
            "state": (components.get(str(placement["Id"]), {}).get("LoWorldStageComponent") or {}).get("State", 0),
            "active": (components.get(str(placement["Id"]), {}).get("LoWorldActiveableEntityComponent") or {}).get("Active", True),
            **entity_runtime_rules(int(placement["Id"]), components),
            "modelPath": model_path,
            "stageIds": stage_ids,
            "stageBindings": stage_behavior_bindings(int(placement["Id"]), components),
        })

    output_data = {
        "manifest": VERSION,
        "mapClassId": map_id,
        "entities": entities,
        "models": list(models.values()),
        "missingDisplayEntityClassIds": sorted({entity["entityClassId"] for entity in entities if entity["modelPath"] is None}),
        "geometryFiles": geometry_outputs,
        "spineFiles": spine_outputs,
        "textureUris": sorted(textures),
    }
    payload_bytes = json_bytes(output_data)
    source_manifest = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": map_id,
        "operation": f"Resolve the original Map{map_id} EntityDisplay prefab view for each placed entity, preferring Map/Map_{map_id} over Map/Common; copy source geometry, texture, Spine frame, interaction, lock, item-award, and map-change data byte-for-byte or by schema-preserving serialization. Map11 also preserves every exported official stage-state geometry and its Spine bindings.",
        "inputs": {
            "map_definition": source_digest(map_data_path),
            "map_entity_components": source_digest(components_path),
            "map_model_catalog": source_digest(MODEL_CATALOG),
            "official_map_entity_display_table": {
                "path": ADVENTURE_DATA.relative_to(ROOT).as_posix(),
                "sha256": sha256(adventure_bytes),
            },
            "map_model_source_manifest": source_digest(MODEL_SOURCE_MANIFEST),
            "map_extra_model_source_manifest": source_digest(EXTRA_MODEL_SOURCE_MANIFEST),
            **({"world_exploration": source_digest(exploration_path)} if map_id in (11, 12) else {}),
            "map_spine_source_manifest": source_digest(SPINE_SOURCE_MANIFEST),
            "map_story_actor_source_manifest": source_digest(STORY_ACTOR_SOURCE_MANIFEST),
            **({"battle_data": source_digest(BATTLE_DATA),
                "battle_source_manifest": source_digest(BATTLE_SOURCE_MANIFEST)} if map_id == 10 else {}),
        },
        "geometry": geometry_outputs,
        "spines": spine_outputs,
        "textures": [{"uri": uri, "sha256": textures[uri]} for uri in sorted(textures)],
        **({"additionalStateTextureSources": state_texture_sources} if state_texture_sources else {}),
        "output": {"path": output_name, "resourcePath": f"Map{map_id}/Entities/catalog",
                   "resourceSha256": sha256(payload_bytes), "size": len(payload_bytes),
                   "sha256": sha256(payload_bytes)},
    }
    output.mkdir(parents=True, exist_ok=True)
    output_path = output / output_name
    source_path = output / SOURCE_MANIFEST_NAME
    entity_catalog_path = output / entity_catalog_resource
    geometry_root = output / geometry_resource_dir
    spine_root = output / spine_resource_dir
    texture_root = output / texture_resource_dir
    unchanged_placements = False
    if source_path.exists():
        previous = json.loads(source_path.read_text(encoding="utf-8"))
        previous_output = output / previous['output']['path']
        if sha256(previous_output.read_bytes()) != previous['output']['sha256']:
            raise ValueError('Previous entity payload differs from its verified source')
        unchanged_placements = json.loads(previous_output.read_bytes())['entities'] == entities
        old_definition = previous['inputs'].get('map_definition')
        if old_definition != source_manifest['inputs']['map_definition'] and unchanged_placements:
            source_manifest['unchangedPlacementInputRefresh'] = dict(previous=old_definition,
                current=source_manifest['inputs']['map_definition'], placementCount=len(entities),
                previousManifestSha256=sha256(source_path.read_bytes()),
                operation='All converted placement fields are identical to the verified previous entity payload; retain every original placement and update the map definition input receipt.')
    manifest_bytes = json_bytes(source_manifest)
    previous_hashes = verified_previous_outputs(output, source_manifest["inputs"], map_id, unchanged_placements)
    for model_id, record in geometry_outputs.items():
        source = ROOT / record["source"]
        target = output / record["path"]
        write_generated(target, source.read_bytes(), previous_hashes)
    for spine_key, record in spine_outputs.items():
        target = output / record["path"]
        write_generated(target, spine_payloads[spine_key], previous_hashes)
    for uri, expected_sha in textures.items():
        source = ROOT / uri.removeprefix("res://")
        data = source.read_bytes()
        if sha256(data) != expected_sha:
            raise ValueError("Map10 entity texture differs from its source manifest: " + uri)
        target = texture_root / Path(uri).name
        write_generated(target, data, previous_hashes)
    write_generated(output_path, payload_bytes, previous_hashes)
    write_generated(entity_catalog_path, payload_bytes, previous_hashes)
    write_generated(source_path, manifest_bytes, previous_hashes)

    if output.is_relative_to((ROOT / "unity").resolve()):
        for directory in (output / "Resources", output / f"Resources/Map{map_id}", output / f"Resources/Map{map_id}/Entities",
                          geometry_root, spine_root, texture_root):
            directory.mkdir(parents=True, exist_ok=True)
            write_meta(directory, "DefaultImporter")
        for path in list(geometry_root.glob("*.geom.bytes")):
            write_meta(path, "TextScriptImporter")
        for path in spine_root.glob("*.json"):
            write_meta(path, "TextScriptImporter")
        for path in texture_root.glob("*.png"):
            write_texture_meta(path)
        write_meta(output_path, "TextScriptImporter")
        write_meta(entity_catalog_path, "TextScriptImporter")
        write_meta(source_path, "TextScriptImporter")
    return source_manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, choices=(10, 11, 12), default=10)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    output = (args.output or ROOT / "unity/Assets/Official/World/Map" / str(args.map_id)).resolve()
    manifest = build(output, args.map_id)
    data = json.loads((output / f"map{args.map_id}-entities.json").read_text(encoding="utf-8"))
    print(f"Map{args.map_id} entity placements:", len(data["entities"]))
    print(f"Map{args.map_id} entity displays:", len(data["models"]))
    print(f"Map{args.map_id} entity geometries:", len(data["geometryFiles"]))
    print(f"Map{args.map_id} entity Spine instances:", len(data["spineFiles"]))
    print(f"Map{args.map_id} entity textures:", len(data["textureUris"]))
    print("Entity data SHA256:", manifest["output"]["sha256"])


if __name__ == "__main__":
    main()
