"""Verify the original 24008 component and absence of prefab visual bindings."""
import json
from pathlib import Path

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    components = entities['24008']
    assert components['ActionComponent']['ActionType'] == 50
    assert components['FightStatusActionInvisibleComponent'] == {}
    assert components['FightStatusComponent']['DurationRound'] == -1
    configuration = verify_selected_components({'24008': components})
    apk = Apk.current()
    assert apk.version == '884_156692'
    schemas = ScriptSchemas(code_root=code)
    records = []
    try:
        objects = list(apk.objects('defaultpackage_assets_prefabs_ecentity_fight_fightskill_24008.bundle'))
        for obj in objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            name = obj.read(check_read=False).m_Script.read().m_ClassName
            fields = schemas.read(obj)
            records.append(dict(component=name, fields=fields, source=apk.provenance(obj)))
        assert {row['component'] for row in records} == {
            'MyAction', 'MyEntity', 'MyFightStatus', 'MyFightStatusActionInvisible'}
        for row in records:
            for key, value in row['fields'].items():
                if key in ('m_GameObject', 'm_Script', 'myContainer'):
                    continue
                if isinstance(value, dict) and 'm_PathID' in value:
                    assert value['m_PathID'] == 0, (row['component'], key)
                if isinstance(value, list):
                    assert value == [], (row['component'], key)
    finally:
        apk.archive.close()
    proof = dict(manifest='884_156692', configuration=configuration, original_prefab=records,
        original_code=[identity(code / path) for path in (
            'common-source/Common/FightStatusActionInvisibleComponent.cs',
            'common-source/Common/EntityHasInvisibleStatusFilter.cs',
            'client-source/MyFightStatusActionInvisible.cs')],
        operation='APK-first exact component verification and decoded original prefab visual-reference inspection; '
                  'no replacement visual asset generated. Offline behavior is documented in skills.md.',
        verifier=identity(Path(__file__)))
    output = ROOT / 'assets/reference/battle-rules/action-invisible-source-manifest.json'
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 24008: permanent action 50, empty handler, four original components, no visual bindings.')


if __name__ == '__main__':
    main()
