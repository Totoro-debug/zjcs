"""Bind the complete original TreasureSlotsPanelContent subtree and original dynamic icon dependency."""
import hashlib,json,uuid
from pathlib import Path
from official_source import ROOT
from home_native_assets import install_staged_assets
from home_native_prefab import bind_prefab,register_script
from native_spine_assets import guid
from prepare_treasure_icon_native import TreasureIconBinding,component,ITEM_BUNDLE,SCOPE as ICON_SCOPE
from prepare_treasure_slots_ui import prepare as prepare_controls,NAMES
from prepare_treasure_ui_native import prepare as prepare_cache,BUNDLE
from prepare_current_partner_native import PartnerNativeRecovery,documents,identity
from prepare_partner_card_native import sha,script_id
from official_manifest import read_asset_manifest
from home_native_assets import write_staged_assets
from npc_travel_language import language

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativeSlots'
RECEIPT=ROOT/'assets/reference/treasure-slots-native-source-manifest.json'
SCOPE='zjcs:treasure-native-slots'

class SlotBinding(TreasureIconBinding):
    def register_scripts(self):
        super().register_scripts();runtime=ROOT/'unity/Assets/Game/Runtime/Official'
        for name in (*NAMES,'ItemListTypeMultiText'):register_script(self,name,runtime/'TreasureSlotsUI'/(name+'.cs'))
        register_script(self,'PlanSwitchWidget',runtime/'BattlePlanUI/PlanSwitchWidget.cs')
        register_script(self,'ContentModerationClickMask',runtime/'WorldUI/WorldContentModerationClickMask.cs')
        # Plan buttons use the existing PCM-backed binding, as in the standalone original widget.
        register_script(self,'ButtonSound',runtime/'WorldUI/WorldButtonSound.cs')
    def load(self,source_guid):
        obj=self.objects.get(source_guid)
        if obj is not None and obj.type.name=='GameObject' and obj.read().m_Name=='ItemIconTreasurePanel':
            proof=ROOT/'assets/reference/treasure-icon-native-source-manifest.json';data=json.loads(proof.read_bytes())
            if data['manifest']!=self.recovery.apk.version or data['selection']['root']!=identity(obj):raise ValueError('Original shared icon root differs')
            for name,expected in data['outputs'].items():
                if sha((ROOT/name).read_bytes())!=expected:raise ValueError('Shared icon payload changed: '+name)
                self.inputs[name]=expected
                if name.endswith('.meta'):self.script_guids.add(guid(ROOT/name))
            self.inputs[proof.relative_to(ROOT).as_posix()]=sha(proof.read_bytes())
            self.redirects[source_guid]=data['prefab'];return data['prefab']
        return super().load(source_guid)
    def replace(self,value):
        if isinstance(value,dict) and set(value)=={'class','ns','asm'} and value['ns']=='Zjcs.Official.WorldUI' and value['asm']=='Zjcs.Official.WorldUI' and value['class'] in ('WorldStateGameObjectConfig','WorldStateUIColorConfig','WorldStateMultiLanguagesConfig'):
            return value
        if isinstance(value,dict) and set(value)=={'class','ns','asm'} and value['class']=='StateSpriteAssetProxyConfig':
            return dict(value,ns='Zjcs.Official.HomeHudUI',asm='Zjcs.Official.HomeHudUI')
        return super().replace(value)
    def original_asset(self,obj):
        if obj.type.name!='GameObject':return super().original_asset(obj)
        # Resolve the original container identity; a similar prefab name never establishes identity.
        proof=self.recovery.apk.provenance(obj)
        original_path,=[name for name,pointer in self.recovery.apk.container(proof['bundle']) if identity(pointer.deref())==identity(obj)]
        if not original_path.casefold().startswith('assets/'):raise ValueError('Original prefab has no Assets address')
        path=self.native/original_path.split('/',1)[1]
        token=guid(Path(str(path)+'.meta'));self.objects[token]=obj
        return self.load(token)

def original_slots(root):
    found=[]
    def visit(go):
        components=[p.component.deref() for p in go.read().m_Component]
        if any(o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName=='TreasureSlotsPanelContent' for o in components):found.append(go)
        for child in components[0].read().m_Children:visit(child.read().m_GameObject.deref())
    visit(root);result,=found;return result

def prepare():
    prepare_controls(True);cache=prepare_cache();work=ROOT/'output/validation'/('treasure-slots-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8');print('Original treasure slots:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=BUNDLE)
    try:
        binding=SlotBinding(work,recovery=recovery,guid_prefix=ICON_SCOPE+':');relative='Prefabs/UI/Treasure/TreasurePanel.prefab'
        panel,=[p.deref() for name,p in recovery.apk.container(BUNDLE) if name.casefold()==('Assets/'+relative).casefold()]
        root=original_slots(panel);native=documents((binding.native/relative).read_text('utf8'))
        native_id,=[v['MonoBehaviour']['m_GameObject']['fileID'] for v in native.values() if v.get('MonoBehaviour',{}).get('m_Script',{}).get('fileID')==script_id('','TreasureSlotsPanelContent')]
        outputs,staged,pointer,selection=bind_prefab(binding,BUNDLE,relative,'TreasureSlotsPanelContent',SCOPE,source_root=root,native_root_id=native_id,
            prepare_component=component,external_asset_types=('Sprite','AnimationClip','GameObject'))
        asset_records=read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage',recovery.apk.archive))
        requested=set();text_keys=set()
        for _,_,docs in binding.queue:
            if not isinstance(docs,dict):continue
            def collect(value):
                if isinstance(value,dict):
                    if 'ContainerSource' in value and value.get('mAssetGuid'):requested.add(value['mAssetGuid'])
                    if value.get('myKey'):text_keys.add(value['myKey'])
                    for item in value.values():collect(item)
                elif isinstance(value,list):
                    for item in value:collect(item)
            collect(docs)
        dynamic=[]
        for token in sorted(requested):
            row,=[r for r in asset_records if r['guid']==token]
            obj,=[p.deref() for name,p in recovery.apk.container(row['bundle']) if name.casefold()==row['path'].casefold() and p.deref().type.name=='Sprite']
            dynamic.append(dict(guid=token,pointer=binding.original_asset(obj),source=recovery.apk.provenance(obj),manifest_asset=row))
        index=0
        while index<len(binding.queue):
            _,_,docs=binding.queue[index]
            if isinstance(docs,dict):updated=binding.replace(docs);docs.clear();docs.update(updated)
            index+=1
        texts,text_sources=language(text_keys,recovery.apk.source)
        catalog=ROOT/'unity/Assets/Game/Runtime/Offline/TreasureUI/OfflineTreasureSlotsAssets.cs';catalog_meta=Path(str(catalog)+'.meta')
        for path in (catalog,catalog_meta):binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        fields=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),m_PrefabAsset=dict(fileID=0),
            m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,m_Script=dict(fileID=11500000,guid=guid(catalog_meta),type=3),
            m_Name='OfflineTreasureSlotsAssets',m_EditorClassIdentifier='',Sprites=[dict(Guid=row['guid'],Sprite=row['pointer']) for row in dynamic],
            Texts=[dict(Key=k,Value=v) for k,v in sorted(texts.items())])
        token=hashlib.md5((SCOPE+':catalog').encode()).hexdigest()
        binding.queue.append((Path('OfflineTreasureSlotsAssets.asset'),token,{11400000:dict(MonoBehaviour=fields)}))
        outputs=write_staged_assets(binding.queue,staged)
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'treasure-slots-native',previous_outputs=prior.get('outputs'))
        for record in binding.script_sources.values():
            if sha((ROOT/record['path']).read_bytes())!=record['sha256']:
                raise ValueError('Native slot component changed during binding: '+record['path'])
        for name in ('prepare_treasure_slots_native.py','prepare_treasure_icon_native.py','home_native_prefab.py','home_native_furniture_builder.py'):
            p=ROOT/'scripts'/name;binding.inputs[p.relative_to(ROOT).as_posix()]=sha(p.read_bytes())
        record=dict(manifest=recovery.apk.version,inputs=binding.inputs,selection=selection,recovery=recovery.proof,native_scripts=binding.script_sources,
            resources=binding.proof,prefab=pointer,audio=binding.audio_proof,identical_dependencies=binding.identical_dependencies,outputs=installed,
            dynamic_sprites=dynamic,texts=[dict(Key=k,Value=v) for k,v in sorted(texts.items())],language=text_sources)
        text=json.dumps(record,ensure_ascii=False,indent=2)+'\n';RECEIPT.write_text(text,'utf8',newline='\n');(work/'bound-source-manifest.json').write_text(text,'utf8',newline='\n')
        print('Bound complete original treasure slot panel:',len(installed),flush=True)
    finally:recovery.apk.close()

if __name__=='__main__':prepare()
