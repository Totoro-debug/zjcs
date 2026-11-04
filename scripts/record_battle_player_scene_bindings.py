"""Record original Game-player application/data inputs and independent wiring.

Reads no third-party animation SDK implementation.
"""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    path = Path(path)
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    directory = ROOT / 'assets/reference/battle-animation'
    prior = json.loads((directory / 'scene-player-animator-source-manifest.json').read_bytes())
    applications = []
    for row in prior['application']:
        assert identity(ROOT / row['path']) == row
        applications.append(row)
    model = json.loads((directory / 'player-model-prefab-source-manifest.json').read_bytes())
    for relative in ('AppearanceSystem.Runtime/AppearanceMecanim.cs',
                     'AppearanceSystem/PlayerAppearance.cs', 'ECFightRoleCharacterView.cs'):
        applications.append(identity(ROOT / '.tools/source-packages/current/884_156692/official-code/client-source' / relative))
    assert identity(ROOT / model['hierarchy']['path']) == model['hierarchy']
    assert identity(ROOT / prior['assembly']['path']) == prior['assembly']
    paths = [
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionScenePlayerBindings.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.PlayerAppearance.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeScenePlayerPlayback.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.PlayerBindings.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.cs',
    ]
    controllers = sorted((ROOT / 'unity/Assets/Official/Combat/GameAnimators/Resources/Combat/GameAnimators/Anim').glob('*Controller.*'))
    controllers = [path for path in controllers if path.suffix in ('.controller', '.overrideController')]
    assert len(controllers) == 4
    result = dict(
        manifest=prior['manifest'], assembly=prior['assembly'], application=applications,
        dependencies=[identity(directory / (name + '-source-manifest.json')) for name in
                      ('player-model-prefab', 'scene-player-animator', 'game-animators', 'player-live-physics')],
        hierarchy=model['hierarchy'],
        controllers=[identity(path) for path in controllers],
        outputs=[identity(ROOT / (path + suffix)) for path in paths for suffix in ('', '.meta')],
        generator=identity(Path(__file__).resolve()),
        binding='Bind the inactive original PlayerModel_Game graph and its serialized skin/loader, renderer, part slot and sorting references. Reuse original PlayerAnimatorAppearance, saved-player projection and character/part refresh. Bind physics once on first skin setup; an unevaluated initial Animator uses completed setup constraints with zero elapsed time. The original RefreshAppearance then evaluates Animator.Update(0); subsequent real LateUpdate samples it. Repeated appearance events and disable/pool refresh retain the same physical owner.',
        limits=[
            'An injectable production binding for the existing Game model; the main battle host still must supply its local-player/material/effect/pool services and the original outer 3100 entity hierarchy.',
            'The rendered lifecycle fixture uses real original Game prefab/controllers and an isolated local-player profile. Its effect service rejects optional external appearance effects; this is not coverage of every wardrobe/effect/skill.',
            'No UI input, disk save, complete original ECS character view, full battle-HP event routing or Windows Player delivery is claimed.',
        ])
    (directory / 'player-scene-bindings-source-manifest.json').write_bytes(
        (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print('Recorded original Game player graph, controllers and independent saved-player binding.')


if __name__ == '__main__':
    main()
