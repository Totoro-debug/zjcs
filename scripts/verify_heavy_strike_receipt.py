"""Check 10009 casts against the original adjacent hit and airborne configuration."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {"up": (2, 0, 1), "down": (0, 0, -1),
              "left": (1, -1, 0), "right": (3, 1, 0)}


def verify(path, direction):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    skill = entities["10610"]["FightSkillComponent"]
    hit = entities["10611"]["FightHitFixedComponent"]
    links, scope = skill["HitList"], hit["Scope"]
    valid_range = {(p["X"], p["Y"]) for p in skill["Range"]}
    if (skill["SkillTargetType"] != 1 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or valid_range != {(0, 1), (0, -1), (1, 0), (-1, 0)}
            or len(links) != 1 or links[0]["ClassId"] != 10611 or hit["DamageId"] != 10612
            or hit["DamageType"] != 1 or hit["IsTrueDamage"] or len(scope) != 1
            or scope[0]["Pos"] != {"X": 0, "Y": 1} or scope[0]["Delay"] != 0
            or hit["AirCombo"] != {"AirComboStatusId": 24269, "BasePercent": 1.0, "AffectedByProp": False}):
        raise ValueError("Review the original heavy-strike hit and airborne configuration")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    unlimited_hp = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10009 or cast["Slot"] != 0:
                continue
            pose = cast["Presentation"]
            dx, dy = pose["TargetX"] - pose["SourceX"], pose["TargetY"] - pose["SourceY"]
            facing = (pose["FacingIndex"], dx, dy)
            if (cast["SkillEntityId"] != 10610 or facing not in DIRECTIONS.values()
                    or direction and facing != DIRECTIONS[direction]
                    or abs(pose["Duration"] - skill["Duration"]) > .0001
                    or abs(pose["ActionDuration"] - skill["ActionDuration"]) > .0001):
                raise ValueError("Heavy strike changed its target, facing or action duration")
            selections = cast["HitSelections"]
            if (len(selections) != 1 or selections[0]["HitEntityId"] != 10611
                    or len(selections[0]["Targets"]) != 1
                    or selections[0]["Targets"][0]["TargetId"] != pose["TargetId"]
                    or abs(selections[0]["Delay"] - links[0]["Delay"]) > .0001):
                raise ValueError("Heavy strike changed its original hit selection or delay")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(ids) != 1 or len(events) != 1:
                raise ValueError("Heavy strike did not produce exactly one linked hit")
            event = events[0]
            if (event["SourceId"] != cast["ActorId"] or event["TargetId"] != pose["TargetId"]
                    or event["SkillEntityId"] != 10610 or event["HitEntityId"] != 10611
                    or event["DamageEntityId"] != hit["DamageId"] or event["DamageType"] != hit["DamageType"]
                    or event["ElementType"] != skill["ElementType"] or event["TrueDamage"] or event["IsCure"]
                    or event["Damage"] >= 0
                    or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                        and not (unlimited_hp and event["AfterHp"] == event["BeforeHp"]))
                    or not event["HasDamagePosition"] or (event["DamageX"], event["DamageY"]) != (pose["TargetX"], pose["TargetY"])
                    or abs(event["Delay"] - cast["Delay"] - links[0]["Delay"]) > .0001):
                raise ValueError("Heavy strike changed its physical damage, position or timing")
            count += 1
    if not count:
        raise ValueError("No equipped 10009 cast was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--direction", choices=DIRECTIONS)
    args = parser.parse_args()
    try:
        count = verify(args.receipt, args.direction)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10009 physical hit and timing passed: {count} casts")
