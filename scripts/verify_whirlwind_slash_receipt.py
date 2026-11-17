"""Check 10014 casts against the original eight-cell physical area hit."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {"up": 2, "down": 0, "left": 1, "right": 3}


def verify(path, direction, initial=None):
    entities = {}
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((ROOT / "unity/Assets/Official/Combat" / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    skill = entities["10660"]["FightSkillComponent"]
    hits = {int(key): entities[key]["FightHitFixedComponent"] for key in ("10661", "10662")}
    links = skill["HitList"]
    offsets = [(-1, 0), (-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1)]
    expected_links = [(10661, .36), (10661, .54), (10662, .69), (10662, .91)]
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or len(links) != 4 or any(link["ClassId"] != entity or abs(link["Delay"] - delay) > .0001
                                     for link, (entity, delay) in zip(links, expected_links))):
        raise ValueError("Review the original whirlwind slash hit sequence")
    for entity, damage, interval in ((10661, 10022, .025), (10662, 10663, .015)):
        hit = hits[entity]
        if (hit["DamageId"] != damage or hit["DamageType"] != 1 or hit["IsTrueDamage"]
                or [(row["Pos"]["X"], row["Pos"]["Y"]) for row in hit["Scope"]] != offsets
                or any(abs(row["Delay"] - i * interval) > .0001 for i, row in enumerate(hit["Scope"]))):
            raise ValueError("Review the original whirlwind slash area and cell timings")
    checkpoint = json.loads(path.read_text(encoding="utf-8-sig"))["checkpoint"]
    assembled = json.loads(initial.read_text(encoding="utf-8-sig"))["checkpoint"] if initial else checkpoint
    equipped = [value for unit in assembled["Units"] if unit["Id"] == 1
                for value in unit["Skills"] if value["ItemId"] == 10014 and value["Slot"] == 0]
    if len(equipped) != 1:
        raise ValueError("The equipped original whirlwind slash snapshot is absent")
    actual_hits = {value["EntityId"]: value for value in equipped[0]["HitGraphEntities"]}
    for entity, hit in hits.items():
        actual = actual_hits.get(entity)
        if (actual is None or actual["DamageId"] != hit["DamageId"]
                or actual["DamageProperty"] != hit["DamageProp"] or len(actual["ScopeEntries"]) != 8):
            raise ValueError("The assembled whirlwind slash lost its original damage or scope")
    unlimited_hp = bool((checkpoint.get("DamageTest") or {}).get("EnableMonsterUnlimitHp"))
    count = 0
    for receipt in checkpoint["Receipts"]:
        step = receipt["Event"]
        for cast in step["SkillCasts"]:
            if cast["SkillItemId"] != 10014 or cast["Slot"] != 0:
                continue
            pose = cast["Presentation"]
            if (cast["SkillEntityId"] != 10660 or pose["FacingIndex"] not in (0, 1, 2, 3)
                    or direction and pose["FacingIndex"] != DIRECTIONS[direction]
                    or abs(pose["Duration"] - skill["Duration"]) > .0001
                    or abs(pose["ActionDuration"] - skill["ActionDuration"]) > .0001):
                raise ValueError("Whirlwind slash changed its entity, facing or action duration")
            facing = pose["FacingIndex"]
            common_direction = {0: 1, 1: 2, 2: 0, 3: 3}[facing]
            def world(position):
                x, y = position
                if pose["FlipX"]:
                    x = -x
                if common_direction == 1:
                    x, y = -x, -y
                elif common_direction == 2:
                    x, y = -y, x
                elif common_direction == 3:
                    x, y = y, -x
                return pose["SourceX"] + x, pose["SourceY"] + y
            cell_index = {world(position): i for i, position in enumerate(offsets)}
            selections = cast["HitSelections"]
            if len(selections) != 4 or any(selection["HitEntityId"] != link["ClassId"]
                    or abs(selection["Delay"] - link["Delay"]) > .0001
                    for selection, link in zip(selections, links)):
                raise ValueError("Whirlwind slash changed its original hit selections or delays")
            ids = cast["DamageEventIds"]
            events = [event for event in step["DamageEvents"] if event["DamageId"] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError("Whirlwind slash lost or duplicated a linked damage event")
            observed = set()
            for event in events:
                entity = event["HitEntityId"]
                cell = event["HitGridX"], event["HitGridY"]
                if entity not in hits or cell not in cell_index:
                    raise ValueError("Whirlwind slash hit outside its original square")
                hit = hits[entity]
                scope_delay = hit["Scope"][cell_index[cell]]["Delay"]
                matching = [i for i, link in enumerate(links) if link["ClassId"] == entity
                            and abs(event["Delay"] - cast["Delay"] - link["Delay"] - scope_delay) < .0001]
                if (len(matching) != 1 or (event["TargetId"], cell, matching[0]) in observed
                        or event["SourceId"] != cast["ActorId"] or event["TargetId"] == cast["ActorId"]
                        or event["SkillEntityId"] != 10660 or event["DamageEntityId"] != hit["DamageId"]
                        or event["DamageType"] != hit["DamageType"] or event["ElementType"] != 0
                        or event["TrueDamage"] or event["IsCure"] or event["Damage"] >= 0
                        or (event["AfterHp"] - event["BeforeHp"] != event["Damage"]
                            and not (unlimited_hp and event["AfterHp"] == event["BeforeHp"]))
                        or not event["HasHitGridPosition"]):
                    raise ValueError("Whirlwind slash changed its original physical hit, scope or cell timing")
                observed.add((event["TargetId"], cell, matching[0]))
            count += 1
    if not count:
        raise ValueError("No equipped 10014 cast was recorded")
    return count


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--direction", choices=DIRECTIONS)
    parser.add_argument("--initial", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt, args.direction, args.initial)
    except ValueError as error:
        parser.exit(1, str(error) + "\n")
    print(f"Original 10014 eight-cell physical hit and timing passed: {count} casts")
