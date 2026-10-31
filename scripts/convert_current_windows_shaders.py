"""Translate every current raster shader's retained GLES3 executable cases."""
import argparse
from collections import defaultdict
from copy import copy
import gc
import hashlib
import json
from pathlib import Path
import re

import UnityPy
from UnityPy.export.ShaderConverter import ConvertSerializedProperties

from prepare_current_asset_inventory import sha
from prepare_unity_bundle_inputs import serialized_files
from official_platform_shader_programs import platform_shader, gles3_programs
from prepare_unity_map13_entity_shaders import program_body, referenced_programs
from prepare_unity_map13_environment_shader import render_state
from prepare_unity_map13_pipeline_shaders import program_structure
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map13_water_shaders import structure
from prepare_battle_hud_materials import hlsl
from official_apk import ROOT
from current_shader_programs import programs as original_programs
from current_vulkan_shader import programs as vulkan_programs, translate as translate_vulkan


def calls(text, function, transform):
    pattern = re.compile(r'\b' + re.escape(function) + r'\(')
    at = 0
    while (match := pattern.search(text, at)):
        start = match.end()
        depth, args, last = 0, [], start
        for end in range(start, len(text)):
            char = text[end]
            if char == '(':
                depth += 1
            elif char == ')':
                if depth == 0:
                    args.append(text[last:end].strip())
                    break
                depth -= 1
            elif char == ',' and depth == 0:
                args.append(text[last:end].strip())
                last = end + 1
        else:
            raise ValueError('Unclosed original GPU call: ' + function)
        value = transform(args)
        text = text[:match.start()] + value + text[end + 1:]
        at = match.start() + len(value)
    return text


def extended_body(program, vertex):
    program = program.split('#ifdef GEOMETRY', 1)[0]
    program = re.sub(r'(?m)^layout\(location\s*=\s*\d+\)\s+', '', program)
    program = program.replace('gs_TEXCOORD0', 'vs_TEXCOORD0')
    program = re.sub(r'(?m)^flat (in|out) ', r'\1 ', program)
    instance_varying = 'vs_SV_InstanceID0' in program
    program = program.replace('vs_SV_InstanceID0', 'vs_TEXCOORD7')
    if 'samplerExternalOES' in program:
        stage_source = program.split('#ifdef FRAGMENT')[0] if vertex else program.split('#ifdef FRAGMENT')[1]
        body = stage_source.rsplit('void main()', 1)[1]
        body = body[body.index('{') + 1:body.rfind('}')]
        varying = 'textureCoordSplit' if 'textureCoordSplit' in stage_source else 'textureCoord'
        body = body.replace('gl_ModelViewProjectionMatrix * gl_Vertex', 'UnityObjectToClipPos(input.vertex)')
        body = body.replace('TRANSFORM_TEX_ST(gl_MultiTexCoord0, _MainTex_ST)', '(input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw)')
        body = body.replace('gl_MultiTexCoord0', 'input.uv').replace('gl_Position', 'output.position').replace('gl_FragColor', 'result')
        body = re.sub(r'\b' + varying + r'\b', ('output.' if vertex else 'input.') + varying, body)
        body = body.replace('texture2D(', 'tex2D(')
        helper = ''
        if not vertex:
            start = stage_source.index('vec4 AdjustForColorSpace')
            end = stage_source.index('varying ', start)
            helper = hlsl(stage_source[start:end])
        declaration = 'Varying vert(Input input) { Varying output = (Varying)0;\n' if vertex else 'half4 frag(Varying input):SV_Target { half4 result = 0;\n'
        return helper + declaration + hlsl(body) + ('\nreturn output;\n}' if vertex else '\nreturn result;\n}'), ['float4 _MainTex_ST;', 'sampler2D _MainTex;']
    # Preserve GLES column indexing for matrices not covered by the earlier
    # module converters; keep the original uniform name for Unity's binding.
    native_uniforms = []
    native_types = []
    for name, size in re.findall(r'struct\s+(\w+)\s*\{\s*uint\[(\d+)\]\s+value;\s*\};', program):
        native_types.append('struct ' + name + ' { uint value[' + size + ']; };')
    for name, kind, data in re.findall(r'layout\(std430,\s*binding\s*=\s*\d+\)\s+readonly buffer\s+(\w+)\s*\{\s*(\w+)\s+(\w+)\[\];\s*\};', program):
        native_uniforms.append('StructuredBuffer<' + kind + '> ' + name + ';')
        program = re.sub(r'\b' + re.escape(data) + r'\b', name, program)
    def flatten_buffer(match):
        fields = re.sub(r'\bUNITY_UNIFORM\s*', '', match[1])
        fields = re.sub(r'(?m)^\s*#(?:if HLSLCC_ENABLE_UNIFORM_BUFFERS|endif)\s*$', '', fields)
        return '\n'.join('uniform ' + line.strip() + ';' for line in fields.split(';') if line.strip())
    program = re.sub(r'(?:UNITY_BINDING\(\d+\)\s+)?uniform\s+\w+\s*\{([^{}]*)\};', flatten_buffer, program)
    for prefix, native in [('', 'float'), ('i', 'int'), ('u', 'uint'), ('b', 'bool')]:
        for size in (2, 3, 4):
            kind = prefix + 'vec' + str(size)
            program = calls(program, kind, lambda args, k=kind, n=native + str(size):
                            'ZjcsCast' + {'float':'F','int':'I','uint':'U','bool':'B'}[n[:-1]] + n[-1] + '(' + args[0] + ')' if len(args) == 1 else k + '(' + ','.join(args) + ')')
    program = re.sub(r'(?m)^\s+((?:UNITY_UNIFORM|uniform)\s+)', r'\1', program)
    for original, native in [('floatBitsToUint', 'asuint'), ('floatBitsToInt', 'asint'), ('uintBitsToFloat', 'asfloat'), ('intBitsToFloat', 'asfloat'), ('dFdx', 'ddx'), ('dFdy', 'ddy')]:
        program = re.sub(r'\b' + original + r'\(', native + '(', program)
    view_id = 'gl_ViewID_OVR' in program
    program = program.replace('gl_ViewID_OVR', 'ZjcsOriginalViewID')
    blend_index = 'vs_BLENDINDICES0' in program
    program = program.replace('vs_BLENDINDICES0', 'vs_TEXCOORD6')
    handled = {'unity_ObjectToWorld', 'unity_MatrixVP', 'unity_MatrixV',
               'glstate_matrix_projection', 'unity_WorldToObject', '_MainLightWorldToLight', '_LightInvMatrix'}
    editor = Path(json.loads((ROOT / 'unity-tools.json').read_bytes())['editorExecutable'])
    variables = (editor.parent / 'Data/CGIncludes/UnityShaderVariables.cginc').read_text(encoding='utf-8-sig')
    builtins = set(re.findall(r'\bfloat4x4\s+(\w+)', variables))
    precision = r'(?:(?:highp|mediump|lowp)\s+)?'
    declarations = re.findall(r'(?m)^\s*(?:UNITY_UNIFORM|uniform)\s+' + precision + r'vec4\s+hlslcc_mtx4x4(\w+)\[(\d+)\];\s*$', program)
    for name, count_text in declarations:
        if name in handled:
            continue
        count = int(count_text)
        if count % 4:
            raise ValueError('Unexpected matrix column count: ' + name)
        token = 'hlslcc_mtx4x4' + name
        program = re.sub(r'(?m)^\s*(?:UNITY_UNIFORM|uniform)\s+' + precision + r'vec4\s+' + re.escape(token) + r'\[\d+\];\s*$', '', program)
        def column(match):
            index = match[1]
            array = '' if count == 4 else '[(' + index + ')/4]'
            col = index if count == 4 else '((' + index + ')%4)'
            return 'vec4(' + ','.join(name + array + '[' + str(row) + '][' + col + ']' for row in range(4)) + ')'
        program = re.sub(re.escape(token) + r'\[([^\[\]]+)\]', column, program)
        if name not in builtins:
            native_uniforms.append('float4x4 ' + name + ('[' + str(count // 4) + ']' if count > 4 else '') + ';')
    color_slots = {}
    for color in sorted(set(re.findall(r'\b(?:vs|in)_COLOR([1-9]\d*)\b', program))):
        color_slot = next((i for i in (7, 6, 5, 4, 3) if not re.search(r'\b(?:vs|in)_TEXCOORD' + str(i) + r'\b', program)), None)
        if color_slot is None:
            raise ValueError('Additional color needs an explicit interpolator')
        color_slots[color] = color_slot
        program = program.replace('vs_COLOR' + color, 'vs_TEXCOORD' + str(color_slot)).replace('in_COLOR' + color, 'in_TEXCOORD' + str(color_slot))
    layer = 'gl_Layer' in program
    instance = 'gl_InstanceID' in program
    facing = 'gl_FrontFacing' in program
    program = program.replace('gl_Layer', 'ZjcsOriginalLayer').replace('gl_InstanceID', 'ZjcsOriginalInstance').replace('gl_FrontFacing', 'ZjcsOriginalFrontFace')
    code, uniforms = program_body(program, vertex)
    for name, binding in re.findall(r'writeonly\s+layout\(binding=(\d+),\s*r32f\)\s+highp\s+uniform\s+image2D\s+(\w+);', program):
        native_uniforms.append('RWTexture2D<float> ' + binding + ' : register(u' + name + ');')
        code = calls(code, 'imageStore', lambda a, n=binding: n + '[' + a[1] + '] = (' + a[2] + ').x' if a[0] == n else 'imageStore(' + ','.join(a) + ')')
    suffix = 'Vertex' if vertex else 'Fragment'
    helpers = []
    if 'packHalf2x16(' in code or 'unpackHalf2x16(' in code:
        helpers += ['uint ZjcsPackHalf' + suffix + '(float2 v) { return (f32tof16(v.x) & 65535u) | (f32tof16(v.y) << 16u); }',
                    'float2 ZjcsUnpackHalf' + suffix + '(uint v) { return float2(f16tof32(v & 65535u),f16tof32(v >> 16u)); }']
        code = code.replace('unpackHalf2x16(', 'ZjcsUnpackHalf' + suffix + '(').replace('packHalf2x16(', 'ZjcsPackHalf' + suffix + '(')
    if 'bitfieldInsert(' in code:
        helpers.append('uint ZjcsInsertBits' + suffix + '(uint baseValue,uint inserted,int offset,int bits) { uint mask = bits == 0 ? 0u : bits == 32 ? 0xffffffffu : ((1u << bits)-1u); mask <<= offset; return (baseValue & ~mask) | ((inserted << offset) & mask); }')
        code = code.replace('bitfieldInsert(', 'ZjcsInsertBits' + suffix + '(')
    for helper in re.findall(r'(?:uint|int)\s+(?:uint_bitfieldExtract|int_bitfieldInsert)\([^{}]*\)\s*\{[^{}]*\}', code):
        code = code.replace(helper, '')
        name = re.search(r'\b(\w+)\(', helper)[1]
        helper = helper.replace(name, name + suffix)
        code = code.replace(name + '(', name + suffix + '(')
        helpers.append(helper)
    code = '\n'.join(helpers) + ('\n' if helpers else '') + code
    native_uniforms += native_types
    for kind, name, array in re.findall(r'(?m)^(?:UNITY_UNIFORM|uniform)\s+(?:(?:highp|mediump|lowp)\s+)?([biu]vec[234])\s+(\w+)(\[\d+\])?;', program):
        native_uniforms.append(hlsl(kind) + ' ' + name + array + ';')
    code = re.sub(r'\bZjcsCast(F|I|U|B)([234])\(', lambda m:'(' + {'F':'float','I':'int','U':'uint','B':'bool'}[m[1]] + m[2] + ')(', code)
    code = re.sub(r'(?m)^flat (?:in|out) [^;]+;\s*$', '', code)
    for name in set(re.findall(r'\buniform\s+(?:(?:highp|mediump|lowp)\s+)?sampler2DArray\s+(\w+)', program)):
        native_uniforms += ['Texture2DArray<float4> ' + name + ';', 'SamplerState sampler' + name + ';']
        code = calls(code, 'tex2D', lambda a, n=name: n + '.Sample(sampler' + n + ',' + a[1] + ')' if a[0] == n else 'tex2D(' + ','.join(a) + ')')
        code = calls(code, 'tex2Dlod', lambda a, n=name: n + '.SampleLevel(sampler' + n + ',(' + a[1] + ').xyz,(' + a[1] + ').w)' if a[0] == n else 'tex2Dlod(' + ','.join(a) + ')')
    for sampler, kind in re.findall(r'\buniform\s+(?:(?:highp|mediump|lowp)\s+)?(sampler3D|samplerCube)\s+(\w+)', program):
        if sampler == 'sampler3D':
            code = re.sub(r'\btex2D(?=\(' + re.escape(kind) + r',)', 'tex3D', code)
            native_uniforms.append('sampler3D ' + kind + ';')
        else:
            code = re.sub(r'\btex2D(?=\(' + re.escape(kind) + r',)', 'texCUBE', code)
            def cube_bias(args, name=kind):
                if args[0] != name:
                    return 'tex2Dbias(' + ','.join(args) + ')'
                coord = args[1]
                coord = re.sub(r'^float4\(([^,]+),\s*0,\s*([^,]+)\)$', r'float4(\1,\2)', coord)
                return 'texCUBEbias(' + name + ',' + coord + ')'
            code = calls(code, 'tex2Dbias', cube_bias)
    for kind, name in set(re.findall(r'\buniform\s+(?:(?:highp|mediump|lowp)\s+)?(sampler2DShadow|samplerCubeShadow)\s+(\w+)', program)):
        native_name = name.removeprefix('hlslcc_zcmp')
        uniforms = [u for u in uniforms if u.rstrip(';').split()[-1] != native_name]
        native_uniforms += [('TextureCube' if kind == 'samplerCubeShadow' else 'Texture2D') + '<float> ' + native_name + ';', 'SamplerComparisonState sampler' + native_name + ';']
        for function in ['tex2D', 'tex2Dlod']:
            def compare(args, n=name, native=native_name, f=function, cube=kind == 'samplerCubeShadow'):
                if args[0] != n:
                    return f + '(' + ','.join(args) + ')'
                coord = args[1]
                if f == 'tex2Dlod':
                    coord = re.sub(r'^float4\(([^,]+),\s*0,\s*[^,]+\)$', r'\1', coord)
                return native + '.SampleCmpLevelZero(sampler' + native + ',(' + coord + ').' + ('xyz' if cube else 'xy') + ',(' + coord + ').' + ('w' if cube else 'z') + ')'
            code = calls(code, function, compare)
    targets = sorted(set(re.findall(r'\bSV_Target([1-9]\d*)\b', code)))
    if targets and not vertex:
        declaration = 'struct ZjcsTargets { float4 target0:SV_Target0; ' + ' '.join('float4 target' + n + ':SV_Target' + n + ';' for n in targets) + ' };\n'
        code = code.replace('half4 frag(Varying input) : SV_Target { half4 result = 0;', 'ZjcsTargets frag(Varying input) { ZjcsTargets mrt = (ZjcsTargets)0; half4 result = 0;')
        for n in targets:
            code = code.replace('SV_Target' + n, 'mrt.target' + n)
        code = code.replace('return result;', 'mrt.target0 = result; return mrt;')
        code = declaration + code
    fetched = set(re.findall(r'\btexelFetch\((\w+),', code))
    fetched.update(re.findall(r'\btextureGather\((\w+),', code))
    for name in fetched:
        uniforms = [u for u in uniforms if u.rstrip(';').split()[-1] != name]
        sampler_kind = re.search(r'\b(sampler2DMSArray|sampler2DMS|sampler2DArray|sampler2D)\s+' + re.escape(name) + r'\b', program)[1]
        multisample = 'MS' in sampler_kind
        array = 'Array' in sampler_kind
        native_uniforms += ['Texture2D' + ('MS' if multisample else '') + ('Array' if array else '') + '<float4> ' + name + ';']
        if not multisample:
            native_uniforms.append('SamplerState sampler' + name + ';')
        code = calls(code, 'texelFetch', lambda a, n=name, ms=multisample, arr=array:
                     n + '.Load(' + a[1] + ',' + a[2] + ')' if a[0] == n and ms else
                     n + '.Load(int' + ('4' if arr else '3') + '(' + a[1] + ',' + a[2] + '))' if a[0] == n else 'texelFetch(' + ','.join(a) + ')')
        for function, method in [('tex2D', 'Sample'), ('tex2Dlod', 'SampleLevel'), ('tex2Dbias', 'SampleBias')]:
            code = calls(code, function, lambda a, n=name, f=function, m=method:
                         n + '.' + m + '(sampler' + n + ',' + a[1] + ')' if a[0] == n and f == 'tex2D' else
                         n + '.' + m + '(sampler' + n + ',(' + a[1] + ').xy,(' + a[1] + ').w)' if a[0] == n else f + '(' + ','.join(a) + ')')
        code = calls(code, 'textureGather', lambda a, n=name:
                     n + '.Gather' + ['Red','Green','Blue','Alpha'][int(a[2]) if len(a)>2 else 0] + '(sampler' + n + ',' + a[1] + ')' if a[0] == n else 'textureGather(' + ','.join(a) + ')')
    for color, color_slot in color_slots.items():
        code = re.sub(r'\b(input|output)\.uv' + str(color_slot) + r'\b', r'\1.color' + color, code)
    if layer:
        code = code.replace('ZjcsOriginalLayer', 'output.layer')
    if instance:
        code = code.replace('ZjcsOriginalInstance', 'input.instanceID')
    if view_id:
        code = code.replace('ZjcsOriginalViewID', '(input.instanceID & 1u)' if vertex else 'input.stereoEye')
        if vertex and not layer:
            code = code.replace('return output;', 'output.layer = input.instanceID & 1u; return output;')
    if blend_index:
        code = re.sub(r'\b(input|output)\.uv6\b', r'\1.stereoEye', code)
    if instance_varying:
        code = re.sub(r'\b(input|output)\.uv7\b', r'\1.originalInstance', code)
    if facing and not vertex:
        code = code.replace('frag(Varying input)', 'frag(Varying input, bool zjcsFrontFace : SV_IsFrontFace)').replace('ZjcsOriginalFrontFace', 'zjcsFrontFace')
    # UnityCG includes these same engine bindings; a second declaration fails D3D.
    builtin_values = set(re.findall(r'\b(?:float|half|fixed|uint|int)(?:[234](?:x[234])?)?\s+(\w+)', variables))
    builtin_values.update({'unity_Lightmap_HDR', 'unity_DynamicLightmap_HDR', 'unity_BaseInstanceID'})
    builtin_textures = set(re.findall(r'UNITY_DECLARE_TEX2D(?:_NOSAMPLER)?(?:_HALF|_FLOAT)?\((\w+)\)', variables))
    for name in builtin_textures:
        code = re.sub(r'\btex2D\(' + re.escape(name) + r',', 'UNITY_SAMPLE_TEX2D(' + name + ',', code)
    builtin_values.update(builtin_textures)
    uniforms = [u for u in uniforms + native_uniforms if u.rstrip(';').split()[-1].split('[')[0] not in builtin_values]
    return code, uniforms


def extended_structure(program):
    program = re.sub(r'(?m)^layout\(location\s*=\s*\d+\)\s+', '', program)
    if 'samplerExternalOES' in program:
        kind, name = re.findall(r'varying (vec[23]) (textureCoord\w*);', program)[-1]
        return 'struct Input { float4 vertex:POSITION; float4 uv:TEXCOORD0; };\nstruct Varying { float4 position:SV_POSITION; ' + hlsl(kind) + ' ' + name + ':TEXCOORD0; };\n'
    normalized = program.replace('vs_NORMAL0', 'vs_TEXCOORD8').replace('vs_TANGENT0', 'vs_TEXCOORD9')
    head = normalized.split('void main()', 1)[0]
    declarations = re.findall(r'^(flat\s+)?(in|out)\s+(?:(?:highp|mediump|lowp)\s+)?([biu]?vec[234]|float|int|uint)\s+(\w+);', head, re.M)
    inputs, outputs = [], []
    for flat, direction, kind, name in declarations:
        if direction == 'in':
            if name.startswith('in_TEXCOORD'):
                n = name.removeprefix('in_TEXCOORD')
                field, semantic = ('uv' if n == '0' else 'uv' + n), 'TEXCOORD' + n
            elif name.startswith('in_COLOR'):
                n = name.removeprefix('in_COLOR')
                field, semantic = ('color' if n == '0' else 'color' + n), 'COLOR' + n
            else:
                field, semantic = {'in_POSITION0': ('vertex', 'POSITION'), 'in_NORMAL0': ('normal', 'NORMAL'),
                                   'in_TANGENT0': ('tangent', 'TANGENT'), 'in_COLOR0': ('color', 'COLOR0'),
                                   'in_COLOR1': ('color1', 'COLOR1')}[name]
            inputs.append(hlsl(kind) + ' ' + field + ':' + semantic + ';')
        else:
            if name == 'vs_SV_InstanceID0':
                field, semantic = 'originalInstance', 'TEXCOORD7'
            elif name == 'vs_BLENDINDICES0':
                field, semantic = 'stereoEye', 'TEXCOORD6'
            elif name.startswith('vs_TEXCOORD'):
                n = name.removeprefix('vs_TEXCOORD')
                field = {'0': 'uv', '1': 'world', '2': 'mask'}.get(n, 'uv' + n)
                semantic = 'TEXCOORD' + n
            else:
                n = name.removeprefix('vs_COLOR')
                if n == name:
                    raise ValueError('Unmapped varying: ' + name)
                field, semantic = ('color' if n == '0' else 'color' + n), 'COLOR' + n
            outputs.append(('nointerpolation ' if flat else '') + hlsl(kind) + ' ' + field + ':' + semantic + ';')
    if 'gl_VertexID' in program:
        inputs.append('uint vertexID:SV_VertexID;')
    if 'gl_InstanceID' in program or 'gl_ViewID_OVR' in program:
        inputs.append('uint instanceID:SV_InstanceID;')
    if 'gl_Layer' in program or 'gl_ViewID_OVR' in program:
        outputs.append('uint layer:SV_RenderTargetArrayIndex;')
    return 'struct Input { ' + ' '.join(inputs) + ' };\nstruct Varying { float4 position:SV_POSITION; ' + ' '.join(outputs) + ' };\n'


def translate_pass(shader):
    programs = original_programs(shader)
    keywords = sorted({k for p in programs for k in p['keywords']})
    pass_ = shader.m_ParsedForm.m_SubShaders[0].m_Passes[0]
    text = 'Pass {\n' + render_state(pass_.m_State) + '\nCGPROGRAM\n#pragma target 4.5\n#pragma vertex vert\n#pragma fragment frag\n'
    if '_WATER_ABOVE' in keywords:
        text += '#pragma multi_compile_local _WATER_ABOVE _WATER_UNDER\n'
    for keyword in keywords:
        if keyword not in ('_WATER_ABOVE', '_WATER_UNDER'):
            text += '#pragma multi_compile __ ' + keyword + '\n'
    text += '#include "UnityCG.cginc"\n'
    for index, program in enumerate(programs):
        if keywords:
            condition = ' && '.join(('defined(' + k + ')') if k in program['keywords'] else ('!defined(' + k + ')') for k in keywords)
            text += ('#if ' if index == 0 else '#elif ') + condition + '\n'
        vertex, vu = extended_body(program['source'], True)
        fragment, fu = extended_body(program['source'], False)
        geometry = ''
        if '#ifdef GEOMETRY' in program['source']:
            source = program['source'].split('#ifdef GEOMETRY', 1)[1]
            if 'layout(triangles) in;' not in source or 'layout(triangle_strip) out;' not in source or 'layout(max_vertices = 3) out;' not in source:
                raise ValueError('Original geometry topology requires explicit conversion')
            body = source.split('void main()',1)[1]
            body = body[body.index('{')+1:body.rfind('}')]
            body = re.sub(r'gl_in\[(\d+)\]\.gl_Position', r'vertices[\1].position', body)
            body = re.sub(r'vs_TEXCOORD0\[(\d+)\]', r'vertices[\1].uv', body)
            body = body.replace('gs_TEXCOORD0','output.uv').replace('gl_Position','output.position').replace('gl_Layer','output.layer')
            body = body.replace('EmitVertex()', 'stream.Append(output)').replace('EndPrimitive()', 'stream.RestartStrip()')
            geometry = '[maxvertexcount(3)] void geom(triangle Varying vertices[3], inout TriangleStream<Varying> stream) { Varying output = (Varying)0; uint u_xlatu0;\n' + hlsl(body) + '\n}\n'
            vu.append('float _ArraySliceIndex;')
        uniforms = {}
        for uniform in sorted(set(vu + fu), key=lambda u:(not u.startswith('struct '), u)):
            uniforms[uniform if uniform.startswith('struct ') else uniform.split()[-1]] = uniform
        text += extended_structure(program['source']) + '\n' + '\n'.join(uniforms.values()) + '\n' + vertex + '\n' + fragment + '\n' + geometry
    if keywords:
        text += '#endif\n'
    if any('#ifdef GEOMETRY' in p['source'] for p in programs):
        text = text.replace('#pragma fragment frag\n', '#pragma fragment frag\n#pragma geometry geom\n')
    return text + 'ENDCG\n}\n', programs


def translate_shader(shader, token, output):
    properties = ConvertSerializedProperties(shader.m_ParsedForm.m_PropInfo)
    chunks = ['Shader ' + json.dumps(shader.m_ParsedForm.m_Name) + ' {\n', properties]
    records, stripped = [], []
    vulkan = vulkan_programs(shader) if 18 in shader.platforms else []
    for si, original_sub in enumerate(shader.m_ParsedForm.m_SubShaders):
        chunks.append('SubShader {\nLOD ' + str(original_sub.m_LOD) + '\nTags { ' + ' '.join(json.dumps(k) + '=' + json.dumps(v) for k, v in original_sub.m_Tags.tags) + ' }\n')
        for pi, original_pass in enumerate(original_sub.m_Passes):
            if original_pass.m_Type != 0:
                raise ValueError('Original UsePass/GrabPass needs explicit recovery')
            sub = copy(original_sub)
            sub.m_Passes = [original_pass]
            form = copy(shader.m_ParsedForm)
            form.m_SubShaders = [sub]
            one = copy(shader)
            one.m_ParsedForm = form
            selected = platform_shader(one, 9, {2, 3, 4})
            if not referenced_programs(selected.m_ParsedForm.m_SubShaders[0].m_Passes[0]):
                extra = [p for p in vulkan if p['subshader'] == si and p['pass_index'] == pi]
                if extra:
                    if len(extra) != 1:
                        raise ValueError('Vulkan-only pass needs explicit variant recovery')
                    passage, record = translate_vulkan(one, extra[0], output)
                    chunks.append(passage)
                    records.append(record)
                    continue
                chunks.append('Pass {\n' + render_state(original_pass.m_State) + '\n}\n')
                stripped.append([si, pi])
                continue
            text, programs = translate_pass(selected)
            for keyword, flags in zip(form.m_KeywordNames, form.m_KeywordFlags):
                if flags not in (0, 1):
                    raise ValueError('Unknown keyword scope')
                if flags == 1:
                    text = text.replace('#pragma multi_compile __ ' + keyword + '\n', '#pragma multi_compile_local __ ' + keyword + '\n')
            fixed = set.intersection(*(set(p['keywords']) for p in programs))
            for keyword in fixed:
                text = re.sub(r'(?m)^#pragma multi_compile(?:_local)? (?:__ )?' + re.escape(keyword) + '$', '#define ' + keyword + ' 1', text)
            begin = text.index('Pass {\n')
            end = text.index('ENDCG\n}\n') + len('ENDCG\n}\n')
            passage = text[begin:end]
            if 'unity_StereoMatrix' in passage:
                passage = passage.replace('CGPROGRAM', 'HLSLPROGRAM').replace('ENDCG', 'ENDHLSL')
                passage = passage.replace('#include "UnityCG.cginc"', '#if defined(STEREO_MULTIVIEW_ON) && defined(SHADER_API_D3D11)\n#define UNITY_STEREO_INSTANCING_ENABLED\n#endif\n#include "HLSLSupport.cginc"\n#include "UnityCG.cginc"')
            if 'PROCEDURAL_INSTANCING_ON' in {k for p in programs for k in p['keywords']}:
                # The original executable already performs every procedural
                # transform and buffer read. Retain Unity's draw-call cbuffer
                # while avoiding an additional Unity setup callback.
                passage = passage.replace('CGPROGRAM', 'HLSLPROGRAM').replace('ENDCG', 'ENDHLSL')
                passage = passage.replace('#include "UnityCG.cginc"', '#include "HLSLSupport.cginc"\n#if defined(PROCEDURAL_INSTANCING_ON)\n#define ZJCS_DIRECT_PROCEDURAL_BODY\n#undef PROCEDURAL_INSTANCING_ON\n#endif\n#include "UnityCG.cginc"\n#if defined(ZJCS_DIRECT_PROCEDURAL_BODY)\n#define PROCEDURAL_INSTANCING_ON 1\nCBUFFER_START(UnityDrawCallInfo)\nint unity_BaseInstanceID;\nint unity_InstanceCount;\nCBUFFER_END\n#endif')
            if original_pass.m_State.m_Name:
                passage = passage.replace('Pass {\n', 'Pass {\nName ' + json.dumps(original_pass.m_State.m_Name) + '\n', 1)
            chunks.append(passage)
            for program in programs:
                source = program['source'].encode()
                path = output / 'Sources' / (sha(source) + '.glsl.txt')
                if path.exists() and path.read_bytes() != source:
                    raise ValueError('Preserved original GPU program changed')
                path.write_bytes(source)
                records.append(dict(subshader=si, pass_index=pi, index=program['index'],
                                    keywords=program['keywords'], gpu_type=program['gpu_type'], geometry='#ifdef GEOMETRY' in program['source'], path=path.relative_to(output).as_posix(), sha256=sha(source)))
        chunks.append('}\n')
    chunks.append('Fallback Off\n}\n')
    represented = {(p['subshader'], p['pass_index'], tuple(p['keywords'])) for p in records}
    missing = [(p['subshader'], p['pass_index'], p['keywords']) for p in vulkan
               if (p['subshader'], p['pass_index'], tuple(p['keywords'])) not in represented]
    if missing:
        raise ValueError('Unconverted retained Vulkan cases: ' + str(missing))
    return ''.join(chunks).encode(), records, stripped


def build(cache):
    inventory_path = cache / 'all-assets/inventory.json'
    inventory = json.loads(inventory_path.read_bytes())
    inputs = {r['output']: r for r in json.loads((cache / 'source-manifest.json').read_bytes())['inputs']}
    output = cache / 'all-assets/windows-shaders'
    for folder in [output, output / 'Sources', output / 'Assets/Shaders', output / 'Records', output / 'Unresolved', output / 'Converters']:
        folder.mkdir(parents=True, exist_ok=True)
    generator = Path(__file__).read_bytes()
    (output / 'Converters' / (sha(generator) + '.py')).write_bytes(generator)
    dependencies = []
    for name in ['current_shader_programs.py', 'current_vulkan_shader.py', 'official_platform_shader_programs.py',
                 'prepare_unity_map13_entity_shaders.py', 'prepare_unity_map13_environment_shader.py',
                 'prepare_unity_map13_pipeline_shaders.py', 'prepare_unity_map13_water_shaders.py',
                 'prepare_battle_hud_materials.py']:
        data = Path(__file__).with_name(name).read_bytes()
        (output / 'Converters' / (sha(data) + '.py')).write_bytes(data)
        dependencies.append(dict(path='scripts/' + name, sha256=sha(data)))
    grouped = defaultdict(list)
    for row in inventory['shaders']:
        grouped[row['input']].append(row)
    engine_path = cache / 'windows-engine/source-manifest.json'
    engine = json.loads(engine_path.read_bytes())
    engine_input = cache / 'windows-engine' / engine['inputs'][0]['output']
    if sha(engine_input.read_bytes()) != engine['inputs'][0]['decoded_sha256']:
        raise ValueError('Windows engine payload changed')
    engine_objects = {(r['type'], str(r['original_file_id'])): r for r in engine['counterparts']}
    results, failures, bindings = [], [], []
    for source, rows in grouped.items():
        raw = (cache / source).read_bytes()
        if sha(raw) != inputs[source]['decoded_sha256']:
            raise ValueError('Verified shader source changed')
        env = UnityPy.load(raw)
        if source == 'inputs/unity default resources':
            for bundle in env.files.values():
                for file in serialized_files(bundle):
                    file.name = 'unity default resources'
        objects = {(f.name.casefold(), str(o.path_id)): o for b in env.files.values()
                   for f in serialized_files(b) for o in f.objects.values()}
        for row in rows:
            obj = objects[row['cab'].casefold(), row['path_id']]
            if sha(obj.get_raw_data()) != row['object_sha256']:
                raise ValueError('Shader object changed')
            token = hashlib.sha256((row['cab'].casefold() + ':' + row['path_id']).encode()).hexdigest()
            try:
                counterpart = engine_objects.get((row['type'], row['path_id'])) if row['cab'] == 'unity default resources' else None
                if counterpart:
                    if counterpart['original_object_sha256'] != row['object_sha256']:
                        raise ValueError('Windows engine counterpart source differs')
                    binding = dict(source=row, counterpart=counterpart, payload=engine_input.relative_to(cache).as_posix(),
                                   payload_sha256=engine['inputs'][0]['decoded_sha256'],
                                   unity_reference=dict(fileID=counterpart['windows_file_id'], guid='0000000000000000e000000000000000', type=0))
                    (output / 'Records' / (token + '.json')).write_text(json.dumps(binding, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
                    bindings.append(binding)
                    continue
                if row['type'] != 'Shader':
                    raise ValueError('Engine compute shader requires Windows native counterpart')
                payload, programs, stripped = translate_shader(obj.read(), token, output)
                path = output / 'Assets/Shaders' / (token + '.shader')
                if path.exists() and path.read_bytes() != payload:
                    previous = output / 'Previous' / sha(path.read_bytes())
                    previous.parent.mkdir(exist_ok=True)
                    if not previous.exists():
                        previous.write_bytes(path.read_bytes())
                path.write_bytes(payload)
                guid = hashlib.md5(('zjcs-current-native-shader:' + row['cab'].casefold() + ':' + row['path_id']).encode()).hexdigest()
                metadata = ('fileFormatVersion: 2\nguid: ' + guid + '\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
                Path(str(path) + '.meta').write_bytes(metadata)
                result = dict(source=row, programs=programs, stripped_passes=stripped,
                              path=path.relative_to(output).as_posix(), sha256=sha(payload), guid=guid,
                              meta_sha256=sha(metadata), generator_sha256=sha(Path(__file__).read_bytes()))
                (output / 'Records' / (token + '.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
                results.append(result)
                print('SHADER', len(results), row['name'], len(programs), 'programs', flush=True)
            except Exception as error:
                failures.append(dict(source=row, error=str(error)))
                (output / 'Unresolved' / (token + '.json')).write_text(json.dumps(failures[-1], ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
                print('UNRESOLVED', row['name'], str(error)[:180], flush=True)
        gc.collect()
    result = dict(manifest=inventory['manifest'], inventory_sha256=sha(inventory_path.read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), shaders=results,
                  conversion_dependencies=dependencies,
                  native_engine_bindings=bindings, unresolved=failures, completed=not failures)
    (output / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Shader outputs', len(results), 'unresolved', len(failures), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    build(parser.parse_args().cache.resolve())
