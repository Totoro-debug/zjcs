"""Stage the real original acceleration presentation in an owned Unity project."""
import json
import shutil
from prepare_partner_card_native import ROOT, sha
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    project = require_temporary_unity_project(validation_directory()/'unity')
    native_path = ROOT/'assets/reference/home-fast-hangup-progress-native-source-manifest.json'
    native = json.loads(native_path.read_bytes())
    runtime = json.loads((ROOT/'assets/reference/home-fast-hangup-progress-runtime-source-manifest.json').read_bytes())
    files = {**runtime['outputs'], **native['installed_output_sha256']}
    for row in native['reused']:
        files[row['path']] = row['sha256']
        name = row['path']+'.meta'; files[name] = sha((ROOT/name).read_bytes())
    for relative, expected in files.items():
        source = ROOT/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Original presentation validation input changed: '+relative)
        target = project/relative.removeprefix('unity/'); target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_bytes() != source.read_bytes(): shutil.copyfile(source, target)
    folder = project/'Assets/HomeFastHangUpProgressValidation'; folder.mkdir(exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/home_fast_hangup_progress_validation.cs', folder/'HomeFastHangUpProgressValidation.cs')
    (folder/'Zjcs.HomeFastHangUpProgressValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeFastHangUpProgressValidation', includePlatforms=['Editor'], references=[
            'Unity.ugui','Unity.TextMeshPro','Unity.Newtonsoft.Json','Unity.RenderPipelines.Universal.Runtime',
            'Unity.RenderPipelines.Core.Runtime','Zjcs.Official.HomeFastHangUpProgressUI','Zjcs.Official.HomeFastHangUpUI',
            'Zjcs.Official.HomeHangUpWindowsUI','Zjcs.Official.HomeUI','Zjcs.Official.HomeScreenUI','Zjcs.Official.CombatAppearance',
            'Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI',
            'Zjcs.Offline.HomeFastHangUpProgressUI','Zjcs.Offline.HomeHudUI','Zjcs.Offline.PartnersUI',
            'Zjcs.Offline.ProfessionUI','Zjcs.Offline.Progression','Zjcs.Offline.State']), indent=2)+'\n', 'utf8', newline='\n')
    (folder/'fixture.json').write_text(json.dumps(native, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    native['validation_inputs'] = {name: sha((ROOT/name).read_bytes()) for name in (
        'scripts/prepare_home_fast_hangup_progress_validation.py','scripts/fixtures/home_fast_hangup_progress_validation.cs')}
    baseline = project.parent/'committed-asset-baseline.json'
    if baseline.exists(): native['validation_base_commit'] = json.loads(baseline.read_bytes())['commit']
    native_path.write_text(json.dumps(native, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Staged original acceleration presentation fixture:', len(files), 'files', flush=True)


if __name__ == '__main__': prepare()
