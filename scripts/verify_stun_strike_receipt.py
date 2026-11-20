"""Verify acquired 10010 casts against the original hit and stun configuration."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        data = json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())
        for key in ("10620", "10621", "10622"):
            entities.setdefault(key, {}).update(data["entities"].get(key, {}))
    skill = entities["10620"]["FightSkillComponent"]
    hit = entities["10621"]["FightHitFixedComponent"]
    status = entities["10622"]["FightDamageComponent"]["StatusList"]
    links, scope = skill["HitList"], hit["Scope"]
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or {(p["X"], p["Y"]) for p in skill["Range"]} != {(0, 1), (0, -1), (1, 0), (-1, 0)}
            or len(links) != 1 or links[0]["ClassId"] != 10621
            or hit["DamageId"] != 10622 or hit["DamageType"] != 1 or hit["DamageActionType"] != 4
            or hit["IsTrueDamage"] or len(scope) != 1 or scope[0]["Pos"] != {"X": 0, "Y": 0}
            or scope[0]["Delay"] != 0 or len(status) != 1 or status[0]["StatusId"] != 24010
            or status[0]["TargetType"] != 2 or not status[0]["AffectedByProp"]
            or abs(status[0]["BasePercent"] - .4) > .0001):
        raise ValueError("Review the original stun-strike configuration")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    unlimited = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10010 or cast["Slot"] != 0:
                continue
            if cast["SkillEntityId"] != 10620:
                raise ValueError("The acquired skill cast a different entity")
            selections = cast["HitSelections"]
            if len(selections) != 1 or selections[0]["HitEntityId"] != 10621 or len(selections[0]["Targets"]) != 1:
                raise ValueError("Stun strike changed its single-target hit selection")
            ids = cast["DamageEventIds"]
            events = [value for value in step["DamageEvents"] if value["DamageId"] in ids]
            if len(ids) != 1 or len(events) != 1:
                raise ValueError("Stun strike did not produce exactly one linked hit")
            event = events[0]
            if (event["SourceId"] != cast["ActorId"] or event["TargetId"] != selections[0]["Targets"][0]["TargetId"]
                    or event["SkillEntityId"] != 10620 or event["HitEntityId"] != 10621
                    or event["DamageEntityId"] != 10622 or event["DamageType"] != 1 or event["ElementType"] != 0
                    or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                    or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                        and not (unlimited and event["AfterHp"] == event["BeforeHp"]))
                    or abs(event["Delay"] - cast["Delay"] - links[0]["Delay"]) > .0001):
                raise ValueError("Stun strike changed its physical damage or hit timing")
            count += 1
    if not count:
        raise ValueError("No equipped 10010 cast was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10010 physical hit and stun configuration passed: {count} casts")
