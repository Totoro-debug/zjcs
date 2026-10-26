"""Decode native world exploration rules without using the legacy engine payload."""
from pathlib import Path
import json

from official_apk import Apk
from official_tables import Constructors
from prepare_unity_map10_entities import (ROOT, sha256, json_bytes, write_meta,
    MODEL_CATALOG, ADVENTURE_DATA, resolve_display_path)
from prepare_unity_map11_ui import write_ui_payload

OUT = ROOT / "unity/Assets/Game/Resources/World/Mainline/exploration.json"
RECEIPT = ROOT / "assets/reference/official-map/world-exploration-source-manifest.json"


def build():
    apk, decoder, inputs = Apk(reuse_current_cache=True), Constructors(), {}

    def table(name, kind, map_table=False):
        obj = apk.asset("config_luban_map" if map_table else "config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        inputs[name] = dict(apk.provenance(obj), sha256=sha256(raw))
        return decoder.table(raw, kind)

    settings = table("game_settings", "TbGameSettings")
    settings = settings[0] if isinstance(settings, list) else settings
    tokens = table("explore_token_group", "TbExploreTokenGroupInfo")
    explore_groups = table("explore_entity_group", "TbExploreEntityGroupInfo")
    groups = {value for row in explore_groups for value in row["EntityGroups"]}
    groups.add(51)  # Native WorldExplore guide result group; confirmed offline first-success policy.
    entity_groups = [row for row in table("map_entity_group", "TbMapEntityGroupInfo", True) if row["Group"] in groups]
    condition_rows = table("conditions", "TbConditionInfo")
    function_limits = [row for row in table('function_limit', 'TbFunctionLimitInfo') if row['Type'] in (8, 51)]
    fixed_reward = next(row for row in table('fixed_reward','TbFixedRewardInfo')
                        if row['Id'] == settings['WorldExploreGuideFixedRewardId'])
    needed_conditions = set()

    def collect(condition):
        if condition and condition["_type"] in ("CompositeAndCondition", "CompositeOrCondition"):
            for key in condition["Ids"]:
                if key not in needed_conditions:
                    needed_conditions.add(key)
                    collect(next(row["Condition"] for row in condition_rows if row["ClassId"] == key))

    for row in entity_groups:
        collect(row["FilterCondition"])
    for row in function_limits:
        collect(row['Condition'])
    reward_ids = set()
    parameters = table("entity_extend_parameter", "TbEntityExtendParameterInfo", True)
    entity_ids = {entity for row in entity_groups for entity in row["Entitys"]}
    templates_path = ROOT / 'assets/official/map-entities.json'
    template_raw = templates_path.read_bytes()
    template_receipt = json.loads((ROOT / 'assets/reference/official-map/entities-source-manifest.json').read_text(encoding='utf-8'))
    if template_receipt['manifest'] != '854_156383' or template_receipt['output_sha256'] != sha256(template_raw):
        raise ValueError('Exploration EC templates differ from their verified source')
    templates = json.loads(template_raw)['templates']
    inputs['entityTemplates'] = dict(path=templates_path.relative_to(ROOT).as_posix(), sha256=sha256(template_raw))
    model_catalog = json.loads(MODEL_CATALOG.read_text(encoding='utf-8'))
    displays = json.loads(ADVENTURE_DATA.read_text(encoding='utf-8'))['tables']['Map/entity_map_display']
    for key, path in [('displayCatalog', MODEL_CATALOG), ('displayTable', ADVENTURE_DATA)]:
        inputs[key] = dict(path=path.relative_to(ROOT).as_posix(), sha256=sha256(path.read_bytes()))
    for entity in entity_ids:
        for key, field in [('StageResultAwardComponent','AwardInfos'),('StageResultCooperateChestComponent','AwardRuleIds')]:
            for value in templates[str(entity)].get(key,{}).get(field,[]):
                reward_ids.update(value['RuleIds'])
    reward_rules = [row for row in table('reward_rule','TbRewardRuleInfo') if row['Id'] in reward_ids]
    missing = reward_ids - {row['Id'] for row in reward_rules}
    if missing != {711,721,722,732,735,742,745}:
        raise ValueError('Unconfirmed empty exploration award references: '+str(missing))
    package_ids = {item for row in reward_rules for item in row['IdList']}
    package_ids.update(item for row in entity_groups for extra in row['ExtraDataInfos']
                       if extra['_type']=='MapEntityGroupInfoExtraAwardItemData' for item in extra['AwardItemIds'])
    packages = [row for row in table('random_reward','TbRandomRewardInfo') if row['Id'] in package_ids]
    treasures = table('treasure_gacha','TbTreasureGachaInfo')
    for row in reward_rules + treasures:
        collect(row['Condition'])
    activity = next(row for row in table('activity','TbActivityInfo') if row['ActivityType']==49 and row['Id']==220)
    needed_conditions.add(activity['OpenConditionId'])
    collect(next(row['Condition'] for row in condition_rows if row['ClassId']==activity['OpenConditionId']))
    items = table('item','TbItemInfo')
    item_ids = {item for row in packages for item in (row['IdList'] or [])}
    item_ids.update(item for row in treasures for item in row['ChestGachaItems'])
    item_ids.update(row['ItemId'] for row in tokens if row['ItemId'] > 0)
    item_ids.update(item for row in reward_rules for item in row['IdList'])
    item_ids.update(item for row in entity_groups for extra in row['ExtraDataInfos']
                    if extra['_type']=='MapEntityGroupInfoExtraAwardItemData' for item in extra['AwardItemIds'])
    item_ids.update(int(key) for key in fixed_reward['Reward'])
    payload = dict(manifest="854_156383", tokenGroups=tokens, exploreGroups=explore_groups,
                   entityGroups=entity_groups,
                   entities=[dict(entityClassId=entity, components=templates[str(entity)],
                       displayPaths=[dict(mapClassId=map_id, path=resolve_display_path(entity,model_catalog,displays,map_id)) for map_id in (11,)],
                       templateComponents=[dict(Type=kind,Json=json.dumps(value,ensure_ascii=False,separators=(',',':')))
                           for kind,value in templates[str(entity)].items()],
                       parameters=next(row for row in parameters if row['EntityClassId'] == entity))
                       for entity in sorted(entity_ids)],
                   spawners=[dict(mapClassId=entity, **templates[str(entity)]['AreaExploreSpawnerComponent']) for entity in (10, 11)],
                   functionLimits=function_limits,
                   playerMapLevels=table('player_map_level', 'TbPlayerMapLevelInfo', True),
                   rewardRules=reward_rules, randomRewards=packages, treasureGachas=treasures,
                   rewardLevelNumbers=[row for row in table('level_number','TbLevelNumber') if row['Id'] in (1,6)],
                   items=[{key:row[key] for key in ['ClassId','Quality','ElementType']} for row in items if row['ClassId'] in item_ids],
                   cooperativeActivity=activity,
                   professions=[dict(Profession=row['Profession'],PrePro=row['PrePro']) for row in table('profession_base','TbProfessionBaseInfo')],
                   guideReward=dict(Id=fixed_reward['Id'],CanGetTimes=fixed_reward['CanGetTimes'],
                       Items=[dict(ItemId=int(key),Count=value) for key,value in fixed_reward['Reward'].items()]),
                   conditions=[row for row in condition_rows if row["ClassId"] in needed_conditions],
                   limits=table("map_explore_limit", "TbMapExploreLimitInfo"),
                   settings={key: settings[key] for key in ("FoodId", "ExploreFoodCost", "ExploreFoodTokenCost",
                       "WorldExploreMaxTimes", "WorldExploreGuideFixedRewardId",
                       "ExploreEntityMaxDist", "ExploreHistoryRecordDays", "WorldExploreActionId", "WorldExploreMoreTimesActionId",
                       "DailyGachaMaxCountDict", "DailyResetTime")}, dailyLimit=settings['DailyGachaMaxCountDict'][3])
    known = {}
    if RECEIPT.exists():
        old = json.loads(RECEIPT.read_text(encoding="utf-8"))
        known[OUT.resolve()] = old["output"]["sha256"]
        known[RECEIPT.resolve()] = sha256(RECEIPT.read_bytes())
    raw = json_bytes(payload)
    write_ui_payload(OUT, raw, known)
    receipt = dict(manifest="854_156383", inputs=inputs,
                   output=dict(path=OUT.relative_to(ROOT).as_posix(), sha256=sha256(raw)),
                   converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha256(Path(__file__).read_bytes())))
    write_ui_payload(RECEIPT, json_bytes(receipt), known)
    write_meta(OUT, "TextScriptImporter")
    print("World exploration:", len(tokens), "token groups,", len(entity_groups), "native entity group variants")


if __name__ == "__main__":
    build()
