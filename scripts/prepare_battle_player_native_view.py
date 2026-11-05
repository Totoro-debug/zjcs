"""Record the source-backed native player model view and its lifecycle binding."""
from hashlib import sha256
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = 'unity/Assets/Game/Runtime/'
EDITOR = 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/'
PROOFS = 'assets/reference/battle-animation/'


def identity(name):
    return dict(path=name, sha256=sha256((ROOT / name).read_bytes()).hexdigest())


def main():
    source = '.tools/source-packages/current/884_156692/'
    archive = source + 'client-decompilation-manifest.json'
    recorded = {row['path']: row['sha256'] for project in json.loads((ROOT / archive).read_bytes())['projects']
                for row in project['files']}
    original = source + 'official-code/client-source/ECFightRoleCharacterView.cs'
    assert identity(original)['sha256'] == recorded[original]
    runtime = [RUNTIME + name + '.cs' for name in (
        'Presentation/Combat/OfficialBattlePlayerNativeModelView',
        'Offline/ProfessionUI/ProfessionNativeBattlePlayerActor',
        'Offline/ProfessionUI/ProfessionNativeBattlePlayerAttachments')]
    validation = [EDITOR + 'OfficialBattlePlayerModelClipValidation' + suffix + '.cs'
                  for suffix in ('', '.PlayerActor', '.NativeViews')]
    inputs = [PROOFS + name + '-source-manifest.json' for name in (
        'player-resource-service', 'player-attachments', 'character-death-assets', 'move-animators')]
    inputs.append('assets/reference/battle-scenes/character-shadow-source-manifest.json')
    proof = dict(manifest='884_156692', archive=identity(archive), original=identity(original),
        inputs=[identity(p) for p in inputs], runtime=[identity(p) for p in runtime],
        validation=[identity(p) for p in validation],
        metadata=[identity(p + '.meta') for p in runtime + validation],
        generator=identity(Path(__file__).relative_to(ROOT).as_posix()),
        binding='OfficialBattlePlayerNativeModelView',
        operation='Own a resource-service player lease; expose the original 3100 root and nested Animator. '
            'Create a common native body/weapon ordering boundary before appearance initialization, then bind its scene order. '
            'Replace the attachment-only shadow placeholder with the exact original shadow subtree and update attachment identities. '
            'Run extracted movement cues and original ordinary/on-start death graphs, refresh appearance, revive and dispose. '
            'Unity owns Animator and native physics advancement.',
        limits=['Formal shared scene/controller and host entry remain separate.',
            'This is a model-view adapter, not a complete port of ECFightRoleCharacterView generation, fade or HUD.',
            'Validation uses local profile projections; UI input, disk-save flow and Windows Player remain separate.'])
    target = ROOT / PROOFS / 'player-native-view-source-manifest.json'
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()
