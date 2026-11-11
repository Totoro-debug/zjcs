"""Bind the original screen-frame resources and stage their Play Mode fixture."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import yaml

from official_apk import ROOT
from prepare_partner_card_native import guid, sha, Dumper
from prepare_home_bottom_bar_validation import equivalent
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def transfer(work, project, install=False):
    receipt=json.loads((work/'home-screen-native-source-manifest.json').read_bytes())
    assert receipt['generator_sha256']==sha((ROOT/'scripts/prepare_home_screen_native.py').read_bytes())
    for relative,expected in receipt['inputs'].items():
        assert sha((ROOT/relative).read_bytes())==expected,relative
    for row in receipt['script_bindings'].values():
        path=ROOT/row['path']
        assert sha(path.read_bytes())==row['sha256'] and sha(Path(str(path)+'.meta').read_bytes())==row['meta_sha256'],path
    existing={}
    keys={guid(work/'screen-assets'/relative) for relative in receipt['outputs'] if relative.endswith('.meta')}
    keys.add(hashlib.md5(b'zjcs:home-screen-catalog').hexdigest())
    found=subprocess.run(['rg','--files-with-matches','--null','--no-ignore','--glob','*.meta',
        '-e','^guid: ('+'|'.join(sorted(keys))+')\r?$',str(project/'Assets')],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if found.returncode not in (0,1):raise RuntimeError(found.stderr.decode('utf8','replace'))
    for item in found.stdout.decode('utf8').split('\0'):
        if not item:continue
        meta=Path(item)
        source=Path(str(meta)[:-5])
        if source.is_file():
            key=guid(meta)
            assert key not in existing, key
            existing[key]=source
    destination=project/'Assets/Official/Home/UI/ScreenFrame'
    outputs,reused,plan={},{},[]
    for relative,expected in receipt['outputs'].items():
        source=work/'screen-assets'/relative
        assert sha(source.read_bytes())==expected,relative
        if source.suffix=='.meta':continue
        key=guid(Path(str(source)+'.meta'))
        target=existing.get(key,destination/relative)
        if target.exists():
            assert equivalent(source.read_bytes(),target.read_bytes()),target
            reused[target.relative_to(project).as_posix()]=sha(target.read_bytes())
        else:
            plan.extend(((source,target),(Path(str(source)+'.meta'),Path(str(target)+'.meta'))))
        existing[key]=target
        if target.is_relative_to(destination):
            for item in (target,Path(str(target)+'.meta')):
                staged=source if item==target else Path(str(source)+'.meta')
                outputs[item.relative_to(project).as_posix()]=sha(item.read_bytes()) if item.exists() else sha(staged.read_bytes())
    for source,target in plan:
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    script=ROOT/'unity/Assets/Game/Runtime/Official/HomeScreenUI/OfficialHomeScreenAssetCatalog.cs'
    catalog=project/'Assets/Game/Resources/Home/OfficialHomeScreenAssetCatalog.asset'
    fields=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3),
        m_Name='OfficialHomeScreenAssetCatalog',m_EditorClassIdentifier='',framePrefab=receipt['root'])
    raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        +yaml.dump(dict(MonoBehaviour=fields),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode('utf8')
    assert not catalog.exists() or catalog.read_bytes()==raw,'Preserve different screen catalog'
    catalog.parent.mkdir(parents=True,exist_ok=True);catalog.write_bytes(raw)
    meta=Path(str(catalog)+'.meta')
    raw_meta=('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-screen-catalog').hexdigest()
        +'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode('utf8')
    assert not meta.exists() or meta.read_bytes()==raw_meta,'Preserve different screen catalog identity'
    meta.write_bytes(raw_meta)
    for item in (catalog,meta):outputs[item.relative_to(project).as_posix()]=sha(item.read_bytes())
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        meta=Path(str(folder)+'.meta')
        if not meta.exists():
            key=hashlib.md5(('zjcs:home-screen-folder:'+folder.relative_to(project).as_posix()).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[meta.relative_to(project).as_posix()]=sha(meta.read_bytes())
    if install:
        receipt['installed_output_sha256']={'unity/'+path:value for path,value in outputs.items()}
        receipt['reused_output_sha256']={'unity/'+path:value for path,value in reused.items() if path not in outputs}
        receipt['binding_generator_sha256']=sha(Path(__file__).read_bytes())
        receipt['binding_inputs']={}
        for name in ('prepare_partner_card_native.py','prepare_home_bottom_bar_validation.py',
                     'fixtures/home_screen_native_validation.cs'):
            source=ROOT/'scripts'/name
            receipt['binding_inputs'][source.relative_to(ROOT).as_posix()]=sha(source.read_bytes())
        for name in ('UIUtils','EUIInstantiateFlag'):
            source=ROOT/'.tools/source-packages/current/884_156692/official-code/client-source'/(name+'.cs')
            receipt['binding_inputs'][source.relative_to(ROOT).as_posix()]=sha(source.read_bytes())
        for source in (script,Path(str(script)+'.meta'),ROOT/'assets/reference/home-screen-runtime-source-manifest.json'):
            receipt['binding_inputs'][source.relative_to(ROOT).as_posix()]=sha(source.read_bytes())
        (ROOT/'assets/reference/home-screen-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original screen-frame transfer:',len(outputs),'files;',len(reused),'existing assets',flush=True)


def stage(work,project):
    assert project.resolve().is_relative_to(validation_directory().resolve()),'Owned isolated project required.'
    transfer(work,project)
    stage_runtime_modules(project,ROOT/'unity/Assets/Game/Runtime',['Zjcs.Official.HomeScreenUI'])
    folder=project/'Assets/HomeScreenValidation';folder.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_screen_native_validation.cs',folder/'HomeScreenNativeValidation.cs')
    (folder/'HomeScreenValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeScreenValidation',references=['Zjcs.Official.HomeScreenUI','Unity.ugui'],includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--source-work',type=Path,required=True)
    parser.add_argument('--project',type=Path)
    parser.add_argument('--install',action='store_true')
    args=parser.parse_args()
    if args.install:transfer(args.source_work,ROOT/'unity',True)
    else:stage(args.source_work,args.project)
