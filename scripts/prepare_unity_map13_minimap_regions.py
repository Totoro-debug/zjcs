"""Restore all original Map13 regional Tilemaps and the executable mask shader."""
import argparse
import hashlib
import io
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from PIL import Image
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_minimap_inputs import ROOT_BUNDLE
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_water_entity_export import native_io_path
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map13_water_components import import_meta

DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapRegions'
SHADER=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMap/MiniMapMaskableTilemap.shader'
SOURCE=ROOT/'unity/Assets/Official/World/Map/13/Source/MiniMapShaders'
RECEIPT=ROOT/'assets/reference/official-map/map13-native-minimap-regions-source-manifest.json'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')


def guid_for(relative):
    return hashlib.md5(('zjcs-map13-minimap-regions:'+relative.as_posix()).encode()).hexdigest()


def closure(base):
    index={}
    for path in base.rglob('*.meta'):
        match=GUID.search(native_io_path(path).read_text(encoding='utf-8-sig'))
        if match:
            if match[1] in index:raise ValueError('Duplicate original export GUID')
            index[match[1]]=Path(str(path)[:-5])
    roots=sorted((base/'Prefabs/Map/Map_13/MiniMap/MinimapRegionTilemap').glob('*.prefab'))
    if len(roots)!=106:raise ValueError('Original Map13 requires exactly 106 regional Tilemaps')
    pending=list(roots);found=set()
    while pending:
        path=pending.pop()
        if path in found:continue
        if not native_io_path(path).is_file():raise FileNotFoundError(path)
        found.add(path)
        for item in [path,Path(str(path)+'.meta')]:
            if item.suffix not in ['.prefab','.asset','.mat','.meta']:continue
            for guid in GUID.findall(native_io_path(item).read_text(encoding='utf-8-sig')):
                if guid in index:pending.append(index[guid])
                elif guid not in ['0'*32,'0000000000000000e000000000000000','0000000000000000f000000000000000']:
                    raise ValueError('Unresolved original regional Tilemap dependency: '+guid)
    counts={suffix:sum(p.suffix==suffix for p in found) for suffix in {p.suffix for p in found}}
    if counts!={'.prefab':106,'.asset':13,'.png':2,'.shader':1,'.mat':1}:raise ValueError(counts)
    return roots,sorted(found)


def build(cache):
    cache=cache.resolve()
    if not cache.is_relative_to(ROOT/'output/validation'):raise ValueError('Owned validation cache required')
    input_receipt=cache/'source-manifest.json'
    receipt=json.loads(input_receipt.read_bytes())
    if receipt['manifest']!='871_156598' or receipt['roots']!=[ROOT_BUNDLE]:raise ValueError('Incorrect source version')
    for entry in receipt['inputs']:
        if sha(native_io_path(cache/'inputs'/entry['output']).read_bytes())!=entry['converter_input_sha256']:
            raise ValueError('Original conversion input changed')
    base=cache/'export/ExportedProject/Assets';roots,files=closure(base)
    apk=Apk.current();_,missing=collect(apk,[ROOT_BUNDLE])
    if missing:raise ValueError(missing)
    textures={};shaders=[]
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                if obj.type.name=='Texture2D':
                    texture=obj.read();textures.setdefault((texture.m_Name,texture.m_Width,texture.m_Height),[]).append((obj,texture))
                elif obj.type.name=='Shader' and obj.read().m_ParsedForm.m_Name=='Sprites/MaskableTilemap':shaders.append(obj)
    if len(shaders)!=1:raise ValueError('Original mask shader must resolve uniquely')
    obj=shaders[0];shader=obj.read();text,programs=translate(shader,'MiniMapMaskableTilemap')
    stencil=shader.m_ParsedForm.m_SubShaders[0].m_Passes[0].m_State.stencilOp
    if [getattr(stencil,key).val for key in ['comp','fail','pass_','zFail']]!=[8,0,0,0]:raise ValueError('Restore original stencil state')
    write(SHADER,text.encode());import_meta(SHADER,'ShaderImporter')
    shader_guid=GUID.search(Path(str(SHADER)+'.meta').read_text(encoding='utf8'))[1]
    outputs=[record(SHADER)];shader_programs=[]
    for p in programs:
        target=SOURCE/('MaskableTilemap-'+str(p['index'])+'.glsl.txt');write(target,p['source'].encode());meta(target)
        outputs.append(record(target));shader_programs.append(dict(index=p['index'],keywords=p['keywords'],**record(target)))
    replacements={GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]:(shader_guid if p.suffix=='.shader' else guid_for(p.relative_to(base))) for p in files}
    texture_guids={guid_for(p.relative_to(base)) for p in files if p.suffix=='.png'}
    def remap(text):
        text=GUID.sub(lambda m:'guid: '+replacements.get(m[1],m[1]),text)
        return re.sub(r'(\{fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3(\})',lambda m:m[1]+m[2]+m[3]+('2' if m[2] in texture_guids else '3')+m[4],text)
    # Reuse the exact native Texture2D schema already imported and verified in
    # this Unity version; replace all source-dependent fields and raw mip data.
    template_path=ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset'
    template=template_path.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
    exported=[];texture_records=[];regions=[]
    for path in files:
        if path.suffix=='.shader':continue
        relative=path.relative_to(base);raw=native_io_path(path).read_bytes();metadata=native_io_path(Path(str(path)+'.meta')).read_text(encoding='utf-8-sig')
        exported.append(dict(relative=relative.as_posix(),sha256=sha(raw),meta_sha256=sha(native_io_path(Path(str(path)+'.meta')).read_bytes())))
        target=DEST/relative
        if path.suffix=='.png':
            size=Image.open(io.BytesIO(raw)).size;candidates=textures.get((path.stem,*size),[])
            if len(candidates)!=1:raise ValueError('Original texture is not unique: '+str(relative))
            original,texture=candidates[0];rgba,mips,profile=decode_mips(texture)
            target=target.with_name(target.stem+'.texture.asset');raw=native_texture(template,texture,rgba)
            metadata='fileFormatVersion: 2\nguid: '+guid_for(relative)+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
            texture_records.append(dict(path=source_path(target),source=apk.provenance(original),object_sha256=sha(original.get_raw_data()),compressed_sha256=sha(texture.get_image_data()),rgba_sha256=sha(rgba),profile=profile,mips=mips,width=texture.m_Width,height=texture.m_Height,mip_count=texture.m_MipCount,sRGB=texture.m_ColorSpace==1,filter=texture.m_TextureSettings.m_FilterMode,wrapU=texture.m_TextureSettings.m_WrapU,wrapV=texture.m_TextureSettings.m_WrapV,wrapW=texture.m_TextureSettings.m_WrapW,aniso=texture.m_TextureSettings.m_Aniso,mipBias=texture.m_TextureSettings.m_MipBias,readable=texture.m_IsReadable))
        elif path.suffix in ['.asset','.prefab','.mat']:
            text=raw.decode('utf-8-sig').replace('\r\n','\n');raw=remap(text).encode()
            if path in roots:
                tile_count=len(re.findall(r'^  - first: \{x:',text,re.M))
                regions.append(dict(region=int(path.stem.split('_')[-1]),path=source_path(target),guid=guid_for(relative),tiles=tile_count,source_export_sha256=sha(native_io_path(path).read_bytes())))
        else:raise ValueError('Unexpected dependency '+str(relative))
        metadata=re.sub(r'(?m)^timeCreated: \d+\n','',metadata.replace('\r\n','\n'))
        write(target,raw);write(Path(str(target)+'.meta'),remap(metadata).encode())
        outputs.append(dict(**record(target),meta_sha256=sha(Path(str(target)+'.meta').read_bytes())))
    for folder in [DEST,SOURCE,*sorted(p for p in DEST.rglob('*') if p.is_dir())]:meta(folder,folder=True)
    write(RECEIPT,encode(dict(format=1,mapClassId=13,manifest=apk.version,inputs=receipt['inputs'],converter_input_receipt_sha256=sha(input_receipt.read_bytes()),exported_inputs=exported,regions=sorted(regions,key=lambda r:r['region']),textures=texture_records,shader=dict(source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),programs=shader_programs),outputs=outputs,tools=[record(p) for p in [Path(__file__).resolve(),ROOT/'scripts/prepare_unity_map13_native_scene.py',ROOT/'scripts/prepare_unity_map13_water_shaders.py',ROOT/'scripts/prepare_battle_hud_materials.py',ROOT/'scripts/official_shader_programs.py']],serialization_reference=record(template_path),operation='Restore the complete 106 original regional Tilemap prefabs with all tile records, transforms, colors, matrices, renderer state, twelve original Tile assets and the original Sprite mesh. Decode both original ASTC textures including every mip to native RGBA32 without flipping, resampling or changing color space. Mechanically translate all four original mask shader variants, preserving arithmetic and serialized render state; remap only GUID identities and native Texture2D asset type.',limitations=['Regional resources only. Original root minimap managers, local visibility/status binding, formal minimap input/save and Windows Player remain required.'])))
    print('Restored original Map13 minimap:',len(regions),'regions,',sum(r['tiles'] for r in regions),'tile entries,',len(outputs),'outputs,',len(programs),'shader programs',flush=True)
    apk.archive.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path,required=True);build(parser.parse_args().cache)
