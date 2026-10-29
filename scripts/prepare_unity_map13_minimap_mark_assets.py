"""Restore the actual Map13 original minimap marker prefabs and native assets."""
import argparse,copy,hashlib,io,json,re
from pathlib import Path
import yaml
from PIL import Image
from official_apk import Apk,ROOT
from prepare_unity_bundle_inputs import collect,serialized_files
ROOT_BUNDLE="defaultpackage_assets_prefabs_ui_map_minimap_minimapmark.bundle"
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_scene import decode_mips,native_texture
from prepare_unity_map13_native_sprite_yaml import Dumper,fields,source_header
from prepare_unity_map13_sources import encode,meta,sha,source_path,write
from prepare_unity_map13_water_entity_export import native_io_path
from UnityPy.helpers.MeshHelper import MeshHandler
from UnityPy.enums import TextureFormat

DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapMarks'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')
def select_markers():
    resources=ROOT/'unity/Assets/Game/Resources/Map13'
    paths=[resources/'entity-presentation-reference.json',resources/'entity-presentation.json',ROOT/'unity/Assets/Official/World/Map/13/Source/tables.json']
    reference,presentation,tables=[json.loads(p.read_bytes()) for p in paths]
    extends={row['EntityClassId']:row for row in tables['Map/entity_extend_parameter']}
    counts={}
    for case in reference['Cases']:
        ui=presentation['UiDisplays'][case['Ui']] if case['Ui']>=0 else None
        name=(ui or {}).get('MiniMapMark') or extends[case['EntityClassId']]['MiniMapMark']
        if name:counts[name]=counts.get(name,0)+1
    return dict(roots=['Prefabs/UI/Map/MiniMap/MiniMapMark/'+name+'.prefab' for name in sorted(counts)],placements_by_marker=counts,selection_inputs=[record(p) for p in paths])


def identity(relative):return hashlib.md5(('zjcs-map13-minimap-marks:'+str(relative)).encode()).hexdigest()
def native_meta(guid,fileid):return 'fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(fileid)+'\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'

def original_mips(texture):
    if TextureFormat(texture.m_TextureFormat).name!='RGBA32':return decode_mips(texture)
    raw=texture.get_image_data();offset=0;records=[]
    for level in range(texture.m_MipCount):
        width,height=max(1,texture.m_Width>>level),max(1,texture.m_Height>>level);length=width*height*4;block=raw[offset:offset+length]
        if len(block)!=length:raise ValueError('Truncated original RGBA32 mip')
        records.append(dict(level=level,width=width,height=height,source_format='RGBA32',rgba_offset=offset,rgba_size=length,rgba_sha256=sha(block)));offset+=length
    if offset!=len(raw):raise ValueError('Unconsumed original RGBA32 mip data')
    return raw,records,'OriginalRGBA32'


def build(cache):
    cache=cache.resolve()
    selection=select_markers();ROOTS=selection['roots']
    if len(ROOTS)!=41:raise ValueError('Original Map13 placement-derived marker selection required')
    if not cache.is_relative_to(ROOT/'output/validation'):raise ValueError('Owned validation cache required')
    inputs=json.loads((cache/'source-manifest.json').read_bytes());proof=json.loads((cache/'export-proof.json').read_bytes())
    if inputs['manifest']!='871_156598' or inputs['roots']!=[ROOT_BUNDLE] or proof['counts']['registries']!=175:raise ValueError('Verified original minimap export required')
    for entry in inputs['inputs']:
        if sha(native_io_path(cache/'inputs'/entry['output']).read_bytes())!=entry['converter_input_sha256']:raise ValueError('Converter input changed')
    restored={}
    for entry in proof['outputs']:
        original=ROOT/entry['input'];replacement=ROOT/entry['path']
        if sha(native_io_path(original).read_bytes())!=entry['input_sha256'] or sha(native_io_path(replacement).read_bytes())!=entry['sha256']:raise ValueError('Verified original managed-reference restoration changed')
        restored[original]=replacement
    base=cache/'export/ExportedProject/Assets';index={}
    for p in base.rglob('*.meta'):
        m=GUID.search(native_io_path(p).read_text(encoding='utf-8-sig'))
        if m:
            if m[1] in index:raise ValueError('Duplicate export GUID')
            index[m[1]]=Path(str(p)[:-5])
    old_guid=lambda p:GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]
    targets={};bindings=[];reused=[]
    def redirect(p,target):
        if not target.is_file():raise FileNotFoundError(target)
        targets[old_guid(p)]=GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
        bindings.append(dict(original=p.relative_to(base).as_posix(),**record(target),meta_sha256=sha(Path(str(target)+'.meta').read_bytes()),guid=targets[old_guid(p)]))
    # Reuse the selected official font and translated shader. The dynamic atlas is
    # writable during text population; its original bytes are recorded in its own receipt.
    font_proof_path=ROOT/'assets/reference/official-map/map13-native-text-font-source-manifest.json';font_proof=json.loads(font_proof_path.read_bytes())
    for entry in font_proof['outputs']:
        p=ROOT/entry['path'];dynamic='Atlas.texture.asset' in p.name
        if not dynamic and sha(p.read_bytes())!=entry['sha256']:raise ValueError('Selected official font changed: '+str(p))
        if sha(Path(str(p)+'.meta').read_bytes())!=entry['meta_sha256']:raise ValueError('Selected font binding changed')
        reused.append(dict(path=entry['path'],original_native_sha256=entry['sha256'],dynamic_atlas=dynamic,meta_sha256=entry['meta_sha256']))
    original_font=base/'TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset';font_text=native_io_path(original_font).read_text(encoding='utf-8-sig')
    redirect(original_font,ROOT/'unity/Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset')
    redirect(index[re.search(r'(?m)^  material: \{fileID: 2100000, guid: (\w+)',font_text)[1]],ROOT/'unity/Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.mat')
    code_base=ROOT/'unity/Assets/Game/Runtime/Official/World'
    scripts={name:code_base/folder/(name+'.cs') for folder,names in [
        ('Map13MiniMapMarks',['MiniMapOnlyMarkUI','MiniMapWayPointMarkUI','MiniMapEntryMarkUI']),
        ('Map13MiniMapMarkSupports',['MapEntityTipDotNodeLinker','MiniMapActiveByNPCVisibleSupporter','MiniMapActiveSwitcherSupport','MiniMapPeriodicitySwitcherSupport','MiniMapWorldStageSwitcherSupport','MinimapMarkKeepSizeObjSupport','MinimapMarkTipDotLinkerSupport']),
        ('NativeLabelUI',['MultiLanguageText'])] for name in names}
    pending=[base/p for p in ROOTS];found=set()
    while pending:
        p=pending.pop()
        if old_guid(p) in targets or p in found:continue
        if p.suffix=='.cs':
            target=scripts.get(p.stem)
            if target is None:
                if p.stem in ['TextMeshPro','TextMeshProUGUI','TMP_FontAsset']:package,relative='com.unity.textmeshpro','Scripts/Runtime/'+p.stem+'.cs'
                else:
                    package='com.unity.ugui'
                    layout=['ContentSizeFitter','HorizontalLayoutGroup','LayoutElement','VerticalLayoutGroup']
                    relative='Runtime/UI/Core/'+('Layout/' if p.stem in layout else 'VertexModifiers/' if p.stem=='Shadow' else '')+p.stem+'.cs'
                candidates=list((ROOT/'unity/Packages').glob(package+'*/'+relative))
                if not candidates:candidates=list((ROOT/'unity/Library/PackageCache').glob(package+'*/'+relative))
                if len(candidates)!=1:raise ValueError('Actual package script required: '+p.stem)
                target=candidates[0]
            redirect(p,target);continue
        found.add(p)
        if p.suffix=='.png':continue
        for q in [p,Path(str(p)+'.meta')]:
            for guid in GUID.findall(native_io_path(q).read_text(encoding='utf-8-sig')):
                if guid in index:pending.append(index[guid])
                elif not guid.startswith('0000000000000000'):raise ValueError('Unresolved original dependency '+guid)
    counts={ext:sum(p.suffix==ext for p in found) for ext in {p.suffix for p in found}}
    print('Selected native closure:',counts,flush=True)
    if counts!={'.prefab':41,'.png':14,'.anim':1,'.asset':61}:raise ValueError(counts)
    for p in found:targets[old_guid(p)]=identity(p.relative_to(base).as_posix())
    def remap(text):
        text=GUID.sub(lambda m:'guid: '+targets.get(m[1],m[1]),text)
        # Exact original managed-reference records, with only migrated type names qualified.
        for name,assembly in [('StateGameObjectConfig','NativeLabelUI'),('StateAlphaConfig','NativeLabelUI'),('StateGraphicColorConfig','NativeLabelUI')]:
            text=re.sub(r'(        class: '+name+r'\n        ns:)[^\n]*\n        asm: [^\n]*',r'\1 Zjcs.Official.World.'+assembly+'\n        asm: Zjcs.Official.World.'+assembly,text)
        # Original MapRegionDescHelper/limit helper types live in the label assembly.
        text=text.replace('        ns: \n        asm: Assembly-CSharp','        ns: Zjcs.Official.World.NativeMap13Labels\n        asm: Zjcs.Official.World.NativeMap13Labels')
        return text
    apk=Apk.current();_,missing=collect(apk,[ROOT_BUNDLE])
    if missing:raise ValueError(missing)
    texture_objects={};sprite_objects={}
    for bundle in apk.loaded.values():
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name=='Texture2D':
                    texture=obj.read();texture_objects.setdefault((texture.m_Name,texture.m_Width,texture.m_Height),[]).append((obj,texture))
                elif obj.type.name=='Sprite':sprite_objects.setdefault(obj.read().m_Name,[]).append(obj)
    outputs=[];exported=[];textures=[];sprites=[];texture_bindings={}
    def emit(p,raw,metadata):write(p,raw);write(Path(str(p)+'.meta'),metadata.encode());outputs.append(record(p))
    template=(ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset').read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
    for p in sorted(found):
        relative=p.relative_to(base).as_posix();raw=native_io_path(restored.get(p,p)).read_bytes();metadata=native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig').replace('\r\n','\n');exported.append(dict(relative=relative,export_sha256=sha(native_io_path(p).read_bytes()),native_input_sha256=sha(raw),managed_reference_restored=p in restored,meta_sha256=sha(native_io_path(Path(str(p)+'.meta')).read_bytes())));target=DEST/relative
        if p.suffix=='.png':
            size=Image.open(io.BytesIO(raw)).size;candidates=texture_objects.get((p.stem,*size),[])
            if len(candidates)!=1:raise ValueError('Original texture not unique: '+relative)
            obj,texture=candidates[0];rgba,mips,profile=original_mips(texture);target=target.with_name(target.stem+'.texture.asset');raw=native_texture(template,texture,rgba);metadata=native_meta(targets[old_guid(p)],2800000);texture_bindings[(texture.m_Name,*size)]=targets[old_guid(p)];textures.append(dict(source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),compressed_sha256=sha(texture.get_image_data()),rgba_sha256=sha(rgba),mips=mips,profile=profile))
        elif p.suffix=='.asset' and 'Wwise' not in p.parts:continue # Actual packed Sprite objects are restored below.
        else:raw=remap(raw.decode('utf-8-sig').replace('\r\n','\n')).encode();metadata=remap(metadata)
        emit(target,raw,metadata)
    for p in sorted(found):
        if p.suffix!='.asset' or 'Wwise' in p.parts:continue
        candidates=sprite_objects.get(p.stem,[])
        if len(candidates)!=1:raise ValueError('Unique original Sprite required '+p.stem)
        obj=candidates[0];sprite=obj.read();original=copy.deepcopy(obj.read_typetree(check_read=True));rd=original['m_RD'];atlas_record=None
        if sprite.m_SpriteAtlas.path_id:
            aobj=sprite.m_SpriteAtlas.deref();atlas=aobj.read();af=aobj.read_typetree(check_read=True);entry=next(v for k,v in af['m_RenderDataMap'] if k==original['m_RenderDataKey']);effective=next(v for k,v in atlas.m_RenderDataMap if k==sprite.m_RenderDataKey);tobj=effective.texture.deref();rd.update(copy.deepcopy(entry));original['m_SpriteAtlas']={'fileID':0};atlas_record=dict(source=apk.provenance(aobj),object_sha256=sha(aobj.get_raw_data()))
        else:tobj=sprite.m_RD.texture.deref()
        texture=tobj.read();tguid=texture_bindings[(texture.m_Name,texture.m_Width,texture.m_Height)];uv=copy.deepcopy(rd['uvTransform']);rd['texture']={'fileID':0};key,local_id=original['m_RenderDataKey'];original['m_RenderDataKey']={''.join(f"{key['data['+str(i)+']']:08x}"[::-1] for i in range(4)):local_id};original['m_Rect']['serializedVersion']=2;rd['serializedVersion']=3;rd['textureRect']['serializedVersion']=2
        for submesh in rd['m_SubMeshes']:submesh['serializedVersion']=2
        rd['m_IndexBuffer']=bytes(rd['m_IndexBuffer']).hex();vd=rd['m_VertexData'];vraw=bytes(vd['m_DataSize']);vd.update(serializedVersion=3,m_DataSize=len(vraw),_typelessdata=vraw.hex());native=source_header();native.update(fields(original));native['m_RD']['texture']=dict(fileID=2800000,guid=tguid,type=2)
        raw=('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n'+yaml.dump(dict(Sprite=native),Dumper=Dumper,sort_keys=False,allow_unicode=True)).encode();emit(DEST/p.relative_to(base),raw,native_meta(targets[old_guid(p)],21300000));mesh=MeshHandler(sprite.m_RD,obj.version);mesh.process();sprites.append(dict(path=(DEST/p.relative_to(base)).relative_to(ROOT).as_posix(),source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),atlas=atlas_record,vertices=[dict(x=v[0],y=v[1]) for v in mesh.m_Vertices],triangles=list(mesh.m_IndexBuffer),uv=[dict(x=(v[0]*uv['x']+uv['y'])/texture.m_Width,y=(v[1]*uv['z']+uv['w'])/texture.m_Height) for v in mesh.m_Vertices],rect={k:getattr(sprite.m_Rect,k) for k in ['x','y','width','height']},ppu=sprite.m_PixelsToUnits,pivot=dict(x=sprite.m_Pivot.x*sprite.m_Rect.width,y=sprite.m_Pivot.y*sprite.m_Rect.height)))
    # Native Texture2D references use type 2 after PNG conversion.
    texture_guids=set(texture_bindings.values())
    for entry in outputs:
        p=ROOT/entry['path']
        if p.suffix in ['.prefab','.mat']:
            raw=p.read_text(encoding='utf-8-sig');raw=re.sub(r'(\{fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3(\})',lambda m:m[1]+m[2]+m[3]+('2' if m[2] in texture_guids else '3')+m[4],raw);write(p,raw.encode());entry.update(record(p))
    for folder in [DEST,*sorted(p for p in DEST.rglob('*') if p.is_dir())]:meta(folder,folder=True)
    write(ROOT/'assets/reference/official-map/map13-native-minimap-marks-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest=apk.version,bundle_inputs=inputs['inputs'],converter_input_receipt_sha256=sha((cache/'source-manifest.json').read_bytes()),export_proof_sha256=sha((cache/'export-proof.json').read_bytes()),roots=ROOTS,placements_by_marker=selection['placements_by_marker'],selection_inputs=selection['selection_inputs'],exported_inputs=exported,script_bindings=bindings,reused_font=reused,font_proof=record(font_proof_path),textures=textures,sprites=sprites,outputs=outputs,generator=record(Path(__file__).resolve()),tools=[record(ROOT/'scripts/prepare_unity_map13_native_sprite_yaml.py'),record(ROOT/'scripts/prepare_unity_map13_native_scene.py')],operation='Restore the 41 complete original Map13 placement-selected minimap marker prefabs and all native dependencies. Retain every original hierarchy, field, local object ID, font/effect, Sprite logical geometry/UV/pivot and original ASTC mips. Resolve packed SpriteAtlas render data into explicit native texture bindings; bind all migrated original components and actual TMP/UGUI packages. Reuse selected official native font/TTF/material and original shader. No new UI/art/layout.',limitations=['Actual native UI/root/local-state binding, render/input/save and Windows Player remain required.'])))
    print('Original minimap markers:',len(outputs),'outputs;',len(sprites),'Sprites;',len(bindings),'explicit script/font bindings',flush=True);apk.archive.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path,required=True);build(parser.parse_args().cache)
