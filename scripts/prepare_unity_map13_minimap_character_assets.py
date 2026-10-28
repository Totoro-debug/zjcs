"""Restore the complete original Map13 minimap player marker subtree."""
import argparse,copy,hashlib,json,re
from pathlib import Path
import yaml
from official_apk import Apk,ROOT
from prepare_unity_bundle_inputs import collect,serialized_files
from prepare_unity_map13_minimap_inputs import ROOT_BUNDLE
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_sprite_yaml import Dumper,fields,source_header
from prepare_unity_map13_sources import encode,meta,sha,write
from prepare_unity_map13_water_entity_export import native_io_path
from UnityPy.helpers.MeshHelper import MeshHandler

DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapCharacter'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')


def identity(relative):return hashlib.md5(('zjcs-map13-minimap-character:'+str(relative)).encode()).hexdigest()
def native_meta(guid,fileid):return 'fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(fileid)+'\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'


def build(cache):
    cache=cache.resolve()
    if not cache.is_relative_to(ROOT/'output/validation'):raise ValueError('Owned validation cache required')
    inputs=json.loads((cache/'source-manifest.json').read_bytes());proof=json.loads((cache/'export-proof.json').read_bytes())
    if inputs['manifest']!='871_156598' or inputs['roots']!=[ROOT_BUNDLE] or proof['counts']['registries']!=12:raise ValueError('Verified original minimap export required')
    for entry in inputs['inputs']:
        if sha(native_io_path(cache/'inputs'/entry['output']).read_bytes())!=entry['converter_input_sha256']:raise ValueError('Converter input changed')
    base=cache/'export/ExportedProject/Assets';root_relative='Prefabs/Map/Map_13/MiniMap/MiniMap_13.prefab'
    restored=next(v for v in proof['outputs'] if v['input'].endswith('/'+root_relative))
    if sha(native_io_path(ROOT/restored['input']).read_bytes())!=restored['input_sha256'] or sha(native_io_path(ROOT/restored['path']).read_bytes())!=restored['sha256']:raise ValueError('Original root restoration changed')
    root=native_io_path(ROOT/restored['path']).read_text(encoding='utf-8-sig').replace('\r\n','\n')
    docs={re.match(r'--- !u!\d+ &(-?\d+)',v)[1]:v for v in re.split(r'(?=^--- !u!)',root,flags=re.M) if v.startswith('--- !u!')}
    index={}
    for p in base.rglob('*.meta'):
        m=GUID.search(native_io_path(p).read_text(encoding='utf-8-sig'))
        if m:
            if m[1] in index:raise ValueError('Duplicate export GUID')
            index[m[1]]=Path(str(p)[:-5])
    old_guid=lambda p:GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]
    original_script=next(p for p in index.values() if p.name=='MiniMapCharacterManager.cs')
    original_manager=next(v for v in docs.values() if 'guid: '+old_guid(original_script) in v)
    root_id=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',original_manager)[1]
    included=set();pending=[root_id]
    while pending:
        key=pending.pop()
        if key in included:continue
        included.add(key);text=docs[key];pending+=re.findall(r'component: \{fileID: (-?\d+)\}',text)
        if text.startswith('--- !u!4 '):
            pending+=re.findall(r'^  - \{fileID: (-?\d+)\}',text,re.M);pending+=re.findall(r'm_GameObject: \{fileID: (-?\d+)\}',text)
    if len(included)!=19:raise ValueError('All nineteen original marker subtree objects required')
    text=''.join(v for k,v in docs.items() if k in included)
    # Only external references are detached; all nineteen original objects and
    # their internal IDs, render fields, animation and transforms are retained.
    text=text.replace('  m_Father: {fileID: 4401429660297866}','  m_Father: {fileID: 0}').replace('  MiniMap: {fileID: 114000342987746139}','  MiniMap: {fileID: 0}')
    dependencies=set();pending=[index[g] for g in GUID.findall(text) if g in index]
    while pending:
        p=pending.pop()
        if p in dependencies:continue
        dependencies.add(p)
        if p.suffix in ['.cs','.png']:continue
        for g in GUID.findall(native_io_path(p).read_text(encoding='utf-8-sig')):
            if g in index:pending.append(index[g])
            elif not g.startswith('0000000000000000'):raise ValueError('Unresolved original reference '+g)
    counts={ext:sum(p.suffix==ext for p in dependencies) for ext in {p.suffix for p in dependencies}}
    if counts!={'.asset':2,'.anim':1,'.png':1,'.cs':2}:raise ValueError(counts)
    # The same verified native atlas is already shipped by the fog module.
    mask_receipt=ROOT/'assets/reference/official-map/map13-native-minimap-masks-source-manifest.json';mask=json.loads(mask_receipt.read_bytes())
    atlas=next(v for v in mask['outputs'] if v['path'].endswith('sactx-0-2048x1024-ASTC 6x6-MiniMap_Atlas-204f6491.texture.asset'))
    atlas_path=ROOT/atlas['path']
    if sha(atlas_path.read_bytes())!=atlas['sha256'] or sha(Path(str(atlas_path)+'.meta').read_bytes())!=atlas['meta_sha256']:raise ValueError('Verified original native atlas changed')
    atlas_guid=GUID.search(Path(str(atlas_path)+'.meta').read_text())[1]
    mapping={};bindings=[]
    for p in dependencies:
        if p.suffix=='.cs':
            if p.stem=='MiniMapCharacterManager':target=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapCharacter/MiniMapCharacterManager.cs'
            elif p.stem=='UniversalAdditionalCameraData':target=ROOT/'unity/Packages/com.unity.render-pipelines.universal/Runtime/UniversalAdditionalCameraData.cs'
            else:raise ValueError('Unported script '+p.stem)
            mapping[old_guid(p)]=GUID.search(Path(str(target)+'.meta').read_text())[1];bindings.append(dict(original=p.relative_to(base).as_posix(),**record(target),meta_sha256=sha(Path(str(target)+'.meta').read_bytes())))
        else:mapping[old_guid(p)]=atlas_guid if p.suffix=='.png' else identity(p.relative_to(base).as_posix())
    def remap(s):return GUID.sub(lambda m:'guid: '+mapping.get(m[1],m[1]),s)
    outputs=[];exported=[];sprites=[]
    def emit(p,raw,metadata):
        write(p,raw);write(Path(str(p)+'.meta'),metadata.encode());outputs.append(dict(**record(p),meta_sha256=sha(Path(str(p)+'.meta').read_bytes())))
    apk=Apk.current();_,missing=collect(apk,[ROOT_BUNDLE])
    if missing:raise ValueError(missing)
    original_sprites={}
    for bundle in apk.loaded.values():
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name=='Sprite':original_sprites.setdefault(obj.read().m_Name,[]).append(obj)
    for p in sorted(dependencies):
        if p.suffix=='.cs':continue
        raw=native_io_path(p).read_bytes();metadata=native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig').replace('\r\n','\n');relative=p.relative_to(base).as_posix();exported.append(dict(relative=relative,sha256=sha(raw),meta_sha256=sha(native_io_path(Path(str(p)+'.meta')).read_bytes())))
        if p.suffix=='.png':continue
        if p.suffix=='.asset':
            candidates=original_sprites.get(p.stem,[])
            if len(candidates)!=1:raise ValueError('Unique original player marker Sprite required')
            obj=candidates[0];sprite=obj.read();original=copy.deepcopy(obj.read_typetree(check_read=True));rd=original['m_RD'];aobj=sprite.m_SpriteAtlas.deref();af=aobj.read_typetree(check_read=True);entry=next(v for k,v in af['m_RenderDataMap'] if k==original['m_RenderDataKey']);rd.update(copy.deepcopy(entry));original['m_SpriteAtlas']={'fileID':0}
            effective=next(v for k,v in aobj.read().m_RenderDataMap if k==sprite.m_RenderDataKey);tobj=effective.texture.deref();texture=tobj.read();texture_source=next(v for v in mask['textures'] if v['source']['path_id']==str(tobj.path_id))
            if sha(tobj.get_raw_data())!=texture_source['object_sha256']:raise ValueError('Player marker atlas differs from original native fog atlas')
            uv=copy.deepcopy(rd['uvTransform']);rd['texture']={'fileID':0};key,local_id=original['m_RenderDataKey'];original['m_RenderDataKey']={''.join(f"{key['data['+str(i)+']']:08x}"[::-1] for i in range(4)):local_id};original['m_Rect']['serializedVersion']=2;rd['serializedVersion']=3;rd['textureRect']['serializedVersion']=2
            for submesh in rd['m_SubMeshes']:submesh['serializedVersion']=2
            rd['m_IndexBuffer']=bytes(rd['m_IndexBuffer']).hex();vd=rd['m_VertexData'];vraw=bytes(vd['m_DataSize']);vd.update(serializedVersion=3,m_DataSize=len(vraw),_typelessdata=vraw.hex());native=source_header();native.update(fields(original));native['m_RD']['texture']=dict(fileID=2800000,guid=atlas_guid,type=2)
            raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n'+yaml.dump(dict(Sprite=native),Dumper=Dumper,sort_keys=False,allow_unicode=True)).encode();metadata=native_meta(mapping[old_guid(p)],21300000);mesh=MeshHandler(sprite.m_RD,obj.version);mesh.process();sprites.append(dict(path=(DEST/relative).relative_to(ROOT).as_posix(),source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),atlas_source=apk.provenance(aobj),atlas_object_sha256=sha(aobj.get_raw_data()),vertices=[dict(x=v[0],y=v[1]) for v in mesh.m_Vertices],triangles=list(mesh.m_IndexBuffer),uv=[dict(x=(v[0]*uv['x']+uv['y'])/texture.m_Width,y=(v[1]*uv['z']+uv['w'])/texture.m_Height) for v in mesh.m_Vertices],ppu=sprite.m_PixelsToUnits,pivot=dict(x=sprite.m_Pivot.x*sprite.m_Rect.width,y=sprite.m_Pivot.y*sprite.m_Rect.height)))
        else:raw=remap(raw.decode('utf-8-sig').replace('\r\n','\n')).encode();metadata=remap(metadata)
        emit(DEST/relative,raw,metadata)
    relative='Prefabs/Map/Map_13/MiniMap/MinimapCharacter.prefab';emit(DEST/relative,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+remap(text)).encode(),'fileFormatVersion: 2\nguid: '+identity(relative)+'\nPrefabImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    for folder in [DEST,*sorted(p for p in DEST.rglob('*') if p.is_dir())]:meta(folder,folder=True)
    write(ROOT/'assets/reference/official-map/map13-native-minimap-character-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest=apk.version,bundle_inputs=inputs['inputs'],export_proof_sha256=sha((cache/'export-proof.json').read_bytes()),exported_inputs=exported,root=dict(source_path_id='3244588087784958650',fileid=int(root_id),objects=19,operation='Copy all original marker descendants and components; detach parent and bind external MiniMap reference before activation. Preserve original camera, URP data, 256x256 target size, sprite renderers, hierarchy and legacy animation.'),sprites=sprites,reused_atlas=atlas,script_bindings=bindings,outputs=outputs,generator=record(Path(__file__).resolve()),tools=[record(ROOT/'scripts/prepare_unity_map13_native_sprite_yaml.py')],limitations=['Formal map/controller binding, Play Mode render and lifecycle checks, full minimap mouse/save and Windows Player remain required.'])))
    print('Original minimap player marker:',len(included),'objects;',len(outputs),'outputs;',len(sprites),'Sprites');apk.archive.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path,required=True);build(parser.parse_args().cache)
