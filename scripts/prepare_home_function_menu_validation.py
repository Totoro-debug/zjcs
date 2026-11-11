"""Stage the complete native menu and its isolated pointer/lifecycle fixture."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_card_native import guid,sha
from prepare_home_bottom_bar_validation import equivalent
from prepare_home_function_entry_validation import stage as stage_entry
from validation_workspace import validation_directory


def transfer(work,project,install=False):
    receipt=json.loads((work/'home-function-menu-native-source-manifest.json').read_bytes())
    assert receipt['generator_sha256']==sha((ROOT/'scripts/prepare_home_function_menu_native.py').read_bytes())
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
    destination=project/'Assets/Official/Home/UI/FunctionMenu'
    outputs,reused,plan={},{},[]
    for relative,expected in receipt['outputs'].items():
        source=work/'gift-assets'/relative;assert sha(source.read_bytes())==expected,relative
        if source.suffix=='.meta':continue
        key=guid(Path(str(source)+'.meta'));target=existing.get(key,destination/relative)
        if target.exists():
            assert equivalent(source.read_bytes(),target.read_bytes()),target
            reused[target.relative_to(project).as_posix()]=sha(target.read_bytes())
        else:plan.extend(((source,target),(Path(str(source)+'.meta'),Path(str(target)+'.meta'))))
        if key==receipt['roots']['FunctionEntryDialog']['guid']:prefab_path=target.relative_to(project).as_posix()
        for item in (target,Path(str(target)+'.meta')):
            staged=source if item==target else Path(str(source)+'.meta')
            if item.is_relative_to(destination):outputs[item.relative_to(project).as_posix()]=sha(item.read_bytes()) if item.exists() else sha(staged.read_bytes())
    for source,target in plan:target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        meta=Path(str(folder)+'.meta')
        if not meta.exists():
            key=hashlib.md5(('zjcs:home-function-menu-folder:'+folder.relative_to(project).as_posix()).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[meta.relative_to(project).as_posix()]=sha(meta.read_bytes())
    if install:
        receipt['installed_output_sha256']={'unity/'+path:value for path,value in outputs.items()}
        receipt['reused_output_sha256']={'unity/'+path:value for path,value in reused.items() if path not in outputs}
        receipt['binding_generator_sha256']=sha(Path(__file__).read_bytes())
        source=ROOT/'scripts/fixtures/home_function_menu_native_validation.cs'
        receipt['binding_inputs']={source.relative_to(ROOT).as_posix():sha(source.read_bytes())}
        (ROOT/'assets/reference/home-function-menu-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original function menu transfer:',len(outputs),'outputs;',len(reused),'existing',flush=True)
    return prefab_path


def stage(work,project,entry_work,top_bar_work):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    stage_entry(entry_work,project,top_bar_work)
    prefab=transfer(work,project)
    receipt=json.loads((ROOT/'assets/reference/home-function-menu-runtime-source-manifest.json').read_bytes())
    for relative,expected in receipt['outputs'].items():
        source=ROOT/relative;assert sha(source.read_bytes())==expected,source
        target=project/source.relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    previous=project/'Assets/HomeFunctionEntryNativeValidation';folder=project/'Assets/HomeFunctionMenuNativeValidation'
    for relative in ('Editor/HomeFunctionEntryNativeValidation.cs','HomeFunctionEntryNativeRunner.cs','HomeFunctionEntryNativeRunner.Entry.cs'):
        source=(previous/relative).read_text('utf8').replace('HomeFunctionEntryNative','HomeFunctionMenuNative').replace('HOME_FUNCTION_ENTRY_NATIVE','HOME_FUNCTION_MENU_NATIVE').replace('home-function-entry','home-function-menu').replace('function-entry-profile','function-menu-profile')
        if relative.endswith('Runner.cs'):
            source=source.replace('public static GameObject Prefab;','public static GameObject Prefab;\n    public static GameObject MenuPrefab;')
            source=source.replace('while(nativeEntry.MoveNext())yield return nativeEntry.Current;',
                'while(nativeEntry.MoveNext())yield return nativeEntry.Current;\n        var menu=VerifyNativeMenu();while(menu.MoveNext())yield return menu.Current;')
        if relative.startswith('Editor/'):
            source=source.replace('Full FunctionEntryDialog and production HUD integration remain separate.',
                'Complete FunctionEntryDialog native hierarchy, pointer dispatch, popup animation/audio and lifecycle cleanup passed with controlled service endpoints. Production feature hosts and formal HUD integration remain separate.')
            source=source.replace('HomeFunctionMenuNativeRunner.Completed=Finish;',
                'HomeFunctionMenuNativeRunner.MenuPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("'+prefab+'");HomeFunctionMenuNativeRunner.Completed=Finish;')
        target=folder/relative.replace('HomeFunctionEntryNative','HomeFunctionMenuNative');target.parent.mkdir(parents=True,exist_ok=True);target.write_text(source,'utf8',newline='\n')
    shutil.copy2(ROOT/'scripts/fixtures/home_function_menu_native_validation.cs',folder/'HomeFunctionMenuNativeRunner.Menu.cs')
    asmdef=json.loads((previous/'HomeFunctionEntryNativeValidation.asmdef').read_bytes())
    asmdef['name']='Zjcs.HomeFunctionMenuNativeValidation';asmdef['references']+=['Zjcs.Official.HomeFunctionMenuUI','Zjcs.Official.HomePlayerPanelUI','UniTask']
    (folder/'HomeFunctionMenuNativeValidation.asmdef').write_text(json.dumps(asmdef,indent=2)+'\n','utf8',newline='\n')
    editor=dict(name=asmdef['name']+'.Editor',references=asmdef['references']+[asmdef['name']],includePlatforms=['Editor'])
    (folder/'Editor/HomeFunctionMenuNativeValidation.Editor.asmdef').write_text(json.dumps(editor,indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--source-work',type=Path,required=True)
    parser.add_argument('--project',type=Path);parser.add_argument('--entry-work',type=Path);parser.add_argument('--top-bar-work',type=Path);parser.add_argument('--install',action='store_true')
    args=parser.parse_args()
    transfer(args.source_work,ROOT/'unity',True) if args.install else stage(args.source_work,args.project,args.entry_work,args.top_bar_work)
