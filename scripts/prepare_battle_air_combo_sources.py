"""Verify original launch hits and the two airborne status definitions, APK first."""
import json
from pathlib import Path

from verify_battle_skill_graph_rules import identity, verify_selected_components
from official_apk import Apk
from official_unity_schema import ScriptSchemas
import hashlib

ROOT = Path(__file__).resolve().parents[1]


def visual_inputs(code):
    schemas = ScriptSchemas(code_root=code)
    inputs, assemblies = [], {}
    metadata = json.loads((code.parent / 'code-manifest.json').read_bytes())
    with Apk.current() as apk:
        assert apk.version == '884_156692'
        for key in (24269, 24270):
            bundle, = [name for name in apk.index if name.endswith(f'fightskill_{key}.bundle')]
            rows = []
            for obj in apk.objects(bundle):
                if obj.type.name not in ('GameObject', 'Transform', 'MonoBehaviour'):
                    continue
                fields = schemas.read(obj) if obj.type.name == 'MonoBehaviour' else obj.read_typetree()
                row = dict(type=obj.type.name, source=apk.provenance(obj), fields=fields,
                    payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
                if obj.type.name == 'MonoBehaviour':
                    script = obj.read(check_read=False).m_Script.deref().read()
                    row['script'] = dict(name=script.m_ClassName, namespace=script.m_Namespace,
                        assembly=script.m_AssemblyName)
                    originals = [path for path in (
                        code / 'client-source' / (script.m_ClassName + '.cs'),
                        code / 'game-framework-source/GameFramework' / (script.m_ClassName + '.cs'))
                        if path.is_file()]
                    original, = originals
                    row['original_code'] = identity(original)
                    name = script.m_AssemblyName.removesuffix('.dll') + '.dll'
                    assembly = identity(code / 'managed/decoded' / name)
                    assert assembly['sha256'] == metadata['assemblies'][name]['decoded_sha256']
                    assemblies[name] = assembly
                rows.append(row)
            statuses = [row for row in rows if row.get('script', {}).get('name') == 'MyFightStatus']
            status, = statuses
            if key == 24269:
                animator, = [row for row in rows if row.get('script', {}).get('name') == 'MyPlayableAnimator']
                assert animator['fields']['myTriggerParam'] == 'floating'
                assert animator['fields']['myDuration'] == 1.5
                assert status['fields']['myPlayablePrefab']['m_PathID'] != 0
                assert status['fields']['InvokePlayable'] == status['fields']['myPlayablePrefab']
            else:
                assert status['fields']['myPlayablePrefab']['m_PathID'] == 0
                assert status['fields']['InvokePlayable']['m_PathID'] == 0
            inputs.append(dict(id=key, objects=rows))
    return dict(prefabs=inputs, assemblies=list(assemblies.values()), decoder=identity(ROOT / 'scripts/official_unity_schema.py'))


def main():
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    definitions = {key: entities[key] for key in ('10581', '10611', '11121', '24269', '24270')}
    for key in ('10581', '10611', '11121'):
        assert definitions[key]['FightHitFixedComponent']['AirCombo'] == {
            'AirComboStatusId': 24269, 'BasePercent': 1.0, 'AffectedByProp': False}
    air = definitions['24269']
    landing = definitions['24270']
    assert air['ActionComponent']['ActionType'] == 66
    assert landing['ActionComponent']['ActionType'] == 67
    assert air['FightStatusComponent']['DurationRound'] == -1
    assert landing['FightStatusComponent']['DurationRound'] == -1
    remove = air['FightStatusDamageRemoveComponent']
    assert remove['StatusDamageTypeCfg'] == dict(IsDamage=True, IsCure=False, IsBlock=True, IsCrit=False)
    assert remove['StatusAutoRemoveCfg']['MaxCount'] == 1
    assert remove['StatusAutoRemoveCfg']['RemoveTarget'] == 6
    damage = air['FightStatusDamageSkillComponent']
    assert damage['Damage'] and not damage['Block'] and damage['DamageMove'] == 2
    assert damage['ConditionCount'] == 1 and damage['MaxCount'] == -1
    timer = damage['AutoRemove']
    assert timer['ResetTimerWhenInvoke'] and abs(timer['Duration'] - .7) < 1e-7
    assert timer['RemoveTarget'] == 6
    successor, = timer['RemoveStatusCfgs']
    assert successor['StatusId'] == 24270 and successor['BasePercent'] == 1
    assert not successor['AffectedByProp'] and successor['ApplyTarget'] == 2
    timer = landing['StatusAutoRemoveComponent']['AutoRemove']
    assert not timer['ResetTimerWhenInvoke'] and abs(timer['Duration'] - .8) < 1e-7
    assert timer['RemoveTarget'] == 6 and timer['RemoveStatusCfgs'] == []
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    paths = (
        'common-source/Common/HitAirComboCfg.cs',
        'common-source/Common/FightStatusAutoRemoveCfg.cs',
        'common-source/Common/FightStatusDamageTypeCfg.cs',
        'common-source/Common/FightStatusDamageRemoveComponentInfo.cs',
        'common-source/Common/FightStatusDamageSkillComponentInfo.cs',
        'common-source/Common/StatusAutoRemoveComponentInfo.cs',
        'client-source/MyFightStatusDamageRemove.cs',
        'client-source/MyFightStatusDamageSkill.cs',
        'client-source/MyStatusAutoRemove.cs',
    )
    proof = dict(manifest='884_156692', configuration=verify_selected_components(definitions),
        original_code=[identity(code / path) for path in paths], verifier=identity(Path(__file__)),
        original_visuals=visual_inputs(code),
        operation='APK-first exact verification of three launch hits, action types, block-damage conjunction, non-displacement reset and original second-based successor timers. User-confirmed offline scheduling is maintained in docs/features/skills.md.')
    output = ROOT / 'assets/reference/battle-rules/air-combo-source-manifest.json'
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified three original launch hits, airborne/recovery states, nine rule code inputs and complete original visual prefab objects.')


if __name__ == '__main__':
    main()
