"""Stage verified native item acceleration and its fixture in an owned Unity cache."""
import json
from pathlib import Path
import shutil
from prepare_partner_card_native import ROOT, sha
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    project = require_temporary_unity_project(validation_directory()/'unity')
    native_path = ROOT/'assets/reference/home-fast-hangup-native-source-manifest.json'
    native = json.loads(native_path.read_bytes())
    runtime = json.loads((ROOT/'assets/reference/home-fast-hangup-runtime-source-manifest.json').read_bytes())
    files = dict(runtime['outputs']); files.update(native['installed_output_sha256'])
    for row in native['reused']:
        files[row['path']] = row['sha256']
        path = ROOT/(row['path']+'.meta'); files[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for relative, expected in files.items():
        source = ROOT/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Acceleration validation source changed: '+relative)
        target = project/relative.removeprefix('unity/'); target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_bytes() != source.read_bytes(): shutil.copyfile(source, target)
    folder = project/'Assets/HomeFastHangUpNativeValidation'; folder.mkdir(exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/home_fast_hangup_native_validation.cs', folder/'HomeFastHangUpNativeValidation.cs')
    (folder/'fixture.json').write_text(json.dumps(native, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    (folder/'Zjcs.HomeFastHangUpNativeValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeFastHangUpNativeValidation',
        includePlatforms=['Editor'], references=['Unity.ugui','Unity.TextMeshPro','Unity.Newtonsoft.Json',
        'Zjcs.Official.HomeFastHangUpUI','Zjcs.Official.HomeHangUpWindowsUI','Zjcs.Official.HomeHangUpTips',
        'Zjcs.Official.HomeCountControlsUI','Zjcs.Official.HomeScreenUI','Zjcs.Official.HomeSceneUiControls','Zjcs.Official.HomeUI',
        'Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Offline.HomeHudUI','Zjcs.Offline.HomeCurrencyUI','Zjcs.Offline.PartnersUI',
        'Zjcs.Offline.ProfessionUI','Zjcs.Presentation.World']), indent=2)+'\n', 'utf8', newline='\n')
    native['validation_inputs'] = {relative: sha((ROOT/relative).read_bytes()) for relative in (
        'scripts/prepare_home_fast_hangup_validation.py','scripts/fixtures/home_fast_hangup_native_validation.cs')}
    native_path.write_text(json.dumps(native, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Staged original acceleration fixture:', len(files), 'files', flush=True)


if __name__ == '__main__': prepare()
