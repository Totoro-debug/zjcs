"""Prepare the pinned pet gacha rules, official panels, and referenced sprites."""
import copy
import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "assets/official/rules.json"
ICONS = ROOT / "assets/official/icons.json"
UI = ROOT / "assets/official/ui.json"
EXCHANGE = ROOT / "assets/official/pet-exchange/exchange.json"
PERFORMANCE = ROOT / "assets/official/pet-gacha-performance/performance.json"
PET_MODELS = ROOT / "assets/official/pet-models.json"
FIDELITY_TEXTS = ROOT / "assets/official/fidelity-texts.json"
CODEX_LINKS = ROOT / "assets/official/codex-links/links.json"
SOURCE_MANIFEST = ROOT / "assets/reference/pet-gacha/source-manifest.json"
RESOURCE_DIR = ROOT / "unity/Assets/Game/Resources/PetGacha"
RUNTIME_JSON = RESOURCE_DIR / "pet-gacha-runtime.json"
STREAMING_DIR = ROOT / "unity/Assets/StreamingAssets/PetGacha"
UI_JSON = STREAMING_DIR / "ui.json"

PANEL_KEYS = {
    "pet_gacha": "pet_gacha_main",
    "gacha_shell": "pet_gacha_shell",
    "gacha_rules": "pet_gacha_detail",
    "gacha_result": "pet_gacha_result",
    "gacha_history": "pet_gacha_history",
    "gacha_history_row": "pet_gacha_history_row",
    "gacha_score": "pet_gacha_score",
    "gacha_score_item": "pet_gacha_score_item",
    "common_confirm": "pet_gacha_confirm",
}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def file_name(source):
    return Path((source or "").replace("\\", "/").rsplit("/", 1)[-1]).name


def icon_file_name(item, icon_index):
    indexed = file_name((icon_index.get(str(item["ClassId"])) or {}).get("file"))
    if indexed:
        return indexed
    icon_name = file_name(item.get("RawIcon", ""))
    if not icon_name:
        return ""
    matches = []
    for directory in (ROOT / "assets/official/sprites",
                      ROOT / "assets/official/pet-exchange/sprites",
                      ROOT / "assets/official/pet-gacha-performance/sprites"):
        matches.extend(directory.glob(icon_name + "-*.png"))
    unique = sorted(set(matches))
    if len(unique) > 1:
        raise ValueError("Official item icon is ambiguous: " + str(item["ClassId"]))
    return unique[0].name if unique else ""


def amounts(source):
    return [{"ItemId": int(item), "Count": int(count)}
            for item, count in sorted((source or {}).items(), key=lambda value: int(value[0]))]


def count_param(source):
    if not source:
        return 0
    match = re.search(r"Count\s*:\s*(\d+)", source)
    return int(match.group(1)) if match else 0


def stable_guid(relative):
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def write_meta(path, kind):
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    meta = Path(str(path) + ".meta")
    if kind == "folder":
        body = "fileFormatVersion: 2\nguid: " + stable_guid(relative) + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        importer = "TextScriptImporter" if path.suffix == ".json" and "Resources" in path.parts else "DefaultImporter"
        body = "fileFormatVersion: 2\nguid: " + stable_guid(relative) + "\n" + importer + ":\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta.write_text(body, encoding="utf-8", newline="\n")


def convert_rules(tables, icon_index, pet_models, fidelity_texts, codex_links):
    gacha = next((row for row in tables["gacha"] if row["GachaType"] == 3), None)
    chest = next((row for row in tables["chest"] if row["ClassId"] == 6003), None)
    if gacha is None or chest is None:
        raise ValueError("Pinned pet gacha type 3 or chest 6003 is missing.")

    rule_ids = set(chest.get("RewardRules") or [])
    rule_ids.update(rule for group in (chest.get("GachaRewardOfTimes") or []) for rule in group)
    reward_rules = [row for row in tables["reward_rule"] if row["Id"] in rule_ids]
    random_ids = {reward_id for row in reward_rules for reward_id in row.get("IdList", [])}
    random_rewards = [row for row in tables["random_reward"] if row["Id"] in random_ids]
    random_package_ids = {row["Id"] for row in random_rewards}

    achievements = [row for row in tables["achievement"] if row.get("Group") in (15, 16)]
    shop = next((row for row in tables["shop"] if row["Id"] == 7), None)
    if shop is None:
        raise ValueError("Official pet shop 7 is missing.")
    group_ids = set(shop.get("GroupIdList") or [])
    shop_groups = [row for row in tables["shop_goods_group"] if row["GroupId"] in group_ids]
    goods_ids = set(shop.get("FixedGoodsList") or [])
    for group in shop_groups:
        goods_ids.update(group.get("ShopGoodsList") or [])
    shop_goods = [row for row in tables["shop_goods"] if row["Id"] in goods_ids]

    condition_ids = set()
    for group in shop_groups:
        condition = group.get("Condition") or {}
        condition_ids.update(condition.get("Ids") or [])
        if condition.get("ClassId", 0) > 0:
            condition_ids.add(condition["ClassId"])
    conditions = [row for row in tables["conditions"] if row["ClassId"] in condition_ids]

    item_ids = {int(value) for row in random_rewards for value in (row.get("IdList") or [])}
    item_ids.update(int(value) for row in reward_rules for value in (row.get("IdList") or [])
                    if int(value) not in random_package_ids)
    item_ids.update((gacha["CostItemId"], gacha["RewardScoreId"], 2))
    item_ids.update(int(value) for row in achievements for value in row.get("Reward", {}))
    item_ids.update(row["ItemId"] for row in shop_goods)
    item_by_id = {row["ClassId"]: row for row in tables["item"]}
    gacha_pet_ids = {
        int(item_id) for row in random_rewards for item_id in (row.get("IdList") or [])
        if item_by_id.get(int(item_id), {}).get("ItemType") == 400
    }
    pets_by_id = {row["ClassId"]: row for row in tables["pet"]}
    evolution_by_id = {}
    for row in tables["pet_evolution"]:
        if row.get("EvolutionPhase") == 1:
            evolution_by_id.setdefault(row["ClassId"], row)
    pet_profiles = []
    pet_custom = pet_models["spawner"]["root"]["pet_custom"]
    childhood_scale = float(pet_custom["phases"]["1"])
    pet_offsets = pet_custom.get("offsets") or {}
    for item_id in list(item_ids):
        item = item_by_id.get(item_id)
        if item:
            item_ids.update(int(value) for value in (item.get("RecycleReward") or {}))
            if item.get("AgioItemId", 0) > 0:
                item_ids.add(item["AgioItemId"])
        pet = pets_by_id.get(item_id) if item_id in gacha_pet_ids else None
        evolution = evolution_by_id.get(item_id)
        if pet is not None and evolution is not None:
            type_icon_pattern = "ImgType_" + str(int(pet["Type"]) + 1) + "-*.png"
            type_icons = sorted((ROOT / "assets/official/sprites").glob(type_icon_pattern))
            if len(type_icons) != 1:
                raise FileNotFoundError("Official pet type icon is missing or ambiguous: " + type_icon_pattern)
            skill_ids = [int(value) for value in (evolution.get("SupportSkills") or [])]
            item_ids.update(skill_ids)
            pet_profiles.append({
                "ClassId": item_id, "Type": pet["Type"],
                "UIModel": int(evolution["UIModel"]), "TypeIconFile": type_icons[0].name,
                "ModelScale": childhood_scale,
                "ModelOffsetY": float(pet_offsets.get(str(evolution["UIModel"]), 0.0)),
                "SupportSkillIds": skill_ids,
            })

    skills_by_id = {row["ClassId"]: row for row in tables["skill"]}
    entry_details = {}
    entry_ids_by_skill = {}
    support_skill_ids = sorted({skill_id for profile in pet_profiles
                                for skill_id in profile["SupportSkillIds"]})
    for skill_id in support_skill_ids:
        skill = skills_by_id.get(skill_id)
        if skill is None:
            raise KeyError("Official pet support skill is missing: " + str(skill_id))
        link_types = skill.get("HyperLinkTypes") or []
        link_data = skill.get("HyperLinkDatas") or []
        if len(link_types) != len(link_data):
            raise ValueError("Pet support skill hyperlink arrays do not match: " + str(skill_id))
        entry_ids = []
        for link_type, value in zip(link_types, link_data):
            match = re.fullmatch(r"\{EntryId\s*:\s*(\d+)\}", value or "")
            if link_type != 0 or match is None:
                raise ValueError("Unsupported official pet support skill hyperlink: " + str(skill_id))
            entry_id = int(match.group(1))
            entry = codex_links["entries"].get(str(entry_id))
            name = codex_links["texts"].get("entry_" + str(entry_id) + "_name")
            description = codex_links["texts"].get("entry_" + str(entry_id) + "_desc")
            if entry is None or name is None or description is None:
                raise KeyError("Official codex entry data is incomplete: " + str(entry_id))
            icon = entry.get("Icon_sprite") or {}
            background = entry.get("Bg_sprite") or {}
            entry_details[entry_id] = {
                "Id": entry_id, "Name": name, "Description": description,
                "IconFile": file_name(icon.get("file")),
                "BackgroundFile": file_name(background.get("file")),
            }
            entry_ids.append(entry_id)
        entry_ids_by_skill[skill_id] = entry_ids

    items = []
    missing_icons = []
    for item_id in sorted(item_ids):
        item = item_by_id.get(item_id)
        if item is None:
            continue
        icon_file = icon_file_name(item, icon_index)
        if item.get("RawIcon") and not icon_file:
            missing_icons.append(item_id)
        items.append({
            "ClassId": item_id, "ItemType": item["ItemType"], "Quality": item["Quality"],
            "Name": item.get("Name", ""), "RawIcon": item.get("RawIcon", ""),
            "IconFile": icon_file, "AgioItemId": item.get("AgioItemId", 0),
            "AgioAmount": item.get("AgioAmount", 0), "CanRepeat": item.get("CanRepeat", False),
            "FuncDesc": fidelity_texts.get(f"item_{item_id}_func_desc", ""),
            "HyperlinkEntryIds": entry_ids_by_skill.get(item_id, []),
        })

    reward_rules_out = [{
        "Id": row["Id"], "ItemIds": row.get("IdList") or [],
        "Weights": row.get("WeightList") or [], "Counts": row.get("CountList") or [],
        "GuaranteedCounts": row.get("IndexMininum") or [],
        "RecordSharedId": row.get("RecordSharedId", ""),
    } for row in reward_rules]
    random_rewards_out = []
    for row in random_rewards:
        random_rewards_out.append({
            "Id": row["Id"], "ItemIds": row.get("IdList") or [],
            "Weights": row.get("WeightOfIdList") or [], "Quality": row.get("Quality", 0),
            "MinCount": row.get("MinCount", 1), "MaxCount": row.get("MaxCount", 1),
            "WeightScaleFactor": row.get("WeightScaleFactor", 0),
            "WeightScaleCountLimit": row.get("WeightScaleCountLimit", 0),
        })
    achievements_out = [{
        "Id": row["Id"], "Group": row["Group"], "AchievementType": row["AchievementType"],
        "CycleCount": row.get("CycleCount", 0), "CompleteCount": count_param(row.get("CompleteParams")),
        "CycleIncrementCount": count_param(row.get("CycleIncrementParams")),
        "Reward": amounts(row.get("Reward")),
    } for row in achievements]
    groups_out = [{
        "GroupId": row["GroupId"], "GoodsIds": row.get("ShopGoodsList") or [],
        "ConditionType": (row.get("Condition") or {}).get("_type", ""),
        "ConditionIds": (row.get("Condition") or {}).get("Ids") or [],
        "RefreshWhenConditionChanged": row.get("RefreshWhenConditionChanged", False),
        "HasComplexWeights": bool(row.get("ComplexGroupWeight")),
    } for row in shop_groups]
    goods_out = [{
        "Id": row["Id"], "ItemId": row["ItemId"], "Count": row["Count"],
        "CurrencyItemId": row["Currency"], "Price": int(row["OriginalPrice"]),
        "Inventory": row["Inventory"], "TotalBuyCountLimit": row["TotalBuyCountLimit"],
        "RefreshCycleType": row["RefreshCycleType"], "PopConfirmDialog": row["PopConfirmDialog"],
    } for row in shop_goods]
    conditions_out = []
    for row in conditions:
        condition = row.get("Condition") or {}
        conditions_out.append({
            "ClassId": row["ClassId"], "Type": condition.get("_type", ""),
            "Season": condition.get("Season", 0), "Days": condition.get("Days", 0),
            "Op": condition.get("Op", ">="), "ReleaseTypes": condition.get("ReleaseTypes") or [],
            "Not": condition.get("Not", False), "Ids": condition.get("Ids") or [],
        })

    settings = tables["game_settings"]
    reset_time = re.search(r"(\d{2}):(\d{2}):", settings["DailyResetTime"])
    if reset_time is None:
        raise ValueError("The official daily reset time could not be parsed.")
    return {
        "Manifest": "854_156383", "Gacha": gacha,
        "Chest": {
            "ClassId": chest["ClassId"], "FirstRewardRuleIds": (chest.get("GachaRewardOfTimes") or [[]])[0],
            "FirstRewardCounts": chest.get("GachaTimesOfReward") or [],
            "RewardRules": chest.get("RewardRules") or [], "OpenCounts": chest.get("OpenCounts") or [],
            "CostItems": amounts(chest.get("CostItems")), "Rewards": amounts(chest.get("Rewards")),
        },
        "RewardRules": reward_rules_out, "RandomRewards": random_rewards_out,
        "PetProfiles": sorted(pet_profiles, key=lambda value: value["ClassId"]),
        "Items": items, "Entries": [entry_details[key] for key in sorted(entry_details)],
        "Achievements": achievements_out,
        "PityProbabilities": [row for row in tables["gacha_pity_probability"] if row["GachaType"] == 3],
        "PetShop": {
            "Id": shop["Id"], "GroupIds": shop.get("GroupIdList") or [],
        "GroupRandomCounts": shop.get("GroupRandomCounts") or [],
            "FixedGoodsIds": shop.get("FixedGoodsList") or [],
            "RefreshCycleType": shop["RefreshCycleType"], "RefreshByProfession": shop["RefreshByProfession"],
            "RefreshCountsLimit": shop["RefreshCountsLimit"],
            "UserRefresh": shop["UserRefresh"], "GoodsGroups": groups_out,
            "Goods": goods_out, "Conditions": conditions_out,
        },
        "Settings": {
            "DailyLimit": int(settings["DailyGachaMaxCountDict"]["3"]),
            "HistoryDays": int(settings["GachaHistoryRecordDays"]),
            "HistoryLimit": int(settings["GachaHistoryRecordCacheCount"]),
            "WeeklyStartDayOfWeek": int(settings["WeeklyStartDayOfWeek"]),
            "DailyResetHour": int(reset_time.group(1)),
            "DailyResetMinute": int(reset_time.group(2)),
            "QuickBuyItemId": 107, "QuickBuyCurrencyItemId": 2, "QuickBuyPrice": 160,
            "PetPieceItemType": 402,
        },
    }, missing_icons


def walk_nodes(node):
    yield node
    for child in node.get("children", []):
        yield from walk_nodes(child)


def normalize_panel(root):
    result = copy.deepcopy(root)
    for node in walk_nodes(result):
        sprite = (node.get("image") or {}).get("sprite")
        if sprite and sprite.get("file"):
            sprite["file"] = file_name(sprite["file"])
    return result


def convert_ui(source_ui, exchange, performance, codex_links, runtime):
    panels = []
    required_sprites = set()
    for input_key, output_key in PANEL_KEYS.items():
        source = source_ui.get(input_key)
        if source is None:
            raise KeyError("Official pet gacha UI panel is missing: " + input_key)
        root = normalize_panel(source["root"])
        panels.append({"key": output_key, "root": root})
        for node in walk_nodes(root):
            sprite = (node.get("image") or {}).get("sprite")
            if sprite and sprite.get("file"):
                required_sprites.add(sprite["file"])

    for input_key, output_key in (("shell", "pet_exchange_shell"),
                                   ("goods", "pet_exchange_goods"),
                                   ("icon", "pet_exchange_icon")):
        source = exchange["panels"].get(input_key)
        if source is None or source.get("panel", {}).get("root") is None:
            raise KeyError("Official pet exchange panel is missing: " + input_key)
        root = normalize_panel(source["panel"]["root"])
        panels.append({"key": output_key, "root": root})
        for node in walk_nodes(root):
            sprite = (node.get("image") or {}).get("sprite")
            if sprite and sprite.get("file"):
                required_sprites.add(sprite["file"])

    result_item = (performance.get("panels") or {}).get("result_item") or {}
    result_panel = result_item.get("panel") or {}
    if result_panel.get("root") is None:
        raise KeyError("Official pet gacha result item panel is missing.")
    result_root = normalize_panel(result_panel["root"])
    panels.append({"key": "pet_gacha_result_item", "root": result_root})
    for node in walk_nodes(result_root):
        sprite = (node.get("image") or {}).get("sprite")
        if sprite and sprite.get("file"):
            required_sprites.add(sprite["file"])

    new_pet_panel = (performance.get("panels") or {}).get("new_pet") or {}
    new_pet_root = ((new_pet_panel.get("panel") or {}).get("root"))
    if new_pet_root is None:
        raise KeyError("Official new pet introduction panel is missing.")
    new_pet_root = normalize_panel(new_pet_root)
    panels.append({"key": "pet_gacha_new_pet", "root": new_pet_root})
    for node in walk_nodes(new_pet_root):
        sprite = (node.get("image") or {}).get("sprite")
        if sprite and sprite.get("file"):
            required_sprites.add(sprite["file"])

    for input_key, output_key in (("entry", "pet_gacha_entry_dialog"),
                                   ("entry_row", "pet_gacha_entry_row")):
        source = codex_links["panels"].get(input_key, {}).get("panel", {}).get("root")
        if source is None:
            raise KeyError("Official pet skill entry panel is missing: " + input_key)
        root = normalize_panel(source)
        panels.append({"key": output_key, "root": root})
        for node in walk_nodes(root):
            sprite = (node.get("image") or {}).get("sprite")
            if sprite and sprite.get("file"):
                required_sprites.add(sprite["file"])
    for entry in runtime["Entries"]:
        required_sprites.update(value for value in (entry["IconFile"], entry["BackgroundFile"]) if value)
    return {"version": "854_156383", "panels": panels}, required_sprites


def sprite_source(name):
    candidates = (
        ROOT / "assets/official/sprites" / name,
        ROOT / "assets/official/pet-exchange/sprites" / name,
        ROOT / "assets/official/pet-gacha-performance/sprites" / name,
        ROOT / "assets/official/codex-links" / name,
    )
    return next((path for path in candidates if path.is_file()), None)


def main():
    tables = read_json(RULES)
    icon_index = read_json(ICONS)
    fidelity_texts = read_json(FIDELITY_TEXTS)
    codex_links = read_json(CODEX_LINKS)
    source_ui = read_json(UI)
    exchange = read_json(EXCHANGE)
    performance = read_json(PERFORMANCE)
    pet_models = read_json(PET_MODELS)
    runtime, missing_icons = convert_rules(tables, icon_index, pet_models, fidelity_texts, codex_links)
    if missing_icons:
        raise ValueError("Official pet gacha item icon index is missing: " + ", ".join(map(str, missing_icons[:30])))
    ui_payload, sprite_names = convert_ui(source_ui, exchange, performance, codex_links, runtime)
    runtime["QualityBackgrounds"] = [
        {"Quality": int(quality), "File": file_name(value.get("file"))}
        for quality, value in sorted(exchange.get("qualities", {}).items(), key=lambda pair: int(pair[0]))
    ]
    sprite_names.update(item["IconFile"] for item in runtime["Items"] if item["IconFile"])
    sprite_names.update(profile["TypeIconFile"] for profile in runtime["PetProfiles"])
    sprite_names.update(value["File"] for value in runtime["QualityBackgrounds"] if value["File"])
    sprite_names.update(("BtnCheck_Nor-3b5c5a72eb.png", "BtnCheck_Sel-cbe9e7c698.png"))

    STREAMING_DIR.mkdir(parents=True, exist_ok=True)
    (STREAMING_DIR / "sprites").mkdir(parents=True, exist_ok=True)
    RESOURCE_DIR.mkdir(parents=True, exist_ok=True)
    sprite_manifest = {}
    for name in sorted(sprite_names):
        source = sprite_source(name)
        if source is None:
            raise FileNotFoundError("Official pet gacha sprite is not extracted: " + name)
        target = STREAMING_DIR / "sprites" / name
        target.write_bytes(source.read_bytes())
        sprite_manifest[name] = {
            "source": source.relative_to(ROOT).as_posix(),
            "source_sha256": sha256(source), "output_sha256": sha256(target),
        }
        write_meta(target, "file")

    RUNTIME_JSON.write_text(json.dumps(runtime, ensure_ascii=False, separators=(",", ":")) + "\n",
                            encoding="utf-8", newline="\n")
    UI_JSON.write_text(json.dumps(ui_payload, ensure_ascii=False, separators=(",", ":")) + "\n",
                       encoding="utf-8", newline="\n")
    for directory in (RESOURCE_DIR, STREAMING_DIR, STREAMING_DIR / "sprites"):
        write_meta(directory, "folder")
    write_meta(RUNTIME_JSON, "file")
    write_meta(UI_JSON, "file")

    SOURCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    manifest = {
        "manifest": "854_156383", "generator": "scripts/prepare_pet_gacha.py",
        "inputs": {path.relative_to(ROOT).as_posix(): sha256(path)
                   for path in (RULES, ICONS, UI, EXCHANGE, PERFORMANCE, PET_MODELS, FIDELITY_TEXTS,
                                CODEX_LINKS)},
        "outputs": {
            "runtime_catalog": {"path": RUNTIME_JSON.relative_to(ROOT).as_posix(), "sha256": sha256(RUNTIME_JSON)},
            "ui_extension": {"path": UI_JSON.relative_to(ROOT).as_posix(), "sha256": sha256(UI_JSON)},
        },
        "panels": {entry["key"]: entry["root"]["name"] for entry in ui_payload["panels"]},
        "sprites": sprite_manifest,
    }
    SOURCE_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                               encoding="utf-8", newline="\n")
    print("Pet gacha runtime:", len(runtime["RewardRules"]), "rules,",
          len(runtime["RandomRewards"]), "reward packages,", len(runtime["PetShop"]["Goods"]),
          "shop goods,", len(sprite_names), "official sprites")


if __name__ == "__main__":
    main()
