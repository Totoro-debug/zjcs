"""Bind current Dragon Country EC templates and original statue interaction data."""
from __future__ import annotations

import hashlib
import inspect
import json
from pathlib import Path

from prepare_unity_map13_story_runtime import json_meta, source_record, write_json
from prepare_unity_map11_runtime_data import build_witch_results

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/Source"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map14-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/interactions.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-interactions-source-manifest.json"
VERSION = "871_156598"


def load_inputs():
    receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    if receipt["manifest"] != VERSION or receipt["mapClassId"] != 14:
        raise ValueError("Dragon Country interaction source version changed")
    expected = {row["path"]: row["sha256"] for row in receipt["outputs"]}
    paths = [SOURCE / name for name in ("templates.json", "tables.json")]
    for path in paths:
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected[path.relative_to(ROOT).as_posix()]:
            raise ValueError("Original Dragon Country interaction input changed: " + path.name)
    templates, tables = (json.loads(path.read_bytes()) for path in paths)
    if len(templates) != 169 or sum(len(value) for value in templates.values()) != 1483:
        raise ValueError("Dragon Country original EC template closure changed")
    # Keep the original component order and raw fields; stage result indices
    # address these exact arrays inside each original component.
    entities = [{"EntityClassId": int(key), "Components": [
        {"Type": name, "Json": json.dumps(value, ensure_ascii=False, separators=(",", ":"))}
        for name, value in components.items()]} for key, components in templates.items()]
    return templates, tables, entities, paths


def witch_payload(tables, entities):
    effects, awards = build_witch_results(tables, entities, tables["reward_rule"], map_id=14)
    infos = [{"ActiveCount": row["WitchActivedCount"], "StoryBookId": row["StoryBookId"],
              "IsNextState": row["IsNextState"], "StageResults": [
                  {"ComponentType": int(kind), "ComponentIndex": index}
                  for kind, index in row["StageResultComps"].items()]} for row in tables["witch"]]
    if [row["ActiveCount"] for row in infos] != list(range(14)):
        raise ValueError("Original witch activation progression changed")
    identities = {(row["ComponentType"], row["ComponentIndex"]) for row in effects}
    if any((ref["ComponentType"], ref["ComponentIndex"]) not in identities
           for row in infos + awards for ref in row["StageResults"]):
        raise ValueError("Original witch result component/index is unresolved")
    return dict(witchInfos=infos, witchResultEffects=effects, witchPeriodicityAwards=awards)


def interaction_payload(templates, tables):
    statuses = {row["ClassId"]: row for row in tables["status_effect"]}
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
                    SubRank=row["SubRank"], ForbidExplore=row["ForbidExplore"], DropItems=row["DropItems"])
               for row in tables["Map/map_region"] if row["ClassId"] == 14]
    if sorted(row["Region"] for row in regions) != list(range(111)):
        raise ValueError("Dragon Country interaction regions changed")
    return dict(manifest=VERSION, mapClassId=14, heroes=heroes,
                parameters=[dict(EntityClassId=row["EntityClassId"], CompleteState=row["CompleteState"],
                                 PublicDefaultState=row["PublicDefaultState"], Progress=row["Progress"],
                                 CostItems=row["CostItems"] or []) for row in tables["Map/entity_extend_parameter"]],
                regions=regions, areas=[row for row in tables["Map/map_area"] if row["ClassId"] == 14])


def build():
    templates, tables, entities, paths = load_inputs()
    witch = witch_payload(tables, entities)
    payload = interaction_payload(templates, tables)
    write_json(OUTPUT, payload)
    json_meta(OUTPUT)
    code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    official = [code / "common-source/Common" / (name + ".cs") for name in (
        "MapPeriodicityRandomAwardInteractionInfoParser", "MapEntityPeriodicityInteractionInfoPackParser",
        "StageResultAwardExtraPropComponentInfo", "StageResultSpawnEntityComponentInfo", "WitchInfo")]
    for path in official:
        if not path.is_file():
            raise FileNotFoundError("Original statue implementation needs decompilation: " + str(path))
    receipt = dict(format=1, manifest=VERSION, mapClassId=14,
        operation="Preserve current official hero effect component/index, ordered periodic weights, original costs and all Dragon Country region/area bindings. Resolve witch spawn against Map14 rather than another map.",
        inputs=[source_record(path) for path in [SOURCE_RECEIPT, *paths, Path(__file__),
            ROOT / "scripts/prepare_unity_map13_story_runtime.py",
            ROOT / "scripts/prepare_mainline_interactions.py"]],
        officialCode=[source_record(path) for path in official],
        runtimeBindings=[dict(path=relative, hashLineEndings="LF",
            sha256=hashlib.sha256((ROOT / relative).read_bytes().replace(b"\r\n", b"\n")).hexdigest())
            for relative in (
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs",
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Catalogs.cs",
                "unity/Assets/Game/Runtime/Presentation/World/OfficialMap11WitchWindow.cs",
                "unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Map14Statues.cs")],
        sharedPort=dict(path="scripts/prepare_unity_map11_runtime_data.py", symbol="build_witch_results",
                        sourceSha256LF=hashlib.sha256(inspect.getsource(build_witch_results).replace("\r\n", "\n").encode()).hexdigest()),
        outputs=[source_record(OUTPUT), source_record(Path(str(OUTPUT) + ".meta"))],
        counts=dict(entityTemplates=len(entities), components=sum(len(row["Components"]) for row in entities),
                    heroes=len(payload["heroes"]), parameters=len(payload["parameters"]), regions=len(payload["regions"]),
                    areas=len(payload["areas"]), **{key: len(value) for key, value in witch.items()}),
        remaining=["formal mainline state and UI integration", "statue interaction visual effects", "full Map14 gameplay and persistence", "Windows Player release"])
    write_json(RECEIPT, receipt)
    print(json.dumps(receipt["counts"], ensure_ascii=False))
    return receipt


if __name__ == "__main__":
    build()
