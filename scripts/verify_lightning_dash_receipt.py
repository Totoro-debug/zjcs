"""Reject an animation-only 10016 diagnostic using the actual Player receipts."""
import argparse
import json
from pathlib import Path


def verify(path):
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
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
            return
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
