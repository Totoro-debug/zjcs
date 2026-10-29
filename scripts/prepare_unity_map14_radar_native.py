"""Bind the original StageCostTip hierarchy, fields, sprites, font and animations."""
import collections,json,re,struct,subprocess
from pathlib import Path
from official_apk import Apk,ROOT
from official_native_yaml_verification import documents
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map14_hero_statue_inputs import workspace
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import runtime_meta
DEST=ROOT/'unity/Assets/Official/World/Map/14/NativeRadarTips'
RECEIPT=ROOT/'assets/reference/official-map/map14-native-radar-tips-source-manifest.json'
HEADER=re.compile(r'(?m)^--- !u!(\d+) &(-?\d+)\n')
GUID=re.compile(r'(?m)^guid: ([0-9a-f]{32})$')
PTR=re.compile(r'\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}')
def record(p):
    raw=p.read_bytes();entry=dict(path=source_path(p))
    if p.suffix=='.cs' and p.is_relative_to(ROOT/'unity'):
        raw=raw.replace(b'\r\n',b'\n');entry['hashLineEndings']='LF'
    return dict(entry,sha256=sha(raw))
def io(p):return Path('\\\\?\\'+str(p))
def build():
    cache=workspace();export=cache/'export/ExportedProject/Assets'
    source=export/'Prefabs/Map/Common/EntityUI/RadarTip/StageCostTip.prefab'
    graph_path=ROOT/'unity/Assets/Official/World/Map/14/Source/native-entity-object-graph.json'
    graph=json.loads(graph_path.read_bytes());objects={(o['cab'],o['path_id']):o for o in graph['objects']}
    source_proof=json.loads((ROOT/'assets/reference/official-map/map14-native-entity-object-graph-source-manifest.json').read_bytes())
    expected,=[r for r in source_proof['outputs'] if r['path']==source_path(graph_path)]
    if graph['manifest']!='871_156598' or sha(graph_path.read_bytes())!=expected['sha256']:raise ValueError('Original radar graph changed')
    input_receipt=json.loads((cache/'input-source-manifest.json').read_bytes())
    for r in input_receipt['inputs']:
        if sha((cache/'inputs'/r['output']).read_bytes())!=r['converter_input_sha256']:raise ValueError('Original radar converter input changed')
    for r in input_receipt['outputs']+input_receipt['tools']:
        if sha((ROOT/r['path']).read_bytes())!=r['sha256']:raise ValueError('Original radar component/tool record changed')
    components=json.loads((cache/'components.json').read_bytes())
    rows={(r['cab'],r['path_id']):r for r in components['components']}
    metadata={}
    # Read metadata only; generated SDK implementation files are never opened.
    for p in export.rglob('*.meta'):
        match=GUID.search(io(p).read_text(encoding='utf-8-sig'))
        if match:metadata[match[1]]=p.with_suffix('')
    text=source.read_text(encoding='utf-8-sig');docs=documents(text)
    indexed={};root=('CAB-23bef326149fbeb0f636881d59f9eecc','-6973730358550827748')
    def original_tree(go_key,route):
        go=objects[go_key];counts=collections.Counter()
        for ptr in go['references']:
            if not re.fullmatch(r'\.m_Component\[\d+\]\.component',ptr['field']):continue
            key=(ptr['cab'],ptr['path_id']);o=objects[key];kind=o.get('managed_type',o['type']).split('.')[-1]
            label=(route,kind,counts[kind]);counts[kind]+=1;indexed[label]=key
            if kind in ('Transform','RectTransform'):
                for child in o['references']:
                    if re.fullmatch(r'\.m_Children\[\d+\]',child['field']):
                        t=objects[child['cab'],child['path_id']];ref,=[r for r in t['references'] if r['field']=='.m_GameObject']
                        k=(ref['cab'],ref['path_id']);original_tree(k,route+'/'+objects[k]['name'])
    original_tree(root,objects[root]['name'])
    transforms={int(b['m_GameObject']['fileID']):fid for fid,(kind,b) in docs.items() if kind in ('Transform','RectTransform')}
    def route(goid):
        go=docs[goid][1];parent=int(docs[transforms[goid]][1]['m_Father']['fileID'])
        return (route(int(docs[parent][1]['m_GameObject']['fileID']))+'/' if parent else '')+str(go['m_Name'])
    native={};native_go={}
    for goid,(kind,b) in docs.items():
        if kind!='GameObject':continue
        path=route(goid);counts=collections.Counter();native_go[path]=goid
        for c in b['m_Component']:
            fid=int(c['component']['fileID']);k,f=docs[fid]
            if k=='MonoBehaviour':k=metadata[f['m_Script']['guid']].stem
            label=(path,k,counts[k]);counts[k]+=1;native[label]=fid
    if native.keys()!=indexed.keys():raise ValueError('Original radar hierarchy/components differ: '+str(native.keys()^indexed.keys()))
    by_fid={fid:indexed[label] for label,fid in native.items()}
    native_ids={key:fid for fid,key in by_fid.items()}
    for label,key in indexed.items():
        original_go,=[r for r in objects[key]['references'] if r['field']=='.m_GameObject']
        native_ids[original_go['cab'],original_go['path_id']]=native_go[label[0]]
    checks=[];apk=Apk.current();original_bundles={}
    sprite_proof=json.loads((ROOT/'assets/reference/official-map/map14-native-entity-sprites-source-manifest.json').read_bytes())
    default_materials={r['identity']:r for r in sprite_proof['default_materials']}
    sprite_outputs={r['path']:r for r in sprite_proof['outputs']}
    def original_object(o):
        if o['input'] not in original_bundles:original_bundles[o['input']]={(x.assets_file.name,str(x.path_id)):x for x in apk.objects(o['input'])}
        obj=original_bundles[o['input']][o['cab'],o['path_id']]
        if sha(obj.get_raw_data())!=o['payload_sha256']:raise ValueError('Original radar object payload changed')
        return obj
    def compare(expected,actual,o,field):
        if isinstance(expected,dict):
            if set(expected)=={'m_FileID','m_PathID'}:
                if not expected['m_PathID']:
                    if actual!={'fileID':0}:raise ValueError('Original null radar pointer differs: '+field)
                    return
                ref,=[r for r in o['references'] if r['field']==field]
                key=ref['cab'],ref['path_id']
                if key in native_ids:
                    if actual!={'fileID':native_ids[key]}:raise ValueError('Original local radar pointer differs: '+field)
                elif str(actual.get('guid','')).startswith('00000000'):
                    original=objects[key]
                    proof=default_materials.get(key[0].lower()+':'+key[1])
                    if proof is None or original['payload_sha256']!=proof['original_payload_sha256'] or original.get('name')!='Sprites-Default' or actual!={'fileID':10754,'guid':'0000000000000000f000000000000000','type':0}:raise ValueError('Original default radar material export differs: '+field)
                else:
                    p=metadata[str(actual['guid'])];target=documents(io(p).read_text(encoding='utf-8-sig'))[int(actual['fileID'])]
                    original=objects[key];name=target[1].get('m_Name') or ''
                    if name!=original.get('name',''):raise ValueError('Original external radar pointer name differs: '+field)
                return
            if expected.keys()!=actual.keys():raise ValueError('Original radar field shape differs: '+field)
            for k,v in expected.items():compare(v,actual[k],o,field+'.'+k)
        elif isinstance(expected,list):
            if len(expected)!=len(actual):raise ValueError('Original radar list differs: '+field)
            for i,(a,b) in enumerate(zip(expected,actual)):compare(a,b,o,field+'['+str(i)+']')
        elif isinstance(expected,float):
            if struct.pack('<f',expected)!=struct.pack('<f',float(str(actual))):raise ValueError('Original radar float32 differs: '+field)
        elif isinstance(expected,str):
            if expected!=('' if actual is None else getattr(actual,'scalar',actual)):raise ValueError('Original radar string differs: '+field)
        elif expected!=('' if actual is None and expected=='' else actual):raise ValueError('Original radar scalar differs: '+field)
    for fid,key in by_fid.items():
        o=objects[key];original_object(o)
        if o['type']=='MonoBehaviour':
            row=rows[key]
            if row['payload_sha256']!=o['payload_sha256']:raise ValueError('Original radar component record differs')
            fields=row['fields']
        else:
            original=original_object(o).read_typetree(check_read=True)
            names={
                'Transform':('m_LocalPosition','m_LocalRotation','m_LocalScale'),
                'RectTransform':('m_LocalPosition','m_LocalRotation','m_LocalScale','m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_SizeDelta','m_Pivot'),
                'SpriteRenderer':('m_Enabled','m_SortingLayerID','m_SortingOrder','m_Color','m_FlipX','m_FlipY','m_DrawMode','m_Size','m_Materials','m_Sprite'),
                'MeshRenderer':('m_Enabled','m_SortingLayerID','m_SortingOrder','m_Materials'),
                'Animation':('m_Enabled','m_Animation','m_Animations','m_WrapMode','m_PlayAutomatically','m_AnimatePhysics','m_CullingType')
            }[o['type']]
            fields={k:original[k] for k in names}
        body=docs[fid][1]
        for name,value in fields.items():
            if name in ('m_GameObject','m_Script'):continue
            compare(value,body[name],o,'.'+name)
        checks.append(dict(cab=key[0],path_id=key[1],type=o.get('managed_type',o['type']),fields=list(fields)))
    # Reuse only the exact selected-version original font/material/atlas payloads.
    font_receipt=ROOT/'assets/reference/official-map/map13-native-text-font-source-manifest.json'
    font_proof=json.loads(font_receipt.read_bytes())
    for row in font_proof['inputs']:
        obj=next(o for o in apk.objects(row['source']['bundle']) if str(o.path_id)==row['source']['path_id'])
        if sha(obj.get_raw_data())!=row['object_sha256']:raise ValueError('Original shared radar font changed')
    font_payloads={}
    for row in font_proof['outputs']:
        raw=(ROOT/row['path']).read_bytes()
        if sha(raw)!=row['sha256']:
            # Preserve shared work. Reproduce the pinned committed payload only
            # when its bytes still match the existing official font receipt.
            raw=subprocess.check_output(['git','show','HEAD:'+row['path']],cwd=ROOT)
        if sha(raw)!=row['sha256']:raise ValueError('Pinned native radar font payload changed')
        font_payloads[Path(row['path']).name]=raw
    redirects={};dependencies=[];font_outputs=[]
    def redirect(old,target):
        old_guid=GUID.search(io(Path(str(old)+'.meta')).read_text(encoding='utf-8-sig'))[1]
        new_guid=GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
        redirects[old_guid]=new_guid;dependencies.extend((record(target),record(Path(str(target)+'.meta'))))
    scripts={
        'MapControllerEntityRadarTipStageCost':ROOT/'unity/Assets/Game/Runtime/Official/EntityRadarCost/MapControllerEntityRadarTipStageCost.cs',
        'MultiLanguageText':ROOT/'unity/Assets/Game/Runtime/Official/World/NativeLabelUI/MultiLanguageText.cs',
        'TMPEffect':ROOT/'unity/Assets/Game/Runtime/Official/World/NativeText/TMPEffect.cs'
    }
    scripts['TextMeshPro'],=[p for p in (ROOT/'unity/Library/PackageCache').glob('com.unity.textmeshpro@*/Scripts/Runtime/TextMeshPro.cs')]
    for old in {metadata[b['m_Script']['guid']] for kind,b in docs.values() if kind=='MonoBehaviour'}:redirect(old,scripts[old.stem])
    font_source=metadata[docs[native['StageCostTip/Text','TextMeshPro',0]][1]['m_fontAsset']['guid']]
    font_text=documents(io(font_source).read_text(encoding='utf-8-sig'))[11400000][1]
    font_root=ROOT/'unity/Assets/Official/World/Map/13/NativeText'
    font_guids={}
    for name in ('FZY4K-Dynamic.asset','FZY4K-Dynamic.mat','FZY4K-Dynamic-Atlas.texture.asset'):
        old_guid=GUID.search(Path(str(font_root/name)+'.meta').read_text(encoding='utf-8-sig'))[1]
        target=DEST/'Font'/name;new_guid=meta(target,'NativeFormatImporter');font_guids[old_guid]=new_guid
    for name in ('FZY4K-Dynamic.asset','FZY4K-Dynamic.mat','FZY4K-Dynamic-Atlas.texture.asset'):
        raw=font_payloads[name].decode('utf-8-sig')
        raw=re.sub(r'guid: ([0-9a-f]{32})',lambda m:'guid: '+font_guids.get(m[1],m[1]),raw)
        target=DEST/'Font'/name;write(target,raw.encode());font_outputs.extend((target,Path(str(target)+'.meta')))
    redirect(font_source,DEST/'Font/FZY4K-Dynamic.asset')
    redirect(metadata[font_text['material']['guid']],DEST/'Font/FZY4K-Dynamic.mat')
    redirect(metadata[font_text['m_AtlasTextures'][0]['guid']],DEST/'Font/FZY4K-Dynamic-Atlas.texture.asset')
    dependencies.extend((record(font_root/'FZY4K.ttf'),record(Path(str(font_root/'FZY4K.ttf')+'.meta'))))
    material_proof=json.loads((ROOT/'assets/reference/official-map/map14-native-entity-materials-native-source-manifest.json').read_bytes())
    materials={(r['cab'].lower(),r['path_id']):r['output'] for r in material_proof['materials']}
    for identity,row in default_materials.items():
        cab,pid=identity.split(':');materials[cab,pid]=sprite_outputs[row['output']]
    sprites=[];parts=HEADER.split(text)
    outputs=list(font_outputs);inputs=[];assets=[]
    for i in range(1,len(parts),3):
        fid=int(parts[i+1]);kind,body=docs[fid];o=objects[by_fid[fid]] if fid in by_fid else None
        if kind=='SpriteRenderer':
            refs=[r for r in o['references'] if r['field']=='.m_Sprite']
            if refs:sprites.append(dict(renderer=fid,sourceKey=o['cab'].lower()+':'+o['path_id']))
            parts[i+2]=re.sub(r'(?m)^  m_Sprite: [^\n]+','  m_Sprite: {fileID: 0}',parts[i+2])
        if kind.endswith('Renderer'):
            for index,pointer in enumerate(body.get('m_Materials',[])):
                if 'guid' not in pointer or pointer['guid'] in redirects:continue
                ref,=[r for r in o['references'] if r['field']=='.m_Materials['+str(index)+']']
                record0=materials[ref['cab'].lower(),ref['path_id']];p=ROOT/record0['path']
                if sha(p.read_bytes())!=record0['sha256']:raise ValueError('Native radar renderer material changed')
                if str(pointer['guid']).startswith('00000000'):
                    new_guid=GUID.search(Path(str(p)+'.meta').read_text(encoding='utf-8-sig'))[1]
                    old='{fileID: '+str(pointer['fileID'])+', guid: '+pointer['guid']+', type: '+str(pointer['type'])+'}'
                    parts[i+2]=parts[i+2].replace(old,'{fileID: 2100000, guid: '+new_guid+', type: 2}')
                    dependencies.extend((record(p),record(Path(str(p)+'.meta'))))
                else:redirect(metadata[pointer['guid']],p)
    # All remaining external references must be original animation clips.
    for match in PTR.finditer(text):
        fid,guid,typ=match.groups()
        if guid in redirects or guid.startswith('00000000'):continue
        p=metadata[guid]
        if p.suffix=='.asset' and 'Sprite' in str(p):continue
        if p.suffix!='.anim':
            if any(str(b.get('m_Sprite',{}).get('guid',''))==guid for k,b in docs.values() if k=='SpriteRenderer'):continue
            raise ValueError('Bind remaining original radar dependency: '+str(p))
        target=DEST/p.relative_to(export);raw=p.read_bytes();clip_text=raw.decode('utf-8-sig')
        for clip_ref in PTR.finditer(clip_text):
            if clip_ref[2] not in redirects:raise ValueError('Bind original radar animation script first: '+clip_ref[2])
        clip_text=PTR.sub(lambda m:'{fileID: '+m[1]+', guid: '+redirects[m[2]]+', type: '+m[3]+'}',clip_text)
        write(target,clip_text.encode());meta(target,'NativeFormatImporter')
        outputs.extend((target,Path(str(target)+'.meta')));redirect(p,target);assets.append(dict(output=record(target)))
    text=parts[0]+''.join('--- !u!'+parts[i]+' &'+parts[i+1]+'\n'+parts[i+2] for i in range(1,len(parts),3))
    text=PTR.sub(lambda m:'{fileID: '+m[1]+', guid: '+redirects.get(m[2],m[2])+', type: '+m[3]+'}',text)
    root_go=native_go['StageCostTip'];binder_id=max(docs)+1
    binder=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14HeroStatues/NativeMap14HeroStatueSprites.cs';dependencies.extend((record(binder),record(Path(str(binder)+'.meta'))))
    binder_guid=GUID.search(Path(str(binder)+'.meta').read_text(encoding='utf-8-sig'))[1]
    text=text.replace('  - component: {fileID: '+str(native['StageCostTip','Transform',0])+'}','  - component: {fileID: '+str(native['StageCostTip','Transform',0])+'}\n  - component: {fileID: '+str(binder_id)+'}',1)
    text+='--- !u!114 &'+str(binder_id)+'\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: '+str(root_go)+'}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+binder_guid+', type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n  bindings:\n'
    for s in sprites:text+='  - target: {fileID: '+str(s['renderer'])+'}\n    sourceKey: '+s['sourceKey']+'\n'
    target=DEST/'Prefabs/StageCostTip.prefab';write(target,text.encode());meta(target,'PrefabImporter');outputs.extend((target,Path(str(target)+'.meta')))
    for path in sorted({p.parent for p in outputs}|{DEST},key=lambda p:len(p.parts)):
        while path.is_relative_to(DEST):meta(path,folder=True);outputs.append(Path(str(path)+'.meta'));path=path.parent
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14RadarTips';editor=ROOT/'unity/Assets/Game/Editor/Map14/RadarNative'
    refs=['Zjcs.Official.EntityRadarCost','Zjcs.Official.EntityWorldInput','Zjcs.Official.World.NativeLabelUI','Zjcs.Offline.World.NativeMap14EntityAssets','Zjcs.Offline.World.NativeMap14HeroStatues']
    for folder,name,references,platforms in (
        (runtime,'Zjcs.Offline.World.NativeMap14RadarTips',refs,[]),
        (editor,'Zjcs.Editor.Map14.RadarNative',refs+['Zjcs.Offline.World.NativeMap14RadarTips','Zjcs.Official.EntityRadar','Zjcs.Official.EntityViews','Zjcs.Official.EntityLifecycle','Zjcs.Official.StoryPlayables','Zjcs.Official.StoryPlayableManager','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeText','Zjcs.Offline.World.NativeLabelResources','Zjcs.Offline.World.NativeMap14Labels','Unity.TextMeshPro','Zjcs.Editor.Map14.RadarFastKill'],['Editor'])):
        folder.mkdir(parents=True,exist_ok=True);write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for p in sorted(folder.iterdir()):
            if p.is_file() and p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    catalog=ROOT/'unity/Assets/Game/Resources/Map14/Native/radar-tips-catalog.asset'
    script_guid=GUID.search(Path(str(runtime/'NativeMap14RadarTipCatalog.cs')+'.meta').read_text(encoding='utf-8-sig'))[1]
    prefab_guid=meta(target,'PrefabImporter')
    write(catalog,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+script_guid+', type: 3}\n  m_Name: Map14 original radar tips\n  m_EditorClassIdentifier:\n  Manifest: 871_156598\n  StageCostTip: {fileID: '+str(root_go)+', guid: '+prefab_guid+', type: 3}\n').encode());meta(catalog,'NativeFormatImporter');outputs.extend((catalog,Path(str(catalog)+'.meta')))
    write(RECEIPT,encode(dict(manifest=graph['manifest'],mapClassId=14,source_graph=record(graph_path),inputs=inputs,dependencies=list({r['path']:r for r in dependencies}.values()),font_source=record(font_receipt),tools=[record(Path(__file__))],outputs=[record(p) for p in sorted(set(outputs))],
        prefab=dict(path=source_path(target),guid=meta(target,'PrefabImporter'),originalRoot=root),field_checks=checks,sprite_bindings=sprites,assets=assets,
        operation='Preserve original StageCostTip GameObject/component hierarchy and exported scalar YAML text. Compare complete consumed original MonoBehaviour fields including TMP/font/effect data and original pointer identities, float32 bits, transform/renderer/animation properties. Bind exact current original font and migrated scripts/materials, original animation clips and packed Sprite library before activation. Add only explicit sprite-loading metadata.',
        validation_entry='Zjcs.Editor.Map14.OfficialMap14RadarNativeAcceptance.Run',
        validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile and Play Mode passed: original hierarchy, nonempty text 1/10/100, character-count icon offsets, cloned original dynamic font, original native prefab icon, fade curves/event, original tip-pool inactive return/reactivated reuse, sprite-operation release and localization teardown. Actual rendered frames and four-statue regression viewed. Root project static metadata check passed.',
        limitations=['Cost values and item icon lookup use explicit presentation fixtures; no claim of original item 325 configuration or icon. Formal item-icon loading, player/map/HUD/simulated pointer/save, complete ProjectTools.Validate and Windows Player remain required.'])) )
    print('Original StageCostTip bound:',len(checks),'components;',len(sprites),'sprite bindings;',len(set(outputs)),'outputs')
if __name__=='__main__':build()
