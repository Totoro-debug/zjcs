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
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OFFICIAL = ROOT / "assets" / "official"
OUTPUT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Home"
SPRITE_DIR = OUTPUT / "sprites"

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
]

RULE_TABLES = [
    "furniture", "furniture_level_rule", "hang_up", "fast_hang_up",
    "home_level", "furniture_skin", "home_theme_skin", "home_environment_skin",
    "home_decoration", "function_limit", "hang_up_reward",
    "hang_up_upgrade_material",
]

REWARD_RULE_IDS = {1, 2, 3, 4, 5, 6, 7, 8, 9, 100, 101, 102, 11100,
                   11000, 11001, 11002, 11003, 11004, 11005, 11006, 11007,
                   10010, 10012, 10013}

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
        "localization_key", "mask", "rectmask2d", "layout", "grid", "size_fitter",
        "layout_element", "aspect_fitter", "alpha", "button", "scroll", "children",
        "toggle",
    )
    result = {key: node[key] for key in keep if key in node}
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
    if isinstance(panel.get("root"), dict):
        result["root"] = trim_node(panel["root"])
    return result


def collect_sprites(node, files):
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "file" and isinstance(value, str):
                files.add(value)
            else:
                collect_sprites(value, files)
    elif isinstance(node, list):
        for value in node:
            collect_sprites(value, files)


def load_scene():
    home = read(OFFICIAL / "home.json")
    completion = read(OFFICIAL / "home-completion" / "scene.json")
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
    return {
        "version": home.get("version", "854_156383"),
        "camera": home["camera"],
        "background": background,
        "furniture": furniture,
        "click_regions": [{"path": region.get("path", ""), "vertices": [value for point in (region.get("vertices") or []) for value in point]} for region in home.get("click_regions", [])],
        "outdoor_slots": outdoor,
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
    return {
        "version": "854_156383",
        "panels": [{"key": key, "root": panel.get("root")} for key, panel in selected.items()],
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


def icon_item_ids(rules):
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
    return {int(value) for value in item_ids if value is not None and int(value) > 0}


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


def stage_icons(rules):
    """Copy the item icons referenced by home rewards, costs and tokens."""
    SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    by_id = {item["id"]: item for item in rules["items"]}
    copies = {}
    prepared = 0
    for item_id in sorted(icon_item_ids(rules)):
        item = by_id.get(item_id)
        if not item or not item.get("icon"):
            continue
        name = item["icon"]
        source = OFFICIAL / "sprites" / name
        if not source.is_file():
            continue
        target = SPRITE_DIR / name
        if not target.is_file() or target.stat().st_size != source.stat().st_size:
            shutil.copyfile(source, target)
            prepared += 1
        copies[name] = digest(source.read_bytes())
    return copies, prepared


def write(name, payload):
    path = OUTPUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    path.write_text(text, encoding="utf-8")
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

    sprites, prepared = stage_sprites([scene, ui, actors, environment, rewards, mail, tips, reward_visuals])
    icons, prepared_icons = stage_icons(rules)
    sprites.update(icons)
    prepared += prepared_icons

    outputs = {}
    for name, payload in (
        ("scene.json", scene),
        ("ui.json", ui),
        ("rules.json", rules),
        ("actors.json", actors),
        ("environment.json", environment),
        ("rewards.json", rewards),
        ("mail.json", mail),
        ("reward-visuals.json", reward_visuals),
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
        "counts": {
            "background_layers": len(scene["background"]),
            "furniture": len(scene["furniture"]),
            "click_regions": len(scene["click_regions"]),
            "panels": len(ui["panels"]),
            "texts": len(ui["texts"]),
            "items": len(rules["items"]),
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
