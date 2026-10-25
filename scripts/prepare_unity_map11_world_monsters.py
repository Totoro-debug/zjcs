"""Record the pinned Map11 dynamic monster model and stage bindings."""
from __future__ import annotations

import hashlib
import io
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from official_apk import Apk
from official_apk import canonical_bundle_name
from official_unity_schema import ScriptSchemas

ENTITY_DATA = ROOT / "unity/Assets/Official/World/Map/11/map11-entities.json"
BATTLE_DATA = ROOT / "unity/Assets/Official/World/Map/11/map11-battles.json"
MODEL_IDENTITIES = ROOT / "unity/Assets/Official/Combat/Presentation/model-identities.json"
MODEL_CATALOG = ROOT / "unity/Assets/Game/Resources/Combat/OfficialBattleModelCatalog.asset"
RUNTIME_BINDING = ROOT / "unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.WorldMonsters.cs"
RUNTIME_ENTRY = ROOT / "unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.cs"
FOLLOWER_RUNTIME = ROOT / "unity/Assets/Spine/Runtime/spine-unity/Components/Following/BoneFollower.cs"
OUTPUT = ROOT / "assets/reference/official-map/map11-world-monsters-source-manifest.json"
CONVERTER = ROOT / "scripts/prepare_unity_map11_world_monsters.py"
SHADOW_PNG = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11/Textures/MonsterShadow.png"
SHADOW_META = SHADOW_PNG.with_suffix(".png.meta")

SOURCE_CODE = (
    ".tools/source-packages/official-code/client-source/ECStageBehStageView.cs",
    ".tools/source-packages/official-code/client-source/MapMonsterLoader.cs",
    ".tools/source-packages/official-code/client-source/MapMonsterLoadHandler.cs",
    ".tools/source-packages/official-code/client-source/BaseSkeletonFollower.cs",
    ".tools/source-packages/official-code/client-source/SkeletonRendererFollower.cs",
    ".tools/source-packages/official-code/common-source/Common/FormulaHandler.cs",
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_record(path: Path) -> dict[str, str]:
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())}


def vector(value: object, axes: str = "xyz") -> list[float]:
    return [float(getattr(value, axis)) for axis in axes]


def rectangle(value: object) -> list[float]:
    return [float(value.x), float(value.y), float(value.width), float(value.height)]


def local_pointer(owner: object, pointer: dict[str, int]) -> object:
    if pointer["m_FileID"] != 0:
        raise ValueError("Expected a local transform reference in the Map11 world monster prefab")
    return owner.assets_file.objects[pointer["m_PathID"]]


def main() -> None:
    entities = json.loads(ENTITY_DATA.read_text(encoding="utf-8"))
    battles = json.loads(BATTLE_DATA.read_text(encoding="utf-8"))
    identities = json.loads(MODEL_IDENTITIES.read_text(encoding="utf-8"))
    model_keys = {row["monster_id"]: row["key"] for row in identities["monster_models"]}
    rosters = {row["StageId"]: row for row in battles["monsterRosters"]}

    bindings = []
    for entity in entities["entities"]:
        if entity["entityClassId"] != 5110:
            continue
        stage_bindings = [row for row in entity.get("stageBindings", [])
            if row.get("Component") == "LoStageBehStageComponent"]
        if len(stage_bindings) != 1 or len(stage_bindings[0].get("StageIds", [])) != 1:
            raise ValueError(f"Map11 placement {entity['placementId']} has an ambiguous stage binding")
        stage_id = stage_bindings[0]["StageIds"][0]
        roster = rosters.get(stage_id)
        if roster is None:
            raise ValueError(f"Map11 placement {entity['placementId']} has no roster for stage {stage_id}")
        monster_ids = {unit["MonsterId"] for wave in roster["Waves"] for unit in wave["Units"]}
        if len(monster_ids) != 1:
            raise ValueError(f"Map11 stage {stage_id} requires Formula.GetMainMonster tie-break inputs")
        monster_id = next(iter(monster_ids))
        if monster_id not in model_keys:
            raise ValueError(f"Official battle model identity is missing for monster {monster_id}")
        bindings.append({
            "placement_id": entity["placementId"],
            "entity_class_id": entity["entityClassId"],
            "grid": entity["grid"],
            "stage_id": stage_id,
            "roster_monster_ids": sorted(monster_ids),
            "monster_id": monster_id,
            "battle_model_key": model_keys[monster_id],
            "world_animation": "idle_01",
        })

    if len(bindings) != 6:
        raise ValueError(f"Expected six Map11 class 5110 placements, found {len(bindings)}")

    apk = Apk(manifest="854_156383")
    schemas = ScriptSchemas()
    prefabs = []
    for monster_id in sorted({row["monster_id"] for row in bindings}):
        bundle_name = f"defaultpackage_assets_prefabs_appearance_monster_{monster_id}.bundle"
        suffix = f"MonsterModel_{monster_id}.prefab"
        objects = [obj.deref() for path, obj in apk.container(bundle_name)
            if path.endswith(suffix) and obj.deref().type.name == "GameObject"]
        if len(objects) != 1:
            raise ValueError(f"Expected one official MonsterModel_{monster_id} prefab root, found {len(objects)}")
        game_object = objects[0]
        data = game_object.read()
        if data.m_Name != f"MonsterModel_{monster_id}":
            raise ValueError(f"Unexpected world monster prefab root name for {monster_id}: {data.m_Name}")
        scale = data.m_Transform.deref().read().m_LocalScale
        scale_values = vector(scale)
        if any(abs(actual - expected) > 1e-6
            for actual, expected in zip(scale_values, [0.3, 0.3, 1.0])):
            raise ValueError(f"Official world monster scale changed for {monster_id}: {scale_values}")
        prefabs.append({
            "monster_id": monster_id,
            "source_path": f"Assets/Prefabs/Appearance/Monster/{monster_id}/{suffix}",
            "provenance": apk.provenance(game_object),
            "root_local_scale": scale_values,
            "game_object_raw_sha256": sha256(game_object.get_raw_data()),
        })

    world_bundle = "defaultpackage_assets_prefabs_ecentity_mapworld_worldmonster.bundle"
    world_path = "Assets/Prefabs/EcEntity/MapWorld/WorldMonster/5110.prefab"
    world_roots = [obj.deref() for path, obj in apk.container(world_bundle)
        if path == world_path and obj.deref().type.name == "GameObject"]
    if len(world_roots) != 1:
        raise ValueError(f"Expected the official Map11 class 5110 prefab at {world_path}")
    world_root = world_roots[0]
    loader = None
    loader_data = None
    for obj in apk.objects(world_bundle):
        if obj.type.name != "MonoBehaviour":
            continue
        script = obj.read(check_read=False).m_Script.read()
        if script.m_ClassName != "MapMonsterLoader":
            continue
        fields = schemas.read(obj)
        owner_pointer = fields["m_GameObject"]
        owner = obj.assets_file.objects[owner_pointer["m_PathID"]]
        while owner is not None:
            owner_data = owner.read()
            if owner_data.m_Name == "5110":
                loader = obj
                loader_data = fields
                break
            transform_ptr = owner_data.m_Transform
            if transform_ptr is None or transform_ptr.deref() is None:
                break
            parent_ptr = transform_ptr.deref().read().m_Father
            if parent_ptr is None or parent_ptr.path_id == 0:
                break
            parent_transform = parent_ptr.deref()
            owner = None if parent_transform is None else parent_transform.read().m_GameObject.deref()
        if loader is not None:
            break
    if loader is None or loader_data is None:
        raise ValueError("MapMonsterLoader was not found under the official 5110 prefab root")

    def transform_record(transform_obj: object) -> dict[str, object]:
        data = transform_obj.read()
        game_object = data.m_GameObject.deref()
        return {
            "name": game_object.read().m_Name,
            "local_position": vector(data.m_LocalPosition),
            "local_rotation": vector(data.m_LocalRotation, "xyzw"),
            "local_scale": vector(data.m_LocalScale),
        }

    model_slot = local_pointer(loader, loader_data["Slot"])
    shadow_slot = local_pointer(loader, loader_data["mShadowSlot"])
    model_parent = model_slot.read().m_Father.deref()
    if model_parent is None or transform_record(model_parent)["name"] != "Model":
        raise ValueError("Official Map11 world monster model slot parent differs from Model")
    if transform_record(model_slot)["name"] != "ModelSlot" or transform_record(shadow_slot)["name"] != "Shadow":
        raise ValueError("Official Map11 world monster attachment slots changed")
    model_parent_record = transform_record(model_parent)
    if any(abs(actual - expected) > 1e-6
        for actual, expected in zip(model_parent_record["local_position"], [0.0, -0.5, -0.5])):
        raise ValueError("Official Map11 world monster Model mount offset changed")

    shadow_pointer = loader_data["ShadowPrefab"]
    shadow_file = loader.assets_file.externals[shadow_pointer["m_FileID"] - 1]
    shadow_record = apk.cabs[shadow_file.path.rsplit("/", 1)[-1].lower()]
    shadow_bundle = canonical_bundle_name(shadow_record, apk.index)
    shadow_root = next(obj for obj in apk.objects(shadow_bundle)
        if obj.path_id == shadow_pointer["m_PathID"])
    if shadow_root.type.name != "GameObject" or shadow_root.read().m_Name != "MonsterShadow":
        raise ValueError("Official Map11 MonsterShadow binding changed")
    follower = next(pointer.deref() for pointer in shadow_root.read().m_Components
        if pointer.deref().type.name == "MonoBehaviour"
        and pointer.deref().read(check_read=False).m_Script.read().m_ClassName == "SkeletonRendererFollower")
    follower_data = schemas.read(follower)
    if follower_data["BoneName"] != "shadow":
        raise ValueError("Official MonsterShadow no longer follows the shadow bone")
    shadow_child = next(pointer.deref().read().m_GameObject.deref()
        for pointer in shadow_root.read().m_Transform.deref().read().m_Children
        if pointer is not None and pointer.deref() is not None)
    sprite_renderer = next(pointer.deref() for pointer in shadow_child.read().m_Components
        if pointer.deref().type.name == "SpriteRenderer")
    renderer_data = sprite_renderer.read()
    sprite_pointer = renderer_data.m_Sprite
    sprite_file = sprite_renderer.assets_file.externals[sprite_pointer.m_FileID - 1]
    sprite_record = apk.cabs[sprite_file.path.rsplit("/", 1)[-1].lower()]
    sprite_bundle = canonical_bundle_name(sprite_record, apk.index)
    sprite = next(obj for obj in apk.objects(sprite_bundle) if obj.path_id == sprite_pointer.m_PathID)
    sprite_data = sprite.read()
    texture = sprite_data.m_RD.texture.deref()
    image = texture.read().image
    if image is None or image.size != (717, 272):
        raise ValueError(f"Official MonsterShadow texture dimensions changed: {None if image is None else image.size}")
    output = io.BytesIO()
    image.save(output, format="PNG")
    shadow_png = output.getvalue()
    SHADOW_PNG.write_bytes(shadow_png)

    payload = {
        "format": 1,
        "source_version": "854_156383",
        "rule": "ECStageBehStageView.GetMonsterId calls Formula.GetMainMonster; each mapped roster contains one MonsterId identity, so the original ranking resolves to that ID.",
        "converter": file_record(CONVERTER),
        "source_code": [file_record(ROOT / path) for path in SOURCE_CODE],
        "input_files": [
            file_record(ENTITY_DATA),
            file_record(BATTLE_DATA),
            file_record(MODEL_IDENTITIES),
            file_record(MODEL_CATALOG),
            file_record(RUNTIME_BINDING),
            file_record(RUNTIME_ENTRY),
            file_record(FOLLOWER_RUNTIME),
            file_record(SHADOW_META),
            file_record(SHADOW_PNG),
        ],
        "world_entity_prefab": {
            "source_path": world_path,
            "provenance": apk.provenance(world_root),
            "game_object_raw_sha256": sha256(world_root.get_raw_data()),
            "loader_provenance": apk.provenance(loader),
            "loader_raw_sha256": sha256(loader.get_raw_data()),
        },
        "attachment_slots": {
            "model_mount_parent": model_parent_record,
            "model_slot": transform_record(model_slot),
            "shadow_slot": transform_record(shadow_slot),
        },
        "world_model_prefabs": prefabs,
        "shadow_prefab": {
            "name": shadow_root.read().m_Name,
            "provenance": apk.provenance(shadow_root),
            "game_object_raw_sha256": sha256(shadow_root.get_raw_data()),
            "follower_component": {
                "source_code": SOURCE_CODE[3:5],
                "serialized_fields": {key: follower_data[key] for key in (
                    "BoneName", "FollowZSpacing", "ZFront", "FollowLocalScale", "FollowBoneWorldFlip",
                    "FollowRotation", "FollowSkeletonTransformFlipX", "InitializeOnAwake")},
            },
            "sprite_renderer": {
                "sorting_order": renderer_data.m_SortingOrder,
                "enabled": bool(renderer_data.m_Enabled),
                "root_local_scale": vector(shadow_child.read().m_Transform.deref().read().m_LocalScale),
                "sprite_provenance": apk.provenance(sprite),
                "sprite_raw_sha256": sha256(sprite.get_raw_data()),
                "pixels_per_unit": sprite_data.m_PixelsToUnits,
                "pivot": vector(sprite_data.m_Pivot, "xy"),
                "rect": rectangle(sprite_data.m_Rect),
                "texture_rect": rectangle(sprite_data.m_RD.textureRect),
            },
            "texture_output": {
                "path": SHADOW_PNG.relative_to(ROOT).as_posix(),
                "source_provenance": apk.provenance(texture),
                "source_raw_sha256": sha256(texture.get_raw_data()),
                "size": list(image.size),
                "mode": image.mode,
                "output_sha256": sha256(shadow_png),
            },
        },
        "runtime_bindings": bindings,
    }
    payload_bytes = json.dumps(payload, ensure_ascii=False, sort_keys=True,
        separators=(",", ":")).encode("utf-8")
    result = dict(payload, output_sha256=sha256(payload_bytes))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT.relative_to(ROOT)}: {len(bindings)} placements, "
        f"{len(prefabs)} official monster prefabs, shadow texture {image.size}, "
        f"sha256={result['output_sha256']}")


if __name__ == "__main__":
    main()
