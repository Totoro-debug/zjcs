#!/usr/bin/env python3
"""Prepare the offline fixed-goods subset and official home-shop UGUI panels."""

from __future__ import annotations

import copy
import hashlib
import json
import re
import shutil
from pathlib import Path

from home_ui_animation import normalize_home_ui_animations


ROOT = Path(__file__).resolve().parents[1]
OFFICIAL = ROOT / "assets" / "official"
STREAMING = ROOT / "unity" / "Assets" / "StreamingAssets"
OUTPUT = STREAMING / "HomeShop"
SHOP_SOURCE_MANIFEST = ROOT / "assets" / "reference" / "shop-ui" / "source-manifest.json"
CITY_SOURCE_MANIFEST = ROOT / "assets" / "reference" / "official-city" / "source-manifest.json"
RULE_MANIFEST = ROOT / "assets" / "reference" / "official-city" / "rules-manifest.json"
MANIFEST_VERSION = "854_156383"
PANEL_KEYS = ("shop", "shop_goods", "shop_item")


def read_json(path: Path):
    with path.open("r", encoding="utf-8") as source:
        return json.load(source)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sprite_name(reference: dict) -> str:
    file_name = reference.get("file", "")
    if file_name.startswith("res://"):
        file_name = file_name[6:]
    return Path(file_name).name


def trim_sprite_reference(image: dict | None) -> dict | None:
    if not isinstance(image, dict):
        return image
    image = copy.deepcopy(image)
    sprite = image.get("sprite")
    if isinstance(sprite, dict):
        sprite["file"] = sprite_name(sprite)
    return image


FLAT_NODE_FIELDS = (
    "name", "path", "active", "anchor_min", "anchor_max", "anchored_position",
    "size_delta", "pivot", "scale", "rotation", "scripts", "image", "text",
    "localization_key", "mask", "rectmask2d", "layout", "grid", "size_fitter",
    "layout_element", "aspect_fitter", "alpha", "button", "scroll", "animations",
)


def flat_node(source: dict, parent_path: str | None = None) -> dict:
    result = {key: copy.deepcopy(source[key]) for key in FLAT_NODE_FIELDS if key in source}
    if "animations" in result:
        result["animations"] = normalize_home_ui_animations(result["animations"])
    if "image" in result:
        result["image"] = trim_sprite_reference(result["image"])
    if parent_path is not None:
        result["parent_path"] = parent_path
    return result


def flatten_panel(key: str, source: dict) -> dict:
    root = source.get("root")
    if not isinstance(root, dict):
        raise ValueError(f"Official UI panel has no root: {key}")
    flat_root = flat_node(root)
    nodes = []

    def visit(node: dict, parent_path: str) -> None:
        for child in node.get("children") or []:
            flat = flat_node(child, parent_path)
            nodes.append(flat)
            visit(child, child["path"])

    visit(root, root["path"])
    return {"key": "home_" + key, "root": flat_root, "nodes": nodes}


def collect_sprite_files(panels: list[dict]) -> set[str]:
    files: set[str] = set()
    for panel in panels:
        values = [panel["root"], *panel["nodes"]]
        for node in values:
            sprite = (node.get("image") or {}).get("sprite")
            if isinstance(sprite, dict):
                files.add(sprite.get("file", ""))
    return {file_name for file_name in files if file_name}


def prepare_panel_sprite(file_name: str) -> dict | None:
    home_path = STREAMING / "Home" / "sprites" / file_name
    if home_path.is_file():
        return None
    source_path = OFFICIAL / "sprites" / file_name
    if not source_path.is_file():
        raise FileNotFoundError(f"Official HomeShop UI sprite is not available: {file_name}")
    output_path = OUTPUT / "sprites" / file_name
    output_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path, output_path)
    return {
        "source": "assets/official/sprites/" + file_name,
        "destination": "unity/Assets/StreamingAssets/HomeShop/sprites/" + file_name,
        "sha256": sha256(output_path),
    }


def condition_fields(condition: dict | None) -> dict:
    condition = condition or {}
    kind = condition.get("_type", "NoneCondition")
    if kind == "NoneCondition":
        return {"condition_type": "NoneCondition", "condition_day": 0, "condition_op": ">="}
    if kind == "RegisteredDaysCondition":
        day = condition.get("Day")
        op = condition.get("Op")
        if not isinstance(day, int) or day < 0 or op not in (">=", ">", "==", "<=", "<"):
            raise ValueError(f"Invalid official registered-days condition: {condition}")
        return {"condition_type": kind, "condition_day": day, "condition_op": op}
    if kind == "SubRankBetweenCondition":
        start = condition.get("From")
        end = condition.get("To")
        if not isinstance(start, int) or not isinstance(end, int) or start < 0 or end < start:
            raise ValueError(f"Invalid official sub-rank condition: {condition}")
        return {"condition_type": kind, "condition_day": 0, "condition_op": ">=",
                "condition_from_sub_rank": start, "condition_to_sub_rank": end}
    raise ValueError(f"Unsupported offline HomeShop condition: {kind}")


def price_off_fields(goods_id: int, price_off_by_id: dict) -> dict:
    rows = price_off_by_id.get(goods_id, [])
    return {"price_off_options": [{"sub_rank": row["SubRank"],
                                   "values": [float(value) for value in row.get("PriceOffList") or []]}
                                  for row in rows]}


def runtime_goods(goods_id: int, group_id: int, goods_by_id: dict, item_by_id: dict,
                  icons: dict, price_off_by_id: dict) -> dict:
    source = goods_by_id.get(goods_id)
    if source is None:
        raise ValueError(f"Official HomeShop good is missing: {goods_id}")
    item_id = source.get("ItemId")
    item = item_by_id.get(item_id)
    icon = icons.get(str(item_id))
    currency_icon = icons.get(str(source.get("Currency")))
    if item is None or not icon or not icon.get("file") or not currency_icon or not currency_icon.get("file"):
        raise ValueError(f"Official HomeShop item metadata or icon is missing: {item_id}")
    price = source.get("OriginalPrice")
    if not isinstance(price, (int, float)) or int(price) != price:
        raise ValueError(f"HomeShop good {goods_id} has non-integral price.")
    return {
        "goods_id": goods_id,
        "group_id": group_id,
        "weight": int(source.get("Weight", 0)),
        **condition_fields(source.get("Condition")),
        **price_off_fields(goods_id, price_off_by_id),
        "item_id": item_id,
        "item_count": int(source.get("Count", 0)),
        "currency_id": int(source.get("Currency", 0)),
        "original_price": int(price),
        "inventory": int(source.get("Inventory", 0)),
        "total_buy_count_limit": int(source.get("TotalBuyCountLimit", 0)),
        "refresh_cycle_type": int(source.get("RefreshCycleType", 0)),
        "support_buy_multi_count": bool(source.get("SupportBuyMultiCount")),
        "show_name": bool(source.get("ShowName")),
        "award_dialog_type": int(source.get("AwardDialogType", 0)),
        "item_name": item.get("Name", ""),
        "item_quality": int(item.get("Quality", 1)),
        "icon_file": Path(icon["file"].replace("res://assets/official/sprites/", "")).name,
        "currency_icon_file": Path(currency_icon["file"].replace("res://assets/official/sprites/", "")).name,
    }


def achievement_count(params: str | None) -> int | None:
    if not params:
        return None
    match = re.fullmatch(r"\{Count:(\d+)\}", params)
    if match is None:
        raise ValueError(f"Unsupported ShopScoreCount achievement parameter: {params}")
    return int(match.group(1))


def achievement_reward(row: dict, item_by_id: dict, icons: dict) -> dict:
    rewards = row.get("Reward") or {}
    result = []
    for item_id, count in sorted(rewards.items(), key=lambda value: int(value[0])):
        item_id = int(item_id)
        item = item_by_id.get(item_id)
        icon = icons.get(str(item_id))
        if item is None or not icon or not icon.get("file"):
            raise ValueError(f"Official HomeShop gift item metadata or icon is missing: {item_id}")
        result.append({"item_id": item_id, "count": int(count), "item_name": item.get("Name", ""),
                       "item_quality": int(item.get("Quality", 1)),
                       "icon_file": Path(icon["file"].replace("res://assets/official/sprites/", "")).name})
    if not result or any(value["item_id"] <= 0 or value["count"] <= 0 for value in result):
        raise ValueError(f"Invalid official HomeShop gift reward: {row.get('Id')}")
    return {"raw_id": row["Id"], "target_score": achievement_count(row.get("CompleteParams")),
            "cycle_increment": achievement_count(row.get("CycleIncrementParams")), "rewards": result}


def build() -> dict:
    ui = read_json(OFFICIAL / "ui.json")
    rules = read_json(OFFICIAL / "rules.json")
    icons = read_json(OFFICIAL / "icons.json")
    rule_manifest = read_json(RULE_MANIFEST)
    city_manifest = read_json(CITY_SOURCE_MANIFEST)
    shop_manifest = read_json(SHOP_SOURCE_MANIFEST)

    if shop_manifest.get("version") != MANIFEST_VERSION:
        raise ValueError("HomeShop icon source manifest is not the fixed APK version.")
    if rule_manifest.get("tables", {}).get("shop", {}).get("sha256") != "784ca23dac58f9e9f1eb9db07e9850662dcde60a5e8e565b7dccc8047861f852":
        raise ValueError("Official shop table hash differs from the fixed APK manifest.")
    if rule_manifest.get("tables", {}).get("shop_goods", {}).get("sha256") != "2376414c79ae91f8e923110a435cf46036ca2f5c7ec0958c1a55e61633cbccca":
        raise ValueError("Official shop_goods table hash differs from the fixed APK manifest.")
    expected_table_hashes = {
        "shop_goods_group": "52bfd4289a791df97e80726d09d669d5c18e1b6bfab50d435bec3bcd1fcb0b8f",
        "shop_goods_price_off": "4df46569b425c270767709d5ab938426424ecc21306cc979002d47dc662e9654",
        "achievement": "319e4e07413ae5ccbebf724eb7e2454b841b072e48fe2ef71da8512cbd4978a8",
        "game_settings": "8832aa02061ada1c2dad9cc190fbe6fe54a25be1e30596513b9f8c1a1ef22b27",
    }
    for table, expected_hash in expected_table_hashes.items():
        if rule_manifest.get("tables", {}).get(table, {}).get("sha256") != expected_hash:
            raise ValueError(f"Official {table} table hash differs from the fixed APK manifest.")
    panel_sources = city_manifest.get("panel_prefabs", {})
    if any(panel_sources.get(key, {}).get("bundle_hash") != "a38c91d0d1720027b377b778e38863f5" for key in PANEL_KEYS):
        raise ValueError("Official HomeShop panel prefab provenance differs from the fixed APK manifest.")
    if not all(key in ui for key in PANEL_KEYS):
        raise ValueError("One or more fixed HomeShop prefabs are missing from official ui.json.")

    shop = next((row for row in rules.get("shop", []) if row.get("Id") == 1), None)
    if shop is None or not shop.get("FixedGoodsList"):
        raise ValueError("Official shop 1 fixed goods are missing.")
    item_by_id = {row.get("ClassId"): row for row in rules.get("item", [])}
    goods_by_id = {row.get("Id"): row for row in rules.get("shop_goods", [])}
    price_off_rows = rules.get("shop_goods_price_off", [])
    price_off_by_id = {}
    for row in price_off_rows:
        price_off_by_id.setdefault(int(row["Id"]), []).append(row)
    for rows in price_off_by_id.values():
        rows.sort(key=lambda value: int(value["SubRank"]))
    goods_rows = []
    for goods_id in shop["FixedGoodsList"]:
        goods = goods_by_id.get(goods_id)
        if goods is None:
            raise ValueError(f"Fixed shop goods row is missing: {goods_id}")
        item_id = goods.get("ItemId")
        item = item_by_id.get(item_id)
        icon = icons.get(str(item_id))
        currency_icon = icons.get(str(goods.get("Currency")))
        if item is None or not icon or not icon.get("file") or not currency_icon or not currency_icon.get("file"):
            raise ValueError(f"Official item metadata or icon is missing: {item_id}")
        price = goods.get("OriginalPrice")
        if not isinstance(price, (int, float)) or int(price) != price:
            raise ValueError(f"Fixed good {goods_id} has non-integral original price.")
        condition = goods.get("Condition") or {}
        map_unlock_id = int(condition.get("MapClassId", 0)) if condition.get("_type") == "MapUnlockCondition" else 0
        if condition and condition.get("_type") not in ("NoneCondition", "MapUnlockCondition"):
            raise ValueError(f"Fixed good {goods_id} has an unsupported local condition.")
        goods_rows.append({
            "goods_id": goods_id,
            "group_id": 0,
            "weight": int(goods.get("Weight", 0)),
            **price_off_fields(goods_id, price_off_by_id),
            "item_id": item_id,
            "item_count": goods.get("Count", 0),
            "currency_id": goods.get("Currency", 0),
            "original_price": int(price),
            "inventory": goods.get("Inventory", 0),
            "total_buy_count_limit": goods.get("TotalBuyCountLimit", 0),
            "refresh_cycle_type": int(goods.get("RefreshCycleType", 0)),
            "support_buy_multi_count": bool(goods.get("SupportBuyMultiCount")),
            "show_name": bool(goods.get("ShowName")),
            "award_dialog_type": goods.get("AwardDialogType", 0),
            "map_unlock_id": map_unlock_id,
            "item_name": item.get("Name", ""),
            "item_quality": item.get("Quality", 1),
            "icon_file": Path(icon["file"].replace("res://assets/official/sprites/", "")).name,
            "currency_icon_file": Path(currency_icon["file"].replace("res://assets/official/sprites/", "")).name,
        })

    treasury_shop = next((row for row in rules.get("shop", []) if row.get("Id") == 2), None)
    if treasury_shop is None or not treasury_shop.get("GroupIdList"):
        raise ValueError("Official shop 2 random groups are missing.")
    if len(treasury_shop["GroupIdList"]) != len(treasury_shop.get("GroupRandomCounts") or []):
        raise ValueError("Official shop 2 group IDs and weights do not align.")
    group_by_id = {row.get("GroupId"): row for row in rules.get("shop_goods_group", [])}
    treasury_groups = []
    for group_id, group_weight in zip(treasury_shop["GroupIdList"], treasury_shop["GroupRandomCounts"]):
        group = group_by_id.get(group_id)
        if group is None or group_weight <= 0:
            raise ValueError(f"Official HomeShop random group is missing or has invalid weight: {group_id}")
        group_condition = condition_fields(group.get("Condition"))
        if group_condition["condition_type"] != "NoneCondition":
            raise ValueError(f"Offline HomeShop group condition is not supported: {group_id}")
        goods = []
        for goods_id in group.get("ShopGoodsList") or []:
            goods.append(runtime_goods(goods_id, group_id, goods_by_id, item_by_id, icons, price_off_by_id))
        complex_weights = group.get("ComplexGroupWeight") or {}
        complex_ids = [int(value) for value in complex_weights]
        complex_values = [int(complex_weights[str(value)]) for value in complex_ids]
        if not goods and not complex_ids:
            raise ValueError(f"Official HomeShop random group has no goods or nested groups: {group_id}")
        treasury_groups.append({
            "group_id": group_id,
            "draw_count": int(group_weight),
            "order": int(group.get("Order", 0)),
            "condition_type": group_condition["condition_type"],
            "condition_day": group_condition["condition_day"],
            "condition_op": group_condition["condition_op"],
            "refresh_when_condition_changed": bool(group.get("RefreshWhenConditionChanged")),
            "refresh_filter": int(group.get("RefreshFilter", 0)),
            "complex_group_ids": complex_ids,
            "complex_group_weights": complex_values,
            "goods": goods,
        })

    grocery_shop = shop
    if (len(grocery_shop.get("GroupIdList") or []) != len(grocery_shop.get("GroupRandomCounts") or [])
            or sum(grocery_shop.get("GroupRandomCounts") or []) != 9):
        raise ValueError("Official grocery shop must provide nine weighted random draws.")
    refresh_currency = grocery_shop.get("RefreshCurrency") or []
    refresh_prices = grocery_shop.get("RefreshPriceBase") or []
    refresh_limit = int(grocery_shop.get("RefreshCountsLimit", 0))
    if (not grocery_shop.get("UserRefresh") or refresh_limit != 10
            or len(refresh_currency) != len(refresh_prices) or len(refresh_currency) != refresh_limit
            or int(grocery_shop.get("RefreshPriceFactor", 0)) != 0
            or int(grocery_shop.get("RefreshCycleType", 0)) != 0):
        raise ValueError("Official grocery manual-refresh rules differ from the expected non-cycling price table.")
    needed_group_ids = set(grocery_shop["GroupIdList"])
    pending_group_ids = list(needed_group_ids)
    while pending_group_ids:
        group = group_by_id.get(pending_group_ids.pop())
        if group is None:
            raise ValueError("An official grocery group is missing.")
        for child_id in (group.get("ComplexGroupWeight") or {}):
            child_id = int(child_id)
            if child_id not in needed_group_ids:
                needed_group_ids.add(child_id)
                pending_group_ids.append(child_id)
    grocery_groups = []
    for group_id in sorted(needed_group_ids):
        group = group_by_id[group_id]
        group_condition = condition_fields(group.get("Condition"))
        if group_condition["condition_type"] != "NoneCondition":
            raise ValueError(f"Offline grocery group condition is not supported: {group_id}")
        complex_weights = group.get("ComplexGroupWeight") or {}
        complex_ids = [int(value) for value in complex_weights]
        goods = [runtime_goods(goods_id, group_id, goods_by_id, item_by_id, icons, price_off_by_id)
                 for goods_id in group.get("ShopGoodsList") or []]
        if not goods and not complex_ids:
            raise ValueError(f"Official grocery group has no goods or nested groups: {group_id}")
        grocery_groups.append({
            "group_id": group_id,
            "order": int(group.get("Order", 0)),
            "condition_type": group_condition["condition_type"],
            "condition_day": group_condition["condition_day"],
            "condition_op": group_condition["condition_op"],
            "refresh_when_condition_changed": bool(group.get("RefreshWhenConditionChanged")),
            "refresh_filter": int(group.get("RefreshFilter", 0)),
            "complex_group_ids": complex_ids,
            "complex_group_weights": [int(complex_weights[str(value)]) for value in complex_ids],
            "goods": goods,
        })

    shop_score_rows = [row for row in rules.get("achievement", [])
                       if row.get("AchievementType") == 12 and row.get("Group") == 4]
    independent = [achievement_reward(row, item_by_id, icons) for row in shop_score_rows
                   if not row.get("CycleIncrementParams") and int(row.get("CycleCount", 0)) == 0]
    anchors = [achievement_reward(row, item_by_id, icons) for row in shop_score_rows
               if int(row.get("CycleCount", 0)) > 0]
    cycle_templates = [achievement_reward(row, item_by_id, icons) for row in shop_score_rows
                       if row.get("CycleIncrementParams")]
    if (len(independent) != 39 or len(anchors) != 1 or len(cycle_templates) != 40
            or anchors[0]["target_score"] != 200000 or anchors[0]["cycle_increment"] is not None
            or any(value["cycle_increment"] != 5000 for value in cycle_templates)):
        raise ValueError("Official shop gift achievement milestones do not match the expected cycle layout.")
    coefficient = rules.get("game_settings", {}).get("ShopScoreByCostOneDiamondItem")
    if not isinstance(coefficient, (int, float)) or coefficient <= 0:
        raise ValueError("Official shop score conversion is missing or invalid.")
    reset_match = re.fullmatch(r"\((\d{2}):(\d{2}):(\d{2})\)",
                               rules.get("game_settings", {}).get("DailyResetTime", ""))
    if reset_match is None:
        raise ValueError("Official HomeShop registered-days reset time is missing or invalid.")
    reset_hour, reset_minute, reset_second = map(int, reset_match.groups())
    weekly_start_day = rules.get("game_settings", {}).get("WeeklyStartDayOfWeek")
    if not isinstance(weekly_start_day, int) or not 0 <= weekly_start_day <= 6:
        raise ValueError("Official HomeShop weekly reset weekday is missing or invalid.")

    hashes = rule_manifest.get("tables", {})
    OUTPUT.mkdir(parents=True, exist_ok=True)
    panels = [flatten_panel(key, ui[key]) for key in PANEL_KEYS]
    copied_sprites = {}
    for file_name in sorted(collect_sprite_files(panels)):
        copied = prepare_panel_sprite(file_name)
        if copied is not None:
            copied_sprites[file_name] = copied

    runtime_item_rows = list(goods_rows)
    runtime_item_rows.extend(goods for group in grocery_groups for goods in group["goods"])
    runtime_item_rows.extend(goods for group in treasury_groups for goods in group["goods"])
    runtime_item_rows.extend(reward for milestone in independent + anchors + cycle_templates
                             for reward in milestone["rewards"])
    for row in runtime_item_rows:
        file_names = {row["icon_file"]}
        if row.get("currency_icon_file"):
            file_names.add(row["currency_icon_file"])
        for file_name in file_names:
            source_path = OFFICIAL / "sprites" / file_name
            if not source_path.is_file():
                raise FileNotFoundError(f"Official HomeShop icon is not available: {file_name}")
            output_path = OUTPUT / "sprites" / file_name
            output_path.parent.mkdir(parents=True, exist_ok=True)
            if not output_path.is_file() or sha256(output_path) != sha256(source_path):
                shutil.copy2(source_path, output_path)
            resource_key = "res://assets/official/sprites/" + file_name
            resource = city_manifest.get("resources", {}).get(resource_key)
            if resource is None or resource.get("sha256") != sha256(source_path):
                raise ValueError(f"HomeShop icon is not backed by official-city source manifest: {resource_key}")
            copied_sprites[file_name] = {
                "source": "assets/official/sprites/" + file_name,
                "destination": "unity/Assets/StreamingAssets/HomeShop/sprites/" + file_name,
                "sha256": sha256(output_path),
                "manifest": resource.get("manifest"),
                "bundle": resource.get("bundle"),
                "bundle_hash": resource.get("bundle_hash"),
                "source_sha256": resource.get("sha256"),
            }

    payload = {
        "version": MANIFEST_VERSION,
        "shop_id": 1,
        "currency_policy": "official item currencies and per-good PriceOff table",
        "source_rule_tables": {
            "shop": rule_manifest["tables"]["shop"]["sha256"],
            "shop_goods": rule_manifest["tables"]["shop_goods"]["sha256"],
            "shop_goods_price_off": hashes["shop_goods_price_off"]["sha256"],
            "item": rule_manifest["tables"]["item"]["sha256"],
            "shop_goods_group": hashes["shop_goods_group"]["sha256"],
            "achievement": hashes["achievement"]["sha256"],
            "game_settings": hashes["game_settings"]["sha256"],
        },
        "fixed_goods": goods_rows,
        "grocery_unique": bool(grocery_shop.get("Unique")),
        "grocery_group_ids": grocery_shop["GroupIdList"],
        "grocery_group_draw_counts": grocery_shop["GroupRandomCounts"],
        "grocery_random_count": sum(grocery_shop["GroupRandomCounts"]),
        "grocery_groups": grocery_groups,
        "grocery_user_refresh": bool(grocery_shop.get("UserRefresh")),
        "grocery_refresh_currency_ids": refresh_currency,
        "grocery_refresh_prices": refresh_prices,
        "grocery_refresh_price_factor": int(grocery_shop.get("RefreshPriceFactor", 0)),
        "grocery_refresh_counts_limit": refresh_limit,
        "treasury_shop_id": int(treasury_shop["Id"]),
        "treasury_unique": bool(treasury_shop.get("Unique")),
        "treasury_group_ids": treasury_shop["GroupIdList"],
        "treasury_group_draw_counts": treasury_shop["GroupRandomCounts"],
        "treasury_random_count": sum(treasury_shop["GroupRandomCounts"]),
        "treasury_groups": treasury_groups,
        "shop_score_currency_id": 2,
        "shop_score_multiplier": float(coefficient),
        "registered_days_reset_hour": reset_hour,
        "registered_days_reset_minute": reset_minute,
        "registered_days_reset_second": reset_second,
        "weekly_start_day_of_week": weekly_start_day,
        "shop_score_achievements": {
            "achievement_type": 12,
            "achievement_group": 4,
            "cycle_count": int(anchors[0]["raw_id"] and next(
                row["CycleCount"] for row in shop_score_rows if row["Id"] == anchors[0]["raw_id"])),
            "independent": independent,
            "cycle_anchor": anchors[0],
            "cycle_templates": cycle_templates,
        },
    }
    texts = read_json(OFFICIAL / "fidelity-texts.json")
    achievement_text_keys = ("ui_achievement_title_ShopScoreCount", "ui_achievement_content_ShopScoreCount")
    achievement_texts = [{"key": key, "value": texts[key]} for key in achievement_text_keys if key in texts]
    if len(achievement_texts) != len(achievement_text_keys):
        raise ValueError("Official HomeShop gift achievement localization is missing.")
    (OUTPUT / "ui.json").write_text(json.dumps({"version": MANIFEST_VERSION, "panels": panels,
                                                  "texts": achievement_texts}, ensure_ascii=False,
                                                 separators=(",", ":")), encoding="utf-8")
    (OUTPUT / "rules.json").write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")

    shop_manifest["home_shop_panels"] = {
        key: {
            "bundle": panel_sources[key]["bundle"],
            "bundle_hash": panel_sources[key]["bundle_hash"],
            "path_id": panel_sources[key]["path_id"],
            "prefab": panel_sources[key]["prefab"],
        }
        for key in PANEL_KEYS
    }
    shop_manifest["runtime_sprites"] = copied_sprites
    SHOP_SOURCE_MANIFEST.write_text(json.dumps(shop_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return payload


if __name__ == "__main__":
    payload = build()
    print(f"Generated {payload['grocery_random_count']} grocery draws, {len(payload['fixed_goods'])} conditional fixed goods, "
          f"{payload['treasury_random_count']} treasury draws, and HomeShop prefab panels at {OUTPUT.relative_to(ROOT)}")
