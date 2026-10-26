"""Project verified Map13 EC records into the local water runtime's DTOs."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import DESTINATION, RECEIPT, encode, meta, sha, source_path, write


OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Resources/Map13/water-rules.json"
MANIFEST = ROOT / "assets/reference/official-map/map13-water-source-manifest.json"


def build():
    receipt = json.loads(RECEIPT.read_bytes())
    for record in receipt["outputs"]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]:
            raise ValueError("Map13 decoded source was changed: " + record["path"])
    entities = json.loads((DESTINATION / "entities.json").read_bytes())
    templates = json.loads((DESTINATION / "templates.json").read_bytes())
    terminals = json.loads((DESTINATION / "water-terminals.json").read_bytes())
    placement = {row["id"]: row for row in json.loads((DESTINATION / "placement.json").read_bytes())["entities"]}
    ids = {int(value) for group in terminals.values() for key in ("ExtendRoadLoEntityIds", "NeedResetLoEntityIds")
           for value in group[key]}
    water_components = {"WorldConveyanceComponent", "StageResultElevatorStateUpdateComponent",
                        "StageResultElevatorPollingNotifyComponent", "StageResultEntityFireTriggerComponent"}
    ids.update(int(identity) for identity, entity in entities.items()
               if water_components.intersection(templates[str(placement[int(identity)]["entity"])]))
    pending = list(ids)
    while pending:
        local = entities[str(pending.pop())]
        references = list(local.get("LoStageResultEntityFireTriggerComponent", {}).get("TriggerLoEntityIds", []))
        references.extend(local.get("LoStageConUncoveredEntityComponent", {}).get("LoEntityIds", []))
        references.extend(row["LoEntId"] for update in local.get("LoStageResultElevatorStateUpdateComponent", {}).get("DataList", [])
                          for row in update["StageDatas"])
        for identity in references:
            if identity not in entities and str(identity) not in entities:
                raise ValueError("Missing original water target: " + str(identity))
            if identity not in ids:
                ids.add(identity)
                pending.append(identity)
    rules = []
    for identity in sorted(ids):
        local = entities[str(identity)]
        row = placement[identity]
        template = templates[str(row["entity"])]
        stage = template["WorldStageComponent"]
        override = template.get("StageResultOverrideBodyGroundTypeComponent", {})
        source_state = local.get("LoWorldStageComponent", {}).get("State", 0)
        if source_state < 0 or source_state >= len(stage["StateInfos"]):
            raise ValueError("Map13 water source state is out of range: " + str(identity))
        rules.append(dict(PlacementId=identity, EntityClassId=row["entity"], InitialState=source_state,
            HasGridMover="GridMoverComponent" in template,
            OriginalGrid=local["LoGridTransformComponent"]["Pos"],
            InitiallySpawned=local["LoEntityComponent"]["SpanwInfo"]["PrivateMapSupportData"]["SupportSpawnInMap"],
            Body=template["GridTransformComponent"]["BodyRangeInfos"],
            InteractiveRange=stage["InteractiveRange"], Stage=stage,
            SelfStates=template.get("StageResultSelfStateUpdateComponent", {}).get("InfoList", []),
            GroundOperations=override.get("OperationInfos", []),
            GroundData=override.get("PosInfos", []),
            UncoveredEntityClasses=template.get("StageConUncoveredEntityComponent", {}).get("EntityClassId", []),
            UncoveredEntities=local.get("LoStageConUncoveredEntityComponent", {}).get("LoEntityIds", []),
            IgnoreCoveredEntityClasses=template.get("StageResultElevatorStateUpdateComponent", {}).get("IgnoreEntityClassId", []),
            StateUpdates=local.get("LoStageResultElevatorStateUpdateComponent", {}).get("DataList", []),
            TriggerEntities=local.get("LoStageResultEntityFireTriggerComponent", {}).get("TriggerLoEntityIds", []),
            Routes=local.get("LoWorldConveyanceComponent", {}).get("Data", [])))
    by_id = {rule["PlacementId"]: rule for rule in rules}
    for rule in rules:
        references = set(rule["TriggerEntities"] + rule["UncoveredEntities"])
        references.update(row["LoEntId"] for update in rule["StateUpdates"] for row in update["StageDatas"])
        if not references <= set(by_id):
            raise ValueError("Unresolved original water dependency: " + str(sorted(references - set(by_id))))
        if any(len(route["OffsetPath"]) < 2 for route in rule["Routes"]):
            raise ValueError("Original floating platform route has fewer than two points")
        for previous, following in zip(rule["Routes"], rule["Routes"][1:]):
            if previous["OffsetPath"][-1] != following["OffsetPath"][0]:
                raise ValueError("Original floating platform route segments are disconnected")
    document = dict(Manifest=receipt["manifest"], MapClassId=13, Rules=rules,
        Terminals=[dict(PlacementId=int(identity), **value) for identity, value in sorted(terminals.items())])
    write(OUTPUT, encode(document))
    meta(OUTPUT)
    for folder in (OUTPUT.parent.parent, OUTPUT.parent):
        meta(folder, folder=True)
    code_root = ROOT / ".tools/source-packages/current" / receipt["manifest"] / "official-code"
    names = ("WorldConveyanceComponent", "StageResultEntityFireTriggerComponentImplement",
             "StageResultElevatorStateUpdateComponent", "StageConUncoveredEntityComponent",
             "StageResultOverrideBodyGroundTypeComponent", "MapPuzzleTerminalDataSwitchTheRoad")
    manifest = dict(format=1, manifest=receipt["manifest"], mapClassId=13,
        inputs=[dict(path=source_path(RECEIPT), sha256=sha(RECEIPT.read_bytes()))],
        official_code=[dict(path=source_path(code_root / "common-source/Common" / (name + ".cs")),
            sha256=sha((code_root / "common-source/Common" / (name + ".cs")).read_bytes())) for name in names],
        offline_decisions=dict(manual_conveyance="User confirmed: advance one original OffsetPath segment per trigger; reverse at the final endpoint; carry original passenger offsets; blocked route leaves the platform unmoved.",
            water_level="User confirmed: apply original StageDatas in serialized order without rewards.",
            reset="Restore only original NeedResetLoEntityIds state and position.",
            automatic="Consume the original configured item and open ExtendRoadLoEntityIds; rewards require separate original claim."),
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        runtime=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in (
            ROOT / "unity/Assets/Game/Runtime/Offline/World/Map13WaterDefinition.cs",
            ROOT / "unity/Assets/Game/Runtime/Offline/World/OfflineMap13WaterSession.cs")],
        counts=dict(rules=len(rules), platforms=sum(bool(rule["Routes"]) for rule in rules), terminals=len(terminals)),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())))
    write(MANIFEST, encode(manifest))
    print("Map13 water rules:", manifest["counts"])
    return document


if __name__ == "__main__":
    build()
