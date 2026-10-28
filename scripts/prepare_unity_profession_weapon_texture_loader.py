"""Recover the complete original weapon placeholder atlas/high-loader asset graph."""
import copy
import hashlib
import json
import re
from pathlib import Path
import yaml
from official_apk import Apk
from official_platform_shader_programs import platform_shader
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas, fields_tree, verify_fields
from map13_entity_native_schemas import install
from UnityPy.classes import PPtr
from UnityPy.enums import TextureFormat
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_character_resources import asset, guid, meta
from prepare_unity_profession_native_curves import record
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map13_entity_shaders import program_body, referenced_programs
from prepare_unity_map13_pipeline_shaders import program_structure
from prepare_unity_map13_environment_shader import render_state


def build():
    apk=Apk.current()
    if apk.version!='871_156598':raise ValueError('Unexpected weapon loader version')
    code=ROOT/'.tools/source-packages/current/871_156598/official-code'
    receipt=code.parent/'code-manifest.json';assembly=code/'managed/decoded/Assembly-CSharp.dll';receipt_data=json.loads(receipt.read_bytes())
    if receipt_data['manifest']!=apk.version or record(assembly)['sha256']!=receipt_data['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:raise ValueError('Original loader application assembly changed')
    schemas=NativeSchemas(code,unity_project=ROOT/'unity');schema=install(schemas,code)
    raw_table=apk.source.manifest_bytes('DefaultPackage',apk.archive);table={r['path']:r for r in read_asset_manifest(raw_table)}
    dest=ROOT/'unity/Assets/Official/ProfessionUI/WeaponTextureLoader';dest.mkdir(parents=True,exist_ok=True);meta(dest,folder=True)
    outputs=[dest.with_name(dest.name+'.meta')];sources=[];texture_records=[];material_records=[];shader_records=[]
    shader_receipt=ROOT/'assets/reference/official-map/map13-entity-shaders-source-manifest.json';shader_data=json.loads(shader_receipt.read_bytes())
    shaders={i['original']['payload_sha256']:(i,o) for i,o in zip(shader_data['inputs'],shader_data['outputs'])}
    bound_receipt=ROOT/'assets/reference/official-map/map13-entity-shader-bindings-source-manifest.json';bound_data=json.loads(bound_receipt.read_bytes())
    bound_catalog=json.loads((ROOT/'unity/Assets/Official/World/Map/13/Source/native-entity-shader-bindings.json').read_bytes())
    bound_names={r['original_name']:r for r in bound_catalog['bindings']}
    bound_shaders={r['original']['payload_sha256']:bound_names[r['original']['name']] for r in bound_data['converted']+bound_data['reused']}
    bound_outputs={r['path']:r for r in bound_data['outputs']}
    native_runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI';official_runtime=ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'
    runtime=[native_runtime/(name+'.cs') for name in ('ProfessionNativeTextureAtlas','ProfessionNativeWeaponTextureResources','OfflineProfessionWeaponTextureResources')]
    for path in runtime:meta(path)
    def source(obj,values=None):
        value=dict(source=apk.provenance(obj),payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        if values is not None:value['fields']=values
        sources.append(value);return value
    def pointer(owner,value):return PPtr(**value,assetsfile=owner.assets_file).deref() if value['m_PathID'] else None
    def identity(obj):return (obj.assets_file.name,str(obj.path_id))
    def ref(path,file_id):return dict(fileID=file_id,guid=guid(path),type=2)
    def json_asset(path,value):
        path.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8',newline='\n');meta(path);outputs.extend((path,path.with_name(path.name+'.meta')));return dict(fileID=4900000,guid=guid(path),type=3)
    def native(path,kind,file_id,values):
        path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!'+str(kind)+' &'+str(file_id)+'\n'+yaml.dump({{21:'Material',28:'Texture2D'}[kind]:values},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n')
        identity=meta(path,native=True);path.with_name(path.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+identity+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(file_id)+'\n',encoding='utf-8',newline='\n');outputs.extend((path,path.with_name(path.name+'.meta')));return ref(path,file_id)
    texture_cache={};shader_cache={}
    previous=json.loads((ROOT/'assets/reference/profession-ui/weapon-resources-source-manifest.json').read_bytes())
    old_placeholders={r['original_fields']['m_Name']:r for r in previous['textures'] if r['role']=='placeholder'}
    def texture(obj):
        key=identity(obj)
        if key in texture_cache:return texture_cache[key]
        if obj.type.name!='Texture2D':raise ValueError('Verify non-Texture2D original weapon binding')
        t=obj.read();original=obj.read_typetree();original.pop('image data');source(obj,original)
        encoded=t.get_image_data();encoded_sha=hashlib.sha256(encoded).hexdigest()
        if t.m_MipsStripped or t.m_ImageCount!=1 or t.m_TextureDimension!=2:raise ValueError('Unexpected original placeholder topology')
        if 'ASTC' in TextureFormat(t.m_TextureFormat).name:pixels,mips,profile=decode_mips(t);fmt=4;mips=[dict(level=m['level'],width=m['width'],height=m['height'],size=m['rgba_size'],sha256=m['rgba_sha256']) for m in mips]
        else:
            fmt=int(t.m_TextureFormat);pixels=encoded;profile='original-uncompressed';mips=[];offset=0
            if fmt not in (1,4):raise ValueError('Unexpected weapon placeholder native format '+str(fmt))
            for level in range(t.m_MipCount):
                w,h=max(1,t.m_Width>>level),max(1,t.m_Height>>level);size=w*h*(1 if fmt==1 else 4)
                mips.append(dict(level=level,width=w,height=h,size=size,sha256=hashlib.sha256(pixels[offset:offset+size]).hexdigest()));offset+=size
            if offset!=len(pixels):raise ValueError('Placeholder data not fully consumed')
        if t.m_Name in old_placeholders:
            old=old_placeholders[t.m_Name];path=ROOT/old['output']['path']
            if record(path)!=old['output'] or encoded_sha!=old['encoded_sha256']:raise ValueError('Previously converted default weapon placeholder differs')
            binding=ref(path,2800000);reused=True
        else:
            path=dest/(t.m_Name+'.texture2d.asset');n=source_header();n.update(fields(copy.deepcopy(original)));n.update(serializedVersion=2,m_TextureFormat=fmt,m_CompleteImageSize=len(pixels),m_PlatformBlob='',m_StreamData=dict(serializedVersion=2,offset=0,size=0,path=''));n['image data']=len(pixels);n['_typelessdata']=pixels.hex();binding=native(path,28,2800000,n);reused=False
        texture_records.append(dict(source=apk.provenance(obj),encoded_sha256=encoded_sha,original_fields=original,output=record(path),mips=mips,reused=reused,profile=profile));texture_cache[key]=binding;return binding
    def shader(obj):
        key=identity(obj)
        if key in shader_cache:return shader_cache[key]
        digest=hashlib.sha256(obj.get_raw_data()).hexdigest();parsed=obj.read();form=parsed.m_ParsedForm;name=form.m_Name
        source(obj);programs=[];reused=None
        print('Binding original shader',name,digest,flush=True)
        if digest in bound_shaders:
            item=bound_shaders[digest];path=ROOT/'unity'/item['path']
            if item['original_name']!=name or record(path)['sha256']!=bound_outputs[path.relative_to(ROOT).as_posix()]['sha256']:raise ValueError('Bound original shader graph differs')
            reused=record(bound_receipt)
        else:
            if digest in shaders:
                original,native_shader=shaders[digest];path=ROOT/native_shader['path']
                if record(path)['sha256']!=native_shader['sha256'] or original['original']['name']!=name:raise ValueError('Original/native shader receipt mismatch')
                text=path.read_text(encoding='utf-8');reused=record(shader_receipt)
            else:
                parsed=platform_shader(parsed,9,{4});form=parsed.m_ParsedForm
                if len(form.m_SubShaders)!=1:raise ValueError('Review original weapon config subshader selection')
                passes=[];header=None
                for pi,source_pass in enumerate(form.m_SubShaders[0].m_Passes):
                    if source_pass.m_Type!=0:raise ValueError('Review original weapon config pass kind')
                    if not referenced_programs(source_pass):
                        passes.append('Pass {\n'+render_state(source_pass.m_State)+'\n}\n');continue
                    sub=copy.copy(form.m_SubShaders[0]);sub.m_Passes=[source_pass];one=copy.copy(parsed);one.m_ParsedForm=copy.copy(form);one.m_ParsedForm.m_SubShaders=[sub]
                    text,variants=translate(one,'Profession/WeaponLoaderSkeleton',program_body=program_body,program_structure=program_structure,render_state=render_state,target='4.5')
                    for keyword,flags in zip(form.m_KeywordNames,form.m_KeywordFlags):
                        if flags not in (0,1):raise ValueError('Unknown original shader keyword scope')
                        if flags==1:text=text.replace('#pragma multi_compile __ '+keyword+'\n','#pragma multi_compile_local __ '+keyword+'\n')
                    begin=text.index('Pass {\n');end=text.index('ENDCG\n}\n')+len('ENDCG\n}\n');head=text[:begin]
                    if sub.m_LOD:head=head.replace('SubShader {\n','SubShader {\nLOD '+str(sub.m_LOD)+'\n',1)
                    if header is not None and header!=head:raise ValueError('Original shader pass headers differ')
                    header=head;passes.append(text[begin:end])
                    for program in variants:
                        file=dest/(('config-skeleton' if name=='Universal Render Pipeline/Spine/Skeleton' else 'fallback-'+hashlib.sha256(name.encode()).hexdigest()[:8])+'-p'+str(pi)+'-'+str(program['index'])+'.glsl.txt');file.write_text(program['source'],encoding='utf-8',newline='\n');meta(file);outputs.extend((file,file.with_name(file.name+'.meta')))
                        programs.append(dict(pass_index=pi,index=program['index'],keywords=program['keywords'],file=record(file)))
                text=header+''.join(passes)+'}\nFallback Off\n}\n'
            # Preserve actual fallback references through the original dependency objects.
            if form.m_FallbackName:
                candidates=[r.deref() for r in parsed.m_Dependencies if r.m_PathID]
                candidates=[o for o in candidates if o.read().m_ParsedForm.m_Name==form.m_FallbackName]
                if len(candidates)!=1:raise ValueError('Original weapon shader fallback dependency is not unique: '+form.m_FallbackName)
                fallback_ref=shader(candidates[0]);fallback_path=next(r['output']['path'] for r in shader_records if r['output']['path'] and guid(ROOT/r['output']['path'])==fallback_ref['guid'])
                fallback_name=re.search(r'^Shader "([^"]+)"',(ROOT/fallback_path).read_text(encoding='utf-8'))[1]
                text=text.replace('\nFallback Off\n','\nFallback "'+fallback_name+'"\n')
            if programs or form.m_FallbackName:
                target='WeaponLoader'+re.sub('[^A-Za-z0-9]','',name)+'_'+hashlib.sha256(name.encode()).hexdigest()[:8]
                text=re.sub(r'^Shader "[^"]+"','Shader "Zjcs/Official/ProfessionUI/'+target+'"',text)
                path=official_runtime/(target+'.shader');path.write_text(text,encoding='utf-8',newline='\n');meta(path);outputs.extend((path,path.with_name(path.name+'.meta')))
        shader_records.append(dict(source=apk.provenance(obj),payload_sha256=digest,name=name,fallback=form.m_FallbackName,output=record(path),receipt=reused,programs=programs))
        binding=dict(fileID=4800000,guid=guid(path),type=3);shader_cache[key]=binding;return binding
    def material(obj):
        original=obj.read_typetree();source(obj,original);value=copy.deepcopy(original);value['m_Shader']=shader(pointer(obj,original['m_Shader']))
        for name,entry in value['m_SavedProperties']['m_TexEnvs']:
            target=pointer(obj,entry['m_Texture']);entry['m_Texture']=texture(target) if target is not None else dict(fileID=0)
        n=source_header();n.update(fields(value));n['serializedVersion']=8
        for key,entries in n['m_SavedProperties'].items():n['m_SavedProperties'][key]=[{pair[0]:pair[1]} for pair in entries]
        n['m_SavedProperties']={'serializedVersion':3,**n['m_SavedProperties']}
        path=dest/(original['m_Name']+'.mat');binding=native(path,21,2100000,n)
        material_records.append(dict(source=apk.provenance(obj),original_fields=original,output=record(path)));return binding
    loader_path='Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_TextureLoader_High.asset'
    loader_obj=apk.asset(table[loader_path]['bundle'],loader_path);loader=schemas.read(loader_obj)[0];source(loader_obj,loader)
    atlas_obj=pointer(loader_obj,loader['atlasAsset']);atlas=schemas.read(atlas_obj)[0];source(atlas_obj,atlas)
    atlas_path='Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_Atlas.asset'
    if len(loader['placeholderMap'])!=398 or len(atlas['materials'])!=398:raise ValueError('Original complete weapon loader/atlas order changed')
    mappings=[];materials=[];fixture_rows=[]
    for index,(mapping,matref) in enumerate(zip(loader['placeholderMap'],atlas['materials'])):
        placeholder=pointer(loader_obj,mapping['placeholderTexture']);mat=pointer(atlas_obj,matref);matfields=mat.read_typetree()
        main=next(v['m_Texture'] for k,v in matfields['m_SavedProperties']['m_TexEnvs'] if k=='_MainTex')
        if identity(pointer(mat,main))!=identity(placeholder):raise ValueError('Original atlas material and loader order differ at '+str(index))
        replacement=copy.deepcopy(mapping);replacement['placeholderTexture']=texture(placeholder)
        mappings.append(replacement);materials.append(material(mat))
        fixture_rows.append(dict(index=index,name=placeholder.read().m_Name,main=mapping['targetTextureReference']['AssetReference']['mAssetGuid'],dye=mapping['dyeTextureReference']['AssetReference']['mAssetGuid'],material=matfields['m_Name']))
    metadata=json_asset(dest/'primary-weapon-atlas-original.json',atlas)
    native_atlas=dest/'primary-weapon-atlas.asset';atlas_ref=asset(native_atlas,atlas['m_Name'],native_runtime/'ProfessionNativeTextureAtlas.cs',dict(sourcePath=atlas_path,sourceGuid=table[atlas_path]['guid'],originalMetadata=metadata,materials=materials));outputs.extend((native_atlas,native_atlas.with_name(native_atlas.name+'.meta')))
    native_loader=dest/'primary-weapon-texture-loader-high.asset';fields_to_keep={k:copy.deepcopy(loader[k]) for k in ('maxPlaceholderSize','unloadAfterSecondsUnused','LevelOfResolution','PlaceholderPath','TargetTexturePath','DyeTexturePath')}
    if loader['skeletonDataAsset']['m_PathID']!=0:raise ValueError('Review newly populated original loader skeleton pointer')
    fields_to_keep.update(atlasAsset=atlas_ref,skeletonDataAsset=dict(fileID=0),placeholderMap=mappings)
    loader_ref=asset(native_loader,loader['m_Name'],official_runtime/'YooAssetTextureLoader.cs',fields_to_keep);outputs.extend((native_loader,native_loader.with_name(native_loader.name+'.meta')))
    config_path='Assets/ScriptableConfig/Modules/Appearance/AppearanceAssetManagerConfig.asset';config_obj=apk.asset(table[config_path]['bundle'],config_path)
    verify_fields('AppearanceAssetManagerConfig',('UIDyeShader','DefaultSkeletonMaterial'))
    config=config_obj.read_typetree(nodes=fields_tree(schemas.default,[('UIDyeShader','PPtr<Shader>'),('DefaultSkeletonMaterial','PPtr<Material>')]))
    source(config_obj,config)
    config_ref=asset(dest/'appearance-asset-manager-config.asset',config['m_Name'],official_runtime/'AppearanceAssetManagerConfig.cs',dict(UIDyeShader=shader(pointer(config_obj,config['UIDyeShader'])),DefaultSkeletonMaterial=material(pointer(config_obj,config['DefaultSkeletonMaterial']))));outputs.extend((dest/'appearance-asset-manager-config.asset',dest/'appearance-asset-manager-config.asset.meta'))
    # High pages retain original GUIDs, with only previously converted actual payloads bound.
    weapon_catalog=ROOT/'unity/Assets/Game/Resources/ProfessionUI/weapon-resources.asset';catalog_text=weapon_catalog.read_text(encoding='utf-8');catalog=yaml.safe_load(catalog_text[catalog_text.index('MonoBehaviour:'):])['MonoBehaviour'];actual=[]
    for p in catalog['pages']:
        for key in ('Main','Dye'):
            if p[key+'Guid']:actual.append(dict(Guid=p[key+'Guid'],Texture=p[key]))
    entry=ROOT/'unity/Assets/Game/Resources/ProfessionUI/weapon-texture-resources.asset';asset(entry,'Original complete weapon texture loader',native_runtime/'ProfessionNativeWeaponTextureResources.cs',dict(loaderPath=loader_path,loaderGuid=table[loader_path]['guid'],loader=loader_ref,atlas=atlas_ref,managerConfig=config_ref,textures=actual));outputs.extend((entry,entry.with_name(entry.name+'.meta')))
    fixture_materials=[]
    for m in material_records:
        f=m['original_fields'];props=f['m_SavedProperties']
        if props['m_Ints']:raise ValueError('Review original material integer parameters')
        fixture_materials.append(dict(path=m['output']['path'].removeprefix('unity/'),name=f['m_Name'],keywords=f['m_ValidKeywords']+f['m_InvalidKeywords'],floats=[dict(name=n,value=v) for n,v in props['m_Floats']],colors=[dict(name=n,**dict(zip(('x','y','z','w'),(v[k] for k in ('r','g','b','a'))))) for n,v in props['m_Colors']]))
    fixture=CACHE/'typed/weapon-texture-loader-validation.json';fixture.write_text(json.dumps(dict(materials=fixture_materials,loaderPath=loader_path,loaderGuid=table[loader_path]['guid'],atlasPath=atlas_path,atlasGuid=table[atlas_path]['guid'],rows=fixture_rows,textures=[dict(path=t['output']['path'].removeprefix('unity/'),mips=t['mips']) for t in texture_records]),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionWeaponTextureLoaderValidation.cs'
    if validation.exists():meta(validation)
    proof=dict(format=1,manifest=apk.version,code_manifest=record(receipt),application_assembly=record(assembly),asset_table_sha256=hashlib.sha256(raw_table).hexdigest(),sources=sources,schema=schema,shaders=shader_records,textures=texture_records,materials=material_records,outputs=[record(p) for p in outputs],fixture=record(fixture),runtime=[record(p) for p in runtime for p in (p,p.with_name(p.name+'.meta'))],generator=record(Path(__file__)),validation=[record(p) for p in (validation,validation.with_name(validation.name+'.meta'))] if validation.exists() else [],
        operation='APK-first complete original 398-entry primary weapon high-loader/atlas order and metadata, all real placeholder texture bytes/mips/native properties and all 398 original material bindings. Original source manager shader/default material config retains all actual fallback dependencies. Reuse byte-hash-verified existing programs; translate missing original GLES3 programs only after selecting their actual graphics API/GPU tables, preserve original empty stripped passes and shader LOD/keyword scope, and retain raw source programs. Reuse the two prior actual high pages and default placeholders without changing their identity. Empty dye proxy objects retain empty original GUIDs. Explicit local catalog owns packaged assets; independent request release drops only the request reference. No SDK implementation or substitute prefab is used.',
        validated_scope='Pending native import/Play Mode full graph and GPU hash validation.',limitations=['Only the two actual default sword/staff high pages are bound. Other original high-page GUIDs remain intact and requests fail explicitly until their payloads are converted.','Production native Graphic, full weapon prefab/parts and profession-page input/save/Windows release remain incomplete.'])
    out=ROOT/'assets/reference/profession-ui/weapon-texture-loader-source-manifest.json';out.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Converted original weapon loader: 398 mappings/materials,',len(texture_records),'native texture payloads and',len(shader_records),'original shader bindings.')
    return proof

if __name__=='__main__':build()
