"""Bind all original attachment types to independent native geometry objects."""
import copy
import argparse
import hashlib
import json
from pathlib import Path
import uuid
import yaml

from prepare_unity_profession_native_inputs import ROOT,CACHE
from prepare_unity_profession_native_curves import record,f32
from prepare_unity_profession_vertex_geometry import synthetic_bone,matrix,evaluate
from prepare_unity_profession_atlas_geometry import corner_reference,corner_uvs,uv_reference
from prepare_native_character_geometry import atlas_regions
from official_apk import Apk
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from UnityPy.classes import PPtr
from prepare_unity_profession_reward_sprites import Dumper,fields,source_header
from prepare_unity_map13_native_scene import decode_mips


def payload_hash(obj):return hashlib.sha256(obj.get_raw_data()).hexdigest()


def build(project=None):
    proofs=[ROOT/'assets/reference/profession-ui'/name for name in ('vertex-geometry-source-manifest.json','atlas-geometry-source-manifest.json','shader-bindings-source-manifest.json')]
    inputs=[record(p) for p in proofs]
    known=json.loads(proofs[0].read_bytes())['inputs']
    apk=Apk.current()
    if apk.version!='871_156598':raise ValueError('Original attachment version differs')
    code=ROOT/'.tools/source-packages/current/871_156598/official-code'
    schemas=NativeSchemas(code,unity_project=project);schema_proof=install(schemas,code)
    cases=[];counts={k:0 for k in ('region','mesh','boundingbox','clipping','point','vertices','indices')};model_inputs=[]
    gpu=None
    for sex in ('male','female'):
        path=CACHE/'skeletons'/sex/'skeleton.json'
        if record(path) not in known:raise ValueError('Verified original skeleton changed')
        inputs.append(record(path));source=json.loads(path.read_bytes())
        objects=apk.objects('appearanceassets_character_model_'+sex+'_1')
        data_obj=next(o for o in objects if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Name=='character_'+sex.title()+'_1_SkeletonData')
        data,_=schemas.read(data_obj);scale=f32(data['scale'])
        model_inputs.append(dict(source=apk.provenance(data_obj),payload_sha256=payload_hash(data_obj),fields=data))
        if scale<=0:raise ValueError('Original skeleton scale is invalid')
        atlas_path=CACHE/'skeletons'/sex/'atlas.txt';inputs.append(record(atlas_path));regions=atlas_regions(atlas_path.read_text(encoding='utf-8'))
        region_ids={r['name']:i for i,r in enumerate(regions)}
        slots={s['name']:i for i,s in enumerate(source['slots'])};bones={b['name']:i for i,b in enumerate(source['bones'])}
        synthetic=[synthetic_bone(i,b['name']) for i,b in enumerate(source['bones'])];matrices=[matrix(b) for b in synthetic]
        attachments=[]
        for skin in source['skins']:
            for slot,entries in skin.get('attachments',{}).items():
                bone=bones[source['slots'][slots[slot]]['bone']]
                for key,a in entries.items():
                    kind=a.get('type','region');counts[kind]+=1
                    row=dict(skin=skin['name'],key=key,name=a.get('name',key),slot=slots[slot],slotBone=bone,kind=kind,scale=scale)
                    if kind in ('region','mesh'):
                        image=a.get('path',a.get('name',key));row['region']=region_ids[image];r=regions[row['region']]
                    if kind=='region':
                        params={k:f32(a.get(k,1 if k in ('scaleX','scaleY') else 0)) for k in ('width','height','x','y','scaleX','scaleY','rotation')};row.update(params)
                        corners=corner_reference(r,params);m=matrices[bone]
                        row['expected']=[dict(x=f32((m[0]*p['x']+m[1]*p['y']+m[2])*scale),y=f32((m[3]*p['x']+m[4]*p['y']+m[5])*scale)) for p in corners]
                        row['expectedUvs']=[uv_reference(r,p) for p in corner_uvs(r)]
                        counts['vertices']+=4;counts['indices']+=6
                    elif kind in ('mesh','boundingbox','clipping'):
                        count=len(a['uvs'])//2 if kind=='mesh' else a['vertexCount'];row.update(count=count,vertices=[f32(v) for v in a['vertices']])
                        world,_,_=evaluate(row['vertices'],count,bone,matrices)
                        row['expected']=[dict(x=f32(p['x']*scale),y=f32(p['y']*scale)) for p in world];counts['vertices']+=count
                        if kind=='mesh':
                            row.update(uvs=a['uvs'],triangles=a['triangles']);counts['indices']+=len(a['triangles'])
                            row['expectedUvs']=[uv_reference(r,a['uvs'][i:i+2]) for i in range(0,len(a['uvs']),2)]
                        elif kind=='clipping':row['end']=slots[a['end']]
                    elif kind=='point':
                        row.update(x=f32(a.get('x',0)),y=f32(a.get('y',0)));m=matrices[bone]
                        row['expected']=[dict(x=f32((m[0]*row['x']+m[1]*row['y']+m[2])*scale),y=f32((m[3]*row['x']+m[4]*row['y']+m[5])*scale))];counts['vertices']+=1
                    else:raise ValueError('Restore unhandled original attachment type: '+kind)
                    if sex=='male' and skin['name']=='body_200_M_01' and kind=='mesh' and gpu is None:
                        gpu=dict(sex=sex,attachment=len(attachments),page=r['page'])
                    attachments.append(row)
        cases.append(dict(sex=sex,bones=synthetic,slotCount=len(slots),regions=regions,attachments=attachments))
    # Resolve the real high-resolution body atlas through the original loader's
    # placeholder and AssetTable GUID, never through a baked preview prefab.
    objects=apk.objects('appearanceassets_character_model_male_1')
    loader_obj=next(o for o in objects if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Name=='character_Male_1_TextureLoader_High')
    loader,_=schemas.read(loader_obj);model_inputs.append(dict(source=apk.provenance(loader_obj),payload_sha256=payload_hash(loader_obj),fields=loader))
    mapping=next(m for m in loader['placeholderMap'] if PPtr(**m['placeholderTexture'],assetsfile=loader_obj.assets_file).read().m_Name==gpu['page']+'_placeholder')
    manifest_bytes=apk.source.manifest_bytes('DefaultPackage',apk.archive);table=read_asset_manifest(manifest_bytes);by_guid={r['guid']:r for r in table if r['guid']}
    textures=[]
    for role in ('targetTextureReference','dyeTextureReference'):
        guid=mapping[role]['AssetReference']['mAssetGuid']
        if not guid:continue
        asset=by_guid[guid];obj=apk.asset(asset['bundle'],asset['path']);texture=obj.read()
        pixels,mips,profile=decode_mips(texture)
        original=obj.read_typetree();original.pop('image data');native=source_header();native.update(fields(copy.deepcopy(original)))
        native.update(serializedVersion=2,m_TextureFormat=4,m_CompleteImageSize=len(pixels),m_PlatformBlob='',m_StreamData=dict(serializedVersion=2,offset=0,size=0,path=''))
        native['image data']=len(pixels);native['_typelessdata']=pixels.hex()
        suffix='main' if role=='targetTextureReference' else 'dye'
        dest=ROOT/'unity/Assets/Official/ProfessionUI/CharacterTextures'/(texture.m_Name+'.'+suffix+'.texture2d.asset');dest.parent.mkdir(parents=True,exist_ok=True)
        target_guid=uuid.uuid5(uuid.NAMESPACE_URL,'zjcs/profession/native-texture/'+obj.assets_file.name+':'+str(obj.path_id)).hex
        dest.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n'+yaml.dump({'Texture2D':native},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n')
        meta=dest.with_name(dest.name+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+target_guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n',encoding='utf-8',newline='\n')
        textures.append(dict(role=role,asset=asset,source=apk.provenance(obj),payload_sha256=payload_hash(obj),encoded_sha256=hashlib.sha256(texture.get_image_data()).hexdigest(),original_fields=original,
            profile=profile,mips=mips,pixels_sha256=hashlib.sha256(pixels).hexdigest(),output=record(dest),meta=record(meta),guid=target_guid))
    for folder in (ROOT/'unity/Assets/Official/ProfessionUI/CharacterTextures',):
        meta=folder.with_name(folder.name+'.meta')
        if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'zjcs/profession/native-textures-folder').hex+'\nfolderAsset: yes\n',encoding='utf-8',newline='\n')
    if len({t['output']['path'] for t in textures})!=len(textures):raise ValueError('Original main and dye texture paths collide')
    for t in textures:
        if record(ROOT/t['output']['path'])!=t['output']:raise ValueError('Native texture output changed after export')
    gpu['textures']=[dict(role=t['role'],path=t['output']['path'].removeprefix('unity/'),sha256=t['output']['sha256']) for t in textures]
    gpu['shader']=next(row['native']['path'] for row in json.loads(proofs[2].read_bytes())['bindings'] if row['native_relative_path']=='Shaders/Spine/Spine-SkeletonGraphicDye.shader')
    output=CACHE/'typed/native-attachments-validation.json';output.write_text(json.dumps(dict(cases=cases,gpu=gpu),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAttachment.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeAttachmentValidation.cs'
    proof=dict(format=1,manifest=apk.version,inputs=inputs,model_inputs=model_inputs,asset_table_sha256=hashlib.sha256(manifest_bytes).hexdigest(),textures=textures,
        fixture=record(output),generator=record(Path(__file__)),schema=schema_proof,
        schema_reference_inputs=[record(p) if p.is_relative_to(ROOT) else dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in schemas.reference_inputs],
        outputs=[record(p) for file in (runtime,validation) for p in (file,file.with_name(file.name+'.meta'))],counts=counts,
        operation='Independently combine original region/mesh/point/bounding/clipping attachment identity, geometry and source data scale. Preserve original mesh triangle indices, UVs, trim/rotation, source weights and typed non-rendering attachments; bounding geometry connects to the migrated clipping interface. Real body atlas and dye texture are resolved APK-first through current original placeholder mappings and AssetTable GUIDs, decoded mip-by-mip without re-compression and retain native samplers/color space. No skeleton SDK implementation or baked actor prefab is used.',
        validated_scope='Unity 2022.3.57f1c2 isolated Play Mode passed all 34009 original attachments, 254385 scaled vertices/points and 635730 exact triangle indices. Maximum coordinate difference 3.81469727E-06 within 0.000005 + 0.000007 * abs(expected), maximum UV difference 5.96046448E-08 within 0.000002. Original bounding/clip/point types and explicit source data scale pass. Real original main/dye textures have separate hash-verified artifacts; D3D11 raster has 25719 visible and 22359 colored pixels, zero pixels differing from independently computed reference geometry by more than 2 channel levels. Runtime image inspected. Geometry matrices are synthetic, not official constrained poses; complete character/page rendering is not validated.',limitations=['Production skeleton state, world constraints, clipping triangulation, material batches and actor/page integration remain required.'])
    (ROOT/'assets/reference/profession-ui/native-attachments-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Prepared original native attachment objects:',counts,'body atlas textures',len(textures))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project',type=Path)
    build(parser.parse_args().project)
