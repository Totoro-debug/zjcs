"""Recover exact default weapon model, UI prefab metadata and actual page bindings."""
import copy
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import Apk
from official_manifest import read_asset_manifest
from convert_official_skeletons import reader_type, WHEEL_SHA256
from map13_native_schemas import NativeSchemas, fields_tree, verify_fields
from map13_entity_native_schemas import install
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record, sample, f32
from prepare_unity_profession_world_inheritance import world
from prepare_unity_profession_skeleton_data import string, block
from prepare_native_character_geometry import atlas_regions
from prepare_unity_profession_character_resources import asset, guid, meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
from prepare_unity_map13_entity_shaders import program_body
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map13_environment_shader import render_state
from prepare_unity_map13_pipeline_shaders import program_structure
from UnityPy.classes import PPtr
from UnityPy.enums import TextureFormat
import yaml


def build():
    apk = Apk.current()
    if apk.version != '871_156598': raise ValueError('Unexpected original weapon version')
    code = ROOT/'.tools/source-packages/current/871_156598/official-code'
    code_manifest=code.parent/'code-manifest.json';code_receipt=json.loads(code_manifest.read_bytes())
    assembly=code/'managed/decoded/Assembly-CSharp.dll'
    if code_receipt['manifest']!=apk.version or record(assembly)['sha256']!=code_receipt['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Original weapon application assembly differs')
    schemas = NativeSchemas(code, unity_project=ROOT/'unity'); schema_proof = install(schemas, code)
    manifest_bytes = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    manifest = read_asset_manifest(manifest_bytes)
    by_guid = {r['guid']: r for r in manifest if r['guid']}
    by_path = {r['path']: r for r in manifest}
    cache = CACHE/'skeletons/primary-weapon'; cache.mkdir(parents=True, exist_ok=True)
    dest = ROOT/'unity/Assets/Official/ProfessionUI/WeaponResources'; dest.mkdir(parents=True,exist_ok=True);meta(dest,folder=True)
    outputs = [dest.with_name(dest.name+'.meta')]
    sources = []

    def source(obj, values=None):
        row = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        if values is not None: row['fields'] = values
        sources.append(row); return row

    def save_json(path, value):
        path.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8',newline='\n')
        identity=meta(path);outputs.extend((path,path.with_name(path.name+'.meta')));return dict(fileID=4900000,guid=identity,type=3)

    def pointer(owner, value): return PPtr(**value,assetsfile=owner.assets_file).deref()

    def read_component(obj):
        script=obj.read(check_read=False).m_Script.read()
        if script.m_ClassName == 'UIBloomPlayerElementPart':
            names=('BackParticles','SkeletonGraphic','FrontParticles','SpineStencilParts');verify_fields(script.m_ClassName,names)
            descriptions=[(n,'PPtr<MonoBehaviour>' if n=='SkeletonGraphic' else 'PPtr<MonoBehaviour>[]') for n in names]
            return obj.read_typetree(nodes=fields_tree(schemas.default,descriptions))
        return schemas.read(obj)[0]

    prefabs=[]; graphic_obj=None; graphic_fields=None
    for skin in ('pWeapon_100_G_007','pWeapon_200_G_007'):
        for suffix in ('_UI','_UI_Integrated'):
            path=f'Assets/Prefabs/Appearance/PlayerPart/{skin}/{skin}{suffix}.prefab'
            entry=by_path[path];root=apk.asset(entry['bundle'],path);nodes=[]
            def visit(obj,parent):
                nonlocal graphic_obj,graphic_fields
                go=obj.read();components=[];children=[]
                for ref in go.m_Component:
                    c=ref.component.deref();kind=c.type.name
                    if kind=='MonoBehaviour':
                        sc=c.read(check_read=False).m_Script.read();kind=sc.m_Namespace+'.'+sc.m_ClassName;value=read_component(c)
                        if sc.m_ClassName=='SkeletonGraphic':graphic_obj,graphic_fields=c,value
                    else:value=c.read_typetree()
                    components.append(dict(type=kind,**source(c,value)))
                    if c.type.name in ('Transform','RectTransform'):children.extend(c.read().m_Children)
                nodes.append(dict(parent=parent,name=go.m_Name,game_object=source(obj,obj.read_typetree()),components=components))
                for child in children:visit(child.read().m_GameObject.deref(),str(obj.path_id))
            visit(root,None)
            file=dest/(skin+suffix+'-source.json');binding=save_json(file,dict(path=path,guid=entry['guid'],nodes=nodes))
            prefabs.append(dict(Path=path,Guid=entry['guid'],Hierarchy=binding))

    model_obj=pointer(graphic_obj,graphic_fields['skeletonDataAsset']);model=schemas.read(model_obj)[0];source(model_obj,model)
    model_path=next(r for r in manifest if r['bundle']==apk.provenance(model_obj)['bundle'] and r['path'].endswith('/primaryWeapon_SkeletonData.asset'))
    raw_obj=pointer(model_obj,model['skeletonJSON']);raw=raw_obj.read().m_Script.encode('utf-8','surrogateescape');source(raw_obj)
    (cache/'primaryWeapon.skel.bytes').write_bytes(raw)
    reader=reader_type()[0]();skeleton=reader.convert(raw)
    if reader.inp.index!=len(raw):raise ValueError('Weapon skeleton binary not fully consumed')
    # This original model has six inline animations and no split-animation bundle.
    if any('pweapon_splitanimations' in n for n in apk.index):raise ValueError('Review newly present weapon split animation source')
    skeleton_bytes=json.dumps(skeleton,ensure_ascii=False,separators=(',',':'),allow_nan=False).encode()
    (cache/'skeleton.json').write_bytes(skeleton_bytes)
    atlas_obj=pointer(model_obj,model['atlasAssets'][0]);atlas=schemas.read(atlas_obj)[0];source(atlas_obj,atlas)
    atlas_text_obj=pointer(atlas_obj,atlas['atlasFile']);atlas_text=atlas_text_obj.read().m_Script;source(atlas_text_obj)
    (cache/'atlas.txt').write_text(atlas_text,encoding='utf-8',newline='\n')
    regions=atlas_regions(atlas_text);atlas_bytes=json.dumps(regions,ensure_ascii=False,separators=(',',':')).encode()
    payload=dest/'primary-weapon.bytes';payload.write_bytes(b'ZJCSPROF'+struct.pack('<I',1)+string(apk.version)+string('primary-weapon')+struct.pack('<f',model['scale'])+block(skeleton_bytes)+block(atlas_bytes));meta(payload);outputs.extend((payload,payload.with_name(payload.name+'.meta')))
    metadata=dest/'primary-weapon-original-metadata.json';metadata_binding=save_json(metadata,model)
    carrier=dest/'primary-weapon-skeleton.asset'
    carrier_binding=asset(carrier,model['m_Name'],ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonAsset.cs',dict(sourcePath=model_path['path'],sourceGuid=model_path['guid'],sex='primary-weapon',payload=dict(fileID=4900000,guid=guid(payload),type=3),originalMetadata=metadata_binding));outputs.extend((carrier,carrier.with_name(carrier.name+'.meta')))

    # Restore the original UI program, not the dummy Shader text export.
    ui_mat_obj=pointer(graphic_obj,graphic_fields['m_Material']);ui_mat=ui_mat_obj.read_typetree();source(ui_mat_obj,ui_mat)
    shader_obj=ui_mat_obj.read().m_Shader.deref();shader=shader_obj.read();source(shader_obj)
    if shader.m_ParsedForm.m_Name!='Spine/SkeletonGraphicHeightClip':raise ValueError('Original weapon UI shader differs')
    native,programs=translate(shader,'Profession/SkeletonGraphicHeightClip',program_body=program_body,program_structure=program_structure,render_state=render_state,target='4.5')
    shader_path=ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/SpineSkeletonGraphicHeightClip.shader'
    shader_path.write_text(native,encoding='utf-8',newline='\n');meta(shader_path);outputs.extend((shader_path,shader_path.with_name(shader_path.name+'.meta')))
    program_records=[]
    for p in programs:
        path=dest/('height-clip-'+str(p['index'])+'.glsl.txt')
        path.write_text(p['source'],encoding='utf-8',newline='\n');meta(path);outputs.extend((path,path.with_name(path.name+'.meta')))
        program_records.append(dict(index=p['index'],keywords=p['keywords'],file=record(path)))

    # AppearanceUILite.High loads this exact placeholder mapping through original GUIDs.
    loader_obj=next(o for o in apk.objects('appearanceassets_character_pweapon') if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Name=='primaryWeapon_TextureLoader_High')
    loader=schemas.read(loader_obj)[0];source(loader_obj,loader)
    required={r['page'] for r in regions if r['name'] in ('pWeapon_100_007/W_jian_07','pWeapon_200_007/W_zhang_07')}
    if len(required)!=2:raise ValueError('Original default weapon pages differ')
    pages=[];textures=[]
    def export_texture(obj,role):
        t=obj.read();original=obj.read_typetree();original.pop('image data');source(obj,original)
        if t.m_MipsStripped or t.m_ImageCount!=1 or t.m_TextureDimension!=2:raise ValueError('Unexpected weapon texture topology')
        if 'ASTC' in TextureFormat(t.m_TextureFormat).name:pixels,mips,profile=decode_mips(t);fmt=4
        else:
            pixels=t.get_image_data();fmt=int(t.m_TextureFormat);profile='original-uncompressed';mips=[];offset=0
            if fmt not in (1,4):raise ValueError('Unexpected original uncompressed weapon texture format')
            for level in range(t.m_MipCount):
                w,h=max(1,t.m_Width>>level),max(1,t.m_Height>>level);size=w*h*(1 if fmt==1 else 4)
                mips.append(dict(level=level,width=w,height=h,rgba_offset=offset,rgba_size=size,rgba_sha256=hashlib.sha256(pixels[offset:offset+size]).hexdigest()));offset+=size
            if offset!=len(pixels):raise ValueError('Weapon texture mips not fully consumed')
        n=source_header();n.update(fields(copy.deepcopy(original)));n.update(serializedVersion=2,m_TextureFormat=fmt,m_CompleteImageSize=len(pixels),m_PlatformBlob='',m_StreamData=dict(serializedVersion=2,offset=0,size=0,path=''));n['image data']=len(pixels);n['_typelessdata']=pixels.hex()
        path=dest/(t.m_Name+'.'+role+'.texture2d.asset');path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n'+yaml.dump({'Texture2D':n},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n');identity=meta(path,native=True)
        # Texture native importer uses the texture object's fileID.
        path.with_name(path.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+identity+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n',encoding='utf-8',newline='\n')
        outputs.extend((path,path.with_name(path.name+'.meta')));textures.append(dict(role=role,output=record(path),mips=mips,profile=profile,source=apk.provenance(obj),encoded_sha256=hashlib.sha256(t.get_image_data()).hexdigest(),original_fields=original))
        return dict(fileID=2800000,guid=identity,type=2)
    for mapping in loader['placeholderMap']:
        placeholder=pointer(loader_obj,mapping['placeholderTexture']);page=placeholder.read().m_Name.removesuffix('_placeholder')
        if page not in required:continue
        placeholder_ref=export_texture(placeholder,'placeholder');bindings={};ids={}
        for role,key in (('main','targetTextureReference'),('dye','dyeTextureReference')):
            identity=mapping[key]['AssetReference']['mAssetGuid'];ids[role]=identity
            if not identity:bindings[role]=dict(fileID=0);continue
            row=by_guid[identity];bindings[role]=export_texture(apk.asset(row['bundle'],row['path']),role)
        original_ui=copy.deepcopy(ui_mat);original_ui.pop('m_Shader');material=source_header();material['serializedVersion']=8;material.update(fields(original_ui));
        for key,values in material['m_SavedProperties'].items(): material['m_SavedProperties'][key]=[{pair[0]:pair[1]} for pair in values]
        material['serializedVersion']=8;material['m_Shader']=dict(fileID=4800000,guid=guid(shader_path),type=3)
        material['m_SavedProperties']={'serializedVersion':3,**material['m_SavedProperties']}
        material['m_SavedProperties']['m_TexEnvs'][0]['_MainTex']['m_Texture']=bindings['main']
        path=dest/(page+'.mat');path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\n'+yaml.dump({'Material':material},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n');identity=meta(path,native=True);path.with_name(path.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+identity+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n',encoding='utf-8',newline='\n');outputs.extend((path,path.with_name(path.name+'.meta')))
        pages.append(dict(Name=page,MainGuid=ids['main'],DyeGuid=ids['dye'],Placeholder=placeholder_ref,Main=bindings['main'],Dye=bindings['dye'],Material=dict(fileID=2100000,guid=identity,type=2)))
    if len(pages)!=2 or {p['Name'] for p in pages}!=required:raise ValueError('Original weapon high texture mappings incomplete')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeWeaponCatalog.cs'
    if not runtime.with_name(runtime.name+'.meta').exists():meta(runtime)
    catalog=ROOT/'unity/Assets/Game/Resources/ProfessionUI/weapon-resources.asset';asset(catalog,'Original default weapon source resources',runtime,dict(model=carrier_binding,pages=pages,prefabs=prefabs));outputs.extend((catalog,catalog.with_name(catalog.name+'.meta')))
    fixture=references(skeleton,textures,prefabs,model_path)
    fixture_path=CACHE/'typed/native-weapon-resources-validation.json';fixture_path.write_text(json.dumps(fixture,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeWeaponResourceValidation.cs'
    if not validation.with_name(validation.name+'.meta').exists():meta(validation)
    proof=dict(format=1,manifest=apk.version,code_manifest=record(code_manifest),application_assembly=record(assembly),asset_table_sha256=hashlib.sha256(manifest_bytes).hexdigest(),sources=sources,schema=schema_proof,
        decoder=dict(wheel_sha256=WHEEL_SHA256,license='MIT',implementation=record(ROOT/'scripts/convert_official_skeletons.py'),consumed_bytes=len(raw)),
        source_cache=[record(cache/n) for n in ('primaryWeapon.skel.bytes','skeleton.json','atlas.txt')],
        counts={k:len(skeleton.get(k,[])) for k in ('bones','slots','skins','animations','ik','transform','physics')},
        textures=textures,programs=program_records,outputs=[record(p) for p in outputs],fixture=record(fixture_path),
        application=[record(code/'client-source'/n) for n in ('AppearanceSystem/AppearanceUILite.cs','UIBloomPlayerElementPart.cs','UIParticleFlipFix.cs')],
        runtime=[record(p) for p in (runtime,runtime.with_name(runtime.name+'.meta'),ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs',ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAppearanceSkeleton.cs',ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeCurve.cs')],
        generator=record(Path(__file__)),validation=[record(p) for p in (validation,validation.with_name(validation.name+'.meta'))],
        conversion_tools=[record(ROOT/'scripts'/(name+'.py')) for name in ('official_shader_programs','prepare_unity_map13_entity_shaders','prepare_unity_map13_water_shaders','prepare_unity_map13_environment_shader','prepare_unity_map13_pipeline_shaders','prepare_battle_hud_materials','prepare_unity_map13_native_scene','prepare_native_character_geometry','prepare_unity_profession_character_resources','prepare_unity_profession_world_inheritance','prepare_unity_profession_native_curves')],
        operation='APK-first current original default sword/staff standalone/integrated UI source hierarchies and shared model. Fully decode original inline binary, retain every bone/skin/attachment/constraint/animation and atlas region; native carrier retains complete metadata. Resolve actual high texture GUIDs through original AppearanceUILite loader; preserve placeholders and original texture settings/mips. Translate original executable GLES height-clip shader and original UI material values to Windows. Source hierarchy metadata is retained for subsequent prefab/component conversion, not instantiated as a substitute UI.',
        limitations=['Only two default weapon pages are packaged; other skins remain in the lossless data but unavailable pages fail explicitly.',
            'Default skins have no active constraints; constrained weapon skins, physics/mixing, production SkeletonGraphic/AppearanceUILite and full original weapon prefab/part pool still require conversion.',
            'Complete profession page/input/save and Windows release remain incomplete.'],validated_scope='Pending actual Direct3D Play Mode data, all six default-weapon animation/geometry cases and source texture/shader validation.')
    proof['native_curve_precision']='Retain Unity weighted key representation and the original absolute handles. Evaluate cubic time inversion directly in double precision without resampling/baking: a real weapon translation at t=3.2 differed by 0.00413 source units with AnimationCurve.Evaluate and failed the existing independent tolerance. Source key records are copied so callers cannot mutate the exact sampler after construction. Linear/stepped native evaluation and original boundary non-application remain unchanged; character-curve/application-graph regressions are required.'
    path=ROOT/'assets/reference/profession-ui/weapon-resources-source-manifest.json';path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Recovered original default weapon resources:',proof['counts'],len(textures),'texture payloads',len(programs),'shader variants')
    return proof


def references(skeleton,textures,prefabs,model_path):
    indices={b['name']:i for i,b in enumerate(skeleton['bones'])};modes=dict(normal=0,onlyTranslation=1,noRotationOrReflection=2,noScale=3,noScaleOrReflection=4)
    bones=[]
    for b in skeleton['bones']:
        row=dict(name=b['name'],parent=indices[b['parent']] if 'parent'in b else -1,inherit=modes[b.get('inherit','normal')],skinRequired=b.get('skin',False))
        for key in ('x','y','rotation','scaleX','scaleY','shearX','shearY'):row[key]=b.get(key,1 if key in ('scaleX','scaleY') else 0)
        bones.append(row)
    active={i for i,b in enumerate(bones) if not b['skinRequired']};frames=[]
    for name,a in skeleton['animations'].items():
        for time in (-.001,0,.17,.5,1,2,3.2,4):
            local=[dict(b) for b in bones]
            for bone,channels in a.get('bones',{}).items():
                for channel,values in channels.items():
                    axes={'rotate':['value'],'translate':['x','y'],'scale':['x','y']}[channel]
                    for axis,field in zip(axes,{'rotate':['rotation'],'translate':['x','y'],'scale':['scaleX','scaleY']}[channel]):
                        keys=[]
                        for value in values:
                            curve=value.get('curve');key=dict(time=value.get('time',0),value=value.get(axis,1 if channel=='scale' else 0),mode=2 if isinstance(curve,list) else 1 if curve=='stepped' else 0)
                            if isinstance(curve,list):key['controls']=dict(zip(('x','y','z','w'),curve[axes.index(axis)*4:axes.index(axis)*4+4]))
                            keys.append(key)
                        v=sample(keys,f32(time))
                        if v['applies']:
                            i=indices[bone];local[i][field]=f32(bones[i][field]*v['value'] if channel=='scale' else bones[i][field]+v['value'])
            frames.append(dict(animation=name,time=f32(time),expected=world(bones,local,active)))
    return dict(frames=frames,modelPath=model_path['path'],modelGuid=model_path['guid'],prefabs=prefabs,
                textures=[dict(path=t['output']['path'].removeprefix('unity/'),mips=t['mips']) for t in textures],
                counts={k:len(skeleton.get(k,[])) for k in ('bones','slots','skins','animations','ik','transform','physics')})


if __name__=='__main__':build()
