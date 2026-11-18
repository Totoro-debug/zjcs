"""Verify equipped 10051 self-property status and original application timing."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path, initial=None):
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    skill = entities['11260']['FightSkillComponent']
    status = entities['24214']['FightStatusComponent']
    link, = skill['HitList']
    if (skill['TargetType'] != 0 or link['ClassId'] != 11261 or abs(link['Delay'] - .2) > .0001
            or status['DurationRound'] != 4 or status['RoundTarget'] != 2 or status['RoundUpdateTiming'] != 1):
        raise ValueError('Review original battle spirit self-status configuration')
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
    assembled = json.loads(initial.read_text(encoding='utf-8-sig'))['checkpoint'] if initial else checkpoint
    equipped = [value for unit in assembled['Units'] if unit['Id'] == 1
                for value in unit['Skills'] if value['ItemId'] == 10051 and value['Slot'] == 0]
    if len(equipped) != 1:
        raise ValueError('Equipped original battle spirit snapshot is absent')
    count = 0
    for receipt in checkpoint['Receipts']:
        step = receipt['Event']
        for cast in step['SkillCasts']:
            if cast['SkillItemId'] != 10051 or cast['Slot'] != 0:
                continue
            selections = cast['HitSelections']
            if (cast['SkillEntityId'] != 11260 or len(selections) != 1
                    or selections[0]['HitEntityId'] != 11261 or abs(selections[0]['Delay'] - .2) > .0001):
                raise ValueError('Battle spirit changed its original self-status hit')
            events = [value for value in step['DamageEvents'] if value['DamageId'] in cast['DamageEventIds']]
            if (len(events) != 1 or len(cast['DamageEventIds']) != 1 or events[0]['SourceId'] != cast['ActorId']
                    or events[0]['TargetId'] != cast['ActorId'] or events[0]['HitEntityId'] != 11261
                    or events[0]['DamageEntityId'] != 11262 or events[0]['Damage'] != 0
                    or abs(events[0]['Delay'] - cast['Delay'] - .25) > .0001):
                raise ValueError('Battle spirit lost its original zero-damage self hit')
            starts = [value for value in step['StatusViewEvents'] if value['Kind'] == 0
                      and value['OwnerId'] == cast['ActorId'] and value['Status']['ClassId'] == 24214
                      and value['Status']['CreatorId'] == cast['ActorId']
                      and abs(value['Delay'] - cast['Delay'] - .25) < .0001]
            if len(starts) != 1:
                raise ValueError('Battle spirit lost its original self-status startup')
            count += 1
    if not count:
        raise ValueError('No equipped 10051 cast was recorded')
    return count


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('receipt', type=Path)
    parser.add_argument('--initial', type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt, args.initial)
    except ValueError as error:
        parser.exit(1, str(error) + '\n')
    print(f'Original 10051 self-status and timing passed: {count} casts')
