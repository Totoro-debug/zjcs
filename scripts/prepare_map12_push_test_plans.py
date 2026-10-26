"""Find test inputs on copied Map12 grids; never used by the game or its rewards."""
from collections import deque
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def build():
    base = ROOT / "unity/Assets/Official/World/Map/12"
    paths = [base / "map12.json", base / "Resources/Map12/stories.json"]
    definition, runtime = [json.loads(p.read_text(encoding="utf8")) for p in paths]
    rules = {r["PlacementId"]: r for r in runtime["puzzleRules"]}
    nav = definition["Navigation"]
    directions = [(0, -1), (-1, 0), (0, 1), (1, 0)]
    plans = []
    for goal in (r for r in rules.values() if r["LockType"] == 6):
        boxes = goal["PushBoxIds"]
        rect = rules[boxes[0]]["MovableRect"]
        lower, upper = rect["MinGrid"], rect["MaxGrid"]
        positions = lambda ids: tuple(sorted((rules[i]["GridX"], rules[i]["GridY"]) for i in ids))
        targets = set(positions(goal["PushBoxDetectorIds"]))
        terminal = next(r for r in rules.values() if goal["PlacementId"] in r["TerminalLockIds"])
        # Development search bounds only. Runtime checks use the original Nav and Rect.
        lo_x, hi_x = min(lower["X"] - 2, terminal["GridX"]), max(upper["X"] + 2, terminal["GridX"])
        lo_y, hi_y = min(lower["Y"] - 2, terminal["GridY"]), max(upper["Y"] + 2, terminal["GridY"])
        masks = {(x, y): 1 << nav["Rows"][y]["Cells"][x]["RawValues"][1]
                 for y in range(lo_y, hi_y + 1) for x in range(lo_x, hi_x + 1)}
        overrides = {}
        for rule in rules.values():
            if rule["PlacementId"] in boxes or not rule["InitiallySpawned"]:
                continue
            cx, cy = rule["RealCenter"]
            for body in rule["Body"]:
                x, y = body["Pos"]["X"] - cx, body["Pos"]["Y"] - cy
                if rule["InitialFlipX"]:
                    x = -x
                x, y = ((x, y), (-x, -y), (-y, x), (y, -x))[rule["InitialDirection"]]
                pos = rule["GridX"] + round(x + cx), rule["GridY"] + round(y + cy)
                if pos not in masks:
                    continue
                destination = overrides if body["OverrideGroundType"] else masks
                destination[pos] = destination.get(pos, 0) | 1 << body["GroundType"]
        masks.update(overrides)
        player_cells = {p for p, mask in masks.items() if mask & ~definition["CharacterMover"]["MoveMask"] == 0}
        box_cells = {p for p, mask in masks.items() if mask & ~rules[boxes[0]]["MoveMask"] == 0
                     and lower["X"] <= p[0] <= upper["X"] and lower["Y"] <= p[1] <= upper["Y"]}

        def reachable(player, occupied):
            found, pending = {player}, [player]
            for x, y in pending:
                for dx, dy in directions:
                    pos = x + dx, y + dy
                    if pos in player_cells and pos not in occupied and pos not in found:
                        found.add(pos)
                        pending.append(pos)
            return found

        initial = positions(boxes)
        player = terminal["GridX"], terminal["GridY"]
        key = initial, min(reachable(player, set(initial)))
        pending = deque([(key, player)])
        parents = {key: None}
        end = None
        while pending:
            key, player = pending.popleft()
            placed = key[0]
            if targets <= set(placed):
                end = key
                break
            reach = reachable(player, set(placed))
            for old in placed:
                for dx, dy in directions:
                    behind = old[0] - dx, old[1] - dy
                    nxt = old[0] + dx, old[1] + dy
                    if behind not in reach or nxt not in box_cells or nxt in placed:
                        continue
                    moved = tuple(sorted(nxt if p == old else p for p in placed))
                    next_key = moved, min(reachable(old, set(moved)))
                    if next_key in parents:
                        continue
                    parents[next_key] = key, dict(BoxX=old[0], BoxY=old[1], PlayerX=behind[0], PlayerY=behind[1])
                    pending.append((next_key, old))
        if end is None:
            raise ValueError(f"No test plan for original lock {goal['PlacementId']}; explored {len(parents)}")
        actions = []
        while parents[end] is not None:
            end, action = parents[end]
            actions.append(action)
        actions.reverse()
        plans.append(dict(LockId=goal["PlacementId"], TerminalId=terminal["PlacementId"], Actions=actions))
        print(f"Map12 lock {goal['PlacementId']}: {len(actions)} pushes, {len(parents)} copied search states", flush=True)
    out = ROOT / "scripts/testdata/map12-push-plans.json"
    out.write_text(json.dumps(dict(Inputs=[dict(Path=p.relative_to(ROOT).as_posix(),
        Sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths], Plans=plans),
        ensure_ascii=False, indent=2) + "\n", encoding="utf8")


if __name__ == "__main__":
    build()
