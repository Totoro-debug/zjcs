"""Verify acquired water-bullet casts against the original projectile configuration."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    combat = ROOT / "unity/Assets/Official/Combat"
    skill_rules = json.loads((combat / "skill-rules.json").read_bytes())
    battle_rules = json.loads((combat / "battle-rules.json").read_bytes())
    skill = skill_rules["entities"]["10500"]["FightSkillComponent"]
    hit = battle_rules["entities"]["10501"]["FightHitFixedComponent"]
    scope = hit["Scope"]
    if (skill["TargetType"] != 1 or skill["ElementType"] != 101 or len(skill["HitList"]) != 1
            or skill["HitList"][0]["ClassId"] != 10501 or hit["DamageId"] != 10502
            or hit["DamageType"] != 1 or hit["IsTrueDamage"] or abs(hit["DelayDistFactor"] - .1) > .0001
            or len(scope) != 1 or scope[0]["Pos"] != {"X": 0, "Y": 0} or scope[0]["Delay"] != 0):
        raise ValueError("Review the original water-bullet configuration")

    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    casts = hits = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10000 or cast["Slot"] != 0:
                continue
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 10500 or len(selections) != 1
                    or selections[0]["HitEntityId"] != 10501
                    or abs(selections[0]["DelayDistFactor"] - hit["DelayDistFactor"]) > .0001):
                raise ValueError("Water bullet lost its original projectile hit")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or not events:
                raise ValueError("Water bullet lost a linked damage event")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["HitEntityId"] != 10501 or event["DamageEntityId"] != 10502
                        or event["DamageType"] != 1 or event["ElementType"] != 101
                        or event["TrueDamage"] or event["Damage"] >= 0):
                    raise ValueError("Water bullet changed its original water damage")
                hits += 1
            casts += 1
    if not casts:
        raise ValueError("No equipped 10000 cast was recorded")
    return casts, hits


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    try:
        count, hits = verify(parser.parse_args().receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10000 water bullet passed: {count} casts and {hits} hits")
