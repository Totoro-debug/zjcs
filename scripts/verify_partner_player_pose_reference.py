"""Compare owned native avatar traces with public calls on an explicit binary.

Optional numerical reference only; the game and free native conversion never
invoke this tool or load the reference binary.
"""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import shutil
import struct
import subprocess

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--reference-dll', type=Path, required=True)
    parser.add_argument('--record-bezier', action='store_true', help='Also record original and synthetic public-call curve observations')
    args = parser.parse_args()
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned workspace required')
    output = work / 'player-pose-reference'
    output.mkdir(exist_ok=True)
    binary = args.reference_dll.resolve(strict=True)
    copied = output / binary.name
    shutil.copy2(binary, copied)
    config = json.loads((ROOT / 'unity-tools.json').read_bytes())
    mono_root = Path(config['editorExecutable']).parent / 'Data/MonoBleedingEdge'
    mono, csc = mono_root / 'bin/mono.exe', mono_root / 'lib/mono/4.5/csc.exe'
    exe = output / 'PlayerPoseReference.exe'
    fixture = ROOT / 'scripts/fixtures/partner_player_pose_reference.cs'
    subprocess.run([str(mono), str(csc), '/nologo', '/r:' + str(copied),
        '/r:' + str(mono_root / 'lib/mono/4.8-api/Facades/netstandard.dll'),
        '/out:' + str(exe), str(fixture)], check=True)
    results = []
    curve_source = None
    for sex in ('male', 'female'):
        source = ROOT / ('unity/Assets/Official/Combat/SceneCharacterSources/' + sex + '.bytes')
        raw = source.read_bytes()
        stream = io.BytesIO(raw)
        assert stream.read(8) == b'ZJCSPROF' and struct.unpack('<I', stream.read(4))[0] == 1
        def text():
            return stream.read(struct.unpack('<I', stream.read(4))[0]).decode('utf8')
        assert text() == '884_156692' and text() == sex
        struct.unpack('<f', stream.read(4))
        size = struct.unpack('<I', stream.read(4))[0]
        digest = stream.read(32)
        original = gzip.decompress(stream.read(struct.unpack('<I', stream.read(4))[0]))
        assert len(original) == size and hashlib.sha256(original).digest() == digest
        data = json.loads(original)
        if sex == 'male':
            curve_source = data['animations']['map/idle_01']['bones']['shenti_0']['rotate'][:2]
        selected = {key: data[key] for key in ('skeleton', 'bones', 'ik', 'transform', 'physics')}
        # This lossless binary projection omits unset transform setup flag bits
        # (zero mix). The public JSON reader has different, nonzero defaults.
        # Restore binary setup values before using JSON as a reference transport.
        for constraint in selected['transform']:
            for axis in ('mixRotate', 'mixX', 'mixY', 'mixScaleX', 'mixScaleY', 'mixShearY'):
                constraint.setdefault(axis, 0)
        selected.update(slots=[], skins=[], animations={'map/idle_01': {
            key: value for key, value in data['animations']['map/idle_01'].items()
            if key in ('bones', 'ik', 'transform', 'physics')}})
        input_path = output / (sex + '.json')
        input_path.write_text(json.dumps(selected), 'utf8')
        for profession in (1, 10):
            trace = work / ('native-pose-' + sex.title() + '-' + str(profession) + '.txt')
            target = output / (sex + '-' + str(profession) + '.json')
            subprocess.run([str(mono), str(exe), str(input_path), str(trace), str(target)], check=True)
            result = json.loads(target.read_text('utf-8-sig'))
            result.update(sex=sex, profession=profession, input_sha256=hashlib.sha256(raw).hexdigest(),
                          trace_sha256=hashlib.sha256(trace.read_bytes()).hexdigest())
            results.append(result)
    receipt = dict(reference_sha256=hashlib.sha256(binary.read_bytes()).hexdigest(),
        fixture_sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), results=results)
    if args.record_bezier:
        original = [curve_source[0].get('time', 0), curve_source[0]['value'],
                    *curve_source[0]['curve'], curve_source[1]['time'], curve_source[1]['value']]
        cases = [original, [0, -3, .04, 8, .9, -7, 1, 2],
                 [0, 4, 0, -8, 1, 13, 1, -2], [0, 0, .9, 0, .1, 1, 1, 1]]
        observations = ['# Public numerical observations; no animation SDK implementation.\n']
        for index, values in enumerate(cases):
            start, value, x1, y1, x2, y2, end, final = values
            animation = dict(rotate=[dict(time=start, value=value, curve=[x1, y1, x2, y2]), dict(time=end, value=final)])
            source = output / ('curve-' + str(index) + '.json')
            target = output / ('curve-' + str(index) + '.txt')
            source.write_text(json.dumps(dict(skeleton=dict(spine='4.2.0', hash='public-numerical-probe'),
                bones=[dict(name='root')], animations=dict(a=dict(bones=dict(root=animation))))), 'utf8')
            subprocess.run([str(mono), str(exe), '--curve', str(source), str(target)], check=True)
            observations.append('C|' + '|'.join(map(str, values)) + '\n')
            observations.append(target.read_text('utf-8-sig'))
        fixture_path = output / 'partner_player_bezier_reference.txt'
        fixture_path.write_text(''.join(observations), 'utf8', newline='\n')
        receipt['bezier_observations'] = dict(cases=4, samples=244,
            sha256=hashlib.sha256(fixture_path.read_bytes()).hexdigest())
    (output / 'reference.json').write_text(json.dumps(receipt, indent=2) + '\n', 'utf8')
    if any(row['maximum_basis_error'] > .00002 or row['maximum_position_error'] > .002 for row in results):
        raise SystemExit('Original native player hierarchy differs beyond float/coordinate precision.')


if __name__ == '__main__':
    main()
