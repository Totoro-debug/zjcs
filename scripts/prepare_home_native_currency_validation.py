"""Exercise the actual home currency owner with isolated saves and pointer events."""
import json
import shutil
import hashlib
from pathlib import Path
from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project

def prepare():
    work=validation_directory(); project=require_temporary_unity_project(work/'unity')
    folder=project/'Assets/HomeNativeCurrencyValidation'; folder.mkdir(exist_ok=True)
    source=(ROOT/'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source=source.replace('HomeFurnitureHostValidation','HomeNativeCurrencyValidation').replace('HomeFurnitureHostRunner','HomeNativeCurrencyRunner')
    source=source.replace('public static class HomeNativeCurrencyValidation','public static partial class HomeNativeCurrencyValidation').replace('public sealed class HomeNativeCurrencyRunner','public sealed partial class HomeNativeCurrencyRunner')
    source=source.replace('EditorApplication.EnterPlaymode();','ConfigureNativeFrame();\n        EditorApplication.EnterPlaymode();')
    source=source.replace('Zjcs.HomeFurnitureHost.Active','Zjcs.HomeNativeCurrency.Active')
    source=source.replace('SessionState.SetBool(Active, true);','SessionState.SetBool(\"Zjcs.HomeFurnitureHost.Active\", false);\n        SessionState.SetBool(Active, true);')
    source=source.replace('AudioListener.volume = 0;','Application.runInBackground = true;\n        AudioListener.volume = 0;')
    source=source.replace('+ 240);','+ 600);')
    source=source.replace('var checks = Check();','var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source=source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }','try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source=source.replace('            yield return current;','            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    a=source.index('        Require(home.Rules.FurnitureVersion'); b=source.index('    private void Open()',a)
    source=source[:a]+'        yield return CheckFormalCurrency();\n    }\n'+source[b:]
    source=source.replace('home.Scene.Camera.targetTexture = target;','home.Scene.Camera.targetTexture = null;\n        home.Scene.Camera.gameObject.AddComponent<AudioListener>();')
    source=source.replace('target.height - 40','target.height - 160')
    source=source.replace('private void Capture(string name)\n    {','private void Capture(string name)\n    {\n        home.Scene.Camera.targetTexture = target;')
    source=source.replace('        Save(name);','        Save(name);\n        home.Scene.Camera.targetTexture = null;')
    source=source.replace('        CloseCapture();\n        HomeNativeCurrencyValidation.Finish','        CloseCapture();\n        yield return null;\n        HomeNativeCurrencyValidation.Finish')
    source=source.replace('HOME_FURNITURE_HOST_PASSED','HOME_NATIVE_CURRENCY_PASSED')
    source=source.replace('actual home entry, simulated scene and native repair input, four-state binding and saved restoration.','actual home currency pointer entry, purchase, exchange, masks and saved restoration.')
    source=source.replace('"Repair button is top UI hit"','"Button is top UI hit: " + button.name + "; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name))')
    (folder/'HomeNativeCurrencyRunner.cs').write_text('#if UNITY_EDITOR\n'+source+'\n#endif\n','utf8',newline='\n')
    config=(ROOT/'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split('public sealed partial class HomeIncomeFormulaHostRunner')[0]
    config=config[config.index('public static partial class HomeIncomeFormulaValidation'):]
    config='using System;\nusing System.Reflection;\nusing UnityEngine;\n'+config.replace('HomeIncomeFormulaValidation','HomeNativeCurrencyValidation')
    (folder/'NativeFrame.cs').write_text('#if UNITY_EDITOR\n'+config+'\n#endif\n','utf8',newline='\n')
    shutil.copyfile(ROOT/'scripts/fixtures/home_native_currency_validation.cs',folder/'CurrencyChecks.cs')
    references=['Unity.ugui','Zjcs.Offline.Home','Zjcs.Official.HomeUI','Zjcs.Official.ProfessionUI','Zjcs.Offline.CharacterCreation','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Presentation.Home','Zjcs.Offline.HomeFunctionMenuUI','Zjcs.Official.HomeFunctionMenuUI','Zjcs.Official.WorldUI','Unity.TextMeshPro','Zjcs.Offline.HomeUI','Zjcs.Offline.HomeCurrencyUI','Zjcs.Official.HomeCurrencyWindowsUI','Zjcs.Official.HomeHudUI','Zjcs.Offline.ProfessionUI','Zjcs.Official.HomeCountControlsUI','Zjcs.Official.World.NativeLabelUI']
    (folder/'Zjcs.HomeNativeCurrencyValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeNativeCurrencyValidation',references=references),indent=2)+'\n','utf8')
    paths=['HomeAcceptanceDriver.cs','HomeHudView.Skins.cs','HomeHudView.cs','HomeHudView.Settings.cs','HomeHudView.NativeFunctionMenu.cs','HomeHudView.NativeFunctionMenu.cs.meta','HomeHudView.NativeCurrency.cs','HomeHudView.NativeCurrency.cs.meta','HomeHudView.NativeCurrencyIcons.cs','HomeHudView.NativeCurrencyIcons.cs.meta','Zjcs.Presentation.Home.asmdef']
    for name in paths:
        relative=Path('unity/Assets/Game/Runtime/Presentation/Home')/name
        shutil.copyfile(ROOT/relative,work/relative)
    relative=Path('unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeTopBar.cs')
    shutil.copyfile(ROOT/relative,work/relative)
    relative=Path('unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyAssets.cs')
    shutil.copyfile(ROOT/relative,work/relative)
    for relative in [Path('unity/Assets/Game/Resources/Home')/name for name in ('home-mainline-task-data.json','home-mainline-task-data-hash.json')]+[Path('unity/Assets/Game/Resources/Official/Progression')/name for name in ('mainline-tasks.json','mainline-tasks-source-manifest.json')]:
        shutil.copyfile(ROOT/relative,work/relative)
    relative=Path('unity/Assets/Official/Partners/UI/Card/Converted/ac573a40fda4c096.asset')
    shutil.copyfile(ROOT/relative,work/relative)
    inputs=['scripts/prepare_home_native_currency_validation.py','scripts/fixtures/home_native_currency_validation.cs','scripts/fixtures/home_furniture_host_validation.cs','scripts/fixtures/home_income_formula_validation.cs',
        'assets/reference/home-currency-windows-runtime-source-manifest.json','assets/reference/home-currency-actions-runtime-source-manifest.json',
        'assets/reference/home-currency-windows-native-source-manifest.json','unity/Assets/StreamingAssets/Home/ui.json',
        'unity/Assets/Game/Resources/Home/home-mainline-task-data.json','unity/Assets/Game/Resources/Home/home-mainline-task-data-hash.json']
    official='.tools/source-packages/current/884_156692/official-code/'
    inputs += [official+name for name in ['client-source/HomeUI.cs','client-source/TopBarHandler.cs','client-source/QuickBuyDialog.cs','client-source/PayMoneyChangeDialog.cs','common-source/Common/Player.cs','framework-firstpass-source/Framework.UI/WindowContainer.cs']]
    bindings=['unity/Assets/Game/Runtime/Presentation/Home/'+name for name in paths]
    bindings += ['unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeTopBar.cs','unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyAssets.cs']
    native=ROOT/'assets/reference/home-top-bar-native-source-manifest.json'
    catalog=json.loads(native.read_bytes())
    catalog['runtime_code']['unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeTopBar.cs']=hashlib.sha256((ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeTopBar.cs').read_bytes()).hexdigest()
    native.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    currency_native=ROOT/'assets/reference/home-currency-windows-native-source-manifest.json'
    currency_proof=json.loads(currency_native.read_bytes())
    asset_code='unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyAssets.cs'
    currency_proof['offline_output_sha256'][asset_code]=hashlib.sha256((ROOT/asset_code).read_bytes()).hexdigest()
    currency_native.write_text(json.dumps(currency_proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    proof=dict(manifest='884_156692',inputs={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in inputs},runtime_bindings={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in bindings})
    for name in bindings:
        if (ROOT/name).read_bytes() != (work/name).read_bytes(): raise ValueError('Runtime overlay differs: '+name)
    (ROOT/'assets/reference/home-currency-host-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged actual home native currency validation')

if __name__=='__main__': prepare()
