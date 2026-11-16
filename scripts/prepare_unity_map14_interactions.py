"""Bind Map14 interactions from the active local official source."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from prepare_unity_map13_story_runtime import json_meta, source_record, write_json
from prepare_mainline_interactions import load_current_interaction_inputs

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/interactions.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-interactions-source-manifest.json"


def active_source_version():
    return json.loads((ROOT / "official-source.json").read_bytes())["active"]


def interaction_payload(templates, tables, map_id=14, region_count=111, version=None, hero_ids=None,
                       fast_stage_templates=None):
    version = version or active_source_version()
    statuses = {row["ClassId"]: row for row in tables["status_effect"]}
    configured_heroes = None if hero_ids is None else list(hero_ids)
    selected_heroes = (6570, 6571, 6572, 6573) if configured_heroes is None else configured_heroes
    if not selected_heroes or len(set(selected_heroes)) != len(selected_heroes):
        raise ValueError("Hero template IDs must be nonempty and unique")
    heroes = []
    for entity_id in selected_heroes:
        if str(entity_id) not in templates:
            raise ValueError("Original hero template is missing: " + str(entity_id))
        components = templates[str(entity_id)]
        condition = components["StageConPlayerPeriodicityInteractionComponent"]
        result = components["StageResultPeriodicityRandomAwardComponent"]
        if (condition["PeriodicityInteractionGroupIds"] != [1] or condition["IsNegation"]
                or result["PeriodicityGroupIds"] != [1] or result["PeriodicityRandomAwardGroupIds"] != [2]):
            raise ValueError("Original hero periodic groups require an unported rule")
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
                    raise ValueError("Current hero status requires an unported duration rule")
                resolved.append(dict(StatusEffectId=status_id, FightStatusClassIds=status["FightStatusIds"],
                                     EntryId=status["EntryId"], EffectType=status["EffectType"]))
            effects.append(dict(ComponentType=235, ComponentIndex=index, StatusEffects=resolved))
        awards = [dict({key: row[key] for key in ("PeriodicityGroupId", "AwardGroupId", "PeriodicityId", "Weight",
                                                 "Action", "ActionTime", "IsFirstReward")},
                       StageResults=[dict(ComponentType=int(kind), ComponentIndex=index)
                                     for kind, index in row["StageResultComps"].items()])
                  for row in tables["Map/map_periodicity_random_award_interaction"]
                  if row["PeriodicityGroupId"] == 1 and row["AwardGroupId"] == 2]
        if len(awards) != 3 or sum(row["Weight"] for row in awards) != 100:
            raise ValueError("Current hero periodic reward closure changed")
        identities = {(row["ComponentType"], row["ComponentIndex"]) for row in effects}
        if any((ref["ComponentType"], ref["ComponentIndex"]) not in identities
               for row in awards for ref in row["StageResults"]):
            raise ValueError("Original hero periodic result component/index is unresolved")
        heroes.append(dict(EntityClassId=entity_id, Effects=effects, PeriodicAwards=awards))
    regions = [dict(MapClassId=row["ClassId"], Region=row["Region"], Level=row["Level"],
                    SubRank=row["SubRank"], FastStageCombatRating=row["FastStageCombatRating"],
                    GameFastType=row["GameFastType"], ForbidExplore=row["ForbidExplore"], DropItems=row["DropItems"])
               for row in tables["Map/map_region"] if row["ClassId"] == map_id]
    if sorted(row["Region"] for row in regions) != list(range(region_count)):
        raise ValueError("Original interaction regions changed for map " + str(map_id))
    if fast_stage_templates is None:
        stage_fast_templates = []
        for entity_id, components in templates.items():
            for component_type, value in components.items():
                if not component_type.startswith("StageBeh") or not isinstance(value, dict) or "FastForwardType" not in value:
                    continue
                fast_type = int(value["FastForwardType"])
                if fast_type < 0 or fast_type > 7:
                    raise ValueError("Original FastForwardType is out of range: " + component_type)
                stage_fast_templates.append(dict(EntityClassId=int(entity_id), Component=component_type,
                                                 FastForwardType=fast_type))
        stage_fast_templates.sort(key=lambda row: (row["EntityClassId"], row["Component"]))
    else:
        stage_fast_templates = [dict(row) for row in fast_stage_templates]
        identities = [(row["EntityClassId"], row["Component"]) for row in stage_fast_templates]
        if (any(row["FastForwardType"] < 0 or row["FastForwardType"] > 7 for row in stage_fast_templates)
                or len(identities) != len(set(identities))):
            raise ValueError("Current official FastForwardType template set is invalid")
    payload = dict(manifest=version, mapClassId=map_id, heroes=heroes,
                parameters=[dict(EntityClassId=row["EntityClassId"], CompleteState=row["CompleteState"],
                                 PublicDefaultState=row["PublicDefaultState"], Progress=row["Progress"],
                                 CostItems=row["CostItems"] or []) for row in tables["Map/entity_extend_parameter"]],
                regions=regions, areas=[row for row in tables["Map/map_area"] if row["ClassId"] == map_id],
                stageFastTemplates=stage_fast_templates)
    if configured_heroes is not None:
        payload["heroTemplateIds"] = configured_heroes
    return payload


def build():
    version, templates, tables, stage_fast_types, template_evidence, table_inputs = load_current_interaction_inputs()
    payload = interaction_payload(templates, tables, map_id=14, region_count=111, version=version,
                                  fast_stage_templates=stage_fast_types)
    write_json(OUTPUT, payload)
    json_meta(OUTPUT)
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    official = [code / relative for relative in (
        "client-source/MapControllerEntityRadarTipFastKill.cs",
        "client-source/MapWorldStageInteractionHandler.cs",
        "common-source/MapUtils.cs",
        "common-source/Common/MapStageFastForwardType.cs")]
    for path in official:
        if not path.is_file():
            raise FileNotFoundError("Original statue implementation needs decompilation: " + str(path))
    receipt = dict(format=1, manifest=version, mapClassId=14,
        operation="Convert Map14 hero component/index bindings, ordered periodic weights, original costs, current FastForwardType templates, fast-stage thresholds, configured game fast types and all current Dragon Country region/area bindings.",
        officialSource=source_record(ROOT / "official-source.json"),
        inputs=dict(tables=table_inputs, templates=template_evidence),
        officialCode=[source_record(path) for path in official],
        runtimeBindings=[dict(path=relative, hashLineEndings="LF",
            sha256=hashlib.sha256((ROOT / relative).read_bytes().replace(b"\r\n", b"\n")).hexdigest())
            for relative in (
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs",
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs",
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Battle.cs",
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Catalogs.cs",
                "unity/Assets/Game/Runtime/Presentation/World/OfficialMap11WitchWindow.cs",
                "unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Map14Statues.cs")],
        outputs=[source_record(OUTPUT), source_record(Path(str(OUTPUT) + ".meta"))],
        counts=dict(heroes=len(payload["heroes"]), parameters=len(payload["parameters"]),
                    regions=len(payload["regions"]), areas=len(payload["areas"]),
                    stageFastTemplates=len(stage_fast_types)),
        remaining=["formal mainline state and UI integration", "statue interaction visual effects", "full Map14 gameplay and persistence", "Windows Player release"])
    write_json(RECEIPT, receipt)
    print(json.dumps(receipt["counts"], ensure_ascii=False))
    return receipt


if __name__ == "__main__":
    build()
