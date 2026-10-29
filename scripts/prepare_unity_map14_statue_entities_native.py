"""Bind all original components of the four hero-statue entity prefabs."""
import json,re
from pathlib import Path
from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map14_hero_statue_inputs import workspace
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import runtime_meta

DEST=ROOT/'unity/Assets/Official/World/Map/14/NativeStatueEntities'
RUNTIME=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14StatueEntities'
EDITOR=ROOT/'unity/Assets/Game/Editor/Map14/StatueEntities'
RECEIPT=ROOT/'assets/reference/official-map/map14-native-statue-entities-source-manifest.json'
GUID=re.compile(r'(?m)^guid: ([0-9a-f]{32})$')
PTR=re.compile(r'\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}')
HEADER=re.compile(r'(?m)^--- !u!(\d+) &(-?\d+)\n')
MODULES={
    'MyEntity':'EntityLifecycle','MyGridTransform':'EntityLifecycle',
    'MyWorldStage':'LegacyWorldStages','AttachAgent':'StoryAttachment',
    'MyVisibilityGrid':'EntityRootVisibility','WorldStageInput':'EntityWorldInput',
    'MyMapDisplay':'MapDisplays','EntityAwardItemHook':'EntityAwardHook',
    'MapControllerEntityRadarSupport':'EntityRadar',
    **dict.fromkeys(('MyWorldEntityLevel','MyStageBehTouch','MyStageResultAwardExtraProp',
       'MyStageResultAwardStatusEffect','MyStageResultModifyHpPercent',
       'MyStageConPlayerPeriodicityInteraction','MyStageResultPeriodicityRandomAward','MyEntitySource'),'EntityClientComponents')}

def record(p):
    raw=p.read_bytes();entry=dict(path=source_path(p))
    if p.suffix=='.cs' and p.is_relative_to(ROOT/'unity'):
        raw=raw.replace(b'\r\n',b'\n');entry['hashLineEndings']='LF'
    return dict(entry,sha256=sha(raw))
def guid(p):return GUID.search(Path(str(p)+'.meta').read_text(encoding='utf-8-sig'))[1]

def build():
    cache=workspace();export=cache/'export/ExportedProject/Assets'
    proof_path=cache/'export-source-manifest.json';proof=json.loads(proof_path.read_bytes())
    if proof['manifest']!='871_156598':raise ValueError('Original statue entity version differs')
    for key in ('input_receipt','tool','generator','entry','verification_library'):
        row=proof[key]
        if sha((ROOT/row['path']).read_bytes())!=row['sha256']:raise ValueError('Original entity converter proof changed: '+row['path'])
    input_proof=json.loads((ROOT/proof['input_receipt']['path']).read_bytes())
    for row in input_proof['inputs']:
        if sha((cache/'inputs'/row['output']).read_bytes())!=row['converter_input_sha256']:raise ValueError('Original entity hydrated converter input changed')
    for row in input_proof['outputs']+input_proof['tools']:
        if sha((ROOT/row['path']).read_bytes())!=row['sha256']:raise ValueError('Original entity source/tool changed: '+row['path'])
    exports={Path(row['path']).stem:row for row in proof['outputs'] if '/CommonPlay/' in row['path']}
    metadata={}
    # Only serialization metadata is inspected, never generated SDK implementations.
    for p in export.rglob('*.meta'):
        match=GUID.search(Path('\\\\?\\'+str(p)).read_text(encoding='utf-8-sig'))
        if match:metadata[match[1]]=p.with_suffix('')
    dependencies={};outputs=[];prefabs=[]
    def dependency(p):
        for f in (p,Path(str(p)+'.meta')):dependencies[source_path(f)]=record(f)
    scripts={name:ROOT/'unity/Assets/Game/Runtime/Official'/module/(name+'.cs') for name,module in MODULES.items()}
    for p in scripts.values():dependency(p)
    adapter=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14EntityLifecycle/NativeMyEntityBinding.cs';dependency(adapter)
    code_proofs={
        'EntityLifecycle':'entity-lifecycle','LegacyWorldStages':'legacy-world-stage',
        'StoryAttachment':'story-attachment-runtime','EntityRootVisibility':'entity-root-visibility',
        'EntityWorldInput':'world-input','MapDisplays':'map-display-runtime',
        'EntityAwardHook':'entity-award-hook','EntityRadar':'entity-radar','EntityClientComponents':'client-components'}
    for module,name in code_proofs.items():
        source_receipt=ROOT/'assets/reference/official-map'/('map14-'+name+'-source-manifest.json')
        source_proof=json.loads(source_receipt.read_bytes());dependencies[source_path(source_receipt)]=record(source_receipt)
        if source_proof['manifest']!='871_156598':raise ValueError('Original statue component source version differs')
        expected={r['path']:r for r in source_proof['outputs']}
        paths=[p for key,p in scripts.items() if MODULES[key]==module]
        if module=='EntityLifecycle':paths.append(adapter)
        for path in paths:
            entry=expected[source_path(path)];raw=path.read_bytes()
            if entry.get('hashLineEndings')=='LF':raw=raw.replace(b'\r\n',b'\n')
            if sha(raw)!=entry['sha256']:raise ValueError('Verified original statue application component changed: '+str(path))
    tip_proof=ROOT/'assets/reference/official-map/map14-native-radar-tips-source-manifest.json'
    tip_receipt=json.loads(tip_proof.read_bytes());tip_path=ROOT/tip_receipt['prefab']['path'];dependency(tip_path)
    if sha(tip_path.read_bytes())!=next(r['sha256'] for r in tip_receipt['outputs'] if r['path']==source_path(tip_path)):raise ValueError('Original native cost tip binding changed')
    native_tip_id,=[fid for fid,(kind,b) in documents(tip_path.read_text(encoding='utf-8-sig')).items() if kind=='MonoBehaviour' and b['m_Script']['guid']==guid(ROOT/'unity/Assets/Game/Runtime/Official/EntityRadarCost/MapControllerEntityRadarTipStageCost.cs')]
    inputs=[record(proof_path),record(tip_proof)]
    for cid in range(6570,6574):
        row=exports[str(cid)];p=ROOT/row['path'];raw=p.read_bytes()
        if sha(raw)!=row['sha256']:raise ValueError('Verified original entity export changed: '+str(cid))
        inputs.append(record(p));text=raw.decode('utf-8-sig');docs=documents(text)
        goid,=[fid for fid,(kind,b) in docs.items() if kind=='GameObject'];go=docs[goid][1]
        if len(go['m_Component'])!=18 or str(go['m_Name'])!=str(cid):raise ValueError('Original statue root component count/name differs')
        by_name={metadata[b['m_Script']['guid']].stem:fid for fid,(kind,b) in docs.items() if kind=='MonoBehaviour'}
        if by_name.keys()!=MODULES.keys():raise ValueError('Original statue root component types differ: '+str(by_name.keys()^MODULES.keys()))
        redirects={docs[fid][1]['m_Script']['guid']:guid(scripts[name]) for name,fid in by_name.items()}
        parts=HEADER.split(text);adapter_id=max(docs)+1;changes=[]
        for i in range(1,len(parts),3):
            fid=int(parts[i+1]);kind,b=docs[fid]
            if kind!='MonoBehaviour':continue
            name=metadata[b['m_Script']['guid']].stem
            if name=='MyMapDisplay':
                old='  mMyEntity: {fileID: '+str(by_name['MyEntity'])+'}'
                if old not in parts[i+2]:raise ValueError('Original display owner pointer differs')
                parts[i+2]=parts[i+2].replace(old,'  mMyEntity: {fileID: '+str(adapter_id)+'}')
                changes.append(dict(field='MyMapDisplay.mMyEntity',original=by_name['MyEntity'],native=adapter_id,reason='Consumed ECViewEntityBinding interface adapter over the same original MyEntity.'))
            if name=='MapControllerEntityRadarSupport':
                original=b['mRadarTipPrefab'];source_tip=metadata[original['guid']]
                if source_tip.stem!='StageCostTip' or original['fileID']!=native_tip_id:raise ValueError('Original statue cost radar pointer differs')
                redirects[original['guid']]=guid(tip_path)
            if name=='MyWorldStage':
                types=b['references']['RefIds']
                if len(types)!=1 or types[0]['type']!={'class':'StateGameObjectConfig','ns':None,'asm':'Framework'}:raise ValueError('Original stage switcher managed-reference type differs')
                parts[i+2]=parts[i+2].replace('        ns:\n        asm: Framework','        ns: Zjcs.Official.WorldUI\n        asm: Zjcs.Official.WorldUI')
        text=parts[0]+''.join('--- !u!'+parts[i]+' &'+parts[i+1]+'\n'+parts[i+2] for i in range(1,len(parts),3))
        for match in PTR.finditer(text):
            if match[2] not in redirects:raise ValueError('Bind remaining original statue pointer: '+match[0])
        text=PTR.sub(lambda m:'{fileID: '+m[1]+', guid: '+redirects[m[2]]+', type: '+m[3]+'}',text)
        first_component=go['m_Component'][0]['component']['fileID']
        text=text.replace('  - component: {fileID: '+str(first_component)+'}','  - component: {fileID: '+str(first_component)+'}\n  - component: {fileID: '+str(adapter_id)+'}',1)
        text+='--- !u!114 &'+str(adapter_id)+'\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: '+str(goid)+'}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+guid(adapter)+', type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n  owner: {fileID: '+str(by_name['MyEntity'])+'}\n'
        path=DEST/'Prefabs'/(str(cid)+'.prefab');write(path,text.encode());meta(path,'PrefabImporter');outputs.extend((path,Path(str(path)+'.meta')))
        prefabs.append(dict(classId=cid,rootFileId=goid,original_components=by_name,interface_adapter=adapter_id,explicit_pointer_changes=changes,output=record(path)))
    refs=['Zjcs.Offline.World.NativeMap14StatueEntities','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityClientComponents','Zjcs.Official.EntityViews','Zjcs.Offline.World.NativeMap14EntityLifecycle']
    editor_base=ROOT/'unity/Assets/Game/Editor/Map14/HeroStatues/Zjcs.Editor.Map14.HeroStatues.asmdef'
    editor_refs=json.loads(editor_base.read_bytes())['references']+['Zjcs.Offline.World.NativeMap14EntityRadar'];dependencies[source_path(editor_base)]=record(editor_base)
    for folder,name,references,platforms in ((RUNTIME,'Zjcs.Offline.World.NativeMap14StatueEntities',refs,[]),
        (EDITOR,'Zjcs.Editor.Map14.StatueEntities',refs+['Zjcs.Editor.Map14.HeroStatues','Zjcs.Editor.Map14.RadarNative','Zjcs.Official.MapDisplays','Zjcs.Official.EntityRootVisibility','Zjcs.Official.LegacyWorldStages','Zjcs.Official.EntityRadar','Zjcs.Official.EntityRadarCost','Zjcs.Official.EntityWorldInput','Zjcs.Official.EntityWorldStages','Zjcs.Official.EntityTransformViews','Zjcs.Official.EntityTipDotViews','Zjcs.Official.EntityViewPlayables','Zjcs.Official.EntityStageOperations','Zjcs.Official.World.Map13Entities','Zjcs.Official.World.Map13EntityVisibilityViews','Zjcs.Official.World','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeTerrain','Zjcs.Official.StoryPlayables','Zjcs.Official.StoryPlayableManager','Zjcs.Official.ProfessionUI','Zjcs.Offline.World.NativeMap14EntityAssets','Zjcs.Offline.World.NativeMap14HeroStatues','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14ClientComponents','Zjcs.Offline.World.NativeMap14RadarTips','Unity.RenderPipelines.Universal.Runtime','UniTask'],['Editor'])):
        references=[r for r in references if r!=name]
        if platforms:references=sorted(set(references+editor_refs))
        folder.mkdir(parents=True,exist_ok=True);write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for f in folder.iterdir():
            if f.is_file() and f.suffix!='.meta':runtime_meta(f);outputs.extend((f,Path(str(f)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    catalog=ROOT/'unity/Assets/Game/Resources/Map14/Native/statue-entity-catalog.asset'
    body='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+guid(RUNTIME/'NativeMap14StatueEntityCatalog.cs')+', type: 3}\n  m_Name: Map14 original statue entities\n  m_EditorClassIdentifier:\n  Manifest: 871_156598\n  Entries:\n'
    for r in prefabs:body+='  - ClassId: '+str(r['classId'])+'\n    Prefab: {fileID: '+str(r['rootFileId'])+', guid: '+guid(ROOT/r['output']['path'])+', type: 3}\n'
    write(catalog,body.encode());meta(catalog,'NativeFormatImporter');outputs.extend((catalog,Path(str(catalog)+'.meta')))
    for p in (DEST,DEST/'Prefabs'):meta(p,folder=True);outputs.append(Path(str(p)+'.meta'))
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,prefabs=prefabs,inputs=inputs,dependencies=list(dependencies.values()),tools=[record(Path(__file__))],outputs=[record(p) for p in sorted(set(outputs))],
        operation='Reuse verified original native prefab export, preserving component order, complete serialized fields and scalar YAML spellings; bind migrated original scripts, managed StateGameObjectConfig and original native StageCostTip. Add only the consumed ECViewEntityBinding interface adapter over the same original MyEntity; redirect MyMapDisplay owner to it explicitly.',
        validation_entry='Zjcs.Editor.Map14.OfficialMap14StatueEntityAcceptance.Run',
        validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile and Play Mode passed: all original root scripts, ordered component Bind/Start/End/Remove, native component projection completeness and original owner identities, original display selection and three stage/window requests, visibility requests/handlers, grid events, FIFO reuse, low-memory loan retention/release/reload, radar removal-service teardown. Native cost/radar/input/container/loading regressions passed. Actual assembled root loop/disappear/hidden frames and regression frames viewed; root static metadata check passed.',
        limitations=['Map host, visibility flags/handlers, tip graph and window services remain explicit fixtures. Original native StageCostTip reference is bound; its own nonempty text/icon/pool validation runs as a regression. Formal native entity presentation state, map/player/UI/simulated pointer/save, complete ProjectTools.Validate and Windows Player remain required.'])))
    print('Original statue entity roots bound:',len(prefabs),'prefabs;',len(outputs),'outputs')

if __name__=='__main__':build()
