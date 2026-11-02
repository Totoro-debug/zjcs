"""Record current player pose inputs and independent native binding identities."""
import argparse
import hashlib
import json
from pathlib import Path
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    path = ROOT / path
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--observation-receipt', type=Path, required=True)
    args = parser.parse_args()
    receipt = args.observation_receipt.resolve(strict=True)
    if validation_directory() not in receipt.parents:
        raise ValueError('Use this owned validation invocation\'s numerical observations')
    observed = json.loads(receipt.read_bytes())
    if observed['fixture_sha256'] != identity('scripts/fixtures/partner_player_pose_reference.cs')['sha256']:
        raise ValueError('Reference capture helper differs from the observed invocation')
    curve_fixture = identity('scripts/fixtures/partner_player_bezier_reference.txt')
    if observed['bezier_observations'] != dict(cases=4, samples=244, sha256=curve_fixture['sha256']):
        raise ValueError('Recorded curve observations differ')
    dependencies = [
        'assets/reference/battle-animation/scene-core-source-manifest.json',
        'assets/reference/partner-ui-appearance-source-manifest.json',
        'assets/reference/partner-player-physics-source-manifest.json',
        'assets/reference/battle-animation/constraint-pose-source-manifest.json',
        'assets/reference/battle-animation/physics-channels-source-manifest.json']
    scene = json.loads((ROOT / dependencies[0]).read_bytes())
    appearance = json.loads((ROOT / dependencies[1]).read_bytes())
    physics = json.loads((ROOT / dependencies[2]).read_bytes())
    if any(value['manifest'] != '884_156692' for value in (scene, appearance, physics)):
        raise ValueError('Use selected current player inputs without source substitution')
    runtime = 'unity/Assets/Game/Runtime/Offline/ProfessionUI/'
    names = ['ProfessionNativePlayerPhysicsPose', 'ProfessionNativeSpringConstraint',
             'ProfessionNativeUiCharacterPose', 'ProfessionNativeConstraintPose',
             'ProfessionNativeConstraintAnimation', 'ProfessionNativePhysicsAnimation',
             'ProfessionNativeAppearanceSkeleton', 'ProfessionNativeBonePose', 'ProfessionNativeAnimationChannels',
             'ProfessionNativeCurve', 'ProfessionNativeBezierPolyline', 'ProfessionNativeDeformAnimation']
    outputs = [runtime + name + suffix for name in names for suffix in ('.cs', '.cs.meta')]
    helpers = ['scripts/fixtures/partner_main_avatar_validation.cs',
        'scripts/prepare_partner_main_avatar_validation.py',
        'scripts/fixtures/partner_player_pose_reference.cs',
        'scripts/verify_partner_player_pose_reference.py', 'scripts/test_partner_player_bezier.py',
        'scripts/fixtures/partner_player_bezier_validation.cs', 'scripts/fixtures/partner_player_bezier_reference.txt']
    result = dict(manifest='884_156692', dependencies=[identity(path) for path in dependencies],
        original_inputs=[identity('unity/Assets/Official/Combat/SceneCharacterSources/' + sex + '.bytes')
                         for sex in ('male', 'female')],
        outputs=[identity(path) for path in outputs], helpers=[identity(path) for path in helpers],
        generator=identity('scripts/record_partner_player_pose.py'),
        recorded_numerical_inputs=dict(reference_binary_sha256=observed['reference_sha256'],
            fixture=curve_fixture, cases=4, samples=244,
            selected_original_curve='male / map/idle_01 / shenti_0 / rotate, first two authored keys',
            other_inputs='Three synthetic control-point configurations used only to distinguish numerical sampling semantics'),
        operation=[
            'Use original unscaled local bone coordinates, setup bone lengths and skeleton referenceScale. Mesh conversion retains the original dataScale.',
            'Sample original skin-selected constraints and animation channels. Solve verified IK/transform stages, then update active x/y/rotate physics in original order and invalidate descendant world matrices.',
            'Retain physical histories and skeleton clock through animation replay and skin changes. Newly created skeletons own new histories. Initially inactive constraints bind on their first active update.',
            'Explicit setup-only samplers apply original setup constraints before any animation track exists. No arbitrary animation substitutes for setup.',
            'Independently construct ten equal-parameter planar Bezier chords for the current UI producer, including local, color, constraint, physics and deform curves. Other callers retain their explicitly selected existing curve policy.',
            'The optional JSON reference transport restores omitted transform setup mix fields to their original binary zero defaults; packaged source payloads remain byte-identical.',
            'Unknown constraint kinds, interleaved physics ordering and unsupported scale/shear physics are rejected.',
            'The game and native producer have no animation SDK dependency. The optional reference helper invokes public APIs on an explicitly supplied existing binary in an owned validation workspace.',
            'Earlier receipts keep their historical implementation identities; this receipt records the current extensions without modifying their original input hashes.'])
    target = ROOT / 'assets/reference/partner-player-pose-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded current player native pose and ordered physics bindings.')


if __name__ == '__main__':
    main()
