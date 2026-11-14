"""Verify the original Knight return-strike passive and its conditional damage status."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25003', '24268')}
    damage = definitions['25003']['FightStatusDamageSkillComponent']
    assert damage['Damage'] and not damage['Cure'] and damage['Rate'] == 1
    assert damage['ConditionCount'] == 1 and damage['MaxCount'] == damage['EachRoundMaxCount'] == -1
    assert damage['StatusId'] == 24268 and damage['SourceStatusId'] == damage['SkillId'] == 0
    assert not damage['IsAttackerTarget'] and damage['SkillCfgs'] == damage['StatusCfgs'] == []
    stack = definitions['24268']['FightStatusComponent']
    assert stack['IsOpenStack'] and stack['InitStackCount'] == stack['AddStackedCount'] == 1
    assert stack['MaxStackedCount'] == 10 and not stack['ScalePropWhenChangeStacked']
    hit = definitions['24268']['FightStatusHitDmgAddPerComponent']
    assert hit['HitSourceType'] == 2 and hit['Damage'] and not hit['Cure']
    assert hit['DurationSkillCount'] == 1 and hit['OnlySkillActions'] == [2, 6]
    assert not hit['UpdateDurationSkillCountIfTrigger']
    skill_payload = json.loads((ROOT / 'unity/Assets/Official/Combat/skill-rules.json').read_bytes())
    assert skill_payload['prop_names']['231'] == 'StatusDmgAddPer'
    assert skill_payload['prop_names']['235'] == 'FixedStatusDmgAdd'
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10509)
    assert skill['EcEntityId'] == 25003 and skill['PassiveStatusIdList'] == [25003]
    configuration = verify_selected_components(definitions, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusDamageBaseComponent.cs',
        'common-source/Common/FightStatusDamageBaseComponentInfo.cs',
        'common-source/Common/FightStatusDamageSkillComponent.cs',
        'common-source/Common/FightStatusDamageSkillComponentInfo.cs',
        'common-source/Common/FightStatusHitDmgAddPerComponent.cs',
        'common-source/Common/FightStatusHitDmgAddPerComponentInfo.cs',
        'common-source/Common/FightStatusComponentImplement.cs',
        'common-source/Common/PropType.cs',
        'client-source/MyFightStatusDamageSkill.cs')]
    # Reuse the already extracted APK/cloud closure, preserving its independent identities.
    asset_proof_path = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    asset_proof = json.loads(asset_proof_path.read_bytes())
    native_inputs = [entry for entry in asset_proof['native_inputs']
        if any(f'/{entity}/{entity}.prefab' in entry['path'] for entity in (25003, 24268))]
    assert len(native_inputs) == 4
    for entry in native_inputs:
        assert identity(ROOT / entry['path']) == entry
        if entry['path'].endswith('.prefab'):
            text = (ROOT / entry['path']).read_text(encoding='utf-8')
            for field in ('myEffect', 'myEndEffect', 'LoopEffectPlayable', 'myPlayablePrefab',
                    'StackedPlayablePrefab', 'InvokePlayable', 'StackPlayable', 'MasterCastPlayable'):
                assert f'  {field}: {{fileID: 0}}' in text
            assert '  ShowSpriteAbovePlayer: 1' in text and '  StackPlayableMap: []' in text
    bindings_path = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    bindings = json.loads(bindings_path.read_bytes())
    graphs = {entry['id']: entry['resource'] for entry in bindings['graphs']}
    runtime_views = [bindings_path]
    for entity in (25003, 24268):
        runtime_views.append(ROOT / ('unity/Assets/Official/Combat/FirstPromotionStatuses/Resources/'
            + graphs[entity] + '.prefab'))
    for path in runtime_views:
        record, = (entry for entry in asset_proof['outputs'] if entry['path'] == path.relative_to(ROOT).as_posix())
        assert identity(path) == record
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        runtime_inputs=[identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json',
            'unity/Assets/Official/Skills/source-manifest.json')]
            + [identity(asset_proof_path)] + [identity(path) for path in runtime_views],
        native_inputs=native_inputs,
        original_status_views='Both original status prefabs show the original overhead sprite; '
            'their start/end/loop/stack/invoke/master-cast playable references are null. '
            'Reuse the verified native prefabs and explicit local graph bindings.',
        input_versions='Existing 854 battle payloads and existing skill UI text retain their separately recorded provenance; no input is replaced.',
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10509 -> 25003 -> 24268, received-damage '
            'configuration, stack limits and conditional cast duration. Server execution is absent; '
            'confirmed offline hit gates and implementation scope are maintained in docs/features/skills-knight.md.')
    (ROOT / 'assets/reference/battle-rules/return-strike-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10509/25003/24268 configuration and nine code inputs.')


if __name__ == '__main__':
    main()
