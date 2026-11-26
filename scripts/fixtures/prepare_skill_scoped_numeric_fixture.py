"""Make an in-memory regression fixture; never writes official/runtime data."""
import copy
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
combat = root / 'unity/Assets/Official/Combat'
skill = json.loads((combat / 'skill-rules.json').read_bytes())
battle = json.loads((combat / 'battle-rules.json').read_bytes())
prop = next(row for row in skill['tables']['entity_prop'] if row['EntityId'] == 12070)
group = prop['GroupLevelPropId']
sentinel = next(row['EntityId'] for row in skill['tables']['entity_prop']
                if row['GroupLevelPropId'] == group and row['EntityId'] != 12070)
levels = [row for row in skill['tables']['entity_prop_group_level']
          if row['GroupId'] == group and row['SubRank'] == 8]
ids = {str(prop['RankPropId']), *(str(row['LevelPropId']) for row in levels)}
values = {key: {'rows': {level: row for level, row in skill['props'][key]['rows'].items()
                       if level in ('1', '2')}} for key in ids}
for row in values[str(levels[0]['LevelPropId'])]['rows'].values():
    for key in row:
        row[key] *= 2
entity = copy.deepcopy(skill['entities']['12070'])
entity.update(battle['entities'].get('12070', {}))
pack = dict(manifest='scoped-numeric-fixture', item_id=10098, scope_numeric=True,
            entities={'12070': entity}, previous_entities={'12070': entity},
            tables=dict(entity_prop=[prop], entity_prop_group_level=levels), props=values)
invalid = []
for mode in range(4):
    broken = copy.deepcopy(pack)
    if mode == 0:
        broken['tables']['entity_prop'][0]['EntityId'] = sentinel
    elif mode == 1:
        del broken['props'][str(prop['RankPropId'])]
    elif mode == 2:
        broken['tables']['entity_prop_group_level'] = []
    else:
        del broken['props'][str(levels[0]['LevelPropId'])]
    invalid.append(json.dumps(broken, separators=(',', ':')))
print(json.dumps(dict(pack=json.dumps(pack, separators=(',', ':')), sentinel=sentinel, invalid=invalid)))
