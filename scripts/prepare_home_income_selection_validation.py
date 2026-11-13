"""Exercise selected income tables through the actual home host and original selectors."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    folder = project / 'Assets/HomeIncomeSelectionValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source = source.replace('HomeFurnitureHost', 'HomeIncomeSelectionHost')
    source = source.replace('public sealed class HomeIncomeSelectionHostRunner', 'public sealed partial class HomeIncomeSelectionHostRunner')
    source = source.replace('Capture("home-current-reopened.png");',
        'Capture("home-current-reopened.png");\n        yield return CheckIncomeSelection();\n        yield return CheckIncomeFormula();')
    source = source.replace('HOME_FURNITURE_HOST_PASSED', 'HOME_INCOME_SELECTION_HOST_FINISHED')
    source = source.replace('+ 240);', '+ 600);')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    source = source.replace('home?.Close();', 'if (home != null) home.Close();')
    (folder / 'HomeIncomeSelectionHostRunner.cs').write_text('#if UNITY_EDITOR\n' + source + '\n#endif\n', 'utf8', newline='\n')
    for name in ('selection', 'formula'):
        checks = (ROOT / ('scripts/fixtures/home_income_' + name + '_validation.cs')).read_text('utf8')
        checks = checks.replace('HomeIncomeFormulaHostRunner', 'HomeIncomeSelectionHostRunner')
        (folder / (name + '.cs')).write_text('#if UNITY_EDITOR\n' + checks + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'Zjcs.HomeIncomeSelectionValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeIncomeSelectionValidation', references=[
            'Unity.ugui', 'Zjcs.Offline.Home', 'Zjcs.Offline.HomeUI', 'Zjcs.Official.HomeIncomeRules',
            'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.Commerce',
            'Zjcs.Official.Partners', 'Zjcs.Official.HomeConditions',
            'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State',
            'Zjcs.Presentation.Home']), indent=2) + '\n', 'utf8', newline='\n')
    path = ROOT / 'assets/reference/home-income-selection-source-manifest.json'
    receipt = json.loads(path.read_bytes())
    receipt['validation_inputs'] = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in (
        'scripts/prepare_home_income_selection_validation.py', 'scripts/fixtures/home_income_selection_validation.cs',
        'scripts/fixtures/home_income_formula_validation.cs', 'scripts/fixtures/home_furniture_host_validation.cs')}
    receipt['validation_base_commit'] = json.loads((work / 'selection-base-commit.json').read_bytes())['commit']
    receipt['runtime_bindings'] = {}
    for name in (
        'unity/Assets/Game/Runtime/Offline/Home/OfficialHomeRules.cs',
        'unity/Assets/Game/Runtime/Offline/Home/OfficialHomeRules.CurrentIncome.cs',
        'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeProgression.cs',
        'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineCurrentHomeIncome.cs',
        'unity/Assets/Game/Runtime/Offline/HomeUI/OfficialCurrentHomeRuleAssetCatalog.cs',
        'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineCurrentHomeFurniture.cs',
        'unity/Assets/Game/Runtime/Official/HomeConditions/HomeConditionRules.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs'):
        raw = (ROOT / name).read_bytes()
        if (work / name).read_bytes() != raw:
            raise ValueError('Validation runtime overlay differs: ' + name)
        receipt['runtime_bindings'][name] = hashlib.sha256(raw).hexdigest()
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared actual selected home income validation')


if __name__ == '__main__':
    prepare()
