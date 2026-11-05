"""Record original stack-threshold definitions and their shared execution bindings."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    path = ROOT / path
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    rules = 'unity/Assets/Official/Combat/battle-rules.json'
    source = json.loads((ROOT / rules).read_bytes())
    entries = [dict(entity_id=int(key), component=value['StatusStackCountComponent'])
        for key, value in source['entities'].items() if 'StatusStackCountComponent' in value]
    code = '.tools/source-packages/official-code/common-source/Common/'
    proof = dict(generator=identity('scripts/prepare_battle_stack_count_runtime.py'),
        original_code=[identity(code + name + '.cs') for name in
            ('StatusStackCountComponentInfo', 'StatusStackCountComponent', 'FightStatusTriggerSkillCfg', 'SkillTargetType', 'StatusTriggerTarget', 'EStatusTriggerTargetPos')],
        inputs=[identity(rules), identity('unity/Assets/Official/Combat/battle-rules-source-manifest.json'),
            identity('unity/Assets/Official/Combat/skill-rules.json'),
            identity('unity/Assets/Official/Combat/skill-rules-source-manifest.json')],
        definitions=entries,
        bindings=[identity(path) for path in (
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightStatusLifecycle.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightStatusTriggerRuntime.StackCount.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.StackCount.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.StatusStartup.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.HpStartup.cs',
            'scripts/fixtures/battle_stack_count_harness.cs',
            'scripts/test_battle_stack_count.ps1',
            'unity/Assets/Game/Editor/OfficialBattleStackCountHostAcceptance.cs')],
        operation='Reuse verified original layer threshold, quota, removal and trigger skill definitions. '
            'Route supported applicator/creator skill effects through existing configured status skill execution. '
            'Preserve saved quotas and restore missing legacy definitions. '
            'AI/TriggerTarget, property target, custom damage and stack-damage branches remain unsupported.')
    target = ROOT / 'assets/reference/battle-rules/stack-count-source-manifest.json'
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode())
    print('Recorded', len(entries), 'original stack-count definitions')


if __name__ == '__main__':
    build()
