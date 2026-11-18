"""Verify 10015 cast, landing, physical hit and armor-break receipts."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path, initial=None):
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    skill = entities['10670']['FightSkillComponent']
    hit = entities['10671']['FightHitFixedComponent']
    damage = entities['10673']['FightDamageComponent']
    status = entities['24113']['FightStatusComponent']
    move, = skill['SourceMoveList']
    link, = skill['HitList']
    application, = damage['StatusList']
    scope = {(row['Pos']['X'], row['Pos']['Y']) for row in hit['Scope']}
    if (skill['SkillTargetType'] != 2 or move['MoveBehaviourId'] != 10672
            or abs(move['Delay'] - .1) > .0001 or link['ClassId'] != 10671
            or abs(link['Delay'] - .65) > .0001 or link['ActOnSouce'] != 2
            or hit['DamageId'] != 10673 or hit['DamageType'] != 1 or hit['IsTrueDamage']
            or scope != {(1, 0), (-1, 0), (0, 1), (0, -1)}
            or application['StatusId'] != 24113 or application['BasePercent'] != .5
            or not application['AffectedByProp'] or status['DurationRound'] != 2):
        raise ValueError('Review the original jump slash or armor-break configuration')
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
    assembled = json.loads(initial.read_text(encoding='utf-8-sig'))['checkpoint'] if initial else checkpoint
    equipped = [value for unit in assembled['Units'] if unit['Id'] == 1
                for value in unit['Skills'] if value['ItemId'] == 10015 and value['Slot'] == 0]
    if len(equipped) != 1:
        raise ValueError('The equipped original jump slash snapshot is absent')
    runtime_hit = next((value for value in equipped[0]['HitGraphEntities'] if value['EntityId'] == 10671), None)
    if (runtime_hit is None or runtime_hit['DamageId'] != 10673
            or runtime_hit['DamageProperty'] != hit['DamageProp'] or len(runtime_hit['ScopeEntries']) != 4):
        raise ValueError('The assembled jump slash lost its original hit or scope')
    unlimited = bool((checkpoint.get('DamageTest') or {}).get('EnableMonsterUnlimitHp'))
    positions = {(row['X'], row['Y']) for row in skill['Range']}
    count = 0
    for receipt in checkpoint['Receipts']:
        step = receipt['Event']
        for cast in step['SkillCasts']:
            if cast['SkillItemId'] != 10015 or cast['Slot'] != 0:
                continue
            pose = cast['Presentation']
            source = pose['SourceX'], pose['SourceY']
            target = pose['TargetX'], pose['TargetY']
            if (cast['SkillEntityId'] != 10670 or (target[0]-source[0], target[1]-source[1]) not in positions
                    or target == source or pose['FacingIndex'] not in (0, 1, 2, 3)
                    or abs(pose['Duration'] - skill['Duration']) > .0001
                    or abs(pose['ActionDuration'] - skill['ActionDuration']) > .0001):
                raise ValueError('Jump slash changed its original point, entity or action duration')
            moves = [value for value in step['Movements'] if value['UnitId'] == cast['ActorId']
                     and value['MoveBehaviourId'] == 10672 and abs(value['Delay'] - cast['Delay'] - .1) < .0001
                     and value['Path'] and (value['Path'][0]['X'], value['Path'][0]['Y']) == source]
            if (len(moves) != 1 or len(moves[0]['Path']) != 2
                    or (moves[0]['Path'][-1]['X'], moves[0]['Path'][-1]['Y']) != target):
                raise ValueError('Jump slash did not reach its selected point with original movement')
            selections = cast['HitSelections']
            if (len(selections) != 1 or selections[0]['HitEntityId'] != 10671
                    or abs(selections[0]['Delay'] - .65) > .0001
                    or selections[0]['EffectCenters'] != [dict(X=target[0], Y=target[1])]):
                raise ValueError('Jump slash changed its original landing hit center or timing')
            ids = cast['DamageEventIds']
            events = [value for value in step['DamageEvents'] if value['DamageId'] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError('Jump slash lost or duplicated a linked hit')
            cells = {(target[0] + x, target[1] + y) for x, y in scope}
            for event in events:
                if (event['SourceId'] != cast['ActorId'] or event['TargetId'] == cast['ActorId']
                        or event['SkillEntityId'] != 10670 or event['HitEntityId'] != 10671
                        or event['DamageEntityId'] != 10673 or event['DamageType'] != 1
                        or event['ElementType'] != 0 or event['TrueDamage'] or event['IsCure'] or event['Damage'] >= 0
                        or (event['AfterHp'] - event['BeforeHp'] != event['Damage']
                            and not (unlimited and event['AfterHp'] == event['BeforeHp']))
                        or not event['HasHitGridPosition'] or (event['HitGridX'], event['HitGridY']) not in cells
                        or abs(event['Delay'] - cast['Delay'] - .65) > .0001):
                    raise ValueError('Jump slash changed its original physical hit or landing scope')
            affected = {event['TargetId'] for event in events}
            for event in step['StatusViewEvents']:
                if (event['Kind'] != 0 or event['Status']['ClassId'] != 24113
                        or abs(event['Delay'] - cast['Delay'] - .65) > .0001):
                    continue
                if event['OwnerId'] not in affected or event['Status']['CreatorId'] != cast['ActorId']:
                    raise ValueError('Jump slash armor break lost its original caster or hit target')
            count += 1
    if not count:
        raise ValueError('No equipped 10015 cast was recorded')
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
    print(f'Original 10015 landing, physical hit and timing passed: {count} casts')
