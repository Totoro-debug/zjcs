"""Restore the complete selected official MiniMapSettings native asset closure."""
import argparse
import hashlib
import io
import json
import re
from pathlib import Path
from official_apk import Apk,ROOT
from PIL import Image
from prepare_unity_bundle_inputs import collect,serialized_files
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_scene import decode_mips,native_texture
from prepare_unity_map13_sources import encode,meta,sha,source_path,write
from prepare_unity_map13_water_entity_export import native_io_path

BUNDLE='defaultpackage_assets_scriptableconfig_modules_mapcommon.bundle'
DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapSettings'
SOURCE=ROOT/'unity/Assets/Official/World/Map/13/Source/NativeMiniMapSource/minimap-settings.json'
RECEIPT=ROOT/'assets/reference/official-map/map13-native-minimap-settings-source-manifest.json'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')


def guid_for(relative):return hashlib.md5(('zjcs-map13-minimap-settings:'+relative.as_posix()).encode()).hexdigest()


def closure(base):
    index={}
    for p in base.rglob('*.meta'):
        m=GUID.search(native_io_path(p).read_text(encoding='utf-8-sig'))
        if m:
            if m[1] in index:raise ValueError('Duplicate export GUID')
            index[m[1]]=Path(str(p)[:-5])
    root=base/'ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset';pending=[root];found=set()
    while pending:
        p=pending.pop()
        if p in found:continue
        if not native_io_path(p).is_file():raise FileNotFoundError(p)
        found.add(p)
        if p.suffix in ['.cs','.shader']:continue
        for item in [p,Path(str(p)+'.meta')]:
            if item.suffix not in ['.asset','.mat','.prefab','.meta']:continue
            for g in GUID.findall(native_io_path(item).read_text(encoding='utf-8-sig')):
                if g in index:pending.append(index[g])
                elif g not in ['0'*32,'0000000000000000e000000000000000','0000000000000000f000000000000000']:raise ValueError('Unresolved original settings reference '+g)
    counts={kind:sum(p.suffix==kind for p in found) for kind in {p.suffix for p in found}}
    if counts!={'.asset':73,'.cs':2,'.png':1,'.prefab':1,'.mat':1}:raise ValueError(counts)
    return root,sorted(found)


def build(cache):
    cache=cache.resolve()
    if not cache.is_relative_to(ROOT/'output/validation'):raise ValueError('Owned validation cache required')
    original_receipt=cache/'source-manifest.json';inputs=json.loads(original_receipt.read_bytes())
    if inputs['manifest']!='871_156598' or inputs['roots']!=[BUNDLE]:raise ValueError('Wrong official source')
    for item in inputs['inputs']:
        if sha(native_io_path(cache/'inputs'/item['output']).read_bytes())!=item['converter_input_sha256']:raise ValueError('Converter input changed')
    proof_path=cache/'export-proof.json';proof=json.loads(proof_path.read_bytes())
    if proof['counts']['registries']!=1:raise ValueError('Original reference-registry verification required')
    base=cache/'export/ExportedProject/Assets';root,files=closure(base);bindings=[];replacements={}
    for p in files:
        old=GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]
        if p.suffix=='.cs':
            target=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapCamera'/(p.stem+'.cs')
            if not target.is_file():raise ValueError('Original runtime script not migrated: '+p.stem)
            replacements[old]=GUID.search(Path(str(target)+'.meta').read_text(encoding='utf8'))[1]
            bindings.append(dict(original=p.relative_to(base).as_posix(),**record(target),guid=replacements[old],file_id=11500000))
        else:replacements[old]=guid_for(p.relative_to(base))
    texguids={guid_for(p.relative_to(base)) for p in files if p.suffix=='.png'}
    def remap(text):
        text=GUID.sub(lambda m:'guid: '+replacements.get(m[1],m[1]),text)
        return re.sub(r'(\{fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3(\})',lambda m:m[1]+m[2]+m[3]+('2' if m[2] in texguids else '3')+m[4],text)
    apk=Apk.current();_,missing=collect(apk,[BUNDLE])
    if missing:raise ValueError(missing)
    original=apk.asset(BUNDLE,'/MiniMapSettings.asset');fields=next(c['fields'] for c in inputs['components'] if c['type']=='MiniMapSettings')
    write(SOURCE,encode(dict(source=apk.provenance(original),object_sha256=sha(original.get_raw_data()),fields=fields)));meta(SOURCE)
    textures={}
    for bundle in apk.loaded.values():
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name=='Texture2D':
                    t=obj.read();textures.setdefault((t.m_Name,t.m_Width,t.m_Height),[]).append((obj,t))
    template_path=ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset';template=template_path.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
    exported=[];outputs=[record(SOURCE)];texture_records=[]
    for p in files:
        if p.suffix=='.cs':continue
        relative=p.relative_to(base);raw=native_io_path(p).read_bytes();metadata=native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig').replace('\r\n','\n')
        exported.append(dict(relative=relative.as_posix(),sha256=sha(raw),meta_sha256=sha(native_io_path(Path(str(p)+'.meta')).read_bytes())));target=DEST/relative
        if p.suffix=='.png':
            size=Image.open(io.BytesIO(raw)).size;candidates=textures[(p.stem,*size)]
            if len(candidates)!=1:raise ValueError('Source texture not unique')
            obj,t=candidates[0];rgba,mips,profile=decode_mips(t);target=target.with_name(target.stem+'.texture.asset');raw=native_texture(template,t,rgba)
            metadata='fileFormatVersion: 2\nguid: '+guid_for(relative)+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
            texture_records.append(dict(path=source_path(target),source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),compressed_sha256=sha(t.get_image_data()),rgba_sha256=sha(rgba),profile=profile,mips=mips,width=t.m_Width,height=t.m_Height,mip_count=t.m_MipCount,sRGB=t.m_ColorSpace==1,filter=t.m_TextureSettings.m_FilterMode,wrapU=t.m_TextureSettings.m_WrapU,wrapV=t.m_TextureSettings.m_WrapV,wrapW=t.m_TextureSettings.m_WrapW,aniso=t.m_TextureSettings.m_Aniso,mipBias=t.m_TextureSettings.m_MipBias,readable=t.m_IsReadable))
        elif p.suffix in ['.asset','.mat','.prefab']:raw=remap(raw.decode('utf-8-sig').replace('\r\n','\n')).encode()
        else:raise ValueError('Unexpected source dependency '+str(relative))
        write(target,raw);write(Path(str(target)+'.meta'),remap(re.sub(r'(?m)^timeCreated: \d+\n','',metadata)).encode());outputs.append(dict(**record(target),meta_sha256=sha(Path(str(target)+'.meta').read_bytes())))
    for folder in [DEST,*sorted(p for p in DEST.rglob('*') if p.is_dir())]:meta(folder,folder=True)
    write(RECEIPT,encode(dict(format=1,mapClassId=13,manifest=apk.version,root=source_path(DEST/root.relative_to(base)),inputs=inputs['inputs'],converter=dict(version=proof['tool']['version'],sha256=proof['tool']['sha256']),converter_input_receipt_sha256=sha(original_receipt.read_bytes()),export_proof_sha256=sha(proof_path.read_bytes()),exported_inputs=exported,script_bindings=bindings,textures=texture_records,outputs=outputs,tools=[record(p) for p in [Path(__file__).resolve(),ROOT/'scripts/prepare_unity_map13_minimap_inputs.py',ROOT/'scripts/prepare_unity_map13_native_scene.py']],serialization_reference=record(template_path),operation='Restore the complete original MiniMapSettings asset and every referenced Tile, Sprite, dashed-line prefab, material and texture. Rebind both actual migrated scripts; preserve every original setting, tile-selection row, field and prefab component. Decode the complete original ASTC mip chain using the matching color profile without flipping, filtering or resampling. Built-in native Tile and sprite shader references remain original Unity built-in identities.',limitations=['Unity import, settings/tile lookup, original dashed mesh rendering, full-screen input/camera and formal root bindings/save/Windows Player remain required.'])))
    print('Original MiniMapSettings restored:',len(outputs),'outputs,',len(fields['Settings']),'camera settings,',len(fields['TileSettings']),'tile configurations',flush=True)
    apk.archive.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path,required=True)
    parser.add_argument('--prepare-inputs',action='store_true');parser.add_argument('--convert',action='store_true');options=parser.parse_args()
    cache=options.cache.resolve()
    if options.prepare_inputs:
        from prepare_unity_map13_minimap_inputs import build as prepare
        prepare(cache,BUNDLE)
    if options.convert:
        from prepare_unity_map13_water_entity_export import convert,build as verify
        inputs=json.loads((cache/'source-manifest.json').read_bytes());write(cache/'export-input-receipt.json',encode(dict(inputs,outputs=[])));convert(cache)
        verify(receipt_path=cache/'export-input-receipt.json',component_path=cache/'source-manifest.json',roots=[BUNDLE],cache_root=cache,proof_path=cache/'export-proof.json',expected_counts=(1,1),entry_path=Path(__file__).resolve(),subject='MiniMapSettings')
    build(cache)
