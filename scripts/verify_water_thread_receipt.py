"""Check actual 10017 casts, child damage, cold provenance and delayed pulls."""
import argparse
import json
from pathlib import Path


def verify(path):
    cp = json.loads(path.read_text('utf-8-sig'))['checkpoint']
    actor = next(unit for unit in cp['Units'] if unit['Id'] == 1)
    equipped = next(skill for skill in actor['Skills'] if skill['ItemId'] == 10017 and skill['Slot'] == 0)
    assert equipped['EntityId'] == 10690
    unlimited = bool((cp.get('DamageTest') or {}).get('EnableMonsterUnlimitHp'))
    count, damage_count = 0, 0
    for receipt in cp['Receipts']:
        step = receipt['Event']
        roots = [cast for cast in step['SkillCasts'] if cast['ActorId'] == 1 and cast['Slot'] == 0 and cast['SkillItemId'] == 10017]
        if not roots:
            continue
        for root in roots:
            assert root['SkillEntityId'] == 10690
            children = [cast for cast in step['SkillCasts'] if cast['SkillEntityId'] == 10970 and cast['ActorId'] == 1]
            assert children and all(cast['Slot'] == -1 for cast in children)
            for child in children:
                assert abs(child['Delay'] - root['Delay'] - .15) < .0001
                hits = [hit for hit in step['DamageEvents'] if hit['DamageId'] in child['DamageEventIds']]
                assert len(hits) == 1
                hit = hits[0]
                assert hit['SourceId'] == 1 and hit['TargetId'] != 1
                assert hit['SkillEntityId'] == 10970 and hit['HitEntityId'] == 10971 and hit['DamageEntityId'] == 10972
                assert hit['ElementType'] == 101 and hit['DamageType'] == 1 and hit['Damage'] < 0
                assert not hit['TrueDamage'] and not hit['IsCure']
                assert hit['AfterHp'] - hit['BeforeHp'] == hit['Damage'] or unlimited and hit['AfterHp'] == hit['BeforeHp']
                assert abs(hit['Delay'] - child['Delay'] - .25) < .0001
                damage_count += 1
            for movement in step['Movements']:
                if movement['MoveBehaviourId'] != 10973:
                    continue
                assert len(movement['Path']) > 1 and abs(movement['MoveDuration'] - .15) < .0001
                assert any(hit['TargetId'] == movement['UnitId'] and hit['SkillEntityId'] == 10970
                           and abs(movement['Delay'] - hit['Delay'] - .3) < .0001 for hit in step['DamageEvents'])
            for event in step['StatusViewEvents']:
                status = event['Status']
                if status['ClassId'] != 24266:
                    continue
                assert status['CreatorId'] == 1 and status['DurationRound'] == 2
                assert status['Origin']['SkillItemId'] == 10017 and status['Origin']['SkillEntityId'] == 10690
                assert status['SourceSkillRank'] == equipped['Rank'] and status['SourceSkillLevel'] == equipped['Level']
                assert status['SourceSubRank'] == equipped['SubRank']
            count += 1
    assert count and damage_count, 'No original water-thread child damage was recorded'
    return count, damage_count


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('receipt', type=Path)
    args = parser.parse_args()
    casts, hits = verify(args.receipt)
    print(f'Original water-thread receipts passed: {casts} casts, {hits} child water hits')
