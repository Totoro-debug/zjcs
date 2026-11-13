"""Run original output-addition queries against the actual home save in Play Mode."""
import json
import hashlib
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    folder = project / 'Assets/HomeOutputAdditionValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source = source.replace('HomeFurnitureHost', 'HomeOutputAdditionHost')
    source = source.replace('public sealed class HomeOutputAdditionHostRunner', 'public sealed partial class HomeOutputAdditionHostRunner')
    source = source.replace('Capture("home-current-reopened.png");',
                            'Capture("home-current-reopened.png");\n        yield return CheckOutputAddition();')
    source = source.replace('HOME_FURNITURE_HOST_PASSED', 'HOME_OUTPUT_ADDITION_HOST_FINISHED')
    source = source.replace('+ 240);', '+ 600);')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    source = source.replace('home?.Close();', 'if (home != null) home.Close();')
    checks = (ROOT / 'scripts/fixtures/home_output_addition_validation.cs').read_text('utf8')
    header = source[:source.index('[InitializeOnLoad]')]
    (folder / 'HomeOutputAdditionHostRunner.cs').write_text('#if UNITY_EDITOR\n' + source + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'checks.cs').write_text('#if UNITY_EDITOR\n' + header + checks + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'Zjcs.HomeOutputAdditionValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeOutputAdditionValidation', references=[
            'Unity.ugui', 'Zjcs.Offline.Home', 'Zjcs.Offline.HomeUI', 'Zjcs.Official.HomeOutputAddition',
            'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Progression',
            'Zjcs.Offline.State', 'Zjcs.Presentation.Home', 'Unity.Newtonsoft.Json']), indent=2) + '\n', 'utf8', newline='\n')
    receipt_path = ROOT / 'assets/reference/home-output-addition-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    receipt['runtime_bindings'] = {}
    for name in (
        'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs',
        'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeOutputAddition.cs',
        'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeOutputAddition.cs.meta',
        'unity/Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef'):
        raw = (ROOT / name).read_bytes()
        if raw != (work / name).read_bytes():
            raise ValueError('Validation overlay differs: ' + name)
        receipt['runtime_bindings'][name] = hashlib.sha256(raw).hexdigest()
    receipt['validation_inputs'] = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in (
        'scripts/prepare_home_output_addition_validation.py', 'scripts/fixtures/home_output_addition_validation.cs',
        'scripts/fixtures/home_furniture_host_validation.cs')}
    receipt['validation_base_commit'] = json.loads((work / 'selection-base-commit.json').read_bytes())['commit']
    receipt['validation_home_overlay'] = json.loads((ROOT / 'assets/reference/home-income-selection-source-manifest.json').read_bytes())['runtime_bindings']
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared actual home output-addition validation')


if __name__ == '__main__':
    prepare()
