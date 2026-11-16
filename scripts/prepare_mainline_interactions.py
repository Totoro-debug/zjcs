"""Resolve shared map interactions from the active APK and decoded EC templates."""
from __future__ import annotations

import json
from pathlib import Path

from official_apk import Apk
from official_ec_codec import Formats
from official_map_codec import unpack
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr
from prepare_unity_map10_entities import ROOT, sha256, json_bytes, write_meta
from prepare_unity_map11_ui import write_ui_payload
from prepare_unity_character_catalog import decode_level_props

OUT = ROOT / "unity/Assets/Game/Resources/World/Mainline"
RECEIPT = ROOT / "assets/reference/official-map/mainline-interactions-source-manifest.json"
FAST_STAGE_TEMPLATE_COMPONENTS = {
    str(entity_id): {"StageResultAwardExtraPropComponent", "StageResultModifyHpPercentComponent",
                     "StageResultAwardStatusEffectComponent"}
    for entity_id in (6570, 6571, 6572, 6573, 6574, 6575)
}
INTERACTION_HERO_TEMPLATE_COMPONENTS = {
    str(entity_id): {"StageConPlayerPeriodicityInteractionComponent",
                     "StageResultPeriodicityRandomAwardComponent"}
    for entity_id in (6570, 6571, 6572, 6573, 6574, 6575)
}
INTERACTION_TEMPLATE_COMPONENTS = {
    **FAST_STAGE_TEMPLATE_COMPONENTS,
    **{identity: FAST_STAGE_TEMPLATE_COMPONENTS.get(identity, set()) | components
       for identity, components in INTERACTION_HERO_TEMPLATE_COMPONENTS.items()},
    "8011": {"StageResultAwardComponent"},
    "8021": {"StageResultAwardComponent"},
    "8026": {"StageResultAwardComponent"},
}


def load_fast_stage_templates(apk):
    formats = Formats()
    templates = {}
    fast_stage_templates = {}
    selected = {}
    superseded = []
    source_records = {}
    for path, pointer in apk.container("config_ec"):
        name = path.rsplit("/", 1)[-1]
        if not name.startswith(("World", "ActionWorld", "ActionCommon", "Map", "map_", "ActivityMusicFestiva")):
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        prefix = ""
        try:
            rows = unpack(raw)[0]
        except Exception:
            rows = unpack(raw[2:])[0]
            prefix = raw[:2].hex()
        for parent, identity, components in rows:
            identity = str(identity)
            if parent:
                continue
            values = {}
            stage_values = {}
            for component_type, data in components:
                component_type = component_type.removeprefix("Common.")
                if (component_type not in INTERACTION_TEMPLATE_COMPONENTS.get(identity, set())
                        and not component_type.startswith("StageBeh")):
                    continue
                value = formats.component(component_type, data[2:] if prefix and data else data)
                if component_type in INTERACTION_TEMPLATE_COMPONENTS.get(identity, set()):
                    values[component_type] = value
                if component_type.startswith("StageBeh") and isinstance(value, dict) and "FastForwardType" in value:
                    fast_type = int(value["FastForwardType"])
                    if fast_type < 0 or fast_type > 7:
                        raise ValueError("Current official FastForwardType is out of range: " + component_type)
                    stage_values[component_type] = fast_type
            if identity not in INTERACTION_TEMPLATE_COMPONENTS and not stage_values:
                continue
            source = dict(path=path, legacy=bool(prefix))
            if identity in selected:
                if prefix and not selected[identity]["legacy"]:
                    superseded.append(dict(entityClassId=int(identity), discarded=source,
                                           selected=selected[identity]))
                    continue
                superseded.append(dict(entityClassId=int(identity), discarded=selected[identity], selected=source))
            if identity in INTERACTION_TEMPLATE_COMPONENTS:
                templates[identity] = values
            fast_stage_templates[identity] = stage_values
            selected[identity] = source
            source_records[path] = dict(apk.provenance(obj), sha256=sha256(raw))

    missing = {}
    for required in (FAST_STAGE_TEMPLATE_COMPONENTS, INTERACTION_HERO_TEMPLATE_COMPONENTS):
        for identity, component_types in required.items():
            missing[identity] = sorted(set(missing.get(identity, ()))
                                       | (component_types - set(templates.get(identity, {}))))
    missing = {identity: components for identity, components in missing.items() if components}
    if missing:
        raise ValueError("Current official EC templates are missing fast-stage dependencies: " + str(missing))

    stage_fast_types = [dict(EntityClassId=int(identity), Component=component_type, FastForwardType=fast_type)
                        for identity, components in fast_stage_templates.items()
                        for component_type, fast_type in components.items()]
    stage_fast_types.sort(key=lambda row: (row["EntityClassId"], row["Component"]))
    if not stage_fast_types:
        raise ValueError("Current official EC templates contain no map stage FastForwardType values")
    selected_by_path = {}
    for identity, source in selected.items():
        selected_by_path.setdefault(source["path"], []).append(int(identity))
    sources = [dict(source_records[path], entityClassIds=sorted(identities))
               for path, identities in sorted(selected_by_path.items())]
    formatter_sources = [dict(path=path.relative_to(ROOT).as_posix(), sha256=sha256(path.read_bytes()))
                         for path in sorted(set(formats.sources.values()))]
    return templates, stage_fast_types, dict(sources=sources, selected=selected, superseded=superseded,
        formatters=formatter_sources, stageFastTypesSha256=sha256(json_bytes(stage_fast_types)))


def load_current_interaction_inputs():
    apk, constructors = Apk(), Constructors()
    templates, stage_fast_types, template_evidence = load_fast_stage_templates(apk)
    input_records = {}

    def table(name, kind, map_table=True):
        obj = apk.asset("config_luban_map" if map_table else "config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        input_records[name] = dict(apk.provenance(obj), sha256=sha256(raw))
        return constructors.table(raw, kind)

    tables = {
        "status_effect": table("status_effect", "TbStatusEffectInfo", False),
        "Map/map_periodicity_random_award_interaction": table(
            "map_periodicity_random_award_interaction", "TbMapPeriodicityRandomAwardInteractionInfo"),
        "Map/map_region": table("map_region", "TbMapRegionInfo"),
        "Map/map_area": table("map_area", "TbMapAreaInfo"),
        "Map/entity_extend_parameter": table("entity_extend_parameter", "TbEntityExtendParameterInfo"),
    }
    template_evidence["decoded_sha256"] = sha256(json_bytes(templates))
    return apk.version, templates, tables, stage_fast_types, template_evidence, input_records


def build():
    apk, constructors, schemas = Apk(), Constructors(), ScriptSchemas()
    source_manifest = apk.version
    templates, stage_fast_types, template_evidence = load_fast_stage_templates(apk)
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
    level_prop_path = ROOT / ".tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile"
    level_prop_raw = level_prop_path.read_bytes()
    character_receipt = json.loads((ROOT / "unity/Assets/Official/Character/source-manifest.json").read_text(encoding="utf-8"))
    level_records = [value for value in character_receipt["inputs"]
                     if isinstance(value, dict) and value.get("path") == level_prop_path.relative_to(ROOT).as_posix()]
    if len(level_records) != 1 or level_records[0]["sha256"] != sha256(level_prop_raw):
        raise ValueError("Hero LevelProp input differs from its verified character source")
    inputs["heroLevelProps"] = dict(path=level_prop_path.relative_to(ROOT).as_posix(), sha256=sha256(level_prop_raw),
                                    decoder="LevelPropBinaryReader indexed signed-varint rows")
    inputs["entityTemplateFormatters"] = template_evidence["formatters"]
    template_evidence["decoded_sha256"] = sha256(json_bytes(templates))
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
        if entity_id == 5011:
            native_stage = next(row for row in stage_rows if row["Id"] == 5004)
            if not native_stage["AwardUsePlayerLevelAndSubRank"] or native_stage["Awards"] != [82, 6]:
                raise ValueError("Native public map monster award binding changed")
            rule_ids, addition = native_stage["Awards"], 29
        else:
            components = templates[str(entity_id)]
            award = components["StageResultAwardComponent"]["AwardInfos"][0]
            rule_ids, addition = award["RuleIds"], award["AdditionPropType"]
        stamina_entities.append(dict(EntityClassId=entity_id, RewardRuleIds=rule_ids, AdditionPropType=addition))
    selected_rewards = [row for row in rewards if row["Id"] in (82, 6, 115, 183, 194)]
    for row in selected_rewards:
        condition = row.pop("Condition")
        if condition and condition["_type"] != "MapGroupLocationCondition":
            raise ValueError("A stamina reward requires an unported condition")
        row["MapGroupId"] = condition["MapGroupId"] if condition else 0
    payload = dict(manifest=source_manifest, heroes=heroes, staminaEntities=stamina_entities,
                   stageFastTemplates=stage_fast_types,
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
                                 SubRank=row["SubRank"], FastStageCombatRating=row["FastStageCombatRating"],
                                 GameFastType=row["GameFastType"], ForbidExplore=row["ForbidExplore"],
                                 DropItems=row["DropItems"])
                            for row in regions if 10 <= row["ClassId"] <= 16])
    target = OUT / "interactions.json"
    raw = json_bytes(payload)
    known = {}
    if RECEIPT.exists():
        old = json.loads(RECEIPT.read_text(encoding="utf-8"))
        if target.exists():
            previous_output = target.read_bytes()
            if sha256(previous_output.replace(b"\r\n", b"\n")) != old["output"]["sha256"]:
                raise ValueError("Shared interaction payload differs from its canonical source receipt")
            known[target.resolve()] = sha256(previous_output)
        known[RECEIPT.resolve()] = sha256(RECEIPT.read_bytes())
    OUT.mkdir(parents=True, exist_ok=True)
    write_ui_payload(target, raw, known)
    provenance = dict(manifest=source_manifest, operation="Preserve current official EC result type/index and static stage FastForwardType values, ordered random weights, native costs, region levels, fast-stage combat thresholds and configured game fast types.",
                      inputs=inputs, templates=template_evidence,
                      schemas=schemas.records,
                      runtimeBindings=[dict(path=relative, hashLineEndings="LF",
                          sha256=sha256((ROOT / relative).read_bytes().replace(b"\r\n", b"\n")))
                          for relative in (
                              "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs",
                              "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs",
                              "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Battle.cs",
                              "unity/Assets/Game/Runtime/Offline/Progression/OfflineMapStaminaService.cs",
                              "unity/Assets/Game/Runtime/Offline/Progression/OfficialHeroStatueProperties.cs")],
                      output=dict(path=target.relative_to(ROOT).as_posix(), sha256=sha256(raw)),
                      converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha256(Path(__file__).read_bytes())))
    write_ui_payload(RECEIPT, json_bytes(provenance), known)
    write_meta(OUT, "DefaultImporter")
    write_meta(target, "TextScriptImporter")
    print("Shared map interactions:", len(heroes), "heroes,", len(parameters), "entity parameters")


if __name__ == "__main__":
    build()
