"""Restore Map12's original matching UIMesh geometry, materials and complete texture mips."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT/'.tools/character-art-python'), str(ROOT/'scripts')]
import yaml
import astc_encoder
import UnityPy
from UnityPy.classes.PPtr import PPtr
from official_apk import Apk
from official_unity_schema import ScriptSchemas
from official_native_yaml_verification import documents
from official_shader_programs import gles_programs
from prepare_dungeon_ui_bindings import CODE, VERSION, relative_transform_path
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_map12_matching_animation import encode, equal, sha, source_record, write

DEST = ROOT/'unity/Assets/Official/World/Map/12/Resources/Map12/Matching'
RECEIPT = ROOT/'assets/reference/official-map/map12-matching-mesh-source-manifest.json'
MESH_TEMPLATE = ROOT/'unity/Assets/Official/Home/Effect/models/Cylinder001.asset'
SHADER_RECEIPT = ROOT/'assets/reference/official-map/map13-entity-shaders-source-manifest.json'
NATIVE_PREFAB = ROOT/'unity/Assets/Game/Resources/Dungeons/UI/DailyDungeonDialogPrivateMap.prefab'
GUID = re.compile(r'(?m)^guid: ([a-f0-9]{32})$')


def identity(obj): return obj.assets_file.name.lower()+':'+str(obj.path_id)
def plain(value):
    if isinstance(value, dict): return {key:plain(item) for key,item in value.items()}
    if isinstance(value, (list,tuple)): return [plain(item) for item in value]
    if isinstance(value, bool): return int(value)
    if isinstance(value, bytes): return value.hex() or None
    return value


def native_mesh(original):
    """Use the existing native Mesh schema, without decoding/repacking geometry."""
    fields = documents(MESH_TEMPLATE.read_text(encoding='utf8'))[4300000][1]
    for key,value in original.items():
        if key not in fields: raise ValueError('Unmapped original Mesh field: '+key)
        fields[key] = plain(value)
    fields['m_SubMeshes'] = [dict(serializedVersion=2,**plain(row)) for row in original['m_SubMeshes']]
    fields['m_IndexBuffer'] = bytes(original['m_IndexBuffer']).hex()
    vertex = original['m_VertexData']
    fields['m_VertexData'] = dict(serializedVersion=3,m_VertexCount=vertex['m_VertexCount'],
        m_Channels=plain(vertex['m_Channels']),m_DataSize=len(vertex['m_DataSize']),_typelessdata=bytes(vertex['m_DataSize']).hex())
    fields['m_StreamData'] = dict(serializedVersion=2,**original['m_StreamData'])
    if original['m_BoneNameHashes'] or original['m_VariableBoneCountWeights']['m_Data']:
        raise ValueError('Review original matching mesh bone arrays')
    fields['m_BoneNameHashes'] = None
    fields['m_VariableBoneCountWeights']['m_Data'] = None
    for name in ('m_BakedConvexCollisionMesh','m_BakedTriangleCollisionMesh'):
        if original[name]: raise ValueError('Review original matching mesh collision bytes')
        fields[name] = None
    for vector in fields['m_CompressedMesh'].values():
        if isinstance(vector,dict):
            if vector['m_Data']: raise ValueError('Review original compressed matching geometry')
            vector['m_Data'] = None
    # Cast scalars read by NativeLoader back to standard types before dumping.
    def scalar(value):
        if isinstance(value,dict): return {key:scalar(item) for key,item in value.items()}
        if isinstance(value,list): return [scalar(item) for item in value]
        if isinstance(value,int): return int(value)
        if isinstance(value,float): return float(value)
        return value
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!43 &4300000\n'
    text += yaml.safe_dump({'Mesh':scalar(fields)},sort_keys=False,allow_unicode=True,width=100000)
    restored = documents(text)[4300000][1]
    # Independently reconstruct every source field from the emitted native YAML.
    def recover(expected,actual,path):
        if isinstance(expected,bytes) or path=='Mesh.m_IndexBuffer':
            decoded = bytes.fromhex(str(actual)) if actual is not None else b''
            if bytes(expected)!=decoded: raise ValueError('Matching mesh byte buffer changed: '+path)
        elif isinstance(expected,dict):
            for key,value in expected.items():
                if path=='Mesh.m_VertexData' and key=='m_DataSize':
                    if actual[key]!=len(value): raise ValueError('Matching vertex byte count changed')
                    recover(bytes(value),actual['_typelessdata'],path+'.'+key)
                else: recover(value,actual[key],path+'.'+key)
        elif isinstance(expected,list):
            if not expected and actual is None: return
            if not isinstance(actual,list) or len(expected)!=len(actual): raise ValueError('Matching mesh array changed: '+path)
            for i,(a,b) in enumerate(zip(expected,actual)): recover(a,b,path+'['+str(i)+']')
        else: equal(expected,actual,path)
    recover(original,restored,'Mesh')
    return text.encode('utf8')


def build(update=False):
    apk = Apk.current()
    if apk.version!=VERSION: raise ValueError('Unexpected matching mesh source version')
    schemas = ScriptSchemas(code_root=CODE)
    root = apk.asset('prefabs_ui_dailydungeon','/DailyDungeonDialogPrivateMap.prefab').read()
    host,host_path,host_names,_ = next(row for row in relative_transform_path(root) if row[2][-3:]==('NormalGo','GameMatch','Content'))
    native_docs = documents(NATIVE_PREFAB.read_text(encoding='utf8'))
    native_root, = [key for key,(kind,fields) in native_docs.items() if kind=='GameObject' and fields['m_Name']==root.m_Name]
    def native_node(indices,names):
        go_id=native_root;actual_names=[native_docs[go_id][1]['m_Name']]
        for index in indices:
            go=native_docs[go_id][1]
            transform, = [native_docs[pair['component']['fileID']][1] for pair in go['m_Component']
                          if native_docs[pair['component']['fileID']][0] in ('Transform','RectTransform')]
            child=native_docs[transform['m_Children'][index]['fileID']][1]
            go_id=child['m_GameObject']['fileID'];actual_names.append(native_docs[go_id][1]['m_Name'])
        if actual_names!=list(names): raise ValueError('Original matching native hierarchy differs')
        return go_id,native_docs[go_id][1]
    shader_proof=json.loads(SHADER_RECEIPT.read_bytes())
    shader_input, = [row for row in shader_proof['inputs'] if row['original']['name']=='UI/EffectDistort']
    shader_output, = [row for row in shader_proof['outputs'] if row['original_shader']=='UI/EffectDistort']
    shader_path=ROOT/shader_output['path']
    if sha(shader_path.read_bytes())!=shader_output['sha256']: raise ValueError('Verified Windows UI/EffectDistort changed')
    shader_guid=GUID.search(Path(str(shader_path)+'.meta').read_text(encoding='utf8'))[1]
    shader_name=re.search(r'Shader\s+"([^"]+)"',shader_path.read_text(encoding='utf8'))[1]
    outputs=[];natives={};textures=[];meshes=[];materials=[];bindings=[];shader_checked=False
    texture_template=min((ROOT/'unity/Assets/Official/World/Map/13/NativeScene').rglob('*.texture.asset'),key=lambda p:p.stat().st_size)
    template=texture_template.read_text(encoding='utf8').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
    def save(path,raw,file_id):
        guid=sha(('zjcs-map12-matching-native:'+path.relative_to(DEST).as_posix()).encode())[:32]
        metadata=('fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(file_id)+'\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
        write(path,raw,update);write(Path(str(path)+'.meta'),metadata,update)
        outputs.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(raw),meta_sha256=sha(metadata)))
        return dict(fileID=file_id,guid=guid,type=2)
    def ptr(value): return '{'+', '.join(key+': '+str(item) for key,item in value.items())+'}'
    def texture_asset(obj):
        key=identity(obj)
        if key in natives:return natives[key]
        texture=obj.read(check_read=True);rgba,mips,profile=decode_mips(texture)
        text=native_texture(template,texture,rgba).decode()
        for name,value in dict(m_TextureFormat=4,m_Name=json.dumps(texture.m_Name,ensure_ascii=False),
            m_ForcedFallbackFormat=texture.m_ForcedFallbackFormat,m_DownscaleFallback=int(texture.m_DownscaleFallback),
            m_IsAlphaChannelOptional=int(texture.m_IsAlphaChannelOptional)).items():
            text,count=re.subn(r'(?m)^(  '+name+r':)[^\n]*',lambda m:m[1]+' '+str(value),text)
            if count!=1:raise ValueError('Original matching texture schema differs: '+name)
        text=re.sub(r'(?m)[ \t]+$','',text)
        path=DEST/'Textures'/(sha(key.encode())[:16]+'.texture.asset')
        natives[key]=save(path,text.encode(),2800000)
        textures.append(dict(identity=key,**source_record(apk,obj),path=path.relative_to(ROOT).as_posix(),
            width=texture.m_Width,height=texture.m_Height,mipCount=texture.m_MipCount,profile=profile,mips=mips,
            source_pixels_sha256=sha(texture.get_image_data()),rgba_sha256=sha(rgba),
            filter=texture.m_TextureSettings.m_FilterMode,wrapU=texture.m_TextureSettings.m_WrapU,
            wrapV=texture.m_TextureSettings.m_WrapV,wrapW=texture.m_TextureSettings.m_WrapW,
            aniso=texture.m_TextureSettings.m_Aniso,mipBias=texture.m_TextureSettings.m_MipBias,
            colorSpace=texture.m_ColorSpace,readable=texture.m_IsReadable))
        return natives[key]
    def material_asset(obj):
        nonlocal shader_checked
        key=identity(obj)
        if key in natives:return natives[key]
        source=obj.read_typetree();shader=obj.read().m_Shader.deref()
        if identity(shader)!=shader_input['original']['cab'].lower()+':'+shader_input['original']['path_id'] or sha(shader.get_raw_data())!=shader_input['original']['payload_sha256']:
            raise ValueError('Original matching material uses a different shader')
        if not shader_checked:
            actual={row['index']:row for row in gles_programs(shader.read(),with_keywords=True)}
            if set(actual)!={row['index'] for row in shader_input['programs']}:raise ValueError('Original UI shader program set changed')
            for row in shader_input['programs']:
                if actual[row['index']]['keywords']!=row['keywords'] or sha(actual[row['index']]['source'].encode())!=row['sha256']:
                    raise ValueError('Original matching shader program changed')
                if sha((ROOT/row['path']).read_bytes())!=row['sha256']:raise ValueError('Recorded original matching shader source changed')
            shader_checked=True
        exported=ROOT/'unity/Assets/Official/Dungeons/UIEffect/Material'/(source['m_Name']+'.mat')
        text=exported.read_text(encoding='utf8');data=documents(text)[2100000][1]
        def compare(a,b,path):
            if isinstance(a,list) and isinstance(b,dict):a=dict(a)
            if isinstance(a,dict):
                for k,v in a.items():compare(v,b[k],path+'.'+k)
            else:equal(a,b,path)
        for name,value in source.items():
            if name not in ('m_Shader','m_SavedProperties'):compare(value,data[name],'Material.'+name)
        props=source['m_SavedProperties'];native_props=data['m_SavedProperties']
        for name,value in props.items():
            if name!='m_TexEnvs':compare(value,native_props[name],'Material.'+name)
        if set(dict(props['m_TexEnvs']))!=set(native_props['m_TexEnvs']):raise ValueError('Original matching texture slots changed')
        for name,value in props['m_TexEnvs']:
            for field in ('m_Scale','m_Offset'):compare(value[field],native_props['m_TexEnvs'][name][field],name+'.'+field)
            pointer=value['m_Texture']
            target=dict(fileID=0) if not pointer['m_PathID'] else texture_asset(PPtr(**pointer,assetsfile=obj.assets_file).deref())
            text,count=re.subn(r'(?m)(^      '+re.escape(name)+r':\n        m_Texture: )[^\n]*',lambda m:m[1]+ptr(target),text)
            if count!=1:raise ValueError('Cannot bind original matching texture '+name)
        text,count=re.subn(r'(?m)^(  m_Shader: )[^\n]*',lambda m:m[1]+ptr(dict(fileID=4800000,guid=shader_guid,type=3)),text)
        if count!=1:raise ValueError('Cannot bind original matching shader')
        path=DEST/'Materials'/(source['m_Name']+'.mat');natives[key]=save(path,text.encode(),2100000)
        materials.append(dict(identity=key,**source_record(apk,obj),fields=source,path=path.relative_to(ROOT).as_posix(),
            native_input=dict(path=exported.relative_to(ROOT).as_posix(),sha256=sha(exported.read_bytes()))))
        return natives[key]
    for go,indices,names,_ in relative_transform_path(host):
        for pair in go.m_Component:
            obj=pair.component.deref()
            if obj.type.name!='MonoBehaviour' or obj.read(check_read=False).m_Script.read().m_ClassName!='UIMesh':continue
            fields=schemas.read(obj);renderer=PPtr(**fields['mMeshRenderer'],assetsfile=obj.assets_file).deref();rf=renderer.read_typetree()
            if set(fields)!={'m_GameObject','m_Enabled','m_Script','m_Name','m_Material','m_Color','m_RaycastTarget',
                'm_RaycastPadding','m_Maskable','m_OnCullStateChanged','mMeshRenderer','Mesh'} or fields['m_OnCullStateChanged']['m_PersistentCalls']['m_Calls']:
                raise ValueError('Unmapped original UIMesh field or culling event')
            if fields['m_GameObject']!=rf['m_GameObject'] or fields['m_Material']!=rf['m_Materials'][0] or len(rf['m_Materials'])!=1:
                raise ValueError('Original UIMesh renderer/material reference differs')
            go_id,native_go=native_node(host_path+indices,host_names+names[1:])
            renderer_native, = [native_docs[row['component']['fileID']][1] for row in native_go['m_Component'] if native_docs[row['component']['fileID']][0]=='MeshRenderer']
            missing=[]
            for name,value in rf.items():
                if name in ('m_GameObject','m_Materials'):continue
                if name not in renderer_native:missing.append(name);continue
                if isinstance(value,dict) and set(value)=={'m_FileID','m_PathID'}:
                    if any(value.values()) or renderer_native[name]['fileID']:raise ValueError('Unbound matching renderer reference')
                else:equal(value,renderer_native[name],'Renderer.'+name)
            build_only_defaults=dict(m_LightmapIndex=65535,m_LightmapIndexDynamic=65535,
                m_LightmapTilingOffset=dict(x=1.,y=1.,z=0.,w=0.),m_LightmapTilingOffsetDynamic=dict(x=1.,y=1.,z=0.,w=0.),
                m_EnlightenVertexStream=dict(m_FileID=0,m_PathID=0))
            if set(missing)-set(build_only_defaults):raise ValueError('Original matching renderer settings missing: '+str(missing))
            for name in missing:equal(build_only_defaults[name],rf[name],'Renderer.build-only.'+name)
            mesh=PPtr(**fields['Mesh'],assetsfile=obj.assets_file).deref();key=identity(mesh);mf=mesh.read_typetree()
            if key not in natives:
                path=DEST/'Meshes'/'Cylinder001.asset';natives[key]=save(path,native_mesh(mf),4300000)
                meshes.append(dict(identity=key,**source_record(apk,mesh),path=path.relative_to(ROOT).as_posix(),vertexCount=mf['m_VertexData']['m_VertexCount'],
                    vertices_sha256=sha(bytes(mf['m_VertexData']['m_DataSize'])),indices_sha256=sha(bytes(mf['m_IndexBuffer']))))
            material=PPtr(**fields['m_Material'],assetsfile=obj.assets_file).deref();material_asset(material)
            bindings.append(dict(path='/'.join(map(str,indices)),objects=list(names),enabled=bool(fields['m_Enabled']),
                color=[fields['m_Color'][c] for c in ('r','g','b','a')],raycastTarget=bool(fields['m_RaycastTarget']),
                raycastPadding=[fields['m_RaycastPadding'][c] for c in ('x','y','z','w')],maskable=bool(fields['m_Maskable']),
                mesh=dict(resource='Map12/Matching/Meshes/Cylinder001',name=mf['m_Name'],vertexCount=mf['m_VertexData']['m_VertexCount'],
                    indexCount=sum(sub['indexCount'] for sub in mf['m_SubMeshes'])),
                material=dict(resource='Map12/Matching/Materials/'+material.read().m_Name,name=material.read().m_Name,shader=shader_name),
                renderer=dict(enabled=rf['m_Enabled'],fields=rf,native_runtime_only_unserialized=missing,**source_record(apk,renderer)),**source_record(apk,obj)))
    if len(bindings)!=2 or len(meshes)!=1 or len(materials)!=2:raise ValueError('Original matching mesh component count changed')
    for folder in (DEST/'Meshes',DEST/'Materials',DEST/'Textures'):
        raw=('fileFormatVersion: 2\nguid: '+sha(('zjcs-map12-matching-folder:'+folder.name).encode())[:32]+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
        path=Path(str(folder)+'.meta');write(path,raw,update);outputs.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(raw)))
    references=[MESH_TEMPLATE,texture_template,NATIVE_PREFAB,SHADER_RECEIPT,shader_path,Path(str(shader_path)+'.meta')]
    codes=[CODE/'launch-source'/name for name in ('UIMesh.cs','UIMeshUtils.cs')]
    receipt=dict(manifest=VERSION,mapClassId=12,binding=bindings,meshes=meshes,materials=materials,textures=textures,outputs=outputs,
        references=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in references],
        shader=dict(original=shader_input['original'],programs=shader_input['programs'],output=shader_output),
        code_sources=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in codes],
        generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(),sha256=sha(Path(__file__).read_bytes())),
        decoder=dict(unitypy_version=UnityPy.__version__,package='astc_encoder',
            binaries=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes()))
                      for p in sorted(Path(astc_encoder.__file__).parent.rglob('*.pyd'))]),
        serialization_adaptations=['Retain the native Mesh template editor-only m_MeshOptimizationFlags; every field present in the APK Mesh is replaced and independently checked.',
            'Build-only renderer lightmap indices/offsets and Enlighten pointer are absent in the native prefab; require the original unbaked/default/null values.'],
        operation='Read the selected APK/CAB objects before verified downloaded cloud fallback. Preserve every Mesh field and vertex/UV/index buffer; use the existing native Mesh serialization schema. Verify original material fields against native exports, rebind all exact textures with every ASTC mip decoded to RGBA32 using the original color-space profile and sampler settings; trim trailing whitespace on empty native YAML fields. Reuse the exact source-hash-matched Windows UI/EffectDistort conversion and verify all four original GLES variants. Retain the existing original hierarchy and MeshRenderer settings; restore original UIMesh and UIMeshUtils execution.',
        limitations=['Unity import, actual matching rendering/animation/masking, input and Windows Player validation remain pending while automatic runtime acceptance is paused.'])
    write(RECEIPT,encode(receipt),update)
    print(json.dumps(dict(components=len(bindings),meshes=len(meshes),materials=len(materials),textures=len(textures),mips=sum(len(t['mips']) for t in textures)),ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--update',action='store_true')
    build(parser.parse_args().update)
