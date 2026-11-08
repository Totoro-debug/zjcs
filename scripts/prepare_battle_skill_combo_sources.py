"""Record verified official combo configuration and reused hit-anchor branches."""
import hashlib
import json
from pathlib import Path
from verify_battle_skill_graph_rules import verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest())


def main():
    selected = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8-sig'))
    version = selected['active']
    assert version == '884_156692', 'Review a changed official source version.'
    current = ROOT / '.tools/source-packages/current' / version
    receipt = current / 'code-manifest.json'
    code = json.loads(receipt.read_text(encoding='utf-8-sig'))
    assert code['manifest'] == version
    assert code['apk_sha256'] == selected['profiles'][version]['apk']['sha256']
    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        path = current / 'official-code/managed/decoded' / name
        record = identity(path)
        assert record['sha256'] == code['assemblies'][name]['decoded_sha256']
        assemblies.append(record)
    source_names = [
        'client-source/MyFightSkillCombo.cs',
        'client-source/MyFightSkillAIRandomTargetFallbackSpawner.cs',
        'common-source/Common/FightSkillComboComponentInfo.cs',
        'common-source/Common/FightSkillAIRandomTargetFallbackSpawnerComponentInfo.cs',
        'common-source/Common/FightSKillMoveCfg.cs',
        'common-source/Common/SkillAiCfg.cs', 'common-source/Common/Direction.cs',
        'common-source/Common/GridTransformComponent.cs', 'common-source/Common/Pos2d.cs',
        'common-source/Common/HitAnchor.cs', 'common-source/Common/HitExecAnchor.cs',
        'common-source/Common/HitAnchorExtensions.cs']
    sources = [identity(current / 'official-code' / name) for name in source_names]
    paths = [ROOT / 'unity/Assets/Official/Combat' / name for name in ('skill-rules.json', 'battle-rules.json')]
    entities = {}
    for path in paths:
        for key, components in json.loads(path.read_text(encoding='utf-8-sig'))['entities'].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: {name: fields for name, fields in entities[key].items() if name in (
        'FightSkillComponent', 'FightSkillComboComponent', 'FightSkillAIRandomTargetFallbackSpawnerComponent')}
        for key in ('13100', '13104', '13105', '13106', '13107', '13108')}
    selected_configuration = verify_selected_components(definitions)
    assert definitions['13100']['FightSkillComboComponent']['ComboSkills'] == [13104, 13105, 13106, 13107, 13108]
    for key in definitions:
        move = definitions[key]['FightSkillComponent']['SourceMoveList'][0]
        assert move['AiMustMoveTo'] and not move['CanExpandOffset'] and move['Distance'] == -1
        if key != '13100':
            ai = definitions[key]['FightSkillComponent']['Ai']
            assert ai['IsFixSkillDirection'] and ai['FixedDirection'] == (1 if key == '13107' else 0)
            fallback = definitions[key]['FightSkillAIRandomTargetFallbackSpawnerComponent']
            assert fallback['ReplaceSkills'] == [13104, 13105, 13106, 13107] and len(fallback['FixedRange']) == 25
    output = ROOT / 'assets/reference/battle-rules/skill-combo-source-manifest.json'
    record = dict(manifest=version, code_receipt=identity(receipt), assemblies=assemblies,
        official_sources=sources, configuration_inputs=[identity(path) for path in paths],
        definitions=definitions, selected_configuration=selected_configuration,
        operation='Reuse official HitAnchor/HitExecAnchor mapping. Preserve original combo order, '
            'skill parameters, fixed facing, offsets, mandatory-move flags and fallback range. The two empty '
            'client executors are implemented using the user-confirmed offline sequence/fallback '
            'and nearest-legal-body-cell selection rules; '
            'these execution rules are not attributed to the missing server.', generator=identity(Path(__file__)))
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original combo, five fallback definitions and official hit-anchor branches.')


if __name__ == '__main__':
    main()
