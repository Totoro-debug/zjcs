"""Bind the actual home host fixture to the migrated official income arithmetic."""
import json
import hashlib
import subprocess

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
    source = source.replace('public static class HomeIncomeFormulaValidation', 'public static partial class HomeIncomeFormulaValidation')
    source = source.replace('EditorApplication.EnterPlaymode();', 'ConfigureNativeFrame();\n        EditorApplication.EnterPlaymode();')
    source = source.replace('public sealed class HomeIncomeFormulaHostRunner', 'public sealed partial class HomeIncomeFormulaHostRunner')
    source = source.replace('Capture("home-current-reopened.png");',
                            'Capture("home-current-reopened.png");\n        yield return CheckIncomeFormula();')
    source = source.replace('HOME_FURNITURE_HOST_PASSED', 'HOME_INCOME_FORMULA_HOST_FINISHED')
    source = source.replace('AudioListener.volume = 0;', 'Application.runInBackground = true;\n        AudioListener.volume = 0;')
    source = source.replace('+ 240);', '+ 600);')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    source = source.replace('home?.Close();', 'if (home != null) home.Close();')
    source = source.replace('"Repair button is top UI hit"',
        '"Button is top UI hit: " + button.name + " @ " + point + "; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name))')
    source = source.replace('home.Scene.Camera.targetTexture = target;',
        'home.Scene.Camera.targetTexture = null;\n        home.Scene.Camera.gameObject.AddComponent<AudioListener>();')
    # Keep the real backbuffer alive between captures. Original blur capture
    # reads that frame at end-of-frame, not the fixture's offscreen texture.
    source = source.replace('private void Capture(string name)\n    {',
        'private void Capture(string name)\n    {\n        home.Scene.Camera.targetTexture = target;')
    source = source.replace('        Save(name);', '        Save(name);\n        home.Scene.Camera.targetTexture = null;')
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
            'Zjcs.Official.WorldUI', 'Zjcs.Official.HomeFastHangUpUI',
            'Zjcs.Official.HomeFastHangUpProgressUI', 'Zjcs.Offline.ProfessionUI',
            'Zjcs.Official.HomeCountControlsUI']), indent=2) + '\n', 'utf8', newline='\n')
    receipt_path = ROOT / 'assets/reference/home-income-formula-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    # This initial input preceded the later auto-increase migration. Preserve
    # its original hash and identify the exact historical bytes instead of
    # relabeling the current generator as the old input.
    historical = 'scripts/prepare_home_auto_increase_runtime.py'
    revision = 'db9172b66980caa77304791edafe3890ab61f065'
    original = subprocess.check_output(['git', 'show', revision + ':' + historical], cwd=ROOT)
    if hashlib.sha256(original).hexdigest() != receipt['inputs'][historical]:
        raise ValueError('Original income input revision differs')
    receipt.setdefault('input_revisions', {})[historical] = revision
    source = '.tools/source-packages/current/884_156692/official-code/client-source/FurnitureBedDialog.cs'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    for name in ('PlayerHangUpDataExt', 'Player', 'FunctionType', 'FurnitureLevelRuleInfo',
                 'EnumExt', 'MapRegionInfoParser', 'ServerInfoManager', 'FunctionsDisabledData'):
        source = '.tools/source-packages/current/884_156692/official-code/common-source/Common/' + name + '.cs'
        receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    source = '.tools/source-packages/current/884_156692/official-code/client-source/HangUpUIUtils.cs'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    for name in ('client-source/UIRootManager.cs', 'client-source/UIRootGlobalModule.cs',
                 'client-source/UIRootScopeModule.cs', 'framework-source/Framework/CanvasGroupExtensions.cs'):
        source = '.tools/source-packages/current/884_156692/official-code/' + name
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
        'its pointer, scrolling, highlight and mask-close behavior are covered. Secondary acceleration '
        'has its separate production sequence below; activity-output details remain pending.')
    source = 'assets/reference/home-fast-hangup-native-source-manifest.json'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    source = 'assets/reference/home-hangup-windows-native-source-manifest.json'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    source = 'assets/reference/partner-confirm-dialog-source-manifest.json'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    receipt['income_reward_adapter'] = (
        'Selected hang_up_reward and reward_rule rows select all 259 reward/cost item definitions and 15 unique '
        'original image addresses; original ItemIconManager priority proves the default template for each. '
        'The original ignored-award list is retained. Actual cart repair/unlock/claim/drain and its original '
        'queue display use a real saved receipt; presentation never writes either save. General indexed '
        'equipment instances and detail popups remain separate; acceleration uses the production sequence below.')
    receipt['fast_reward_adapter'] = (
        'Free-use settings belong to the configured furniture; all unlocked members of its original HangUpIdList '
        'receive acceleration, including cart rows whose own FastHangUpId is -1. Free and paid income batches '
        'use the same selected count formulas. Free batches return exact committed output deltas and ledger '
        'serials; paid batches keep settled counts in each furniture, including counts above capacity, and '
        'defer random item selection until a manual claim. Older pending inventory and sealed transfers are '
        'excluded. Checks cover duplicate members, capacity bypass, daily limits, overflow rollback, disk '
        'reopen and manual bed/cart transfers. Extra reward '
        'handler request integration and cost recovery use the separate production owner; native progress/window integration has its own boundary below.')
    source = 'assets/reference/home-auto-increase-source-manifest.json'
    receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    receipt['fast_auto_increase_adapter'] = (
        'Original auto-increase extra-reward numeric/state branches run against the actual item wrapper. '
        'The original update-message timestamp remainder is consumed in the same local profile transaction. '
        'Checks cover elapsed/fraction/bonus inputs, natural-cap crossing, overflow and holding limit, activity '
        'offset, zero result and absent parameters; a real character stamina receipt is applied once and '
        'reopened from disk. The production acceleration owner now attaches the selected extra handlers.')
    receipt['fast_request_journal_adapter'] = (
        'A local pending operation snapshots selected definitions, numeric properties, duration, costs, '
        'extra-handler inputs and acceptance time before the character transaction. Free usage is reserved '
        'with the intent; normal reward completion requires the exact profile receipt. Reopen, detached '
        'snapshots, duplicate completion, unpaid cancellation and whole-batch overflow rollback are checked. '
        'Production free/paid routing and sleep-progress presentation use the separate owners below.')
    for source in (
        '.tools/source-packages/current/884_156692/official-code/common-source/PlayerServiceProxyImpl.cs',
        '.tools/source-packages/current/884_156692/official-code/common-source/Common/TreatmentHangUpRewardHandler.cs',
        '.tools/source-packages/current/884_156692/official-code/common-source/Common/AutoIncreaseItemOutputAdditionUtils.cs'):
        receipt['inputs'][source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    receipt['fast_request_owner_adapter'] = (
        'Production free/item requests stage one home intent, commit actual character costs and original '
        'extra-handler effects once, then complete ordinary output against its exact character receipt. '
        'PlayerServiceProxyImpl.OperPlayerFastHangUpCallback supplies IsFree as hasOutputAddition, loops '
        'every configured extra and rewinds bed random reward timers by FastHangUpHours seconds exactly. '
        'Actual recovery after the character phase is exercised by reopening home; native selection '
        'and sleep-progress integration are recorded separately below.')
    receipt['extra_reward_preview_adapter'] = (
        'Original HangUpUtils.CalcRewardCount resolves an optional furniture level row. Missing rows '
        'use LevelNum=1 unless both rule ID and level are positive, which logs and returns zero. '
        'Actual fruit/stamina extra rows (-1) and the level-zero call exercise this branch using '
        'selected hourly output, original property arithmetic and both base/normal previews.')
    receipt['native_fast_sequence_adapter'] = (
        'The original bed and quantity selection route through the original OperFastHangUp helper to '
        'the actual recoverable request owner. Paid ordinary output remains in each furniture settled '
        'count, including output above capacity, until a manual claim. Free ordinary output stays in '
        'its durable ledger until the original sleep callback; its disposable hold releases on teardown. '
        'The original progress prefab, saved player appearance, sleep override, bubble, reward images '
        'and legacy animation events run with a sibling canvas while the main HUD follows original '
        'reference-counted CanvasGroup visibility. The bed environment signal has a production event '
        'boundary; original rapid day-cycle subscription remains to be migrated. Item-detail popups '
        'and full HUD restoration remain outside this sequence.')
    receipt['validation_inputs'] = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in (
        'scripts/prepare_home_income_formula_validation.py', 'scripts/fixtures/home_income_formula_validation.cs',
        'scripts/fixtures/home_furniture_host_validation.cs', 'scripts/bind_home_fast_audio.py')}
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
