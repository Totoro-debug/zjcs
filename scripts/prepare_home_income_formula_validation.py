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
    source = source.replace('home.Scene.Camera.targetTexture = target;',
        'home.Scene.Camera.targetTexture = target;\n        home.Scene.Camera.gameObject.AddComponent<AudioListener>();')
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
            'Zjcs.Presentation.Home', 'Unity.TextMeshPro', 'Zjcs.Official.HomeAwardUI',
            'Zjcs.Offline.HomeUI', 'Zjcs.Official.HomeHangUpWindowsUI',
            'Zjcs.Official.WorldUI', 'Zjcs.Official.HomeFastHangUpUI']), indent=2) + '\n', 'utf8', newline='\n')
    receipt_path = ROOT / 'assets/reference/home-income-formula-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    source = '.tools/source-packages/current/884_156692/official-code/client-source/FurnitureBedDialog.cs'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    for name in ('PlayerHangUpDataExt', 'Player', 'FunctionType', 'FurnitureLevelRuleInfo',
                 'EnumExt', 'MapRegionInfoParser', 'ServerInfoManager', 'FunctionsDisabledData'):
        source = '.tools/source-packages/current/884_156692/official-code/common-source/Common/' + name + '.cs'
        receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    source = '.tools/source-packages/current/884_156692/official-code/client-source/HangUpUIUtils.cs'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    receipt['free_fast_forward_adapter'] = (
        'Selected function 53 keyed by fast-hang-up ID, configured free count plus original non-battle '
        'property 1101, and read-only expired-count projection. User-confirmed local 06:00 day boundary '
        'reuses OfflineRefreshClock; old midnight-era morning counters migrate without granting another use.')
    receipt['collection_adapter'] = (
        'CollectReward projects only the committed claim delta, selected output item and durable ledger identity; '
        'presentation does not issue a second grant. Failed character experience mutations retain the local ledger; '
        'previously committed character receipts acknowledge without another award. The ledger persists a sealed '
        'transfer amount before granting; later same-item claims receive a separate serial until acknowledgement.')
    receipt['ordinary_settlement_adapter'] = (
        'User-confirmed ordinary claims follow the original client unsettled preview, independently of '
        'hourly/fast output argument order. Original fractional remainder is persisted when finite; '
        'pending local time and its subsecond checkpoint commit atomically with the claim. Failed grants '
        'retain the previous time, reward selection state and receipt.')
    receipt['native_bed_adapter'] = (
        'Complete selected FurnitureBedDialog prefab/controller, original output foldouts, quick-receive '
        'condition, property chunks and inventory response phases are bound to the actual local home/profile. '
        'No remote disable keys are installed, matching original empty-key HasKey=false. The original blessing-rule '
        'window uses selected rule_tip rows, actual blessing level, eleven original pooled rows and real home limits; '
        'its pointer, scrolling, highlight and mask-close behavior are covered. Secondary acceleration and '
        'activity-output details are not covered by this production-bed validation.')
    source = 'assets/reference/home-fast-hangup-native-source-manifest.json'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    receipt['income_reward_adapter'] = (
        'Selected hang_up_reward and reward_rule rows select all 259 reward/cost item definitions and 15 unique '
        'original image addresses; original ItemIconManager priority proves the default template for each. '
        'The original ignored-award list is retained. Actual cart repair/unlock/claim/drain and its original '
        'queue display use a real saved receipt; presentation never writes either save. General indexed '
        'equipment instances, detail popups and the production acceleration sequence remain separate.')
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
