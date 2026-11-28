"""Bind the complete original treasure detail prefab, its native suite, NPC rows and UI dependencies."""
import hashlib
import argparse
import json
import os
import uuid
from pathlib import Path
from home_native_assets import install_staged_assets,write_staged_assets
from home_native_prefab import bind_prefab,register_script
from native_spine_assets import guid
from official_manifest import read_asset_manifest
from official_source import ROOT,OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery,identity,documents,pointer
from prepare_treasure_suit_native import SuitBinding,asset_header,sha
from prepare_treasure_icon_native import component
from prepare_treasure_ui_native import prepare as prepare_cache,DETAIL_BUNDLE
from prepare_treasure_detail_controls import prepare as prepare_controls
from prepare_current_creation_ui_sprites import CurrentUiSprites

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativeDetail'
RECEIPT=ROOT/'assets/reference/treasure-detail-native-source-manifest.json'
SCOPE='zjcs:treasure-native-detail'


class DetailSprites(CurrentUiSprites):
    """Reuse a verified conversion only while its original object, stream and converter still match."""
    def __init__(self,apk,cache):
        super().__init__(apk,cache);self.cached_sprites={};self.cached_textures={}
        if RECEIPT.is_file():
            prior=json.loads(RECEIPT.read_bytes())
            scripts=('prepare_current_creation_ui_sprites.py','prepare_current_creation_skin_icons.py',
                'prepare_unity_map12_team_panel_sprites.py','prepare_unity_profession_reward_sprites.py','prepare_unity_map13_native_scene.py')
            if prior['manifest']==apk.version and all(prior['inputs'].get('scripts/'+name)==sha((ROOT/'scripts'/name).read_bytes()) for name in scripts):
                self.cached_sprites={row['identity']:row for row in prior.get('dynamic_sprites',[])}
                self.cached_textures={row['identity']:row for row in prior.get('dynamic_textures',[])}
    def valid(self,row,obj):
        return row is not None and row['object_sha256']==sha(obj.get_raw_data()) and all(
            (ROOT/(row['path']+suffix)).is_file() and sha((ROOT/(row['path']+suffix)).read_bytes())==row[key]
            for suffix,key in (('','sha256'),('.meta','meta_sha256')))
    def texture(self,obj):
        key=obj.assets_file.name+':'+str(obj.path_id)
        if key in self.textures:return self.textures[key]
        row=self.cached_textures.get(key)
        if self.valid(row,obj) and row['encoded_sha256']==sha(obj.read().get_image_data()):
            self.textures[key]=row;return row
        return super().texture(obj)
    def sprite(self,obj):
        key=obj.assets_file.name+':'+str(obj.path_id)
        if key in self.sprites:return self.sprites[key]
        row=self.cached_sprites.get(key)
        if self.valid(row,obj):
            sprite=obj.read();rd=sprite.m_RD;atlas=None
            if sprite.m_SpriteAtlas.path_id:
                atlas=sprite.m_SpriteAtlas.deref();rd=dict(atlas.read().m_RenderDataMap)[sprite.m_RenderDataKey]
            if (row['atlas'] is None and atlas is None) or (atlas is not None and row['atlas']['object_sha256']==sha(atlas.get_raw_data())):
                texture=self.texture(rd.texture.deref())
                if texture['identity']==row['texture']:
                    self.sprites[key]=row;return row
        return super().sprite(obj)


class DetailBinding(SuitBinding):
    def register_scripts(self):
        super().register_scripts();runtime=ROOT/'unity/Assets/Game/Runtime/Official'
        for name in ('TreasureDetailDialog','ElementPropItem','TreasureNpcEffectContent','TreasureNpcEffectItem','ShareItemBtn'):
            register_script(self,name,runtime/'TreasureDetailUI'/(name+'.cs'))
        for path in (runtime/'ItemDetailUI').glob('*.cs'):
            if Path(str(path)+'.meta').exists():register_script(self,path.stem,path)
        for name,path in {
            'GMFunction.ItemDetailGmTools':'ItemDetailUI/ItemDetailGmTools.cs',
            'IntroductionPopBtn':'HomePlayerInfoUI/HomeIntroductionPopBtn.cs',
            'PlayerCostMoneyContent':'HomeCurrencyWindowsUI/PlayerCostMoneyContent.cs',
            'BlessPropBtn':'ProfessionUI/ProfessionBlessPropBtn.cs',
            'SafeArea':'HomeScreenUI/SafeArea.cs',
            'BgDialogElement':'WorldUI/WorldBgDialogElement.cs',
            'PopupWindow':'WorldUI/WorldPopupWindow.cs',
            'PopupWindowSound':'WorldUI/WorldPopupWindowSound.cs',
            'CountRedDotImage':'WorldUI/WorldCountRedDotImage.cs',
            'MirrorImage':'WorldUI/MirrorImage.cs',
            'TMPColorGradient':'World/NativeLabelUI/TMPColorGradient.cs',
            'UIToggleObject':'SkillsUI/UIToggleObject.cs',
            'LoopVerticalScrollWithMaxHeight':'SkillsUI/LoopVerticalScrollWithMaxHeight.cs',
            'AwardPanelLayoutElement':'WorldUI/AwardPanelLayoutElement.cs',
            }.items():register_script(self,name,runtime/path)
        self.builtin_sprites={}
    def learn(self,source,native,owner):
        if (isinstance(source,dict) and source.get('m_PathID') and isinstance(native,dict)
                and native.get('guid')=='0000000000000000f000000000000000'):
            obj=pointer(source,owner)
            if obj.type.name!='Sprite':raise ValueError('Review original built-in detail dependency')
            fields=self.recovery.fields(obj);key,file_id=fields['m_RenderDataKey']
            if file_id!=native['fileID'] or key!={'data[0]':0,'data[1]':0,'data[2]':15,'data[3]':0}:
                raise ValueError('Original built-in sprite identity differs')
            self.builtin_sprites[identity(obj)]=dict(source=self.recovery.apk.provenance(obj),
                original_sha256=sha(obj.get_raw_data()),name=fields['m_Name'],pointer=dict(native))
            return
        return super().learn(source,native,owner)
    def load(self,source_guid):
        if source_guid in self.redirects:return self.redirects[source_guid]
        obj=self.objects.get(source_guid)
        if obj is not None and obj.type.name=='RectTransform':
            receipt_path=ROOT/'assets/reference/treasure-suit-native-source-manifest.json'
            receipt=json.loads(receipt_path.read_bytes());file_id=receipt['member_template']['objects'].get(identity(obj))
            if file_id is None:raise ValueError('Resolve original external detail transform: '+identity(obj))
            path=ROOT/'unity/Assets/Game/Resources/Treasure/NativeSuit/TreasureSuitMemberTemplate.prefab'
            for consumed in (path,Path(str(path)+'.meta')):
                key=consumed.relative_to(ROOT).as_posix()
                if sha(consumed.read_bytes())!=receipt['outputs'][key]:raise ValueError('Shared original suit member template differs')
                self.inputs[key]=receipt['outputs'][key]
            self.inputs[receipt_path.relative_to(ROOT).as_posix()]=sha(receipt_path.read_bytes())
            result=dict(fileID=file_id,guid=guid(Path(str(path)+'.meta')),type=3)
            self.redirects[source_guid]=result;self.script_guids.add(result['guid']);return result
        if obj is not None and obj.type.name=='GameObject':
            path=self.native_path(source_guid,obj);relative=path.relative_to(self.native).as_posix()
            native=documents(path.read_text('utf8'))
            root,=[key for key,value in native.items() if 'GameObject' in value and
                (native[value['GameObject']['m_Component'][0]['component']['fileID']].get('Transform') or
                 native[value['GameObject']['m_Component'][0]['component']['fileID']].get('RectTransform'))['m_Father']['fileID']==0]
            token=hashlib.md5((SCOPE+':prefab:'+identity(obj)).encode()).hexdigest()
            result=dict(fileID=root,guid=token,type=3);self.redirects[source_guid]=result;self.script_guids.add(token)
            name='DetailPrefab_'+sha(identity(obj).encode())[:16]
            bundle=self.recovery.apk.provenance(obj)['bundle']
            _,_,actual,selection=bind_prefab(self,bundle,relative,name,SCOPE,source_root=obj,native_root_id=root,
                prepare_component=detail_component,external_asset_types=('Sprite','AnimationClip','GameObject','RectTransform'))
            if actual!=result:raise ValueError('Original detail prefab identity changed')
            self.prefab_selections[identity(obj)]=dict(path=relative,output=name+'.prefab',pointer=result,selection=selection)
            return result
        return super().load(source_guid)
    def original_asset(self,obj):
        if obj.type.name=='RectTransform':
            token=hashlib.md5((SCOPE+':external-transform:'+identity(obj)).encode()).hexdigest()
            self.objects[token]=obj;return self.load(token)
        if obj.type.name=='Sprite':
            # The original atlas transform must also be resolved for exported dynamic sprites.
            if not hasattr(self,'detail_sprite_converter'):self.detail_sprite_converter=DetailSprites(self.recovery.apk,self.work/'dynamic-source')
            native=self.detail_sprite_converter.sprite(obj)
            result=dict(fileID=native['file_id'],guid=native['guid'],type=2)
            self.script_guids.add(result['guid']);return result
        return super().original_asset(obj)
    def stage_dynamic_sprites(self,staged):
        outputs={}
        if not hasattr(self,'detail_sprite_converter'):return outputs
        for folder,rows in (('DynamicTextures',self.detail_sprite_converter.textures),('DynamicSprites',self.detail_sprite_converter.sprites)):
            for key,row in rows.items():
                relative=Path(folder)/(sha(key.encode())[:20]+'.asset')
                for suffix in ('','.meta'):
                    original=Path(str(ROOT/row['path'])+suffix);raw=original.read_bytes()
                    if sha(raw)!=row['meta_sha256' if suffix else 'sha256']:raise ValueError('Original dynamic sprite conversion differs')
                    path=Path(str(staged/relative)+suffix);path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
                    outputs[relative.as_posix()+suffix]=sha(raw)
        return outputs
    def replace(self,value):
        if isinstance(value,dict) and set(value)=={'class','ns','asm'}:
            if value['ns']==value['asm']=='Zjcs.Official.WorldUI' and value['class'].startswith('WorldState'):
                path=ROOT/'unity/Assets/Game/Runtime/Official/WorldUI'/(value['class']+'.cs')
                if value['class'] in ('WorldStateGameObjectConfig','WorldStateUIColorConfig'):
                    path=path.with_name('WorldStateSwitcher.cs')
                if not path.is_file():raise ValueError('Resolve migrated native detail state config: '+str(value))
                self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
                return dict(value)
        return super().replace(value)


def detail_component(binding,script,fields):
    component(binding,script,fields)
    if script['type']=='PopupWindowSound':
        owner=binding.original(script['original']);original=binding.recovery.fields(owner)
        for key in ('ShowSound','HideSound'):fields[key]=binding.sound(original[key],owner)


def prepare(verified_work=None,work=None):
    prepare_controls(True)
    if verified_work is None:cache=prepare_cache()
    else:
        verified_work=verified_work.resolve()
        if not verified_work.is_relative_to(ROOT/'output/validation') or not (verified_work/'.zjcs-validation.json').is_file():
            raise ValueError('Reuse only an owned original-recovery workspace')
        proof=json.loads((verified_work/'source-manifest.json').read_bytes())['component_cache']
        receipt=ROOT/proof['path'];cache=OfficialSource().prepared_path/'home-treasure-ui-native'
        if receipt.resolve()!=(cache/'native-export-source-manifest.json').resolve() or sha(receipt.read_bytes())!=proof['sha256']:
            raise ValueError('Previously verified native export receipt differs')
        # PartnerNativeRecovery and the binding verify each consumed prefab and dependency against this receipt.
    if work is None:
        work=ROOT/'output/validation'/('treasure-detail-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
        (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
    else:
        work=work.resolve()
        if not work.is_relative_to(ROOT/'output/validation') or not (work/'.zjcs-validation.json').is_file():
            raise ValueError('Reuse only an owned detail binding workspace')
    print('Original complete treasure detail:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=DETAIL_BUNDLE)
    try:
        binding=DetailBinding(work,recovery=recovery,guid_prefix=SCOPE+':')
        relative='Prefabs/UI/ItemDetail/TreasureDetailDialog.prefab'
        _,staged,prefab,selection=bind_prefab(binding,DETAIL_BUNDLE,relative,'TreasureDetailDialog',SCOPE,
            prepare_component=detail_component,external_asset_types=('Sprite','AnimationClip','GameObject','RectTransform'))
        docs=binding.queue[-1][2]
        controller,=[row for row in selection['scripts'] if row['type']=='TreasureDetailDialog']
        fields=docs[controller['nativeFileID']]['MonoBehaviour']
        suite_fields=('mySuitRoot','mySuitTitle','mySuitCount','mySuitItemNameRoot','mySuitItemPrefab','mySuitPropItems')
        suit=asset_header(ROOT/'unity/Assets/Game/Runtime/Official/TreasureDetailUI/TreasureSuitContent.cs','')
        suit.update({name:fields.pop(name) for name in suite_fields});suit['m_GameObject']=suit['mySuitRoot']
        suit_id=max(map(int,docs))+1;docs[suit_id]=dict(MonoBehaviour=suit)
        docs[suit['mySuitRoot']['fileID']]['GameObject']['m_Component'].append(dict(component=dict(fileID=suit_id)))
        fields['mSuit']=dict(fileID=suit_id)
        # The NPC row is loaded through the original GUID proxy; retain that GUID in the local image/prefab catalog.
        npc,=[row for row in selection['scripts'] if row['type']=='TreasureNpcEffectContent']
        proxy=docs[npc['nativeFileID']]['MonoBehaviour']['mNpcEffectItemPrefab']['mAssetGuid']
        records=read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage',recovery.apk.archive))
        row,=[row for row in records if row['guid']==proxy]
        obj,=[p.deref() for n,p in recovery.apk.container(row['bundle']) if n.casefold()==row['path'].casefold()]
        native=binding.native/row['path'].removeprefix('Assets/');token=guid(Path(str(native)+'.meta'));binding.objects[token]=obj
        npc_prefab=binding.load(token)
        images=[dict(Path=row['path'],AssetGuid=proxy,Asset=npc_prefab)]
        configuration_path=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-detail.json'
        configuration=json.loads(configuration_path.read_bytes())
        for address in configuration['ImagePaths']:
            rows=[row for row in records if row.get('address','').casefold()==address.casefold()]
            if not rows:rows=[row for row in records if row['path'].removesuffix('.png').casefold()==address.casefold()]
            record,=rows
            sprite,=[p.deref() for n,p in recovery.apk.container(record['bundle'])
                if n.casefold()==record['path'].casefold() and p.deref().type.name=='Sprite']
            try:asset=binding.original_asset(sprite)
            except Exception as error:raise ValueError('Resolve the actual original dynamic detail image: '+address) from error
            images.append(dict(Path=address,AssetGuid=record['guid'],Asset=asset))
        print('Bound original dynamic detail images:',len(images),flush=True)
        image_script=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
        image_fields=asset_header(image_script,'Original complete treasure detail resources');image_fields['Entries']=images
        image_guid=hashlib.md5((SCOPE+':images').encode()).hexdigest()
        binding.queue.append((Path('DetailImages.asset'),image_guid,{11400000:dict(MonoBehaviour=image_fields)}))
        property_guid=hashlib.md5((SCOPE+':property-config').encode()).hexdigest()
        asset_script=ROOT/'unity/Assets/Game/Runtime/Offline/TreasureUI/OfflineTreasureDetailAssets.cs'
        asset=asset_header(asset_script,'OfflineTreasureDetailAssets')
        suite=ROOT/'unity/Assets/Game/Resources/Treasure/NativeSuit/OfflineTreasureSuitAssets.asset'
        asset.update(Prefab=prefab,Images=dict(fileID=11400000,guid=image_guid,type=2),
            Configuration=dict(fileID=4900000,guid=guid(Path(str(configuration_path)+'.meta')),type=3),
            PropertyConfiguration=dict(fileID=4900000,guid=property_guid,type=3),
            Suit=dict(fileID=11400000,guid=guid(Path(str(suite)+'.meta')),type=2))
        binding.queue.append((Path('OfflineTreasureDetailAssets.asset'),hashlib.md5((SCOPE+':assets').encode()).hexdigest(),{11400000:dict(MonoBehaviour=asset)}))
        outputs=write_staged_assets(binding.queue,staged)
        outputs.update(binding.stage_dynamic_sprites(staged))
        property_file=staged/'detail-property-config.json'
        property_file.write_text(json.dumps(configuration['PropertyConfiguration'],ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        property_meta=Path(str(property_file)+'.meta');property_meta.write_text('fileFormatVersion: 2\nguid: '+property_guid+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[property_file.name]=sha(property_file.read_bytes());outputs[property_meta.name]=sha(property_meta.read_bytes())
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'treasure-detail-native',previous_outputs=prior.get('outputs'))
        for path in (Path(__file__),ROOT/'scripts/prepare_treasure_detail_controls.py',ROOT/'scripts/prepare_treasure_suit_native.py',
            ROOT/'scripts/home_native_prefab.py',ROOT/'scripts/prepare_treasure_icon_native.py',asset_script,Path(str(asset_script)+'.meta'),
            configuration_path,Path(str(configuration_path)+'.meta'),ROOT/'assets/reference/treasure-detail-configuration-source-manifest.json',
            *[ROOT/'scripts'/name for name in ('prepare_current_creation_ui_sprites.py','prepare_current_creation_skin_icons.py',
                'prepare_unity_map12_team_panel_sprites.py','prepare_unity_profession_reward_sprites.py','prepare_unity_map13_native_scene.py')]):
            binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        record=dict(manifest=recovery.apk.version,inputs=binding.inputs,selection=selection,recovery=recovery.proof,
            native_scripts=binding.script_sources,resources=binding.proof,prefab=prefab,npc_guid=proxy,npc_prefab=npc_prefab,
            extra_prefabs=binding.prefab_selections,audio=binding.audio_proof,builtin_sprites=binding.builtin_sprites,outputs=installed,
            dynamic_sprites=list(binding.detail_sprite_converter.sprites.values()) if hasattr(binding,'detail_sprite_converter') else [],
            dynamic_textures=list(binding.detail_sprite_converter.textures.values()) if hasattr(binding,'detail_sprite_converter') else [])
        text=json.dumps(record,ensure_ascii=False,indent=2)+'\n'
        replacement=work/'receipt-replacement.json';replacement.write_text(text,'utf8',newline='\n');os.replace(replacement,RECEIPT)
        (work/'bound-source-manifest.json').write_text(text,'utf8',newline='\n')
        print('Bound complete treasure detail:',len(installed),'files',flush=True)
    finally:recovery.apk.close()


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--verified-work',type=Path);p.add_argument('--work',type=Path)
    args=p.parse_args();prepare(args.verified_work,args.work)
