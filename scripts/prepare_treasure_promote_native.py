"""Bind original treasure promotion and awakening windows, generic material icons and source configuration."""
import argparse
import hashlib
import json
import os
import re
import uuid
from pathlib import Path
from home_native_assets import install_staged_assets,write_staged_assets
from home_native_prefab import bind_prefab,register_script
from native_spine_assets import guid
from npc_travel_language import language
from official_manifest import read_asset_manifest
from official_source import ROOT,OfficialSource
from official_tables import Constructors
from prepare_current_partner_native import PartnerNativeRecovery,identity,documents,pointer
from prepare_treasure_detail_native import DetailBinding,detail_component
from prepare_treasure_suit_native import asset_header
from prepare_treasure_icon_native import ITEM_BUNDLE
from prepare_treasure_promote_controls import prepare as prepare_controls,NAMES,COMMON,sha
from prepare_treasure_ui_native import prepare as prepare_cache,BUNDLE
from prepare_selected_wwise_inputs import prepare as prepare_audio
from prepare_pet_item_detail_native import enum_names

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativePromote'
RECEIPT=ROOT/'assets/reference/treasure-promote-native-source-manifest.json'
SCOPE='zjcs:treasure-native-promote'

class PromoteBinding(DetailBinding):
    def stage_dynamic_sprites(self,staged):
        outputs={};prior=json.loads((ROOT/'assets/reference/treasure-detail-native-source-manifest.json').read_bytes())
        for folder,rows in (('DynamicTextures',self.detail_sprite_converter.textures),('DynamicSprites',self.detail_sprite_converter.sprites)):
            for key,row in rows.items():
                relative=Path(folder)/(sha(key.encode())[:20]+'.asset')
                for suffix,hash_key in (('','sha256'),('.meta','meta_sha256')):
                    original=ROOT/row['path'];original=Path(str(original)+suffix);raw=original.read_bytes()
                    if sha(raw)!=row[hash_key]:raise ValueError('Original promotion sprite payload differs')
                    shared=ROOT/'unity/Assets/Game/Resources/Treasure/NativeDetail'/Path(str(relative)+suffix)
                    shared_key=shared.relative_to(ROOT).as_posix()
                    if shared_key in prior['outputs']:
                        if sha(shared.read_bytes())!=row[hash_key] or prior['outputs'][shared_key]!=row[hash_key]:raise ValueError('Shared original treasure sprite differs')
                        self.inputs[shared_key]=row[hash_key];continue
                    target=staged/Path(str(relative)+suffix);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
                    outputs[relative.as_posix()+suffix]=sha(raw)
        return outputs
    def sound(self,payload,owner):
        if not hasattr(self,'audio_cache'):
            self.audio_cache=prepare_audio(self.cache.parent/'treasure-promote-audio',[
                'wwisebnk_assets_wwisedata_android_minimum_pck.rawfile','wwisebnk_assets_wwisedata_android_ui_sfx_01_pck.rawfile'])
        return super().sound(payload,owner)
    def register_scripts(self):
        super().register_scripts();runtime=ROOT/'unity/Assets/Game/Runtime/Official'
        for name in NAMES:register_script(self,name,runtime/'TreasurePromoteUI'/(name+'.cs'))
        for name,target in COMMON.items():register_script(self,name,runtime/'ProfessionUI'/(target+'.cs'))
        for name in ('TreasurePageSelectItem','ItemIconTreasureSelectableItem'):
            register_script(self,name,runtime/'TreasureSlotsUI'/(name+'.cs'))
        for name,path in {'UnityEngine.UI.Ext.TabGroup':'WorldUI/WorldTabGroup.cs',
                          'UnityEngine.UI.Ext.TabItem':'WorldUI/WorldTabItem.cs',
                          'UnityEngine.UI.Ext.TabAnimatorTransition':'WorldUI/WorldTabAnimatorTransition.cs',
                          'SlicedFilledImage':'WorldUI/SlicedFilledImage.cs',
                          'UIToggle':'SkillsUI/UIToggle.cs'}.items():register_script(self,name,runtime/path)
        self.promote_prefabs={}
    def load(self,source_guid):
        if source_guid in self.redirects:return self.redirects[source_guid]
        obj=self.objects.get(source_guid)
        if obj is not None and obj.type.name=='GameObject':return self.prefab(obj)
        if obj is not None and obj.type.name=='AnimatorController':
            fields=self.recovery.fields(obj)
            (self.work/('controller-'+fields['m_Name']+'.json')).write_text(json.dumps(fields,ensure_ascii=False,indent=2),'utf8')
        return super().load(source_guid)
    def prefab(self,obj):
        key=identity(obj)
        if key in self.promote_prefabs:return self.promote_prefabs[key]['pointer']
        proof=self.recovery.apk.provenance(obj);address,=[name for name,p in self.recovery.apk.container(proof['bundle']) if identity(p.deref())==key]
        relative=address.removeprefix('Assets/');native=documents((self.native/relative).read_text('utf8'))
        root,=[k for k,v in native.items() if 'GameObject' in v and
            (native[v['GameObject']['m_Component'][0]['component']['fileID']].get('RectTransform') or
             native[v['GameObject']['m_Component'][0]['component']['fileID']].get('Transform'))['m_Father']['fileID']==0]
        result=dict(fileID=root,guid=hashlib.md5((SCOPE+':prefab:'+key).encode()).hexdigest(),type=3)
        path=self.native/relative;source_token=guid(Path(str(path)+'.meta'));self.objects[source_token]=obj
        self.redirects[source_token]=result;self.script_guids.add(result['guid'])
        name=Path(relative).stem
        _,_,actual,selection=bind_prefab(self,proof['bundle'],relative,name,SCOPE,source_root=obj,native_root_id=root,
            prepare_component=promote_component,external_asset_types=('Sprite','AnimationClip','GameObject','RectTransform'))
        if actual!=result:raise ValueError('Original promotion prefab identity differs')
        self.promote_prefabs[key]=dict(address=address,pointer=actual,selection=selection,output=name+'.prefab')
        return actual

def promote_component(binding,script,fields):
    detail_component(binding,script,fields)
    if script['type']=='AkEvent':
        owner=binding.original(script['original']);original=binding.recovery.fields(owner)
        if original['enableActionOnEvent'] or original['useCallbacks'] or original['Callbacks']:
            raise ValueError('Migrate the actual promotion Wwise callback/action scope')
        if any(value not in (0xece0d9f2-2**32,0x4c66e1f7,716467161) for value in original['triggerList']):
            raise ValueError('Migrate the actual promotion Wwise trigger scope')
        # The event's original reference remains serialized; the existing facade validates it before posting.
        event=binding.recovery.fields(pointer(original['data']['WwiseObjectReference'],owner))
        print('Original promotion event:',event['objectName'],event['id'],flush=True)
        binding.sound(original['data'],owner)

def enum_values(path):
    result={};value=0;body=path.read_text('utf-8-sig').split('{',1)[1].rsplit('}',1)[0]
    for token in body.split(','):
        token=token.strip()
        if not token:continue
        if '=' in token:name,raw=token.split('=',1);value=int(raw.strip(),0)
        else:name=token
        result[name.strip()]=value;value+=1
    return result

def prepare(verified_work=None):
    prepare_controls(True)
    if verified_work is None:cache=prepare_cache()
    else:
        verified_work=verified_work.resolve()
        if not verified_work.is_relative_to(ROOT/'output/validation') or not (verified_work/'.zjcs-validation.json').exists():raise ValueError('Use an owned verified source directory')
        proof=json.loads((verified_work/'source-manifest.json').read_bytes())['component_cache'];cache=OfficialSource().prepared_path/'home-treasure-ui-native'
        if (ROOT/proof['path']).resolve()!=(cache/'native-export-source-manifest.json').resolve() or sha((ROOT/proof['path']).read_bytes())!=proof['sha256']:
            raise ValueError('Previously verified native cache differs')
    work=ROOT/'output/validation'/('treasure-promote-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8');print('Original promotion binding:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=BUNDLE)
    try:
        binding=PromoteBinding(work,recovery=recovery,guid_prefix=SCOPE+':')
        roots={}
        for name in ('TreasurePromotePanel','TreasureAwakeMaterialPanel'):
            obj,=[p.deref() for address,p in recovery.apk.container(BUNDLE) if address.casefold()==('Assets/Prefabs/UI/Treasure/'+name+'.prefab').casefold()]
            roots[name]=binding.prefab(obj)
        records=read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage',recovery.apk.archive))
        config,=[row for row in records if row['path'].endswith('/ItemIconManagerConfig.asset')]
        obj=recovery.apk.asset(config['bundle'],config['path']);manager=recovery.fields(obj)
        required=set()
        for path in (ROOT/'unity/Assets/Game/Resources/Treasure/treasure-rules.json',ROOT/'unity/Assets/Game/Resources/Treasure/treasure-slot-levels.json'):
            data=json.loads(path.read_bytes())
            for name in ('UpgradeMaterials','LevelUpMaterials'):
                for row in data.get(name,[]):required.update(item['ItemId'] for item in row['Materials'] if item['ItemId']>0)
            for name,field in (('LevelCosts','Materials'),('BlessCosts','LevelCostItems')):
                for row in data.get(name,[]):required.update(int(key) for key in row[field] if int(key)>0)
            binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        detail_path=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-detail.json';detail=json.loads(detail_path.read_bytes())
        required.update(row['ClassId'] for row in detail['Items'] if row['ItemType']==306);required.add(1)
        items={row['ClassId']:row for row in detail['Items']};required_types={items[item]['ItemType'] for item in required}
        def prefab(value):return binding.prefab(pointer(value,obj))
        default=prefab(manager['DefPrefab'])
        overrides=[dict(Id=item,Prefab=prefab(p) if item in required else dict(fileID=0)) for item,p in zip(manager['IdList'],manager['IdPrefabList'])]
        types=[dict(Id=item,Prefab=prefab(p) if item in required_types else dict(fileID=0)) for item,p in zip(manager['TypeList'],manager['PrefabList'])]
        manager_proof=dict(source=recovery.apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),required_items=sorted(required),required_types=sorted(required_types),
            original_id_order=manager['IdList'],original_type_order=manager['TypeList'])
        cfg_row,=[row for row in records if row['path'].endswith('/ItemIconConfig.asset')]
        cfg_obj=recovery.apk.asset(cfg_row['bundle'],cfg_row['path']);cfg=recovery.fields(cfg_obj)
        icon_config=asset_header(ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/ItemIconConfig.cs','Original promotion icon configuration')
        icon_config['QualityBgImages']=[binding.original_asset(pointer(p,cfg_obj)) if p['m_PathID'] else dict(fileID=0) for p in cfg['QualityBgImages']]
        cfg_guid=hashlib.md5((SCOPE+':icon-config').encode()).hexdigest()
        binding.queue.append((Path('ItemIconConfig.asset'),cfg_guid,{11400000:dict(MonoBehaviour=icon_config)}))
        keys=set()
        for _,_,docs in binding.queue:
            if not isinstance(docs,dict):continue
            def collect(value):
                if isinstance(value,dict):
                    if value.get('myKey'):keys.add(str(value['myKey']))
                    for item in value.values():collect(item)
                elif isinstance(value,list):
                    for item in value:collect(item)
            collect(docs)
        code=recovery.apk.source.prepared_path/'official-code'
        for name in NAMES:
            path=code/'client-source'/(name+'.cs');keys.update(re.findall(r'(?:SetKey|GetText|GetTextFormated|CultureTextArg)\("([^"{}]+)"',path.read_text('utf-8-sig')))
        subrank=code/'common-source/Common/SubRank.cs'
        for name in enum_values(subrank):keys.add('SubRank.'+name)
        keys.add('ui_common_count_plus')
        keys.update(('function_limit_subrank','function_limit_season','treasure_limit_player_level','ui_treasure_limit_gap_level',
            'ui_treasure_share_level_up_limit1','ui_treasure_share_level_up_limit2'))
        seasons=json.loads((ROOT/'unity/Assets/Game/Resources/Treasure/treasure-slot-levels.json').read_bytes())['Seasons']
        keys.update('astrological_season_'+str(row['Season'])+'_name' for row in seasons)
        texts,language_proof=language(keys,recovery.apk.source)
        def replacement_keys(value):
            return {key for key in re.findall(r'\{([^{}]+)\}',value) if key!='c' and not key.isdigit()}
        pending=set().union(*(replacement_keys(value) for value in texts.values()))-texts.keys()
        while pending:
            more,_=language(pending,recovery.apk.source)
            if pending-more.keys():raise ValueError('Unresolved original promotion text aliases: '+str(pending-more.keys()))
            keys.update(pending);texts.update(more)
            pending=set().union(*(replacement_keys(value) for value in more.values()))-texts.keys()
        texts,language_proof=language(keys,recovery.apk.source)
        theme=enum_values(code/'common-source/ThemeColorType.cs')['Light'];red=enum_values(code/'common-source/ColorType.cs')['Red']
        colors=detail['GlobalColors']['TextColorConfig']['TextColorLists'];color_row,=[row for row in colors if row['ThemeColorType']==theme]
        light_red=color_row['TextColors'][color_row['TypeList'].index(red)]['Color']
        images=[];quality_names=enum_names(code/'common-source/Common/Quality.cs')
        constructors=Constructors(code)
        config_kind,=[kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^\"]+)"\)\)',constructors.source('Tables')) if name.rsplit('/',1)[-1]=='game_settings']
        settings_object=recovery.apk.asset('defaultpackage_assets_config_luban.bundle','/game_settings.g.bytes')
        settings_raw=settings_object.read().m_Script.encode('utf8','surrogateescape');settings=constructors.table(settings_raw,config_kind)
        sand_id=settings['SandsOfTimeItemId'];sand_info=items[sand_id];sand_address='Assets/Image/'+sand_info['RawIcon']
        item_kind,=[kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^\"]+)"\)\)',constructors.source('Tables')) if name.rsplit('/',1)[-1]=='item']
        item_object=recovery.apk.asset('defaultpackage_assets_config_luban.bundle','/item.g.bytes')
        item_raw=item_object.read().m_Script.encode('utf8','surrogateescape');source_sand,=[row for row in constructors.table(item_raw,item_kind) if row['ClassId']==sand_id]
        sand_row,=[row for row in records if row.get('address','').casefold()==sand_address.casefold() or row['path'].removesuffix('.png').casefold()==sand_address.casefold()]
        sand_object,=[p.deref() for name,p in recovery.apk.container(sand_row['bundle'])
            if name.casefold()==sand_row['path'].casefold() and p.deref().type.name=='Sprite']
        sand_sprite=binding.original_asset(sand_object)
        currencies=[dict(type=19,itemId=sand_id,icon=sand_sprite)]
        currency_items=[dict(Type=19,ItemId=sand_id,HasAutoIncrease=int(any(effect.get('_type')=='ItemEffectAutoIncrease' for effect in (source_sand['Effects'] or []))))]
        for quality in sorted({row['Quality'] for row in detail['Items'] if row['ItemType']==305}):
            address='Assets/Image/UI/Common/ImgTreasureQuality_'+quality_names[quality]
            matches=[row for row in records if row.get('address','').casefold()==address.casefold()]
            if not matches:matches=[row for row in records if row['path'].removesuffix('.png').casefold()==address.casefold()]
            image_row,=matches
            sprite=recovery.apk.asset(image_row['bundle'],image_row['path'])
            images.append(dict(Path=address,AssetGuid=image_row['guid'],Asset=binding.original_asset(sprite)))
        image_script=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
        image_fields=asset_header(image_script,'Original treasure quality backgrounds');image_fields['Entries']=images
        images_guid=hashlib.md5((SCOPE+':images').encode()).hexdigest()
        binding.queue.append((Path('PromoteImages.asset'),images_guid,{11400000:dict(MonoBehaviour=image_fields)}))
        asset_script=ROOT/'unity/Assets/Game/Runtime/Offline/TreasureUI/OfflineTreasurePromoteAssets.cs'
        fields=asset_header(asset_script,'OfflineTreasurePromoteAssets');fields.update(Prefab=roots['TreasurePromotePanel'],AwakeMaterialPrefab=roots['TreasureAwakeMaterialPanel'],
            DefaultIconPrefab=default,ItemTypes=types,ItemOverrides=overrides,IconConfig=dict(fileID=11400000,guid=cfg_guid,type=2),LightRed=light_red,
            Images=dict(fileID=11400000,guid=images_guid,type=2),
            Currencies=currencies,CurrencyItems=currency_items,
            Texts=[dict(Key=k,Value=v) for k,v in sorted(texts.items())])
        binding.queue.append((Path('OfflineTreasurePromoteAssets.asset'),hashlib.md5((SCOPE+':assets').encode()).hexdigest(),{11400000:dict(MonoBehaviour=fields)}))
        staged=work/'assets';outputs=write_staged_assets(binding.queue,staged);outputs.update(binding.stage_dynamic_sprites(staged))
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'treasure-promote-native',previous_outputs=prior.get('outputs'))
        for path in (Path(__file__),ROOT/'scripts/prepare_treasure_promote_controls.py',ROOT/'scripts/prepare_treasure_detail_native.py',
            ROOT/'scripts/prepare_treasure_detail_configuration.py',asset_script,Path(str(asset_script)+'.meta'),detail_path,subrank,
            ROOT/'scripts/partner_gift_animator_binding.py',ROOT/'scripts/home_native_prefab.py',ROOT/'scripts/official_tables.py',
            code/'client-source/PlayerItemUIGetItemIdFactory.cs',
            ROOT/'unity/Assets/Game/Runtime/Official/HomeHudUI/OfficialHomeTopBarAssetCatalog.cs',
            code/'common-source/ThemeColorType.cs',code/'common-source/ColorType.cs',code/'common-source/GlobalColors.cs'):
            binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        record=dict(manifest=recovery.apk.version,inputs=binding.inputs,prefabs=binding.promote_prefabs,recovery=recovery.proof,native_scripts=binding.script_sources,
            audio=binding.audio_proof,resources=binding.proof,icon_manager=manager_proof,icon_config=dict(source=recovery.apk.provenance(cfg_obj),object_sha256=sha(cfg_obj.get_raw_data())),
            language=language_proof,outputs=installed,currency_settings=dict(source=recovery.apk.provenance(settings_object),decoded_sha256=sha(settings_raw),SandsOfTimeItemId=sand_id,
                item_source=recovery.apk.provenance(item_object),item_decoded_sha256=sha(item_raw),item_constructor=item_kind),
            dynamic_sprites=list(binding.detail_sprite_converter.sprites.values()),dynamic_textures=list(binding.detail_sprite_converter.textures.values()))
        raw=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode();temp=work/'receipt-replacement.json';temp.write_bytes(raw);os.replace(temp,RECEIPT)
        (work/'bound-source-manifest.json').write_bytes(raw);print('Bound original promotion windows:',len(installed),flush=True)
    finally:recovery.apk.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--verified-work',type=Path);prepare(parser.parse_args().verified_work)
