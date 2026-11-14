"""Record original status notifications and local occurrence-time bindings."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    raw = (subprocess.check_output(['git', 'show', ':' + path], cwd=ROOT)
           if '--index' in sys.argv and not path.startswith('.tools/') else (ROOT / path).read_bytes())
    return dict(path=path, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())


def main():
    code = '.tools/source-packages/current/884_156692/official-code/'
    runtime = 'unity/Assets/Game/Runtime/'
    receipt = dict(code_manifest='884_156692', configuration_manifest='854_156383',
        original=[identity(code + path) for path in (
            'common-source/Common/FightStatusSkillModifyCDComponent.cs',
            'common-source/Common/FightStatusAgentComponent.cs',
            'common-source/Common/StatusStatusApplyConditionComponent.cs',
            'common-source/Common/EStatusCustomAction.cs',
            'client-source/MyFightStatusSkillModifyCD.cs',
            'client-source/CharacterDamageUIManager.cs')],
        configuration=[identity('unity/Assets/Official/Combat/' + name) for name in (
            'skill-rules.json', 'skill-rules-source-manifest.json',
            'rules.json', 'rules-source-manifest.json')],
        migration=[identity(runtime + path) for path in (
            'Official/Combat/FightBattleState.cs', 'Official/Combat/FightStatusCooldownRuntime.cs',
            'Official/Combat/FightBattleExecutor.cs', 'Offline/Combat/FightBattleExecutor.StatusStartup.cs',
            'Offline/Combat/FightBattleExecutor.StatusActionEvents.cs',
            'Offline/Combat/FightBattleExecutor.GridItems.cs', 'Offline/Combat/FightStatusApplyCondition.cs',
            'Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
            'Presentation/Combat/OfficialBattleSkillEntityPlayback.DamageUI.cs',
            'Presentation/Combat/OfficialBattleDamageUIPlayback.cs')],
        assets=[identity('assets/reference/' + name) for name in (
            'battle-animation/damage-view-runtime-source-manifest.json',
            'battle-hud/damage-ui-runtime-source-manifest.json', 'battle-hud/damage-ui-native-source-manifest.json')],
        validation=[identity(path) for path in (
            'scripts/test_battle_status_action_timing.ps1', 'scripts/test_battle_cooldown_status.ps1',
            'scripts/test_battle_status_immunity.ps1', 'scripts/fixtures/battle_status_immunity_harness.cs',
            'scripts/test_battle_skill_graph_dispatch.ps1', 'scripts/fixtures/battle_skill_graph_dispatch_harness.cs',
            'scripts/prepare_battle_damage_playback_validation.py',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleStatusActionTimingCases.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleDamageReceiptAcceptance.cs')],
        generator=identity('scripts/record_battle_status_action_timing_sources.py'),
        binding='Original status custom action -> step-scoped occurrence receipt -> existing native graph clock -> CharacterDamageUIManager',
        operation='Record each local cooldown invocation and in-step immunity refusal at its existing presentation clock, '
            'retaining the owning cast until approach/replay/trigger offsets are finalized. '
            'Prefer an unambiguous direct damage association; otherwise use an explicitly recorded finite occurrence delay. '
            'Keep old pending counters and receipts without timing evidence on the existing compatible path. '
            'No gameplay cooldown arithmetic, RNG, status acceptance, source prefab, animation, material or texture is changed.')
    path = ROOT / 'assets/reference/battle-animation/status-action-timing-source-manifest.json'
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Recorded status occurrence source bindings.')


if __name__ == '__main__':
    main()
