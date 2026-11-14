"""Restore the original 25035 entity and pin the confirmed reflection adapter inputs."""
import argparse
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_status_damage_sources import CODE, COMBAT, code_identity, identity
from prepare_battle_status_orbit_assets import build


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--record-only', action='store_true')
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    if not args.record_only and not args.verify:
        build(DEST=ROOT / 'unity/Assets/Official/Combat/AdditionalStatuses/Resources/Combat/AdditionalStatuses',
            DATA=ROOT / 'unity/Assets/Game/Resources/Combat/additional-statuses.json',
            RECEIPT=ROOT / 'assets/reference/battle-animation/additional-status-assets-source-manifest.json',
            ROOTS={}, graph_roots={25035: 'Prefabs/EcEntity/Fight/FightSkill/25035/25035.prefab'},
            MAPPED={1200213464: ('EntityLifecycle', 'MyEntity'), -109006141: ('EntityActions', 'MyAction'),
                -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                -812331650: ('BattleGridItemEntities', 'MyFightStatusLogic')},
            family='AdditionalStatuses', shader_prefix='AdditionalStatus_', generator=Path(__file__),
            limits=['25035 has no authored status, action, stack, invoke or master-cast playable reference. '
                    'Its visible reaction is the separately restored 11480 skill graph.'])
    files = ['FightHandleCfgStatusDamageByDamageResultValueAndStore',
        'FightHandleStatusDamageByDamageResultValueAndStore', 'FightHandleCfgTriggerSkillWithCustomDamageByStatus',
        'FightHandleTriggerSkillWithCustomDamageByStatus', 'DamageResultParameter',
        'DamageResultValueType', 'DamageResultTypeFilter', 'DamageResultValueTypeCompareFilter',
        'DamageShieldValueGreaterThanZeroFilter', 'DamageSkillCanTriggerStatusFilter']
    adapter = ROOT / 'unity/Assets/Game/Runtime/Offline/Combat/FightStatusLogicRuntime.ReflectionArmor.cs'
    record = dict(manifest='884_156692', official_code=[identity(CODE / (name + '.cs')) for name in files],
        runtime_inputs=[identity(COMBAT / (name + '.json')) for name in ('rules', 'skill-rules', 'battle-rules')],
        formula_proof=identity(ROOT / 'assets/reference/battle-rules/status-damage-source-manifest.json'),
        adapter=code_identity(adapter), generator=code_identity(Path(__file__)),
        validation=[code_identity(ROOT / p) for p in ('scripts/test_battle_reflection_armor.ps1',
            'scripts/fixtures/battle_reflection_armor_harness.cs')],
        native_entity=identity(ROOT / 'assets/reference/battle-animation/additional-status-assets-source-manifest.json'),
        reaction_graph_proof=identity(ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'),
        operation='Confirmed offline 25035 graph adapter. Successful damage result, actual shield absorption, '
            'living holder and original skill trigger permission gate each occurrence. Read original ShieldAddFinalDamageValue '
            '(signed pre-HP-clamp DamageValue minus positive ShieldValue), original status properties and StatusDamage formula; '
            'convert its negative HP delta to the normal damage formula custom-base magnitude. Holder casts 11480 at attacker '
            'with status cultivation, blessing, ancestry and occurrence delay; no persistent runtime board or random draw.')
    path = ROOT / 'assets/reference/battle-rules/reflection-armor-source-manifest.json'
    if args.verify:
        assert json.loads(path.read_bytes()) == record, 'Reflection armor source identities changed.'
        print('Original reflection armor source identities verified.')
        return
    path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Original reflection armor entity and source identities recorded.')


if __name__ == '__main__':
    main()
