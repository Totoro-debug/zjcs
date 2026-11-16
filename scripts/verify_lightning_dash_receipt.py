"""Verify every 10016 cast's original dash, damage identities and authored timing."""
import argparse
import json
from pathlib import Path


def verify(path):
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
    verified = 0
    for receipt in checkpoint['Receipts']:
        step = receipt['Event']
        for cast in step['SkillCasts']:
            if cast['SkillItemId'] != 10016 or cast['Slot'] != 0:
                continue
            actor = cast['ActorId']
            moves = [move for move in step['Movements']
                     if move['UnitId'] == actor and move['MoveBehaviourId'] == 10682]
            hits = [hit for hit in step['DamageEvents']
                    if hit['SourceId'] == actor and hit['SkillEntityId'] == 10680]
            if not moves or not hits:
                raise ValueError(f'{path}: 10016 played without its required dash/hit: '
                                 f'10682 movements={len(moves)}, damage events={len(hits)}')
            if len(moves) != 1:
                raise ValueError(f'{path}: an equipped dash has multiple 10682 movements')
            if (abs(moves[0]['Delay'] - cast['Delay'] - .2333) > .0001
                    or abs(moves[0]['MoveDuration'] - .1) > .0001):
                raise ValueError(f'{path}: 10016 changed its original movement timing')
            points = [(point['X'], point['Y']) for point in moves[0]['Path']]
            pose = cast['Presentation']
            start = (pose['SourceX'], pose['SourceY'])
            end = (pose['TargetX'], pose['TargetY'])
            dx, dy = end[0] - start[0], end[1] - start[1]
            if (len(points) != 6 or points[0] != start or points[-1] != end
                    or (dx != 0 and dy != 0) or abs(dx) + abs(dy) != 5):
                raise ValueError(f'{path}: 10016 did not reach its original five-cell cardinal landing')
            sx, sy = (dx > 0) - (dx < 0), (dy > 0) - (dy < 0)
            previous = 0
            for x, y in points[1:]:
                distance = (x - start[0]) * sx + (y - start[1]) * sy
                if (x - start[0]) * sy != 0 or (y - start[1]) * sx != 0 or distance != previous + 1:
                    raise ValueError(f'{path}: 10016 detoured or reversed during its straight dash')
                previous = distance
            if len({hit['TargetId'] for hit in hits}) != len(hits):
                raise ValueError(f'{path}: 10016 damaged an intermediate target more than once')
            for hit in hits:
                if (hit['HitEntityId'] != 10681 or hit['DamageEntityId'] != 10026
                        or not hit['TrueDamage'] or hit['ElementType'] != 0 or not hit['HasDamagePosition']):
                    raise ValueError(f'{path}: 10016 did not use its original positioned hit entity')
                x, y = hit['DamageX'] - start[0], hit['DamageY'] - start[1]
                if x * sy != 0 or y * sx != 0 or not 1 <= x * sx + y * sy <= 4:
                    raise ValueError(f'{path}: 10016 damaged outside its intermediate four-cell strip')
                delay = .25 + (0, .03, .07, .09)[x * sx + y * sy - 1]
                if abs(hit['Delay'] - cast['Delay'] - delay) > .0001:
                    raise ValueError(f'{path}: 10016 changed its original positioned hit timing')
            verified += 1
    if not verified:
        raise ValueError(f'{path}: no original equipped 10016 cast was recorded')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('receipts', type=Path, nargs='+')
    args = parser.parse_args()
    failures = []
    for path in args.receipts:
        try:
            verify(path)
        except ValueError as error:
            failures.append(str(error))
    if failures:
        parser.exit(1, '\n'.join(failures) + '\n')
    print(f'Original 10016 dash and actual hit receipts passed: {len(args.receipts)}')


if __name__ == '__main__':
    main()
