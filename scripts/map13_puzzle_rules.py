"""Preserve source EC puzzle fields using the established component conversion.

Derived from prepare_unity_map12_runtime_data.py; Map13 inputs are separately pinned.
"""
import json
import re


def parse_terminal(params):
    # This is the original non-JSON inline object grammar used by the EC data.
    value = re.sub(r"([A-Za-z][A-Za-z0-9_]*):", r'"\1":', params)
    parsed = json.loads(value)
    if not isinstance(parsed, dict):
        raise ValueError("Puzzle terminal must contain an object")
    return parsed


def puzzle_rules(definition, catalog, templates, mover_rows):
    components = {r["EntityId"]: json.loads(r["Json"]) for r in definition["EntityComponents"]}
    movers = {r["Id"]: r["GroundTypes"] for r in mover_rows}
    result = []
    # Preserve every original entity's component ordering and original result
    # indices. The runtime selects the puzzle entities; conversion does not
    # remove dependent rewards, obstacles or inactive spawns.
    for entity in catalog["entities"]:
        local = components[entity["placementId"]]
        template = templates[str(entity["entityClassId"])]
        terminal = local.get("LoEntityPuzzleTerminalComponent")
        terminal_data = parse_terminal(terminal["Params"]) if terminal else {}
        params = json.loads(entity["lockParamsJson"])
        conditions = [{"PlacementId": int(k), "State": int(v)}
                      for k, v in params.get("EntityStateDict", {}).items()]
        if not conditions and entity["lockType"] in (5, 7):
            conditions = [{"PlacementId": int(v), "State": 0} for v in entity["lockConditionEntityIds"]]
        transform = template.get("GridTransformComponent", {})
        grid = local.get("LoGridTransformComponent", {})
        mover = template.get("GridMoverComponent", {})
        ground_types = movers.get(mover.get("WalkableGroundTypeKey"), [])
        pushed = local.get("LoStageResultEntityPushedComponent", {})
        range_override = template.get("StageResultOverrideInteractiveRangeComponent", {})
        # Common.LoEntityComponentSpawnInfo.BlockDefaultSpawnInMap(Private)
        # is independent of WorldActiveable.Active. Deferred Lo-spawn results
        # must not exist until their original result 234 executes.
        support = local["LoEntityComponent"]["SpanwInfo"]["PrivateMapSupportData"]
        row = {
            "PlacementId": entity["placementId"], "EntityClassId": entity["entityClassId"],
            "InitialState": entity["state"], "GridX": entity["grid"][0], "GridY": entity["grid"][1],
            "InitiallySpawned": entity["active"] and support["SupportSpawnInMap"]
                and not support["BlockDefaultSpawn"], "LockType": entity["lockType"],
            "LockConditions": conditions,
            "PushBoxDetectorIds": params.get("LoOverlapDetectorEntityIds", []),
            "PushBoxIds": params.get("LoBoxEntityIds", []),
            "HasPushResult": "StageResultEntityPushedComponent" in template,
            "MoveMask": sum(1 << ground_type for ground_type in ground_types),
            "MovableRect": pushed.get("MovableRect"),
            "InitialDirection": grid.get("Dir", 0), "InitialFlipX": grid.get("FlipX", False),
            "Body": transform.get("BodyRangeInfos", []),
            "RealCenter": transform.get("RealCenter", [0, 0]),
            "ConditionEntityInactiveInUnlock": entity["conditionEntityInactiveInUnlock"],
            "ConditionEntityNeverDestroy": entity["conditionEntityNeverDestroy"],
            "TerminalLockIds": terminal_data.get("LockLoEntityIds", []),
            "TerminalPuzzleType": terminal["MapWorldPuzzleType"] if terminal else 0,
            "TerminalRemoveIds": terminal_data.get("NeedRemoveLoEntityIds", []),
            "TerminalBoxIds": terminal_data.get("BoxLoEntityIds", []),
            "ResetTransferActionId": terminal_data.get("ResetTransferActionId", 0),
            "TerminalJson": json.dumps(terminal, ensure_ascii=False, separators=(",", ":")) if terminal else "",
            "HasOverlapDetector": "CharacterPositionOverlapDetectorComponent" in template,
            "OverlapDetector": template.get("CharacterPositionOverlapDetectorComponent"),
            "Stage": template.get("WorldStageComponent"),
            "SelfStates": template.get("StageResultSelfStateUpdateComponent", {}).get("InfoList", []),
            "SpawnIds": local.get("LoStageResultLoEntitySpawnComponent", {}).get("LoEntityIds", []),
            "OtherStates": local.get("LoStageResultStateUpdateComponent", {}).get("DataList") or [],
            "OfflineIgnoredMissingStateTargets": [],
            "Notify": local.get("LoStageResultNotifyCompleteComponent", {}).get("Configs", []),
        }
        if range_override:
            row["InteractiveRangeInfos"] = range_override.get("InteractionRangeInfos", [])
            row["InteractiveRangeOperations"] = range_override.get("StateOperationInfos", [])
        result.append(row)
    ids = {r["PlacementId"] for r in result}
    for rule in result:
        referenced = (rule["SpawnIds"] + rule["TerminalLockIds"] + rule["TerminalRemoveIds"]
                      + rule["TerminalBoxIds"] + rule["PushBoxDetectorIds"] + rule["PushBoxIds"])
        referenced += [r["PlacementId"] for r in rule["LockConditions"]]
        referenced += [r["LoEntId"] for group in rule["OtherStates"] for r in group.get("StageDatas", [])]
        if set(referenced) - ids:
            raise ValueError(f"Unresolved Map13 puzzle targets for {rule['PlacementId']}: {set(referenced) - ids}")
    if sum(bool(r["TerminalJson"]) for r in result) != 11:
        raise ValueError("Expected all eleven original marsh puzzle terminals")
    return result
