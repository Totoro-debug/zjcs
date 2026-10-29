"""Bind exact original map display tables and shared identity/tag queries."""
import json
from pathlib import Path
from official_apk import ROOT,Apk
from prepare_unity_map14_sources import encode,sha,source_path,write,meta
from prepare_unity_story_sound_runtime import CODE,runtime_meta
from prepare_unity_story_attachment_runtime import method

def record(p):
    raw=p.read_bytes();e=dict(path=source_path(p))
    if p.is_relative_to(ROOT/'unity') and p.suffix in ('.cs','.asmdef'):
        raw=raw.replace(b'\r\n',b'\n');e['hashLineEndings']='LF'
    return dict(e,sha256=sha(raw))

def build():
    proof_path=ROOT/'assets/reference/official-map/map14-source-manifest.json'
    proof=json.loads(proof_path.read_bytes())
    apk=Apk.current()
    if apk.version!='871_156598' or proof['manifest']!=apk.version:raise ValueError('Original display table version differs')
    folder=ROOT/'unity/Assets/Official/World/Map/14/NativeDisplayData/Resources/Map14/DisplayData'
    outputs=[];inputs=[];entries=[]
    for table,parser,config in [('map','MapInfoParser','MapInfo'),('map_area','MapAreaInfoParser','MapAreaInfo'),('map_logic_entity_tag','MapLogicEntityTagInfoParser','MapLogicEntityTagInfo')]:
        suffix='/Map/'+table+'.g.bytes';expected=proof['inputs'][suffix]
        obj=apk.asset('config_luban_map',suffix);raw=obj.read().m_Script.encode('utf8','surrogateescape')
        if sha(raw)!=expected['payload_sha256'] or sha(obj.get_raw_data())!=expected['object_sha256']:raise ValueError('Original display table source changed: '+table)
        current=apk.provenance(obj)
        for key in ('bundle_hash','bundle_sha256','manifest'):
            if current[key]!=expected[key]:raise ValueError('Original table bundle differs: '+key)
        p=folder/(table+'.g.bytes');write(p,raw);meta(p);outputs.extend((p,Path(str(p)+'.meta')))
        entries.append(dict(OriginalPath='Config/Luban/Map/'+table+'.g.bytes',Resource='Map14/DisplayData/'+table+'.g',Sha256=sha(raw),Parser=parser,Config=config))
        inputs.append(dict(expected,asset=suffix))
    catalog=folder/'catalog.json';write(catalog,encode(dict(Manifest=apk.version,Entries=entries)));meta(catalog);outputs.extend((catalog,Path(str(catalog)+'.meta')))
    root=ROOT/'unity/Assets/Official/World/Map/14/NativeDisplayData'
    for f in [root,*root.rglob('*')]:
        if f.is_dir():runtime_meta(f);outputs.append(Path(str(f)+'.meta'))
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14DisplayData'
    official=ROOT/'unity/Assets/Game/Runtime/Official/World/Map14DisplayTags'
    body=method((CODE/'common-source/Common/MapEntityExtra.cs').read_text(encoding='utf-8-sig'),'public static string GetEntityTag(')
    replacements={
        'this EcEntity ecEntity':'object ecEntity, IMapEntityTagInputs inputs',
        'MeManager.Instance.MapData.GetLoEntIdByEntId(ecEntity.Id)':'inputs.GetLogicalEntityId(ecEntity)',
        'Parsers.Instance.MapLogicEntityTagInfoParser.GetEntityTag(ecEntity.Container.ClassId, loEntIdByEntId)':'inputs.GetEntityTag(ecEntity, loEntIdByEntId)'}
    for old,new in replacements.items():
        if body.count(old)!=1:raise ValueError('Original entity-tag source boundary changed: '+old)
        body=body.replace(old,new)
    text='// Migrated original MapEntityExtra.GetEntityTag; see Map14 display-data source receipt.\nusing System;\nnamespace Zjcs.Official.World.Map14DisplayTags\n{\npublic interface IMapEntityTagInputs\n{ int GetLogicalEntityId(object entity); string GetEntityTag(object entity,int logicalId); }\npublic static class OfficialMapEntityTagQuery\n{\n'+body+'\n}\n}\n'
    write(official/'OfficialMapEntityTagQuery.cs',text.encode())
    write(official/'Zjcs.Official.World.Map14DisplayTags.asmdef',encode(dict(name='Zjcs.Official.World.Map14DisplayTags',autoReferenced=False,references=[])))
    runtime_meta(official);outputs.append(Path(str(official)+'.meta'))
    for p in official.iterdir():
        if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    refs=['Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Official.EntityViews','Zjcs.Official.MapDisplays','Zjcs.Official.World.NativeTerrain','Zjcs.Official.StoryPlayables','Zjcs.Official.World.Map13Entities','Zjcs.Official.World.Map14DisplayTags','Config']
    write(runtime/'Zjcs.Offline.World.NativeMap14DisplayData.asmdef',encode(dict(name='Zjcs.Offline.World.NativeMap14DisplayData',autoReferenced=False,references=refs)))
    runtime_meta(runtime);outputs.append(Path(str(runtime)+'.meta'))
    for p in runtime.iterdir():
        if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    editor=ROOT/'unity/Assets/Game/Editor/Map14/DisplayData'
    refs=json.loads((ROOT/'unity/Assets/Game/Editor/Map14/NativeVisibility/Zjcs.Editor.Map14.NativeVisibility.asmdef').read_bytes())['references']
    refs=sorted(set(refs+['Zjcs.Offline.World.NativeMap14DisplayData','Zjcs.Editor.Map14.NativeVisibility','Zjcs.Official.World.Map14DisplayTags','Config']))
    write(editor/'Zjcs.Editor.Map14.DisplayData.asmdef',encode(dict(name='Zjcs.Editor.Map14.DisplayData',autoReferenced=False,references=refs,includePlatforms=['Editor'])))
    runtime_meta(editor);outputs.append(Path(str(editor)+'.meta'))
    for p in editor.iterdir():
        if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    sources=[CODE/'common-source/Common'/(name+'.cs') for name in ('MapInfo','MapInfoParser','MapAreaInfo','MapAreaInfoParser','MapLogicEntityTagInfo','MapLogicEntityTagInfoParser','MapEntityExtra','MapLoEntityIdManager','PlayerMap','LubanKeyCsvParser','LubanDoubleKeyCsvParser','LubanParserHelper','GridGroundComponentInfo','AreaAgentComponent','MapInfoManager','MapEntityFunctionDisableInfoParser','LoGridTransformComponent','LoGridTransformComponentInfo')]
    sources.append(CODE/'client-source/MyMapDisplay.cs')
    code_proof=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Console.dll','Assembly-CSharp.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=code_proof['assemblies'][name]['decoded_sha256']:raise ValueError('Original display assembly changed: '+name)
        assemblies.append(record(p))
    write(ROOT/'assets/reference/official-map/map14-native-display-data-source-manifest.json',encode(dict(manifest=apk.version,mapClassId=14,assemblies=assemblies,inputs=[record(proof_path)]+[record(p) for p in sources],payloads=inputs,outputs=[record(p) for p in sorted(set(outputs))],tools=[record(Path(__file__))],operation='Load unchanged original map/group, area and logical-entity-tag tables with original GConfig parsers. Resolve explicit original logical identity mapping with unchanged MapLoEntityIdManager; extract the original GetEntityTag method body with two explicit local service substitutions, preserving its lookup order and empty-tag branch. Forward original map-group/area/ground queries and live entity state; require explicit destruction and function-disable host inputs.',validation_entry='Zjcs.Editor.Map14.OfficialMap14DisplayDataAcceptance.Run',validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile/Play Mode passed: exact original map/area/tag tables and original parser postprocessing; independent parser scope/global factory restoration; original logical mapping and empty/nonempty/container isolation branches; original removing/position reads; original map group/area/ground queries and explicit host-state forwarding. Query fixture restores original placement 201 LoGridTransform position/direction/flip into the original 6106 template before original registration, Awake and container Start. Native visibility/four-statue/cost/radar/input/container/loading regressions passed and current rendered frames were viewed. Full ProjectTools.Validate and Windows Player remain pending.',limitations=['Local mapping registration is a required host input; this binding does not infer placement-to-runtime identities or server state. The 6106 query fixture does not validate dungeon gameplay. Formal four-statue host wiring, player/map/UI/simulated input/save, full ProjectTools.Validate and Windows Player remain pending.'])))
    apk.archive.close();print('Native display tables prepared:',len(outputs),'outputs')
if __name__=='__main__':build()
