"""Record selected player physics inputs and independent implementation hashes."""
import argparse
import hashlib
import json
from pathlib import Path

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    path = Path(path)
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--observation-receipt', required=True, type=Path)
    args = parser.parse_args()
    receipt = args.observation_receipt.resolve(strict=True)
    if validation_directory() not in receipt.parents:
        raise ValueError('Use this invocation\'s owned observation directory.')
    observed = json.loads(receipt.read_bytes())
    binary = Path(observed['path'])
    assert identity(binary)['sha256'] == observed['sha256']
    fixture = ROOT / 'scripts/fixtures/partner_player_physics_reference.txt'
    assert identity(fixture)['sha256'] == observed['output_sha256']
    assert observed['cases'] == 34 and observed['frames'] == 4080
    for sex, count in (('male', 1543), ('female', 1807)):
        row = observed['player_assets'][sex]
        assert row['constraints'] == count and row['checks'] == count * 120
        assert row['source'] == identity(ROOT / row['source']['path'])
    outputs = [ROOT / path for path in (
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSpringConstraint.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSpringConstraint.cs.meta',
        'scripts/test_partner_player_physics.py',
        'scripts/capture_partner_player_physics_reference.py',
        'scripts/fixtures/partner_player_physics_reference_capture.cs',
        'scripts/fixtures/partner_player_physics_asset_reference.cs',
        'scripts/fixtures/partner_player_physics_validation.cs',
        'scripts/fixtures/partner_player_physics_reference.txt')]
    result = dict(manifest='884_156692',
        original_player_data=identity(ROOT / 'assets/reference/battle-animation/scene-core-source-manifest.json'),
        reference_binary=identity(binary),
        recorded_observations=dict(cases=observed['cases'], frames=observed['frames'], fixture=identity(fixture),
            player_assets=observed['player_assets']),
        outputs=[identity(path) for path in outputs], generator=identity(Path(__file__)),
        operation=[
            'Independently implement planar spring and affine mathematics from asset parameters; no SDK implementation is copied, translated or shipped by this module.',
            'The optional observation tool calls only public APIs on an explicitly supplied existing binary, in an owned temporary directory. Numerical observations are separate from implementation source.',
            'The game, native conversion and recorded-observation regression test have no reference-runtime dependency.',
            'C rows bind an initial world pose and clock; D rows record an initially inactive constraint whose first active pose is bound before subsequent updates.',
            'Reference asset checks supply each individual constraint input matrix; they do not verify the independent hierarchy, pose controller, rendered animation or Windows Player.'
        ], supported_channels=['x', 'y', 'rotate'],
        host_responsibilities=['source constraint order', 'skin activation', 'initial pose and skeleton clock',
            'pose sampling', 'replay and skin lifecycle', 'reject unimplemented scaleX/shearX channels'])
    target = ROOT / 'assets/reference/partner-player-physics-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Recorded selected player physics sources and independent implementation hashes.')


if __name__ == '__main__':
    main()
