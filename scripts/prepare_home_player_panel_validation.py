"""Install the complete native player panel with verified GUID reuse."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import yaml

from official_apk import ROOT
from prepare_home_bottom_bar_validation import equivalent
from prepare_partner_card_native import Dumper, guid, sha
from prepare_partner_input_dialog import metadata
from validation_assemblies import stage_runtime_modules
from validation_workspace import require_temporary_unity_project, validation_directory


def transfer(work, project, *, install=False):
    if not install: project=require_temporary_unity_project(project)
    receipt=json.loads((work/'home-player-panel-native-source-manifest.json').read_bytes())
    previous=ROOT/'assets/reference/home-player-panel-native-source-manifest.json'
    previous_outputs=json.loads(previous.read_bytes()).get('installed_output_sha256',{}) if install and previous.exists() else {}
    generator=ROOT/'scripts/prepare_home_player_panel_native.py'
    if sha(generator.read_bytes())!=receipt['generator_sha256']:raise ValueError('Regenerate original player panel')
    for relative,expected in {**receipt['inputs'],**receipt['runtime_code']}.items():
        if sha((ROOT/relative).read_bytes())!=expected:raise ValueError('Panel dependency changed: '+relative)
    for row in receipt['script_bindings'].values():
        path=ROOT/row['path']
        if sha(path.read_bytes())!=row['sha256'] or sha(Path(str(path)+'.meta').read_bytes())!=row['meta_sha256']:
            raise ValueError('Panel script changed: '+row['path'])
    destination=project/'Assets/Official/Home/UI/PlayerPanel'
    existing={}
    for meta in (project/'Assets').rglob('*.meta'):
        file=Path(str(meta)[:-5])
        if not file.is_file():continue
        key=guid(meta)
        if key in existing:raise ValueError('Existing GUID collision: '+key)
        existing[key]=file
    outputs,reused,plan={},[],[]
    for relative,expected in receipt['outputs'].items():
        source=work/'player-panel-assets'/relative
        if sha(source.read_bytes())!=expected:raise ValueError('Staged panel bytes changed: '+relative)
        if source.suffix=='.meta':continue
        target=existing.get(guid(Path(str(source)+'.meta')),destination/relative)
        if target.exists():
            if not equivalent(source.read_bytes(),target.read_bytes()):raise ValueError('Preserve different native resource: '+str(target))
            if not target.is_relative_to(destination):reused.append(dict(path=target.relative_to(ROOT if install else project).as_posix(),sha256=sha(target.read_bytes())))
        else:plan.extend(((source.read_bytes(),target),(Path(str(source)+'.meta').read_bytes(),Path(str(target)+'.meta'))))
        if target.is_relative_to(destination):
            for file,staged in ((target,source),(Path(str(target)+'.meta'),Path(str(source)+'.meta'))):
                outputs[file.relative_to(ROOT if install else project).as_posix()]=sha(file.read_bytes() if file.exists() else staged.read_bytes())
    context=destination/'original-parent-context.json'
    context_bytes=(json.dumps(dict(chain=receipt['original_parent_context']),ensure_ascii=False,indent=2)+'\n').encode()
    context_guid=hashlib.md5(b'zjcs:home-player-panel-parent-context').hexdigest()
    context_meta=('fileFormatVersion: 2\nguid: '+context_guid+'\nTextScriptImporter:\n  externalObjects: {}\n').encode()
    script=ROOT/'unity/Assets/Game/Runtime/Official/HomePlayerPanelUI/OfficialHomePlayerPanelAssetCatalog.cs'
    fields=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3),m_Name='OfficialHomePlayerPanelAssetCatalog',m_EditorClassIdentifier='',
        prefab=receipt['roots']['PublicPlayerInfo'],originalParentContext=dict(fileID=4900000,guid=context_guid,type=3),
        localizedTexts=dict(fileID=4900000,guid=guid(work/'player-panel-assets/home-player-panel-texts.json.meta'),type=3),
        statusPrefabs=[dict(guid=key,prefab=row['pointer']) for key,row in receipt['status_prefabs'].items()],
        sprites=[dict(guid=key,sprite=row['pointer']) for key,row in receipt['dynamic_sprites'].items()],
        sounds=[dict(eventId=row['event']['id'],clip=row['pointer']) for row in receipt['audio'].values()])
    catalog=project/'Assets/Game/Resources/Home/OfficialHomePlayerPanelAssetCatalog.asset'
    raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(dict(MonoBehaviour=fields),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode()
    raw_meta=('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-player-panel-catalog').hexdigest()+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode()
    for file,data in ((context,context_bytes),(Path(str(context)+'.meta'),context_meta),(catalog,raw),(Path(str(catalog)+'.meta'),raw_meta)):
        if file.exists() and file.read_bytes()!=data and (install or file!=catalog):
            relative=file.relative_to(ROOT if install else project).as_posix()
            if not install or previous_outputs.get(relative)!=sha(file.read_bytes()):
                raise ValueError('Preserve different original panel catalog: '+str(file))
        plan.append((data,file));outputs[file.relative_to(ROOT if install else project).as_posix()]=sha(data)
    for data,target in plan:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        file=metadata(folder,'DefaultImporter',True);outputs[file.relative_to(ROOT if install else project).as_posix()]=sha(file.read_bytes())
    if install:
        receipt.update(installed_output_sha256=outputs,reused=reused)
        (ROOT/'assets/reference/home-player-panel-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Complete player-panel transfer:',len(outputs),'files; reused',len(reused),flush=True)


def stage_fixture(project):
    stage_runtime_modules(project,ROOT/'unity/Assets/Game/Runtime',['Zjcs.Official.HomePlayerPanelUI',
        'Zjcs.Offline.HomeUI','Zjcs.Offline.CharacterCreation','Zjcs.Offline.ProfessionUI'])
    folder=project/'Assets/HomePlayerPanelValidation';folder.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/home_player_panel_native_validation.cs',folder/'HomePlayerPanelNativeValidation.cs')
    refs=['Zjcs.Official.HomePlayerPanelUI','Zjcs.Official.HomePlayerInfoUI','Zjcs.Official.HomeHudUI',
        'Zjcs.Offline.HomeUI','Zjcs.Offline.PartnersUI','Zjcs.Offline.ProfessionUI','Zjcs.Offline.Progression','Zjcs.Offline.CharacterCreation',
        'Zjcs.Offline.State','Zjcs.Official.Partners','Zjcs.Official.ProfessionUI','Zjcs.Official.TeamUI','Zjcs.Official.WorldUI',
        'Zjcs.Official.World.NativeLabelUI','Zjcs.Official.World.NativeText','Unity.TextMeshPro','Unity.ugui',
        'Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Core.Runtime']
    (folder/'HomePlayerPanelValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomePlayerPanel.NativeValidation',references=refs,includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--install',action='store_true');parser.add_argument('--project',type=Path);args=parser.parse_args()
    work=validation_directory()
    if args.install:transfer(work,ROOT/'unity',install=True)
    elif args.project:transfer(work,args.project);stage_fixture(args.project)
    else:parser.error('Supply --install or --project for the owned full native portrait project.')
