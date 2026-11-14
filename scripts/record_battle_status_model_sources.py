"""Record original character-status lifecycle and native model rebinding."""
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
    code = '.tools/source-packages/current/884_156692/official-code/client-source/'
    runtime = 'unity/Assets/Game/Runtime/'
    value = dict(code_manifest='884_156692', configuration_manifest='854_156383',
        original=[identity(code + name + '.cs') for name in ('MyFightStatus', 'MyFightStatusAgent', 'MyAction', 'MyEntity')],
        assets=[identity('assets/reference/battle-animation/' + name + '-source-manifest.json') for name in (
            'first-promotion-status-assets', 'first-promotion-status-runtime', 'live-monsters', 'native-monster-appearance')],
        migration=[identity(runtime + path + '.cs') for path in (
            'Presentation/Combat/OfficialBattleStatusPlayback', 'Presentation/Combat/OfficialBattleSkillEntityPlayback',
            'Presentation/Combat/OfficialBattleSceneView', 'Presentation/Combat/OfficialBattleSceneView.UnitEnds',
            'Official/BattleStatusPlayables/MyFightStatus', 'Official/BattleStatusPlayables/MyFightStatusAgent',
            'Official/EntityActions/MyAction', 'Official/EntityLifecycle/MyEntity')],
        validation=[identity(path) for path in (
            'scripts/prepare_battle_damage_playback_validation.py',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleStatusModelAcceptance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef')],
        generator=identity('scripts/record_battle_status_model_sources.py'),
        binding='Original status End/Remove on its bound owner -> retained presentation snapshot -> original OnStart(false) on the current model',
        operation='Keep action owner lookup bound to the original model through removal; defer still-live status views while '
            'their owner model is absent and restore their latest layers, forbidden flag and persistent counters when it returns. '
            'Preserve original first attachment, pending update/end order and elapsed invocation behavior. '
            'No combat state, status lifetime, prefab, texture, skeleton, animation or material payload is changed.')
    target = ROOT / 'assets/reference/battle-animation/status-model-lifecycle-source-manifest.json'
    target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Recorded status/model lifecycle source bindings.')


if __name__ == '__main__':
    main()
