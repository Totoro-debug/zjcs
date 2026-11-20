"""Verify the original battle-start challenge and its physical damage receipts."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    combat = ROOT / "unity/Assets/Official/Combat"
    skill_rules = json.loads((combat / "skill-rules.json").read_bytes())
    battle_rules = json.loads((combat / "battle-rules.json").read_bytes())
    items = json.loads((combat / "rules.json").read_bytes())
    item = next(value for value in items["skill"] if value["ClassId"] == 10049)
    skill = skill_rules["entities"]["11100"]["FightSkillComponent"]
    hit = battle_rules["entities"]["11101"]["FightHitFixedComponent"]
    damage = battle_rules["entities"]["11102"]["FightDamageComponent"]
    applications = damage["StatusList"]
    positions = {(entry["Pos"]["X"], entry["Pos"]["Y"]) for entry in hit["Scope"]}
    expected = {(x, y) for x in range(-3, 4) for y in range(-3, 4) if 0 < abs(x) + abs(y) <= 3}
    if (item["EcEntityId"] != 11100 or item["TryAtStartType"] != 2
            or skill["TargetType"] != 1 or len(skill["HitList"]) != 1
            or skill["HitList"][0]["ClassId"] != 11101 or hit["DamageId"] != 11102
            or hit["DamageType"] != 1 or hit["IsTrueDamage"] or positions != expected
            or len(hit["Scope"]) != len(expected) or len(applications) != 1
            or applications[0]["StatusId"] != 24216 or abs(applications[0]["BasePercent"] - .85) > .0001
            or applications[0]["AffectedByProp"]
            or {entry["CharacterType"] for entry in applications[0]["OverrideRateByCharacterTypes"]} != {2, 3, 7}
            or any(entry["OverrideRate"] != 1 or entry["OverrideAffectedByProp"]
                   for entry in applications[0]["OverrideRateByCharacterTypes"])):
        raise ValueError("Review the original challenge configuration")

    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    casts = hits = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10049 or cast["Slot"] != 0:
                continue
            if casts == 0 and step["ActorRound"] != 0:
                raise ValueError("The first challenge did not occur before the actor's first turn")
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 11100 or len(selections) != 1
                    or selections[0]["HitEntityId"] != 11101):
                raise ValueError("Challenge lost its original area hit")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or not events:
                raise ValueError("Challenge lost a linked damage event")
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["HitEntityId"] != 11101 or event["DamageEntityId"] != 11102
                        or event["DamageType"] != 1 or event["TrueDamage"] or event["Damage"] >= 0):
                    raise ValueError("Challenge changed its original physical damage")
                hits += 1
            casts += 1
    if not casts:
        raise ValueError("No equipped 10049 cast was recorded")
    return casts, hits


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    try:
        count, hits = verify(parser.parse_args().receipt)
    except (ValueError, StopIteration) as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10049 opening challenge passed: {count} casts and {hits} hits")
