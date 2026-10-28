"""Restore 23 original minimap resource boards and their original dynamic icons."""
import argparse
import copy
import hashlib
import json
import re
from pathlib import Path
import yaml
from official_apk import Apk,ROOT
from prepare_unity_bundle_inputs import collect,serialized_files
from prepare_unity_map13_minimap_inputs import ROOT_BUNDLE
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_scene import decode_mips,native_texture
from prepare_unity_map13_prop_parts import meta as code_meta
from prepare_unity_map13_sources import encode,meta,sha,source_path,write
from prepare_unity_map13_water_entity_export import native_io_path
from prepare_unity_map13_native_sprite_yaml import Dumper,fields,source_header
from UnityPy.helpers.MeshHelper import MeshHandler

DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapResourcePoints'
RUNTIME=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap13MiniMapResources'
CATALOG=ROOT/'unity/Assets/Game/Resources/Map13/Native/minimap-resource-point-assets.asset'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')
PREFABS='Prefabs/Map/Map_13/MiniMap/MapResourcePoint13'


def identity(relative):return hashlib.md5(('zjcs-map13-minimap-resource-points:'+relative).encode()).hexdigest()


def build(cache):
    cache=cache.resolve()
    if not cache.is_relative_to(ROOT/'output/validation'):raise ValueError('Owned validation cache required')
    inputs=json.loads((cache/'source-manifest.json').read_bytes());proof=json.loads((cache/'export-proof.json').read_bytes())
    if inputs['manifest']!='871_156598' or inputs['roots']!=[ROOT_BUNDLE] or proof['counts']['registries']!=12:raise ValueError('Verified original minimap export required')
    for entry in inputs['inputs']:
        if sha(native_io_path(cache/'inputs'/entry['output']).read_bytes())!=entry['converter_input_sha256']:raise ValueError('Converter input changed')
    base=cache/'export/ExportedProject/Assets';index={}
    for p in base.rglob('*.meta'):
        m=GUID.search(native_io_path(p).read_text(encoding='utf-8-sig'))
        if m:
            if m[1] in index:raise ValueError('Duplicate export GUID')
            index[m[1]]=Path(str(p)[:-5])
    roots=sorted((base/PREFABS).glob('*.prefab'));pending=list(roots);found=set()
    if len(roots)!=23:raise ValueError('Complete original 23-board set required')
    while pending:
        p=pending.pop()
        if p in found:continue
        if not native_io_path(p).is_file():raise FileNotFoundError(p)
        found.add(p)
        if p.suffix=='.cs':continue
        for item in [p,Path(str(p)+'.meta')]:
            if item.suffix not in ['.asset','.prefab','.mat','.meta']:continue
            for g in GUID.findall(native_io_path(item).read_text(encoding='utf-8-sig')):
                if g in index:pending.append(index[g])
                elif g not in ['0'*32,'0000000000000000e000000000000000','0000000000000000f000000000000000']:raise ValueError('Unresolved original board reference '+g)
    counts={suffix:sum(p.suffix==suffix for p in found) for suffix in {p.suffix for p in found}}
    if counts!={'.prefab':23,'.asset':2,'.png':2,'.cs':2}:raise ValueError(counts)
    replacements={};bindings=[]
    for p in sorted(found):
        old=GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]
        if p.suffix=='.cs':
            target=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapResourcePoints'/p.name
            if not target.is_file():raise ValueError('Unported original resource-point script: '+p.name)
            guid=GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8'))[1];bindings.append(dict(original=p.relative_to(base).as_posix(),**record(target),guid=guid))
        else:guid=identity(p.relative_to(base).as_posix())
        replacements[old]=guid
    manager_script=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapResourcePoints/MiniMapResourcePointManager.cs'
    manager_old=GUID.search(native_io_path(base/'Scripts/Assembly-CSharp/MiniMapResourcePointManager.cs.meta').read_text(encoding='utf-8-sig'))[1]
    replacements[manager_old]=GUID.search(Path(str(manager_script)+'.meta').read_text())[1]
    bindings.append(dict(original='Scripts/Assembly-CSharp/MiniMapResourcePointManager.cs',**record(manager_script),guid=replacements[manager_old]))
    def remap(text):return GUID.sub(lambda m:'guid: '+replacements.get(m[1],m[1]),text)
    apk=Apk.current();_,missing=collect(apk,[ROOT_BUNDLE])
    if missing:raise ValueError(missing)
    textures={}
    for bundle in list(apk.loaded.values()):
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name=='Texture2D':
                    t=obj.read();textures.setdefault(t.m_Name,[]).append((obj,t))
    template=ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset';template_text=template.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
    outputs=[];exported=[];texture_proofs=[]
    def emit(target,raw,metadata):
        write(target,raw);write(Path(str(target)+'.meta'),metadata.encode());outputs.append(dict(**record(target),meta_sha256=sha(Path(str(target)+'.meta').read_bytes())))
    def native_meta(guid,fileid):return 'fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(fileid)+'\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    for p in sorted(found):
        if p.suffix=='.cs':continue
        raw=native_io_path(p).read_bytes();relative=p.relative_to(base);metadata=native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig').replace('\r\n','\n');exported.append(dict(relative=relative.as_posix(),sha256=sha(raw),meta_sha256=sha(native_io_path(Path(str(p)+'.meta')).read_bytes())));target=DEST/relative
        if p.suffix=='.png':
            candidates=textures[p.stem]
            if len(candidates)!=1:raise ValueError('Unique original board texture required')
            obj,t=candidates[0];rgba,mips,profile=decode_mips(t);target=target.with_name(target.stem+'.texture.asset');raw=native_texture(template_text,t,rgba);metadata=native_meta(identity(relative.as_posix()),2800000)
            texture_proofs.append(dict(source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),compressed_sha256=sha(t.get_image_data()),rgba_sha256=sha(rgba),mips=mips,profile=profile,output=source_path(target)))
        else:
            text=remap(raw.decode('utf-8-sig').replace('\r\n','\n'))
            if p.suffix=='.asset':text=re.sub(r'(\{fileID: 2800000, guid: [a-f0-9]{32}, type: )3(\})',r'\g<1>2\2',text)
            raw=text.encode()
        emit(target,raw,remap(re.sub(r'(?m)^timeCreated: \d+\n','',metadata)))
    original_root=native_io_path(base/'Prefabs/Map/Map_13/MiniMap/MiniMap_13.prefab').read_text(encoding='utf-8-sig').replace('\r\n','\n')
    documents={int(m[2]):m[0] for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n.*?(?=^--- !u!|\Z)',original_root)}
    manager_document=next(v for v in documents.values() if 'guid: '+manager_old+',' in v)
    manager_go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',manager_document)[1]);go_document=documents[manager_go]
    component_ids=[int(v) for v in re.findall(r'component: \{fileID: (\d+)\}',go_document)]
    if len(component_ids)!=2 or not documents[component_ids[0]].startswith('--- !u!4 '):raise ValueError('Original manager component hierarchy changed')
    manager_relative='Prefabs/Map/Map_13/MiniMap/ResourcePointManager.prefab'
    manager_native='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+go_document+re.sub(r'm_Father: \{fileID: \d+\}','m_Father: {fileID: 0}',documents[component_ids[0]])+manager_document
    emit(DEST/manager_relative,remap(manager_native).encode(),'fileFormatVersion: 2\nguid: '+identity(manager_relative)+'\nPrefabImporter:\n  externalObjects: {}\n')
    source_dir=ROOT/'unity/Assets/Official/World/Map/13/Source';components_path=source_dir/'NativeMiniMapSource/native-minimap-components.json';tables_path=source_dir/'tables.json'
    components=json.loads(components_path.read_bytes())['components'];manager=next(c['fields'] for c in components if c['type']=='MiniMapResourcePointManager');tables=json.loads(tables_path.read_bytes());layer=manager['mResourceLayerPrefabGuidDict']['mSerializeBuffer'];wanted={r['Key'] for r in layer};groups=[g for g in tables['Map/map_entity_group'] if g['Id'] in wanted]
    if len(groups)!=23:raise ValueError('All original group icon paths required')
    icon_paths={g['Icon'] for g in groups};names={Path(p).name for p in icon_paths};sprites={}
    for bundle in ['image_itemicon_gameheadicon_monster_map4countries_noatlas','image_itemicon_materials_public_noatlas','image_itemicon_materials_map11_noatlas','image_itemicon_materials_map13_noatlas']:
        for obj in apk.objects(bundle):
            if obj.type.name=='Sprite':
                sprite=obj.read()
                if sprite.m_Name in names:
                    if sprite.m_Name in sprites:raise ValueError('Ambiguous original resource icon')
                    sprites[sprite.m_Name]=obj
    if set(sprites)!=names:raise ValueError('Original icon not located: '+str(names-set(sprites)))
    icon_proofs=[];icon_assets={};static_sprite_proofs=[]
    def write_native(kind,relative,native):
        fileid=21300000 if kind=='Sprite' else 2800000;classid=213 if kind=='Sprite' else 28;target=DEST/relative
        raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!'+str(classid)+' &'+str(fileid)+'\n'+yaml.dump({kind:native},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode();guid=identity(relative);emit(target,raw,native_meta(guid,fileid));return dict(guid=guid,file_id=fileid,path=source_path(target),sha256=sha(raw))
    for path in sorted(icon_paths):
        obj=sprites[Path(path).name];sprite=obj.read()
        if sprite.m_SpriteAtlas.path_id or sprite.m_RD.alphaTexture.path_id or sprite.m_RD.secondaryTextures or sprite.m_Bones:raise ValueError('Additional original dynamic icon dependencies require restoration')
        tobj=sprite.m_RD.texture.deref();texture=tobj.read();rgba,mips,profile=decode_mips(texture);relative='Icons/'+sprite.m_Name+'.texture.asset';tguid=identity(relative);emit(DEST/relative,native_texture(template_text,texture,rgba),native_meta(tguid,2800000))
        original=copy.deepcopy(obj.read_typetree(check_read=True));rd=original['m_RD'];rd['texture']={'fileID':0};key,local_id=original['m_RenderDataKey'];original['m_RenderDataKey']={''.join(f"{key['data['+str(i)+']']:08x}"[::-1] for i in range(4)):local_id};original['m_Rect']['serializedVersion']=2;rd['serializedVersion']=3;rd['textureRect']['serializedVersion']=2
        for submesh in rd['m_SubMeshes']:submesh['serializedVersion']=2
        rd['m_IndexBuffer']=bytes(rd['m_IndexBuffer']).hex();vd=rd['m_VertexData'];raw=bytes(vd['m_DataSize']);vd.update(serializedVersion=3,m_DataSize=len(raw),_typelessdata=raw.hex());native=source_header();native.update(fields(original));native['m_RD']['texture']=dict(fileID=2800000,guid=tguid,type=2);asset=write_native('Sprite','Icons/'+sprite.m_Name+'.sprite.asset',native);icon_assets[path]=asset
        mesh=MeshHandler(sprite.m_RD,obj.version);mesh.process();icon_proofs.append(dict(path=path,source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),texture_source=apk.provenance(tobj),texture_object_sha256=sha(tobj.get_raw_data()),compressed_sha256=sha(texture.get_image_data()),rgba_sha256=sha(rgba),mips=mips,profile=profile,vertices=[dict(x=v[0],y=v[1]) for v in mesh.m_Vertices],triangles=list(mesh.m_IndexBuffer),ppu=sprite.m_PixelsToUnits,pivot=dict(x=sprite.m_Pivot.x*sprite.m_Rect.width,y=sprite.m_Pivot.y*sprite.m_Rect.height),asset=asset))
    # AssetRipper rewrites packed Sprite logical rectangles. Restore the original
    # logical Sprite/mesh and explicitly bind its original effective Atlas data.
    for p in sorted(v for v in found if v.suffix=='.asset'):
        matches=[]
        for bundle in list(apk.loaded.values()):
            for f in serialized_files(bundle):
                for obj in f.objects.values():
                    if obj.type.name=='Sprite' and obj.read().m_Name==p.stem:matches.append(obj)
        if len(matches)!=1:raise ValueError('Unique original static marker Sprite required')
        obj=matches[0];sprite=obj.read();original=copy.deepcopy(obj.read_typetree(check_read=True));rd=original['m_RD'];atlas_record=None
        if sprite.m_SpriteAtlas.path_id:
            atlas_obj=sprite.m_SpriteAtlas.deref();atlas=atlas_obj.read();atlas_fields=atlas_obj.read_typetree(check_read=True)
            entry=next(value for key,value in atlas_fields['m_RenderDataMap'] if key==original['m_RenderDataKey']);effective=next(value for key,value in atlas.m_RenderDataMap if key==sprite.m_RenderDataKey);tobj=effective.texture.deref();rd.update(copy.deepcopy(entry));original['m_SpriteAtlas']={'fileID':0};atlas_record=dict(source=apk.provenance(atlas_obj),object_sha256=sha(atlas_obj.get_raw_data()),effective_render_data=entry)
        else:tobj=sprite.m_RD.texture.deref()
        texture=tobj.read();source=apk.provenance(tobj);recorded=next(t for t in texture_proofs if t['source']['bundle']==source['bundle'] and t['source']['path_id']==source['path_id']);texture_target=ROOT/recorded['output'];texture_guid=GUID.search(Path(str(texture_target)+'.meta').read_text())[1];uv=rd['uvTransform'];rd['texture']={'fileID':0};key,local_id=original['m_RenderDataKey'];original['m_RenderDataKey']={''.join(f"{key['data['+str(i)+']']:08x}"[::-1] for i in range(4)):local_id};original['m_Rect']['serializedVersion']=2;rd['serializedVersion']=3;rd['textureRect']['serializedVersion']=2
        for submesh in rd['m_SubMeshes']:submesh['serializedVersion']=2
        rd['m_IndexBuffer']=bytes(rd['m_IndexBuffer']).hex();vd=rd['m_VertexData'];raw=bytes(vd['m_DataSize']);vd.update(serializedVersion=3,m_DataSize=len(raw),_typelessdata=raw.hex());native=source_header();native.update(fields(original));native['m_RD']['texture']=dict(fileID=2800000,guid=texture_guid,type=2);asset=write_native('Sprite',p.relative_to(base).as_posix(),native)
        mesh=MeshHandler(sprite.m_RD,obj.version);mesh.process();static_sprite_proofs.append(dict(source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),atlas=atlas_record,vertices=[dict(x=v[0],y=v[1]) for v in mesh.m_Vertices],triangles=list(mesh.m_IndexBuffer),uv=[dict(x=(v[0]*uv['x']+uv['y'])/texture.m_Width,y=(v[1]*uv['z']+uv['w'])/texture.m_Height) for v in mesh.m_Vertices],rect={key:getattr(sprite.m_Rect,key) for key in ['x','y','width','height']},ppu=sprite.m_PixelsToUnits,pivot=dict(x=sprite.m_Pivot.x*sprite.m_Rect.width,y=sprite.m_Pivot.y*sprite.m_Rect.height),asset=asset))
    # The final source-native Sprite replaces the converter's intermediary copy.
    latest={r['path']:r for r in outputs};outputs=list(latest.values())
    data_path=DEST/'resource-point-data.json';data=dict(Groups=[dict(Id=g['Id'],Icon=g['Icon']) for g in groups],ItemLocations=tables['Map/item_map_location'],WorldLocations=tables['Map/map_world_refresh_group_location'],Regions=[r for r in tables['Map/map_region'] if r['ClassId']==13]);write(data_path,encode(data));meta(data_path);outputs.append(record(data_path))
    manager_path=DEST/'resource-point-manager-fields.json';write(manager_path,encode(manager));meta(manager_path);outputs.append(record(manager_path))
    code_meta(RUNTIME,folder=True)
    for p in RUNTIME.iterdir():
        if p.suffix in ['.cs','.asmdef']:code_meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter')
    script=RUNTIME/'MiniMapResourcePointAssets.cs';script_guid=GUID.search(Path(str(script)+'.meta').read_text())[1]
    body='  ManagerPrefab: {fileID: '+str(manager_go)+', guid: '+identity(manager_relative)+', type: 3}\n  Boards:\n'
    for item in layer:
        relative=PREFABS+'/ResourcePoint_13_'+str(item['Key'])+'.prefab'
        if base/relative not in found:raise ValueError('Original board name/group mismatch')
        body+='  - RefreshGroup: '+str(item['Key'])+'\n    OriginalGuid: '+item['Value']['mAssetGuid']+'\n    Prefab: {fileID: 1709254077376921, guid: '+identity(relative)+', type: 3}\n'
    body+='  Icons:\n'
    for path,asset in sorted(icon_assets.items()):body+='  - Path: '+path+'\n    Sprite: {fileID: 21300000, guid: '+asset['guid']+', type: 2}\n'
    body+='  SourceData: {fileID: 4900000, guid: '+GUID.search(Path(str(data_path)+'.meta').read_text())[1]+', type: 3}\n  ManagerFields: {fileID: 4900000, guid: '+GUID.search(Path(str(manager_path)+'.meta').read_text())[1]+', type: 3}\n'
    catalog_raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+script_guid+', type: 3}\n  m_Name: Map13MiniMapResourcePoints\n  m_EditorClassIdentifier:\n'+body).encode();write(CATALOG,catalog_raw);catalog_guid=code_meta(CATALOG,'NativeFormatImporter');write(Path(str(CATALOG)+'.meta'),native_meta(catalog_guid,11400000).encode());outputs.append(record(CATALOG))
    for folder in [DEST,*sorted(p for p in DEST.rglob('*') if p.is_dir())]:meta(folder,folder=True)
    write(ROOT/'assets/reference/official-map/map13-native-minimap-resource-points-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest=apk.version,inputs=[record(components_path),record(tables_path)],bundle_inputs=inputs['inputs'],converter_input_receipt_sha256=sha((cache/'source-manifest.json').read_bytes()),export_proof_sha256=sha((cache/'export-proof.json').read_bytes()),exported_inputs=exported,script_bindings=bindings,textures=texture_proofs,icons=icon_proofs,static_sprites=static_sprite_proofs,outputs=outputs,generator=record(Path(__file__).resolve()),tools=[record(ROOT/'scripts/prepare_unity_map13_native_sprite_yaml.py'),record(ROOT/'scripts/prepare_unity_map13_native_scene.py')],runtime_assets=[record(p) for p in RUNTIME.iterdir() if p.suffix in ['.cs','.asmdef']],operation='Restore all 23 original resource-board prefabs and every referenced native object. Extract the original manager GameObject/Transform/component as a standalone loadable component prefab; change only its parent reference to detach the subtree. Explicitly bind original group GUIDs, exact official configuration rows and all 23 dynamic icon paths. Preserve complete original Sprite logical rectangles, geometry, UVs, pivot, PPU, render keys and sampler flags; resolve the original packed background Sprite using its actual Atlas render data and explicit native texture binding. Decode exact original ASTC mips to native RGBA32 without resampling/recompression. No UI layouts or substitute assets are created.',limitations=['Actual manager/board/icon lifecycle, formal map mouse/save, original root binding and Windows Player remain required.'])))
    print('Original minimap resource boards restored:',len(roots),'boards,',len(icon_assets),'dynamic icons,',len(outputs),'outputs',flush=True);apk.archive.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path,required=True);build(parser.parse_args().cache)
