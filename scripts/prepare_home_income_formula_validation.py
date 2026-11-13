"""Bind the actual home host fixture to the migrated official income arithmetic."""
import json
import hashlib

from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    folder = project / 'Assets/HomeIncomeFormulaValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source = source.replace('HomeFurnitureHostValidation', 'HomeIncomeFormulaValidation')
    source = source.replace('HomeFurnitureHostRunner', 'HomeIncomeFormulaHostRunner')
    source = source.replace('public sealed class HomeIncomeFormulaHostRunner', 'public sealed partial class HomeIncomeFormulaHostRunner')
    source = source.replace('Capture("home-current-reopened.png");',
                            'Capture("home-current-reopened.png");\n        yield return CheckIncomeFormula();')
    source = source.replace('HOME_FURNITURE_HOST_PASSED', 'HOME_INCOME_FORMULA_HOST_FINISHED')
    source = source.replace('+ 240);', '+ 600);')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    source = source.replace('home?.Close();', 'if (home != null) home.Close();')
    # Unity requires one MonoBehaviour partial declaration to match its filename.
    for name in ('HomeIncomeFormulaValidation.cs', 'HomeIncomeFormulaValidation.cs.meta'):
        (folder / name).unlink(missing_ok=True)
    (folder / 'HomeIncomeFormulaHostRunner.cs').write_text('#if UNITY_EDITOR\n' + source + '\n#endif\n', 'utf8', newline='\n')
    checks = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8')
    (folder / 'IncomeChecks.cs').write_text('#if UNITY_EDITOR\n' + checks + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'Zjcs.HomeIncomeFormulaValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeIncomeFormulaValidation', references=[
            'Unity.ugui', 'Zjcs.Offline.Home', 'Zjcs.Official.HomeIncomeRules', 'Zjcs.Official.HomeUI',
            'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.Commerce',
            'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State',
            'Zjcs.Presentation.Home']), indent=2) + '\n', 'utf8', newline='\n')
    receipt_path = ROOT / 'assets/reference/home-income-formula-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    receipt['validation_inputs'] = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in (
        'scripts/prepare_home_income_formula_validation.py', 'scripts/fixtures/home_income_formula_validation.cs',
        'scripts/fixtures/home_furniture_host_validation.cs')}
    baseline = json.loads((work / 'base-commit.json').read_bytes())
    receipt['validation_base_commit'] = baseline['commit']
    receipt['runtime_bindings'] = {}
    for path in baseline['overlays']:
        if '/Official/HomeIncomeRules' in path.replace('\\', '/'):
            continue
        raw = (ROOT / path).read_bytes()
        if (work / path).read_bytes() != raw:
            raise ValueError('Validation overlay differs from current home runtime: ' + path)
        receipt['runtime_bindings'][path.replace('\\', '/')] = hashlib.sha256(raw).hexdigest()
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged actual home income validation')


if __name__ == '__main__':
    prepare()
