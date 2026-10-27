"""Restore official summon skeleton, native textures and model bindings without a Spine runtime."""
from __future__ import annotations
import json
from pathlib import Path
import sys
import uuid
import copy
import yaml

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/python-packages'))
import UnityPy
from UnityPy.classes.PPtr import PPtr
from official_apk import Apk
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from convert_official_skeletons import reader_type, WHEEL_SHA256
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_map13_sources import sha, encode
from official_shader_programs import gles_programs

DEST = ROOT / 'unity/Assets/Official/Combat/Summons'
RECEIPT = ROOT / 'assets/reference/battle-rules/summon-models-source-manifest.json'


def atlas_regions(text):
    header, regions, name = {}, {}, None
    for line in text.strip().splitlines()[1:]:
        if not line.strip(): continue
        if ':' not in line:
            name=line.strip(); regions[name]={}
        else:
            key,value=map(str.strip,line.split(':',1))
            (regions[name] if name else header)[key]=value
    if set(header)-{'size','filter','scale'} or header['filter']!='Linear, Linear':
        raise ValueError('Restore original atlas page settings')
    width,height=map(int,header['size'].split(','))
    if float(header.get('scale',1))<=0: raise ValueError('Invalid source atlas scale')
    for name,region in regions.items():
        if set(region)-{'bounds','offsets','rotate'}: raise ValueError('Restore original atlas region settings')
        x,y,w,h=map(int,region['bounds'].split(','))
        ox,oy,ow,oh=map(int,region.get('offsets',f'0,0,{w},{h}').split(','))
        rotation=int(region.get('rotate',0))
        if rotation not in (0,90,180,270): raise ValueError('Restore atlas rotation')
        regions[name]=dict(x=x,y=y,w=w,h=h,ox=ox,oy=oy,ow=ow,oh=oh,rotation=rotation)
    return width,height,regions


def output(path, raw, importer='DefaultImporter', file_id=0):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary=Path(str(path)+'.battle-tmp');temporary.write_bytes(raw);temporary.replace(path)
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:summon-model:' + str(path.relative_to(ROOT))).hex
        meta.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + importer + ':\n  externalObjects: {}\n'
                        + ('  mainObjectFileID: ' + str(file_id) + '\n' if file_id else '')
                        + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf8')
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw), meta_sha256=sha(meta.read_bytes()))


def flatten(skeleton, atlas):
    if set(skeleton) - {'skeleton', 'bones', 'slots', 'skins', 'animations', 'ik'}:
        raise ValueError('Restore additional official summon skeleton features')
    if len(skeleton['skins']) != 1 or skeleton['skins'][0]['name'] != 'default':
        raise ValueError('Restore original summon skin selection')
    width, height, regions = atlas_regions(atlas)
    bones = {b['name']: i for i, b in enumerate(skeleton['bones'])}
    slots = {s['name']: i for i, s in enumerate(skeleton['slots'])}
    constraints = {c['name']: i for i, c in enumerate(skeleton.get('ik', []))}
    result = dict(bones=[], slots=[], constraints=[], attachments=[], animations=[])
    for bone in skeleton['bones']:
        if set(bone) - {'name', 'parent', 'rotation', 'x', 'y', 'length', 'scaleX', 'scaleY', 'inherit'}:
            raise ValueError('Restore original summon bone inheritance or shear')
        if bone.get('inherit','normal') not in ('normal','noRotationOrReflection'):
            raise ValueError('Restore original summon inheritance mode')
        result['bones'].append(dict(name=bone['name'], parent=bones.get(bone.get('parent'), -1),
            x=bone.get('x', 0), y=bone.get('y', 0), rotation=bone.get('rotation', 0),
            scaleX=bone.get('scaleX', 1), scaleY=bone.get('scaleY', 1), length=bone.get('length', 0),
            inherit=bone.get('inherit','normal')))
    for constraint in skeleton.get('ik', []):
        if set(constraint) - {'name', 'order', 'bones', 'target', 'bendPositive', 'mix'}:
            raise ValueError('Restore original summon IK softness/stretch/compress')
        ids = [bones[n] for n in constraint['bones']]
        if len(ids) not in (1, 2): raise ValueError('Restore original summon IK chain length')
        result['constraints'].append(dict(name=constraint['name'], order=constraint.get('order', 0),
            parent=ids[0], child=ids[1] if len(ids) == 2 else -1, target=bones[constraint['target']],
            mix=constraint.get('mix', 1), bend=1 if constraint.get('bendPositive', True) else -1))
    ids = {}
    skins = skeleton['skins'][0]['attachments']
    for slot in skeleton['slots']:
        if set(slot) - {'name', 'bone', 'attachment', 'color', 'blend'}:
            raise ValueError('Restore original summon slot setup')
        result['slots'].append(dict(name=slot['name'], bone=bones[slot['bone']],
            color=slot.get('color', 'ffffffff'), attachment=-1, blend=slot.get('blend', 'normal')))
        for name, attachment in skins.get(slot['name'], {}).items():
            ids[slot['name'], name] = len(result['attachments'])
            kind = attachment.get('type', 'region')
            if kind not in ('region', 'mesh', 'clipping'):
                raise ValueError('Restore original summon attachment: ' + kind)
            if set(attachment)-{'type','path','name','width','height','rotation','x','y','scaleX','scaleY','hull','triangles','uvs','vertices','vertexCount','end'}:
                raise ValueError('Restore additional original summon attachment fields')
            definition = dict(name=name, slot=slots[slot['name']], kind=kind,
                endSlot=slots.get(attachment.get('end'), -1), vertices=[], influences=[], uv=[], triangles=[])
            def point(influences):
                definition['vertices'].append(dict(first=len(definition['influences']), count=len(influences)))
                definition['influences'].extend(influences)
            def influence(bone, x, y, weight=1): return dict(bone=bone, x=x, y=y, weight=weight)
            if kind == 'region':
                import math
                region = regions[attachment.get('path', attachment.get('name', name))]
                left, bottom = -region['ow']/2 + region['ox'], -region['oh']/2 + region['oy']
                right, top = left + region['w'], bottom + region['h']
                r = math.radians(attachment.get('rotation', 0)); c, s = math.cos(r), math.sin(r)
                uvs = []
                for x, y in [(left,bottom), (left,top), (right,top), (right,bottom)]:
                    uvs += [(x + region['ow']/2)/region['ow'], 1 - (y + region['oh']/2)/region['oh']]
                    x *= attachment['width']/region['ow'] * attachment.get('scaleX', 1)
                    y *= attachment['height']/region['oh'] * attachment.get('scaleY', 1)
                    point([influence(bones[slot['bone']], c*x-s*y+attachment.get('x',0), s*x+c*y+attachment.get('y',0))])
                definition['triangles'] = [0,1,2,2,3,0]
            else:
                values = attachment['vertices']; cursor = 0
                count = attachment['vertexCount'] if kind == 'clipping' else len(attachment['uvs'])//2
                for i in range(count):
                    if len(values) == count*2:
                        point([influence(bones[slot['bone']], *values[i*2:i*2+2])]); cursor += 2
                    else:
                        n = int(values[cursor]); cursor += 1; influences = []
                        for _ in range(n):
                            bone, x, y, weight = values[cursor:cursor+4]; cursor += 4
                            if int(bone) != bone or not 0 <= bone < len(bones): raise ValueError('Original summon influence bone differs')
                            influences.append(influence(int(bone), x, y, weight))
                        point(influences)
                if cursor != len(values): raise ValueError('Original summon influence data is not fully consumed')
                uvs = attachment.get('uvs', [])
                definition['triangles'] = attachment.get('triangles', [])
            if kind != 'clipping':
                region = regions[attachment.get('path', attachment.get('name', name))]
                for u, v in zip(uvs[::2], uvs[1::2]):
                    x = u*region['ow'] - region['ox']
                    y = v*region['oh'] - (region['oh']-region['oy']-region['h'])
                    rotation=region['rotation']
                    px,py=({0:(x,y),90:(y,region['w']-x),180:(region['w']-x,region['h']-y),
                            270:(region['h']-y,x)})[rotation]
                    px+=region['x'];py+=region['y']
                    definition['uv'].extend([px/width, 1-py/height])
            result['attachments'].append(definition)
        result['slots'][-1]['attachment'] = ids.get((slot['name'], slot.get('attachment')), -1)
    for name, animation in skeleton['animations'].items():
        if set(animation) - {'bones', 'slots', 'ik', 'drawOrder'}:
            raise ValueError('Restore original summon deform/event timeline')
        anim = dict(name=name, duration=0, tracks=[], attachments=[], drawOrders=[])
        def track(kind, index, axis, keys, field, default=0, component=0):
            frames = []
            for key in keys:
                curve = key.get('curve', [])
                if isinstance(curve, list): curve = curve[component*4:component*4+4]
                elif curve != 'stepped': raise ValueError('Unknown original interpolation')
                frames.append(dict(time=key.get('time',0), value=key.get(field,default),
                    stepped=curve=='stepped', curve=curve if isinstance(curve,list) else []))
            anim['tracks'].append(dict(kind=kind,index=index,axis=axis,keys=frames))
        for bone, channels in animation.get('bones', {}).items():
            for channel, keys in channels.items():
                if channel == 'rotate': track(0,bones[bone],2,keys,'value')
                elif channel in ('translate','scale'):
                    for axis, field in enumerate(('x','y')):
                        track(0 if channel=='translate' else 1,bones[bone],axis,keys,field,1 if channel=='scale' else 0,axis)
                else: raise ValueError('Restore original summon bone timeline: '+channel)
        for slot, channels in animation.get('slots', {}).items():
            for channel, keys in channels.items():
                if channel == 'attachment':
                    anim['attachments'].extend(dict(time=k.get('time',0),slot=slots[slot],
                        attachment=ids.get((slot,k.get('name')),-1)) for k in keys)
                elif channel == 'rgba':
                    for axis in range(4):
                        converted=[dict(k, value=int(k.get('color','ffffffff')[axis*2:axis*2+2],16)/255) for k in keys]
                        track(2,slots[slot],axis,converted,'value',component=axis)
                else: raise ValueError('Restore original summon color channel: '+channel)
        for constraint, keys in animation.get('ik', {}).items():
            track(3,constraints[constraint],0,keys,'mix',1)
            track(3,constraints[constraint],1,[dict(k, bend=1 if k.get('bendPositive',True) else -1,curve='stepped') for k in keys],'bend')
        for key in animation.get('drawOrder', []):
            order = [-1]*len(slots); unchanged = []; cursor = 0
            for offset in key.get('offsets', []):
                index = slots[offset['slot']]
                while cursor < index: unchanged.append(cursor); cursor += 1
                delta=offset['offset']
                # The MIT reader exposes this unsigned varint as uint32; the binary field is
                # a signed 32-bit slot offset. Preserve its two's-complement value.
                if delta>=2**31: delta-=2**32
                destination=cursor+delta
                if not 0<=destination<len(order): raise ValueError('Invalid original signed draw-order offset')
                order[destination] = cursor; cursor += 1
            unchanged.extend(range(cursor,len(slots)))
            for index in range(len(order)-1,-1,-1):
                if order[index] == -1: order[index] = unchanged.pop()
            if sorted(order) != list(range(len(slots))): raise ValueError('Original draw order is not a permutation')
            anim['drawOrders'].append(dict(time=key.get('time',0),slots=order))
        anim['duration'] = max([0]+[k['time'] for t in anim['tracks'] for k in t['keys']]
            +[k['time'] for k in anim['attachments']+anim['drawOrders']])
        result['animations'].append(anim)
    return result


def build():
    apk=Apk.current(); code=ROOT/'.tools/source-packages/current'/apk.version/'official-code'
    schemas=NativeSchemas(code); schema_proof=install(schemas,code); Reader,_=reader_type()
    asset_table=apk.source.manifest_bytes('DefaultPackage',apk.archive)
    guids={a['guid']:a for a in read_asset_manifest(asset_table) if a['guid']}
    outputs=[]; inputs={}; models=[]; shader_binding=None
    previous=json.loads(RECEIPT.read_bytes()) if RECEIPT.is_file() else {}
    templates=[ROOT/f'.tools/map{i}-native-cache-871_156598/native-texture-template.asset' for i in (13,14)]
    template_path=next((p for p in templates if p.is_file()),None)
    if template_path is None: raise FileNotFoundError('Generate the Unity 2022.3 native Texture2D serialization template')
    template=template_path.read_text('utf8')
    def record(obj):
        inputs[obj.assets_file.name+':'+str(obj.path_id)]=dict(apk.provenance(obj),object_sha256=sha(obj.get_raw_data()))
        return obj
    def read(obj): record(obj);return schemas.read(obj)[0]
    def ptr(obj,value): return record(PPtr(**value,assetsfile=obj.assets_file).deref())
    for model,entity,prefab in [(3181,3181,'prefabs_ecentity_fight_fightmonster_3181'),(3193,4600,'prefabs_ecentity_fight_fightother_4600')]:
        objects=apk.objects(f'appearanceassets_summons_summons_{model}')
        data_obj=next(o for o in objects if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName=='SkeletonDataAsset')
        data=read(data_obj); skel_obj=ptr(data_obj,data['skeletonJSON']);raw=skel_obj.read().m_Script.encode('utf8','surrogateescape')
        reader=Reader(); skeleton=reader.convert(raw)
        if reader.inp.index!=len(raw): raise ValueError('Original summon binary was not fully decoded')
        if any(data[k] for k in ['skeletonDataModifiers','fromAnimation','toAnimation','duration','zSlots']): raise ValueError('Restore original summon modifiers/mix overrides')
        if len(data['atlasAssets'])!=1: raise ValueError('Restore original multi-atlas summon')
        atlas_obj=ptr(data_obj,data['atlasAssets'][0]); atlas=read(atlas_obj)
        atlas_text=ptr(atlas_obj,atlas['atlasFile']).read().m_Script
        definition=flatten(skeleton,atlas_text)
        loaders=[(o,read(o)) for o in objects if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName=='YooAssetTextureLoader']
        high=next(fields for _,fields in loaders if fields['LevelOfResolution']==2)
        if len(high['placeholderMap'])!=1: raise ValueError('Restore original multi-page summon')
        guid=high['placeholderMap'][0]['targetTextureReference']['AssetReference']['mAssetGuid'];asset=guids[guid]
        texture_obj=record(apk.asset(asset['bundle'],asset['path'])); texture=texture_obj.read()
        width,height,_=atlas_regions(atlas_text)
        if (texture.m_Width,texture.m_Height)!=(width,height): raise ValueError('Original high atlas dimensions differ')
        rgba,mips,profile=decode_mips(texture)
        texture_path=DEST/(str(model)+'.texture.asset')
        outputs.append(output(texture_path,native_texture(template,texture,rgba),'NativeFormatImporter',2800000))
        definition.update(id=entity,modelId=model,scale=data['scale'],defaultMix=data['defaultMix'],texture=texture_path.relative_to(ROOT/'unity').as_posix())
        renderers=[o for o in apk.objects(prefab) if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName=='SkeletonMecanim']
        if len(renderers)!=1: raise ValueError('Original summon model renderer is not unique')
        renderer=renderers[0];definition['renderer']=read(renderer);transforms=[]
        mesh_renderer=next(c.component.deref() for c in renderer.read(check_read=False).m_GameObject.read().m_Component
            if c.component.deref().type.name=='MeshRenderer')
        mesh_fields=record(mesh_renderer).read_typetree()
        definition['sortingLayer']=mesh_fields['m_SortingLayer'];definition['sortingOrder']=mesh_fields['m_SortingOrder']
        transform=renderer.read(check_read=False).m_GameObject.read().m_Component[0].component.deref()
        while transform.read().m_Father.path_id:
            record(transform);v=transform.read();transforms.insert(0,dict(name=v.m_GameObject.read().m_Name,fields=transform.read_typetree()))
            transform=v.m_Father.deref()
        definition['transforms']=transforms
        round_objects=[o for o in apk.objects(prefab) if o.type.name=='MonoBehaviour'
            and o.read(check_read=False).m_Script.read().m_ClassName=='MyFightRound']
        if len(round_objects)!=1: raise ValueError('Original summon round display component differs')
        definition['showOnRoundProgress']=bool(read(round_objects[0])['mIsShowOnRoundProgress'])
        definition['portraitData']=None
        if definition['showOnRoundProgress']:
            portrait_asset=next(a for a in guids.values() if a['address']==f'Assets/Image/ItemIcon/GameHeadIcon/Monster/G_{entity}')
            portrait_obj=record(apk.asset(portrait_asset['bundle'],portrait_asset['path']))
            portrait_texture=portrait_obj.read()
            sprite_obj=record(next(o for o in apk.objects(portrait_asset['bundle']) if o.type.name=='Sprite' and o.read().m_Name==f'G_{entity}'))
            sprite=sprite_obj.read()
            if (sprite.m_RD.texture.path_id!=portrait_obj.path_id or sprite.m_SpriteAtlas.path_id
                or tuple(getattr(sprite.m_Rect,k) for k in ('x','y','width','height'))!=(0,0,portrait_texture.m_Width,portrait_texture.m_Height)
                or sprite.m_RD.alphaTexture.path_id or sprite.m_PhysicsShape):
                raise ValueError('Restore additional original summon portrait parameters')
            rgba_portrait,_,_=decode_mips(portrait_texture)
            portrait_path=DEST/f'G_{entity}.texture.asset'
            outputs.append(output(portrait_path,native_texture(template,portrait_texture,rgba_portrait),'NativeFormatImporter',2800000))
            from UnityPy.helpers.MeshHelper import MeshHandler
            mesh=MeshHandler(sprite.m_RD,sprite_obj.version);mesh.process()
            definition['portraitData']=dict(name=sprite.m_Name,texture=portrait_path.relative_to(ROOT/'unity').as_posix(),
                width=portrait_texture.m_Width,height=portrait_texture.m_Height,
                pivot=dict(x=sprite.m_Pivot.x,y=sprite.m_Pivot.y),ppu=sprite.m_PixelsToUnits,
                extrude=sprite.m_Extrude,border=dict(x=sprite.m_Border.x,y=sprite.m_Border.y,z=sprite.m_Border.z,w=sprite.m_Border.w),
                vertices=[dict(x=v[0],y=v[1]) for v in mesh.m_Vertices],
                triangles=[v for submesh in mesh.get_triangles() for triangle in submesh for v in triangle])
            sprite_path=DEST/f'G_{entity}.sprite.asset'
            native=copy.deepcopy(sprite_obj.read_typetree())
            texture_guid=uuid.uuid5(uuid.NAMESPACE_URL,'zjcs:summon-model:'+str(portrait_path.relative_to(ROOT))).hex
            def native_fields(value):
                if isinstance(value,list):return [native_fields(v) for v in value]
                if isinstance(value,dict):
                    if set(value)=={'m_FileID','m_PathID'}:
                        if value==native['m_RD']['texture']:return dict(fileID=2800000,guid=texture_guid,type=2)
                        if value['m_PathID']:raise ValueError('Restore additional original summon Sprite reference')
                        return dict(fileID=0)
                    return {k:native_fields(v) for k,v in value.items()}
                return value
            native=native_fields(native)
            rd=native['m_RD'];rd['m_IndexBuffer']=bytes(rd['m_IndexBuffer']).hex()
            vertex=rd['m_VertexData'];raw_vertices=bytes(vertex['m_DataSize'])
            vertex['m_DataSize']=len(raw_vertices);vertex['_typelessdata']=raw_vertices.hex()
            native['m_RenderDataKey']={uuid.uuid5(uuid.NAMESPACE_URL,'zjcs:summon-model:'+str(sprite_path.relative_to(ROOT))).hex:21300000}
            class Dumper(yaml.SafeDumper):pass
            def represent(dumper,value):return dumper.represent_mapping('tag:yaml.org,2002:map',value.items(),
                flow_style=bool(value) and all(not isinstance(v,(dict,list)) for v in value.values()))
            Dumper.add_representer(dict,represent)
            sprite_text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n'+yaml.dump({'Sprite':native},Dumper=Dumper,allow_unicode=True,sort_keys=False,width=10000)
            outputs.append(output(sprite_path,sprite_text.encode(),'NativeFormatImporter',21300000))
            definition['portraitData']['spritePath']=sprite_path.relative_to(ROOT/'unity').as_posix()
        material_obj=ptr(atlas_obj,atlas['materials'][0]);material=material_obj.read()
        definition['material']=material_obj.read_typetree();shader_obj=record(material.m_Shader.deref())
        shader_path=ROOT/'unity/Assets/Game/Runtime/Official/Combat/Shaders/OfficialSummonSkeletonEffect2D.shader'
        previous_shader=next((entry for entry in previous.get('outputs',[]) if entry['path']==shader_path.relative_to(ROOT).as_posix()),None)
        if (previous.get('shader',{}).get('object_sha256')==sha(shader_obj.get_raw_data())
            and previous_shader and shader_path.is_file() and sha(shader_path.read_bytes())==previous_shader['sha256']):
            # A clean checkout can reuse this module's verified native translation. Recheck
            # the original shader object and native output instead of requiring another map's cache.
            shader_text=shader_path.read_text('utf8')
            translation=dict(source=previous['shader']['source'],object_sha256=previous['shader']['object_sha256'],
                translation_receipt=previous['shader']['translation_receipt'],
                translation_input=dict(path=shader_path.relative_to(ROOT).as_posix(),sha256=previous_shader['sha256']),
                operation="Reuse this module's hash-verified native translation; original source lineage remains recorded above.")
        else:
            shader_receipt_path=ROOT/'assets/reference/official-map/map13-entity-shaders-source-manifest.json'
            shader_receipt=json.loads(shader_receipt_path.read_bytes())
            candidates=[entry for entry in shader_receipt['inputs'] if entry['original']['payload_sha256']==sha(shader_obj.get_raw_data())]
            if len(candidates)!=1: raise ValueError('Original summon shader has no unique verified native translation')
            shared=next(entry for entry in shader_receipt['outputs'] if entry['original_shader']==shader_obj.read().m_ParsedForm.m_Name)
            shared_path=ROOT/shared['path']
            if sha(shared_path.read_bytes())!=shared['sha256']: raise ValueError('Verified shared shader output changed')
            shader_text=shared_path.read_text('utf8')
            first=shader_text.split('"',2)[1]
            shader_text=shader_text.replace('Shader "'+first+'"','Shader "Zjcs/Official/Combat/Summon/SkeletonEffect2D"',1)
            translation=dict(source=apk.provenance(shader_obj),object_sha256=sha(shader_obj.get_raw_data()),
                translation_receipt=dict(path=shader_receipt_path.relative_to(ROOT).as_posix(),sha256=sha(shader_receipt_path.read_bytes())),
                translation_input=shared,operation='Reuse verified translation with only the Shader asset namespace changed.')
        if shader_binding is None:
            outputs.append(output(shader_path,shader_text.encode(),'ShaderImporter'))
            outputs.append(output(DEST/'SkeletonEffect2D.programs.json',encode(gles_programs(shader_obj.read(),with_keywords=True))))
            shader_binding=translation
        definition['shaderPath']=shader_path.relative_to(ROOT/'unity').as_posix()
        settings=definition['material'];props=settings['m_SavedProperties'];textures=[]
        for key,env in material.m_SavedProperties.m_TexEnvs:
            path=''
            if key=='_MainTex': path=texture_path.relative_to(ROOT/'unity').as_posix()
            elif env.m_Texture.path_id:
                obj=record(env.m_Texture.deref());tex=obj.read();raw_tex,mip_records,tex_profile=decode_mips(tex)
                path_obj=DEST/'Textures'/(tex.m_Name+'-'+sha(obj.get_raw_data())[:12]+'.asset')
                if not any(entry['path']==path_obj.relative_to(ROOT).as_posix() for entry in outputs):
                    outputs.append(output(path_obj,native_texture(template,tex,raw_tex),'NativeFormatImporter',2800000))
                path=path_obj.relative_to(ROOT/'unity').as_posix()
            textures.append(dict(name=key,path=path,scale=dict(x=env.m_Scale.x,y=env.m_Scale.y),offset=dict(x=env.m_Offset.x,y=env.m_Offset.y)))
        definition['materialSettings']=dict(name=settings['m_Name'],renderQueue=settings['m_CustomRenderQueue'],
            keywords=settings['m_ValidKeywords'],disabledPasses=settings['disabledShaderPasses'],
            enableInstancing=settings['m_EnableInstancingVariants'],doubleSidedGI=settings['m_DoubleSidedGI'],
            floats=[dict(name=k,value=v) for k,v in props['m_Floats']],
            ints=[dict(name=k,value=v) for k,v in props.get('m_Ints',[])],
            colors=[dict(name=k,value=v) for k,v in props['m_Colors']],textures=textures)
        outputs.append(output(DEST/(str(model)+'.skeleton-source.txt'),encode(skeleton)))
        outputs.append(output(DEST/(str(model)+'.atlas-source.txt'),atlas_text.encode()))
        outputs.append(output(DEST/(str(model)+'.json'),encode(definition)))
        models.append(dict(entityId=entity,modelId=model,source=apk.provenance(data_obj),
            high_texture=dict(asset=asset,source=apk.provenance(texture_obj),profile=profile,mips=mips),
            bones=len(skeleton['bones']),constraints=len(skeleton.get('ik',[])),animations=list(skeleton['animations'])))
        print('Decoded original summon',model,models[-1]['bones'],'bones,',len(skeleton['animations']),'animations',flush=True)
    for path in [DEST,DEST/'Textures',shader_path.parent]:
        meta=Path(str(path)+'.meta')
        if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n',encoding='utf8')
    RECEIPT.parent.mkdir(parents=True,exist_ok=True)
    RECEIPT.write_bytes(encode(dict(manifest=apk.version,inputs=list(inputs.values()),outputs=outputs,models=models,
        schemas=schema_proof,asset_table_sha256=sha(asset_table),texture_template=dict(path=template_path.relative_to(ROOT).as_posix(),sha256=sha(template_path.read_bytes())),
        reader=dict(name='SkelToJson',version='1.0.1',wheel_sha256=WHEEL_SHA256,license='MIT'),
        shader=shader_binding,
        pipeline=dict(path='unity/Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset',
            sha256=sha((ROOT/'unity/Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset').read_bytes()),
            original='globalgamemanagers.assets:22209',
            source_receipt='assets/reference/official-map/map13-native-pipeline-source-manifest.json',
            source_receipt_sha256=sha((ROOT/'assets/reference/official-map/map13-native-pipeline-source-manifest.json').read_bytes()),
            use='Explicitly share the recovered original APK global URP 2D pipeline during battle; restore the previous scene pipeline on release.'),
        public_format_reference=dict(url='https://eu.esotericsoftware.com/spine-clipping',
            use='End slot equal to the clipping slot means clipping continues to the end of draw order. Documentation only; no runtime implementation copied.'),
        operation='APK-first verified source recovery, lossless skeleton decoding, signed32 draw-order offsets, original weighted influences/curves/attachments/draw order/IK, full high-resolution texture mips and original prefab transforms. Unity native meshes, source curves, IK, clipping, original material/shader/pipeline and summon portraits are bound and exercised in DX12 Play Mode. No Spine runtime loaded or sampled.',
        limitations=['Only original skeletal channels and direct named-pose playback are migrated; original Animator state transitions, mixing and controller behavior remain incomplete.',
            'Only summon entities 3181 and 4600 are bound. Generation/connection/destruction effects and remaining summon models are not migrated.',
            'Play Mode validation covers the two models and existing battle HUD flow; Windows Player and complete battle acceptance remain outstanding.'])))


if __name__=='__main__': build()
