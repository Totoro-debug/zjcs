"""Bind the original TreasureDetailDialog suit subtree and selected-source property/text dependencies."""
import hashlib
import json
import re
import uuid
from pathlib import Path
from home_native_assets import install_staged_assets, write_staged_assets
from home_native_prefab import bind_prefab, register_script
from native_spine_assets import guid
from npc_travel_language import language
from official_manifest import read_asset_manifest
from official_source import ROOT
from official_tables import Constructors
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, pointer
from prepare_partner_card_native import script_id
from prepare_treasure_icon_native import TreasureIconBinding, component
from prepare_treasure_suit_ui import prepare as prepare_controls
from prepare_treasure_ui_native import prepare as prepare_cache, DETAIL_BUNDLE

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativeSuit'
RECEIPT=ROOT/'assets/reference/treasure-suit-native-source-manifest.json'
SCOPE='zjcs:treasure-native-suit'


def sha(data):return hashlib.sha256(data).hexdigest()


class SuitBinding(TreasureIconBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('TreasureDetailSuitItem','TreasureSuitPropItem','TreasureSuitContent'):
            register_script(self,name,ROOT/'unity/Assets/Game/Runtime/Official/TreasureDetailUI'/(name+'.cs'))
        register_script(self,'PlayerPropItem',ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionPlayerPropItem.cs')
        for name in ('VerticalLayoutGroupJSZX','HorizontalLayoutGroupJSZX'):
            path=ROOT/'unity/Assets/Game/Runtime/Official/WorldUI'/(name+'.cs')
            ref=dict(fileID=11500000,guid=guid(Path(str(path)+'.meta')),type=3)
            full='UGUIJszx.'+name
            self.scripts[full]=ref;self.class_ids[script_id('UGUIJszx',name)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[full]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))


def asset_header(script,name):
    return dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),m_PrefabAsset=dict(fileID=0),
        m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,m_Script=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3),
        m_Name=name,m_EditorClassIdentifier='')


def prepare():
    prepare_controls(True)
    cache=prepare_cache()
    work=ROOT/'output/validation'/('treasure-suit-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
    print('Original treasure suit closure:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=DETAIL_BUNDLE)
    try:
        binding=SuitBinding(work,recovery=recovery,guid_prefix=SCOPE+':')
        relative='Prefabs/UI/ItemDetail/TreasureDetailDialog.prefab'
        panel_root,=[p.deref() for name,p in recovery.apk.container(DETAIL_BUNDLE) if name.casefold()==('Assets/'+relative).casefold()]
        owner,=[p.component.deref() for p in panel_root.read().m_Component if p.component.deref().type.name=='MonoBehaviour'
            and p.component.deref().read(check_read=False).m_Script.read().m_ClassName=='TreasureDetailDialog']
        original=recovery.fields(owner);root=pointer(original['mySuitRoot'],owner)
        native=documents((binding.native/relative).read_text('utf8'))
        native_id,=[k for k,d in native.items() if d.get('GameObject',{}).get('m_Name')==root.read().m_Name]
        outputs,staged,prefab,selection=bind_prefab(binding,DETAIL_BUNDLE,relative,'TreasureSuitContent',SCOPE,
            source_root=root,native_root_id=native_id,prepare_component=component,external_asset_types=('Sprite','AnimationClip','GameObject'))
        # All six original enclosing-controller fields point into this exact subtree.
        docs=binding.queue[-1][2]
        template=pointer(original['mySuitItemPrefab'],owner)
        template_root=pointer(recovery.fields(template)['m_GameObject'],template)
        template_path,=[n.removeprefix('Assets/') for n,p in recovery.apk.container(DETAIL_BUNDLE)
            if identity(p.deref())==identity(template_root)]
        _,_,member_prefab,member_selection=bind_prefab(binding,DETAIL_BUNDLE,template_path,'TreasureSuitMemberTemplate',SCOPE,
            prepare_component=component,
            external_asset_types=('Sprite','AnimationClip','GameObject'))
        template_reference=dict(member_prefab,fileID=member_selection['objects'][identity(template)])
        object_map=selection['objects']
        def local(value):
            if isinstance(value,list):return [local(v) for v in value]
            obj=pointer(value,owner)
            if identity(obj)==identity(template):return template_reference
            if identity(obj) not in object_map:raise ValueError('Original suit reference leaves its subtree: '+identity(obj))
            return dict(fileID=object_map[identity(obj)])
        fields=asset_header(ROOT/'unity/Assets/Game/Runtime/Official/TreasureDetailUI/TreasureSuitContent.cs','')
        # m_GameObject is the root GameObject, not its RectTransform.
        fields['m_GameObject']=dict(fileID=prefab['fileID'])
        names=('mySuitRoot','mySuitTitle','mySuitCount','mySuitItemNameRoot','mySuitItemPrefab','mySuitPropItems')
        fields.update({name:local(original[name]) for name in names})
        controller_id=max(map(int,docs))+1;docs[controller_id]=dict(MonoBehaviour=fields)
        docs[prefab['fileID']]['GameObject']['m_Component'].append(dict(component=dict(fileID=controller_id)))

        # Current-source configuration supplies the actual flags, icon addresses and entry metadata.
        constructors=Constructors(recovery.apk.source.prepared_path/'official-code');tables={};table_sources={}
        for name,kind in (('prop_cfg','TbPropCfgInfo'),('entry','TbEntryInfo')):
            obj=recovery.apk.asset('defaultpackage_assets_config_luban.bundle','/'+name+'.g.bytes')
            raw=obj.read().m_Script.encode('utf8','surrogateescape');tables[name]=constructors.table(raw,kind)
            table_sources[name]=dict(source=recovery.apk.provenance(obj),decoded_sha256=sha(raw))
        properties=json.loads((ROOT/'unity/Assets/Game/Resources/Treasure/treasure-properties.json').read_bytes())
        prop_types={p for suit in properties['Suits'] for item in suit['Props'] for p in item['PropTypes']}
        props=[p for p in tables['prop_cfg'] if p['PropType'] in prop_types]
        entry_ids={p['EntryId'] for p in props if p['EntryId']>0}
        entries=[e for e in tables['entry'] if e['Id'] in entry_ids]
        addresses={p['SmallIcon'] for p in props if p['SmallIcon']}
        addresses.update(e['Icon'] for e in entries if e['Icon'])
        records=read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage',recovery.apk.archive))
        images=[]
        for icon in sorted(addresses):
            address='Assets/Image/'+icon
            candidates=[r for r in records if r.get('address','').casefold()==address.casefold()]
            if not candidates:candidates=[r for r in records if r['path'].removesuffix('.png').casefold()==address.casefold()]
            row,=candidates
            obj,=[p.deref() for n,p in recovery.apk.container(row['bundle']) if n.casefold()==row['path'].casefold() and p.deref().type.name=='Sprite']
            images.append(dict(Path=address,AssetGuid=row['guid'],Asset=binding.original_asset(obj)))
        image_script=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
        image_fields=asset_header(image_script,'Original treasure suit property images');image_fields['Entries']=images
        image_guid=hashlib.md5((SCOPE+':images').encode()).hexdigest()
        binding.queue.append((Path('PropertyImages.asset'),image_guid,{11400000:dict(MonoBehaviour=image_fields)}))

        prop_enum=recovery.apk.source.prepared_path/'official-code/common-source/Common/PropType.cs'
        enum_names={int(v):n for n,v in re.findall(r'^\s*(\w+)\s*=\s*(-?\d+)',prop_enum.read_text('utf8'),re.M)}
        text_keys={'treasureDialog_suit_count','{0}','ui_space_before_value','ui_common_prop_plus'}
        text_keys.update('item_'+str(item)+'_name' for suit in properties['Suits'] for item in [suit['ClassId'],*suit['Treasures']])
        text_keys.update('PropType.'+enum_names[p] for p in prop_types)
        text_keys.update('TreasureSuitPropCondition.'+v for v in ('FullCollected','FullThreeStar','FullMaxStar'))
        text_keys.update(p['Unit'] for p in props if p['Unit'])
        def collect(value):
            if isinstance(value,dict):
                if value.get('myKey'):text_keys.add(value['myKey'])
                for v in value.values():collect(v)
            elif isinstance(value,list):
                for v in value:collect(v)
        for _,_,value in binding.queue:
            if isinstance(value,dict):collect(value)
        texts,language_sources=language(text_keys,recovery.apk.source)
        config_guid=hashlib.md5((SCOPE+':property-config').encode()).hexdigest()
        asset_script=ROOT/'unity/Assets/Game/Runtime/Offline/TreasureUI/OfflineTreasureSuitAssets.cs'
        asset=asset_header(asset_script,'OfflineTreasureSuitAssets')
        asset.update(Prefab=prefab,PropertyConfiguration=dict(fileID=4900000,guid=config_guid,type=3),
            PropertyImages=dict(fileID=11400000,guid=image_guid,type=2),Texts=[dict(Key=k,Value=v) for k,v in sorted(texts.items())])
        binding.queue.append((Path('OfflineTreasureSuitAssets.asset'),hashlib.md5((SCOPE+':assets').encode()).hexdigest(),{11400000:dict(MonoBehaviour=asset)}))
        outputs=write_staged_assets(binding.queue,staged)
        config=staged/'property-config.json';config.write_text(json.dumps(dict(prop_cfg=props,entry=entries),ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        meta=Path(str(config)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+config_guid+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[config.name]=sha(config.read_bytes());outputs[meta.name]=sha(meta.read_bytes())
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'treasure-suit-native',previous_outputs=prior.get('outputs'))
        for path in (Path(__file__),ROOT/'scripts/prepare_treasure_suit_ui.py',ROOT/'scripts/prepare_treasure_icon_native.py',
                     asset_script,Path(str(asset_script)+'.meta'),image_script,Path(str(image_script)+'.meta'),prop_enum):
            binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        for path in (ROOT/'scripts/home_native_prefab.py',ROOT/'scripts/home_native_furniture_builder.py',ROOT/'scripts/npc_travel_language.py',
                     ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/HorizontalOrVerticalLayoutGroupJSZX.cs',
                     ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/HorizontalOrVerticalLayoutGroupJSZX.cs.meta',
                     ROOT/'assets/reference/treasure-properties-source-manifest.json',ROOT/'unity/Assets/Game/Resources/Treasure/treasure-properties.json'):
            binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        code_inputs=[constructors.source_root/(n+'.cs') for n,t in constructors.sources.items() if t and (constructors.source_root/(n+'.cs')).exists()]
        layout_source=recovery.apk.source.prepared_path/'official-code/framework-source/UGUIJszx'
        binding.inputs.update(recovery.apk.source.verify_code_files(code_inputs+[prop_enum]+[
            layout_source/(name+'.cs') for name in ('VerticalLayoutGroupJSZX','HorizontalOrVerticalLayoutGroupJSZX')]))
        receipt=dict(manifest=recovery.apk.version,inputs=binding.inputs,selection=selection,member_template=member_selection,recovery=recovery.proof,
            original_suit_fields={k:original[k] for k in names},native_scripts=binding.script_sources,resources=binding.proof,
            audio=binding.audio_proof,prefab=prefab,configuration=table_sources,language=language_sources,outputs=installed)
        text=json.dumps(receipt,ensure_ascii=False,indent=2)+'\n';RECEIPT.write_text(text,'utf8',newline='\n')
        (work/'bound-source-manifest.json').write_text(text,'utf8',newline='\n')
        print('Bound original treasure suit content:',len(installed),'files',flush=True)
    finally:recovery.apk.close()


if __name__=='__main__':prepare()
