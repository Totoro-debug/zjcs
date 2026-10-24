"""Stage the Home scene, UI, rules and actor payloads read by the Unity home runtime.

The payloads under ``assets/official`` are already extracted from the fixed
``854_156383`` client.  This tool only merges, trims and copies them into
``unity/Assets/StreamingAssets/Home`` together with every sprite those payloads
reference, so the runtime loads exactly what the official prefabs and scene use.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import shutil
import tempfile
from pathlib import Path

from home_ui_animation import normalize_home_ui_animations

ROOT = Path(__file__).resolve().parents[1]
OFFICIAL = ROOT / "assets" / "official"
OUTPUT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Home"
SPRITE_DIR = OUTPUT / "sprites"
FIXED_REWARD_TABLE = ROOT / ".tools" / "source-packages" / "official-data" / "tables.json"
WISH_SOURCE_MANIFEST = ROOT / "assets" / "reference" / "wishes" / "source-manifest.json"
HOME_PLAYER_SOURCE_MANIFEST = ROOT / "assets" / "reference" / "home-player" / "source-manifest.json"
HOME_TUTORIAL_FIXED_REWARD_ID = 7
# The pinned ParticleSystem type tree has these modules enabled; the actor payload omits the flags.
HOME_METEOR_ENABLED_MODULES = {
    "SizeModule": True,
    "VelocityModule": True,
    "ClampVelocityModule": True,
    "ColorModule": True,
}

# Layers of ``assets/official/home.json`` that belong to a furniture prefab and
# are therefore rebuilt from the richer per-state trees in
# ``assets/official/home-completion/scene.json``.
FURNITURE_ROOTS = {
    "Bed", "Desk", "DiningTable", "Danlu", "StatueOfGod", "PostBox",
    "StorageBox", "VendingMachine", "GachaChest", "Cart", "AstrologicalDestiny",
    "Airship",
}

# Panels read by the runtime.  ``home-completion`` holds the furniture dialogs,
# ``ui.json`` the shared HUD trees, the mail payload its own window and the
# tutorial payloads the talk bubbles.
HUD_PANELS = [
    "home", "top_bar", "bottom_bar", "player_icon", "functions",
    "common_confirm", "popup", "money", "item", "button", "dialog",
    "decor", "decor_furniture", "decor_item", "decor_theme_tab",
    "decor_environment_tab", "decor_furniture_tab", "home_skin_detail",
    "bed", "cart", "fast_idle", "upgrade", "bed_rules", "bed_rule_row",
    "bed_rule_tip", "cart_rule", "cart_rule_tip", "cart_rule_reward",
    "cart_row", "cart_detail", "cart_output_row", "cart_material",
    "fast_item_row", "award_row", "awards", "awards_list", "awards_list_item",
    "repair", "repair_notify", "repair_prop", "upgrade_switch", "decor_wearing",
    "furniture_icon_0", "furniture_icon_1", "furniture_icon_2", "furniture_icon_3",
    "furniture_icon_5", "furniture_icon_7", "furniture_icon_8", "furniture_icon_9",
    "furniture_icon_10", "furniture_icon_12", "furniture_icon_13",
    "astrological_entry", "astrological", "astrological_props", "astrological_rank",
    "astrological_contract_tag", "astrological_rule", "astrological_season_1",
    "astrological_season_2", "astrological_season_3", "astrological_season_4",
    "astrological_season_5", "astrological_season_6", "character_prop_row",
]

RULE_TABLES = [
    "furniture", "furniture_level_rule", "hang_up", "fast_hang_up",
    "home_level", "furniture_skin", "home_theme_skin", "home_environment_skin",
    "home_decoration", "function_limit", "hang_up_reward",
    "hang_up_upgrade_material",
]

REWARD_RULE_IDS = {1, 2, 3, 4, 5, 6, 7, 8, 9, 100, 101, 102, 11100,
                   11000, 11001, 11002, 11003, 11004, 11005, 11006, 11007,
                   10010, 10012, 10013, 11050}

PROGRESS_SCRIPT = "OfflineHomeRuntime"


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


SPRITE_SOURCES: set[str] = set()


def relative_sprite(record):
    """Replace the extraction-time ``res://`` path with a staging file name."""
    if not isinstance(record, dict):
        return None
    file = record.get("file")
    if not isinstance(file, str) or not file:
        return None
    if file.startswith("res://"):
        SPRITE_SOURCES.add(file)
        file = file[len("res://"):]
    name = file.rsplit("/", 1)[-1]
    record["file"] = name
    return file


def trim_sprite(record):
    relative_sprite(record)
    return record


def trim_layer(layer):
    sprite = trim_sprite(layer.get("sprite"))
    result = {
        "path": layer.get("path", ""),
        "sprite": sprite,
        "vertices": [value for point in (layer.get("vertices") or []) for value in point],
        "color": layer.get("color"),
        "sorting_layer": layer.get("sorting_layer", 0),
        "sorting_order": layer.get("sorting_order", 0),
        "depth": layer.get("depth", 0.0),
    }
    materials = layer.get("materials") or []
    if materials:
        result["materials"] = [
            {
                "name": material.get("name", ""),
                "shader": material.get("shader", ""),
                "colors": material.get("colors") or {},
            }
            for material in materials
        ]
    return result


def trim_node(node):
    """Keep the UGUI properties the runtime builder understands."""
    if not isinstance(node, dict):
        return node
    keep = (
        "name", "path", "active", "anchor_min", "anchor_max", "anchored_position",
        "size_delta", "pivot", "scale", "rotation", "scripts", "image", "text",
        "localization_key", "mask", "mask_component_flags", "mask_enabled", "mask_show_graphic",
        "has_tab_group", "tab_group_allow_switch_off", "no_drawing_raycast", "raycast_target", "raycast_padding",
        "rectmask2d", "layout", "grid", "size_fitter",
        "layout_element", "aspect_fitter", "alpha", "button", "scroll", "animations", "children",
        "toggle",
    )
    result = {key: node[key] for key in keep if key in node}
    if "animations" in result:
        result["animations"] = normalize_home_ui_animations(result["animations"])
    if isinstance(result.get("image"), dict):
        image = result["image"]
        trim_sprite(image.get("sprite"))
        result["image"] = image
    children = node.get("children")
    result["children"] = [trim_node(child) for child in children] if isinstance(children, list) else []
    return result


def trim_prefab(panel):
    if not isinstance(panel, dict):
        return panel
    result = {key: value for key, value in panel.items() if key != "root"}
    root = panel.get("root")
    if not isinstance(root, dict) and isinstance(panel.get("path"), str):
        # The wish tutorial payload stores each prefab root directly, unlike
        # the wrapped panels in the home, mail, and talk payloads.
        root = panel
    if isinstance(root, dict):
        result["root"] = trim_node(root)
    return result


def flatten_panel(panel):
    root = panel.get("root")
    if not isinstance(root, dict):
        raise ValueError("Home UI panel is missing its root node: " + str(panel.get("key")))

    root_record = {key: value for key, value in root.items() if key != "children"}
    nodes = []

    def append_children(parent_path, parent):
        for child in parent.get("children") or []:
            record = {key: value for key, value in child.items() if key != "children"}
            record["parent_path"] = parent_path
            nodes.append(record)
            append_children(child.get("path", ""), child)

    append_children(root.get("path", ""), root)
    return {"key": panel.get("key", ""), "root": root_record, "nodes": nodes}


def collect_sprites(node, files):
    if isinstance(node, dict):
        for key, value in node.items():
            if key in ("file", "page") and isinstance(value, str) and value:
                files.add(value)
            else:
                collect_sprites(value, files)
    elif isinstance(node, list):
        for value in node:
            collect_sprites(value, files)


def load_fruit_tree():
    source = read(OFFICIAL / "fruit-tree.json")
    instance = (source.get("skeleton_instances") or [])[0]
    matrix = instance["matrix"]
    skeletons = source.get("skeletons") or {}
    animations = []
    for name in ("loop_1", "loop_2", "loop_3"):
        skeleton = skeletons.get("fruit-tree:" + name)
        if not skeleton:
            raise ValueError("Official fruit tree animation is missing: " + name)
        textures = skeleton.get("textures") or {}
        frames = []
        for source_frame in skeleton.get("frames") or []:
            meshes = []
            for layer in source_frame:
                page = layer.get("page", "")
                page_source = textures.get(page, "")
                if page_source:
                    SPRITE_SOURCES.add(page_source)
                meshes.append({
                    "page": page_source.rsplit("/", 1)[-1],
                    "order": layer.get("order", 0),
                    "vertices": layer.get("vertices") or [],
                    "uv": layer.get("uv") or [],
                    "triangles": layer.get("triangles") or [],
                    "color": layer.get("color") or [1, 1, 1, 1],
                    "blend": layer.get("blend", 0),
                })
            frames.append({"meshes": meshes})
        animations.append({
            "name": name,
            "duration": skeleton.get("duration", 0),
            "fps": skeleton.get("fps", 12),
            "scale": skeleton.get("scale", 0.01),
            "frames": frames,
        })

    matrices = source.get("object_matrices") or {}
    fruit_slots = []
    for index in range(1, 11):
        key = "/FruitTree/Fruits/Fruit" + ("" if index == 1 else " (" + str(index) + ")")
        slot_matrix = matrices.get(key)
        if slot_matrix is None:
            raise ValueError("Official fruit tree slot is missing: " + key)
        fruit_slots.append({
            "path": key,
            "position": [slot_matrix[0][3], slot_matrix[1][3], slot_matrix[2][3]],
            "scale": [slot_matrix[0][0], slot_matrix[1][1]],
        })

    states = []
    raw_states = source.get("states") or {}
    root = raw_states.get("root", {}).get("states", {})
    for state, paths in root.items():
        states.append({"key": "root:" + str(state), "paths": paths})
    for furniture_type in (4, 11):
        data = raw_states.get(str(furniture_type), {}).get("states", {})
        for state, paths in data.items():
            states.append({"key": str(furniture_type) + ":" + str(state), "paths": paths})

    state_animations = [
        {"state": int(state), "name": name}
        for state, name in sorted((source.get("animations") or {}).items(), key=lambda value: int(value[0]))
    ]

    fruit_effect = source["fruit_effect"]["layers"][0]
    return {
        "tree_position": [matrix[0][3], matrix[1][3], matrix[2][3]],
        "tree_scale": matrix[0][0],
        "animations": animations,
        "fruit_effect": trim_layer(fruit_effect),
        "fruit_slots": fruit_slots,
        "layers": [trim_layer(layer) for layer in source.get("layers", [])],
        "states": states,
        "state_animations": state_animations,
    }


def load_scene():
    home = read(OFFICIAL / "home.json")
    completion = read(OFFICIAL / "home-completion" / "scene.json")
    fruit_tree = load_fruit_tree()
    background = [trim_layer(layer) for layer in home["layers"]]
    furniture = []
    for key in sorted(completion["furniture"], key=lambda value: int(value)):
        record = completion["furniture"][key]
        furniture.append({
            "type": record.get("type", int(key)),
            "name": record.get("name", ""),
            "slot": record.get("slot"),
            "layers": [trim_layer(layer) for layer in record.get("layers", [])],
        })
    outdoor = [
        {"id": int(key), "name": value.get("name", ""), "position": value.get("position")}
        for key, value in home.get("outdoor_slots", {}).items()
    ]
    click_regions = [
        {"path": region.get("path", ""),
         "vertices": [value for point in (region.get("vertices") or []) for value in point]}
        for region in home.get("click_regions", [])
    ]
    known_clicks = {value["path"] for value in click_regions}
    for region in read(OFFICIAL / "fruit-tree.json").get("click_regions", []):
        path = region.get("path", "")
        if path == "/FruitTree/touch" or path in known_clicks:
            continue
        click_regions.append({
            "path": path,
            "vertices": [value for point in (region.get("vertices") or []) for value in point],
        })
        known_clicks.add(path)
    return {
        "version": home.get("version", "854_156383"),
        "camera": home["camera"],
        "background": background,
        "furniture": furniture,
        "click_regions": click_regions,
        "outdoor_slots": outdoor,
        "fruit_tree": fruit_tree,
        "strings": [
            {"key": key, "value": value}
            for key, value in (completion.get("strings") or {}).items()
        ],
    }


def flatten_texts(source):
    result = []
    for key, value in source.items():
        if isinstance(value, dict):
            text = value.get("value", "")
            color = value.get("color")
        else:
            text, color = value, None
        result.append({"key": key, "value": text, "color": color})
    return result


def load_ui():
    panels = {}
    texts = {}
    dynamic = {}
    for source in (
        OFFICIAL / "home-completion" / "ui.json",
        OFFICIAL / "ui.json",
    ):
        data = read(source)
        source_panels = data.get("panels")
        if not isinstance(source_panels, dict):
            # ui.json stores panels directly under the top-level keys.
            source_panels = {
                key: value for key, value in data.items()
                if isinstance(value, dict) and isinstance(value.get("root"), dict)
            }
        for key, panel in source_panels.items():
            if key in panels:
                continue
            panels[key] = trim_prefab(panel)
        for key, value in (data.get("texts") or {}).items():
            texts.setdefault(key, value)
        for key, value in (data.get("dynamic_sprites") or {}).items():
            dynamic.setdefault(key, trim_sprite(value))

    mail = read(OFFICIAL / "home-mail" / "ui.json")
    for key, panel in (mail.get("panels") or {}).items():
        panels.setdefault("mail_" + key, trim_prefab(panel))
    texts.update(mail.get("texts") or {})

    talk = read(OFFICIAL / "home-tutorial" / "talk.json")
    for key, panel in (talk.get("panels") or {}).items():
        panels.setdefault("talk_" + key, trim_prefab(panel))

    wish = read(OFFICIAL / "home-wish-tutorial.json")
    for key, panel in (wish.get("panels") or {}).items():
        panels.setdefault("wish_" + key, trim_prefab(panel))

    selected = {}
    for key in HUD_PANELS:
        if key in panels:
            selected[key] = panels[key]
    for key, panel in panels.items():
        if key.startswith(("mail_", "talk_", "wish_")):
            selected[key] = panel
    completion = read(OFFICIAL / "home-completion" / "ui.json")
    fidelity = read(OFFICIAL / "fidelity-rules.json")
    astrological_art = fidelity.get("astrological_art", {}).get("ContractTagSetting", {})
    astrological_tag_art = []
    for prop_type, setting in astrological_art.items():
        icon = setting.get("mIconImgSpriteProxy")
        color = setting.get("mBgColor") or {}
        if not isinstance(icon, dict) or not icon.get("file"):
            raise ValueError(f"Official astrological tag icon is missing for property {prop_type}.")
        astrological_tag_art.append({
            "property_type": int(prop_type),
            "icon": trim_sprite(dict(icon)),
            "color": [color.get(channel, 1.0) for channel in ("r", "g", "b", "a")],
        })
    return {
        "version": "854_156383",
        "panels": [
            flatten_panel({"key": key, "root": panel.get("root")})
            for key, panel in selected.items()
        ],
        "texts": flatten_texts(texts),
        "dynamic_sprites": [
            {"key": key, "file": value.get("file")}
            for key, value in dynamic.items()
        ],
        "furniture_names": [
            {"key": key, "value": value}
            for key, value in (completion.get("furniture_names") or {}).items()
        ],
        "furniture_type_names": [
            {"key": key, "value": value}
            for key, value in (completion.get("furniture_type_names") or {}).items()
        ],
        "strings": [
            {"key": key, "value": value}
            for key, value in (wish.get("strings") or {}).items()
        ],
        "astrological_tag_art": sorted(astrological_tag_art, key=lambda value: value["property_type"]),
    }


def cost_list(cost):
    return [{"itemId": int(key), "count": value} for key, value in (cost or {}).items()]


def condition_rows(rules):
    return [
        {"classId": row.get("ClassId"), "node": row.get("Condition")}
        for row in rules.get("conditions", [])
    ]


def load_rules():
    rules = read(OFFICIAL / "rules.json")
    items = []
    icons = read(OFFICIAL / "icons.json")
    for item in rules["item"]:
        item_id = str(item.get("ClassId"))
        icon = icons.get(item_id)
        icon_file = icon.get("file") if icon else ""
        items.append({
            "id": item.get("ClassId"),
            "name": item.get("Name", ""),
            "quality": item.get("Quality", 0),
            "type": item.get("ItemType", 0),
            "icon": icon_file.rsplit("/", 1)[-1] if icon_file else "",
            "rank": item.get("Rank", -1),
            "overlap": bool(item.get("CanOverlap")),
        })
    furniture = []
    for row in rules["furniture"]:
        furniture.append({
            "classId": row.get("ClassId"),
            "type": row.get("FurnitureType"),
            "unlock": row.get("UnlockCondition"),
            "repairNotice": row.get("RepairNoticeCondition"),
            "autoRepair": row.get("AutoRepairCondition"),
            "repairCost": cost_list(row.get("RepairCost")),
            "additionalRepair": row.get("AdditionalRepairList") or [],
            "repairStory": row.get("RepairStoryName") or "",
            "canLevelUp": bool(row.get("CanLevelUp")),
        })
    hangUps = []
    for row in rules["hang_up"]:
        hangUps.append({
            "id": row.get("Id"),
            "bindFurniture": row.get("BindFurniture"),
            "sortOrder": row.get("SortOrder"),
            "levelRuleId": row.get("LevelRuleId"),
            "unlock": row.get("UnlockCondition"),
            "notice": row.get("NoticeCondition"),
            "canLevelUp": bool(row.get("CanLevelUp")),
            "useBlessingLevel": bool(row.get("UseBlessingLevel")),
            "rewardType": row.get("RewardType"),
            "rewardId": row.get("RewardId"),
            "fastHangUpId": row.get("FastHangUpId"),
            "effectPercentProp": row.get("EffectPercentPropType"),
            "effectFixedProp": row.get("EffectFixedPropType"),
            "maxStoreProp": row.get("MaxStorePropType"),
            "finallyEffectPercentProp": row.get("FinallyEffectPercentPropType"),
            "initAutoUnlock": bool(row.get("InitAutoUnlock")),
            "initialExtraRewardSeconds": row.get("InitialExtraRewardSeconds"),
            "icon": row.get("Icon") or "",
        })
    rewards = []
    for row in rules["hang_up_reward"]:
        rewards.append({
            "id": row.get("Id"),
            "order": row.get("Order", -1),
            "condition": row.get("Condition"),
            "rewardRuleId": row.get("RewardRuleId"),
            "hourlyOutput": row.get("HourlyOutput"),
        })
    upgrades = []
    for row in rules["hang_up_upgrade_material"]:
        upgrades.append({
            "level": row.get("Level"),
            "hangUpId": row.get("HangUpId"),
            "cost": cost_list(row.get("LevelUpCost")),
        })
    fast = []
    for row in rules["fast_hang_up"]:
        fast.append({
            "fastHangUpId": row.get("FastHangUpId"),
            "bindFurniture": row.get("BindFurniture"),
            "hangUpIdList": row.get("HangUpIdList") or [],
            "extraHangUpRewardList": row.get("ExtraHangUpRewardList") or [],
            "freeTimes": row.get("FastHangUpFreeTimes"),
            "items": [
                {"itemId": int(key), "hours": value}
                for key, value in (row.get("FastHangUpItemDic") or {}).items()
            ],
            "freeHours": row.get("FreeFastHangUpHour"),
            "maxUseItemCount": row.get("MaxUseItemCount"),
            "autoReceive": bool(row.get("AutoReceive")),
        })
    levels = [
        {"level": row.get("Level"), "furnitureLevelUpLimit": row.get("FurnitureLevelUpLimit")}
        for row in rules["home_level"]
    ]
    levelRules = [
        {"id": row.get("Id"), "level": row.get("Level"), "levelNum": row.get("LevelNum"),
         "maxStoreThread": row.get("MaxStoreTimeThread")}
        for row in rules["furniture_level_rule"]
    ]
    furnitureSkins = [
        {"skinItemId": row.get("SkinItemId"), "furnitureType": row.get("FurnitureType"),
         "prefabName": row.get("PrefabName"), "isDefault": bool(row.get("IsDefault")),
         "order": row.get("Order")}
        for row in rules["furniture_skin"]
    ]
    themeSkins = [
        {"skinItemId": row.get("SkinItemId"), "themeType": row.get("ThemeType"),
         "prefabName": row.get("PrefabName"), "isDefault": bool(row.get("IsDefault")),
         "order": row.get("Order")}
        for row in rules["home_theme_skin"]
    ]
    environmentSkins = [
        {"skinItemId": row.get("SkinItemId"), "environmentType": row.get("EnvironmentType"),
         "prefabName": row.get("PrefabName"), "isDefault": bool(row.get("IsDefault")),
         "order": row.get("Order")}
        for row in rules.get("home_environment_skin", [])
    ]
    functions = [
        {"type": row.get("Type"), "key0": row.get("Key0"), "key1": row.get("Key1"),
         "condition": row.get("Condition")}
        for row in rules.get("function_limit", [])
    ]
    rewardRules = []
    for row in rules["reward_rule"]:
        if row.get("Id") not in REWARD_RULE_IDS:
            continue
        rewardRules.append({
            "id": row.get("Id"),
            "order": row.get("Order", -1),
            "condition": row.get("Condition"),
            "percent": row.get("Percent"),
            "ids": row.get("IdList") or [],
            "weights": row.get("WeightList") or [],
            "counts": row.get("CountList") or [],
        })
    return {
        "version": "854_156383",
        "furniture": furniture,
        "hangUps": hangUps,
        "hangUpRewards": rewards,
        "hangUpUpgradeMaterials": upgrades,
        "fastHangUps": fast,
        "homeLevels": levels,
        "furnitureLevelRules": levelRules,
        "furnitureSkins": furnitureSkins,
        "themeSkins": themeSkins,
        "environmentSkins": environmentSkins,
        "functionLimits": functions,
        "rewardRules": rewardRules,
        "conditions": condition_rows(rules),
        "items": items,
    }


def icon_item_ids(rules, additional_item_ids=()):
    """Item ids whose icons the home dialogs can display."""
    rule_ids = set(REWARD_RULE_IDS)
    rule_ids.update(reward.get("rewardRuleId") for reward in rules["hangUpRewards"])
    for entry in rules["fastHangUps"]:
        rule_ids.update(entry.get("extraHangUpRewardList") or [])
    item_ids = set()
    for rule in rules["rewardRules"]:
        if rule.get("id") in rule_ids:
            item_ids.update(rule.get("ids") or [])
    for hang in rules["hangUps"]:
        if hang.get("rewardType") == 0:
            item_ids.add(hang.get("rewardId"))
    for material in rules["hangUpUpgradeMaterials"]:
        item_ids.update(cost["itemId"] for cost in material.get("cost") or [])
    for furniture in rules["furniture"]:
        item_ids.update(furniture.get("additionalRepair") or [])
        item_ids.update(cost["itemId"] for cost in furniture.get("repairCost") or [])
        item_ids.add(item_of_type(furniture.get("type")))
    for entry in rules["fastHangUps"]:
        item_ids.update(item["itemId"] for item in entry.get("items") or [])
    item_ids.update((6013, 6015, 6017, 50))
    item_ids.update(additional_item_ids)
    return {int(value) for value in item_ids if value is not None and int(value) > 0}


def home_tutorial_fixed_reward_item_ids():
    """Read reward 7 from the pinned table and verify its existing source record."""
    actual_hash = digest(FIXED_REWARD_TABLE.read_bytes())
    source_manifest = read(WISH_SOURCE_MANIFEST)
    expected_hash = (source_manifest.get("inputHashes") or {}).get("fixed-reward-table.json")
    if actual_hash != expected_hash:
        raise ValueError("The official fixed-reward table does not match the pinned wish source manifest.")
    rows = read(FIXED_REWARD_TABLE).get("fixed_reward") or []
    reward = next((row for row in rows if int(row.get("Id", 0)) == HOME_TUTORIAL_FIXED_REWARD_ID), None)
    if reward is None or not isinstance(reward.get("Reward"), dict) or not reward["Reward"]:
        raise ValueError("Official home tutorial fixed reward 7 is missing or empty.")
    return {int(item_id) for item_id in reward["Reward"] if int(item_id) > 0}


def item_of_type(furniture_type):
    """Furniture type to its owning item class id (EFurniture -> item id)."""
    return {
        0: 4000, 1: 4001, 2: 4009, 3: 4015, 4: 4016, 5: 4004,
        7: 4018, 8: 4013, 9: 4007, 10: 4011, 11: 4017, 12: 4019, 13: 4020,
    }.get(furniture_type)


def load_actors():
    player = read(OFFICIAL / "home-player" / "player.json")
    npc = read(OFFICIAL / "home-npc" / "npc.json")
    performance = read(OFFICIAL / "home-fast" / "performance.json")
    talk = read(OFFICIAL / "home-tutorial" / "talk.json")
    matrix = player.get("transform")
    preset = (player.get("presets") or {}).get("player_init")
    if not matrix or len(matrix) != 4 or preset is None:
        raise ValueError("Official home player transform or player_init preset is missing.")
    pose = "home/idle_01"
    for skeleton in (player.get("players", {}).get("skeletons", {}) or {}).values():
        if pose not in (skeleton.get("animations") or {}):
            raise ValueError(f"Official home player animation is missing: {pose}")
    layer_name = preset.get("SortingLayerName")
    sorting_layers = npc.get("sorting_layers") or {}
    if layer_name not in sorting_layers:
        raise ValueError(f"Official home player sorting layer is missing: {layer_name}")
    basis_x = matrix[0][:3]
    basis_y = matrix[1][:3]
    basis_z = matrix[2][:3]
    runtime = {
        "position": [matrix[0][3], matrix[1][3], matrix[2][3]],
        "scale": [math.sqrt(sum(value * value for value in basis_x)),
                  math.sqrt(sum(value * value for value in basis_y)),
                  math.sqrt(sum(value * value for value in basis_z))],
        "rotation_z": math.degrees(math.atan2(matrix[1][0], matrix[0][0])),
        "sorting_layer_name": layer_name,
        "sorting_layer": int(sorting_layers[layer_name]),
        "sorting_order": int(preset.get("SortingOrder", 0)),
        "animation": pose,
    }
    player["runtime"] = runtime
    return {
        "player": player,
        "npc": npc,
        "performance": performance,
        "talk": talk,
    }


def load_environment():
    decor = read(OFFICIAL / "home-decor" / "catalog.json")
    config = decor["home_environment"]
    moon = read(OFFICIAL / "home-environment" / "moon.json")
    player = read(OFFICIAL / "home-player" / "player.json")
    meteor = (player.get("meteor") or {}).get("config") or config.get("MeteorConfig") or {}
    meteor_templates = (player.get("meteor") or {}).get("templates") or []
    configured_templates = meteor.get("Templates") or []
    if len(meteor_templates) != len(configured_templates):
        raise ValueError("Home meteor particle payload does not match the official template list.")

    def curve(value):
        value = value or {}

        def keys(raw):
            return [
                {
                    "time": key[0],
                    "value": key[1],
                    "inSlope": key[3] if len(key) > 3 and key[3] is not None else 0.0,
                    "outSlope": key[2] if len(key) > 2 and key[2] is not None else 0.0,
                }
                for key in raw or []
            ]

        return {
            "mode": value.get("mode", 0),
            "min": value.get("min", 0.0),
            "max": value.get("max", 0.0),
            "low": keys(value.get("low")),
            "high": keys(value.get("high")),
        }

    def color_value(value):
        value = value or [1.0, 1.0, 1.0, 1.0]
        return {key: value[index] for index, key in enumerate(("r", "g", "b", "a"))}

    def gradient(value):
        value = value or {}
        return {
            "rgb": [
                {"time": key[0], "r": key[1], "g": key[2], "b": key[3]}
                for key in value.get("rgb") or []
            ],
            "alpha": [
                {"time": key[0], "alpha": key[1]}
                for key in value.get("alpha") or []
            ],
        }

    def color(value):
        value = value or {}
        return {
            "mode": value.get("mode", 0),
            "min": color_value(value.get("min")),
            "max": color_value(value.get("max")),
            "low": gradient(value.get("low")),
            "high": gradient(value.get("high")),
        }

    def material(value):
        if not value:
            return None
        return {
            "name": value.get("name", ""),
            "textures": [
                {
                    "property": name,
                    "file": texture.get("file", ""),
                    "scale": texture.get("scale") or [1.0, 1.0],
                    "offset": texture.get("offset") or [0.0, 0.0],
                }
                for name, texture in sorted((value.get("textures") or {}).items())
            ],
            "floats": [
                {"name": name, "value": item}
                for name, item in sorted((value.get("floats") or {}).items())
            ],
            "colors": [
                {"name": name, "value": color_value(item)}
                for name, item in sorted((value.get("colors") or {}).items())
            ],
        }

    def particle_system(value):
        matrix = value.get("matrix") or []
        if len(matrix) != 4 or any(len(row) != 4 for row in matrix):
            raise ValueError("Home meteor particle has an invalid official transform matrix.")
        modules = value.get("modules") or {}
        trail = modules.get("TrailModule") or {}
        if value.get("unconverted_modules"):
            raise ValueError("Home meteor has unsupported particle modules: "
                             + ", ".join(value["unconverted_modules"]))
        return {
            "path": value.get("path", ""),
            "matrix": [entry for row in matrix for entry in row],
            "duration": value.get("duration", 0.0),
            "loop": bool(value.get("loop")),
            "prewarm": bool(value.get("prewarm")),
            "speed": value.get("speed", 1.0),
            "max_particles": value.get("max_particles", 1),
            "emission_enabled": True,
            "stop_action": 0,
            "culling_mode": 0,
            "ring_buffer_mode": 0,
            "emitter_velocity_mode": 1,
            "scaling_mode": 0,
            "use_unscaled_time": False,
            "delay": curve(value.get("delay")),
            "life": curve(value.get("life")),
            "velocity": curve(value.get("velocity")),
            "size": curve(value.get("size")),
            "size_y": curve(value.get("size_y")),
            "size3d": bool(value.get("size3d")),
            "rotation": curve(value.get("rotation")),
            "rotation3d": bool(value.get("rotation3d")),
            "rotation_x": curve(value.get("rotation_x")),
            "rotation_y": curve(value.get("rotation_y")),
            "color": color(value.get("color")),
            "shape": {
                "enabled": bool((value.get("shape") or {}).get("enabled")),
                "type": (value.get("shape") or {}).get("type", 0),
                "angle": (value.get("shape") or {}).get("angle", 0.0),
                "length": (value.get("shape") or {}).get("length", 0.0),
                "radiusThickness": (value.get("shape") or {}).get("radiusThickness", 0.0),
                "donutRadius": (value.get("shape") or {}).get("donutRadius", 0.0),
                "radius": ((value.get("shape") or {}).get("radius") or {}).get("value", 0.0),
                "arc": ((value.get("shape") or {}).get("arc") or {}).get("value", 360.0),
                "boxThickness": (value.get("shape") or {}).get("boxThickness") or {"x": 0, "y": 0, "z": 0},
                "m_Position": (value.get("shape") or {}).get("m_Position") or {"x": 0, "y": 0, "z": 0},
                "m_Rotation": (value.get("shape") or {}).get("m_Rotation") or {"x": 0, "y": 0, "z": 0},
                "m_Scale": (value.get("shape") or {}).get("m_Scale") or {"x": 1, "y": 1, "z": 1},
            },
            "rate": curve(value.get("rate")),
            "bursts": [
                {
                    "time": burst.get("time", 0.0),
                    "count": curve(burst.get("count")),
                    "cycles": burst.get("cycles", 1),
                    "interval": burst.get("interval", 0.0),
                    "probability": burst.get("probability", 1.0),
                }
                for burst in value.get("bursts") or []
            ],
            "render_mode": value.get("render_mode", 0),
            "alignment": value.get("alignment", 0),
            "sorting_layer": value.get("sorting_layer", 0),
            "sorting_order": value.get("sorting_order", 0),
            "simulation_space": value.get("simulation_space", 0),
            "stretch": value.get("stretch") or {},
            "material": material(value.get("material")),
            "modules": {
                "SizeModule": {
                    "enabled": HOME_METEOR_ENABLED_MODULES["SizeModule"],
                    "curve": curve((modules.get("SizeModule") or {}).get("curve")),
                    "x": curve((modules.get("SizeModule") or {}).get("x")),
                    "y": curve((modules.get("SizeModule") or {}).get("y")),
                    "z": curve((modules.get("SizeModule") or {}).get("z")),
                    "separateAxes": bool((modules.get("SizeModule") or {}).get("separateAxes")),
                },
                "VelocityModule": {
                    "enabled": HOME_METEOR_ENABLED_MODULES["VelocityModule"],
                    "inWorldSpace": False,
                    **{
                        key: curve((modules.get("VelocityModule") or {}).get(key))
                        for key in ("x", "y", "z", "orbitalZ", "radial", "speedModifier")
                    },
                },
                "ClampVelocityModule": {
                    "enabled": HOME_METEOR_ENABLED_MODULES["ClampVelocityModule"],
                    "inWorldSpace": False,
                    "separateAxes": False,
                    "multiplyDragByParticleSize": True,
                    "multiplyDragByParticleVelocity": True,
                    "magnitude": curve((modules.get("ClampVelocityModule") or {}).get("magnitude")),
                    "drag": curve({"mode": 0, "min": 0.0, "max": 0.0}),
                    "dampen": (modules.get("ClampVelocityModule") or {}).get("dampen", 0.0),
                },
                "ColorModule": {
                    "enabled": HOME_METEOR_ENABLED_MODULES["ColorModule"],
                    **color((modules.get("ColorModule") or {}).get("gradient", modules.get("ColorModule"))),
                },
                "TrailModule": {
                    key: item
                    for key, item in trail.items()
                    if key not in {"lifetime", "widthOverTrail", "colorOverLifetime", "colorOverTrail", "material"}
                } | {
                    "lifetime": curve(trail.get("lifetime")),
                    "widthOverTrail": curve(trail.get("widthOverTrail")),
                    "colorOverLifetime": color(trail.get("colorOverLifetime")),
                    "colorOverTrail": color(trail.get("colorOverTrail")),
                    "material": material(trail.get("material")),
                },
            },
        }

    source_manifest = read(HOME_PLAYER_SOURCE_MANIFEST)
    meteor_runtime = {
        "format": 1,
        "sourceManifest": source_manifest.get("manifest", "854_156383"),
        "templates": [
            {
                "assetGuid": (configured_templates[index].get("mProxyRes") or {}).get("mAssetGuid", ""),
                "systems": [particle_system(system) for system in group],
            }
            for index, group in enumerate(meteor_templates)
        ],
    }

    def moment(value):
        value = value or {}
        return {"hour": value.get("Hour", 0), "minute": value.get("Minute", 0), "second": value.get("Second", 0)}

    lights = []
    for row in config.get("HomeLightTimeConfigs") or []:
        lights.append({
            "begin": moment(row.get("BeginTime")),
            "lights": [{"name": item.get("Name", ""), "on": bool(item.get("IsOn"))}
                       for item in row.get("DefaultLights") or []],
        })
    return {
        "version": "854_156383",
        "config": {
            "timeScale": config.get("HomeTimeScale", 30.0),
            "secondsPerMinute": config.get("SecondsPerMinute", 60),
            "minutesPerHour": config.get("MinutesPerHour", 60),
            "hoursPerDay": config.get("HoursPerDay", 24),
            "daysPerMonth": config.get("DaysPerMonth", 30),
            "monthsPerYear": config.get("MonthsPerYear", 12),
            "refreshDay": moment(config.get("RefreshDayTime")),
            "stages": [{"stage": row.get("Stage"), "time": moment(row.get("Time"))}
                       for row in config.get("DayStageList") or []],
            "lightTimes": lights,
            "effectLightOn": moment(config.get("EffectLightOnTime")),
            "effectLightOff": moment(config.get("EffectLightOffTime")),
            "moon": config.get("MoonConfig") or moon.get("config") or {},
            "meteor": {
                "begin": moment(meteor.get("BeginTime")),
                "end": moment(meteor.get("EndTime")),
                "probability": meteor.get("Probability", 0.0),
                "interval": meteor.get("Interval", 0),
            },
        },
        "meteorRuntime": meteor_runtime,
        "moonVariants": [
            {"index": int(key), "sprite": value.get("moon", {}).get("sprite", {}).get("file", "")
             .rsplit("/", 1)[-1]}
            for key, value in sorted(moon.get("variants", {}).items(), key=lambda item: int(item[0]))
        ],
    }


def load_rewards():
    data = read(OFFICIAL / "home-random-rewards" / "catalog.json")
    tables = data["tables"]
    return {
        "version": "854_156383",
        "types": [
            {
                "type": row.get("RandomRewardType"),
                "minInterval": row.get("MinRefreshInterval"),
                "maxInterval": row.get("MaxRefreshInterval"),
                "maxCount": row.get("MaxCount"),
            }
            for row in tables["home_random_reward_type"]
        ],
        "groups": [
            {
                "groupId": row.get("GroupId"),
                "type": row.get("RandomRewardType"),
                "ruleId": row.get("RuleId"),
            }
            for row in tables["home_random_reward_group"]
        ],
        "slots": [
            {
                "slotId": row.get("SlotId"),
                "groupIds": row.get("GroupIds") or [],
                "furnitureLimit": row.get("FurnitureLimit"),
            }
            for row in tables["home_random_reward_slot"]
        ],
    }


def load_reward_visuals():
    """Official slot positions, leaf prefabs and the first animal pose."""
    home = read(OFFICIAL / "home.json")
    data = read(OFFICIAL / "home-random-rewards" / "catalog.json")
    positions = home.get("slots") or {}
    ground = data["grounds"]["Ground"]
    slots = []
    for index, slot in enumerate(ground.get("slots") or []):
        position = positions.get(slot.get("path"))
        if not position:
            continue
        slots.append({
            "index": index,
            "position": position,
            "order": slot.get("order", 5),
            "direction": slot.get("direction", 0),
            "prefabIndex": slot.get("prefab_index", -1),
        })
    leaves = []
    for key, layers in (data.get("leaves") or {}).items():
        if not layers:
            continue
        layer = layers[0]
        sprite = layer.get("sprite") or {}
        leaves.append({
            "name": key.rsplit("/", 1)[-1].replace(".prefab", ""),
            "sprite": (sprite.get("file") or "").rsplit("/", 1)[-1],
            "order": layer.get("sorting_order", 5),
            "vertices": [value for point in (layer.get("vertices") or []) for value in point],
        })
    animals = {}
    skeletons = data.get("skeletons") or {}
    for item_id, ids in (ground.get("prefabs") or {}).items():
        models = []
        for skeleton_id in ids:
            skeleton = skeletons.get(skeleton_id)
            if skeleton is None:
                skeleton = next((value for key, value in skeletons.items() if key.startswith(skeleton_id)), None)
            if not skeleton:
                continue
            frames = skeleton.get("frames") or []
            if not frames:
                continue
            textures = skeleton.get("textures") or {}
            layers = []
            for layer in frames[0]:
                page = layer.get("page") or ""
                layers.append({
                    "page": (textures.get(page) or "").rsplit("/", 1)[-1],
                    "order": layer.get("order", 0),
                    "vertices": layer.get("vertices") or [],
                    "uv": layer.get("uv") or [],
                    "triangles": layer.get("triangles") or [],
                })
            if layers:
                models.append({"scale": skeleton.get("scale", 0.01), "layers": layers})
        if models:
            animals[item_id] = models
    return {
        "version": "854_156383",
        "slots": slots,
        "leaves": leaves,
        "animals": [
            {"itemId": int(item_id), "models": models}
            for item_id, models in animals.items()
        ],
    }

def load_mail():
    mail = read(OFFICIAL / "home-mail" / "ui.json")
    owls = read(OFFICIAL / "home-mail" / "owls.json")
    return {"version": "854_156383", "texts": mail.get("texts") or {}, "owls": owls}


def load_tips():
    return read(OFFICIAL / "home-tips" / "graphs.json")


def load_tutorials():
    adventure = read(OFFICIAL / "adventure.json")
    guide_names = [
        "RepaireFurniture2", "GoHomeUnlockHeap", "RepaireBed",
        "BedSpeedUp", "RepaireBaGuaLu", "FirstMergeItem",
    ]
    story_info = {row.get("StoryName"): row for row in adventure["tables"]["story"]}
    guides = []
    for name in guide_names:
        graph = adventure["stories"].get(name)
        if not isinstance(graph, dict) or not isinstance(graph.get("nodes"), dict):
            raise ValueError(f"Official home tutorial graph is missing: {name}")
        nodes = []
        for key, node in sorted(graph["nodes"].items(), key=lambda pair: int(pair[0])):
            ports_source = node.get("ports") or node.get("Ports") or {}
            ports = []
            for port_name, port in ports_source.items():
                connections = []
                for connection in port.get("connections") or []:
                    target = connection.get("node", connection.get("NodeId"))
                    if target is None or target == "":
                        continue
                    connections.append({
                        "node": int(target),
                        "port": connection.get("port", connection.get("fieldName", "")),
                    })
                ports.append({"name": str(port_name), "connections": connections})
            payload = {field: value for field, value in node.items()
                       if field not in ("ports", "Ports", "Entry", "Exit", "_type")}
            nodes.append({
                "id": int(key),
                "type": node.get("_type", ""),
                "name": node.get("m_Name", node.get("name", "")),
                "payload": json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
                "ports": ports,
            })
        dialogue_nodes = []
        for key, lines in (adventure["dialogues"].get(name) or {}).items():
            for line in lines or []:
                dialogue_nodes.append({
                    "nodeId": int(key),
                    "order": int(line.get("Order", 0)),
                    "text": line.get("Text", ""),
                    "roleId": int(line.get("RoleId", 0)),
                    "face": line.get("RoleFaceType", ""),
                    "type": line.get("DialogType", ""),
                    "voiceEvent": line.get("VoiceEventKey", ""),
                })
        info = story_info.get(name, {})
        guides.append({
            "name": name,
            "order": int(info.get("Order", 0)),
            "resPath": info.get("ResPath", ""),
            "nodes": nodes,
            "dialogues": dialogue_nodes,
            "graphPropertiesJson": json.dumps(graph.get("properties") or {}, ensure_ascii=False, separators=(",", ":")),
        })
    return {
        "version": "854_156383",
        "guides": guides,
        "sourceSha256": digest((OFFICIAL / "adventure.json").read_bytes()),
    }

def stage_sprites(payloads):
    SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    files = set(SPRITE_SOURCES)
    for payload in payloads:
        collect_sprites(payload, files)
    copies = {}
    prepared = 0
    for res in sorted(files):
        if not res.startswith("res://"):
            continue
        source = ROOT / res[len("res://"):]
        if not source.is_file():
            raise FileNotFoundError(f"Missing official sprite: {source}")
        target = SPRITE_DIR / source.name
        if not target.is_file() or target.stat().st_size != source.stat().st_size:
            shutil.copyfile(source, target)
            prepared += 1
        copies[source.name] = digest(source.read_bytes())
    return copies, prepared


def stage_reward_pages(reward_visuals):
    """Copy the animal texture pages referenced by the first-pose meshes."""
    SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    source_dir = OFFICIAL / "home-random-rewards" / "sprites"
    copies = {}
    prepared = 0
    for animal in reward_visuals.get("animals") or []:
        for model in animal.get("models") or []:
            for layer in model.get("layers") or []:
                name = layer.get("page") or ""
                if not name or name in copies:
                    continue
                source = source_dir / name
                if not source.is_file():
                    raise FileNotFoundError(f"Missing reward page: {source}")
                target = SPRITE_DIR / name
                if not target.is_file() or target.stat().st_size != source.stat().st_size:
                    shutil.copyfile(source, target)
                    prepared += 1
                copies[name] = digest(source.read_bytes())
    return copies, prepared

def stage_icons(rules, additional_item_ids=()):
    """Copy the item icons referenced by home rewards, costs and tokens."""
    SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    by_id = {item["id"]: item for item in rules["items"]}
    copies = {}
    prepared = 0
    required_ids = {int(value) for value in additional_item_ids}
    for item_id in sorted(icon_item_ids(rules, required_ids)):
        item = by_id.get(item_id)
        if not item or not item.get("icon"):
            if item_id in required_ids:
                raise ValueError(f"Official tutorial reward item {item_id} has no configured icon.")
            continue
        name = item["icon"]
        source = OFFICIAL / "sprites" / name
        if not source.is_file():
            if item_id in required_ids:
                raise FileNotFoundError(f"Official tutorial reward icon is missing: {source}")
            continue
        target = SPRITE_DIR / name
        if not target.is_file() or target.stat().st_size != source.stat().st_size:
            shutil.copyfile(source, target)
            prepared += 1
        copies[name] = digest(source.read_bytes())
    return copies, prepared


def ensure_runtime_sprite_meta(name):
    target = SPRITE_DIR / name
    meta = target.with_name(target.name + ".meta")
    if meta.exists():
        return
    guid = hashlib.sha256(("Zjcs.HomeSprite/" + name).encode("utf-8")).hexdigest()[:32]
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
        newline="\n",
    )


def write(name, payload):
    path = OUTPUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    temporary_path = None
    try:
        with tempfile.NamedTemporaryFile(
                mode="w", encoding="utf-8", newline="", dir=path.parent,
                prefix=".home-runtime-", suffix=".tmp", delete=False) as temporary:
            temporary_path = Path(temporary.name)
            temporary.write(text)
        os.replace(temporary_path, path)
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)
    return path


def build():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    scene = load_scene()
    ui = load_ui()
    rules = load_rules()
    actors = load_actors()
    environment = load_environment()
    rewards = load_rewards()
    mail = load_mail()
    reward_visuals = load_reward_visuals()
    tips = load_tips()
    tutorials = load_tutorials()
    tutorial_reward_item_ids = home_tutorial_fixed_reward_item_ids()
    home_player_source_manifest = read(HOME_PLAYER_SOURCE_MANIFEST)

    sprites, prepared = stage_sprites([scene, ui, actors, environment, rewards, mail, tips, reward_visuals, tutorials])
    pages, prepared_pages = stage_reward_pages(reward_visuals)
    sprites.update(pages)
    prepared += prepared_pages
    icons, prepared_icons = stage_icons(rules, tutorial_reward_item_ids)
    sprites.update(icons)
    prepared += prepared_icons
    for name in sprites:
        ensure_runtime_sprite_meta(name)

    outputs = {}
    for name, payload in (
        ("scene.json", scene),
        ("ui.json", ui),
        ("rules.json", rules),
        ("actors.json", actors),
        ("player-runtime.json", actors["player"]["runtime"]),
        ("environment.json", environment),
        ("rewards.json", rewards),
        ("mail.json", mail),
        ("reward-visuals.json", reward_visuals),
        ("tutorials.json", tutorials),
        ("tips.json", tips),
    ):
        path = write(name, payload)
        outputs[name] = {"bytes": path.stat().st_size, "sha256": digest(path.read_bytes())}

    manifest = {
        "manifest": "854_156383",
        "generator": "scripts/prepare_unity_home_runtime.py",
        "operation": "Merge the extracted official Home scene, UI, rules and actor "
                     "payloads into the runtime StreamingAssets set and copy every "
                     "referenced sprite.",
        "inputs": {
            "homePlayerRuntime": {
                "transformSource": "assets/official/home-player/player.json",
                "transformSourceSha256": digest((OFFICIAL / "home-player" / "player.json").read_bytes()),
                "sortingLayerSource": "assets/official/home-npc/npc.json",
                "sortingLayerSourceSha256": digest((OFFICIAL / "home-npc" / "npc.json").read_bytes()),
                "preset": "player_init",
                "pose": actors["player"]["runtime"]["animation"],
            },
            "homeMeteorRuntime": {
                "source": "assets/official/home-player/player.json",
                "sourceSha256": digest((OFFICIAL / "home-player" / "player.json").read_bytes()),
                "assetManifest": "assets/reference/home-player/source-manifest.json",
                "assetManifestSha256": digest(HOME_PLAYER_SOURCE_MANIFEST.read_bytes()),
                "particleSystems": [
                    {"bundle": source["bundle"], "pathId": source["path_id"], "sha256": source["sha256"]}
                    for source in home_player_source_manifest.get("inputs", {}).values()
                    if source.get("type") == "ParticleSystem"
                    and source.get("bundle") == "defaultpackage_assets_prefabs_home_dynamic_nature.bundle"
                ],
                "counterpartDependencies": home_player_source_manifest.get("counterpart_dependencies", {}),
            },
            "tutorialFixedReward": {
                "id": HOME_TUTORIAL_FIXED_REWARD_ID,
                "table": ".tools/source-packages/official-data/tables.json",
                "sha256": digest(FIXED_REWARD_TABLE.read_bytes()),
                "itemIds": sorted(tutorial_reward_item_ids),
            },
            "astrologicalTagArt": {
                "source": "assets/official/fidelity-rules.json",
                "sha256": digest((OFFICIAL / "fidelity-rules.json").read_bytes()),
                "properties": [value["property_type"] for value in ui["astrological_tag_art"]],
            },
        },
        "counts": {
            "background_layers": len(scene["background"]),
            "furniture": len(scene["furniture"]),
            "click_regions": len(scene["click_regions"]),
            "fruit_tree_animations": len(scene["fruit_tree"]["animations"]),
            "fruit_tree_frames": sum(len(value["frames"]) for value in scene["fruit_tree"]["animations"]),
            "panels": len(ui["panels"]),
            "texts": len(ui["texts"]),
            "items": len(rules["items"]),
            "tutorial_guides": len(tutorials["guides"]),
            "tutorial_nodes": sum(len(guide["nodes"]) for guide in tutorials["guides"]),
            "meteor_templates": len(environment["meteorRuntime"]["templates"]),
            "meteor_particle_systems": sum(len(row["systems"])
                                            for row in environment["meteorRuntime"]["templates"]),
            "sprites": len(sprites),
        },
        "outputs": outputs,
        "sprites": sprites,
        "copied_sprites": prepared,
    }
    write("source-manifest.json", manifest)
    print("Home runtime payloads:", manifest["counts"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    build()
