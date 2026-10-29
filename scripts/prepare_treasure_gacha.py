"""Stage the pinned treasure gacha rules, original UI trees, and sprites for Unity."""
import copy
import gzip
import hashlib
import io
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "assets/official/rules.json"
UI = ROOT / "assets/official/ui.json"
ICONS = ROOT / "assets/official/icons.json"
NAVIGATION = ROOT / "assets/official/relic_navigation.json"
CODEX_UI = ROOT / "assets/official/codex-ui.json"
RELIC_DRAW = ROOT / "assets/official/relic_draw.json"
PERFORMANCE_MESHES = ROOT / "assets/official/relic-performance/meshes.json"
PERFORMANCE_MANIFEST = ROOT / "assets/reference/relic-performance/source-manifest.json"
PERFORMANCE_SPRITES = ROOT / "assets/official/relic-performance/sprites"
RESOURCE_DIR = ROOT / "unity/Assets/Game/Resources/TreasureGacha"
RUNTIME_JSON = RESOURCE_DIR / "treasure-gacha-runtime.json"
STREAMING_DIR = ROOT / "unity/Assets/StreamingAssets/TreasureGacha"
UI_JSON = STREAMING_DIR / "ui.json"
MACHINE_STATES_JSON = STREAMING_DIR / "machine-states.json"
PERFORMANCE_JSON_GZ = STREAMING_DIR / "performance-data.json.gz"
PERFORMANCE_SPRITE_DIR = STREAMING_DIR / "performance-sprites"
OUTPUT_MANIFEST = ROOT / "assets/reference/treasure-gacha/source-manifest.json"

PANEL_KEYS = {
    "relic_draw": "treasure_gacha_main",
    "relic_maps": "treasure_gacha_maps",
    "relic_map_row": "treasure_gacha_map_row",
    "gacha_rules": "treasure_gacha_detail",
    "gacha_result": "treasure_gacha_result",
    "gacha_history": "treasure_gacha_history",
    "gacha_history_row": "treasure_gacha_history_row",
    "gacha_score": "treasure_gacha_score",
    "gacha_score_item": "treasure_gacha_score_item",
    "common_confirm": "treasure_gacha_confirm",
}

CODEX_PANEL_KEYS = {
    "CollectBookPanel": "treasure_gacha_collection",
    "TreasureCollectBookContent": "treasure_gacha_collection_content",
    "CollectBookContentCollectSet": "treasure_gacha_collection_set",
    "CollectBookTreasureContentItem": "treasure_gacha_collection_item",
    "CollectBookItemIcon": "treasure_gacha_collection_icon",
    "CollectBookItemTemplate": "treasure_gacha_collection_slot",
}

UI_TRANSLATIONS = {
    "\u0411\u0430\u0437\u043e\u0432\u044b\u0439 \u0448\u0430\u043d\u0441: {0}": "基础概率：{0}",
    "\u0412\u044b\u0442\u044f\u043d\u0443\u0442\u044c x1": "抽取 x1",
    "\u0412\u044b\u0442\u044f\u043d\u0443\u0442\u044c x{0}": "抽取 x{0}",
    "\u0413\u0430\u0440\u0430\u043d\u0442\u0438\u0440\u0443\u0435\u0442 <color=#7CD2FF>\u0440\u0435\u0434\u043a\u0443\u044e</color> \u0440\u0435\u043b\u0438\u043a\u0432\u0438\u044e \u0437\u0430 10 \u0432\u044b\u0442\u044f\u0433\u0438\u0432\u0430\u043d\u0438\u0439.": "10 次抽取必得 <color=#7CD2FF>稀有</color>古遗物。",
    "\u0413\u0430\u0447\u0430-\u0432\u044b\u0442\u044f\u0433\u0438\u0432\u0430\u043d\u0438\u044f \u0441\u0435\u0433\u043e\u0434\u043d\u044f: {0}/{1}": "今日抽取：{0}/{1}",
    "\u041a\u043e\u043b\u043b\u0435\u043a\u0446\u0438\u044f: {0}/{1}": "收藏：{0}/{1}",
    "\u041a\u043e\u0440\u043e\u043b\u0435\u0432\u0441\u0442\u0432\u043e": "珍稀宝箱",
    "\u041c\u043e\u043b\u0438\u0442\u0432\u044b x1": "抽取 x1",
    "\u041c\u043e\u043b\u0438\u0442\u0432\u044b x10": "抽取 x10",
    "\u041c\u043e\u043b\u0438\u0442\u0432\u044b x100": "抽取 x100",
    "\u041c\u043e\u043b\u0438\u0442\u0432\u044b \u0441\u0435\u0433\u043e\u0434\u043d\u044f: {0}/{1}": "今日抽取：{0}/{1}",
    "\u041d\u043e\u0432\u0430\u044f \u0441\u0435\u0440\u0438\u044f \u0433\u0430\u0447\u0438\n\u043e\u0442\u043a\u0440\u043e\u0435\u0442\u0441\u044f \u0447\u0435\u0440\u0435\u0437 {0}.": "新系列将在 {0} 后开放。",
    "\u041e\u0431\u043c\u0435\u043d": "商店",
    "\u041f\u043e\u043f\u044b\u0442\u043e\u043a \u0434\u043e \u0433\u0430\u0440\u0430\u043d\u0442. <color=#7CD2FF>\u0440\u0435\u0434\u043a\u043e\u0439</color> \u0440\u0435\u043b\u0438\u043a\u0432\u0438\u0438: {0}": "距稀有古遗物保底还需：{0} 次",
    "\u0420\u0435\u0439\u0442\u0438\u043d\u0433 {0}": "阶位 {0}",
    "\u0421\u0440\u0435\u0434\u043d\u0438\u0439 \u0448\u0430\u043d\u0441 (\u0441 \u0433\u0430\u0440\u0430\u043d\u0442\u0438\u0435\u0439): {0}": "平均概率（含保底）：{0}",
    "\u0426\u0435\u043b\u0438": "目标",
    "\u042d\u043b\u0435\u043c\u0435\u043d\u0442": "元素宝箱",
    "\u042d\u043e\u043d\u0438\u0447\u0435\u0441\u043a\u0430\u044f \u0433\u0430\u0447\u0430": "古遗物扭蛋",
}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def file_name(source):
    return Path((source or "").replace("\\", "/").rsplit("/", 1)[-1]).name


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


def convert_rules(tables, icon_index, navigation, relic_draw):
    gachas = [row for row in tables["gacha"] if 4 <= row["GachaType"] <= 12]
    chest_ids = {row["ChestId"] for row in gachas}
    chests = [row for row in tables["chest"] if row["ClassId"] in chest_ids]
    series_rows = sorted(tables["treasure_gacha"], key=lambda row: (row["Order"], row["ChestGachaId"]))
    rule_ids = {rule for chest in chests for rule in chest.get("RewardRules", [])}
    rule_ids.update(rule for chest in chests for group in (chest.get("GachaRewardOfTimes") or []) for rule in group)
    reward_rules = [row for row in tables["reward_rule"] if row["Id"] in rule_ids
                    or row.get("RecordSharedId") in {str(value) for value in rule_ids}]
    reward_ids = {reward for row in reward_rules for reward in row.get("IdList", [])}
    random_rewards = [row for row in tables["random_reward"] if row["Id"] in reward_ids]

    condition_ids = set()
    for series in series_rows:
        condition = series.get("Condition") or {}
        condition_ids.update(condition.get("Ids") or [])
    conditions = [row for row in tables["conditions"] if row["ClassId"] in condition_ids]

    item_ids = {item for series in series_rows for item in series["ChestGachaItems"]}
    item_ids.update(row["CostItemId"] for row in gachas)
    item_ids.add(2)
    item_ids.update(item for chest in chests for item in chest.get("Rewards", {}))
    item_ids.update(item for row in tables["achievement"] if row["Group"] in (11, 12, 13)
                    for item in row.get("Reward", {}))
    item_ids = {int(item) for item in item_ids}
    treasure_by_id = {row["ClassId"]: row for row in tables["treasure"]}
    item_by_id = {row["ClassId"]: row for row in tables["item"]}
    for item_id in list(item_ids):
        treasure = treasure_by_id.get(item_id)
        if treasure and treasure.get("TreasurePieceId", 0) > 0:
            item_ids.add(treasure["TreasurePieceId"])

    changed = True
    while changed:
        changed = False
        for item_id in list(item_ids):
            item = item_by_id.get(item_id)
            if not item:
                continue
            for amount in item.get("RecycleReward") or {}:
                reward_id = int(amount)
                if reward_id not in item_ids:
                    item_ids.add(reward_id)
                    changed = True

    items = []
    missing_icons = []
    for item_id in sorted(item_ids):
        item = item_by_id.get(item_id)
        if item is None:
            continue
        icon = icon_index.get(str(item_id)) or {}
        icon_file = file_name(icon.get("file"))
        if not icon_file and item.get("RawIcon"):
            missing_icons.append(item_id)
        items.append({
            "ClassId": item_id, "ItemType": item["ItemType"], "Quality": item["Quality"],
            "ElementType": item.get("ElementType", 0), "Name": item.get("Name", ""),
            "RawIcon": item.get("RawIcon", ""), "IconFile": icon_file,
            "CanRepeat": item.get("CanRepeat", False), "CanDrop": item.get("CanDrop", False),
            "CanDropWhenExist": item.get("CanDropWhenExist", False), "ShowInBag": item.get("ShowInBag", False),
            "AgioItemId": item.get("AgioItemId", 0), "AgioAmount": item.get("AgioAmount", 0),
            "RecycleGroupCount": item.get("RecycleGroupCount", 1),
            "RecycleReward": amounts(item.get("RecycleReward")),
        })

    conditions_out = []
    for row in conditions:
        condition = row.get("Condition") or {}
        conditions_out.append({
            "ClassId": row["ClassId"], "Type": condition.get("_type", ""),
            "MapClassId": condition.get("MapClassId", 0), "Rank": condition.get("Rank", 0),
            "Days": condition.get("Days", 0), "Op": condition.get("Op", ">="),
            "IsOnlyRank": condition.get("IsOnlyRank", False),
        })

    series_out = []
    maps = navigation.get("maps", {})
    for row in series_rows:
        condition = row.get("Condition") or {}
        map_sprites = maps.get(str(row["ChestGachaId"]), {})
        series_out.append({
            "ChestGachaId": row["ChestGachaId"], "Order": row["Order"],
            "MapClassId": row["MapClassId"], "MainTitle": row.get("MainTitle", ""),
            "Subtitle": row.get("Subtitle", ""), "PreviewDuration": row.get("PreviewDuration", 0),
            "BgSpritePath": row.get("BgSpritePath", ""), "ConditionType": condition.get("_type", ""),
            "ConditionMapClassId": condition.get("MapClassId", 0), "ConditionIds": condition.get("Ids") or [],
            "ChestGachaItems": row["ChestGachaItems"],
            "EntryBgSpriteFile": file_name((map_sprites.get("EntryBgSpriteGuidProxy") or {}).get("file")),
            "EntryIconSpriteFile": file_name((map_sprites.get("EntryIconSpriteGuidProxy") or {}).get("file")),
            "IconSpriteFile": file_name((map_sprites.get("IconSpriteGuidProxy") or {}).get("file")),
        })

    rewards_out = []
    for row in random_rewards:
        selector = row.get("RandomItemRangeSelector") or {}
        rewards_out.append({
            "Id": row["Id"], "Quality": row.get("Quality", 0), "MinCount": row.get("MinCount", 1),
            "MaxCount": row.get("MaxCount", 1), "IdList": row.get("IdList") or [],
            "WeightOfIdList": row.get("WeightOfIdList") or [],
            "SelectorType": selector.get("_type", ""),
            "DefaultGachaIds": selector.get("DefaultGachaIds") or [],
            "MapClassId": selector.get("MapClassId", -1), "ElementTypes": selector.get("ElementTypes") or [],
            "IsNeedMet": selector.get("IsNeedMet", True), "IsPiece": selector.get("IsPiece", False),
        })

    chests_out = []
    for row in chests:
        first_rules = row.get("GachaRewardOfTimes") or []
        chests_out.append({
            "ClassId": row["ClassId"], "FirstRewardRuleIds": first_rules[0] if first_rules else [],
            "RewardRules": row.get("RewardRules") or [], "OpenCounts": row.get("OpenCounts") or [],
            "Rewards": amounts(row.get("Rewards")), "CostItems": amounts(row.get("CostItems")),
        })

    achievement_rows = []
    for row in tables["achievement"]:
        if row["Group"] not in (11, 12, 13):
            continue
        achievement_rows.append({
            "Id": row["Id"], "Group": row["Group"], "AchievementType": row["AchievementType"],
            "CycleCount": row.get("CycleCount", 0), "CompleteCount": count_param(row.get("CompleteParams")),
            "CycleIncrementCount": count_param(row.get("CycleIncrementParams")),
            "Reward": amounts(row.get("Reward")),
        })

    treasures_out = [{
        "ClassId": row["ClassId"], "TreasurePieceId": row.get("TreasurePieceId", 0),
        "EffectTypeName": row.get("EffectTypeName") or "None",
        "InitialHitCount": 0 if row.get("EffectTypeName") == "Task" else 1,
    } for row in tables["treasure"] if row["ClassId"] in item_ids]
    settings = tables["game_settings"]
    return {
        "Manifest": "854_156383", "Gachas": gachas, "Chests": chests_out,
        "RewardRules": reward_rules, "RandomRewards": rewards_out, "Series": series_out,
        "Conditions": conditions_out, "Items": items, "Treasures": treasures_out,
        "Achievements": achievement_rows,
        "PityProbabilities": [row for row in tables["gacha_pity_probability"]
                               if 4 <= row["GachaType"] <= 12],
        "Settings": {
            "DailyLimit": int(settings["DailyGachaMaxCountDict"]["2"]),
            "ScoreItemId": int(settings["PreciousChestScoreItemId"]),
            "HistoryDays": int(settings["GachaHistoryRecordDays"]),
            "HistoryLimit": int(settings["GachaHistoryRecordCacheCount"]),
            "DefaultGachaId": int(settings["DefaultRandomTreasureGachaId"]),
            "BlockInactiveElementMaps": bool(settings["IsElementGachaBlockInactiveMap"]),
            "QualityDistributions": [
                {"ChestId": int(chest_id), "Qualities": qualities}
                for chest_id, qualities in sorted(relic_draw["settings"]["distributions"].items(),
                                                 key=lambda value: int(value[0]))
            ],
            "ElementGachaMappings": [
                {"ElementId": int(element_id), "GachaType": int(gacha_type)}
                for element_id, gacha_type in relic_draw["settings"]["element_types"]
            ],
        },
    }, missing_icons


def walk_nodes(node):
    yield node
    for child in node.get("children", []):
        yield from walk_nodes(child)


def flatten_panel(root):
    def shallow(node):
        return {key: value for key, value in node.items() if key != "children"}

    flat_nodes = []

    def visit(parent):
        parent_path = parent.get("path")
        if not parent_path:
            raise ValueError("Official treasure gacha UI node has no path: " + parent.get("name", ""))
        for child in parent.get("children", []):
            child_path = child.get("path")
            if not child_path:
                raise ValueError("Official treasure gacha UI node has no path: " + child.get("name", ""))
            flat_nodes.append({"parent_path": parent_path, **shallow(child)})
            visit(child)

    visit(root)
    return shallow(root), flat_nodes


def normalize_panel(panel):
    result = copy.deepcopy(panel["root"])
    for node in walk_nodes(result):
        if node.get("text"):
            node["text"]["value"] = UI_TRANSLATIONS.get(
                node["text"].get("value", ""), node["text"].get("value", ""))
        sprite = (node.get("image") or {}).get("sprite")
        if sprite and sprite.get("file"):
            sprite["file"] = file_name(sprite["file"])
        for animation in node.get("animations", []):
            for track in animation.get("tracks", []):
                track["keys"] = [{
                    "time": key[0], "value": key[1],
                    "in_slope": key[2] or 0.0, "out_slope": key[3] or 0.0,
                    "in_step": key[2] is None, "out_step": key[3] is None,
                } for key in track.get("keys", [])]
    return result


def extract_official_ui_images(apk, schemas, source_ui, codex_ui):
    from UnityPy.classes.PPtr import PPtr
    from prepare_character_creation_opening import SpriteCache
    from PIL import Image as PillowImage

    sprite_folder = ROOT / "assets/official/sprites"
    sprite_cache = SpriteCache(apk)
    extracted_sprites = {}
    panel_sources = []
    for catalog, panel_keys in ((source_ui, PANEL_KEYS), (codex_ui, CODEX_PANEL_KEYS)):
        for source_key in panel_keys:
            panel = catalog.get(source_key)
            if panel is None:
                raise KeyError("Official UI panel is missing while reading Image components: " + source_key)
            source = panel.get("source") or {}
            bundle = Path(source.get("bundle", "")).stem
            if bundle.startswith("defaultpackage_assets_"):
                bundle = bundle[len("defaultpackage_assets_"):]
            prefab = source.get("prefab")
            if not bundle or not prefab:
                raise ValueError("Official UI panel has no pinned prefab source: " + source_key)
            panel_sources.append((catalog, source_key, panel, source, bundle, prefab))

    sprite_files = set()
    source_records = []
    for catalog, source_key, panel, source, bundle, prefab in panel_sources:
        root = apk.asset(bundle, prefab).read(check_read=False)
        bindings = {}

        def visit(game_object, parent_path, name=None):
            segment = game_object.m_Name if name is None else name
            path = parent_path + "/" + segment
            transform = next((entry.component.deref().read(check_read=False)
                              for entry in game_object.m_Component
                              if entry.component.type.name in ("RectTransform", "Transform")), None)
            if transform is None:
                raise ValueError("Pinned UI GameObject has no Transform: " + path)
            for entry in game_object.m_Component:
                if entry.component.type.name != "MonoBehaviour":
                    continue
                reader = entry.component.deref()
                basic = reader.read(check_read=False)
                if basic.m_Script.read().m_ClassName != "Image":
                    continue
                fields = schemas.read(reader)
                sprite_ref = fields["m_Sprite"]
                sprite_path_id = int(sprite_ref["m_PathID"])
                sprite = None
                if sprite_path_id:
                    sprite_reader = PPtr(**sprite_ref, assetsfile=reader.assets_file).deref()
                    sprite_object = sprite_reader.read(check_read=False)
                    sprite_name = sprite_object.m_Name
                    candidates = sorted(sprite_folder.glob(sprite_name + "-*.png"))
                    if not candidates:
                        exact = sprite_folder / (sprite_name + ".png")
                        if exact.is_file():
                            candidates = [exact]
                    decoded = sprite_cache.sprite_image(
                        reader.assets_file, int(sprite_ref["m_FileID"]), sprite_path_id)
                    if decoded is None:
                        raise ValueError("Pinned UI sprite could not be decoded: " + sprite_name)
                    matches = []
                    for candidate in candidates:
                        with PillowImage.open(candidate) as existing:
                            existing = existing.convert("RGBA")
                            if existing.size == decoded["image"].size and existing.tobytes() == decoded["image"].tobytes():
                                matches.append(candidate)
                    if matches:
                        source_file = matches[0]
                    else:
                        buffer = io.BytesIO()
                        decoded["image"].save(buffer, format="PNG", optimize=False)
                        contents = buffer.getvalue()
                        content_hash = hashlib.sha256(contents).hexdigest()
                        source_file = None
                        for length in (10, 16, 24, 32, 64):
                            filename = sprite_name + "-" + content_hash[:length] + ".png"
                            candidate = sprite_folder / filename
                            if not candidate.exists() or sha256(candidate) == content_hash:
                                source_file = candidate
                                break
                        if source_file is None:
                            raise FileExistsError("Pinned UI sprite export collides with another source: " + sprite_name)
                        sprite_folder.mkdir(parents=True, exist_ok=True)
                        source_file.write_bytes(contents)
                        extracted_sprites[filename] = {
                            "sha256": content_hash,
                            "source": sprite_cache.sources[sprite_reader.assets_file.name + ":" + str(sprite_reader.path_id)],
                        }
                    rect = sprite_object.m_Rect
                    pivot = sprite_object.m_Pivot
                    border = sprite_object.m_Border
                    sprite = {
                        "file": source_file.name,
                        "name": sprite_name,
                        "width": int(round(rect.width)),
                        "height": int(round(rect.height)),
                        "pivot": [float(pivot.x), float(pivot.y)],
                        "ppu": float(sprite_object.m_PixelsToUnits or 100.0),
                        "border": [float(border.x), float(border.y), float(border.z), float(border.w)],
                        "rect_size": [float(rect.width), float(rect.height)],
                        "mesh_type": 1 if bool(sprite_object.m_IsPolygon) else 0,
                    }
                    sprite_files.add(source_file.name)
                color = fields["m_Color"]
                if path in bindings:
                    raise ValueError("Pinned UI GameObject has multiple Image components: " + source_key + path)
                bindings[path] = {
                    "sprite": sprite,
                    "color": [float(color[channel]) for channel in ("r", "g", "b", "a")],
                    "type": int(fields["m_Type"]),
                    "preserve_aspect": bool(fields["m_PreserveAspect"]),
                    "component_flags": True,
                    "enabled": bool(basic.m_Enabled),
                    "raycast_target": bool(fields["m_RaycastTarget"]),
                    "maskable": bool(fields["m_Maskable"]),
                    "fill_center": bool(fields["m_FillCenter"]),
                    "fill_method": int(fields["m_FillMethod"]),
                    "fill_amount": float(fields["m_FillAmount"]),
                    "fill_clockwise": bool(fields["m_FillClockwise"]),
                    "fill_origin": int(fields["m_FillOrigin"]),
                    "use_sprite_mesh": bool(fields["m_UseSpriteMesh"]),
                }

            names = {}
            for child in transform.m_Children:
                child_transform = child.read()
                child_object = child_transform.m_GameObject.read()
                child_name = child_object.m_Name
                occurrence = names.get(child_name, 0)
                names[child_name] = occurrence + 1
                segment = child_name if occurrence == 0 else child_name + " (" + str(occurrence) + ")"
                visit(child_object, path, segment)

        visit(root, "")
        expected = {node.get("path") for node in walk_nodes(panel["root"])
                    if "Image" in (node.get("scripts") or [])}
        missing = sorted(expected.difference(bindings))
        extra = sorted(set(bindings).difference(expected))
        if missing or extra:
            raise ValueError("Official UI Image hierarchy differs from the extracted prefab for " + source_key
                             + "; missing=" + ",".join(missing[:5]) + "; extra=" + ",".join(extra[:5]))
        for node in walk_nodes(panel["root"]):
            image = bindings.get(node.get("path"))
            if image is not None:
                node["image"] = image
        source_records.append({
            "panel": source_key,
            "prefab_source": source,
            "image_count": len(bindings),
            "sprite_files": sorted({entry["sprite"]["file"] for entry in bindings.values()
                                     if entry["sprite"] is not None}),
        })
    return source_records, sprite_files, extracted_sprites, sprite_cache.sources


def extract_machine_states(source_ui, codex_ui):
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from UnityPy.classes.PPtr import PPtr

    apk = Apk()
    schemas = ScriptSchemas()
    ui_image_sources, ui_sprite_files, extracted_ui_sprites, ui_image_sprite_sources = \
        extract_official_ui_images(apk, schemas, source_ui, codex_ui)
    owner_path = "/ChestGachaPanel/BgPanel1/Content/Mask/GachaponMachine"
    fitter_owner_path = "/ChestGachaPanel/BgPanel1/Content"
    component = None
    fitter_component = None

    def object_path(pointer):
        if pointer.type.name != "GameObject":
            pointer = pointer.deref().read(check_read=False).m_GameObject
        game_object = pointer.read()
        transform_pointer = next((entry.component for entry in game_object.m_Component
                                  if entry.component.type.name in ("RectTransform", "Transform")), None)
        if transform_pointer is None:
            raise ValueError("Pinned gacha object has no Transform: " + game_object.m_Name)
        transform = transform_pointer.read()
        names = []
        while True:
            names.insert(0, transform.m_GameObject.read().m_Name)
            if not transform.m_Father.path_id:
                break
            transform = transform.m_Father.read()
        return "/" + "/".join(names)

    for obj in apk.objects("prefabs_ui_chestgacha"):
        if obj.type.name != "MonoBehaviour":
            continue
        basic = obj.read(check_read=False)
        script = basic.m_Script.read()
        if script.m_ClassName not in ("GachaponMachineView", "AspectRatioScaleFitterForGachaponMachine"):
            continue
        path = object_path(basic.m_GameObject)
        if script.m_ClassName == "GachaponMachineView" and path == owner_path:
            component = obj
        elif script.m_ClassName == "AspectRatioScaleFitterForGachaponMachine" and path == fitter_owner_path:
            fitter_component = obj
        if component is not None and fitter_component is not None:
            break
    if component is None:
        raise ValueError("The pinned ChestGachaPanel GachaponMachineView was not found.")
    if fitter_component is None:
        raise ValueError("The pinned gacha machine aspect-ratio fitter was not found.")

    fields = schemas.read(component)
    fitter_fields = schemas.read(fitter_component)
    references = {str(entry["rid"]): entry["data"] for entry in fields["references"]["RefIds"]}

    def scale_curve(field):
        curve = fitter_fields[field]
        return {
            "pre_wrap_mode": int(curve["m_PreInfinity"]),
            "post_wrap_mode": int(curve["m_PostInfinity"]),
            "keys": [{
                "time": float(key["time"]), "value": float(key["value"]),
                "in_slope": float(key["inSlope"]), "out_slope": float(key["outSlope"]),
            } for key in curve["m_Curve"]],
        }

    def state_configs(field, data_key):
        result = []
        for reference in fields[field]["mStateConfigs"]:
            data = references.get(str(reference["rid"]))
            if data is None:
                raise ValueError("The pinned gacha state switcher reference is missing.")
            if data_key not in data:
                continue
            for config in data[data_key]["GoConfig"]:
                result.append({
                    "state": int(config["State"]["mValue"]),
                    "game_objects": [object_path(PPtr(**entry, assetsfile=component.assets_file))
                                     for entry in config["GameObjects"]],
                })
        if not result:
            raise ValueError("The pinned gacha state switcher contains no " + data_key + " entries.")
        return result

    manifest_entry = next(name for name in apk.archive.namelist()
                          if "PackageManifest_DefaultPackage_" in name and name.endswith(".bytes"))
    package_manifest = apk.archive.read(manifest_entry)
    guid_paths = {match[2].decode(): match[1].decode() for match in re.finditer(
        rb"(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})", package_manifest)}
    sprite_records = {}

    def sprite_file(guid):
        if guid in sprite_records:
            return sprite_records[guid]["file"]
        asset_path = guid_paths.get(guid)
        if not asset_path:
            raise ValueError("Gacha state sprite GUID is absent from the pinned package manifest: " + guid)
        asset_key = "defaultpackage_" + asset_path.replace("/", "_").lower()
        bundles = sorted((name for name in apk.index
                          if asset_key.startswith(name.removesuffix(".bundle"))), key=len, reverse=True)
        sprite = None
        source_bundle = None
        for bundle in bundles:
            try:
                for path, pointer in apk.container(bundle):
                    if path.lower() == asset_path.lower() and pointer.type.name == "Sprite":
                        sprite = pointer.read()
                        source_bundle = bundle
                        break
            except FileNotFoundError:
                continue
            if sprite is not None:
                break
        if sprite is None:
            raise ValueError("Pinned gacha state sprite is unavailable: " + asset_path)
        candidates = list((ROOT / "assets/official/sprites").glob(sprite.m_Name + "-*.png"))
        if len(candidates) != 1:
            raise ValueError("Official gacha state sprite export is missing or ambiguous: " + sprite.m_Name)
        output = {
            "guid": guid,
            "asset_path": asset_path,
            "bundle": source_bundle,
            "bundle_hash": apk.index[source_bundle],
            "sprite_name": sprite.m_Name,
            "file": candidates[0].name,
            "sha256": sha256(candidates[0]),
        }
        sprite_records[guid] = output
        return output["file"]

    def sprite_states(field):
        result = []
        for reference in fields[field]["mStateConfigs"]:
            data = references.get(str(reference["rid"]))
            if data is None:
                raise ValueError("The pinned gacha image switcher reference is missing.")
            if "mStateProxyData" not in data:
                continue
            for config in data["mStateProxyData"]["SpriteRendererConfigs"]:
                result.append({
                    "path": object_path(PPtr(**config["Img"], assetsfile=component.assets_file)),
                    "states": [{
                        "state": int(entry["Key"]["mValue"]),
                        "sprite_file": sprite_file(entry["Value"]["mAssetGuid"]),
                    } for entry in config["Proxy"]["mSerializeBuffer"]],
                })
        if not result:
            raise ValueError("The pinned gacha image switcher contains no sprite entries.")
        return result

    raw = component.get_raw_data()
    source_path = ROOT / ".tools/source-packages/official-code/client-source/GachaponMachineView.cs"
    fitter_raw = fitter_component.get_raw_data()
    fitter_source = ROOT / ".tools/source-packages/official-code/client-source/AspectRatioScaleFitterForGachaponMachine.cs"
    fitter_target = object_path(PPtr(**fitter_fields["mSubTransform"], assetsfile=fitter_component.assets_file))
    if fitter_target != owner_path:
        raise ValueError("The pinned gacha aspect-ratio fitter targets an unexpected object: " + fitter_target)
    return {
        "data": {
            "map_game_object_states": state_configs("mMapBgSwitcher", "mStateGameObjectData"),
            "map_sprite_states": sprite_states("mMapBgSwitcher"),
            "element_sprite_states": sprite_states("mElementBgSwitcher"),
            "machine_show_game_object_states": state_configs("mMachineShowSwitcher", "mStateGameObjectData"),
            "aspect_ratio_scale_fitter": {
                "owner_path": fitter_owner_path,
                "target_path": fitter_target,
                "first_curve": scale_curve("mFirstCurve"),
                "second_curve": scale_curve("mSecondCurve"),
            },
        },
        "sprite_files": {entry["file"] for entry in sprite_records.values()},
        "ui_image_sources": ui_image_sources,
        "ui_sprite_files": ui_sprite_files,
        "extracted_ui_sprites": extracted_ui_sprites,
        "ui_image_sprite_sources": ui_image_sprite_sources,
        "sources": {
            "component": dict(apk.provenance(component), object_path=owner_path,
                               script="GachaponMachineView", serialized_sha256=hashlib.sha256(raw).hexdigest()),
            "scale_fitter_component": dict(apk.provenance(fitter_component), object_path=fitter_owner_path,
                                             script="AspectRatioScaleFitterForGachaponMachine",
                                             serialized_sha256=hashlib.sha256(fitter_raw).hexdigest()),
            "client_source_sha256": sha256(source_path),
            "scale_fitter_client_source_sha256": sha256(fitter_source),
            "state_sprites": sorted(sprite_records.values(), key=lambda entry: entry["guid"]),
            "operation": "Decoded machine switchers, scale-fitter curves, GameObject paths, sprite GUIDs, and original APK sprite exports",
        },
    }


def convert_ui(source_ui, codex_ui, navigation, runtime_catalog):
    panels = []
    required_sprites = set()
    for input_key, output_key in PANEL_KEYS.items():
        source = source_ui.get(input_key)
        if source is None:
            raise KeyError("Official treasure gacha UI panel is missing: " + input_key)
        root = normalize_panel(source)
        flat_root, flat_nodes = flatten_panel(root)
        panels.append({"key": output_key, "root": flat_root, "nodes": flat_nodes})
        for node in walk_nodes(root):
            sprite = (node.get("image") or {}).get("sprite")
            if sprite and sprite.get("file"):
                required_sprites.add(sprite["file"])
    for source_key, output_key in CODEX_PANEL_KEYS.items():
        source = codex_ui.get(source_key)
        if source is None:
            raise KeyError("Official collection UI panel is missing: " + source_key)
        root = normalize_panel(source)
        flat_root, flat_nodes = flatten_panel(root)
        panels.append({"key": output_key, "root": flat_root, "nodes": flat_nodes})
        for node in walk_nodes(root):
            sprite = (node.get("image") or {}).get("sprite")
            if sprite and sprite.get("file"):
                required_sprites.add(sprite["file"])
    for series in runtime_catalog["Series"]:
        required_sprites.update(value for value in (
            series["EntryBgSpriteFile"], series["EntryIconSpriteFile"], series["IconSpriteFile"]) if value)
    for item in runtime_catalog["Items"]:
        if item["IconFile"]:
            required_sprites.add(item["IconFile"])
    required_sprites.update(("BtnCheck_Nor-3b5c5a72eb.png", "BtnCheck_Sel-cbe9e7c698.png"))
    return {"version": "854_156383", "panels": panels}, required_sprites


def convert_performance(source, relic_draw):
    geometry = [{"vertices": item[0], "triangles": item[1]}
                for item in source["_geometry"]]
    uvs = [{"values": values} for values in source["_uvs"]]
    animations = []
    sprite_names = set()
    for key, record in source.items():
        if key.startswith("_"):
            continue
        if not record.get("compact_meshes"):
            raise ValueError("Treasure gacha performance data is not in the verified compact mesh format: " + key)
        textures = record.get("textures") or {}
        if len(textures) != 1:
            raise ValueError("A treasure gacha animation must use one official atlas: " + key)
        texture_name = next(iter(textures))
        texture_file = file_name(textures[texture_name])
        sprite_names.add(texture_file)
        frames = []
        for frame in record["frames"]:
            parts = []
            for part in frame:
                if len(part) != 8 or part[2] != texture_name:
                    raise ValueError("Treasure gacha mesh frame has an unsupported part encoding: " + key)
                parts.append({"mesh": part[0], "uv": part[1],
                              "color": part[3:7], "blend": part[7]})
            frames.append({"parts": parts})
        animations.append({
            "key": key,
            "name": record["animation"],
            "duration": record["duration"],
            "fps": record["fps"],
            "scale": record["scale"],
            "texture": texture_file,
            "frames": frames,
        })
    capsule = relic_draw["egg"]
    capsule_transform = {key: capsule[key] for key in (
        "anchor_min", "anchor_max", "anchored_position", "size_delta", "pivot", "scale", "rotation")}
    return {"version": "854_156383", "capsule_transform": capsule_transform,
            "geometry": geometry, "uvs": uvs,
            "animations": animations}, sprite_names


def main():
    tables = read_json(RULES)
    icon_index = read_json(ICONS)
    navigation = read_json(NAVIGATION)
    relic_draw = read_json(RELIC_DRAW)
    performance_source = read_json(PERFORMANCE_MESHES)
    source_ui = read_json(UI)
    codex_ui = read_json(CODEX_UI)
    runtime, missing_icons = convert_rules(tables, icon_index, navigation, relic_draw)
    if missing_icons:
        raise ValueError("Official item icon index is missing gacha items: " + ", ".join(map(str, missing_icons[:30])))
    machine_states = extract_machine_states(source_ui, codex_ui)
    ui_payload, sprite_names = convert_ui(source_ui, codex_ui, navigation, runtime)
    machine_payload = {"manifest": "854_156383", **machine_states["data"]}
    sprite_names.update(machine_states["sprite_files"])
    sprite_names.update(machine_states["ui_sprite_files"])
    performance_payload, performance_sprite_names = convert_performance(performance_source, relic_draw)

    sprite_source = ROOT / "assets/official/sprites"
    sprite_manifest = {}
    STREAMING_DIR.mkdir(parents=True, exist_ok=True)
    (STREAMING_DIR / "sprites").mkdir(parents=True, exist_ok=True)
    RESOURCE_DIR.mkdir(parents=True, exist_ok=True)
    for name in sorted(sprite_names):
        source = sprite_source / name
        if not source.is_file():
            raise FileNotFoundError("Official treasure gacha sprite is not extracted: " + str(source))
        target = STREAMING_DIR / "sprites" / name
        target.write_bytes(source.read_bytes())
        sprite_manifest[name] = {"source_sha256": sha256(source), "output_sha256": sha256(target)}
        write_meta(target, "file")

    PERFORMANCE_SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    performance_sprite_manifest = {}
    for name in sorted(performance_sprite_names):
        source = PERFORMANCE_SPRITES / name
        if not source.is_file():
            raise FileNotFoundError("Official treasure gacha performance atlas is missing: " + str(source))
        target = PERFORMANCE_SPRITE_DIR / name
        target.write_bytes(source.read_bytes())
        write_meta(target, "file")
        performance_sprite_manifest[name] = {
            "source_sha256": sha256(source), "output_sha256": sha256(target),
        }

    RUNTIME_JSON.write_text(json.dumps(runtime, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    UI_JSON.write_text(json.dumps(ui_payload, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    MACHINE_STATES_JSON.write_text(json.dumps(machine_payload, ensure_ascii=False, separators=(",", ":")) + "\n",
                                   encoding="utf-8", newline="\n")
    performance_json = json.dumps(performance_payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    PERFORMANCE_JSON_GZ.write_bytes(gzip.compress(performance_json, compresslevel=9, mtime=0))
    for directory in (RESOURCE_DIR, STREAMING_DIR, STREAMING_DIR / "sprites",
                      PERFORMANCE_SPRITE_DIR):
        write_meta(directory, "folder")
    write_meta(RUNTIME_JSON, "file")
    write_meta(UI_JSON, "file")
    write_meta(MACHINE_STATES_JSON, "file")
    write_meta(PERFORMANCE_JSON_GZ, "file")

    manifest = {
        "module": "treasure-gacha",
        "manifest": "854_156383",
        "inputs": {
            "rules.json": sha256(RULES), "ui.json": sha256(UI),
            "icons.json": sha256(ICONS), "relic_navigation.json": sha256(NAVIGATION),
            "codex-ui.json": sha256(CODEX_UI),
            "relic_draw.json": sha256(RELIC_DRAW),
            "relic-performance/meshes.json": sha256(PERFORMANCE_MESHES),
            "relic-performance/source-manifest.json": sha256(PERFORMANCE_MANIFEST),
        },
        "outputs": {
            "runtime_catalog": {"path": str(RUNTIME_JSON.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(RUNTIME_JSON)},
            "ui_extension": {"path": str(UI_JSON.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(UI_JSON)},
            "machine_states": {"path": str(MACHINE_STATES_JSON.relative_to(ROOT)).replace("\\", "/"),
                               "sha256": sha256(MACHINE_STATES_JSON)},
            "performance_data": {
                "path": str(PERFORMANCE_JSON_GZ.relative_to(ROOT)).replace("\\", "/"),
                "sha256": sha256(PERFORMANCE_JSON_GZ),
                "source_manifest": "assets/reference/relic-performance/source-manifest.json",
                "animations": len(performance_payload["animations"]),
                "geometry_meshes": len(performance_payload["geometry"]),
                "uv_sets": len(performance_payload["uvs"]),
            },
        },
        "panels": {
            **{output: source_ui[source]["source"] for source, output in PANEL_KEYS.items()},
            **{output: codex_ui[source]["source"] for source, output in CODEX_PANEL_KEYS.items()},
        },
        "sprites": sprite_manifest,
        "performance_sprites": performance_sprite_manifest,
        "ui_image_sources": machine_states["ui_image_sources"],
        "ui_image_sprite_sources": sorted(machine_states["ui_image_sprite_sources"].values(),
                                           key=lambda record: (record["bundle"], record["path_id"])),
        "extracted_ui_sprites": machine_states["extracted_ui_sprites"],
        "machine_state_source": machine_states["sources"],
    }
    OUTPUT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    stale_image_bindings = STREAMING_DIR / "ui-image-bindings.json"
    stale_image_bindings_meta = stale_image_bindings.with_name(stale_image_bindings.name + ".meta")
    for stale_path in (stale_image_bindings, stale_image_bindings_meta):
        if stale_path.exists():
            stale_path.unlink()
    print("Treasure gacha runtime data:", len(runtime["Series"]), "series,", len(runtime["Items"]),
          "items,", len(sprite_names), "UI sprites,", len(performance_payload["animations"]),
          "official machine animations,", len(performance_sprite_names), "performance atlases")


if __name__ == "__main__":
    main()
