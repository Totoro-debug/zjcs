"""Verify acquired 10044 casts against the original area and water damage."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        data = json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())
        for key in ("10750", "10751", "10752"):
            entities.setdefault(key, {}).update(data["entities"].get(key, {}))
    skill = entities["10750"]["FightSkillComponent"]
    hit = entities["10751"]["FightHitFixedComponent"]
    links, scope = skill["HitList"], hit["Scope"]
    positions = {(entry["Pos"]["X"], entry["Pos"]["Y"]) for entry in scope}
    if (skill["TargetType"] != 1 or skill["ElementType"] != 101 or len(links) != 1
            or links[0]["ClassId"] != 10751 or links[0]["ActOnSouce"] != 1
            or hit["DamageId"] != 10752 or hit["DamageType"] != 1 or hit["IsTrueDamage"]
            or len(scope) != 8 or positions != {(x, y) for x in (-1, 0, 1) for y in (-1, 0, 1) if x or y}
            or any(entry["Delay"] != 0 for entry in scope)):
        raise ValueError("Review the original frost flower configuration")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    unlimited = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10044 or cast["Slot"] != 0:
                continue
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 10750 or len(selections) != 1
                    or selections[0]["HitEntityId"] != 10751
                    or abs(selections[0]["Delay"] - links[0]["Delay"]) > .0001):
                raise ValueError("Frost flower changed its original area hit")
            ids = cast["DamageEventIds"]
            events = [value for value in step["DamageEvents"] if value["DamageId"] in ids]
            if len(events) != len(ids):
                raise ValueError("Frost flower lost linked damage events")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 10750 or event["HitEntityId"] != 10751
                        or event["DamageEntityId"] != 10752 or event["DamageType"] != 1 or event["ElementType"] != 101
                        or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                            and not (unlimited and event["AfterHp"] == event["BeforeHp"]))
                        or abs(event["Delay"] - cast["Delay"] - links[0]["Delay"]) > .0001):
                    raise ValueError("Frost flower changed its water damage or hit timing")
                count += 1
    if not count:
        raise ValueError("No equipped 10044 damage was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10044 area and water damage passed: {count} hits")
