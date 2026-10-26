"""Find traversal fixtures; gameplay still verifies every move and original award.

Inputs are JSON floor, blocked-edge, start and plate definitions supplied with --input.
OR-Tools is a development dependency in .tools/map-solver-python, never exported.
"""
import argparse
import json
import sys
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/map-solver-python'))
from ortools.sat.python import cp_model

DIRECTIONS = ((-1, 0), (1, 0), (0, -1), (0, 1))


def incoming(source, target):
    dx, dy = source[0] - target[0], source[1] - target[1]
    return 0 if dy > abs(dx) else 1 if dy < -abs(dx) else 2 if dx < 0 else 3


def solve(problem):
    floor = {tuple(p) for p in problem['floor']}
    blocked = {tuple(sorted(((e[0], e[1]), (e[2], e[3])))) for e in problem['blocked']}
    plates = {tuple(p['at']): p for p in problem['plates']}
    nodes = [dict(at=problem['start'], count=0)]
    for plate in problem['plates']:
        nodes.extend([plate] * plate['count'])
    arcs, routes = [], {}
    model = cp_model.CpModel()

    def arc(source, target, route):
        if source == target or (source, target) in routes:
            return
        literal = model.new_bool_var(f'{source}_{target}')
        arcs.append((source, target, literal))
        routes[source, target] = route

    for index, node in enumerate(nodes):
        at = tuple(node['at'])
        if 'portal' in node:
            landing = tuple(node['portal'])
            target = plates.get(landing)
            if target and (not target['directions'] or incoming(at, landing) in target['directions']):
                for other, copy in enumerate(nodes[1:], 1):
                    if tuple(copy['at']) == landing:
                        arc(index, other, [])
                continue
            at = landing
        if index:
            arc(index, 0, [])
        previous = {at: None}
        queue = deque([at])
        while queue:
            cell = queue.popleft()
            for dx, dy in DIRECTIONS:
                following = cell[0] + dx, cell[1] + dy
                if following not in floor or tuple(sorted((cell, following))) in blocked:
                    continue
                target = plates.get(following)
                if target and (not target['directions'] or incoming(cell, following) in target['directions']):
                    route, cursor = [following], cell
                    while cursor is not None:
                        route.append(cursor)
                        cursor = previous[cursor]
                    route.reverse()
                    for other, copy in enumerate(nodes[1:], 1):
                        if tuple(copy['at']) == following:
                            arc(index, other, route)
                    continue
                if following not in previous:
                    previous[following] = cell
                    queue.append(following)
    model.add_circuit(arcs)
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 60
    solver.parameters.num_search_workers = 4
    solver.parameters.stop_after_first_solution = True
    status = solver.solve(model)
    if status not in (cp_model.FEASIBLE, cp_model.OPTIMAL):
        raise RuntimeError(f"Map {problem['map']}: {solver.status_name(status)}")
    following = {source: target for source, target, literal in arcs if solver.boolean_value(literal)}
    answer, at = [problem['start']], 0
    while True:
        target = following[at]
        for point in routes[at, target][1:]:
            if list(point) != answer[-1]:
                answer.append(list(point))
        if target == 0:
            break
        at = target
    return answer


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True, help='Directory of plate problem fixtures')
    parser.add_argument('--output', type=Path, required=True, help='Gameplay route fixture destination')
    args = parser.parse_args()
    output = args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    plans = json.loads(output.read_text(encoding='utf8')) if output.exists() else {}
    for source in sorted(args.input.glob('*.json')):
        problem = json.loads(source.read_text(encoding='utf8'))
        key = str(problem['map'])
        if key in plans:
            continue
        plans[key] = solve(problem)
        output.write_text(json.dumps(plans, separators=(',', ':')), encoding='utf8')
        print(f"Solved native plate map {key}: {len(plans[key]) - 1} moves", flush=True)


if __name__ == '__main__':
    main()
