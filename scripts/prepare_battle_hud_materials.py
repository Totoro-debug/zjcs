"""Restore the three serialized Image materials in the original FightMainPanel.

Translate their selected GLES vertex/fragment programs mechanically to HLSL,
retaining expression order and all four Unity UI clipping variants. No Spine
runtime or generated placeholder shader is used.
"""
from __future__ import annotations
import hashlib
import io
import json
from pathlib import Path
import re
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_shader_programs import gles_programs
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path
from UnityPy.classes.PPtr import PPtr

DEST = ROOT/'unity/Assets/Official/Combat/HudMaterials'
OUTPUT = DEST/'Resources/Combat/battle-hud-materials.json'
RECEIPT = ROOT/'assets/reference/battle-hud/materials-source-manifest.json'
SHADERS = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/Shaders'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def hlsl(text):
    matrices = {'unity_ObjectToWorld':'unity_ObjectToWorld', 'unity_MatrixVP':'UNITY_MATRIX_VP',
                'glstate_matrix_projection':'UNITY_MATRIX_P'}
    for original, native in matrices.items():
        text = re.sub(r'hlslcc_mtx4x4'+original+r'\[(\d)\]',
                      lambda m:'float4('+','.join(f'{native}[{i}][{m[1]}]' for i in range(4))+')',text)
    text = re.sub(r'\b([biu]?)vec([234])\b',lambda m:dict(b='bool',i='int',u='uint').get(m[1],'float')+m[2],text)
    text = re.sub(r'\bmediump\s+(float[234]?)',lambda m:m[1].replace('float','half'),text)
    text = re.sub(r'\b(mediump|highp|lowp)\s+','',text)
    # GLSL scalar-vector constructors splat; spell them explicitly for HLSL.
    text = re.sub(r'\b(float|half|int|bool)([234])\(([-+\w.]+)\)',
                  lambda m:m[1]+m[2]+'('+','.join([m[3]]*int(m[2]))+')',text)
    text = re.sub(r'\btexture\((\w+), ([^,\n]+), ([^)\n]+)\)',r'tex2Dbias(\1, float4(\2, 0, \3))',text)
    text = re.sub(r'\btexture\(', 'tex2D(', text)
    text = re.sub(r'\bfract\(', 'frac(', text)
    text = re.sub(r'\broundEven\(', 'round(', text)
    # The sole vector comparison in these programs is the stock UGUI gamma branch.
    text = text.replace('lessThan(in_COLOR0.xyzx, float4(0.0725490004, 0.0725490004, 0.0725490004, 0.0))',
                        '(in_COLOR0.xyzx < float4(0.0725490004, 0.0725490004, 0.0725490004, 0.0))')
    for original, native in {'in_POSITION0':'input.vertex','in_TEXCOORD0':'input.uv','in_COLOR0':'input.color',
                             'gl_Position':'output.position','vs_COLOR0':'output.color','vs_TEXCOORD0':'output.uv',
                             'vs_TEXCOORD1':'output.world','vs_TEXCOORD2':'output.mask','SV_Target0':'result'}.items():
        text = re.sub(r'\b'+original+r'\b',native,text)
    return text.replace('discard;', 'clip(-1);')


def stage(program, vertex):
    source = program.split('#ifdef FRAGMENT')[0] if vertex else program.split('#ifdef FRAGMENT')[1]
    head, body = source.split('void main()', 1)
    # Program temporaries are declared after the final input/output declaration.
    end = max(m.end() for m in re.finditer(r'^(?:in|out|layout\([^\n]+\) out) [^;]+;',head,re.M))
    locals_ = head[end:].strip()
    if '#' in locals_ or 'uniform' in locals_:
        raise ValueError('Unexpected declaration layout in official UI shader')
    body = body[:body.rfind('}')+1].strip()[1:-1]
    if vertex:
        body = body.replace('return;', 'return output;')
        declaration = 'Varying vert(Input input) { Varying output = (Varying)0;\n'
    else:
        body = body.replace('return;', 'return result;')
        declaration = 'half4 frag(Varying input) : SV_Target { half4 result = 0;\n'
    code = hlsl(declaration+locals_+'\n'+body+'\n}')
    if not vertex:
        code = code.replace('output.', 'input.')
    uniforms = []
    for m in re.finditer(r'^(?:UNITY_LOCATION\(\d+\)\s+)?uniform\s+((?:(?:mediump|highp)\s+)?(?:float|int|vec[234]|sampler2D))\s+(\w+);', head, re.M):
        name = m[2]
        if name in ('_Time','_ScreenParams'):
            continue
        typ = hlsl(m[1])
        # Original ShaderLab properties are floats; retain the explicit casts in
        # the translated integer temporaries rather than changing their storage.
        if name in ('_SquenceRow','_SquenceColumn'):
            typ = 'float'
        uniforms.append(typ+' '+name+';')
    return code, uniforms


def shader_text(shader, base_keywords):
    variants = gles_programs(shader, with_keywords=True)
    allowed = {'UNITY_UI_CLIP_RECT','UNITY_UI_ALPHACLIP'}
    chosen = [v for v in variants if set(v['keywords'])-allowed == set(base_keywords)]
    if len(chosen) != 4:
        raise ValueError('Restore all selected official UGUI shader variants')
    uniforms, branches = set(), []
    for index, variant in enumerate(sorted(chosen,key=lambda v:len(v['keywords']),reverse=True)):
        optional = set(variant['keywords']) & allowed
        conditions = [('defined('+key+')') if key in optional else ('!defined('+key+')') for key in sorted(allowed)]
        branches.append(('#if ' if index == 0 else '#elif ')+' && '.join(conditions))
        for vertex in (True,False):
            code, declarations = stage(variant['source'], vertex)
            uniforms.update(declarations);branches.append(code)
    branches.append('#endif')
    props = []
    for p in shader.m_ParsedForm.m_PropInfo.m_Props:
        name = p.m_Name
        values = [getattr(p,'m_DefValue_'+str(i)+'_') for i in range(4)]
        if p.m_Type == 4:
            props.append(f'{name} ("{name}", 2D) = "white" {{}}')
        elif p.m_Type in (0,1):
            props.append(name+' ("'+name+'", '+('Color' if p.m_Type == 0 else 'Vector')+') = ('+','.join(map(str,values))+')')
        elif p.m_Type in (2,3):
            props.append(f'{name} ("{name}", Float) = {values[0]}')
        else:
            raise ValueError('Unsupported original UI shader property type')
    state = shader.m_ParsedForm.m_SubShaders[0].m_Passes[0].m_State
    blend_names = ('Zero','One','DstColor','SrcColor','OneMinusDstColor','SrcAlpha','OneMinusSrcColor','DstAlpha','OneMinusDstAlpha','SrcAlphaSaturate','OneMinusSrcAlpha')
    def value(v, names=None):
        if v.name not in ('','<noninit>'):return '['+v.name+']'
        return names[int(v.val)] if names else str(int(v.val))
    blend = state.rtBlend0
    states = '\n'.join(['Cull '+value(state.culling,('Off','Front','Back')),'ZWrite '+value(state.zWrite,('Off','On')),'ZTest '+value(state.zTest),
        'Blend '+value(blend.srcBlend,blend_names)+' '+value(blend.destBlend,blend_names)+', '+value(blend.srcBlendAlpha,blend_names)+' '+value(blend.destBlendAlpha,blend_names),
        'ColorMask '+value(blend.colMask),
        'Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }'])
    name = shader.m_ParsedForm.m_Name.rsplit('/',1)[-1]
    text = 'Shader "Zjcs/Combat/Hud'+name+'" {\nProperties {\n'+'\n'.join(props)+'\n}\nSubShader {\n'
    text += 'Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }\n'+states+'\nPass {\nCGPROGRAM\n'
    text += '#pragma target 3.0\n#pragma vertex vert\n#pragma fragment frag\n#pragma multi_compile_local _ UNITY_UI_CLIP_RECT\n#pragma multi_compile_local _ UNITY_UI_ALPHACLIP\n#include "UnityCG.cginc"\n'
    text += 'struct Input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };\n'
    head = chosen[0]['source'].split('void main()',1)[0]
    outputs = {name:hlsl(typ) for typ,name in re.findall(r'^out ((?:(?:mediump|highp)\s+)?vec[234]) (\w+);',head,re.M)}
    text += 'struct Varying { float4 position : SV_POSITION; '+outputs['vs_COLOR0']+' color : COLOR; '+outputs['vs_TEXCOORD0']+' uv : TEXCOORD0; '+outputs['vs_TEXCOORD1']+' world : TEXCOORD1; '+outputs['vs_TEXCOORD2']+' mask : TEXCOORD2; };\n'
    # Some programs use half and float declarations for the same uniform across
    # stages. The first spelling in the selected compiled stages is preserved.
    unique = {}
    for declaration in sorted(uniforms): unique.setdefault(declaration.split()[-1],declaration)
    text += '\n'.join(unique.values())+'\n'+'\n'.join(branches)+'\nENDCG\n}\n}\n}\n'
    if 'hlslcc_mtx' in text or 'lessThan(' in text:
        raise ValueError('Untranslated original shader operation')
    return name, text, variants


def build():
    apk, schemas = Apk(), ScriptSchemas()
    inputs, materials, bindings, textures, shaders = {}, {}, [], {}, {}
    def record(obj):
        p = dict(apk.provenance(obj),object_sha256=sha(obj.get_raw_data()))
        inputs[(p['bundle'],p['path_id'])] = p
        return obj
    for obj in apk.objects('prefabs_ui_fight_fightmainpanel_fightmainpanel'):
        if obj.type.name != 'MonoBehaviour':continue
        basic = obj.read(check_read=False)
        if not basic.m_Script.path_id or basic.m_Script.read().m_ClassName != 'Image':continue
        fields = schemas.read(obj)
        if not fields['m_Material']['m_PathID']:continue
        record(obj)
        mat_obj = record(PPtr(**fields['m_Material'],assetsfile=obj.assets_file).deref())
        mat = mat_obj.read();key = str(mat_obj.path_id)
        path, _ = object_path(basic.m_GameObject.deref())
        bindings.append(dict(path=path,material=key))
        if key in materials:continue
        shader_obj = record(mat.m_Shader.deref());shader = shader_obj.read();shader_key=str(shader_obj.path_id)
        name, native, variants = shader_text(shader,mat.m_ValidKeywords)
        shader_path = SHADERS/('Hud'+name+'.shader');write(shader_path,native.encode())
        for i, variant in enumerate(variants):write(DEST/'Source/Shaders'/name/(str(i)+'.glsl.txt'),variant['source'].encode())
        shaders[shader_key] = dict(key=shader_key,name=shader.m_ParsedForm.m_Name,path=shader_path.relative_to(ROOT/'unity').as_posix(),
                                  sha256=sha(shader_path.read_bytes()),selectedKeywords=mat.m_ValidKeywords)
        props = mat.m_SavedProperties;texs=[]
        for prop, value in props.m_TexEnvs:
            texture_key = ''
            if value.m_Texture.path_id:
                tex_obj=record(value.m_Texture.deref());tex=tex_obj.read();texture_key=str(tex_obj.path_id)
                stream=io.BytesIO();tex.image.save(stream,format='PNG');target=DEST/'Source/Textures'/(tex.m_Name+'.png');write(target,stream.getvalue())
                if tex.m_MipCount != 1:raise ValueError('Restore all original UI texture mips')
                ts=tex.m_TextureSettings
                textures[texture_key]=dict(key=texture_key,path=target.relative_to(ROOT/'unity').as_posix(),sha256=sha(target.read_bytes()),
                    width=tex.m_Width,height=tex.m_Height,filter=ts.m_FilterMode,wrapU=ts.m_WrapU,wrapV=ts.m_WrapV,
                    aniso=ts.m_Aniso,bias=ts.m_MipBias,sRGB=tex.m_ColorSpace==1)
            texs.append(dict(name=prop,texture=texture_key,scale=[value.m_Scale.x,value.m_Scale.y],offset=[value.m_Offset.x,value.m_Offset.y]))
        materials[key]=dict(key=key,name=mat.m_Name,shader=shader_key,queue=mat.m_CustomRenderQueue,keywords=mat.m_ValidKeywords,
            floats=[dict(name=k,value=v) for k,v in props.m_Floats],colors=[dict(name=k,value=[getattr(v,a) for a in 'rgba']) for k,v in props.m_Colors],textures=texs)
    if len(bindings)!=3:raise ValueError('Recheck the original FightMainPanel material closure')
    data=dict(manifest=apk.version,bindings=bindings,materials=list(materials.values()),textures=list(textures.values()),shaders=list(shaders.values()))
    write(OUTPUT,(json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n').encode())
    outputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in DEST.rglob('*') if p.is_file() and p.suffix!='.meta'}
    outputs.update({'unity/'+v['path']:v['sha256'] for v in shaders.values()})
    write(RECEIPT,(json.dumps(dict(manifest=apk.version,inputs=list(inputs.values()),outputs=outputs,
        generator_sha256=sha(Path(__file__).read_bytes()),
        runtime=[dict(path=str(path.relative_to(ROOT)).replace('\\','/'),sha256=sha(path.read_bytes())) for path in (
            ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudMaterialAssets.cs',
            ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs')],
        operation='Three original FightMainPanel Image material references, original texture and serialized properties. GLES-to-HLSL translation retains selected expressions, matrix columns, precision and all four UI clip variants; unused original variants remain archived.'),ensure_ascii=False,indent=2)+'\n').encode())
    print('Battle HUD materials:',len(materials),'shaders:',len(shaders),'textures:',len(textures))


if __name__=='__main__':build()
