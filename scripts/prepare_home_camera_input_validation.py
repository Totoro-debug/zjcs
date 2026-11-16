"""Stage selected camera input operations and process-local input checks."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
import prepare_unity_map13_camera_controllers as camera
from prepare_partner_card_native import guid, sha
from validation_workspace import require_temporary_unity_project, validation_directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    work = validation_directory()
    proof = json.loads((work / 'home-camera-input-source-manifest.json').read_bytes())
    for path, expected in proof['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Changed official camera input source: ' + path)
    stage = work / 'home-camera-input'
    for name, expected in proof['outputs'].items():
        if sha((stage / name).read_bytes()) != expected: raise ValueError('Changed staged camera input: ' + name)
    wanted = work / 'home-camera-input-validation-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(stage / name) + '\n' for name in proof['outputs'] if name.endswith('.meta')), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), str(project / 'Assets'), '-g', '*.meta'],
                           capture_output=True, text=True)
    if found.returncode not in (0, 1): raise ValueError(found.stderr)
    target = project / 'Assets/Game/Runtime/Official/HomeCameraInputValidation'
    retired = target / 'InputOperationLayer.cs'
    retired_meta = Path(str(retired) + '.meta')
    if retired.exists() or retired_meta.exists():
        if not retired.is_file() or not retired_meta.is_file(): raise ValueError('Incomplete previous isolated input-layer staging.')
        camera.NAMESPACE = 'Zjcs.Official.HomeCamera'
        original = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/InputOperationLayer.cs'
        expected = camera.port(original.read_text('utf8'), 'InputOperationLayer').replace(
            'CameraVectorCast.Convert(', 'CameraInputVectorCast.Convert(').encode('utf8')
        key = hashlib.md5(b'zjcs:home-camera-input:884:InputOperationLayer.cs').hexdigest()
        expected_meta = ('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n  externalObjects: {}\n').encode()
        if retired.read_bytes() != expected or retired_meta.read_bytes() != expected_meta:
            raise ValueError('Preserve changed isolated input-layer files.')
        retired.unlink(); retired_meta.unlink()
    for existing in found.stdout.splitlines():
        if not Path(existing).is_relative_to(target): raise ValueError('Preserve existing camera input GUID: ' + existing)
    target.mkdir(parents=True, exist_ok=True)
    for name in proof['outputs']:
        shutil.copy2(stage / name, target / name)
    fixture = project / 'Assets/Editor/HomeCameraInputValidation'
    fixture.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/home_camera_input_validation.cs', fixture / 'HomeCameraInputValidation.cs')
    (fixture / 'HomeCameraInputValidation.asmdef').write_text(json.dumps(dict(name='HomeCameraInputValidation',
        includePlatforms=['Editor'], references=['Zjcs.Official.HomeCamera', 'Zjcs.Official.HomeCameraInput',
                                                'UnityEngine.UI']), indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original camera pointer and zoom checks.', flush=True)


if __name__ == '__main__':
    main()
