"""Record numerical observations from public calls on an explicitly supplied binary.

This optional reference tool writes only to an owned validation directory. The
game, native conversion and regression tests do not invoke it or load the binary.
"""
import argparse
import hashlib
import json
import shutil
import subprocess
import struct
import gzip
import io
from pathlib import Path

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--reference-dll', type=Path, required=True)
    parser.add_argument('--verify-player-assets', action='store_true')
    args = parser.parse_args()
    binary = args.reference_dll.resolve(strict=True)
    work = validation_directory() / 'player-physics-observations'
    work.mkdir(exist_ok=True)
    config = json.loads((ROOT / 'unity-tools.json').read_bytes())
    mono_root = Path(config['editorExecutable']).parent / 'Data/MonoBleedingEdge'
    mono, csc = mono_root / 'bin/mono.exe', mono_root / 'lib/mono/4.5/csc.exe'
    copied = work / binary.name
    shutil.copy2(binary, copied)
    exe = work / 'PhysicsReference.exe'
    subprocess.run([str(mono), str(csc), '/nologo', '/r:' + str(copied),
        '/r:' + str(mono_root / 'lib/mono/4.8-api/Facades/netstandard.dll'), '/out:' + str(exe),
        str(ROOT / 'scripts/fixtures/partner_player_physics_reference_capture.cs')], check=True)
    base = dict(skeleton=dict(spine='4.2.0', hash='black-box-test', referenceScale=100),
        bones=[dict(name='root', length=100)], physics=[dict(name='p', bone='root', x=0, y=0,
        rotate=1, fps=60, inertia=.5, strength=100, damping=.85, mass=1, wind=0, gravity=0, mix=1)])
    cases = []
    variants = [('rotation', {}), ('turn-partial', {'rotate': .3}), ('mix-partial', {'mix': .4}),
        ('translation', {'rotate': 0, 'x': 1, 'y': 1}), ('combined', {'rotate': .5, 'x': .5, 'y': .25}),
        ('wind', {'wind': -30}), ('forces', {'wind': 2, 'gravity': 3}), ('limit', {'limit': 1}),
        ('damping', {'damping': .5, 'mass': 2}), ('zero-mix', {'mix': 0}),
        ('wind-partial', {'wind': 2, 'gravity': 3, 'rotate': .3}),
        ('force-combined', {'wind': 2, 'gravity': 3, 'mix': .4, 'x': .5, 'y': .25, 'rotate': .5}),
        ('fps30', {'fps': 30}), ('fps100', {'fps': 100})]
    for name, patch in variants:
        for motion in ('harmonic', 'mix-flip'):
            value = json.loads(json.dumps(base))
            value['physics'][0].update(patch)
            if name in ('wind', 'wind-partial', 'force-combined'):
                value['bones'][0]['length'] = 14.870136260986328
            cases.append((name + '-' + motion, value, '0.016666667', motion))
    for dt in ('0', '0.008333334', '0.03125'):
        cases.append(('clock-' + dt, base, dt, 'harmonic'))
    for length in (0, 1, 4):
        value = json.loads(json.dumps(base)); value['bones'][0]['length'] = length
        cases.append(('length-' + str(length), value, '0.016666667', 'harmonic'))
    observations = ['# Recorded public-call numerical observations only. See partner-player-physics-source-manifest.json.\n']
    for name, value, dt, motion in cases:
        source, target = work / (name + '.json'), work / (name + '.txt')
        source.write_text(json.dumps(value), encoding='utf8', newline='\n')
        subprocess.run([str(mono), str(exe), str(source), dt, motion, str(target)], check=True,
            stdout=subprocess.DEVNULL)
        observations.append('# ' + name + '\n' + target.read_text(encoding='utf-8-sig'))
    output = work / 'partner_player_physics_reference.txt'
    output.write_text(''.join(observations), encoding='utf8', newline='\n')
    reference = dict(path=str(binary), sha256=hashlib.sha256(binary.read_bytes()).hexdigest(),
        output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(), cases=len(cases), frames=len(cases) * 120)
    if args.verify_player_assets:
        asset_exe = work / 'PhysicsAssetReference.exe'
        subprocess.run([str(mono), str(csc), '/nologo', '/r:' + str(copied),
            '/r:' + str(mono_root / 'lib/mono/4.8-api/Facades/netstandard.dll'), '/out:' + str(asset_exe),
            str(ROOT / 'scripts/fixtures/partner_player_physics_asset_reference.cs'),
            str(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSpringConstraint.cs')], check=True)
        reference['player_assets'] = {}
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
            compressed = stream.read(struct.unpack('<I', stream.read(4))[0])
            skeleton = gzip.decompress(compressed)
            assert len(skeleton) == size and hashlib.sha256(skeleton).digest() == digest
            original = json.loads(skeleton)
            # Keep original bones, constraints and authored pose channels. No
            # attachment/texture runtime is needed for a dynamics-only check.
            selected = {key: original[key] for key in ('skeleton', 'bones', 'ik', 'transform', 'physics')}
            selected.update(slots=[], skins=[], animations={'map/idle_01': {
                key: value for key, value in original['animations']['map/idle_01'].items()
                if key in ('bones', 'ik', 'transform', 'physics')}})
            json_path, result_path = work / (sex + '-constraints.json'), work / (sex + '-result.json')
            json_path.write_text(json.dumps(selected), encoding='utf8')
            subprocess.run([str(mono), str(asset_exe), str(json_path), str(result_path)], check=True)
            result = json.loads(result_path.read_text(encoding='utf-8-sig'))
            result.update(source=dict(path=source.relative_to(ROOT).as_posix(),
                sha256=hashlib.sha256(raw).hexdigest()), skeleton_sha256=digest.hex(), animation='map/idle_01')
            reference['player_assets'][sex] = result
    (work / 'reference.json').write_text(json.dumps(reference, indent=2) + '\n', encoding='utf8')
    print('Recorded isolated observations:', output)


if __name__ == '__main__':
    main()
