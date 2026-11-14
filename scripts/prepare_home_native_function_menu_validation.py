"""Exercise the actual home menu owner with isolated saves and pointer events."""
import json
import shutil
import hashlib
from pathlib import Path
from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project

def prepare():
    work=validation_directory(); project=require_temporary_unity_project(work/'unity')
    folder=project/'Assets/HomeNativeFunctionMenuValidation'; folder.mkdir(exist_ok=True)
    source=(ROOT/'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source=source.replace('HomeFurnitureHostValidation','HomeNativeFunctionMenuValidation').replace('HomeFurnitureHostRunner','HomeNativeFunctionMenuRunner')
    source=source.replace('public static class HomeNativeFunctionMenuValidation','public static partial class HomeNativeFunctionMenuValidation').replace('public sealed class HomeNativeFunctionMenuRunner','public sealed partial class HomeNativeFunctionMenuRunner')
    source=source.replace('EditorApplication.EnterPlaymode();','ConfigureNativeFrame();\n        EditorApplication.EnterPlaymode();')
    source=source.replace('Zjcs.HomeFurnitureHost.Active','Zjcs.HomeNativeFunctionMenu.Active')
    source=source.replace('SessionState.SetBool(Active, true);','SessionState.SetBool(\"Zjcs.HomeFurnitureHost.Active\", false);\n        SessionState.SetBool(Active, true);')
    source=source.replace('AudioListener.volume = 0;','Application.runInBackground = true;\n        AudioListener.volume = 0;')
    source=source.replace('+ 240);','+ 600);')
    source=source.replace('var checks = Check();','var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source=source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }','try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source=source.replace('            yield return current;','            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    a=source.index('        Require(home.Rules.FurnitureVersion'); b=source.index('    private void Open()',a)
    source=source[:a]+'        yield return CheckFormalMenu();\n    }\n'+source[b:]
    source=source.replace('home.Scene.Camera.targetTexture = target;','home.Scene.Camera.targetTexture = null;\n        home.Scene.Camera.gameObject.AddComponent<AudioListener>();')
    source=source.replace('private void Capture(string name)\n    {','private void Capture(string name)\n    {\n        home.Scene.Camera.targetTexture = target;')
    source=source.replace('        Save(name);','        Save(name);\n        home.Scene.Camera.targetTexture = null;')
    source=source.replace('        CloseCapture();\n        HomeNativeFunctionMenuValidation.Finish','        CloseCapture();\n        yield return null;\n        HomeNativeFunctionMenuValidation.Finish')
    source=source.replace('HOME_FURNITURE_HOST_PASSED','HOME_NATIVE_FUNCTION_MENU_PASSED')
    source=source.replace('actual home entry, simulated scene and native repair input, four-state binding and saved restoration.','actual home menu pointer entry, conditions, page dispatch, animated close and saved restoration.')
    source=source.replace('"Repair button is top UI hit"','"Button is top UI hit: " + button.name + "; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name))')
    (folder/'HomeNativeFunctionMenuRunner.cs').write_text('#if UNITY_EDITOR\n'+source+'\n#endif\n','utf8',newline='\n')
    config=(ROOT/'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split('public sealed partial class HomeIncomeFormulaHostRunner')[0]
    config=config[config.index('public static partial class HomeIncomeFormulaValidation'):]
    config='using System;\nusing System.Reflection;\nusing UnityEngine;\n'+config.replace('HomeIncomeFormulaValidation','HomeNativeFunctionMenuValidation')
    (folder/'NativeFrame.cs').write_text('#if UNITY_EDITOR\n'+config+'\n#endif\n','utf8',newline='\n')
    shutil.copyfile(ROOT/'scripts/fixtures/home_native_function_menu_validation.cs',folder/'MenuChecks.cs')
    references=['Unity.ugui','Zjcs.Offline.Home','Zjcs.Official.HomeUI','Zjcs.Official.ProfessionUI','Zjcs.Offline.CharacterCreation','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Presentation.Home','Zjcs.Offline.HomeFunctionMenuUI','Zjcs.Official.HomeFunctionMenuUI','Zjcs.Official.WorldUI','Unity.TextMeshPro']
    (folder/'Zjcs.HomeNativeFunctionMenuValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeNativeFunctionMenuValidation',references=references),indent=2)+'\n','utf8')
    paths=['HomeAcceptanceDriver.cs','HomeHudView.Skins.cs','HomeHudView.cs','HomeHudView.Settings.cs','HomeHudView.NativeFunctionMenu.cs','HomeHudView.NativeFunctionMenu.cs.meta','Zjcs.Presentation.Home.asmdef']
    for name in paths:
        relative=Path('unity/Assets/Game/Runtime/Presentation/Home')/name
        shutil.copyfile(ROOT/relative,work/relative)
    for relative in [Path('unity/Assets/Game/Resources/Home')/name for name in ('home-mainline-task-data.json','home-mainline-task-data-hash.json')]+[Path('unity/Assets/Game/Resources/Official/Progression')/name for name in ('mainline-tasks.json','mainline-tasks-source-manifest.json')]:
        shutil.copyfile(ROOT/relative,work/relative)
    relative=Path('unity/Assets/Official/Partners/UI/Card/Converted/ac573a40fda4c096.asset')
    shutil.copyfile(ROOT/relative,work/relative)
    inputs=['scripts/prepare_home_native_function_menu_validation.py','scripts/fixtures/home_native_function_menu_validation.cs','scripts/fixtures/home_furniture_host_validation.cs','scripts/fixtures/home_income_formula_validation.cs',
        'assets/reference/home-function-menu-owner-source-manifest.json','assets/reference/home-function-menu-player-source-manifest.json','assets/reference/home-function-menu-configuration-source-manifest.json',
        'unity/Assets/Game/Resources/Home/home-mainline-task-data.json','unity/Assets/Game/Resources/Home/home-mainline-task-data-hash.json']
    official='.tools/source-packages/current/884_156692/official-code/'
    inputs += [official+name for name in ['client-source/FunctionEntryTaskBtn.cs','client-source/TaskPanelNavigateHandler.cs','client-source/SettingsBtn.cs','client-source/HomeSkinBtn.cs','framework-firstpass-source/Framework.UI/WindowContainer.cs']]
    bindings=['unity/Assets/Game/Runtime/Presentation/Home/'+name for name in paths]
    proof=dict(manifest='884_156692',inputs={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in inputs},runtime_bindings={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in bindings})
    for name in bindings:
        if (ROOT/name).read_bytes() != (work/name).read_bytes(): raise ValueError('Runtime overlay differs: '+name)
    (ROOT/'assets/reference/home-function-menu-host-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged actual home native function menu validation')

if __name__=='__main__': prepare()
