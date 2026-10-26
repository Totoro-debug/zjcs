"""Translate the selected original world-UI GLES programs to Unity desktop HLSL."""
import re
from prepare_battle_hud_materials import hlsl
from official_shader_programs import gles_programs


def translated(text):
    text = re.sub(r'hlslcc_mtx4x4unity_WorldToObject\[(\d)\]',
                  lambda m: 'float4('+','.join(f'unity_WorldToObject[{i}][{m[1]}]' for i in range(4))+')', text)
    text = hlsl(text)
    for source, target in {'in_NORMAL0': 'input.normal', 'in_TEXCOORD1': 'input.uv1',
                           'in_TANGENT0': 'input.tangent', 'vs_COLOR1': 'output.color1'}.items():
        text = re.sub(r'\b'+source+r'\b', target, text)
    for source, target in {'inversesqrt': 'rsqrt', 'dFdx': 'ddx', 'dFdy': 'ddy', 'mix': 'lerp'}.items():
        text = re.sub(r'\b'+source+r'\(', target+'(', text)
    return text


def stage(program, vertex):
    source = program.split('#ifdef FRAGMENT')[0] if vertex else program.split('#ifdef FRAGMENT')[1]
    head, body = source.split('void main()', 1)
    end = max(m.end() for m in re.finditer(r'^(?:in|out|layout\([^\n]+\) out) [^;]+;', head, re.M))
    locals_ = head[end:].strip()
    if '#' in locals_ or 'uniform' in locals_:
        raise ValueError('Unexpected original world-UI declaration layout')
    body = body[:body.rfind('}')+1].strip()[1:-1]
    body = body.replace('return;', 'return output;' if vertex else 'return result;')
    signature = 'Varying vert(Input input) { Varying output = (Varying)0;\n' if vertex else 'half4 frag(Varying input) : SV_Target { half4 result = 0;\n'
    code = translated(signature+locals_+'\n'+body+'\n}')
    if not vertex:
        code = code.replace('output.', 'input.')
    builtin = {'_Time', '_ScreenParams', '_ProjectionParams', '_WorldSpaceCameraPos', 'unity_OrthoParams'}
    uniforms = []
    pattern = r'\b(?:uniform|UNITY_UNIFORM)\s+((?:(?:mediump|highp|lowp)\s+)?(?:float|int|vec[234]|sampler2D))\s+(\w+)(\[\d+\])?;'
    for m in re.finditer(pattern, head):
        name = m[2]
        if name in builtin or name.startswith(('hlslcc_', 'Xhlslcc_')):
            continue
        typ = translated(m[1])
        if name in ('_SquenceRow', '_SquenceColumn'):
            typ = 'float'
        uniforms.append(typ+' '+name+(m[3] or '')+';')
    return code, uniforms


def shader_text(shader, name, optional_keywords):
    available = gles_programs(shader, with_keywords=True)
    first_pass = shader.m_ParsedForm.m_SubShaders[0].m_Passes[0]
    indexes = set()
    for pipeline_stage in (first_pass.progVertex, first_pass.progFragment):
        indexes.update(p.m_BlobIndex for p in pipeline_stage.m_SubPrograms)
        for platform in getattr(pipeline_stage, 'm_PlayerSubPrograms', []) or []:
            indexes.update(p.m_BlobIndex for p in platform)
    available = [v for v in available if v['index'] in indexes]
    allowed = set(optional_keywords)
    chosen = [v for v in available if set(v['keywords']) <= allowed]
    by_keywords = {}
    for variant in chosen:
        key = tuple(variant['keywords'])
        if key in by_keywords:
            raise ValueError('Ambiguous original world-UI program: '+shader.m_ParsedForm.m_Name)
        by_keywords[key] = variant
    if len(chosen) != 2**len(allowed):
        raise ValueError('Incomplete original world-UI shader variants')
    branches, uniforms = [], set()
    for index, variant in enumerate(sorted(chosen, key=lambda v: len(v['keywords']), reverse=True)):
        if allowed:
            conditions = [('defined('+key+')') if key in variant['keywords'] else ('!defined('+key+')') for key in sorted(allowed)]
            branches.append(('#if ' if index == 0 else '#elif ')+' && '.join(conditions))
        for vertex in (True, False):
            code, declarations = stage(variant['source'], vertex)
            branches.append(code)
            uniforms.update(declarations)
    if allowed:
        branches.append('#endif')
    props = []
    for prop in shader.m_ParsedForm.m_PropInfo.m_Props:
        values = [getattr(prop, 'm_DefValue_'+str(i)+'_') for i in range(4)]
        if prop.m_Type == 4:
            value = '2D) = "white" {}'
        elif prop.m_Type in (0, 1):
            value = ('Color' if prop.m_Type == 0 else 'Vector')+') = ('+','.join(map(str, values))+')'
        elif prop.m_Type in (2, 3):
            value = 'Float) = '+str(values[0])
        else:
            raise ValueError('Unsupported original world-UI property')
        props.append(prop.m_Name+' ("'+prop.m_Name+'", '+value)
    state = shader.m_ParsedForm.m_SubShaders[0].m_Passes[0].m_State
    def value(entry, names=None):
        if entry.name not in ('', '<noninit>'):
            return '['+entry.name+']'
        return names[int(entry.val)] if names else str(int(entry.val))
    blends = ('Zero','One','DstColor','SrcColor','OneMinusDstColor','SrcAlpha','OneMinusSrcColor','DstAlpha','OneMinusDstAlpha','SrcAlphaSaturate','OneMinusSrcAlpha')
    blend = state.rtBlend0
    states = ['Cull '+value(state.culling, ('Off','Front','Back')), 'ZWrite '+value(state.zWrite, ('Off','On')),
              'ZTest '+value(state.zTest, ('Off','Never','Less','Equal','LEqual','Greater','NotEqual','GEqual','Always')),
              'Blend '+value(blend.srcBlend, blends)+' '+value(blend.destBlend, blends)+', '+value(blend.srcBlendAlpha, blends)+' '+value(blend.destBlendAlpha, blends),
              'ColorMask '+value(blend.colMask, tuple(''.join(c for c,bit in [('R',8),('G',4),('B',2),('A',1)] if mask & bit) or '0' for mask in range(16)))]
    names = {p.m_Name for p in shader.m_ParsedForm.m_PropInfo.m_Props}
    if '_Stencil' in names:
        states.append('Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }')
    heads = '\n'.join(v['source'].split('void main()', 1)[0] for v in chosen)
    interface = sorted(set(re.findall(r'^in ((?:(?:mediump|highp)\s+)?vec[234]) (in_\w+);', heads, re.M)))
    fields = {'POSITION0':'vertex','NORMAL0':'normal','COLOR0':'color','TEXCOORD0':'uv','TEXCOORD1':'uv1','TANGENT0':'tangent'}
    inputs = []
    for typ, field in interface:
        semantic = field.removeprefix('in_')
        inputs.append(translated(typ)+' '+fields[semantic]+' : '+semantic+';')
    interface = sorted(set(re.findall(r'^out ((?:(?:mediump|highp)\s+)?vec[234]) (vs_\w+);', heads, re.M)))
    fields = {'COLOR0':'color','COLOR1':'color1','TEXCOORD0':'uv','TEXCOORD1':'world','TEXCOORD2':'mask'}
    outputs = [translated(typ)+' '+fields[field.removeprefix('vs_')]+' : '+field.removeprefix('vs_')+';' for typ, field in interface]
    text = 'Shader "Zjcs/Combat/World'+name+'" {\nProperties {\n'+'\n'.join(props)+'\n}\nSubShader {\nTags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }\n'
    text += '\n'.join(states)+'\nPass {\nCGPROGRAM\n#pragma target 3.0\n#pragma vertex vert\n#pragma fragment frag\n'
    text += '\n'.join('#pragma multi_compile_local _ '+key for key in sorted(allowed))+'\n#include "UnityCG.cginc"\n'
    text += 'struct Input { '+' '.join(inputs)+' };\nstruct Varying { float4 position : SV_POSITION; '+' '.join(outputs)+' };\n'
    text += '\n'.join(sorted(uniforms))+'\n'+'\n'.join(branches)+'\nENDCG\n}\n}\n}\n'
    return text, [{'index': v['index'], 'keywords': v['keywords']} for v in chosen]
