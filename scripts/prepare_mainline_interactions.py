"""Resolve shared map interactions from the pinned APK and decoded EC templates.

The map runtime is pinned to 854_156383. Source and output hashes are recorded
separately from the active cloud profile; no expected hash is rewritten.
"""
from __future__ import annotations

import json
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr
from prepare_unity_map10_entities import ROOT, sha256, json_bytes, write_meta
from prepare_unity_map11_ui import write_ui_payload
from prepare_unity_character_catalog import decode_level_props

OUT = ROOT / "unity/Assets/Game/Resources/World/Mainline"
RECEIPT = ROOT / "assets/reference/official-map/mainline-interactions-source-manifest.json"


def build():
    apk, constructors, schemas = Apk(), Constructors(), ScriptSchemas()
    inputs = {}

    def table(name, kind):
        obj = apk.asset("config_luban" if name in ("status_effect", "reward_rule", "random_reward", "level_number", "stage")
                        else "config_luban_map", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        inputs[name] = dict(apk.provenance(obj), sha256=sha256(raw))
        return constructors.table(raw, kind)

    parameters = table("entity_extend_parameter", "TbEntityExtendParameterInfo")
    periodic = table("map_periodicity_random_award_interaction", "TbMapPeriodicityRandomAwardInteractionInfo")
    regions = table("map_region", "TbMapRegionInfo")
    areas = table("map_area", "TbMapAreaInfo")
    rewards = table("reward_rule", "TbRewardRuleInfo")
    random_rewards = table("random_reward", "TbRandomRewardInfo")
    level_numbers = table("level_number", "TbLevelNumber")
    stage_rows = table("stage", "TbStageInfo")
    statuses = {row["ClassId"]: row for row in table("status_effect", "TbStatusEffectInfo")}
    templates_path = ROOT / "assets/official/map-entities.json"
    template_receipt = ROOT / "assets/reference/official-map/entities-source-manifest.json"
    receipt = json.loads(template_receipt.read_text(encoding="utf-8"))
    if receipt["manifest"] != "854_156383" or receipt["output_sha256"] != sha256(templates_path.read_bytes()):
        raise ValueError("Decoded EC templates do not match their official source receipt")
    templates = json.loads(templates_path.read_text(encoding="utf-8"))["templates"]
    level_prop_path = ROOT / ".tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile"
    level_prop_raw = level_prop_path.read_bytes()
    character_receipt = json.loads((ROOT / "unity/Assets/Official/Character/source-manifest.json").read_text(encoding="utf-8"))
    level_records = [value for value in character_receipt["inputs"]
                     if isinstance(value, dict) and value.get("path") == level_prop_path.relative_to(ROOT).as_posix()]
    if len(level_records) != 1 or level_records[0]["sha256"] != sha256(level_prop_raw):
        raise ValueError("Hero LevelProp input differs from its verified character source")
    inputs["heroLevelProps"] = dict(path=level_prop_path.relative_to(ROOT).as_posix(), sha256=sha256(level_prop_raw),
                                    decoder="LevelPropBinaryReader indexed signed-varint rows")
    hero_props = decode_level_props(level_prop_raw, {410})["410"]
    heroes = []
    for entity_id in (6570, 6571, 6572, 6573):
        components = templates[str(entity_id)]
        effects = []
        for index, info in enumerate(components["StageResultAwardExtraPropComponent"]["AwardExtraPropInfos"]):
            effects.append(dict(ComponentType=238, ComponentIndex=index, ExtraProps=info["AwardExtraProps"]))
        for index, amount in enumerate(components["StageResultModifyHpPercentComponent"]["HpModifyInfos"]):
            effects.append(dict(ComponentType=212, ComponentIndex=index, HpPercentChange=amount))
        for index, info in enumerate(components["StageResultAwardStatusEffectComponent"]["AwardStatusEffectInfos"]):
            resolved = []
            for status_id in info["mAwardStatusEffects"]:
                status = statuses[status_id]
                if status["Duration"] != 0 or status["MaxActivateTimes"] != 1:
                    raise ValueError("A hero effect is not the confirmed one-battle status")
                resolved.append(dict(StatusEffectId=status_id, FightStatusClassIds=status["FightStatusIds"],
                                     EntryId=status["EntryId"], EffectType=status["EffectType"]))
            effects.append(dict(ComponentType=235, ComponentIndex=index, StatusEffects=resolved))
        awards = [dict({key: row[key] for key in ("PeriodicityGroupId", "AwardGroupId", "PeriodicityId", "Weight",
                                                 "Action", "ActionTime", "IsFirstReward")},
                       StageResults=[dict(ComponentType=int(kind), ComponentIndex=index)
                                     for kind, index in row["StageResultComps"].items()])
                  for row in periodic if row["PeriodicityGroupId"] == 1 and row["AwardGroupId"] == 2]
        heroes.append(dict(EntityClassId=entity_id, Effects=effects, PeriodicAwards=awards))

    config = apk.asset("scriptableconfig_modules_mapcommon", "/WorldStageInteractionPreviewDialogSetting.asset")
    config_data = schemas.read(config)
    playable = PPtr(**config_data["mPreviewPlayableConfigs"][0]["Playable"], assetsfile=config.assets_file).deref()
    inputs["previewDialogSettings"] = dict(apk.provenance(config), sha256=sha256(config.get_raw_data()))
    inputs["heroActivationPlayable"] = dict(apk.provenance(playable), sha256=sha256(playable.get_raw_data()),
                                           name=playable.read().m_Name)
    # These four public refresh templates are the native stamina gathering /
    # repeatable battle points. Keep the two-tier random reward expansion.
    stamina_entities = []
    for entity_id in (8011, 8021, 8026, 5011):
        components = templates[str(entity_id)]
        if entity_id == 5011:
            native_stage = next(row for row in stage_rows if row["Id"] == 5004)
            if not native_stage["AwardUsePlayerLevelAndSubRank"] or native_stage["Awards"] != [82, 6]:
                raise ValueError("Native public map monster award binding changed")
            rule_ids, addition = native_stage["Awards"], 29
        else:
            award = components["StageResultAwardComponent"]["AwardInfos"][0]
            rule_ids, addition = award["RuleIds"], award["AdditionPropType"]
        stamina_entities.append(dict(EntityClassId=entity_id, RewardRuleIds=rule_ids, AdditionPropType=addition))
    selected_rewards = [row for row in rewards if row["Id"] in (82, 6, 115, 183, 194)]
    for row in selected_rewards:
        condition = row.pop("Condition")
        if condition and condition["_type"] != "MapGroupLocationCondition":
            raise ValueError("A stamina reward requires an unported condition")
        row["MapGroupId"] = condition["MapGroupId"] if condition else 0
    payload = dict(manifest="854_156383", heroes=heroes, staminaEntities=stamina_entities,
                   heroLevelProps=[dict(Id=410, Level=int(level), Types=[int(key) for key in props], Values=list(props.values()))
                                   for level, props in hero_props["rows"].items()],
                   rewards=selected_rewards,
                   randomRewards=[row for row in random_rewards if row["Id"] in (50000, 50029, 50045)],
                   levelNumbers=[row for row in level_numbers if row["Id"] == 6],
                   areas=[row for row in areas if row["ClassId"] in (10, 11)],
                   parameters=[dict(EntityClassId=row["EntityClassId"], CompleteState=row["CompleteState"],
                                    PublicDefaultState=row["PublicDefaultState"], Progress=row["Progress"],
                                    CostItems=row["CostItems"] or []) for row in parameters],
                   regions=[dict(MapClassId=row["ClassId"], Region=row["Region"], Level=row["Level"],
                                 SubRank=row["SubRank"], ForbidExplore=row["ForbidExplore"], DropItems=row["DropItems"])
                            for row in regions if row["ClassId"] in (10, 11)])
    target = OUT / "interactions.json"
    raw = json_bytes(payload)
    known = {}
    if RECEIPT.exists():
        old = json.loads(RECEIPT.read_text(encoding="utf-8"))
        known[target.resolve()] = old["output"]["sha256"]
        known[RECEIPT.resolve()] = sha256(RECEIPT.read_bytes())
    OUT.mkdir(parents=True, exist_ok=True)
    write_ui_payload(target, raw, known)
    provenance = dict(manifest="854_156383", operation="Preserve EC result type/index, ordered random weights, native cost items and region levels.",
                      inputs=inputs, templates=dict(path=templates_path.relative_to(ROOT).as_posix(), sha256=sha256(templates_path.read_bytes())),
                      schemas=schemas.records,
                      output=dict(path=target.relative_to(ROOT).as_posix(), sha256=sha256(raw)),
                      converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha256(Path(__file__).read_bytes())))
    write_ui_payload(RECEIPT, json_bytes(provenance), known)
    write_meta(OUT, "DefaultImporter")
    write_meta(target, "TextScriptImporter")
    print("Shared map interactions:", len(heroes), "heroes,", len(parameters), "entity parameters")


if __name__ == "__main__":
    build()
