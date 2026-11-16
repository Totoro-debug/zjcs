"""Check every equipped fire slash against its authored directional hit and cell timing."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FACING = {'up': 2, 'down': 0, 'left': 1, 'right': 3}
ENTITIES = {2: 11640, 0: 11660, 1: 11650, 3: 11650}


def verify(path, direction=None):
    definitions = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            definitions.setdefault(key, {}).update(value)
    checkpoint = json.loads(path.read_text(encoding='utf-8-sig'))['checkpoint']
    verified = 0
    for receipt in checkpoint['Receipts']:
        step = receipt['Event']
        for cast in step['SkillCasts']:
            if cast['SkillItemId'] != 10026 or cast['Slot'] != 0:
                continue
            pose = cast['Presentation']
            facing = pose['FacingIndex']
            if facing not in ENTITIES or direction is not None and facing != FACING[direction]:
                raise ValueError(f'{path}: 10026 did not face the requested direction')
            # Caster-area releases without a selected adjacent target retain the
            # original base entity and the role's previous facing/mirror.
            base_release = cast['SkillEntityId'] == 10780 and not cast['TargetIds']
            if ((not base_release and cast['SkillEntityId'] != ENTITIES[facing])
                    or (direction is not None and base_release)
                    or (not base_release and (direction is not None or facing in (1, 3))
                        and pose['FlipX'] != (facing == 1))):
                raise ValueError(f'{path}: 10026 used the wrong directional entity or mirror')
            skill = definitions[str(cast['SkillEntityId'])]['FightSkillComponent']
            link, = skill['HitList']
            hit = definitions[str(link['ClassId'])]['FightHitFixedComponent']
            scope = {}
            for cell in hit['Scope']:
                x, y = cell['Pos']['X'], cell['Pos']['Y']
                # Common.GridTransformComponent flips local X before rotation.
                if pose['FlipX']:
                    x = -x
                x, y = {2: (x, y), 0: (-x, -y), 1: (-y, x), 3: (y, -x)}[facing]
                scope[pose['SourceX'] + x, pose['SourceY'] + y] = cell['Delay']
            if (abs(pose['Duration'] - skill['Duration']) > .0001
                    or abs(pose['ActionDuration'] - skill['ActionDuration']) > .0001):
                raise ValueError(f'{path}: 10026 changed its original action clock')
            events = [event for event in step['DamageEvents']
                      if event['SourceId'] == cast['ActorId']
                      and event['SkillEntityId'] == cast['SkillEntityId']]
            if not events:
                raise ValueError(f'{path}: 10026 played without its required damage')
            if len({event['TargetId'] for event in events}) != len(events):
                raise ValueError(f'{path}: 10026 damaged an entity more than once')
            for event in events:
                if (event['HitEntityId'] != link['ClassId'] or event['DamageEntityId'] != hit['DamageId']
                        or event['ElementType'] != 102 or not event['TrueDamage'] or event['IsCure']
                        or not event['HasDamagePosition']):
                    raise ValueError(f'{path}: 10026 lost its original fire annihilation hit')
                position = event['DamageX'], event['DamageY']
                if position not in scope:
                    raise ValueError(f'{path}: 10026 damaged outside its original 3x2 area')
                if abs(event['Delay'] - cast['Delay'] - link['Delay'] - scope[position]) > .0001:
                    raise ValueError(f'{path}: 10026 changed its directional cell hit timing')
            verified += 1
    if not verified:
        raise ValueError(f'{path}: no equipped 10026 cast was recorded')
    return verified


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--direction', choices=FACING)
    parser.add_argument('receipts', nargs='+', type=Path)
    args = parser.parse_args()
    try:
        count = sum(verify(path, args.direction) for path in args.receipts)
    except ValueError as error:
        parser.exit(1, str(error) + '\n')
    print(f'Original 10026 directional entities, fire damage, scope and authored timing passed: {count} casts')


if __name__ == '__main__':
    main()
