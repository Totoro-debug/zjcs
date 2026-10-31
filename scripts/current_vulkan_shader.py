"""Recover the retained Vulkan-only particle pass from its actual SPIR-V."""
import ctypes as C
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile

from UnityPy.helpers import CompressionHelper
from UnityPy.streams import EndianBinaryReader
from UnityPy.export.ShaderConverter import ShaderSubProgram

from official_apk import ROOT
from official_platform_shader_programs import STAGES, platform_shader
from prepare_unity_map13_environment_shader import render_state


def programs(shader):
    selected = platform_shader(shader, 18, {25})
    chunks = [CompressionHelper.decompress_lz4(bytes(selected.compressedBlob)[a:a+b], size)
              for a, b, size in zip(selected.offsets[0], selected.compressedLengths[0], selected.decompressedLengths[0])]
    table = EndianBinaryReader(chunks[0], endian='<')
    count = table.read_int()
    result = []
    for si, sub in enumerate(selected.m_ParsedForm.m_SubShaders):
        for pi, pass_ in enumerate(sub.m_Passes):
            indexes = set()
            for name in STAGES:
                stage = getattr(pass_, name, None)
                if stage:
                    indexes.update(p.m_BlobIndex for p in stage.m_SubPrograms)
                    indexes.update(p.m_BlobIndex for g in (stage.m_PlayerSubPrograms or []) for p in g)
            for index in sorted(indexes):
                if not 0 <= index < count:
                    raise ValueError('Invalid Vulkan executable table index')
                table.Position = 4 + index * 12
                start, length, segment = table.read_int(), table.read_int(), table.read_int()
                if not 0 <= start < start + length <= len(chunks[segment]):
                    raise ValueError('Invalid Vulkan executable segment')
                p = ShaderSubProgram(EndianBinaryReader(chunks[segment][start:start+length], endian='<'))
                if p.m_ProgramType.value != 25:
                    raise ValueError('Unexpected Vulkan executable GPU type')
                result.append(dict(subshader=si, pass_index=pi, index=index,
                                   keywords=sorted(set(p.m_Keywords + (p.m_LocalKeywords or []))), raw=p.m_ProgramCode))
    return result


def cross_hlsl(raw):
    dll = ROOT / '.tools/spirv-cross/spirv-cross.dll'
    if hashlib.sha256(dll.read_bytes()).hexdigest() != 'b059a29ff16d294852dd1416f3c99921548d067db58e9b4d11d927aa293d9521':
        raise ValueError('SPIRV-Cross native input changed')
    d = C.CDLL(str(dll)); v = C.c_void_p; u = C.c_uint
    signatures = {
        'context_create': [C.POINTER(v)], 'context_destroy': [v],
        'context_parse_spirv': [v, C.POINTER(u), C.c_size_t, C.POINTER(v)],
        'context_create_compiler': [v, C.c_int, v, C.c_int, C.POINTER(v)],
        'compiler_create_compiler_options': [v, C.POINTER(v)],
        'compiler_options_set_uint': [v, u, u], 'compiler_install_compiler_options': [v, v],
        'compiler_compile': [v, C.POINTER(C.c_char_p)]}
    for name, args in signatures.items():
        f = getattr(d, 'spvc_' + name); f.argtypes = args; f.restype = C.c_int
    d.spvc_context_get_last_error_string.argtypes = [v]
    d.spvc_context_get_last_error_string.restype = C.c_char_p
    ctx = v()
    if d.spvc_context_create(C.byref(ctx)):
        raise ValueError('Cannot create SPIRV-Cross context')
    def call(name, *args):
        if getattr(d, 'spvc_' + name)(*args):
            raise ValueError(d.spvc_context_get_last_error_string(ctx).decode())
    try:
        words = (u * (len(raw) // 4)).from_buffer_copy(raw)
        ir = v(); call('context_parse_spirv', ctx, words, len(words), C.byref(ir))
        compiler = v(); call('context_create_compiler', ctx, 2, ir, 1, C.byref(compiler))
        options = v(); call('compiler_create_compiler_options', compiler, C.byref(options))
        call('compiler_options_set_uint', options, 13 | 0x4000000, 50)
        call('compiler_install_compiler_options', compiler, options)
        source = C.c_char_p(); call('compiler_compile', compiler, C.byref(source))
        return source.value.decode()
    finally:
        d.spvc_context_destroy(ctx)


def translate(shader, program, output):
    raw = program['raw']
    digest = hashlib.sha256(raw).hexdigest()
    if digest != '08be9dac7602e62c3608ffc3e6e1b8f0f6c0ff3e7d6ee35326cc84a14c5310c0':
        raise ValueError('Vulkan-only particle executable identity changed')
    folder = output / 'Sources/Vulkan' / digest
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'original.bin').write_bytes(raw)
    tools = ROOT / '.tools/spirv-cross'
    decoder = tools / 'smolv-decoder.exe'
    if hashlib.sha256(decoder.read_bytes()).hexdigest() != 'add5f1e355c4c3c095dd58e8b921e68c15dbaea9a011b6752f4c6806fd405102':
        raise ValueError('SMOL-V decoder input changed')
    # Temporary decoder outputs are confined to this conversion's own session.
    temporary = ROOT / 'output/validation'
    temporary.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='vulkan-pass-', dir=temporary) as temporary_name:
        base = Path(temporary_name)
        (base / 'original.bin').write_bytes(raw)
        subprocess.run(['D:/Tool/Unity/Editor/Data/MonoBleedingEdge/bin/mono.exe', str(decoder),
                        str(base / 'original.bin'), str(base / 'stage')], check=True, capture_output=True)
        stages = {stage: (base / ('stage.' + stage + '.spv')).read_bytes() for stage in ['vert', 'frag']}
    for stage, data in stages.items():
        (folder / (stage + '.spv')).write_bytes(data)
    pass_ = shader.m_ParsedForm.m_SubShaders[0].m_Passes[0]
    if shader.m_ParsedForm.m_Name != 'Universal Render Pipeline/Particles/Unlit' or pass_.m_State.m_Name != 'DepthNormalsOnly' or program['keywords']:
        raise ValueError('Vulkan-only pass requires an explicit resource layout')
    names = {index: name for name, index in pass_.m_NameIndices}
    cbuffers = pass_.progVertex.m_CommonParameters.m_ConstantBuffers
    reflected = {names[c.m_NameIndex]: {names[p.m_NameIndex]: p.m_Index for p in c.m_MatrixParams + c.m_VectorParams} for c in cbuffers}
    expected = {'UnityPerDraw': {'unity_ObjectToWorld': 0, 'unity_WorldToObject': 64},
                'VGlobals3698109016': {'unity_MatrixV': 32, 'unity_MatrixVP': 96, '_WorldSpaceCameraPos': 0, 'unity_OrthoParams': 16}}
    if reflected != expected:
        raise ValueError('Original particle uniform reflection changed')
    vertex, fragment = cross_hlsl(stages['vert']), cross_hlsl(stages['frag'])
    (folder / 'vert.cross.hlsl').write_text(vertex, encoding='utf-8')
    (folder / 'frag.cross.hlsl').write_text(fragment, encoding='utf-8')
    # Stripped SPIR-V names are recovered from the original Unity pass metadata.
    # Each vector-array element is one original matrix column.
    vertex = re.sub(r'cbuffer\s+\w+\s*:\s*register\(b\d+\)\s*\{[^}]+\};', '', vertex)
    for member, matrix in [('_25_m0', 'unity_ObjectToWorld'), ('_25_m1', 'unity_WorldToObject'),
                           ('_65_m2', 'UNITY_MATRIX_V'), ('_65_m3', 'UNITY_MATRIX_VP')]:
        vertex = re.sub(re.escape(member) + r'\[(\d+)\]', lambda m, mat=matrix:
                        'float4(' + ','.join(mat + '[' + str(row) + '][' + m[1] + ']' for row in range(4)) + ')', vertex)
    vertex = re.sub(r'\b_65_m0\b', '_WorldSpaceCameraPos', vertex)
    vertex = re.sub(r'\b_65_m1\b', 'unity_OrthoParams', vertex)
    if re.search(r'\b_(?:25|65)_m\d+\b', vertex):
        raise ValueError('Unmapped original particle constant buffer member')
    # Vulkan's generated viewport inversion is omitted when Unity supplies its
    # native D3D projection matrix. The original world/normal arithmetic stays.
    inversion = 'gl_Position.y = -gl_Position.y;'
    if vertex.count(inversion) != 1:
        raise ValueError('Original Vulkan viewport convention changed')
    vertex = vertex.replace(inversion, '')
    # The matching retained GLES pass confirms these original vertex channels.
    if 'float4 _12 : TEXCOORD0;' not in vertex or 'float3 _101 : TEXCOORD1;' not in vertex:
        raise ValueError('Original particle vertex layout changed')
    vertex = vertex.replace('float4 _12 : TEXCOORD0;', 'float4 _12 : POSITION;').replace('float3 _101 : TEXCOORD1;', 'float3 _101 : NORMAL;')
    def scoped(source, prefix, entry):
        source = re.sub(r'\b(?:_\d+|vs_TEXCOORD\d+|gl_Position|SPIRV_Cross_Input|SPIRV_Cross_Output|vert_main|frag_main|main)\b', lambda m: prefix + m[0], source)
        return re.sub(r'\b' + prefix + r'main\b', entry, source)
    vertex, fragment = scoped(vertex, 'v_', 'vert'), scoped(fragment, 'f_', 'frag')
    text = ('Pass {\nName ' + json.dumps(pass_.m_State.m_Name) + '\n' + render_state(pass_.m_State)
            + '\nHLSLPROGRAM\n#pragma target 5.0\n#pragma vertex vert\n#pragma fragment frag\n#include "UnityCG.cginc"\n'
            + vertex + '\n' + fragment + '\nENDHLSL\n}\n')
    record = {k: value for k, value in program.items() if k != 'raw'}
    record.update(gpu_type='kShaderGpuProgramSPIRV', geometry=False,
                  path=(folder / 'original.bin').relative_to(output).as_posix(), sha256=digest,
                  native_stages={stage: hashlib.sha256(data).hexdigest() for stage, data in stages.items()},
                  binding_metadata=expected)
    return text, record
