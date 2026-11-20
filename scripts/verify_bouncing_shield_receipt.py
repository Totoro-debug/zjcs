"""Verify acquired 10048 casts against the original chained physical hits."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        data = json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())
        for key in map(str, (11090, 11091, 11092, 11093, 11094, 11099)):
            entities.setdefault(key, {}).update(data["entities"].get(key, {}))
    skill = entities["11090"]["FightSkillComponent"]
    status = entities["11099"]["FightDamageComponent"]["StatusList"]
    links = skill["HitList"]
    if (skill["SkillTargetType"] != 1 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or len(links) != 1 or links[0]["ClassId"] != 11091 or len(status) != 1
            or status[0]["StatusId"] != 24010 or status[0]["TargetType"] != 2
            or not status[0]["AffectedByProp"] or abs(status[0]["BasePercent"] - .3) > .0001):
        raise ValueError("Review the original bouncing shield configuration")
    for entity in range(11091, 11095):
        hit = entities[str(entity)]["FightHitChainedComponent"]
        following = hit["NextHitList"]
        if (hit["DamageId"] != 11099 or hit["DamageType"] != 1 or hit["IsTrueDamage"]
                or len(following) != (0 if entity == 11094 else 1)
                or following and following[0]["ClassId"] != entity + 1):
            raise ValueError("The original shield bounce chain changed")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    unlimited = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10048 or cast["Slot"] != 0:
                continue
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 11090 or not selections
                    or any(value["HitEntityId"] not in range(11091, 11095) for value in selections)):
                raise ValueError("Bouncing shield lost its original hit selections")
            ids = cast["DamageEventIds"]
            events = [value for value in step["DamageEvents"] if value["DamageId"] in ids]
            if len(events) != len(ids):
                raise ValueError("Bouncing shield lost linked damage events")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 11090 or event["HitEntityId"] not in range(11091, 11095)
                        or event["DamageEntityId"] != 11099 or event["DamageType"] != 1 or event["ElementType"] != 0
                        or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                            and not (unlimited and event["AfterHp"] == event["BeforeHp"]))):
                    raise ValueError("Bouncing shield changed its original physical damage")
                count += 1
    if not count:
        raise ValueError("No equipped 10048 damage was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10048 chained physical damage passed: {count} hits")
