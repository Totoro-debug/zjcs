"""Translate the pinned original Light2D and Sprite-Lit GLES programs to HLSL."""
from pathlib import Path
import json
from official_apk import Apk, ROOT
from official_shader_programs import gles_programs
from prepare_battle_hud_materials import stage
from prepare_unity_map10_entities import sha256, json_bytes, write_meta

DEST = ROOT / 'unity/Assets/Game/Runtime/Presentation/World/Shaders'
RECEIPT = ROOT / 'assets/reference/official-map/map11-light-shaders-source-manifest.json'


def translate(shader, variants, name, properties, blend):
    programs, uniforms = [], set()
    for index, keywords in enumerate(variants):
        source = next(v['source'] for v in gles_programs(shader.read(), with_keywords=True)
                      if set(v['keywords']) == set(keywords))
        if len(variants) > 1:
            programs.append('#if defined(USE_RECESSION)' if index == 0 else '#else')
        for vertex in (True, False):
            code, declarations = stage(source, vertex)
            code = code.replace('vs_TEXCOORD4', 'output.recession' if vertex else 'input.recession')
            code = code.replace('half4 frag', 'float4 frag').replace('half4 result', 'float4 result')
            programs.append(code)
            uniforms.update(d for d in declarations if not d.startswith('float4 _ProjectionParams'))
    if len(variants) > 1:
        programs.append('#endif')
    text = 'Shader "Zjcs/Official/World/' + name + '" {\nProperties {\n' + properties + '\n}\nSubShader {\n'
    state = shader.read().m_ParsedForm.m_SubShaders[0].m_Passes[0].m_State
    cull = ('Off','Front','Back')[int(state.culling.val)]
    zwrite = ('Off','On')[int(state.zWrite.val)]
    ztest = ('Disabled','Never','Less','Equal','LEqual','Greater','NotEqual','GEqual','Always')[int(state.zTest.val)]
    text += 'Tags { "Queue"="Transparent" "RenderType"="Transparent" }\nCull '+cull+' ZWrite '+zwrite+' ZTest '+ztest+'\n'
    text += blend + '\nPass {\nCGPROGRAM\n#pragma target 3.0\n#pragma vertex vert\n#pragma fragment frag\n'
    if len(variants) > 1:
        text += '#pragma multi_compile __ USE_RECESSION\n'
    text += '#include "UnityCG.cginc"\n'
    text += 'struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };\n'
    text += 'struct Varying { float4 position:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 recession:TEXCOORD4; };\n'
    text += '\n'.join(sorted(uniforms)) + '\n' + '\n'.join(programs) + '\nENDCG\n}\n}\n}\n'
    path = DEST / (name + '.shader')
    path.write_text(text, encoding='utf-8', newline='\n')
    write_meta(path, 'ShaderImporter')
    material = ROOT / 'unity/Assets/Game/Resources/World/Mainline' / (name + '.mat')
    guid = Path(str(path)+'.meta').read_text().split('guid: ')[1].splitlines()[0]
    material.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n'
        '  serializedVersion: 8\n  m_Name: '+name+'\n  m_Shader: {fileID: 4800000, guid: '+guid+', type: 3}\n'
        '  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n'
        '  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n'
        '  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats: []\n    m_Colors: []\n', encoding='utf-8', newline='\n')
    write_meta(material, 'NativeFormatImporter')
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha256(path.read_bytes()),
                source=apk.provenance(shader), objectSha256=sha256(shader.get_raw_data()), variants=variants,
                material=dict(path=material.relative_to(ROOT).as_posix(),sha256=sha256(material.read_bytes())))


def build():
    global apk
    apk = Apk()
    apk.load_builtin('assets/bin/Data/data.unity3d')
    light = next(o for o in apk.env.objects if o.type.name == 'Shader'
                 and o.read().m_ParsedForm.m_Name == 'Hidden/Light2D-Shape')
    apk.load('defaultpackage_assets_partition_mapground_11_mapground_11_bare.bundle')
    tilemap = next(o.read() for o in apk.env.objects if o.type.name == 'Tilemap'
                   and o.read().m_GameObject.read().m_Name == 'Ground')
    renderer = next(c.component.read() for c in tilemap.m_GameObject.read().m_Component if c.component.type.name == 'TilemapRenderer')
    sprite = renderer.m_Materials[0].read().m_Shader.deref()
    DEST.mkdir(parents=True, exist_ok=True)
    outputs = [translate(light, [['USE_RECESSION'], []], 'RegionLight2D', '',
                         'Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha'),
               translate(sprite, [['USE_SHAPE_LIGHT_TYPE_0']], 'RegionSpriteLit',
                         '_MainTex ("Main",2D)="white" {}\n_MaskTex ("Mask",2D)="white" {}\n'
                         '_Color ("Color",Color)=(1,1,1,1)\n_RendererColor ("Renderer",Color)=(1,1,1,1)',
                         'Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha')]
    RECEIPT.write_bytes(json_bytes(dict(manifest=apk.version, outputs=outputs,
        operation='Mechanical GLES-to-HLSL translation; original expressions, native alpha light overlap and single Multiply blend style retained.',
        converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha256(Path(__file__).read_bytes())))))
    print('Native region lighting shaders:', len(outputs))


if __name__ == '__main__':
    build()
