"""Check acquired 10053 casts against the original five-hit area configuration."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    skill = entities["11250"]["FightSkillComponent"]
    hit = entities["11251"]["FightHitFixedComponent"]
    links = skill["HitList"]
    if len(links) != 5 or any(link["ClassId"] != 11251 for link in links):
        raise ValueError("Review the original five-hit configuration")
    expected_delays = sorted(link["Delay"] for link in links)
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    verified = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10053 or cast["Slot"] != 0:
                continue
            pose = cast["Presentation"]
            if cast["SkillEntityId"] != 11250 or pose["FacingIndex"] not in (0, 1, 2, 3):
                raise ValueError("Sunset sword used a foreign entity or facing")
            if (abs(pose["Duration"] - skill["Duration"]) > .0001
                    or abs(pose["ActionDuration"] - skill["ActionDuration"]) > .0001):
                raise ValueError("Sunset sword changed its original action clock")
            scope = set()
            for cell in hit["Scope"]:
                x, y = cell["Pos"]["X"], cell["Pos"]["Y"]
                if pose["FlipX"]:
                    x = -x
                x, y = {2: (x, y), 0: (-x, -y), 1: (-y, x), 3: (y, -x)}[pose["FacingIndex"]]
                scope.add((pose["SourceX"] + x, pose["SourceY"] + y))
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if not events or len(events) != len(ids) or len(set(ids)) != len(ids):
                raise ValueError("Sunset sword lost or duplicated a linked damage event")
            targets = {}
            for event in events:
                if (event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 11250 or event["HitEntityId"] != 11251
                        or event["DamageEntityId"] != hit["DamageId"] or event["ElementType"] != 0
                        or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or not event["HasDamagePosition"]
                        or (event["DamageX"], event["DamageY"]) not in scope):
                    raise ValueError("Sunset sword changed its original physical area hit")
                targets.setdefault(event["TargetId"], []).append(event["Delay"] - cast["Delay"])
            for delays in targets.values():
                if len(delays) != len(expected_delays) or any(
                        abs(actual - expected) > .0001 for actual, expected in zip(sorted(delays), expected_delays)):
                    raise ValueError("Sunset sword changed its five hit times")
            verified += 1
    if not verified:
        raise ValueError("No equipped sunset sword cast was recorded")
    return verified


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10053 five-hit area and timing passed: {count} casts")
