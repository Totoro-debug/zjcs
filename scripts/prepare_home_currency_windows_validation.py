"""Bind the original currency windows and stage its isolated Play Mode fixture."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_card_native import guid,sha
from prepare_home_scene_ui_validation import native_equivalent as equivalent
from validation_workspace import validation_directory
from prepare_home_auto_increase_runtime import metadata
import yaml


def catalog(project,native):
    folder=project/'Assets/Game/Resources/Home'
    image=folder/'OfflineHomeCurrencyImages.asset';target=folder/'OfflineHomeCurrencyAssets.asset'
    def ref(file_id,key,kind=2):return dict(fileID=file_id,guid=key,type=kind)
    def write(path,script,fields):
        body=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
            m_Script=ref(11500000,guid(Path(str(script)+'.meta')),3),m_Name=path.stem,m_EditorClassIdentifier='')
        body.update(fields);path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.safe_dump(dict(MonoBehaviour=body),sort_keys=False,allow_unicode=True),'utf8',newline='\n')
        if not Path(str(path)+'.meta').exists():
            Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs:home-currency:'+path.stem).encode()).hexdigest()+'\nNativeFormatImporter:\n  externalObjects: {}\n','utf8',newline='\n')
    write(image,ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs',dict(Entries=[
        dict(Path='Assets/Image/'+row['item']['RawIcon'],AssetGuid=row['manifest_asset']['guid'],Asset=row['pointer'])
        for row in native['currency_images'].values()]+[
            dict(Path=native['hint_attachment']['asset']['path'],AssetGuid=native['hint_attachment']['asset']['guid'],
                Asset=native['roots']['HomeCurrency'+native['hint_attachment']['name']])]))
    destination=project/'Assets/Official/Home/UI/CurrencyWindows'
    write(target,ROOT/'unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyAssets.cs',dict(
        quickBuyPrefab=native['roots']['ShopQuickBuyDialog'],payChangePrefab=native['roots']['PayMoneyChangeDialog'],
        itemIconPrefab=native['roots']['HomeCurrencyItemIconDefault'],
        configuration=ref(4900000,guid(destination/'home-currency-windows-rules.json.meta'),3),
        texts=ref(4900000,guid(destination/'home-currency-windows-texts.json.meta'),3),iconConfig=native['icon_config'],
        attachmentConfig=native['attachment_config'],images=ref(11400000,guid(Path(str(image)+'.meta'))),
        sounds=[dict(eventId=row['event']['id'],clip=row['pointer']) for row in native['audio'].values()]))
    return {('unity/'+p.relative_to(project).as_posix()):sha(p.read_bytes()) for p in
        (image,Path(str(image)+'.meta'),target,Path(str(target)+'.meta'))}


def transfer(work,project,install=False):
    receipt=json.loads((work/'home-currency-windows-native-source-manifest.json').read_bytes())
    assert receipt['generator_sha256']==sha((ROOT/'scripts/prepare_home_currency_windows_native.py').read_bytes())
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
    destination=project/'Assets/Official/Home/UI/CurrencyWindows';outputs,reused,plan={},{},[]
    for relative,expected in receipt['outputs'].items():
        source=work/'gift-assets'/relative;assert sha(source.read_bytes())==expected,relative
        if source.suffix=='.meta':continue
        key=guid(Path(str(source)+'.meta'));target=existing.get(key,destination/relative)
        if target.exists():
            assert equivalent(source.read_bytes(),target.read_bytes()),target
            reused[target.relative_to(project).as_posix()]=sha(target.read_bytes())
        else:plan.extend(((source,target),(Path(str(source)+'.meta'),Path(str(target)+'.meta'))))
        if key==receipt['roots']['ShopQuickBuyDialog']['guid']:prefab_path=target.relative_to(project).as_posix()
        for item in (target,Path(str(target)+'.meta')):
            staged=source if item==target else Path(str(source)+'.meta')
            if item.is_relative_to(destination):outputs[item.relative_to(project).as_posix()]=sha(item.read_bytes()) if item.exists() else sha(staged.read_bytes())
    for source,target in plan:target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        meta=Path(str(folder)+'.meta')
        if not meta.exists():
            key=hashlib.md5(('zjcs:home-currency-windows-folder:'+folder.relative_to(project).as_posix()).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[meta.relative_to(project).as_posix()]=sha(meta.read_bytes())
    if install:
        receipt['catalog_outputs']=catalog(project,receipt)
        offline=ROOT/'unity/Assets/Game/Runtime/Offline/HomeCurrencyUI'
        receipt['offline_output_sha256']={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in
            (Path(str(offline)+'.meta'),*[p for p in offline.rglob('*') if p.is_file()],ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeCurrencyPlayer.cs')}
        receipt['installed_output_sha256']={'unity/'+path:value for path,value in outputs.items()}
        receipt['reused_output_sha256']={'unity/'+path:value for path,value in reused.items() if path not in outputs}
        receipt['binding_generator_sha256']=sha(Path(__file__).read_bytes())
        fixture=ROOT/'scripts/fixtures/home_currency_windows_native_validation.cs'
        factory=ROOT/'.tools/source-packages/current/884_156692/official-code/client-source/PlayerItemUIGetItemIdFactory.cs'
        assert 'case PlayerItemType.FlareStar:\n\t\t\treturn Parsers.GameSettings.FlareStarId;' in factory.read_text('utf-8-sig')
        icon_manager=factory.with_name('ItemIconManager.cs')
        ui_utils=factory.with_name('UIUtils.cs')
        confirm_proof=ROOT/'assets/reference/partner-confirm-dialog-source-manifest.json'
        confirmed=json.loads(confirm_proof.read_bytes());assert confirmed['manifest']=='884_156692'
        confirm_prefab=ROOT/'unity/Assets/Official/Partners/UI/Confirm/ConfirmDialog.prefab'
        assert sha(confirm_prefab.read_bytes())==confirmed['installed_output_sha256'][confirm_prefab.relative_to(ROOT).as_posix()]
        confirm_runtime=confirmed['script_bindings']['ConfirmDialog']
        assert sha((ROOT/confirm_runtime['path']).read_bytes())==confirm_runtime['sha256']
        confirm_catalog=ROOT/'unity/Assets/Game/Resources/Partners/OfficialPartnerConfirmAssetCatalog.asset'
        assert 'guid: '+confirmed['roots']['ConfirmDialog']['guid'] in confirm_catalog.read_text('utf8')
        receipt['binding_inputs']={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in
            (fixture,factory,icon_manager,ui_utils,confirm_proof,confirm_prefab,confirm_catalog,ROOT/confirm_runtime['path'])}
        (ROOT/'assets/reference/home-currency-windows-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original currency-window transfer:',len(outputs),'outputs;',len(reused),'existing',flush=True)
    return prefab_path


def stage(work,project):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    receipt=json.loads((ROOT/'assets/reference/home-currency-windows-runtime-source-manifest.json').read_bytes())
    for relative,expected in receipt['outputs'].items():
        source=ROOT/relative;assert sha(source.read_bytes())==expected,source
        target=project/source.relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    offline=ROOT/'unity/Assets/Game/Runtime/Offline/HomeCurrencyUI'
    for source in (Path(str(offline)+'.meta'),*[p for p in offline.rglob('*') if p.is_file()]):
        target=project/source.relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    shutil.copy2(ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeCurrencyPlayer.cs',project/'Assets/Game/Runtime/Offline/HomeUI/OfflineHomeCurrencyPlayer.cs')
    prefab=transfer(work,project)
    catalog(project,json.loads((work/'home-currency-windows-native-source-manifest.json').read_bytes()))
    stage_fixture(work,project,prefab)


def stage_fixture(work,project,prefab='Assets/Official/Home/UI/CurrencyWindows/ShopQuickBuyDialog.prefab'):
    folder=project/'Assets/HomeCurrencyWindowsNativeValidation';folder.mkdir(exist_ok=True)
    native=json.loads((work/'home-currency-windows-native-source-manifest.json').read_bytes());audio=next(iter(native['audio'].values()))
    source=(ROOT/'scripts/fixtures/home_currency_windows_native_validation.cs').read_text('utf8').replace('@QUICK_BUY_PREFAB@',prefab).replace('@PAY_CHANGE_PREFAB@',prefab.rsplit('/',1)[0]+'/PayMoneyChangeDialog.prefab')
    source=source.replace('@TEXT_PATH@',prefab.rsplit('/',1)[0]+'/home-currency-windows-texts.json')
    source=source.replace('@RULES_PATH@',prefab.rsplit('/',1)[0]+'/home-currency-windows-rules.json')
    source=source.replace('@SOUND_GUID@',audio['pointer']['guid']).replace('@SOUND_ID@',str(audio['event']['id']))
    (folder/'HomeCurrencyWindowsNativeValidation.cs').write_text(source,'utf8',newline='\n')
    (folder/'fixture.json').write_text(json.dumps(native,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    asmdef=dict(name='Zjcs.HomeCurrencyWindowsNativeValidation',includePlatforms=['Editor'],references=[
        'Unity.ugui','Unity.TextMeshPro','Zjcs.Official.HomeCurrencyWindowsUI','Zjcs.Official.WorldUI',
        'Zjcs.Official.World.NativeLabelUI','Zjcs.Official.ProfessionUI','Zjcs.Offline.HomeHudUI','Zjcs.Offline.PartnersUI','Zjcs.Offline.ProfessionUI','Zjcs.Official.HomeCountControlsUI','Unity.Newtonsoft.Json','UniTask',
        'Zjcs.Offline.HomeCurrencyUI','Zjcs.Offline.HomeUI','Zjcs.Official.HomeScreenUI','Zjcs.Official.HomeHudUI','Zjcs.Presentation.World',
        'Zjcs.Offline.CharacterCreation','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Official.HomeCurrencyUI'])
    (folder/'HomeCurrencyWindowsNativeValidation.asmdef').write_text(json.dumps(asmdef,indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--source-work',type=Path,required=True)
    parser.add_argument('--project',type=Path);parser.add_argument('--install',action='store_true');args=parser.parse_args()
    transfer(args.source_work,ROOT/'unity',True) if args.install else stage(args.source_work,args.project)
