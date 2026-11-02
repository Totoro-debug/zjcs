"""Record original part-notification consumption by the native scene animator."""
import json
from pathlib import Path
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed


def main():
    outputs = []
    for name in ('unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.Animator.cs',
                 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneMecanimParts.cs'):
        path = ROOT / name
        meta(path)
        outputs.extend((identity(path), identity(path.with_name(path.name + '.meta'))))
    source_root = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    prior = json.loads((ROOT / 'assets/reference/battle-animation/renderer-parts-source-manifest.json').read_bytes())
    assert prior['manifest'] == '884_156692'
    originals = {row['path']: row for row in prior['official_application_sources']}
    migrated = {row['path']: row for key in ('migrated_application_and_ports', 'existing_part_dependencies') for row in prior[key]}
    application = []
    for name in ('AppearanceMecanimPartHandler.cs', 'AppearanceSkeletonPart.cs'):
        original = identity(source_root / name)
        runtime = identity(ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / name)
        assert original == originals[original['path']], 'Recheck changed original part application source'
        assert runtime == migrated[runtime['path']], 'Recheck changed migrated part application source'
        application.append(dict(original=original, runtime=runtime))
    receipts = ('renderer-parts', 'scene-native-renderer', 'scene-player-animator', 'native-animator-pose')
    proof = dict(manifest='884_156692', application=application, outputs=outputs,
                 dependencies=[identity(ROOT / ('assets/reference/battle-animation/' + name + '-source-manifest.json')) for name in receipts],
                 generator=identity(Path(__file__).resolve()),
                 binding='Explicit native pose producer; UpdateAnimationAndMesh and LateUpdate consume the actual Animator before drawing. Native animation identities, original motion names and actual current/previous input times cross the existing original Mecanim part interface.',
                 limits=['Only the existing unmixed single-layer pose domain is accepted. Rejected transitions emit no part notification.',
                         'The actual Play Mode fixture explicitly chooses EvaluateBeforePhysics; independent weapon poses still use an explicit diagnostic producer. Full physics, mixing, culling/update timing and production battle/Player integration remain pending.',
                         'The original current initial weapon config has only map source mappings, so Game idle/hit names do not request a new independent weapon motion. No extra mappings or art are created.',
                         'The validation observes and forwards the actual original part interface; callback scalar arguments are native Animator inputs, not a claim of full SDK callback parity. No SDK implementation is used or copied.'])
    write_changed(ROOT / 'assets/reference/battle-animation/scene-mecanim-parts-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded native scene Animator binding and original Mecanim part notification consumption.')


if __name__ == '__main__':
    main()
