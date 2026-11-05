"""Record the native player scene dispatch and explicit local profile binding."""
from hashlib import sha256
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = 'unity/Assets/Game/Runtime/Presentation/Combat/'
EDITOR = 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/'
PROOFS = 'assets/reference/battle-animation/'


def identity(name, root=ROOT):
    return dict(path=name, sha256=sha256((root / name).read_bytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime-root', type=Path, default=ROOT,
                        help='Source tree containing the exact integration being validated/committed.')
    args = parser.parse_args()
    runtime = [RUNTIME + name + '.cs' for name in (
        'IOfficialBattleAnimatorMovementModel', 'OfficialBattlePlayerAppearanceSource',
        'OfficialBattleSceneView.Players', 'OfficialBattleSceneView', 'OfficialBattleRequest',
        'OfficialBattleRuntimeController', 'OfficialBattlePlayerNativeModelView',
        'OfficialBattlePartnerNativeModelView')]
    validation = [EDITOR + 'OfficialBattlePlayerModelClipValidation' + suffix + '.cs'
                  for suffix in ('', '.PlayerActor', '.SceneViews')]
    proof = dict(manifest='884_156692',
        inputs=[identity(PROOFS + name + '-source-manifest.json') for name in (
            'player-native-view', 'move-animators', 'skill-native-graph-assets')],
        runtime=[identity(p, args.runtime_root) for p in runtime],
        validation=[identity(p, args.runtime_root) for p in validation],
        metadata=[identity(p + '.meta', args.runtime_root) for p in runtime + validation],
        generator=identity(Path(__file__).relative_to(ROOT).as_posix()),
        binding='OfficialBattleRequest.PlayerAppearance -> OfficialBattleSceneView.TryCreatePlayer',
        operation='Bind an explicitly selected player unit to a read-only profile and item-type lookup. '
            'The shared scene creates and owns its original native model/resources, including when restoring zero HP. '
            'Dispatch extracted movement leaves through the original player/partner controller interface; '
            'complete native cast graphs retain exclusive ownership of their Animator leaves. '
            'Shared HP refresh starts death, retains the role and revives it; dispose roles before shared player resources.',
        limits=['Host modes must still provide the profile binding and stop attaching borrowed map avatars.',
            'The model view does not complete original player HUD, fade or every skill/status graph.',
            'Validation uses the shared scene with local profile projections; UI input, disk saves and Windows Player remain separate.'])
    target = ROOT / PROOFS / 'player-scene-dispatch-source-manifest.json'
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()
