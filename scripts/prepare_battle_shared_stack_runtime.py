"""Record verified official inputs for shared status stack lifecycle binding."""
import hashlib
import json
from pathlib import Path
from official_apk import ROOT
from inspect_battle_entity_configuration import canonical_digest

def identity(path):
    path = Path(path)
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

def main():
    proof_path = ROOT / 'assets/reference/battle-rules/active-entity-configuration-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    assert proof['manifest'] == '884_156692'
    entities_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    entities = json.loads(entities_path.read_bytes())['entities']
    rows = {row['entity_id']: row for row in proof['rows']}
    selected = []
    for entity_id in (24022, 24023, 24024, 24025, 24166, 24385, 24720):
        components = entities[str(entity_id)]
        row = rows[entity_id]
        assert canonical_digest(components) == row['selected_components_sha256'], entity_id
        selected.append(dict(entity_id=entity_id, path=row['path'], source=row['source'],
            raw_sha256=row['raw_sha256'], components_sha256=row['selected_components_sha256'],
            shared=components['FightStatusShareStackComponent'],
            status=components['FightStatusComponent']))
    rules_path = ROOT / 'unity/Assets/Official/Combat/rules.json'
    limits = json.loads(rules_path.read_bytes())['game_settings']['Battle']['ShareStatusMaxStacks']
    assert limits == {'24029': 10, '25300': 5}
    code = ROOT / '.tools/source-packages/official-code/common-source/Common'
    output = dict(manifest='884_156692', entity_evidence=identity(proof_path),
        configuration_inputs=[identity(entities_path), identity(rules_path)], entities=selected,
        limits=limits, original_implementations=[identity(code / (name + '.cs')) for name in (
            'FightStatusAgentComponentImplement', 'FightStatusComponentImplement',
            'FightStatusShareStackComponent', 'FightStatusShareStackComponentInfo')],
        bindings=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Official/Combat/FightStatusSharedStacks.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.SharedStacks.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightStatusLifecycle.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.MissingStatusComponents.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightStatusLogicRuntime.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightStatusTriggerRuntime.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightHealingStackReduction.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightConditionalHitProperties.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightConditionalApplyProperties.cs')],
        generator=identity(Path(__file__)),
        offline_decision='Confirmed: allocate first application from remaining shared quota; partial allocation allowed; no new status at zero quota; release occupancy on end.',
        validation=[identity(ROOT / 'scripts/fixtures/battle_shared_stack_harness.cs'),
            identity(ROOT / 'scripts/test_battle_shared_stacks.ps1')])
    destination = ROOT / 'assets/reference/battle-rules/shared-stack-source-manifest.json'
    destination.write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified seven original shared statuses and both original pool limits.')

if __name__ == '__main__':
    main()
