"""Recover the original WorldCamera VolumeProfile and bind every Bloom field natively."""
import argparse
import copy
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_native_pipeline import Dumper, Pointer, native_meta
from prepare_unity_map13_water_components import import_meta
from UnityPy.classes.PPtr import PPtr
import yaml

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source/native-camera-volume.json'
ASSET = ROOT / 'unity/Assets/Official/World/Map/13/NativeCamera/GlobalVolumeProfile.asset'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Map13/Native/native-camera-catalog.asset'
RECEIPT = ROOT / 'assets/reference/official-map/map13-camera-volume-source-manifest.json'


def record(path):
    return dict(path=source_path(path) if path.is_relative_to(ROOT) else path.as_posix(), sha256=sha(path.read_bytes()))


def build(unity_project=None):
    project = Path(unity_project).resolve() if unity_project else ROOT / 'unity'
    camera_path = ROOT / 'unity/Assets/Official/World/Map/13/Source/world-camera.json'
    camera = json.loads(camera_path.read_bytes())
    if camera['manifest'] != '871_156598': raise ValueError('Wrong original camera version')
    apk = Apk.current()
    if apk.version != camera['manifest']: raise ValueError('Wrong selected camera version')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    tools = [Path(__file__).resolve()] + [ROOT/'scripts'/n for n in ('map13_native_schemas.py','map13_entity_native_schemas.py','prepare_unity_map13_native_pipeline.py','prepare_unity_map13_water_components.py','official_unity_schema.py','official_apk.py','official_metadata.py','read_official_native.py','prepare_unity_map13_sources.py')]
    tool_records = [record(p) for p in tools]
    schemas = NativeSchemas(code, unity_project=project); schema_proof = install(schemas, code)
    prefab = apk.asset('prefabs_map_common_entityui', '/WorldCamera.prefab')
    def resolve(owner, pointer):
        return PPtr(m_FileID=pointer['m_FileID'],m_PathID=pointer['m_PathID'],assetsfile=owner.assets_file).deref()
    volumes=[]
    def walk(go):
        for component in go.read().m_Component:
            obj=component.component.deref()
            if obj.type.name == 'MonoBehaviour':
                script=obj.read(check_read=False).m_Script.read()
                if script.m_ClassName=='Volume': volumes.append(obj)
            elif obj.type.name=='Transform':
                for child in obj.read().m_Children:walk(child.read().m_GameObject.deref())
    walk(prefab)
    if len(volumes)!=1:raise ValueError('Original WorldCamera volume must be unique')
    volume=volumes[0];fields,_=schemas.read(volume)
    original=next(o for o in camera['objects'] if o['type']=='UnityEngine.Rendering.Volume')
    if sha(volume.get_raw_data()) != original['object_sha256'] or fields != original['fields']:
        raise ValueError('Original camera volume changed from the verified source')
    profile=resolve(volume,fields['sharedProfile']);profile_fields,_=schemas.read(profile)
    if len(profile_fields['components'])!=1:raise ValueError('Original WorldCamera post-processing closure changed')
    bloom=resolve(profile,profile_fields['components'][0]);bloom_fields,_=schemas.read(bloom)
    script=bloom.read(check_read=False).m_Script.read()
    if script.m_Namespace!='UnityEngine.Rendering.Universal' or script.m_ClassName!='Bloom':raise ValueError('Unexpected original post-processing component')
    caches=sorted((project/'Library/PackageCache').glob('com.unity.render-pipelines.core@14.0.11'))
    if len(caches)!=1:raise FileNotFoundError('Pinned Core 14.0.11 source is unavailable')
    core=caches[0]
    scripts={'VolumeProfile':core/'Runtime/Volume/VolumeProfile.cs','Bloom':ROOT/'unity/Packages/com.unity.render-pipelines.universal/Runtime/Overrides/Bloom.cs'}
    engine_sources=[record(p) for p in (scripts['VolumeProfile'],Path(str(scripts['VolumeProfile'])+'.meta'),core/'Runtime/Volume/VolumeParameter.cs',scripts['Bloom'],Path(str(scripts['Bloom'])+'.meta'),ROOT/'unity/Packages/com.unity.render-pipelines.universal/Runtime/Deprecated.cs')]
    guids={kind:re.search(r'(?m)^guid: (\w+)$',Path(str(p)+'.meta').read_text(encoding='utf8'))[1] for kind,p in scripts.items()}
    records=[];documents=[]
    for obj,kind,source_fields,file_id in ((profile,'VolumeProfile',profile_fields,11400000),(bloom,'Bloom',bloom_fields,11400001)):
        values=copy.deepcopy(source_fields)
        values['m_Script']=Pointer(fileID=11500000,guid=guids[kind],type=3)
        values['m_GameObject']=Pointer(fileID=0)
        if kind=='VolumeProfile':values['components']=[Pointer(fileID=11400001)]
        def remap(value):
            if isinstance(value,Pointer):return value
            if isinstance(value,dict):
                if set(value)=={'m_FileID','m_PathID'}:
                    if value['m_FileID'] or value['m_PathID']:raise ValueError('Unexpected non-null camera Volume asset dependency')
                    return Pointer(fileID=0)
                return {k:remap(v) for k,v in value.items()}
            if isinstance(value,list):return [remap(v) for v in value]
            return value
        values=remap(values)
        payload=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=Pointer(fileID=0),m_PrefabInstance=Pointer(fileID=0),m_PrefabAsset=Pointer(fileID=0),m_GameObject=Pointer(fileID=0),m_Enabled=1,m_EditorHideFlags=0)
        payload.update(values);payload['m_EditorClassIdentifier']=''
        documents.append('--- !u!114 &'+str(file_id)+'\n'+yaml.dump({'MonoBehaviour':payload},Dumper=Dumper,allow_unicode=True,sort_keys=False,width=1000))
        checks=[]
        def properties(value,path):
            if isinstance(value,Pointer):checks.append(dict(path=path,kind='ObjectReference',fileId=value['fileID']))
            elif isinstance(value,dict):
                for key,child in value.items():properties(child,path+'.'+key)
            elif isinstance(value,list):
                checks.append(dict(path=path,kind='Array',value=str(len(value))))
                for index,child in enumerate(value):properties(child,path+'.Array.data['+str(index)+']')
            else:checks.append(dict(path=path,kind='Scalar',value=str(value)))
        for key,value in values.items():
            if key not in {'m_GameObject','m_Script','m_Name','m_Enabled'}:properties(value,key)
        records.append(dict(identity=obj.assets_file.name.lower()+':'+str(obj.path_id),type=kind,name=source_fields['m_Name'],source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),fields=source_fields,fileId=file_id,scriptGuid=guids[kind],checks=checks))
    write(ASSET,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(documents)).encode('utf8'));native_meta(ASSET,11400000)
    import_meta(ASSET.parent,'DefaultImporter',folder=True)
    binding_script=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13AssetCatalog.cs'
    binding_guid=re.search(r'(?m)^guid: (\w+)$',Path(str(binding_script)+'.meta').read_text(encoding='utf8'))[1]
    asset_guid=re.search(r'(?m)^guid: (\w+)$',Path(str(ASSET)+'.meta').read_text(encoding='utf8'))[1]
    bindings=[dict(OfficialPath=r['identity'],Asset=Pointer(fileID=r['fileId'],guid=asset_guid,type=2)) for r in records]
    payload=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=Pointer(fileID=0),m_PrefabInstance=Pointer(fileID=0),m_PrefabAsset=Pointer(fileID=0),m_GameObject=Pointer(fileID=0),m_Enabled=1,m_EditorHideFlags=0,m_Script=Pointer(fileID=11500000,guid=binding_guid,type=3),m_Name='native-camera-catalog',m_EditorClassIdentifier='',Bindings=bindings)
    write(CATALOG,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump({'MonoBehaviour':payload},Dumper=Dumper,allow_unicode=True,sort_keys=False,width=1000)).encode());native_meta(CATALOG,11400000)
    data=dict(manifest=apk.version,mapClassId=13,catalogResource='Map13/Native/native-camera-catalog',assetPath=source_path(ASSET).removeprefix('unity/'),sourceCameraVolume=original['identity'],cameraVolumeFields=fields,objects=records,schemaProof=schema_proof)
    write(SOURCE,encode(data));meta(SOURCE)
    for path,pinned in zip(tools,tool_records):
        if sha(path.read_bytes())!=pinned['sha256']:raise ValueError('Camera Volume conversion source changed; rerun')
    proof=dict(manifest=apk.version,mapClassId=13,inputs=[record(camera_path),record(binding_script),record(Path(str(binding_script)+'.meta'))],tools=tool_records,engineSources=engine_sources,referenceAssemblies=[record(p) for p in schemas.reference_inputs],sources=[dict(source=r['source'],object_sha256=r['object_sha256']) for r in records],outputs=[record(p) for p in (SOURCE,Path(str(SOURCE)+'.meta'),ASSET,Path(str(ASSET)+'.meta'),Path(str(ASSET.parent)+'.meta'),CATALOG,Path(str(CATALOG)+'.meta'))],counts=dict(objects=2,bloomParameters=len([k for k in bloom_fields if k not in {'m_GameObject','m_Enabled','m_Script','m_Name','active'}]),checkedFields=sum(len(r['checks']) for r in records)),operation='Resolve the actual original WorldCamera Volume profile from the APK and strictly decode every byte of its original Bloom dependency. Preserve all parameter values, override states, obsolete skipIterations and current maxIterations, texture dimension and the original null dirt texture. Replace only MonoScript and object pointers with explicit pinned Unity native bindings. Runtime camera prefab/service binding and full Map13 gameplay remain pending.')
    write(RECEIPT,encode(proof));apk.archive.close();print('Bound original camera Volume assets:',proof['counts'],flush=True)
    return data

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--unity-project',type=Path);args=parser.parse_args();build(args.unity_project)
