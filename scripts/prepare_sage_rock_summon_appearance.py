"""Install the current original Rock Emperor actor and its presentation bindings."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_battle_skill_actor import build


def verify_combat_tables(actor):
    directory = ROOT / 'unity/Assets/Official/Combat'
    battle = json.loads((directory / 'battle-rules.json').read_bytes())['tables']
    properties = json.loads((directory / 'skill-rules.json').read_bytes())['tables']
    rules = json.loads((directory / 'rules.json').read_bytes())
    fixed, = [row for row in properties['summon_monster_fix_prop'] if row['EntityId'] == actor]
    inherited, = [row for row in properties['summon_monster_add_prop'] if row['ClassId'] == actor]
    group_row, = [row for row in battle['monster_group'] if row['MonsterId'] == actor and row['GroupNo'] == 0]
    group_ids = {fixed['GroupLevelPropId'], group_row['PropCfg']['PropGroupId']}
    skill_items = set(group_row['ActiveSkills'] + group_row['PassiveSkills'])
    selections = (
        (battle, 'monster', 'Id', {actor}),
        (battle, 'monster_group', 'MonsterId', {actor}),
        (properties, 'summon_monster_fix_prop', 'EntityId', {actor}),
        (properties, 'summon_monster_add_prop', 'ClassId', {actor}),
        (properties, 'summon_rank_additive_factor', 'RankGroup', {inherited['RankGroup']}),
        (properties, 'entity_prop_group_level', 'GroupId', group_ids),
        (rules, 'skill', 'ClassId', skill_items),
    )
    apk = Apk.current()
    try:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        parser = Constructors(code)
        table_types = {name: constructor for constructor, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
        proof = []
        for baseline, name, key, ids in selections:
            original = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = original.read().m_Script.encode('utf-8', 'surrogateescape')
            selected = json.loads(json.dumps([row for row in parser.table(raw, table_types[name]) if row[key] in ids]))
            expected = [row for row in baseline[name] if row[key] in ids]
            if name == 'monster_group':
                # The runtime stores an absent list of resilience removals as an empty list.
                for row in selected:
                    resilience = row['ResilienceInfo']
                    if resilience['DisappearStatusIds'] is None:
                        resilience['DisappearStatusIds'] = []
            assert selected and selected == expected, ('Current summon table selection differs', name, ids)
            proof.append(dict(table=name, source=apk.provenance(original), rows=len(selected),
                              row_sha256=hashlib.sha256(json.dumps(
                                  selected, ensure_ascii=False, sort_keys=True).encode()).hexdigest()))
        return proof
    finally:
        apk.archive.close()


def main():
    source = OfficialSource()
    build(3196, cache=ROOT / '.tools/source-packages/current' / source.version / 'sage-rock-summon-closure',
        destination=ROOT / 'unity/Assets/Official/Combat/SageRockSummonActor',
        resource_root='Combat/NativeMonsters/SkillActors/RockEmperor', receipt_prefix='sage-rock-summon',
        generator=Path(__file__), combat_tables=verify_combat_tables(3196))


if __name__ == '__main__':
    main()
