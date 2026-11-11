"""Install selected native player-info text subtrees and stage their fixture."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import yaml
from official_apk import ROOT
from prepare_home_bottom_bar_validation import equivalent
from prepare_partner_card_native import Dumper, guid, sha
from prepare_partner_input_dialog import metadata
from validation_assemblies import stage_runtime_modules
from validation_workspace import require_temporary_unity_project, validation_directory


def transfer(work, project, *, install=False):
    if not install: project=require_temporary_unity_project(project)
    receipt=json.loads((work/'home-player-info-native-source-manifest.json').read_bytes())
    generator=ROOT/'scripts/prepare_home_player_info_native.py'
    if sha(generator.read_bytes())!=receipt['generator_sha256']: raise ValueError('Regenerate player-info source')
    for relative,expected in {**receipt['inputs'],**receipt['runtime_code']}.items():
        if sha((ROOT/relative).read_bytes())!=expected: raise ValueError('Player-info dependency changed: '+relative)
    for row in receipt['script_bindings'].values():
        file=ROOT/row['path']
        if sha(file.read_bytes())!=row['sha256'] or sha(Path(str(file)+'.meta').read_bytes())!=row['meta_sha256']:
            raise ValueError('Player-info binding changed: '+row['path'])
    destination=project/'Assets/Official/Home/UI/PlayerInfo'
    existing={}
    for meta in (project/'Assets').rglob('*.meta'):
        file=Path(str(meta)[:-5])
        if not file.is_file(): continue
        key=guid(meta)
        if key in existing: raise ValueError('Existing GUID collision: '+key)
        existing[key]=file
    outputs,reused,plan={},[],[]
    for relative,expected in receipt['outputs'].items():
        source=work/'player-info-assets'/relative
        if sha(source.read_bytes())!=expected: raise ValueError('Staged player-info bytes changed: '+relative)
        if source.suffix=='.meta': continue
        target=existing.get(guid(Path(str(source)+'.meta')),destination/relative)
        if target.exists():
            if not equivalent(source.read_bytes(),target.read_bytes()): raise ValueError('Preserve different native resource: '+str(target))
            if not target.is_relative_to(destination):
                reused.append(dict(path=target.relative_to(ROOT if install else project).as_posix(),sha256=sha(target.read_bytes())))
        else:
            plan.extend(((source.read_bytes(),target),(Path(str(source)+'.meta').read_bytes(),Path(str(target)+'.meta'))))
        if target.is_relative_to(destination):
            for file,staged in ((target,source),(Path(str(target)+'.meta'),Path(str(source)+'.meta'))):
                outputs[file.relative_to(ROOT if install else project).as_posix()]=sha(file.read_bytes() if file.exists() else staged.read_bytes())
    context=destination/'original-parent-context.json'
    context_bytes=(json.dumps(receipt['original_parent_context'],ensure_ascii=False,indent=2)+'\n').encode()
    context_guid=hashlib.md5(b'zjcs:home-player-info-parent-context').hexdigest()
    context_meta=('fileFormatVersion: 2\nguid: '+context_guid+'\nTextScriptImporter:\n  externalObjects: {}\n').encode()
    script=ROOT/'unity/Assets/Game/Runtime/Official/HomePlayerInfoUI/OfficialHomePlayerInfoAssetCatalog.cs'
    fields=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3),m_Name='OfficialHomePlayerInfoAssetCatalog',m_EditorClassIdentifier='',
        namePrefab=receipt['roots']['HomeNameText'],levelPrefab=receipt['roots']['HomeLevelText'],combatRatingPrefab=receipt['roots']['HomeCombatRatingText'],
        originalParentContext=dict(fileID=4900000,guid=context_guid,type=3),
        localizedTexts=dict(fileID=4900000,guid=guid(work/'player-info-assets/home-player-info-texts.json.meta'),type=3))
    font=receipt['font'];config=font['original_config']
    fields.update(sourceFont=font['source_font'],sourceFontPath=font['source_font_definition']['FontPath'],
        originalFontConfig=font['config_pointer'],defaultFontAsset=config['DefaultFontAsset'],
        fontEntries=[dict(Name=name,**value) for name,value in config['FontAssetInfoDict'].items()])
    catalog=project/'Assets/Game/Resources/Home/OfficialHomePlayerInfoAssetCatalog.asset'
    catalog_bytes=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(dict(MonoBehaviour=fields),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode()
    catalog_meta=('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-player-info-catalog').hexdigest()+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode()
    for file,raw in ((context,context_bytes),(Path(str(context)+'.meta'),context_meta),(catalog,catalog_bytes),(Path(str(catalog)+'.meta'),catalog_meta)):
        if file.exists() and file.read_bytes()!=raw and (install or file!=catalog): raise ValueError('Preserve different native player-info catalog: '+str(file))
        plan.append((raw,file));outputs[file.relative_to(ROOT if install else project).as_posix()]=sha(raw)
    for raw,target in plan: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        file=metadata(folder,'DefaultImporter',True);outputs[file.relative_to(ROOT if install else project).as_posix()]=sha(file.read_bytes())
    if install:
        receipt.update(installed_output_sha256=outputs,reused=reused)
        (ROOT/'assets/reference/home-player-info-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original player-info transfer:',len(outputs),'files; reused',len(reused),flush=True)


def stage_fixture(project):
    stage_runtime_modules(project,ROOT/'unity/Assets/Game/Runtime',['Zjcs.Official.HomePlayerInfoUI'])
    folder=project/'Assets/HomePlayerInfoValidation';folder.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/home_player_info_native_validation.cs',folder/'HomePlayerInfoNativeValidation.cs')
    receipt=json.loads((validation_directory()/'home-player-info-native-source-manifest.json').read_bytes())
    context=dict(entries=[dict(name=key,chain=value) for key,value in receipt['original_parent_context'].items()])
    (folder/'parent-context.json').write_text(json.dumps(context,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    (folder/'HomePlayerInfoValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomePlayerInfo.NativeValidation',references=[
        'Zjcs.Official.HomePlayerInfoUI','Zjcs.Official.HomeHudUI','Zjcs.Official.WorldUI','Zjcs.Official.ProfessionUI',
        'Zjcs.Official.World.NativeLabelUI','Zjcs.Official.World.NativeText','Unity.TextMeshPro','Unity.ugui'],includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--install',action='store_true');parser.add_argument('--project',type=Path);args=parser.parse_args()
    work=validation_directory()
    if args.install: transfer(work,ROOT/'unity',install=True)
    elif args.project: transfer(work,args.project);stage_fixture(args.project)
    else: parser.error('Supply --install or --project pointing to an owned project.')
