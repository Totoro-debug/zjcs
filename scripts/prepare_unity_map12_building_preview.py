"""Restore the lift dialog's original unanimated setup meshes without a Spine runtime.

Read the original SkeletonGraphic, skeleton/atlas and material, including all
shader variants. This deliberately rejects a selected animation or constraints;
the three original dialog components have an empty startingAnimation.
"""
import argparse
import json
import math
import re

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from UnityPy.classes.PPtr import PPtr
from convert_official_skeletons import reader_type, WHEEL_SHA256
from prepare_unity_map12_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_native_scene import decode_mips
from prepare_unity_map13_water_shaders import translate, pass_state, expressions
from prepare_unity_map13_pipeline_shaders import vector_comparisons
from prepare_battle_hud_materials import stage


DEST = ROOT / 'unity/Assets/Official/World/Map/12/Source/BuildingPreview'


def program_body(program, vertex):
    code, uniforms = stage(program, vertex)
    code = vector_comparisons(expressions(code))
    if re.search(r'hlslcc_mtx|\bin_\w+|\bvs_\w+', code):
        raise ValueError('Untranslated original building UI shader operation')
    return code, uniforms


def atlas_regions(text):
    lines = text.strip().splitlines()
    regions, header, name = {}, {}, None
    for line in lines[1:]:
        if not line.strip(): continue
        if ':' not in line:
            name = line.strip(); regions[name] = {}
        else:
            key, value = map(str.strip, line.split(':', 1))
            (regions[name] if name else header)[key] = value
    if set(header) != {'size', 'filter'} or header['filter'] != 'Linear, Linear':
        raise ValueError('Restore additional original atlas page settings')
    width, height = map(int, header['size'].split(','))
    for name, region in regions.items():
        if set(region) - {'bounds', 'offsets', 'rotate'}: raise ValueError('Unknown original atlas region field')
        x, y, w, h = map(int, region['bounds'].split(','))
        ox, oy, ow, oh = map(int, region.get('offsets', f'0,0,{w},{h}').split(','))
        rotation = int(region.get('rotate', '0'))
        if rotation not in (0, 90): raise ValueError('Restore original atlas region rotation')
        regions[name] = dict(x=x, y=y, w=w, h=h, ox=ox, oy=oy, ow=ow, oh=oh, rotation=rotation)
    return width, height, regions


def setup_mesh(skeleton, atlas):
    if set(skeleton) != {'skeleton', 'bones', 'slots', 'skins', 'animations'}:
        raise ValueError('Restore original skeleton constraints before generating its setup mesh')
    if len(skeleton['skins']) != 1 or skeleton['skins'][0]['name'] != 'default':
        raise ValueError('Restore original initial skin selection')
    width, height, regions = atlas_regions(atlas)
    matrices, bones = {}, []
    def apply(m, x, y):
        a, b, c, d, tx, ty = m
        return a*x+b*y+tx, c*x+d*y+ty
    for bone in skeleton['bones']:
        if set(bone) - {'name', 'parent', 'x', 'y', 'rotation', 'length'}:
            raise ValueError('Restore additional original setup bone transforms')
        angle = math.radians(bone.get('rotation', 0)); c, s = math.cos(angle), math.sin(angle)
        local = c, -s, s, c, bone.get('x', 0), bone.get('y', 0)
        if 'parent' in bone:
            a, b, c, d, tx, ty = matrices[bone['parent']]; e, f, g, h, x, y = local
            local = a*e+b*g, a*f+b*h, c*e+d*g, c*f+d*h, a*x+b*y+tx, c*x+d*y+ty
        matrices[bone['name']] = local; bones.append(local)
    positions, uv, triangles = [], [], []
    attachments = skeleton['skins'][0]['attachments']
    for slot in skeleton['slots']:
        if set(slot) != {'name', 'bone', 'attachment'}:
            raise ValueError('Restore original slot color/blend/setup attachment')
        attachment = attachments[slot['name']][slot['attachment']]
        region = regions[attachment.get('path', attachment.get('name', slot['attachment']))]
        vertex_start = len(positions) // 2
        def atlas_uv(u, v):
            x = u*region['ow']-region['ox']; y = v*region['oh']-(region['oh']-region['oy']-region['h'])
            px, py = (region['x']+y, region['y']+region['w']-x) if region['rotation'] else (region['x']+x, region['y']+y)
            return px/width, 1-py/height
        if attachment.get('type', 'region') == 'region':
            if set(attachment) - {'x', 'y', 'rotation', 'scaleX', 'scaleY', 'width', 'height'}:
                raise ValueError('Restore additional original region setup fields')
            left, bottom = -region['ow']/2+region['ox'], -region['oh']/2+region['oy']
            right, top = left+region['w'], bottom+region['h']
            angle = math.radians(attachment.get('rotation', 0)); c, s = math.cos(angle), math.sin(angle)
            for x, y in ((left,bottom), (left,top), (right,top), (right,bottom)):
                u, v = (x+region['ow']/2)/region['ow'], 1-(y+region['oh']/2)/region['oh']
                uv.extend(atlas_uv(u,v))
                x *= attachment['width']/region['ow']*attachment.get('scaleX',1)
                y *= attachment['height']/region['oh']*attachment.get('scaleY',1)
                positions.extend(apply(matrices[slot['bone']], c*x-s*y+attachment.get('x',0), s*x+c*y+attachment.get('y',0)))
            triangles.extend(vertex_start+i for i in (0,1,2,2,3,0))
        elif attachment['type'] == 'mesh':
            if set(attachment) - {'type','uvs','triangles','vertices','hull','edges','width','height'}:
                raise ValueError('Restore additional original mesh setup fields')
            values = attachment['vertices']; count = len(attachment['uvs'])//2; cursor = 0
            for i in range(count):
                uv.extend(atlas_uv(*attachment['uvs'][i*2:i*2+2]))
                if len(values) == count*2:
                    positions.extend(apply(matrices[slot['bone']], *values[i*2:i*2+2])); cursor += 2
                else:
                    influences = int(values[cursor]); cursor += 1; px, py = 0., 0.
                    for _ in range(influences):
                        bone, x, y, weight = values[cursor:cursor+4]; cursor += 4
                        if bone != int(bone) or not 0 <= bone < len(bones): raise ValueError('Original mesh bone index differs')
                        x, y = apply(bones[int(bone)],x,y); px += x*weight; py += y*weight
                    positions.extend((px,py))
            if cursor != len(values): raise ValueError('Original mesh weights were not fully consumed')
            triangles.extend(vertex_start+i for i in attachment['triangles'])
        else: raise ValueError('Restore original setup attachment type')
    return dict(positions=positions, uv=uv, triangles=triangles)


def build():
    apk = Apk.current(); code = ROOT/'.tools/source-packages/current'/apk.version/'official-code'
    schemas = NativeSchemas(code); proof = install(schemas, code); reader_class, _ = reader_type()
    root = apk.asset('prefabs_ui_worldmap','/6302_DialogIcon.prefab')
    outputs, inputs, components, models, shaders = [], [], [], [], {}
    def output(path, raw):
        write(path,raw); meta(path); outputs.append(dict(path=source_path(path), sha256=sha(raw)))
    def read(obj):
        inputs.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
        return schemas.read(obj)[0]
    def pointer(obj, value): return PPtr(**value,assetsfile=obj.assets_file).deref()
    def visit(obj,path=''):
        go=obj.read(); path+='/'+go.m_Name
        for ref in go.m_Component:
            component=ref.component.deref()
            if component.type.name=='RectTransform':
                for child in component.read().m_Children: visit(child.read().m_GameObject.deref(),path)
            elif component.type.name=='MonoBehaviour' and component.read(check_read=False).m_Script.read().m_ClassName=='SkeletonGraphic':
                fields=read(component); asset=pointer(component,fields['skeletonDataAsset']); data=read(asset)
                components.append(dict(path=path, source=apk.provenance(component), fields=fields, asset=data))
                if fields['startingAnimation'] or fields['initialSkinName'] or fields['layoutScaleMode'] or fields['initialFlipX'] or fields['initialFlipY']:
                    raise ValueError('Restore selected original UI animation/skin/layout before conversion')
                if any(data[v] for v in ('skeletonDataModifiers','fromAnimation','toAnimation','duration','zSlots')):
                    raise ValueError('Restore original skeleton modifiers/mixing')
                skeleton_obj=pointer(asset,data['skeletonJSON']); raw=skeleton_obj.read().m_Script.encode('utf8','surrogateescape')
                inputs.append(dict(source=apk.provenance(skeleton_obj), raw_sha256=sha(raw)))
                reader=reader_class(); skeleton=reader.convert(raw)
                if reader.inp.index != len(raw): raise ValueError('Original skeleton was not fully decoded')
                if len(data['atlasAssets']) != 1: raise ValueError('Restore original multi-atlas UI')
                atlas=pointer(asset,data['atlasAssets'][0]); af=read(atlas); atlas_obj=pointer(atlas,af['atlasFile']); atlas_text=atlas_obj.read().m_Script
                inputs.append(dict(source=apk.provenance(atlas_obj), raw_sha256=sha(atlas_text.encode())))
                if len(af['materials']) != 1: raise ValueError('Restore original multi-material atlas')
                atlas_material=pointer(atlas,af['materials'][0]); material=atlas_material.read()
                texture=next(v.m_Texture.deref() for k,v in material.m_SavedProperties.m_TexEnvs if k=='_MainTex')
                inputs.append(dict(source=apk.provenance(texture), object_sha256=sha(texture.get_raw_data())))
                tex=texture.read(); rgba,mips,profile=decode_mips(tex)
                name=go.m_Name; raw_path=DEST/(name+'.rgba.bytes'); output(raw_path,rgba)
                # Source evidence must not trigger the project's historical
                # Spine editor importer; the independent converter owns assets.
                output(DEST/(name+'.skeleton-source.txt'),encode(skeleton)); output(DEST/(name+'.atlas-source.txt'),atlas_text.encode())
                gm=pointer(component,fields['m_Material']); gm_value=gm.read(); shader=gm_value.m_Shader.deref()
                shader_key=(shader.assets_file.name,shader.path_id)
                if shader_key not in shaders:
                    def render_state(state):
                        text=pass_state(state)
                        # UGUI supplies these properties dynamically for masks.
                        text=re.sub(r'ColorMask [^\n]+','ColorMask [_ColorMask]',text)
                        return text+'\nStencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }'
                    shader_text,variants=translate(shader.read(),'BuildingPreview',program_body=program_body,render_state=render_state)
                    shader_text=shader_text.replace('Zjcs/Official/World/Map13/BuildingPreview','Zjcs/Official/World/Map12/BuildingPreview')
                    shader_path=ROOT/'unity/Assets/Game/Runtime/Offline/World/Map12/BuildingPreview.shader'
                    write(shader_path,shader_text.encode());
                    from prepare_unity_map13_water_components import import_meta
                    import_meta(shader_path,'ShaderImporter')
                    outputs.append(dict(path=source_path(shader_path),sha256=sha(shader_path.read_bytes())))
                    for i,v in enumerate(variants): output(DEST/('shader-'+str(i)+'.glsl.txt'),v['source'].encode())
                    inputs.append(dict(source=apk.provenance(shader),object_sha256=sha(shader.get_raw_data()),variants=[v['keywords'] for v in variants]))
                    shaders[shader_key]=shader_path
                inputs.append(dict(source=apk.provenance(gm),object_sha256=sha(gm.get_raw_data())))
                mesh=setup_mesh(skeleton,atlas_text)
                models.append(dict(path=path,name=name,scale=data['scale'],**mesh,
                    texture=dict(path=source_path(raw_path),width=tex.m_Width,height=tex.m_Height,mips=tex.m_MipCount,
                        srgb=tex.m_ColorSpace==1,filter=tex.m_TextureSettings.m_FilterMode,wrapU=tex.m_TextureSettings.m_WrapU,
                        wrapV=tex.m_TextureSettings.m_WrapV,aniso=tex.m_TextureSettings.m_Aniso,bias=tex.m_TextureSettings.m_MipBias),
                    material=dict(floats=[dict(name=k,value=v) for k,v in gm_value.m_SavedProperties.m_Floats],
                        colors=[dict(name=k,r=v.r,g=v.g,b=v.b,a=v.a) for k,v in gm_value.m_SavedProperties.m_Colors],
                        keywords=(gm_value.m_ShaderKeywords or '').split() if hasattr(gm_value,'m_ShaderKeywords') else gm_value.m_ValidKeywords),
                    color=fields['m_SkeletonColor'],raycast=bool(fields['m_RaycastTarget']),maskable=bool(fields['m_Maskable']),
                    mipSource=dict(mips=mips,profile=profile)))
    visit(root)
    if len(models)!=3: raise ValueError('Original lift preview parts differ')
    output(DEST/'components.json',encode(dict(components=components,schema=proof)))
    output(DEST/'preview.json',encode(dict(manifest=apk.version,mapClassId=12,models=models)))
    meta(DEST,folder=True)
    tools=[__file__,'scripts/map13_native_schemas.py','scripts/map13_entity_native_schemas.py','scripts/convert_official_skeletons.py']
    write(ROOT/'assets/reference/official-map/map12-building-preview-source-manifest.json',encode(dict(manifest=apk.version,
        operation='Decode original 4.2.28 setup bones, region trim/rotation and weighted meshes independently. Preserve empty startingAnimation, source hierarchy, complete texture mips and all six original shader programs. No Spine runtime or sampled frames used.',
        reader=dict(name='SkelToJson',version='1.0.1',license='MIT',wheel_sha256=WHEEL_SHA256),
        inputs=inputs,outputs=outputs,tools=[dict(path=source_path(ROOT/p),sha256=sha((ROOT/p).read_bytes())) for p in tools],
        limitations=['Only the three original 6302 dialog components with no selected animation are converted; this does not implement world-building animation or inactive 6103 SkeletonGraphic.'])))
    print('Map12 original building preview: 3 native setup meshes, '+str(sum(len(v['positions'])//2 for v in models))+' vertices',flush=True)


def record_native_bindings():
    path = ROOT/'assets/reference/official-map/map12-building-preview-source-manifest.json'
    receipt = json.loads(path.read_bytes())
    native = ROOT/'unity/Assets/Official/World/Map/12/BuildingPreview'
    files = sorted(p for p in native.rglob('*') if p.is_file())
    files += [ROOT/'unity/Assets/Game/Resources/Map12/UI/6302_DialogIcon.prefab']
    code = [ROOT/p for p in ('unity/Assets/Game/Runtime/Presentation/World/NativeMap12BuildingGraphic.cs',
        'unity/Assets/Game/Editor/OfficialMap12BuildingPreviewTools.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Buildings.cs')]
    record = lambda p: dict(path=source_path(p),sha256=sha(p.read_bytes()),bytes=p.stat().st_size)
    receipt['nativeBindings'] = [record(p) for p in files]
    receipt['adapters'] = [record(p) for p in code]
    write(path,encode(receipt))


if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--record-native-bindings',action='store_true'); args=parser.parse_args()
    record_native_bindings() if args.record_native_bindings else build()
