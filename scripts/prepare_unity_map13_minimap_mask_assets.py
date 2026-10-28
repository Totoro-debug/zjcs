"""Restore exact original minimap fog Tiles, Sprite geometry and native Tilemap."""
import argparse,copy,hashlib,io,json,re
from pathlib import Path
import yaml
from PIL import Image
from official_apk import Apk,ROOT
from prepare_unity_bundle_inputs import collect,serialized_files
from prepare_unity_map13_minimap_inputs import ROOT_BUNDLE
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_scene import decode_mips,native_texture
from prepare_unity_map13_native_sprite_yaml import Dumper,fields,source_header
from prepare_unity_map13_sources import encode,meta,sha,write
from prepare_unity_map13_water_entity_export import native_io_path
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate
from UnityPy.helpers.MeshHelper import MeshHandler

DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapMasks'
SHADER=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMasks/MiniMapFog.shader'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')
def identity(relative):return hashlib.md5(('zjcs-map13-minimap-masks:'+str(relative)).encode()).hexdigest()
def native_meta(guid,fileid):return 'fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(fileid)+'\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'


def build(cache):
    cache=cache.resolve()
    if not cache.is_relative_to(ROOT/'output/validation'):raise ValueError('Owned validation cache required')
    inputs=json.loads((cache/'source-manifest.json').read_bytes());proof=json.loads((cache/'export-proof.json').read_bytes())
    if inputs['manifest']!='871_156598' or inputs['roots']!=[ROOT_BUNDLE] or proof['counts']['registries']!=12:raise ValueError('Verified original minimap export required')
    for entry in inputs['inputs']:
        if sha(native_io_path(cache/'inputs'/entry['output']).read_bytes())!=entry['converter_input_sha256']:raise ValueError('Converter input changed')
    base=cache/'export/ExportedProject/Assets';index={}
    for p in base.rglob('*.meta'):
        match=GUID.search(native_io_path(p).read_text(encoding='utf-8-sig'))
        if match:
            if match[1] in index:raise ValueError('Duplicate export GUID')
            index[match[1]]=Path(str(p)[:-5])
    root=base/'Prefabs/Map/Map_13/MiniMap/MiniMap_13.prefab';raw=native_io_path(root).read_text(encoding='utf-8-sig').replace('\r\n','\n');docs=re.split(r'(?=^--- !u!)',raw,flags=re.M)
    source_parts=[d for d in docs if 'mCornerPatterns:' in d or 'MaskTile:' in d];tile_guids=set(re.findall(r'(?:MyTile|MaskTile): \{fileID: 11400000, guid: (\w+)', '\n'.join(source_parts)))
    if len(tile_guids)!=11:raise ValueError('Original eleven mask Tiles required')
    tilemap_parts=[d for d in docs if '  m_GameObject: {fileID: 1194081240383615}' in d or d.startswith('--- !u!1 &1194081240383615\n')]
    if len(tilemap_parts)!=4:raise ValueError('Complete original native Tilemap GameObject required')
    tilemap_raw=''.join(tilemap_parts);pending=[index[g] for g in tile_guids|set(GUID.findall(tilemap_raw)) if g in index];found=set()
    while pending:
        p=pending.pop()
        if p in found:continue
        found.add(p)
        if p.suffix in ['.png','.cs']:continue
        for q in [p,Path(str(p)+'.meta')]:
            for guid in GUID.findall(native_io_path(q).read_text(encoding='utf-8-sig')):
                if guid in index:pending.append(index[guid])
                elif not guid.startswith('0000000000000000'):raise ValueError('Unresolved native mask reference '+guid)
    counts={ext:sum(p.suffix==ext for p in found) for ext in {p.suffix for p in found}}
    if counts!={'.asset':20,'.png':2,'.mat':1,'.shader':1}:raise ValueError(counts)
    apk=Apk.current();_,missing=collect(apk,[ROOT_BUNDLE])
    if missing:raise ValueError(missing)
    textures={};sprites={};tiles={};shaders=[]
    for bundle in apk.loaded.values():
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name=='Texture2D':
                    t=obj.read();textures.setdefault((t.m_Name,t.m_Width,t.m_Height),[]).append((obj,t))
                elif obj.type.name=='Sprite':sprites.setdefault(obj.read().m_Name,[]).append(obj)
                elif obj.type.name=='Shader' and obj.read().m_ParsedForm.m_Name=='Sprites/MinimapMask':shaders.append(obj)
                elif obj.type.name=='MonoBehaviour':tiles.setdefault(obj.read(check_read=False).m_Name,[]).append(obj)
    if len(shaders)!=1:raise ValueError('Unique original minimap fog shader required')
    sobj=shaders[0];shader=sobj.read();stencil=shader.m_ParsedForm.m_SubShaders[0].m_Passes[0].m_State.stencilOp
    if [getattr(stencil,k).val for k in ['comp','pass_','fail','zFail']]!=[8,0,0,0]:raise ValueError('Restore original mask stencil operation')
    text,programs=translate(shader,'MiniMapFog');write(SHADER,text.encode());import_meta(SHADER,'ShaderImporter');shader_guid=GUID.search(Path(str(SHADER)+'.meta').read_text())[1]
    outputs=[record(SHADER)];shader_programs=[]
    for program in programs:
        p=ROOT/'unity/Assets/Official/World/Map/13/Source/MiniMapShaders'/('MinimapMask-'+str(program['index'])+'.glsl.txt');write(p,program['source'].encode());meta(p);outputs.append(record(p));shader_programs.append(dict(index=program['index'],keywords=program['keywords'],**record(p)))
    old_guid=lambda p:GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]
    mapping={old_guid(p):shader_guid if p.suffix=='.shader' else identity(p.relative_to(base).as_posix()) for p in found}
    def remap(text):return GUID.sub(lambda m:'guid: '+mapping.get(m[1],m[1]),text)
    exported=[];texture_proofs=[];sprite_proofs=[];tile_proofs=[];texture_bindings={}
    template=(ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset').read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
    def emit(p,raw,metadata):write(p,raw);write(Path(str(p)+'.meta'),metadata.encode());outputs.append(dict(**record(p),meta_sha256=sha(Path(str(p)+'.meta').read_bytes())))
    for p in sorted(found):
        relative=p.relative_to(base).as_posix();raw=native_io_path(p).read_bytes();metadata=native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig').replace('\r\n','\n');exported.append(dict(relative=relative,sha256=sha(raw),meta_sha256=sha(native_io_path(Path(str(p)+'.meta')).read_bytes())));target=DEST/relative
        if p.suffix=='.shader':continue
        if p.suffix=='.png':
            size=Image.open(io.BytesIO(raw)).size;candidates=textures.get((p.stem,*size),[])
            if len(candidates)!=1:raise ValueError('Unique original mask texture required '+relative)
            obj,t=candidates[0];rgba,mips,profile=decode_mips(t);target=target.with_name(target.stem+'.texture.asset');raw=native_texture(template,t,rgba);metadata=native_meta(mapping[old_guid(p)],2800000);texture_bindings[(t.m_Name,*size)]=mapping[old_guid(p)];texture_proofs.append(dict(source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),compressed_sha256=sha(t.get_image_data()),rgba_sha256=sha(rgba),mips=mips,profile=profile))
        elif p.suffix=='.asset' and b'--- !u!213 ' in raw:continue
        else:
            raw=remap(raw.decode('utf-8-sig').replace('\r\n','\n')).encode();metadata=remap(metadata)
            if old_guid(p) in tile_guids:
                candidates=tiles.get(p.stem,[])
                if len(candidates)!=1:raise ValueError('Unique original Tile required '+p.stem)
                obj=candidates[0];tile_proofs.append(dict(source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),path=target.relative_to(ROOT).as_posix()))
        emit(target,raw,metadata)
    for p in sorted(found):
        if p.suffix!='.asset' or b'--- !u!213 ' not in native_io_path(p).read_bytes():continue
        candidates=sprites.get(p.stem,[])
        if len(candidates)!=1:raise ValueError('Unique original fog Sprite required '+p.stem)
        obj=candidates[0];sprite=obj.read();original=copy.deepcopy(obj.read_typetree(check_read=True));rd=original['m_RD'];atlas_record=None
        if sprite.m_SpriteAtlas.path_id:
            aobj=sprite.m_SpriteAtlas.deref();atlas=aobj.read();af=aobj.read_typetree(check_read=True);entry=next(v for k,v in af['m_RenderDataMap'] if k==original['m_RenderDataKey']);effective=next(v for k,v in atlas.m_RenderDataMap if k==sprite.m_RenderDataKey);tobj=effective.texture.deref();rd.update(copy.deepcopy(entry));original['m_SpriteAtlas']={'fileID':0};atlas_record=dict(source=apk.provenance(aobj),object_sha256=sha(aobj.get_raw_data()))
        else:tobj=sprite.m_RD.texture.deref()
        texture=tobj.read();tguid=texture_bindings[(texture.m_Name,texture.m_Width,texture.m_Height)];uv=copy.deepcopy(rd['uvTransform']);rd['texture']={'fileID':0};key,local_id=original['m_RenderDataKey'];original['m_RenderDataKey']={''.join(f"{key['data['+str(i)+']']:08x}"[::-1] for i in range(4)):local_id};original['m_Rect']['serializedVersion']=2;rd['serializedVersion']=3;rd['textureRect']['serializedVersion']=2
        for submesh in rd['m_SubMeshes']:submesh['serializedVersion']=2
        rd['m_IndexBuffer']=bytes(rd['m_IndexBuffer']).hex();vd=rd['m_VertexData'];vraw=bytes(vd['m_DataSize']);vd.update(serializedVersion=3,m_DataSize=len(vraw),_typelessdata=vraw.hex());native=source_header();native.update(fields(original));native['m_RD']['texture']=dict(fileID=2800000,guid=tguid,type=2)
        raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n'+yaml.dump(dict(Sprite=native),Dumper=Dumper,sort_keys=False,allow_unicode=True)).encode();target=DEST/p.relative_to(base);emit(target,raw,native_meta(mapping[old_guid(p)],21300000));mesh=MeshHandler(sprite.m_RD,obj.version);mesh.process();sprite_proofs.append(dict(path=target.relative_to(ROOT).as_posix(),source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),atlas=atlas_record,vertices=[dict(x=v[0],y=v[1]) for v in mesh.m_Vertices],triangles=list(mesh.m_IndexBuffer),uv=[dict(x=(v[0]*uv['x']+uv['y'])/texture.m_Width,y=(v[1]*uv['z']+uv['w'])/texture.m_Height) for v in mesh.m_Vertices],rect={k:getattr(sprite.m_Rect,k) for k in ['x','y','width','height']},ppu=sprite.m_PixelsToUnits,pivot=dict(x=sprite.m_Pivot.x*sprite.m_Rect.width,y=sprite.m_Pivot.y*sprite.m_Rect.height)))
    relative='Prefabs/Map/Map_13/MiniMap/GroundMaskTilemap.prefab';tilemap_raw=re.sub(r'  m_Father: \{fileID: 4126042694364515\}', '  m_Father: {fileID: 0}',tilemap_raw);emit(DEST/relative,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+remap(tilemap_raw)).encode(),'fileFormatVersion: 2\nguid: '+identity(relative)+'\nPrefabImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    for folder in [DEST,*sorted(p for p in DEST.rglob('*') if p.is_dir())]:meta(folder,folder=True)
    write(ROOT/'assets/reference/official-map/map13-native-minimap-masks-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest=apk.version,bundle_inputs=inputs['inputs'],converter_input_receipt_sha256=sha((cache/'source-manifest.json').read_bytes()),export_proof_sha256=sha((cache/'export-proof.json').read_bytes()),exported_inputs=exported,tiles=tile_proofs,sprites=sprite_proofs,textures=texture_proofs,shader=dict(source=apk.provenance(sobj),object_sha256=sha(sobj.get_raw_data()),programs=shader_programs),tilemap=dict(root_fileid=1194081240383615,components=4,operation='Copy complete original GameObject/Transform/Tilemap/TilemapRenderer; detach its parent only. Keep all source Tilemap arrays (including unused zero-ref entries), anchor/orientation, sorting/material/render fields.'),outputs=outputs,generator=record(Path(__file__).resolve()),tools=[record(ROOT/'scripts'/n) for n in ['prepare_unity_map13_native_sprite_yaml.py','prepare_unity_map13_native_scene.py','prepare_unity_map13_water_shaders.py','prepare_battle_hud_materials.py','official_shader_programs.py']],operation='Restore all eleven original fog Tiles and their complete original Sprite geometry/atlas UV/pivot, exact ASTC mips and material; mechanically translate all original GLES fog variants and serialized pass state. Extract the complete original fog Tilemap object for native module rendering. Preserve original native Tile and no substitute art/layout.',limitations=['Original mask manager/root bindings, formal input/save and Windows Player remain required.'])))
    print('Original minimap fog assets:',len(tile_proofs),'Tiles;',len(sprite_proofs),'Sprites;',len(outputs),'outputs;',len(programs),'shader variants',flush=True);apk.archive.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path,required=True);build(parser.parse_args().cache)
