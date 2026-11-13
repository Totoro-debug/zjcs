"""Overlay this session's bed output binding into its owned full Unity cache."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project

FILES = [
    'unity/Assets/Game/Resources/Home/home-bed-output-texts.json',
    'unity/Assets/Game/Resources/Home/home-bed-output-texts.json.meta',
    'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI/FurnitureOutputInfo.cs',
    'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI/IHomeFurnitureOutputServices.cs',
    'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI/IHomeFurnitureOutputServices.cs.meta',
    'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeFurnitureOutput.cs',
    'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeFurnitureOutput.cs.meta',
    'unity/Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef',
    'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
    'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
    'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.BedOutput.cs',
    'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.BedOutput.cs.meta',
    'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
]


def prepare():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    for name in FILES:
        target = work / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((ROOT / name).read_bytes())
    folder = project / 'Assets/HomeBedOutputValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source = source.replace('HomeFurnitureHost', 'HomeBedOutputHost')
    source = source.replace('public sealed class HomeBedOutputHostRunner', 'public sealed partial class HomeBedOutputHostRunner')
    source = source.replace('Capture("home-current-reopened.png");',
        'Capture("home-current-reopened.png");\n        yield return CheckBedOutput();')
    source = source.replace('HOME_FURNITURE_HOST_PASSED', 'HOME_BED_OUTPUT_HOST_FINISHED')
    source = source.replace('+ 240);', '+ 600);')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    source = source.replace('home?.Close();', 'if (home != null) home.Close();')
    checks = (ROOT / 'scripts/fixtures/home_bed_output_validation.cs').read_text('utf8')
    header = source[:source.index('[InitializeOnLoad]')]
    (folder / 'HomeBedOutputHostRunner.cs').write_text('#if UNITY_EDITOR\n' + source + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'checks.cs').write_text('#if UNITY_EDITOR\n' + header + checks + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'Zjcs.HomeBedOutputValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeBedOutputValidation', references=[
            'Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Offline.Home', 'Zjcs.Offline.HomeUI',
            'Zjcs.Official.HomeHangUpWindowsUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI',
            'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State',
            'Zjcs.Offline.Commerce', 'Zjcs.Presentation.Home']), indent=2) + '\n', 'utf8', newline='\n')
    proof = dict(manifest='884_156692', runtime_bindings={name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in FILES},
        validation_inputs={name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in (
            'scripts/prepare_home_bed_output_validation.py', 'scripts/fixtures/home_bed_output_validation.cs',
            'scripts/fixtures/home_furniture_host_validation.cs')},
        native_assets='assets/reference/home-hangup-windows-native-source-manifest.json',
        original_methods='assets/reference/home-hangup-windows-runtime-source-manifest.json',
        selected_configuration='assets/reference/home-income-selection-source-manifest.json',
        selected_texts='assets/reference/home-bed-output-texts-source-manifest.json',
        validation_base_commit=json.loads((work / ('bed-output-base-commit.json'
            if (work / 'bed-output-base-commit.json').exists() else 'selection-base-commit.json')).read_bytes())['commit'],
        scope='Production original bed output component and eight native property rows only; whole window/remaining HUD and reward UI remain unfinished.')
    for key in ('native_assets', 'original_methods', 'selected_configuration', 'selected_texts'):
        proof[key + '_sha256'] = hashlib.sha256((ROOT / proof[key]).read_bytes()).hexdigest()
    native = json.loads((ROOT / proof['native_assets']).read_bytes())
    script = native['script_bindings']['FurnitureOutputInfo']
    proof['native_script_rebinding'] = dict(guid=script['pointer']['guid'],
        converted_sha256=script['sha256'], current_sha256=proof['runtime_bindings'][script['path']],
        adaptation='Only data-service boundary and read-only row references added; original serialized fields and method bodies retained.')
    (ROOT / 'assets/reference/home-bed-output-binding-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared actual home native bed output validation')


if __name__ == '__main__':
    prepare()
