"""Verify equipped 10018 wind hit and damage-relative pull timing."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {'up': 2, 'down': 0, 'left': 1, 'right': 3}


def verify(path, direction=None, initial=None):
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    skill = entities['10700']['FightSkillComponent']
    hit = entities['10701']['FightHitFixedComponent']
    move = entities['10702']['FightDamageComponent']['MoveCfg']
    link, = skill['HitList']
    scope = {(row['Pos']['X'], row['Pos']['Y']) for row in hit['Scope']}
    if (skill['SkillTargetType'] != 0 or skill['TargetType'] != 1 or skill['ElementType'] != 100
            or link['ClassId'] != 10701 or link['Delay'] != 0 or hit['DamageId'] != 10702
            or hit['DamageType'] != 1 or hit['IsTrueDamage']
            or scope != {(x, y) for x in (-1, 0, 1) for y in (-1, 0, 1)}
            or any(abs(row['Delay'] - .15) > .0001 for row in hit['Scope'])
            or move['MoveBehaviourId'] != 10703 or move['Distance'] != -1
            or not move['TowardsTargetPos'] or abs(move['Delay'] - .35) > .0001):
        raise ValueError('Review the original pull skill or displacement configuration')
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
    assembled = json.loads(initial.read_text(encoding='utf-8-sig'))['checkpoint'] if initial else checkpoint
    equipped = [value for unit in assembled['Units'] if unit['Id'] == 1
                for value in unit['Skills'] if value['ItemId'] == 10018 and value['Slot'] == 0]
    if len(equipped) != 1:
        raise ValueError('The equipped original pull skill snapshot is absent')
    actual = next((value for value in equipped[0]['HitGraphEntities'] if value['EntityId'] == 10701), None)
    if actual is None or actual['DamageId'] != 10702 or len(actual['ScopeEntries']) != 9:
        raise ValueError('The assembled pull lost its original wind damage or scope')
    unlimited = bool((checkpoint.get('DamageTest') or {}).get('EnableMonsterUnlimitHp'))
    count = 0
    for receipt in checkpoint['Receipts']:
        step = receipt['Event']
        for cast in step['SkillCasts']:
            if cast['SkillItemId'] != 10018 or cast['Slot'] != 0:
                continue
            pose = cast['Presentation']; target = pose['TargetX'], pose['TargetY']
            if (cast['SkillEntityId'] != 10700 or pose['FacingIndex'] not in (0, 1, 2, 3)
                    or direction and pose['FacingIndex'] != DIRECTIONS[direction]
                    or abs(pose['Duration'] - skill['Duration']) > .0001
                    or abs(pose['ActionDuration'] - skill['ActionDuration']) > .0001):
                raise ValueError('Pull changed its original entity, facing or duration')
            selections = cast['HitSelections']
            if (len(selections) != 1 or selections[0]['HitEntityId'] != 10701
                    or abs(selections[0]['Delay']) > .0001):
                raise ValueError('Pull changed its original hit selection')
            ids = cast['DamageEventIds']
            events = [value for value in step['DamageEvents'] if value['DamageId'] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError('Pull lost or duplicated a linked damage event')
            for event in events:
                cell = event['HitGridX'], event['HitGridY']
                if (event['SourceId'] != cast['ActorId'] or event['TargetId'] == cast['ActorId']
                        or event['SkillEntityId'] != 10700 or event['HitEntityId'] != 10701
                        or event['DamageEntityId'] != 10702 or event['DamageType'] != 1
                        or event['ElementType'] != 100 or event['TrueDamage'] or event['IsCure'] or event['Damage'] >= 0
                        or (event['AfterHp'] - event['BeforeHp'] != event['Damage']
                            and not (unlimited and event['AfterHp'] == event['BeforeHp']))
                        or not event['HasHitGridPosition'] or (cell[0] - target[0], cell[1] - target[1]) not in scope
                        or abs(event['Delay'] - cast['Delay'] - .15) > .0001):
                    raise ValueError('Pull changed its original wind hit, scope or timing')
            for movement in step['Movements']:
                if movement['MoveBehaviourId'] != 10703:
                    continue
                hits = [event for event in events if event['TargetId'] == movement['UnitId']
                        and abs(movement['Delay'] - event['Delay'] - .35) < .0001]
                if not hits or len(movement['Path']) < 2 or abs(movement['MoveDuration'] - .15) > .0001:
                    raise ValueError('Pull movement did not follow its actual hit and authored delay')
            count += 1
    if not count:
        raise ValueError('No equipped 10018 cast was recorded')
    return count


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('receipt', type=Path)
    parser.add_argument('--direction', choices=DIRECTIONS)
    parser.add_argument('--initial', type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.receipt, args.direction, args.initial)
    except ValueError as error:
        parser.exit(1, str(error) + '\n')
    print(f'Original 10018 wind hit and damage-relative pull passed: {count} casts')
