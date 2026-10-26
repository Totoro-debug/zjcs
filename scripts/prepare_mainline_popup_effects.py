"""Convert APK ScreenBlur and fingerprint the original TMPColorGradient AOT methods."""
import json
from pathlib import Path
from official_apk import Apk, ROOT
from official_shader_programs import gles_programs
from prepare_battle_hud_materials import stage
from prepare_unity_map10_entities import sha256, json_bytes, write_meta
from read_official_native import NativeMethods


def build():
    apk = Apk(reuse_current_cache=True)
    apk.load_builtin('assets/bin/Data/data.unity3d')
    shader = next(o for o in apk.env.objects if o.type.name == 'Shader'
                  and o.read().m_ParsedForm.m_Name == 'CustomRenderTexture/ScreenBlur')
    programs = gles_programs(shader.read(), with_keywords=True)
    assert len(programs) == 1 and not programs[0]['keywords']
    fragment, uniforms = stage(programs[0]['source'], False)
    # The Built-in renderer supplies a blit quad instead of URP's procedural
    # fullscreen triangle. Interpolated UV coverage is unchanged.
    text = '''Shader "Zjcs/Official/UI/ScreenBlur" {
SubShader { Cull Off ZWrite Off ZTest Always
Pass { CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
Varying vert(appdata_img input) { Varying output; output.position=UnityObjectToClipPos(input.vertex); output.uv=input.texcoord; return output; }
'''+ '\n'.join(uniforms)+'\n'+fragment+'\nENDCG\n}\n}\n}\n'
    output = ROOT/'unity/Assets/Game/Runtime/Presentation/World/Shaders/ScreenBlur.shader'
    output.write_text(text, encoding='utf-8', newline='\n')
    write_meta(output, 'ShaderImporter')
    guid = Path(str(output)+'.meta').read_text().split('guid: ')[1].splitlines()[0]
    material = ROOT/'unity/Assets/Game/Resources/World/Mainline/ScreenBlur.mat'
    material.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n'
        '  serializedVersion: 8\n  m_Name: ScreenBlur\n  m_Shader: {fileID: 4800000, guid: '+guid+', type: 3}\n'
        '  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n'
        '  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n'
        '  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats: []\n    m_Colors: []\n', encoding='utf-8', newline='\n')
    write_meta(material, 'NativeFormatImporter')
    native = NativeMethods(ROOT/'.tools/source-packages/commerce-native/libil2cpp-restored.bin',
                           ROOT/'.tools/source-packages/official-code/global-metadata.dat')
    methods = []
    for name, (address, index) in native.by_name.items():
        if not name.startswith('TMPColorGradient.'):
            continue
        end = min(v for v in native.by_address if v > address)
        methods.append(dict(name=name, metadata_index=index, address=hex(address), length=end-address,
                            sha256=sha256(native.image[address:end])))
    source_root = ROOT/'.tools/source-packages/official-code/framework-firstpass-source'
    receipt = dict(manifest=apk.version, shader=dict(apk.provenance(shader), objectSha256=sha256(shader.get_raw_data())),
        programs=programs, nativeMethods=methods,
        nativeImage=dict(path='.tools/source-packages/commerce-native/libil2cpp-restored.bin',sha256=sha256(native.image)),
        metadata=dict(path='.tools/source-packages/official-code/global-metadata.dat',sha256=sha256(native.metadata)),
        officialCode=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha256(p.read_bytes()))
            for p in [source_root/(v+'.cs') for v in ('ScreenBlurPass','BlurManager','WindowBlurMask','WindowMaskBase','PopupWindow')]],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha256(p.read_bytes())) for p in (output, material)],
        operation='Original seven-tap expressions and four down/up levels; original line extents, rotated vertices and Gradient.Evaluate; Built-in render capture and blit quad adapt the original URP pass.',
        converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(),sha256=sha256(Path(__file__).read_bytes())))
    (ROOT/'assets/reference/official-map/popup-effects-source-manifest.json').write_bytes(json_bytes(receipt))
    print('Native popup effects:', len(methods), 'gradient methods, one original blur program')


if __name__ == '__main__':
    build()
