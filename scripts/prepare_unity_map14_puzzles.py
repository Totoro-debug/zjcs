"""Bind Dragon Country puzzle components from the selected official decode.

The cannon and auxiliary/reset execution boundaries are user-confirmed offline
rules. Original stage/result ordering, targets and placement fields stay intact.
"""
from pathlib import Path
import json
import re

from official_apk import ROOT
from prepare_unity_map14_sources import DESTINATION, RECEIPT, encode, meta, sha, source_path, write


OUTPUT = ROOT / "unity/Assets/Official/World/Map/14/Resources/Map14/puzzle-rules.json"
PROOF = ROOT / "assets/reference/official-map/map14-puzzles-source-manifest.json"


def inline(text):
    return json.loads(re.sub(r"([A-Za-z][A-Za-z0-9_]*):", r'"\1":', text)) if text else {}


def build():
    receipt = json.loads(RECEIPT.read_bytes())
    if (receipt["manifest"], receipt["mapClassId"]) != ("871_156598", 14):
        raise ValueError("Wrong official Dragon Country source")
    inputs = []
    for record in receipt["outputs"]:
        path = ROOT / record["path"]
        if sha(path.read_bytes()) != record["sha256"]:
            raise ValueError("Decoded official source changed: " + str(path))
        inputs.append(dict(path=record["path"], sha256=record["sha256"]))
    local = json.loads((DESTINATION / "entities.json").read_bytes())
    templates = json.loads((DESTINATION / "templates.json").read_bytes())
    tables = json.loads((DESTINATION / "tables.json").read_bytes())
    placed = {r["id"]: r for r in json.loads((DESTINATION / "placement.json").read_bytes())["entities"]}
    terminals = {int(k): v["LoEntityPuzzleTerminalComponent"] for k, v in local.items()
                 if "LoEntityPuzzleTerminalComponent" in v}
    expected = {4: [1512, 2914, 5015, 6912, 8011, 8516, 9413], 6: [909, 1715, 3612, 4214, 4505]}
    for kind, identities in expected.items():
        if sorted(k for k, v in terminals.items() if v["MapWorldPuzzleType"] == kind) != identities:
            raise ValueError("Original terminal group changed: " + str(kind))
    if len(terminals) != 12:
        raise ValueError("Additional original terminal type requires migration")
    ids, pending = set(terminals), list(terminals)
    while pending:
        identity = pending.pop()
        entity = local[str(identity)]
        lock = entity.get("LoEntityLockComponent", {})
        if lock.get("LockType", 0) not in (0, 7):
            raise ValueError("Additional original lock implementation required")
        params = inline(lock.get("Params", ""))
        if params.get("IsMetEntityIdSet"):
            raise ValueError("Restore original pre-activated step-grid state")
        terminal = terminals.get(identity, {})
        data = inline(terminal.get("Params", ""))
        targets = params.get("LoEntityIds", []) + data.get("LockLoEntityIds", []) + data.get("NeedRemoveLoEntityIds", [])
        targets += entity.get("LoStageResultLoEntitySpawnComponent", {}).get("LoEntityIds", [])
        if "LoStageResultTransportComponent" in entity:
            transport = entity["LoStageResultTransportComponent"]
            if transport["MapClassId"] != 0: raise ValueError("Cross-map puzzle transport requires its original placement closure")
            targets.append(transport["TargetLoEntityId"])
        targets += [r["LoEntId"] for group in entity.get("LoStageResultStateUpdateComponent", {}).get("DataList", []) or []
                    for r in group.get("StageDatas", [])]
        for target in targets:
            if str(target) not in local:
                raise ValueError("Missing original puzzle dependency: " + str(target))
            if target not in ids:
                ids.add(target); pending.append(target)
    rules = []
    for identity in sorted(ids):
        entity, placement = local[str(identity)], placed[identity]
        template = templates[str(placement["entity"])]
        lock = entity.get("LoEntityLockComponent", {})
        lock_data = inline(lock.get("Params", ""))
        terminal = terminals.get(identity, {})
        data = inline(terminal.get("Params", ""))
        support = entity["LoEntityComponent"]["SpanwInfo"]["PrivateMapSupportData"]
        active = template.get("WorldStageActiveComponent", {}).get("DefaultActive", True)
        rules.append(dict(
            PlacementId=identity, EntityClassId=placement["entity"], InitialState=entity.get("LoWorldStageComponent", {}).get("State", 0),
            GridX=placement["grid"][0], GridY=placement["grid"][1],
            InitiallySpawned=active and support["SupportSpawnInMap"] and not support["BlockDefaultSpawn"],
            LockType=lock.get("LockType", 0), LockConditions=[dict(PlacementId=v, State=0) for v in lock_data.get("LoEntityIds", [])],
            ConditionEntityInactiveInUnlock=lock_data.get("ConditionEntityInactiveInUnlock", False),
            ConditionEntityNeverDestroy=lock_data.get("ConditionEntityNeverDestroy", False),
            TerminalLockIds=data.get("LockLoEntityIds", []), TerminalRemoveIds=data.get("NeedRemoveLoEntityIds", []),
            TerminalBoxIds=[], TerminalPuzzleType=terminal.get("MapWorldPuzzleType", 0),
            AuxiliaryUnlockOnly=terminal.get("MapWorldPuzzleType") == 6,
            HasTransport="LoStageResultTransportComponent" in entity,
            Transport=entity.get("LoStageResultTransportComponent"),
            ResetTransferActionId=lock_data.get("ResetTransferActionId", 22055 if lock.get("LockType") == 7 else 0),
            HasOverlapDetector="CharacterPositionOverlapDetectorComponent" in template,
            OverlapDetector=template.get("CharacterPositionOverlapDetectorComponent"),
            Stage=template.get("WorldStageComponent"),
            SelfStates=template.get("StageResultSelfStateUpdateComponent", {}).get("InfoList", []),
            SpawnIds=entity.get("LoStageResultLoEntitySpawnComponent", {}).get("LoEntityIds", []),
            OtherStates=entity.get("LoStageResultStateUpdateComponent", {}).get("DataList") or [],
            Notify=entity.get("LoStageResultNotifyCompleteComponent", {}).get("Configs", []),
            OfflineIgnoredMissingStateTargets=[], Body=template.get("GridTransformComponent", {}).get("BodyRangeInfos", []),
            RealCenter=template.get("GridTransformComponent", {}).get("RealCenter", [0, 0]),
            InitialDirection=entity["LoGridTransformComponent"]["Dir"], InitialFlipX=entity["LoGridTransformComponent"]["FlipX"]))
    extension, = [r for r in tables["Map/entity_extend_parameter"] if r["EntityClassId"] == 6502]
    if extension["CostItems"] != [{"CostItems": [{"ItemId": 325, "Amount": 1}]}]:
        raise ValueError("Original terminal cost changed")
    data = encode(dict(Manifest="871_156598", MapClassId=14, Rules=rules,
        Costs=[dict(TerminalId=i, Items=extension["CostItems"][0]["CostItems"]) for i in sorted(terminals)]))
    write(OUTPUT, data); meta(OUTPUT)
    for folder in (OUTPUT.parent.parent.parent, OUTPUT.parent.parent, OUTPUT.parent): meta(folder, folder=True)
    code_manifest = ROOT / receipt["code_manifest"]["path"]
    if sha(code_manifest.read_bytes()) != receipt["code_manifest"]["sha256"]:
        raise ValueError("Current code manifest changed")
    code = code_manifest.parent / "official-code"
    sources = [code / "common-source/Common" / (name + ".cs") for name in (
        "MapPuzzleTerminalDataCannon", "MapPuzzleTerminalDataStepGrid", "MapPuzzleTerminalDataLockBase",
        "EntityLockStepGrid", "EntityLockEntityActivedBase", "CharacterPositionOverlapDetectorComponent",
        "StageResultTransportComponent", "LoStageResultTransportComponentInfo", "StageResultAutoPuzzleComponent", "StageResultResetPuzzleComponent")]
    sources += [code / "client-source" / (name + ".cs") for name in ("MyStageResultAutoPuzzle", "MyStageResultResetPuzzle")]
    adapters = [ROOT / "unity/Assets/Game/Runtime/Offline/World" / name for name in (
        "OfflineMapPuzzleSession.cs", "OfflineMapPuzzleMovement.cs", "Map14PuzzleAssets.cs")]
    write(PROOF, encode(dict(format=1, mapClassId=14, manifest="871_156598", inputs=inputs,
        code_manifest=receipt["code_manifest"], original_code=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sources],
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(data))],
        adapters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in adapters],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Structural projection of all twelve original terminal dependency closures, preserving placement, stage order, duplicate cannon targets and original cost configuration.",
        offline_rules="User confirmed: cannon sets/clears only configured obstacle destroyed flags; step-grid auxiliary unlocks only listed locks; reset restores only that group's lock/pads and preserves independent rewards/claim state. Original completion results generate rewards and update terminal state in source order.",
        limitations=["The source APK contains marker DTOs, not the server executor.", "No backpack payment transaction, official menu, disk save, navigation route or player input is wired by this data module."])))
    print("Map14 puzzle rules:", len(rules), "entities, seven cannon terminals, five step-grid groups")


if __name__ == "__main__": build()
