"""Stage only the verified native income-window closure in an owned Unity cache."""
import json
from pathlib import Path
import shutil

from prepare_partner_card_native import ROOT, sha
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    project = require_temporary_unity_project(validation_directory()/'unity')
    native = json.loads((ROOT/'assets/reference/home-hangup-windows-native-source-manifest.json').read_bytes())
    runtime = json.loads((ROOT/'assets/reference/home-hangup-windows-runtime-source-manifest.json').read_bytes())
    files = dict(runtime['outputs'])
    files.update(native['installed_output_sha256'])
    for row in native['reused']:
        files[row['path']] = row['sha256']
        meta = ROOT/(row['path']+'.meta')
        files[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    for relative, expected in files.items():
        source = ROOT/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Income validation input changed: '+relative)
        target = project/relative.removeprefix('unity/')
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or source.read_bytes() != target.read_bytes(): shutil.copyfile(source, target)
    folder = project/'Assets/HomeHangUpWindowsNativeValidation'
    folder.mkdir(exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/home_hangup_windows_native_validation.cs', folder/'HomeHangUpWindowsNativeValidation.cs')
    (folder/'Zjcs.HomeHangUpWindowsNativeValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeHangUpWindowsNativeValidation', includePlatforms=['Editor'], references=[
            'Unity.ugui', 'Unity.TextMeshPro', 'Unity.Newtonsoft.Json',
            'Zjcs.Official.HomeHangUpWindowsUI', 'Zjcs.Official.HomeHangUpTips',
            'Zjcs.Official.HomeSceneUiControls', 'Zjcs.Official.HomeScreenUI',
            'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI',
            'Zjcs.Official.World.NativeLabelUI', 'Zjcs.Offline.HomeHudUI',
            'Zjcs.Offline.PartnersUI', 'Zjcs.Offline.ProfessionUI', 'Zjcs.Presentation.World']), indent=2)+'\n', 'utf8', newline='\n')
    (folder/'fixture.json').write_text(json.dumps(native,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    native['validation_inputs'] = {relative: sha((ROOT/relative).read_bytes()) for relative in (
        'scripts/prepare_home_hangup_windows_validation.py',
        'scripts/fixtures/home_hangup_windows_native_validation.cs')}
    (ROOT/'assets/reference/home-hangup-windows-native-source-manifest.json').write_text(
        json.dumps(native,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged native income window validation:', len(files), 'verified files', flush=True)


if __name__ == '__main__': prepare()
