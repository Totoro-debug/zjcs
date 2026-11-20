"""Verify acquired fireball casts against the original projectile and area configuration."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    combat = ROOT / "unity/Assets/Official/Combat"
    skills = json.loads((combat / "skill-rules.json").read_bytes())["entities"]
    battle = json.loads((combat / "battle-rules.json").read_bytes())["entities"]
    skill = skills["10800"]["FightSkillComponent"]
    hit = battle["10801"]["FightHitFixedComponent"]
    positions = {(entry["Pos"]["X"], entry["Pos"]["Y"]) for entry in hit["Scope"]}
    if (skill["TargetType"] != 1 or skill["ElementType"] != 102 or len(skill["HitList"]) != 1
            or skill["HitList"][0]["ClassId"] != 10801 or hit["DamageId"] != 10023
            or hit["DamageType"] != 1 or hit["IsTrueDamage"] or abs(hit["DelayDistFactor"] - .125) > .0001
            or len(hit["Scope"]) != 9 or positions != {(x, y) for x in (-1, 0, 1) for y in (-1, 0, 1)}
            or any(entry["Delay"] != 0 for entry in hit["Scope"])):
        raise ValueError("Review the original fireball configuration")

    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    casts = hits = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10028 or cast["Slot"] != 0:
                continue
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 10800 or len(selections) != 1
                    or selections[0]["HitEntityId"] != 10801
                    or abs(selections[0]["DelayDistFactor"] - hit["DelayDistFactor"]) > .0001):
                raise ValueError("Fireball lost its original projectile hit")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or not events:
                raise ValueError("Fireball lost a linked damage event")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["HitEntityId"] != 10801 or event["DamageEntityId"] != 10023
                        or event["DamageType"] != 1 or event["ElementType"] != 102
                        or event["TrueDamage"] or event["Damage"] >= 0):
                    raise ValueError("Fireball changed its original fire damage")
                hits += 1
            casts += 1
    if not casts:
        raise ValueError("No equipped 10028 cast was recorded")
    return casts, hits


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    try:
        count, hits = verify(parser.parse_args().receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10028 fireball passed: {count} casts and {hits} hits")
