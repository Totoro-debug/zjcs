"""Record the recovered distinction between original display damage and actual HP changes."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build(source_root=ROOT, output_root=ROOT):
    official = []
    files = ['client-source/CharacterDamageUIManager.cs', 'client-source/CharacterDamageUI.cs',
             'common-source/Common/DamageResultParameter.cs',
             'common-source/Common/FightDamageComponentImplement.cs']
    for version, directory in (
        ('854_156383', source_root/'.tools/source-packages/official-code'),
        ('871_156598', source_root/'.tools/source-packages/current/871_156598/official-code')):
        manager = (directory/files[0]).read_text('utf8')
        result = (directory/files[2]).read_text('utf8')
        settlement = (directory/files[3]).read_text('utf8')
        if ('damageResult.DamageValue + -damageResult.ShieldValue' not in manager
                or 'DamageValue - ShieldValue' not in result
                or 'realDamage: num - hp, hp: num' not in settlement):
            raise ValueError('Original display/settlement source no longer matches: '+version)
        official.append({'manifest': version, 'files': [
            {'path': (directory/name).relative_to(source_root).as_posix(),
             'sha256': sha(directory/name)} for name in files]})
    runtime = ['unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
               'unity/Assets/Game/Runtime/Official/Combat/FightBattleStatistics.cs',
               'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
               'unity/Assets/Game/Editor/Combat/OfficialBattleDamageDisplayAcceptance.cs',
               'unity/Assets/Game/Editor/Combat/OfficialBattleHudAssetTools.cs',
               'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs',
               'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs',
               'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudAnimationPlayer.cs',
               'unity/Assets/Game/Editor/Combat/OfficialBattleWorldDamageAssetTools.cs',
               'scripts/test_battle_damage_display.ps1']
    receipt = {
        'official_code': official,
        'original_prefabs': {
            'manifest': '854_156383',
            'source_path': 'assets/reference/battle-hud/source-manifest.json',
            'source_sha256': sha(source_root/'assets/reference/battle-hud/source-manifest.json'),
            'runtime_path': 'unity/Assets/Official/Combat/Numbers/Resources/Combat/Numbers/world-damage.json',
            'runtime_sha256': sha(output_root/'unity/Assets/Official/Combat/Numbers/Resources/Combat/Numbers/world-damage.json'),
            'world_source_path': 'assets/reference/battle-hud/world-damage-source-manifest.json',
            'world_source_sha256': sha(output_root/'assets/reference/battle-hud/world-damage-source-manifest.json')},
        'generator_sha256': sha(__file__),
        'runtime': [{'path': name, 'sha256': sha(output_root/name)} for name in runtime],
        'operation': 'Preserve the original DamageResultParameter.DamageValue separately from RealDamageValue. CharacterDamageUIManager displays DamageValue minus ShieldValue, including full shield absorption, overkill and overhealing; actual HP changes remain the basis of local statistics and magic-mark contribution. Reuse the original number prefabs and animation/format/color paths.',
        'legacy_checkpoints': 'Absent HasDamageValue retains the previously saved actual HP change and available shield amount. Historical overkill/overheal cannot be reconstructed and is not fabricated.',
        'remaining': 'Full zero-value/hide-damage filters, status damage distinctions and complete per-event timing remain incomplete. The separate element-feedback-source-manifest.json records the restored element labels and animations.'}
    target = output_root/'assets/reference/battle-hud/damage-display-source-manifest.json'
    target.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2)+'\n').encode())
    print('Verified original display damage, shield subtraction and actual HP settlement in both code versions.')


if __name__ == '__main__':
    build()
