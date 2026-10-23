"""Prepare the pinned wish rules and official UI assets for Unity."""
import hashlib
import json
import shutil
from pathlib import Path

from commerce_ui_conversion import flatten_ui_node


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "assets/official/rules.json"
UI = ROOT / "assets/official/ui.json"
ICONS = ROOT / "assets/official/icons.json"
PURCHASE = ROOT / "assets/official/gacha-purchase.json"
FIXED_REWARDS_SOURCE = ROOT / ".tools/source-packages/official-data/tables.json"
SOURCE_MANIFEST = ROOT / "assets/reference/official-city/source-manifest.json"
PERFORMANCE = ROOT / "assets/official/wish-performance"
FRIEND_PANEL = ROOT / "assets/official/wish-friend-panel/friend-panel.json"
FRIEND_PANEL_SOURCE = ROOT / "assets/reference/wishes/friend-panel-source-manifest.json"
UNITY_ROOT = ROOT / "unity/Assets/Official/Wish"
RUNTIME_ROOT = ROOT / "unity/Assets/Game/Resources/Wishes"
REFERENCE_ROOT = ROOT / "assets/reference/wishes"
PREFABS = [
    "goddess", "gacha_shell", "gacha_senior", "gacha_background",
    "gacha_senior_background", "gacha_result", "gacha_history",
    "gacha_history_row", "gacha_rules", "gacha_profession_item",
    "gacha_professions", "gacha_profession_choice", "gacha_score",
    "gacha_score_item", "shop_goods",
]
GACHA_TYPES = {0, 1, 2}
ACHIEVEMENT_GROUPS = {5, 6, 7, 8, 18, 19}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def index_by(rows, key):
    return {int(row[key]): row for row in rows if row.get(key) is not None}


def collect_ui_sprites(node, result):
    image = node.get("image") or {}
    sprite = image.get("sprite") or {}
    source = sprite.get("file")
    if source:
        result[source] = sprite
    for material_key in ("mesh_material", "particle_material"):
        material = node.get(material_key) or {}
        for texture in (material.get("textures") or {}).values():
            texture_file = texture.get("file")
            if not texture_file:
                continue
            if texture_file.startswith("res://"):
                uri = texture_file
            elif texture_file.startswith("assets/"):
                uri = "res://" + texture_file
            else:
                uri = "res://assets/official/wish-friend-panel/" + texture_file.lstrip("/")
            result.setdefault(uri, {"name": Path(uri).stem, "border": [0, 0, 0, 0], "ppu": 100})
    for child in node.get("children") or []:
        collect_ui_sprites(child, result)


def find_ui_nodes(root):
    nodes = {}
    pending = [root]
    while pending:
        node = pending.pop()
        nodes[node["path"]] = node
        pending.extend(node.get("children") or [])
    return nodes


def unity_asset(source, environments):
    import sys
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    import UnityPy

    bundle = source["bundle"]
    expected_hash = source["bundle_hash"]
    environment = environments.get(bundle)
    if environment is None:
        path = ROOT / ".tools/source-packages/extracted" / bundle
        if not path.is_file():
            raise FileNotFoundError(f"Pinned wish performance bundle is missing: {path}")
        actual_hash = hashlib.md5(path.read_bytes()).hexdigest()
        if actual_hash != expected_hash:
            raise ValueError(f"Pinned wish bundle hash mismatch for {bundle}.")
        environment = UnityPy.load(str(path))
        environments[bundle] = environment
    path_id = int(source["path_id"])
    matches = [obj for obj in environment.objects if obj.path_id == path_id]
    if len(matches) != 1:
        raise ValueError(f"Pinned wish bundle object is missing or ambiguous: {bundle}:{path_id}")
    return matches[0]


def particle_components(emitter, environments):
    particle_object = unity_asset(emitter["source"], environments)
    if particle_object.type.name != "ParticleSystem":
        raise ValueError("Pinned wish effect source is not a ParticleSystem.")
    particle = particle_object.read_typetree()
    game_object_id = int(particle["m_GameObject"]["m_PathID"])
    game_object = particle_object.assets_file.objects.get(game_object_id)
    if game_object is None or game_object.type.name != "GameObject":
        raise ValueError("Pinned wish particle system has no unique GameObject.")
    game_object = game_object.read_typetree()
    renderer = None
    for component in game_object.get("m_Component") or []:
        path_id = int(component["component"]["m_PathID"])
        component_object = particle_object.assets_file.objects.get(path_id)
        if component_object is None:
            continue
        if component_object.type.name == "ParticleSystemRenderer":
            renderer = component_object.read_typetree()
            break
    if renderer is None:
        raise ValueError("Pinned wish particle system has no ParticleSystemRenderer.")
    renderer_fields = (
        "m_Enabled", "m_RenderMode", "m_SortingLayerID", "m_SortingOrder", "m_SortMode",
        "m_Materials", "m_MinParticleSize", "m_MaxParticleSize", "m_LengthScale",
        "m_NormalDirection", "m_Pivot", "m_Flip", "m_MaskInteraction",
    )
    return particle, {key: renderer[key] for key in renderer_fields if key in renderer}


def apply_particle_effects(root, effects, environments):
    nodes = find_ui_nodes(root)
    for effect in effects or []:
        effect_node = nodes.get(effect["path"])
        if effect_node is None:
            raise ValueError(f"Pinned wish effect root is absent from its prefab: {effect['path']}")
        effect_node["active"] = bool(effect.get("active", False))
        effect_node["ui_particle"] = True
        if effect.get("scale"):
            effect_node["scale"] = effect["scale"]
        for emitter in effect.get("emitters") or []:
            node = nodes.get(emitter["path"])
            if node is None:
                raise ValueError(f"Pinned wish particle emitter is absent from its prefab: {emitter['path']}")
            node["particle_system"], node["particle_renderer"] = particle_components(emitter, environments)
            node["particle_material"] = emitter["material"]


def extract_animation_events(animation, environments):
    clip = unity_asset(animation["source"], environments)
    if clip.type.name != "AnimationClip":
        raise ValueError("Pinned GachaStarsFly source is not an AnimationClip.")
    source = clip.read_typetree()
    if source.get("m_Name") != animation["name"]:
        raise ValueError("Pinned GachaStarsFly clip name does not match the UI binding.")
    events = [{"time": float(event["time"]), "functionName": event["functionName"],
               "index": int(event["intParameter"])} for event in source.get("m_Events") or []]
    expected = {("PlayStarEffect", index) for index in range(10)} | {
        ("OnStarEffectEnd", index) for index in range(10)}
    if {(event["functionName"], event["index"]) for event in events} != expected:
        raise ValueError("Pinned GachaStarsFly clip does not contain the expected ten star events.")
    return events


def reward_rows(rewards):
    return [{"ItemId": int(item_id), "Count": int(count)}
            for item_id, count in sorted((rewards or {}).items(), key=lambda pair: int(pair[0]))]


def item_row(row):
    return {"ClassId": row["ClassId"], "Name": row.get("Name", ""),
            "RawIcon": row.get("RawIcon", ""), "ItemType": row["ItemType"],
            "Quality": row.get("Quality", 0), "Rank": row.get("Rank", 0),
            "ElementType": row.get("ElementType", 0),
            "CanRepeat": row.get("CanRepeat", True), "AgioItemId": row.get("AgioItemId", 0),
            "AgioAmount": row.get("AgioAmount", 0), "AgioRuleIdList": row.get("AgioRuleIdList") or [],
            "RecycleAwards": reward_rows(row.get("RecycleReward")),
            "RecycleGroupCount": row.get("RecycleGroupCount", 1), "ShowInBag": row.get("ShowInBag", False)}


def chest_row(row):
    return {"ClassId": row["ClassId"], "RewardRules": row.get("RewardRules") or [],
            "Rewards": reward_rows(row.get("Rewards")), "OpenCounts": row.get("OpenCounts") or [],
            "CostItems": reward_rows(row.get("CostItems"))}


def shop_row(row):
    return {"Id": row["Id"], "Type": row["Type"], "GroupIdList": row.get("GroupIdList") or [],
            "GroupRandomCounts": row.get("GroupRandomCounts") or [],
            "FixedGoodsList": row.get("FixedGoodsList") or [],
            "RefreshCurrency": row.get("RefreshCurrency") or [],
            "RefreshPriceBase": row.get("RefreshPriceBase") or [],
            "RefreshPriceFactor": row.get("RefreshPriceFactor", 0),
            "RefreshCountsLimit": row.get("RefreshCountsLimit", 0),
            "RefreshByProfession": row.get("RefreshByProfession", False),
            "UserRefresh": row.get("UserRefresh", False), "RefreshCycleType": row.get("RefreshCycleType", 0),
            "Unique": row.get("Unique", False), "SortByQuality": row.get("SortByQuality", False)}


def shop_good_row(row):
    return {key: row.get(key) for key in ("Id", "Condition", "Weight", "ItemId", "Count", "Currency",
            "OriginalPrice", "Inventory", "TotalBuyCountLimit", "RefreshCycleType",
            "SupportBuyMultiCount", "PopConfirmDialog", "AwardDialogType", "ShowName", "BuyCondition")}


def group_row(row):
    weights = row.get("ComplexGroupWeight") or {}
    return {"GroupId": row["GroupId"], "Order": row.get("Order", 0),
            "ShopGoodsList": row.get("ShopGoodsList") or [],
            "ComplexGroupWeights": [{"GroupId": int(key), "Weight": int(value)}
                                    for key, value in sorted(weights.items(), key=lambda pair: int(pair[0]))],
            "Condition": row.get("Condition"), "RefreshFilter": row.get("RefreshFilter", 0)}


def achievement_row(row):
    return {"Id": row["Id"], "Group": row["Group"], "AchievementType": row["AchievementType"],
            "CompleteParams": row.get("CompleteParams", ""),
            "CycleIncrementParams": row.get("CycleIncrementParams", ""),
            "CycleCount": row.get("CycleCount", 0), "Reward": reward_rows(row.get("Reward"))}


def source_image(uri):
    name = Path(uri.replace("\\", "/")).name
    for directory in (ROOT / "assets/official/sprites", ROOT / "assets/official/wish",
                      ROOT / "assets/official/wish-friend-panel", PERFORMANCE / "sprites",
                      PERFORMANCE / "wish"):
        if directory.is_file():
            continue
        match = directory / name
        if match.is_file():
            return match
        for candidate in directory.rglob(name):
            if candidate.is_file():
                return candidate
    return None


def copy_asset(source, target, outputs):
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or digest(target) != digest(source):
        shutil.copy2(source, target)
    outputs[str(target.relative_to(ROOT)).replace("\\", "/")] = digest(target)


def fixed_reward_row(row):
    return {"Id": row["Id"], "Reward": reward_rows(row.get("Reward")),
            "CanGetTimes": row.get("CanGetTimes", 0), "SourceOperate": row.get("SourceOperate", 0),
            "Level": row.get("Level", -1)}


def build_runtime(rules, ui, icons, purchase, fixed_rewards, fixed_reward_hash, friend_panel):
    gachas = [row for row in rules["gacha"] if row["GachaType"] in GACHA_TYPES]
    chests_by_id = index_by(rules["chest"], "ClassId")
    chest_ids = {row["ChestId"] for row in gachas}
    chests = [chests_by_id[value] for value in sorted(chest_ids)]
    reward_rule_ids = sorted({rule_id for chest in chests for rule_id in chest.get("RewardRules") or []})
    rules_by_id = index_by(rules["reward_rule"], "Id")
    reward_rules = [rules_by_id[value] for value in reward_rule_ids]

    items_by_id = index_by(rules["item"], "ClassId")
    random_by_id = index_by(rules["random_reward"], "Id")
    pending_random = set()
    direct_item_ids = set()
    for reward_rule in reward_rules:
        direct_item_ids.update(int(value) for value in reward_rule.get("IdList") or [])
    for item_id in list(direct_item_ids):
        item = items_by_id.get(item_id)
        if item and item.get("ItemType") == 110:
            pending_random.add(item_id)
    random_rewards = []
    while pending_random:
        random_id = pending_random.pop()
        if any(row["Id"] == random_id for row in random_rewards):
            continue
        row = random_by_id.get(random_id)
        if row is None:
            raise ValueError(f"RandomRewardInfo row missing for package {random_id}.")
        random_rewards.append(row)
        for child_id in row.get("IdList") or []:
            child = items_by_id.get(int(child_id))
            if child and child.get("ItemType") == 110 and int(child_id) in random_by_id:
                pending_random.add(int(child_id))
    random_rewards.sort(key=lambda row: row["Id"])

    level_number_ids = set()
    for reward_rule in reward_rules:
        level_number_ids.update(int(value) for value in (reward_rule.get("LevelCountId") or []) if int(value) > 0)
        level_number_ids.update(int(value) for value in (reward_rule.get("SubRankCountId") or []) if int(value) > 0)
    for reward in random_rewards:
        for key in ("LevelCountId", "SubRankCountId"):
            value = int(reward.get(key) or 0)
            if value > 0:
                level_number_ids.add(value)
    for shop in rules["shop"]:
        if shop["Id"] not in (4, 5):
            continue
        value = int(shop.get("RefreshPriceFactor") or 0)
        if value > 0:
            level_number_ids.add(value)
    level_numbers_by_id = index_by(rules["level_number"], "Id")
    missing_level_numbers = sorted(level_number_ids - level_numbers_by_id.keys())
    if missing_level_numbers:
        raise ValueError(f"Level-number rows are missing: {missing_level_numbers}.")
    level_numbers = [level_numbers_by_id[value] for value in sorted(level_number_ids)]

    professions = [row for row in rules["profession_base"] if row["Profession"] > 0]
    unlock_profession = {}
    for profession in professions:
        for skill_id in profession.get("Skills") or []:
            unlock_profession.setdefault(int(skill_id), profession["Profession"])
    skills_by_id = index_by(rules["skill"], "ClassId")
    skill_rank_max = max(int(row["Rank"]) for row in rules["skill_rank"])
    skill_ids = sorted(unlock_profession)
    skill_items = [items_by_id[value] for value in skill_ids if value in items_by_id]

    shop_defs = [row for row in rules["shop"] if row["Id"] in (4, 5)]
    shop_goods_ids = set()
    group_ids = set()
    for shop in shop_defs:
        shop_goods_ids.update(int(value) for value in shop.get("FixedGoodsList") or [])
        group_ids.update(int(value) for value in shop.get("GroupIdList") or [])
    groups_by_id = index_by(rules["shop_goods_group"], "GroupId")
    goods_by_id = index_by(rules["shop_goods"], "Id")
    shop_groups = []
    while group_ids:
        group_id = group_ids.pop()
        if any(row["GroupId"] == group_id for row in shop_groups):
            continue
        group = groups_by_id.get(group_id)
        if group is None:
            raise ValueError(f"Shop goods group is missing: {group_id}.")
        shop_groups.append(group)
        shop_goods_ids.update(int(value) for value in group.get("ShopGoodsList") or [])
        group_ids.update(int(value) for value in group.get("ComplexGroupWeight") or {})
    shop_groups.sort(key=lambda row: row["GroupId"])
    shop_goods = [goods_by_id[value] for value in sorted(shop_goods_ids) if value in goods_by_id]

    achievement_rows = [row for row in rules["achievement"] if row["Group"] in ACHIEVEMENT_GROUPS]
    item_ids = set(direct_item_ids)
    item_ids.update(skill_ids)
    item_ids.update(skill_id + 2000 for skill_id in skill_ids)
    for reward in random_rewards:
        item_ids.update(int(value) for value in reward.get("IdList") or [])
        item_types = {int(value) for value in reward.get("ItemTypes") or []}
        quality = int(reward.get("Quality", 0))
        rank = int(reward.get("Rank", -1))
        elements = {int(value) for value in reward.get("ElementTypes") or []}
        blacklist = {int(value) for value in reward.get("BlackList") or []}
        item_ids.update(row["ClassId"] for row in rules["item"]
                        if row.get("ItemType") in item_types
                        and (quality <= 0 or row.get("Quality") == quality)
                        and (rank < 0 or row.get("Rank") == rank)
                        and (not elements or row.get("ElementType") in elements)
                        and row["ClassId"] not in blacklist)
    for quick_buy in purchase.get("quick_buy", []):
        item_ids.add(int(quick_buy["ItemId"]))
        item_ids.add(int(quick_buy["CurrencyId"]))
        if quick_buy.get("Condition", {}).get("ClassId"):
            item_ids.add(int(quick_buy["Condition"]["ClassId"]))
    item_ids.update(int(value) for shop in shop_defs for value in (shop.get("RefreshCurrency") or []))
    for goods in shop_goods:
        item_ids.add(int(goods["ItemId"]))
        item_ids.add(int(goods["Currency"]))
    for reward in achievement_rows:
        item_ids.update(int(value) for value in (reward.get("Reward") or {}))
    for chest in chests:
        item_ids.update(int(key) for key in (chest.get("CostItems") or {}))
        item_ids.update(int(key) for key in (chest.get("Rewards") or {}))
    pending_item_ids = list(item_ids)
    while pending_item_ids:
        item = items_by_id.get(pending_item_ids.pop())
        if item is None:
            continue
        for reward_id in (item.get("RecycleReward") or {}):
            reward_id = int(reward_id)
            if reward_id not in item_ids:
                item_ids.add(reward_id)
                pending_item_ids.append(reward_id)
    item_ids = {item_id for item_id in item_ids if item_id in items_by_id}
    item_rows = [items_by_id[value] for value in sorted(item_ids)]

    ui_prefabs = []
    ui_sprites = {}
    for profession in professions:
        for field in ("Icon1", "Icon3"):
            resource = str(profession.get(field) or "")
            icon = icons.get(Path(resource).name)
            if not resource or icon is None or not icon.get("file"):
                raise ValueError(f"Official profession {field} sprite is missing: {resource!r}.")
            profession[field + "File"] = Path(icon["file"]).name
            ui_sprites.setdefault(icon["file"], icon)
    performance = json.loads((PERFORMANCE / "performance.json").read_text(encoding="utf-8"))
    environments = {}
    result_root = ui["gacha_result"]["root"]
    apply_particle_effects(result_root, performance["skill"].get("effects"), environments)
    for key in PREFABS:
        source = ui.get(key)
        if source is None or not source.get("root"):
            raise ValueError(f"Converted official UI prefab is missing: {key}.")
        ui_prefabs.append({"key": key, "nodes": flatten_ui_node(source["root"])})
        collect_ui_sprites(source["root"], ui_sprites)
    if friend_panel.get("manifest") != "854_156383" or not friend_panel.get("root"):
        raise ValueError("Pinned Friendship Wish prefab conversion is missing or unsupported.")
    collect_ui_sprites(friend_panel["root"], ui_sprites)
    ui_prefabs.append({"key": "gacha_friend", "nodes": flatten_ui_node(friend_panel["root"])})

    for source_key, runtime_key in (("ten_reward", "gacha_ten_reward"),
                                    ("hundred_row", "gacha_hundred_row"),
                                    ("award_icon", "gacha_award_icon")):
        source = performance[source_key]["panel"]["root"]
        if not source:
            raise ValueError(f"Official result prefab is missing: {source_key}.")
        apply_particle_effects(source, performance[source_key].get("effects"), environments)
        ui_prefabs.append({"key": runtime_key, "nodes": flatten_ui_node(source)})
        collect_ui_sprites(source, ui_sprites)
    result_panel_fields = performance["skill"]["bindings"]["GachaResultPanel"][0]["fields"]
    result_hundred_fields = performance["skill"]["bindings"]["GachaHundredContent"][0]["fields"]
    animation_player_node = find_ui_nodes(result_root)[
        performance["skill"]["bindings"]["GachaResultAnimPlayer"][0]["fields"]["m_GameObject"]]
    star_animation = next(animation for animation in animation_player_node.get("animations", [])
                          if animation.get("name") == "GachaStarsFly")
    star_events = extract_animation_events(star_animation, environments)
    scroll_curve = result_hundred_fields["mScrollCurve"]["m_Curve"]
    result_animations = {
        "one": json.dumps(result_panel_fields["mGachaOneAnimClip"], ensure_ascii=False, separators=(",", ":")),
        "onePieces": json.dumps(result_panel_fields["mGachaOnePiecesAnimClip"], ensure_ascii=False, separators=(",", ":")),
        "quality": json.dumps(result_panel_fields["mQualityAnimClip"], ensure_ascii=False, separators=(",", ":")),
        "qualityCut": json.dumps(result_panel_fields["mQualityCutAnimClip"], ensure_ascii=False, separators=(",", ":")),
        "stars": json.dumps(star_animation, ensure_ascii=False, separators=(",", ":")),
        "starEvents": star_events,
    }
    result_timing = {
        "oneSoundDelayPercent": float(result_panel_fields["mOneGachaSoundDelayPercent"]),
        "oneTransDelay": float(result_panel_fields["mOneGachaTransDelay"]),
        "oneFadeInDelay": float(result_panel_fields["mOneGachaFadeInDelay"]),
        "tenFadeInDelay": float(result_panel_fields["mTenGachaFadeInDelay"]),
        "tenTransDelay": float(result_panel_fields["mTenGachaTransDelay"]),
        "tenTransTimeRate": float(result_panel_fields["mTenGachaTransTimeRate"]),
        "additionalAwardsDelay": float(result_panel_fields["mAdditionalAwardsDelay"]),
        "gachaFadeInTime": float(result_panel_fields["mGachaFadeInTime"]),
        "hundredShowUpDelay": float(result_hundred_fields["mShowUpDelay"]),
        "hundredTransDelay": float(result_hundred_fields["mTransDelay"]),
        "hundredScrollDelayThread": float(result_hundred_fields["mScrollDelayThread"]),
        "hundredScrollCurve": json.dumps([[float(key["time"]), float(key["value"]),
                                            float(key["inSlope"]), float(key["outSlope"])]
                                           for key in scroll_curve], separators=(",", ":")),
    }
    audio_bindings = [{"eventName": event, "file": file}
                      for event, value in performance.get("audio", {}).items()
                      for file in value.get("files", [])]

    icon_rows = []
    icon_ids = set(item_ids)
    for item_id in icon_ids:
        icon = icons.get(str(item_id))
        if icon and icon.get("file"):
            icon_rows.append({"id": item_id, "file": icon["file"]})
            ui_sprites.setdefault(icon["file"], icon)

    game_settings = rules["game_settings"]
    selected_settings = {
        "DefaultSkillGachaSettings": {
            "EnsureHighRank": [{"Quality": int(quality), "MissCount": int(count)}
                                for quality, count in game_settings["DefaultSkillGachaSettings"]["EnsureHightRankConfig"].items()],
            "EnsureHighRankPieceCount": game_settings["DefaultSkillGachaSettings"]["EnsureHighRankPieceCount"]},
        "RankSkillGachaSettings": [{"Rank": int(rank),
            "EnsureHighRank": [{"Quality": int(quality), "MissCount": int(count)}
                               for quality, count in setting["EnsureHightRankConfig"].items()],
            "EnsureHighRankPieceCount": setting["EnsureHighRankPieceCount"]}
            for rank, setting in sorted(game_settings["RankSkillGachaSettings"].items(), key=lambda pair: int(pair[0]))],
        "SkillGachaCustomSelectionSettings": {
            "RequiredProfessionRank": game_settings["SkillGachaCustomSelectionSettings"]["RequiredProfessionRank"],
            "SelectionProfessionCount": game_settings["SkillGachaCustomSelectionSettings"]["SelectionProfessionCount"]},
        "SkillGachaGuideFixedRewardId": game_settings["SkillGachaGuideFixedRewardId"],
        "SkillGachaNewbieTimes": game_settings["SkillGachaNewbieTimes"],
        "SkillGachaOnceSupportTimes": game_settings["SkillGachaOnceSupportTimes"],
        "DailyGachaMaxCount": [{"GachaClassType": int(key), "Count": int(count)}
                                for key, count in sorted(game_settings["DailyGachaMaxCountDict"].items(), key=lambda pair: int(pair[0]))],
        "AchievementPanelShowCount": game_settings["AchievementPanelShowCount"],
        "ShopRefreshCoinNeedConfirm": game_settings["ShopRefreshCoinNeedConfirm"]}
    data = {
        "manifest": "854_156383",
        "sourceHashes": {
            "rules": digest(RULES), "ui": digest(UI), "icons": digest(ICONS),
            "performance": digest(PERFORMANCE / "performance.json"),
            "fixedRewards": fixed_reward_hash,
            "friendPanel": digest(FRIEND_PANEL),
        },
        "gachas": gachas,
        "chests": [chest_row(row) for row in chests],
        "rewardRules": reward_rules,
        "randomRewards": random_rewards,
        "levelNumbers": level_numbers,
        "items": [item_row(row) for row in item_rows],
        "professions": professions,
        "skills": [{"id": skill_id, "unlockProfession": unlock_profession[skill_id],
                    "maxRank": (skills_by_id.get(skill_id, {}).get("MaxRankLimit") or skill_rank_max)}
                   for skill_id in skill_ids if skill_id in items_by_id],
        "achievements": [achievement_row(row) for row in achievement_rows],
        "shops": [shop_row(row) for row in shop_defs],
        "shopGoods": [shop_good_row(row) for row in shop_goods],
        "shopGroups": [group_row(row) for row in shop_groups],
        "pityProbabilities": [row for row in rules["gacha_pity_probability"]
                              if row["GachaType"] in (0, 2)],
        "quickBuys": [{key: row.get(key) for key in ("ItemId", "CurrencyId", "PriceList",
                      "PriceBuyCount", "BaseCount", "Condition")}
                      for row in purchase.get("quick_buy", [])],
        "settings": selected_settings,
        "fixedRewards": fixed_rewards,
        "icons": icon_rows,
        "prefabs": ui_prefabs,
        "resultAnimations": result_animations,
        "resultTiming": result_timing,
        "resultAnimationSource": {
            "name": star_animation["name"], "source": star_animation["source"],
            "md5": hashlib.md5((ROOT / ".tools/source-packages/extracted"
                                / star_animation["source"]["bundle"]).read_bytes()).hexdigest(),
            "events": len(star_events),
        },
        "audio": audio_bindings,
        "spriteFiles": sorted(ui_sprites),
        "spriteBindings": [{
            "file": Path(uri).name,
            "border": sprite.get("border") or [0, 0, 0, 0],
            "pixelsPerUnit": float(sprite.get("ppu") or 100),
        } for uri, sprite in sorted(ui_sprites.items())],
    }
    return data


def main():
    source_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != "854_156383":
        raise ValueError("Wish UI source manifest is not pinned to 854_156383.")
    expected = source_manifest.get("data_sha256", {})
    for name, path in (("rules.json", RULES), ("ui.json", UI), ("icons.json", ICONS)):
        if expected.get(name) and expected[name] != digest(path):
            raise ValueError(f"Pinned source hash does not match {name}.")
    rules = json.loads(RULES.read_text(encoding="utf-8"))
    ui = json.loads(UI.read_text(encoding="utf-8"))
    icons = json.loads(ICONS.read_text(encoding="utf-8"))
    purchase = json.loads(PURCHASE.read_text(encoding="utf-8"))
    friend_panel = json.loads(FRIEND_PANEL.read_text(encoding="utf-8"))
    fixed_reward_source = json.loads(FIXED_REWARDS_SOURCE.read_text(encoding="utf-8"))
    guide_reward_id = rules["game_settings"]["SkillGachaGuideFixedRewardId"]
    fixed_reward = next((row for row in fixed_reward_source.get("fixed_reward", [])
                         if int(row.get("Id", 0)) == guide_reward_id), None)
    if fixed_reward is None:
        raise ValueError(f"Pinned guide fixed_reward row is missing: {guide_reward_id}.")
    fixed_rewards = [fixed_reward_row(fixed_reward)]
    fixed_reward_hash = digest(FIXED_REWARDS_SOURCE)
    data = build_runtime(rules, ui, icons, purchase, fixed_rewards, fixed_reward_hash, friend_panel)
    result_animation_source = data.pop("resultAnimationSource")

    outputs = {}
    RUNTIME_ROOT.mkdir(parents=True, exist_ok=True)
    runtime_path = RUNTIME_ROOT / "wishes-runtime.json"
    runtime_path.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    outputs[str(runtime_path.relative_to(ROOT)).replace("\\", "/")] = digest(runtime_path)
    art_root = UNITY_ROOT / "Art/sprites"
    copied = 0
    for uri in data["spriteFiles"]:
        source = source_image(uri)
        if source is None:
            raise FileNotFoundError(f"Official wish sprite was not found: {uri}")
        copy_asset(source, art_root / source.name, outputs)
        copied += 1

    performance_root = UNITY_ROOT / "Performance"
    for source in (PERFORMANCE / "performance.json", *(PERFORMANCE / "sprites").rglob("*.png"),
                   *(PERFORMANCE / "audio").rglob("*")):
        if not source.is_file():
            continue
        target = performance_root / source.relative_to(PERFORMANCE)
        copy_asset(source, target, outputs)

    REFERENCE_ROOT.mkdir(parents=True, exist_ok=True)
    reference = {
        "manifest": "854_156383",
        "inputHashes": {"rules.json": digest(RULES), "ui.json": digest(UI),
                        "icons.json": digest(ICONS),
                        "friend-panel.json": digest(FRIEND_PANEL),
                        "gacha-purchase.json": digest(PURCHASE),
                        "performance.json": digest(PERFORMANCE / "performance.json"),
                        "fixed-reward-table.json": fixed_reward_hash},
        "outputs": outputs,
        "friendPanelSource": json.loads(FRIEND_PANEL_SOURCE.read_text(encoding="utf-8")),
        "resultAnimationSource": result_animation_source,
        "uiSpriteCount": copied,
        "gachaTypes": [0, 1, 2],
        "shopIds": [4, 5],
        "achievementGroups": sorted(ACHIEVEMENT_GROUPS),
    }
    (REFERENCE_ROOT / "source-manifest.json").write_text(
        json.dumps(reference, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wish runtime catalog prepared: {len(data['items'])} item rows, "
          f"{len(data['skills'])} profession skills, {len(data['shopGoods'])} shop goods, "
          f"{copied} official UI sprites, {len(data['prefabs'])} UI prefabs.")


if __name__ == "__main__":
    main()
