"""Verify equipped 10043 wind damage and the original timed cross."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(path, initial=None):
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    skill = entities['10590']['FightSkillComponent']
    hit = entities['10591']['FightHitFixedComponent']
    damage = entities['10592']['FightDamageComponent']
    link, = skill['HitList']
    scope = {(row['Pos']['X'], row['Pos']['Y']): row['Delay'] for row in hit['Scope']}
    if (skill['SkillTargetType'] != 0 or skill['TargetType'] != 1 or skill['ElementType'] != 100
            or link['ClassId'] != 10591 or abs(link['Delay'] - .45) > .0001
            or hit['DamageId'] != 10592 or hit['DamageType'] != 1 or hit['IsTrueDamage']
            or set(scope) != {(0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)}
            or scope[(0, 0)] != 0 or any(abs(delay - .1) > .0001 for point, delay in scope.items() if point != (0, 0))
            or damage['StatusList'] or damage['MoveCfg']['MoveBehaviourId'] != 0):
        raise ValueError('Review the original falling wind range, damage and timed scope')
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
    assembled = json.loads(initial.read_text(encoding='utf-8-sig'))['checkpoint'] if initial else checkpoint
    equipped = [value for unit in assembled['Units'] if unit['Id'] == 1
                for value in unit['Skills'] if value['ItemId'] == 10043 and value['Slot'] == 0]
    if len(equipped) != 1:
        raise ValueError('The equipped original falling wind snapshot is absent')
    actual = next((value for value in equipped[0]['HitGraphEntities'] if value['EntityId'] == 10591), None)
    if actual is None or actual['DamageId'] != 10592 or len(actual['ScopeEntries']) != 5:
        raise ValueError('The assembled falling wind lost its original hit or scope')
    unlimited = bool((checkpoint.get('DamageTest') or {}).get('EnableMonsterUnlimitHp'))
    positions = {(row['X'], row['Y']) for row in skill['Range']}
    count = 0
    for receipt in checkpoint['Receipts']:
        step = receipt['Event']
        for cast in step['SkillCasts']:
            if cast['SkillItemId'] != 10043 or cast['Slot'] != 0:
                continue
            pose = cast['Presentation']
            source = pose['SourceX'], pose['SourceY']
            target = pose['TargetX'], pose['TargetY']
            if (cast['SkillEntityId'] != 10590 or (target[0]-source[0], target[1]-source[1]) not in positions
                    or pose['FacingIndex'] not in (0, 1, 2, 3)
                    or abs(pose['Duration'] - link['Delay'] - skill['AllEndedDuration']) > .0001
                    or abs(pose['ActionDuration'] - skill['ActionDuration']) > .0001):
                raise ValueError('Falling wind changed its original point, entity or action duration')
            selections = cast['HitSelections']
            if (len(selections) != 1 or selections[0]['HitEntityId'] != 10591
                    or abs(selections[0]['Delay'] - .45) > .0001
                    or selections[0]['EffectCenters'] != [dict(X=target[0], Y=target[1])]):
                raise ValueError('Falling wind changed its original hit selection')
            ids = cast['DamageEventIds']
            events = [value for value in step['DamageEvents'] if value['DamageId'] in ids]
            if len(events) != len(ids) or len(set(ids)) != len(ids) or not events:
                raise ValueError('Falling wind lost or duplicated a linked hit')
            for event in events:
                cell = event['HitGridX'], event['HitGridY']
                offset = cell[0] - target[0], cell[1] - target[1]
                if (event['SourceId'] != cast['ActorId'] or event['TargetId'] == cast['ActorId']
                        or event['SkillEntityId'] != 10590 or event['HitEntityId'] != 10591
                        or event['DamageEntityId'] != 10592 or event['DamageType'] != 1
                        or event['ElementType'] != 100 or event['TrueDamage'] or event['IsCure'] or event['Damage'] >= 0
                        or (event['AfterHp'] - event['BeforeHp'] != event['Damage']
                            and not (unlimited and event['AfterHp'] == event['BeforeHp']))
                        or not event['HasHitGridPosition'] or offset not in scope
                        or abs(event['Delay'] - cast['Delay'] - .45 - scope[offset]) > .0001):
                    raise ValueError('Falling wind changed its original wind hit, scope or timing')
            count += 1
    if not count:
        raise ValueError('No equipped 10043 cast was recorded')
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
    print(f'Original 10043 wind damage and timed cross passed: {count} casts')
