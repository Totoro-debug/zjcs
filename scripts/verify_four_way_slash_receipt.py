"""Check 10013 casts against the original four-cell physical area hit."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {"up": 2, "down": 0, "left": 1, "right": 3}


def verify(path, direction, initial=None):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    skill = entities["10650"]["FightSkillComponent"]
    hit = entities["10651"]["FightHitFixedComponent"]
    links, scope = skill["HitList"], hit["Scope"]
    offsets = {(x["Pos"]["X"], x["Pos"]["Y"]) for x in scope}
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or len(links) != 1 or links[0]["ClassId"] != 10651 or hit["DamageId"] != 10022
            or hit["DamageType"] != 1 or hit["IsTrueDamage"] or len(scope) != 4
            or offsets != {(0, 1), (0, -1), (1, 0), (-1, 0)} or any(x["Delay"] != 0 for x in scope)):
        raise ValueError("Review the original four-way slash area configuration")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    assembled = json.loads(initial.read_text(encoding="utf-8-sig"))["checkpoint"] if initial else checkpoint
    equipped = [value for unit in assembled["Units"] if unit["Id"] == 1
                for value in unit["Skills"] if value["ItemId"] == 10013 and value["Slot"] == 0]
    if len(equipped) != 1:
        raise ValueError("The equipped original four-way slash snapshot is absent")
    actual_hit = next((value for value in equipped[0]["HitGraphEntities"] if value["EntityId"] == 10651), None)
    if (actual_hit is None or actual_hit["DamageId"] != hit["DamageId"]
            or actual_hit["DamageProperty"] != hit["DamageProp"] or len(actual_hit["ScopeEntries"]) != len(scope)):
        raise ValueError("The assembled four-way slash lost its original damage or scope")
    unlimited_hp = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10013 or cast["Slot"] != 0:
                continue
            pose = cast["Presentation"]
            if (cast["SkillEntityId"] != 10650 or pose["FacingIndex"] not in (0, 1, 2, 3)
                    or direction and pose["FacingIndex"] != DIRECTIONS[direction]
                    or abs(pose["Duration"] - skill["Duration"]) > .0001
                    or abs(pose["ActionDuration"] - skill["ActionDuration"]) > .0001):
                raise ValueError("Four-way slash changed its entity, facing or action duration")
            cells = {(pose["SourceX"] + x, pose["SourceY"] + y) for x, y in offsets}
            selections = cast["HitSelections"]
            if (len(selections) != 1 or selections[0]["HitEntityId"] != 10651
                    or abs(selections[0]["Delay"] - links[0]["Delay"]) > .0001):
                raise ValueError("Four-way slash changed its original hit selection or delay")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError("Four-way slash lost or duplicated a linked damage event")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 10650 or event["HitEntityId"] != 10651
                        or event["DamageEntityId"] != 10022 or event["DamageType"] != hit["DamageType"]
                        or event["ElementType"] != 0 or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                            and not (unlimited_hp and event["AfterHp"] == event["BeforeHp"]))
                        or not event["HasHitGridPosition"] or (event["HitGridX"], event["HitGridY"]) not in cells
                        or abs(event["Delay"] - cast["Delay"] - links[0]["Delay"]) > .0001):
                    raise ValueError("Four-way slash changed its original physical hit, scope or timing")
            count += 1
    if not count:
        raise ValueError("No equipped 10013 cast was recorded")
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
    print(f"Original 10013 four-cell physical hit and timing passed: {count} casts")
