"""Record native scene MixNext drawing and original application notification seams.

Uses game application code, original prefab data and public documentation only.
No third-party animation SDK implementation or old npm runtime is inspected.
"""
import json
from pathlib import Path
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed


def main():
    names = [
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimatorPose.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.Animator.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneBoundMixNext.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAnimator.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.cs',
    ]
    outputs = []
    for name in names:
        path = ROOT / name
        if name.endswith('.SceneBoundMixNext.cs'):
            meta(path)
        outputs.extend((identity(path), identity(path.with_name(path.name + '.meta'))))
    previous = json.loads((ROOT / 'assets/reference/battle-animation/scene-mecanim-parts-source-manifest.json').read_bytes())
    application = previous['application']
    for pair in application:
        for row in pair.values():
            assert identity(ROOT / row['path']) == row, 'Recheck changed original application input'
    proof = dict(
        manifest='884_156692', application=application,
        inputs=[identity(ROOT / 'unity/Assets/Official/Combat/SceneCharacterSources/PlayerModel_Game-source.json')],
        dependencies=[identity(ROOT / ('assets/reference/battle-animation/' + name + '-source-manifest.json'))
                      for name in ('native-mix-next', 'scene-mecanim-parts', 'scene-player-animator')],
        outputs=outputs, generator=identity(Path(__file__).resolve()),
        documentation=['https://en.esotericsoftware.com/spine-unity-main-components-4.2#Animation-Blending-Control'],
        binding='One explicit pose producer/drawing entry consumes actual unmixed or MixNext Animator inputs. A completed mixed local/constraint pose emits outgoing native identity at full weight followed by the nonzero incoming identity at its actual weight. Input-time history is retained separately for each native motion. Both UpdateAnimationAndMesh and actual renderer LateUpdate use this entry. The original application handler retains its own identity guard and forwards unchanged original names to actual parts.',
        limits=[
            'The fixture explicitly binds pre-physics solvers. Full physical motion, other mix modes/layers, blend trees, interrupted transitions, production battle hosts and Windows Player integration remain pending.',
            'Callback scalar values describe this independent native interface and actual Animator inputs. Public blending documentation and original application consumption do not establish parity with every internal SDK callback timing or scalar convention.',
            'Original Game idle/hit names have no mapping in the initial independent weapon animation dictionary. The original ignore branch is preserved; no weapon mapping, motion or artwork is invented.',
            'Actual current four controllers and eight characters cover source/target event order, identity/history, original handler lifecycle and real GPU body rendering. The full physics/culling/update scheduling domain remains unverified.',
            'Earlier receipts preserve their extraction stages and narrower entry-point scope. This receipt records the subsequent unified drawing and transition notification binding without modifying original payloads.',
        ])
    write_changed(ROOT / 'assets/reference/battle-animation/scene-bound-mix-next-source-manifest.json',
                  json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded unified native MixNext drawing and original application notification consumption.')


if __name__ == '__main__':
    main()
