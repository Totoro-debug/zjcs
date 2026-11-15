"""Record official target relation inputs and their shared local binding."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(relative):
    raw = (ROOT / relative).read_bytes().replace(b'\r\n', b'\n')
    return dict(path=relative, sha256_lf=hashlib.sha256(raw).hexdigest())


def main():
    original = '.tools/source-packages/current/884_156692/official-code/common-source/Common/'
    proof = dict(
        code_manifest='884_156692',
        original=[identity(original + name) for name in (
            'GameUtilsHandler.cs', 'HitTargetType.cs', 'GameCharacterType.cs')],
        configuration=[identity('unity/Assets/Official/Combat/' + name) for name in (
            'skill-rules.json', 'skill-rules-source-manifest.json')],
        migration=identity('unity/Assets/Game/Runtime/Official/Combat/FightBattleTargeting.cs'),
        validation=[identity(name) for name in (
            'scripts/fixtures/battle_target_types_harness.cs',
            'scripts/test_battle_target_types.ps1')],
        generator=identity('scripts/record_battle_target_types_sources.py'),
        binding='GameUtilsHandler.CheckTargetType -> FightBattleTargeting.MatchesTargetType -> '
            'Select / SelectInSkillRange / IsEligibleSkillTarget / SelectScopeTargets',
        operation='Port the official MySummon character-type guard and '
            'FriendPlayerAndNpcNotMe branch into the existing relation filter. '
            'Keep the existing offline confusion, lifecycle, range, AI ranking and '
            'explicit point-target None handling. No skill configuration or presentation assets changed.',
        hash_normalization='UTF-8 source bytes with CRLF replaced by LF')
    path = ROOT / 'assets/reference/battle-rules/target-types-source-manifest.json'
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
