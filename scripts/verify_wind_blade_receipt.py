"""Check 10012 wind-strip hits and any original knockback in a battle receipt."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {"up": (2, 0, 1), "down": (0, 0, -1),
              "left": (1, -1, 0), "right": (3, 1, 0)}


def verify(path, direction, initial=None):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    skill = entities["10640"]["FightSkillComponent"]
    hit = entities["10641"]["FightHitFixedComponent"]
    damage = entities["10642"]["FightDamageComponent"]
    links, scope, move = skill["HitList"], hit["Scope"], damage["MoveCfg"]
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 100
            or len(links) != 1 or links[0]["ClassId"] != 10641 or hit["DamageId"] != 10642
            or hit["DamageType"] != 1 or hit["IsTrueDamage"]
            or [(x["Pos"]["X"], x["Pos"]["Y"]) for x in scope] != [(0, 1), (0, 2), (0, 3), (0, 4)]
            or move["MoveBehaviourId"] != 10643 or move["Distance"] != 1 or move["SkillMoveDirection"] != 0
            or move["BasePercent"] != .5 or not move["AffectedByProp"] or move["TowardsTargetPos"]):
        raise ValueError("Review the original wind-blade hit and knockback configuration")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    assembled = json.loads(initial.read_text(encoding="utf-8-sig"))["checkpoint"] if initial else checkpoint
    equipped = [value for unit in assembled["Units"] if unit["Id"] == 1
                for value in unit["Skills"] if value["ItemId"] == 10012 and value["Slot"] == 0]
    if len(equipped) != 1:
        raise ValueError("The equipped original wind-blade snapshot is absent")
    actual_hit = next((value for value in equipped[0]["HitGraphEntities"] if value["EntityId"] == 10641), None)
    if (actual_hit is None or actual_hit["DamageId"] != hit["DamageId"]
            or actual_hit["DamageProperty"] != hit["DamageProp"] or len(actual_hit["ScopeEntries"]) != len(scope)):
        raise ValueError("The assembled wind blade lost its original damage or scope")
    unlimited_hp = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10012 or cast["Slot"] != 0:
                continue
            pose = cast["Presentation"]
            facing = pose["FacingIndex"]
            if (cast["SkillEntityId"] != 10640 or facing not in (0, 1, 2, 3)
                    or direction and facing != DIRECTIONS[direction][0]
                    or abs(pose["Duration"] - skill["Duration"]) > .0001
                    or abs(pose["ActionDuration"] - skill["ActionDuration"]) > .0001):
                raise ValueError("Wind blade changed its entity, facing or action duration")
            dx, dy = {index: (x, y) for index, x, y in DIRECTIONS.values()}[facing]
            cells = {(pose["SourceX"] + dx * entry["Pos"]["Y"], pose["SourceY"] + dy * entry["Pos"]["Y"]): entry["Delay"]
                     for entry in scope}
            selections = cast["HitSelections"]
            if (len(selections) != 1 or selections[0]["HitEntityId"] != 10641
                    or abs(selections[0]["Delay"] - links[0]["Delay"]) > .0001):
                raise ValueError("Wind blade changed its original hit selection or delay")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError("Wind blade lost or duplicated a linked damage event")
            for event in events:
                cell = (event["HitGridX"], event["HitGridY"])
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 10640 or event["HitEntityId"] != 10641
                        or event["DamageEntityId"] != 10642 or event["DamageType"] != hit["DamageType"]
                        or event["ElementType"] != 100 or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                            and not (unlimited_hp and event["AfterHp"] == event["BeforeHp"]))
                        or not event["HasHitGridPosition"] or cell not in cells
                        or abs(event["Delay"] - cast["Delay"] - links[0]["Delay"] - cells[cell]) > .0001):
                    raise ValueError("Wind blade changed its original wind hit, scope cell or timing")
            for movement in step["Movements"]:
                if movement["MoveBehaviourId"] != 10643 or movement["UnitId"] not in {event["TargetId"] for event in events}:
                    continue
                path_points = movement["Path"]
                if len(path_points) != 2:
                    raise ValueError("Wind blade changed its original one-cell knockback")
                x_offset = path_points[0]["X"] - pose["SourceX"]
                y_offset = path_points[0]["Y"] - pose["SourceY"]
                step_x = (x_offset > 0) - (x_offset < 0)
                step_y = (y_offset > 0) - (y_offset < 0)
                if (path_points[1]["X"] - path_points[0]["X"] != step_x
                        or path_points[1]["Y"] - path_points[0]["Y"] != step_y):
                    raise ValueError("Wind blade changed its original one-cell knockback")
            count += 1
    if not count:
        raise ValueError("No equipped 10012 cast was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--direction", choices=DIRECTIONS)
    parser.add_argument("--initial", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt, args.direction, args.initial)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10012 wind strip and timing passed: {count} casts")
