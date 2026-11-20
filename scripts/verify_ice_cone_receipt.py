"""Verify the acquired ice cone against its original hit and freeze configuration."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    combat = ROOT / "unity/Assets/Official/Combat"
    skills = json.loads((combat / "skill-rules.json").read_bytes())["entities"]
    battle = json.loads((combat / "battle-rules.json").read_bytes())["entities"]
    skill = skills["10900"]["FightSkillComponent"]
    hit = battle["10901"]["FightHitFixedComponent"]
    damage = battle["10902"]["FightDamageComponent"]
    freeze = battle["24215"]["FightStatusComponent"]
    scope = hit["Scope"]
    if (skill["TargetType"] != 1 or skill["ElementType"] != 101
            or len(skill["HitList"]) != 1 or skill["HitList"][0]["ClassId"] != 10901
            or abs(skill["HitList"][0]["Delay"] - .6) > .0001
            or hit["DamageId"] != 10902 or hit["DamageType"] != 1 or hit["IsTrueDamage"]
            or len(scope) != 5 or any(entry["Pos"] != {"X": 0, "Y": index + 1}
                or abs(entry["Delay"] - delay) > .0001
                for index, (entry, delay) in enumerate(zip(scope, (0, .1, .15, .25, .35))))
            or len(damage["StatusList"]) != 1 or damage["StatusList"][0]["StatusId"] != 24215
            or abs(damage["StatusList"][0]["BasePercent"] - .4) > .0001
            or not damage["StatusList"][0]["AffectedByProp"] or freeze["DurationRound"] != 1):
        raise ValueError("Review the original ice cone configuration")

    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    casts = hits = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10035 or cast["Slot"] != 0:
                continue
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 10900 or len(selections) != 1
                    or selections[0]["HitEntityId"] != 10901):
                raise ValueError("Ice cone lost its original area hit")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or not events:
                raise ValueError("Ice cone lost a linked damage event")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["HitEntityId"] != 10901 or event["DamageEntityId"] != 10902
                        or event["DamageType"] != 1 or event["ElementType"] != 101
                        or event["TrueDamage"] or event["Damage"] >= 0):
                    raise ValueError("Ice cone changed its original water damage")
                hits += 1
            casts += 1
    if not casts:
        raise ValueError("No equipped 10035 cast was recorded")
    return casts, hits


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    try:
        count, hits = verify(parser.parse_args().receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10035 ice cone passed: {count} casts and {hits} hits")
