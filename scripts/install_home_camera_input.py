"""Install selected official camera input operations and dispatcher."""
import json
from pathlib import Path
import subprocess

from install_home_story_options import metadata
from official_apk import ROOT
from prepare_partner_card_native import guid, sha
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    proof = json.loads((work / 'home-camera-input-source-manifest.json').read_bytes())
    if proof['manifest'] != '884_156692': raise ValueError('Wrong original camera input version.')
    for path, expected in proof['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original camera input changed: ' + path)
    stage = work / 'home-camera-input'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/HomeCameraInput'
    previous_path = ROOT / 'assets/reference/home-camera-input-source-manifest.json'
    previous = json.loads(previous_path.read_bytes()) if previous_path.is_file() else {}
    for name, expected in proof['outputs'].items():
        source, destination = stage / name, target / name
        if sha(source.read_bytes()) != expected: raise ValueError('Changed staged camera input: ' + name)
        if destination.exists() and destination.read_bytes() != source.read_bytes():
            if previous.get('installed_output_sha256', {}).get(destination.relative_to(ROOT).as_posix()) != sha(destination.read_bytes()):
                raise ValueError('Preserve existing camera input: ' + str(destination))
    wanted = work / 'home-camera-input-install-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(stage / name) + '\n' for name in proof['outputs'] if name.endswith('.meta')), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), 'unity/Assets', '-g', '*.meta'],
                           cwd=ROOT, capture_output=True, text=True)
    if found.returncode not in (0, 1): raise ValueError(found.stderr)
    for row in found.stdout.splitlines():
        if not (ROOT / row).is_relative_to(target): raise ValueError('Preserve existing camera input GUID: ' + row)
    target.mkdir(parents=True, exist_ok=True)
    folder_meta = metadata(target, 'DefaultImporter', folder=True)
    installed = {folder_meta.relative_to(ROOT).as_posix(): sha(folder_meta.read_bytes())}
    for name in proof['outputs']:
        output = target / name
        if not output.exists() or output.read_bytes() != (stage / name).read_bytes():
            output.write_bytes((stage / name).read_bytes())
        installed[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    proof['installed_output_sha256'] = installed
    proof['validation_inputs'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
        (Path(__file__), ROOT / 'scripts/prepare_home_camera_input_validation.py',
         ROOT / 'scripts/fixtures/home_camera_input_validation.cs')}
    proof['scope'] = ('Original pointer, drag and zoom operations and input dispatcher with explicit operation injection; '
                      'complete operation discovery and formal home host binding remain pending.')
    previous_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed selected original home camera input operations.', flush=True)


if __name__ == '__main__':
    main()
