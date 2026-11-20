"""Verify the original battle-start encouragement and its dispel receipt."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    combat = ROOT / "unity/Assets/Official/Combat"
    skill_rules = json.loads((combat / "skill-rules.json").read_bytes())
    battle_rules = json.loads((combat / "battle-rules.json").read_bytes())
    items = json.loads((combat / "rules.json").read_bytes())
    item = next(value for value in items["skill"] if value["ClassId"] == 10066)
    skill = skill_rules["entities"]["11550"]["FightSkillComponent"]
    hit = battle_rules["entities"]["11551"]["FightHitFixedComponent"]
    damage = battle_rules["entities"]["11552"]["FightDamageComponent"]
    status = skill_rules["entities"]["24237"]["FightStatusComponent"]
    applications, dispel = damage["StatusList"], damage["DisperseStatus"]
    if (item["EcEntityId"] != 11550 or item["TryAtStartType"] != 2
            or len(skill["HitList"]) != 1 or skill["HitList"][0]["ClassId"] != 11551
            or hit["DamageId"] != 11552 or len(applications) != 1
            or applications[0]["StatusId"] != 24237 or applications[0]["BasePercent"] != 1
            or applications[0]["AffectedByProp"] or status["DurationRound"] != 2
            or dispel["BasePercent"] != 1 or dispel["AffectedByProp"]
            or sorted(dispel["TargetTypes"]) != [2, 3] or dispel["Count"] != 1 or not dispel["IsRandom"]):
        raise ValueError("Review the original encouragement configuration")

    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    count = 0
    first_disperse = False
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10066 or cast["Slot"] != 0:
                continue
            if count == 0 and step["ActorRound"] != 0:
                raise ValueError("The first encouragement did not occur before the actor's first turn")
            selections = cast["HitSelections"]
            if (cast["SkillEntityId"] != 11550 or len(selections) != 1
                    or selections[0]["HitEntityId"] != 11551):
                raise ValueError("Encouragement lost its original hit entity")
            events = [event for event in step["DamageEvents"] if event["DamageId"] in cast["DamageEventIds"]]
            if (len(events) != 1 or events[0]["TargetId"] != cast["ActorId"]
                    or events[0]["HitEntityId"] != 11551 or events[0]["DamageEntityId"] != 11552
                    or events[0]["Damage"] != 0):
                raise ValueError("Encouragement lost its self-status hit")
            if count == 0:
                first_disperse = (events[0]["HasDisperseNotification"]
                                  and step["DispersedUnitIds"].count(cast["ActorId"]) == 1)
            count += 1
    if not count or not first_disperse:
        raise ValueError("The opening encouragement did not report one successful dispel")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    try:
        result = verify(parser.parse_args().receipt)
    except (ValueError, StopIteration) as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10066 opening encouragement and dispel passed: {result} casts")
