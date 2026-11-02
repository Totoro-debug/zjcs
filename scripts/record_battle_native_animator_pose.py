"""Record the independent Unity Animator to native unmixed-pose adapter."""
import json
from pathlib import Path
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed


def main():
    outputs = []
    for name in ('unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimatorPose.cs',
                 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneAnimatorPose.cs'):
        path = ROOT / name
        meta(path)
        outputs.extend((identity(path), identity(path.with_name(path.name + '.meta'))))
    outputs.append(identity(ROOT / 'unity/Assets/Game/Editor/Map13Mainline/Zjcs.Editor.Map13Mainline.asmdef'))
    receipts = ['scene-core', 'game-animators', 'constraint-pose', 'scene-player-animator', 'mecanim-cache-scope']
    proof = dict(manifest='884_156692', outputs=outputs,
                 dependencies=[identity(ROOT / ('assets/reference/battle-animation/' + name + '-source-manifest.json')) for name in receipts],
                 generator=identity(Path(__file__).resolve()),
                 documentation=['https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AnimatorStateInfo-normalizedTime.html',
                                'https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.GetCurrentAnimatorClipInfo.html'],
                 binding='Explicit Animator and pose solver. Native original motion names and clip time are consumed without a second playback clock; source changes invalidate native channel bindings.',
                 limits=['Only one full-weight layer and motion without transitions. Mixing is explicitly rejected before pose mutation.',
                         'The validation binds EvaluateBeforePhysics, not a complete physics solver. Production battle/prefab integration remains pending.',
                         'No SDK source is read or copied. No original cache is substituted for Game animation.'])
    write_changed(ROOT / 'assets/reference/battle-animation/native-animator-pose-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded actual Animator to native unmixed pose binding and its explicit solver boundary.')


if __name__ == '__main__':
    main()
