"""Bind original treasure icon prefabs with complete recursive native UI dependencies."""
import hashlib
import json
import uuid
from pathlib import Path
from UnityPy.classes.PPtr import PPtr
from home_native_assets import install_staged_assets
from home_native_furniture_builder import HomeNativeFurnitureBinding
from home_native_prefab import bind_prefab,register_script,restore_original_empty_values,restore_root_animation_curve_paths
from native_spine_assets import guid
from official_source import ROOT
from prepare_current_partner_native import PartnerNativeRecovery,documents,identity
from prepare_partner_card_native import sha
from prepare_treasure_icon_controls import prepare as prepare_controls,NAMES
from prepare_treasure_ui_native import prepare as prepare_cache,BUNDLE
from prepare_battle_plan_widget_native import component as plan_component

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/NativeIcons'
RECEIPT=ROOT/'assets/reference/treasure-icon-native-source-manifest.json'
SCOPE='zjcs:treasure-native-icons'
ITEM_BUNDLE='defaultpackage_assets_prefabs_ui_item.bundle'

def component(binding,script,fields):
    plan_component(binding,script,fields)
    if script['type']=='UIClickSound':
        owner=binding.original(script['original'])
        binding.sound(binding.recovery.fields(owner)['AkEvent'],owner)

class TreasureIconBinding(HomeNativeFurnitureBinding):
    def register_scripts(self):
        super().register_scripts();runtime=ROOT/'unity/Assets/Game/Runtime/Official'
        for path in (runtime/'ProfessionUI').glob('*.cs'):
            if Path(str(path)+'.meta').exists():register_script(self,path.stem,path)
        for name in NAMES:register_script(self,name,runtime/'TreasureIconUI'/(name+'.cs'))
        register_script(self,'TreasureStarInfo',runtime/'TreasureUI/TreasureStarInfo.cs')
        for name,file in {'MaintainGlobalScaleUI':'WorldMaintainGlobalScaleUI','NoDrawingRayCast':'NoDrawingRayCast','UIMesh':'UIMesh',
                          'ItemProgress':'WorldItemProgress','LayoutElementClamper':'WorldLayoutElementClamper'}.items():
            register_script(self,name,runtime/'WorldUI'/(file+'.cs'))
        self.prefab_selections={}
        self.identical_dependencies={}
    def verify_duplicate(self,left,right,seen=None):
        pair=(identity(left),identity(right));seen=set() if seen is None else seen
        if pair[0]==pair[1] or pair in seen:return
        seen.add(pair)
        if left.type!=right.type or left.get_raw_data()!=right.get_raw_data():
            raise ValueError('Native export coalesced different original objects: '+repr(pair))
        if left.type.name=='Texture2D' and left.read().image.tobytes()!=right.read().image.tobytes():
            raise ValueError('Coalesced original textures have different stream pixels: '+repr(pair))
        def visit(a,b):
            if isinstance(a,dict) and 'm_PathID' in a and 'm_FileID' in a:
                if a['m_PathID']:
                    self.verify_duplicate(PPtr(**a,assetsfile=left.assets_file).deref(),PPtr(**b,assetsfile=right.assets_file).deref(),seen)
            elif isinstance(a,dict):
                for key,value in a.items():visit(value,b[key])
            elif isinstance(a,list):
                for x,y in zip(a,b):visit(x,y)
        visit(self.recovery.fields(left),self.recovery.fields(right))
        self.identical_dependencies[' / '.join(pair)]=dict(type=left.type.name,sha256=sha(left.get_raw_data()),closure=sorted(seen))
    def same_original_reference(self,left,right):
        self.verify_duplicate(left,right)
        return True
    def learn(self,source,native,owner):
        if isinstance(source,dict) and 'm_PathID' in source and 'm_FileID' in source and source['m_PathID'] and isinstance(native,dict) and native.get('guid'):
            old=self.objects.get(native['guid'])
            if old is not None:
                obj=PPtr(**source,assetsfile=owner.assets_file).deref()
                if identity(old)!=identity(obj):
                    self.verify_duplicate(old,obj)
                    return
        return super().learn(source,native,owner)
    def original_asset(self,obj):
        if obj.type.name=='MonoBehaviour':return super().original_asset(obj)
        key=identity(obj)
        matches=[token for token,source in self.objects.items() if identity(source)==key]
        if not matches and obj.type.name=='Sprite':
            source_key,source_id=self.recovery.fields(obj)['m_RenderDataKey']
            render_guid=''.join(format(source_key['data['+str(i)+']'],'08x')[::-1] for i in range(4))
            name=self.name_key(obj.read(check_read=False).m_Name)
            candidates=[]
            for token in [name]+[name+str(i) for i in range(100)]:
                for path in self.by_name.get(token,[]):
                    if path.suffix!='.asset':continue
                    values=documents(path.read_text('utf8'))
                    if len(values)==1 and next(iter(values.values())).get('Sprite',{}).get('m_RenderDataKey')=={render_guid:source_id}:candidates.append(path)
            path,=candidates;token=guid(Path(str(path)+'.meta'));self.objects[token]=obj;matches=[token]
        token,=matches
        return self.load(token)
    def load(self,source_guid):
        if source_guid in self.redirects:return self.redirects[source_guid]
        obj=self.objects.get(source_guid)
        if obj is not None and obj.type.name=='GameObject':
            path=self.native_path(source_guid,obj);relative=path.relative_to(self.native).as_posix();docs=documents(path.read_text('utf8'))
            native_id,=[key for key,d in docs.items() if 'GameObject' in d and
                (docs[d['GameObject']['m_Component'][0]['component']['fileID']].get('Transform') or docs[d['GameObject']['m_Component'][0]['component']['fileID']].get('RectTransform'))['m_Father']['fileID']==0]
            token=hashlib.md5((SCOPE+':prefab:'+identity(obj)).encode()).hexdigest()
            result=dict(fileID=native_id,guid=token,type=3);self.redirects[source_guid]=result;self.script_guids.add(token)
            name='Prefab_'+sha(identity(obj).encode())[:16]
            _,_,actual,selection=bind_prefab(self,ITEM_BUNDLE,relative,name,SCOPE,source_root=obj,native_root_id=native_id,prepare_component=component,external_asset_types=('Sprite','AnimationClip','GameObject'))
            if actual!=result:raise ValueError('Original UI prefab identity changed while resolving its closure')
            self.prefab_selections[identity(obj)]=dict(path=relative,output=name+'.prefab',pointer=result,selection=selection)
            return result
        result=super().load(source_guid)
        if obj is not None:
            for _,token,data in self.queue:
                if token==result.get('guid') and isinstance(data,dict):
                    for value in data.values():
                        fields,=value.values();restored=restore_original_empty_values(self.recovery.fields(obj),fields)
                        if obj.type.name=='AnimationClip':restored+=restore_root_animation_curve_paths(self,obj,fields)
                        if restored:
                            for proof in self.proof.values():
                                if proof.get('guid')==token:proof['restored_original_empty_values']=restored
        return result
    def replace(self,value):
        if isinstance(value,dict) and set(value)=={'class','ns','asm'}:
            name=value['class']
            path=ROOT/'unity/Assets/Game/Runtime/Official/WorldUI'/('World'+name+'.cs')
            if name in ('StateGameObjectConfig','StateUIColorConfig') or (name.startswith('State') and path.exists()):
                return dict(value,**{'class':'World'+name,'ns':'Zjcs.Official.WorldUI','asm':'Zjcs.Official.WorldUI'})
        return super().replace(value)

def prepare():
    prepare_controls(True);cache=prepare_cache()
    work=ROOT/'output/validation'/('treasure-icon-native-'+uuid.uuid4().hex);work.mkdir(parents=True)
    (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
    print('Original treasure icon closure:',work,flush=True)
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=BUNDLE)
    try:
        binding=TreasureIconBinding(work,recovery=recovery,guid_prefix=SCOPE+':')
        relative='Prefabs/UI/Item/ItemIconTreasurePanel.prefab'
        outputs,staged,pointer,selection=bind_prefab(binding,ITEM_BUNDLE,relative,'ItemIconTreasurePanel',SCOPE,prepare_component=component,external_asset_types=('Sprite','AnimationClip','GameObject'))
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        installed=install_staged_assets(work,staged,outputs,DEST,'treasure-icon-native',previous_outputs=prior.get('outputs'))
        for name in ('prepare_treasure_icon_native.py','home_native_prefab.py','home_native_furniture_builder.py'):
            path=ROOT/'scripts'/name;binding.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        record=dict(manifest=recovery.apk.version,inputs=binding.inputs,selection=selection,recovery=recovery.proof,
            native_scripts=binding.script_sources,resources=binding.proof,prefab=pointer,prefabs=binding.prefab_selections,
            audio=binding.audio_proof,identical_dependencies=binding.identical_dependencies,outputs=installed)
        text=json.dumps(record,ensure_ascii=False,indent=2)+'\n';RECEIPT.write_text(text,'utf8',newline='\n');(work/'bound-source-manifest.json').write_text(text,'utf8',newline='\n')
        print('Bound native treasure icon closure:',len(installed),flush=True)
    finally:recovery.apk.close()

if __name__=='__main__':prepare()
