"""Check 10011 casts against the three original physical hit configurations."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {"up": (2, 0, 1), "down": (0, 0, -1),
              "left": (1, -1, 0), "right": (3, 1, 0)}


def verify(path, direction, initial=None):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    skill = entities["10630"]["FightSkillComponent"]
    links = skill["HitList"]
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or { (p["X"], p["Y"]) for p in skill["Range"] } != {(0, 1), (0, -1), (1, 0), (-1, 0)}
            or [link["ClassId"] for link in links] != [10631, 10632, 10633]):
        raise ValueError("Review the original lion-combo skill configuration")
    hits = {link["ClassId"]: entities[str(link["ClassId"])]["FightHitFixedComponent"] for link in links}
    for hit_id, damage_prop in zip((10631, 10632, 10633), (200, 200, 201)):
        hit = hits[hit_id]
        if (hit["DamageId"] != 10022 or hit["DamageType"] != 1 or hit["DamageProp"] != damage_prop
                or hit["IsTrueDamage"] or len(hit["Scope"]) != 1
                or hit["Scope"][0]["Pos"] != {"X": 0, "Y": 1} or hit["Scope"][0]["Delay"] != 0):
            raise ValueError("Review the original lion-combo physical hit configuration")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    assembled = json.loads(initial.read_text(encoding="utf-8-sig"))["checkpoint"] if initial else checkpoint
    actors = [unit for unit in assembled["Units"] if unit["Id"] == 1]
    equipped = [value for actor in actors for value in actor["Skills"] if value["ItemId"] == 10011 and value["Slot"] == 0]
    if len(equipped) != 1:
        raise ValueError("The original equipped lion-combo skill snapshot is absent")
    runtime_hits = {value["EntityId"]: value for value in equipped[0]["HitGraphEntities"]}
    for hit_id, original in hits.items():
        actual = runtime_hits.get(hit_id)
        if (actual is None or actual["DamageId"] != original["DamageId"]
                or actual["DamageProperty"] != original["DamageProp"]
                or actual["FixedDamageProperty"] != original["FixedDamageProp"]):
            raise ValueError("The assembled lion-combo hit lost its original damage properties")
    unlimited_hp = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10011 or cast["Slot"] != 0:
                continue
            pose = cast["Presentation"]
            facing = pose["FacingIndex"]
            if (cast["SkillEntityId"] != 10630 or facing not in (0, 1, 2, 3)
                    or direction and facing != DIRECTIONS[direction][0]
                    or abs(pose["Duration"] - skill["Duration"]) > .0001
                    or abs(pose["ActionDuration"] - skill["ActionDuration"]) > .0001):
                raise ValueError("Lion combo changed its target, facing or action duration")
            dx, dy = {index: (x, y) for index, x, y in DIRECTIONS.values()}[facing]
            selections = cast["HitSelections"]
            if (len(selections) != 3 or [x["HitEntityId"] for x in selections] != [x["ClassId"] for x in links]
                    or any(abs(selection["Delay"] - link["Delay"]) > .0001 for selection, link in zip(selections, links))):
                raise ValueError("Lion combo changed its original hit order or delay")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError("Lion combo lost or duplicated a linked damage event")
            by_target = {}
            for event in events:
                hit = hits.get(event["HitEntityId"])
                if (hit is None or event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 10630 or event["DamageEntityId"] != hit["DamageId"]
                        or event["DamageType"] != hit["DamageType"] or event["ElementType"] != skill["ElementType"]
                        or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                            and not (unlimited_hp and event["AfterHp"] == event["BeforeHp"]))
                        or not event["HasDamagePosition"]
                        or (event["DamageX"], event["DamageY"]) != (pose["SourceX"] + dx, pose["SourceY"] + dy)):
                    raise ValueError("Lion combo changed its original physical hit or target cell")
                by_target.setdefault(event["TargetId"], []).append(event)
            for target_events in by_target.values():
                ordered = sorted(target_events, key=lambda value: value["Delay"])
                if (len(ordered) != 3 or [event["HitEntityId"] for event in ordered] != [link["ClassId"] for link in links]
                        or any(abs(event["Delay"] - cast["Delay"] - link["Delay"]) > .0001 for event, link in zip(ordered, links))):
                    raise ValueError("Lion combo changed its three original hit times")
            count += 1
    if not count:
        raise ValueError("No equipped 10011 cast was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--direction", choices=DIRECTIONS)
    parser.add_argument("--initial", type=Path, help="initial actor checkpoint if the fixture later replaces the skill")
    args = parser.parse_args()
    try:
        count = verify(args.receipt, args.direction, args.initial)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10011 three physical hits and timing passed: {count} casts")
