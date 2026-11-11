"""Transfer the native entry subtree and stage its isolated pointer/lifecycle fixture."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_card_native import guid,sha
from prepare_home_bottom_bar_validation import equivalent
from prepare_home_currency_portrait_validation import stage as stage_portrait
from validation_workspace import validation_directory


def transfer(work,project,install=False):
    receipt=json.loads((work/'home-function-entry-native-source-manifest.json').read_bytes())
    assert receipt['generator_sha256']==sha((ROOT/'scripts/prepare_home_function_entry_native.py').read_bytes())
    for relative,expected in receipt['inputs'].items():assert sha((ROOT/relative).read_bytes())==expected,relative
    for row in receipt['script_bindings'].values():
        source=ROOT/row['path'];assert sha(source.read_bytes())==row['sha256'],source
        assert sha(Path(str(source)+'.meta').read_bytes())==row['meta_sha256'],source
    keys={guid(work/'gift-assets'/name) for name in receipt['outputs'] if name.endswith('.meta')}
    result=subprocess.run(['rg','--files-with-matches','--null','--no-ignore','--glob','*.meta',
        '-e','^guid: ('+'|'.join(sorted(keys))+')\r?$',str(project/'Assets')],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if result.returncode not in (0,1):raise RuntimeError(result.stderr.decode('utf8','replace'))
    existing={}
    for value in result.stdout.decode('utf8').split('\0'):
        if not value:continue
        meta=Path(value);key=guid(meta);assert key not in existing,key
        existing[key]=Path(str(meta)[:-5])
    destination=project/'Assets/Official/Home/UI/FunctionEntry'
    outputs,reused,plan={},{},[]
    for relative,expected in receipt['outputs'].items():
        source=work/'gift-assets'/relative;assert sha(source.read_bytes())==expected,relative
        if source.suffix=='.meta':continue
        key=guid(Path(str(source)+'.meta'));target=existing.get(key,destination/relative)
        if target.exists():
            assert equivalent(source.read_bytes(),target.read_bytes()),target
            reused[target.relative_to(project).as_posix()]=sha(target.read_bytes())
        else:plan.extend(((source,target),(Path(str(source)+'.meta'),Path(str(target)+'.meta'))))
        if key==receipt['roots']['HomeFunctionEntry']['guid']:prefab_path=target.relative_to(project).as_posix()
        for item in (target,Path(str(target)+'.meta')):
            staged=source if item==target else Path(str(source)+'.meta')
            if item.is_relative_to(destination):outputs[item.relative_to(project).as_posix()]=sha(item.read_bytes()) if item.exists() else sha(staged.read_bytes())
    for source,target in plan:target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        meta=Path(str(folder)+'.meta')
        if not meta.exists():
            key=hashlib.md5(('zjcs:home-function-entry-folder:'+folder.relative_to(project).as_posix()).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[meta.relative_to(project).as_posix()]=sha(meta.read_bytes())
    if install:
        receipt['installed_output_sha256']={'unity/'+path:value for path,value in outputs.items()}
        receipt['reused_output_sha256']={'unity/'+path:value for path,value in reused.items() if path not in outputs}
        receipt['binding_generator_sha256']=sha(Path(__file__).read_bytes())
        receipt['binding_inputs']={}
        for name in ('prepare_home_bottom_bar_validation.py','fixtures/home_function_entry_native_validation.cs'):
            source=ROOT/'scripts'/name;receipt['binding_inputs'][source.relative_to(ROOT).as_posix()]=sha(source.read_bytes())
        (ROOT/'assets/reference/home-function-entry-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original function-entry transfer:',len(outputs),'outputs;',len(reused),'existing',flush=True)
    return prefab_path


def stage(work,project,top_bar_work):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    stage_portrait(top_bar_work,project)
    prefab_path=transfer(work,project)
    proof=json.loads((ROOT/'assets/reference/home-main-ui-controls-source-manifest.json').read_bytes())
    for relative,expected in proof['outputs'].items():
        source=ROOT/relative;assert sha(source.read_bytes())==expected,source
        target=project/source.relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    source=(project/'Assets/HomeCurrencyPortraitValidation/HomeCurrencyPortraitValidation.cs').read_text('utf8')
    for before,after in [('HomeCurrencyPortrait','HomeFunctionEntryNative'),('HOME_CURRENCY_PORTRAIT_PASSED','HOME_FUNCTION_ENTRY_NATIVE_PASSED'),
        ('currency-portrait-profile','function-entry-profile'),('home-currency-portrait','home-function-entry-currency')]:source=source.replace(before,after)
    source=source.replace('public sealed class HomeFunctionEntryNativeRunner','public sealed partial class HomeFunctionEntryNativeRunner')
    source=source.replace('        view.Dispose();view=null;yield return null;',
        '        var nativeEntry=VerifyNativeEntry();\n        while(nativeEntry.MoveNext())yield return nativeEntry.Current;\n        view.Dispose();view=null;yield return null;')
    source=source.replace('Full production HUD and detail windows remain separate.',
        'Native function-entry pointer dispatch, original animation events, condition observers and tip visibility passed. Full FunctionEntryDialog and production HUD integration remain separate.')
    start=source.index('public sealed partial class HomeFunctionEntryNativeRunner');imports=source[:source.index('[InitializeOnLoad]')]
    controller=source[:start].replace('new GameObject("RealProfileHomeCurrencies").AddComponent<HomeFunctionEntryNativeRunner>();',
        '{HomeFunctionEntryNativeRunner.Completed=Finish;new GameObject("RealProfileHomeCurrencies").AddComponent<HomeFunctionEntryNativeRunner>();}')
    controller=controller.replace('    public static void Run()',
        '    public static void Run()\n    {\n        HomeFunctionEntryNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("'+prefab_path+'");\n        RunPrepared();\n    }\n    public static void RunPrepared()')
    # Domain reload clears static references; load the prefab in the entered-play callback.
    controller=controller.replace('HomeFunctionEntryNativeRunner.Completed=Finish;',
        'HomeFunctionEntryNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("'+prefab_path+'");HomeFunctionEntryNativeRunner.Completed=Finish;')
    runner=imports.replace('using UnityEditor;\n','').replace('using UnityEditor.SceneManagement;\n','')+source[start:]
    runner=runner.replace('    sealed class TargetMetrics:', '    public static Action<Exception> Completed;\n    public static GameObject Prefab;\n    sealed class TargetMetrics:')
    runner=runner.replace('HomeFunctionEntryNativeValidation.Finish(', 'Completed(')
    folder=project/'Assets/HomeFunctionEntryNativeValidation';editor=folder/'Editor';editor.mkdir(parents=True,exist_ok=True)
    (editor/'HomeFunctionEntryNativeValidation.cs').write_text(controller,'utf8',newline='\n')
    (folder/'HomeFunctionEntryNativeRunner.cs').write_text(runner,'utf8',newline='\n')
    shutil.copy2(ROOT/'scripts/fixtures/home_function_entry_native_validation.cs',folder/'HomeFunctionEntryNativeRunner.Entry.cs')
    asmdef=json.loads((project/'Assets/HomeCurrencyPortraitValidation/HomeCurrencyPortraitValidation.asmdef').read_bytes())
    asmdef['name']='Zjcs.HomeFunctionEntryNativeValidation';asmdef['references']+=['Zjcs.Official.HomeMainUiControls','Zjcs.Official.PartnersUI','Zjcs.Official.WorldUI']
    asmdef.pop('includePlatforms',None)
    (folder/'HomeFunctionEntryNativeValidation.asmdef').write_text(json.dumps(asmdef,indent=2)+'\n','utf8',newline='\n')
    editor_asmdef=dict(name=asmdef['name']+'.Editor',references=asmdef['references']+[asmdef['name']],includePlatforms=['Editor'])
    (editor/'HomeFunctionEntryNativeValidation.Editor.asmdef').write_text(json.dumps(editor_asmdef,indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--source-work',type=Path,required=True)
    parser.add_argument('--project',type=Path);parser.add_argument('--top-bar-work',type=Path);parser.add_argument('--install',action='store_true')
    args=parser.parse_args()
    transfer(args.source_work,ROOT/'unity',True) if args.install else stage(args.source_work,args.project,args.top_bar_work)
