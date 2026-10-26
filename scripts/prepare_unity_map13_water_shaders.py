"""Translate original Map13 water GLES programs and preserve serialized pass state."""
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_shader_programs import gles_programs
from prepare_battle_hud_materials import stage, hlsl
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_native import ROOTS
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta


SHADERS = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders"
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source/Shaders"
RECEIPT = ROOT / "assets/reference/official-map/map13-water-shaders-source-manifest.json"
FAMILIES = {"Universal Render Pipeline/2D/Sprite-Lit-CustomWater2": "WaterSurface",
            "Unlit/CustomWater": "WaterLegacy", "Sprites/WaterMask": "WaterMask",
            "Sprites/WaterMaskOverwrite": "WaterMaskOverwrite", "Sprites/Submersion": "Submersion"}


def expressions(code):
    code = code.replace("inversesqrt(", "rsqrt(")
    code = re.sub(r"\bin_TEXCOORD(\d)\b", r"input.uv\1", code)
    code = re.sub(r"\bin_NORMAL0\b", "input.normal", code)
    code = re.sub(r"\bin_TANGENT0\b", "input.tangent", code)
    code = re.sub(r"\bvs_TEXCOORD(\d)\b", r"output.uv\1", code)
    # GLSL's vector ==/!= returns one boolean, unlike HLSL's component mask.
    code = re.sub(r"(u_xlatb\w*\s*=\s*)([^;\n]*(?:float|half)[234]\([^;\n]*[!=]=[^;\n]*)(;)",
                  lambda m: m[1] + ("any(" if "!=" in m[2] else "all(") + m[2] + ")" + m[3], code)
    return code


def body(program, vertex):
    text, uniforms = stage(program, vertex)
    text = expressions(text)
    if not vertex: text = text.replace("output.", "input.")
    skip = {"_ProjectionParams", "_Time", "_ScreenParams", "_WorldSpaceCameraPos", "unity_OrthoParams", "_ZBufferParams"}
    uniforms = [u for u in uniforms if u.rstrip(";").split()[-1] not in skip]
    if re.search(r"hlslcc_mtx|\bin_\w+|\bvs_\w+|lessThan\(|greaterThan\(|inversesqrt\(", text):
        raise ValueError("Untranslated original water shader operation")
    return text, uniforms


def structure(program):
    head = program.split("void main()", 1)[0]
    inputs = re.findall(r"^in\s+(?:(?:highp|mediump|lowp)\s+)?(vec[234]|float)\s+(\w+);", head, re.M)
    outputs = re.findall(r"^out\s+(?:(?:highp|mediump|lowp)\s+)?(vec[234]|float)\s+(\w+);", head, re.M)
    def field(kind, name, vertex):
        if name == "in_POSITION0": return hlsl(kind) + " vertex:POSITION;"
        if name == "in_COLOR0": return hlsl(kind) + " color:COLOR;"
        if name == "in_NORMAL0": return hlsl(kind) + " normal:NORMAL;"
        if name == "in_TANGENT0": return hlsl(kind) + " tangent:TANGENT;"
        if name.startswith("in_TEXCOORD"):
            n = name.removeprefix("in_TEXCOORD")
            return hlsl(kind) + " " + ("uv" if n == "0" else "uv" + n) + ":TEXCOORD" + n + ";"
        mapped = {"vs_COLOR0": ("color", "COLOR"), "vs_TEXCOORD0": ("uv", "TEXCOORD0"),
                  "vs_TEXCOORD1": ("world", "TEXCOORD1"), "vs_TEXCOORD2": ("mask", "TEXCOORD2")}
        if name in mapped: member, semantic = mapped[name]
        elif name.startswith("vs_TEXCOORD"): member, semantic = "uv" + name[-1], "TEXCOORD" + name[-1]
        else: raise ValueError("Restore original shader semantic: " + name)
        return hlsl(kind) + " " + member + ":" + semantic + ";"
    return ("struct Input { " + " ".join(field(t, n, True) for t, n in inputs) + " };\n" +
            "struct Varying { float4 position:SV_POSITION; " + " ".join(field(t, n, False) for t, n in outputs) + " };\n")


def pass_state(state):
    blends = ("Zero", "One", "DstColor", "SrcColor", "OneMinusDstColor", "SrcAlpha", "OneMinusSrcColor", "DstAlpha", "OneMinusDstAlpha", "SrcAlphaSaturate", "OneMinusSrcAlpha")
    def value(v, values=None):
        if v.name not in ("", "<noninit>"): return "[" + v.name + "]"
        return values[int(v.val)] if values else str(int(v.val))
    b = state.rtBlend0
    mask = int(b.colMask.val)
    color_mask = "".join(letter for bit, letter in [(8,"R"),(4,"G"),(2,"B"),(1,"A")] if mask & bit) or "0"
    tags = "Tags { " + " ".join('"' + k + '"="' + v + '"' for k, v in state.m_Tags.tags) + " }"
    return "\n".join([tags, "Cull " + value(state.culling, ("Off","Front","Back")),
        "ZWrite " + value(state.zWrite, ("Off","On")),
        "ZTest " + value(state.zTest, ("Disabled","Never","Less","Equal","LEqual","Greater","NotEqual","GEqual","Always")),
        "Blend " + value(b.srcBlend, blends) + " " + value(b.destBlend, blends) + ", " + value(b.srcBlendAlpha, blends) + " " + value(b.destBlendAlpha, blends),
        "BlendOp " + value(b.blendOp, ("Add","Sub","RevSub","Min","Max")) + ", " + value(b.blendOpAlpha, ("Add","Sub","RevSub","Min","Max")),
        "ColorMask " + color_mask])


def translate(shader, name):
    programs = gles_programs(shader, with_keywords=True)
    if len(shader.m_ParsedForm.m_SubShaders) != 1 or len(shader.m_ParsedForm.m_SubShaders[0].m_Passes) != 1:
        raise ValueError("Recheck original water shader passes")
    keywords = sorted({k for p in programs for k in p["keywords"]})
    props = []
    for p in shader.m_ParsedForm.m_PropInfo.m_Props:
        attributes = "".join("[" + a + "] " for a in p.m_Attributes)
        values = [getattr(p, "m_DefValue_" + str(i) + "_") for i in range(4)]
        if p.m_Type == 4:
            default = p.m_DefTexture.m_DefaultName
            props.append(p.m_Name + ' ("' + p.m_Description + '", 2D) = "' + default + '" {}')
        elif p.m_Type in (0, 1):
            props.append(p.m_Name + ' ("' + p.m_Description + '", ' + ("Color" if p.m_Type == 0 else "Vector") + ") = (" + ",".join(map(str, values)) + ")")
        elif p.m_Type in (2, 3):
            kind = "Float" if p.m_Type == 2 else "Range(" + str(values[1]) + "," + str(values[2]) + ")"
            props.append(p.m_Name + ' ("' + p.m_Description + '", ' + kind + ') = ' + str(values[0]))
        else: raise ValueError("Restore original water property kind")
        props[-1] = attributes + props[-1]
    sub = shader.m_ParsedForm.m_SubShaders[0]
    text = 'Shader "Zjcs/Official/World/Map13/' + name + '" {\nProperties {\n' + "\n".join(props) + "\n}\nSubShader {\n"
    text += "Tags { " + " ".join('"' + k + '"="' + v + '"' for k, v in sub.m_Tags.tags) + " }\n"
    text += "Pass {\n" + pass_state(sub.m_Passes[0].m_State) + "\nCGPROGRAM\n#pragma target 3.0\n#pragma vertex vert\n#pragma fragment frag\n"
    if "_WATER_ABOVE" in keywords:
        text += "#pragma multi_compile_local _WATER_ABOVE _WATER_UNDER\n"
    for k in keywords:
        if k not in ("_WATER_ABOVE", "_WATER_UNDER"): text += "#pragma multi_compile __ " + k + "\n"
    text += '#include "UnityCG.cginc"\n'
    for i, p in enumerate(programs):
        if keywords:
            condition = " && ".join(("defined(" + k + ")") if k in p["keywords"] else ("!defined(" + k + ")") for k in keywords)
            text += ("#if " if i == 0 else "#elif ") + condition + "\n"
        vertex, vu = body(p["source"], True)
        fragment, fu = body(p["source"], False)
        uniforms = {}
        for u in sorted(set(vu + fu)): uniforms[u.split()[-1]] = u
        text += structure(p["source"]) + "\n" + "\n".join(uniforms.values()) + "\n" + vertex + "\n" + fragment + "\n"
    if keywords: text += "#endif\n"
    return text + "ENDCG\n}\n}\nFallback Off\n}\n", programs


def build():
    apk = Apk.current()
    _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Missing original shader CAB dependency")
    selected = {}
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                if obj.type.name != "Material": continue
                original = obj.read().m_Shader.deref()
                s = original.read()
                if s.m_ParsedForm.m_Name in FAMILIES: selected[s.m_ParsedForm.m_Name] = original
    if set(selected) != set(FAMILIES): raise ValueError("Original Map13 water shader family missing")
    outputs, sources = [], []
    SHADERS.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    import_meta(SHADERS, "DefaultImporter", folder=True)
    meta(SOURCE, folder=True)
    for original_name, obj in selected.items():
        name = FAMILIES[original_name]
        text, programs = translate(obj.read(), name)
        path = SHADERS / (name + ".shader")
        write(path, text.encode()); import_meta(path, "ShaderImporter")
        outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes()), original_shader=original_name))
        records = []
        for program in programs:
            p = SOURCE / (name + "-" + str(program["index"]) + ".glsl.txt")
            write(p, program["source"].encode()); meta(p)
            records.append(dict(index=program["index"], keywords=program["keywords"], path=source_path(p), sha256=sha(p.read_bytes())))
        sources.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), programs=records))
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=apk.version, inputs=sources, outputs=outputs,
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__), ROOT / "scripts/prepare_battle_hud_materials.py", ROOT / "scripts/official_shader_programs.py"]],
        validation=dict(path="unity/Assets/Game/Editor/OfficialMap13WaterShaderTools.cs",
                        sha256=sha((ROOT / "unity/Assets/Game/Editor/OfficialMap13WaterShaderTools.cs").read_bytes())),
        operation="Mechanical translation of all original water GLES variants, preserving arithmetic order, uniforms, precision qualifiers, vertex semantics, original pass tags, blend/depth/cull/color-mask state and material property defaults. Original executable programs retained beside source configuration.",
        limitations=["Rendered comparison remains required; materials/prefab bindings and original water mask rendering are not installed by this generator. Compilation is checked separately by the recorded Editor validator."])))
    print("Translated Map13 water shaders:", len(outputs), "families,", sum(len(s["programs"]) for s in sources), "variants")


if __name__ == "__main__":
    build()
