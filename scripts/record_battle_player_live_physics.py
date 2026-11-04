"""Record independent player lifecycle binding against verified prefab data.

No third-party SDK implementation or binary is read by this recorder.
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
    prior = json.loads((directory / 'player-model-prefab-source-manifest.json').read_bytes())
    hierarchy = prior['hierarchy']
    assert identity(ROOT / hierarchy['path']) == hierarchy
    data = json.loads((ROOT / hierarchy['path']).read_bytes())
    component = next(c for c in data['nodes'][0]['components'] if c['type'] == 'Spine.Unity.SkeletonMecanim')
    fields = component['fields']
    assert fields['updateTiming'] == 1 and fields['updateWhenInvisible'] == 0
    assert fields['physicsPositionInheritanceFactor'] == dict(x=0.0, y=0.0)
    assert fields['physicsRotationInheritanceFactor'] == 0
    assert fields['physicsMovementRelativeTo'] == dict(m_FileID=0, m_PathID=0)
    assert fields['clearStateOnDisable'] == 0
    assert fields['translator'] == dict(autoReset=1, useCustomMixMode=1, layerMixModes=[1], layerBlendModes=[1])
    paths = [
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeScenePlayerPlayback.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.Animator.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRendererSettings.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.LivePhysics.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterAnimationController.cs',
    ]
    result = dict(
        manifest=prior['manifest'], hierarchy=hierarchy, original_component=component,
        dependencies=[identity(directory / (name + '-source-manifest.json')) for name in
                      ('player-model-prefab', 'player-mixed-physics', 'scene-bound-mix-next')],
        outputs=[identity(ROOT / (path + suffix)) for path in paths for suffix in ('', '.meta')],
        generator=identity(Path(__file__).resolve()),
        binding='Explicit BindPlayerAnimator validates the original player Mecanim settings. Actual renderer LateUpdate consumes the already evaluated Animator and advances its skeleton physics once by scaled frame delta. Explicit pose/mesh redraw uses Physics.Pose with zero elapsed time. The same native physics owner survives motion restart, transitions, renderer disable and inactive pooling. Rebuilt skeletons bind fresh histories. Invisible UpdateMode zero skips advancement. Original zero body movement inheritance is retained.',
        limits=[
            'An explicit independent native engine lifecycle adapter; not a claim of every SDK internal timing or callback scalar convention.',
            'Formal player factory, playable graph integration, all skills, interrupted transitions, alternate timing/mixing/inheritance settings and Windows Player remain separate.',
            'The rendered probe covers both sexes and warrior/mage controllers, actual LateUpdate, mixed hit, death/revival, repeated/reentrant redraw, disable, inactive pooling and real visibility culling. It uses no UI input or user save.',
            'Shared edit-mode validation explicitly initializes the original monster application controller because automatic MonoBehaviour Awake is unavailable there; playing mode retains the original activation lifecycle.',
        ])
    target = directory / 'player-live-physics-source-manifest.json'
    target.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print('Recorded original player settings and independent live physics binding.')


if __name__ == '__main__':
    main()
