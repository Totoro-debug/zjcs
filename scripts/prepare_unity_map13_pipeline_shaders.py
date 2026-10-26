"""Recover executable APK Renderer2D programs, including every original pass."""
from copy import copy
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_shader_programs import gles_programs
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate, structure, expressions, pass_state, SHADERS, SOURCE
from prepare_battle_hud_materials import stage, hlsl


FAMILIES = {65: "LightShapeVolume", 77: "LightPoint", 71: "LightPointVolume", 66: "CoreBlit",
            74: "CoreBlitColorDepth", 50: "Sampling", 72: "ShadowProjected", 56: "ShadowSprite",
            76: "UnshadowSprite", 64: "UnshadowGeometry", 78: "FallbackError", 73: "DebugReplacement"}


def vector_comparisons(text):
    for original, operator in [("lessThan", "<"), ("greaterThanEqual", ">=")]:
        while (start := text.find(original + "(")) >= 0:
            at = start + len(original) + 1; depth = 0; comma = None
            for end in range(at, len(text)):
                if text[end] == "(": depth += 1
                elif text[end] == ")":
                    if depth == 0: break
                    depth -= 1
                elif text[end] == "," and depth == 0: comma = end
            if comma is None: raise ValueError("Original vector comparison has no operands")
            text = text[:start] + "(" + text[at:comma] + " " + operator + text[comma + 1:end] + ")" + text[end + 1:]
    return text


def program_body(program, vertex):
    code, _ = stage(program, vertex)
    source_head = (program.split("#ifdef FRAGMENT")[0] if vertex else program.split("#ifdef FRAGMENT")[1]).split("void main()", 1)[0]
    # HLSLcc may place phase temporaries between output declarations; the UGUI
    # stage reader starts locals after the last output and must retain these too.
    for declaration in re.findall(r"^(?:(?:mediump|highp|lowp)\s+)?(?:vec[234]|float)\s+phase\w+;", source_head, re.M):
        converted = hlsl(declaration)
        if converted not in code: code = code.replace("= (Varying)0;", "= (Varying)0;\n" + converted)
    code = vector_comparisons(expressions(code))
    code = re.sub(r"\bSV_TARGET0\b", "result", code)
    sizes = {}
    for size, variable in re.findall(r"\b(?:[biu]?vec)([234])\s+(\w+)", source_head):
        sizes[expressions(hlsl(variable))] = int(size)
    def constructor(match):
        kind, count, operand = match[1], int(match[2]), match[3]
        tail = operand.rsplit(".", 1)[-1] if "." in operand else ""
        size = len(tail) if tail and all(c in "xyzwrgba" for c in tail) else sizes.get(operand, 1)
        if size > 1:
            if size != count: raise ValueError("Original vector constructor size differs: " + match[0])
            return kind + str(count) + "(" + operand + ")"
        return match[0]
    # The UI translator only needed scalar splats; Renderer2D also casts vectors.
    code = re.sub(r"\b(float|half|int|bool)([234])\(([\w.]+)(?:,\3)+\)", constructor, code)
    if "float4(dot(" in code:
        helper = "Map13SplatDot" + ("Vertex" if vertex else "Fragment")
        code = "float4 " + helper + "(float value) { return float4(value, value, value, value); }\n" + code.replace("float4(dot(", helper + "(dot(")
    for name, index in [("NORMAL0", 8), ("TANGENT0", 9)]:
        code = code.replace("vs_" + name, ("output" if vertex else "input") + ".uv" + str(index))
    code = re.sub(r"hlslcc_mtx4x4_LightInvMatrix\[(\d)\]",
        lambda m: "float4(" + ",".join("_LightInvMatrix[" + str(i) + "][" + m[1] + "]" for i in range(4)) + ")", code)
    code = code.replace("gl_VertexID", "input.vertexID")
    code = re.sub(r"textureLod\((\w+), ([^,\n]+), ([^)\n]+)\)",
        lambda m: ("texCUBElod(" + m[1] + ", float4(" + m[2] + ", " + m[3] + "))")
        if "samplerCube " + m[1] in program else
        "tex2Dlod(" + m[1] + ", float4(" + m[2] + ", 0, " + m[3] + "))", code)
    helper = re.search(r"int int_bitfieldInsert\([^{}]+\) \{[^{}]+\}", code)
    if helper:
        code = code[:helper.start()] + code[helper.end():]
        code = helper[0] + "\n" + code
    if vertex and "hlslcc_mtx4x4unity_MatrixVP" not in program.split("#ifdef FRAGMENT")[0]:
        # Procedural GLES blits use the GL near plane (-1); DX12 reverses depth.
        # Matrix-based vertices already use the native Unity projection matrix.
        code = code.replace("return output;", "output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;\n"
            "#if UNITY_UV_STARTS_AT_TOP\noutput.position.y = -output.position.y;\n#endif\nreturn output;")
    if not vertex and "gl_FragDepth" in code:
        code = code.replace("half4 frag(Varying input) : SV_Target { half4 result = 0;",
            "struct FragmentResult { half4 color:SV_Target; float depth:SV_Depth; };\n"
            "FragmentResult frag(Varying input) { FragmentResult fragment = (FragmentResult)0; half4 result = 0;")
        code = code.replace("gl_FragDepth", "fragment.depth").replace("return result;", "fragment.color = result; return fragment;")
    if not vertex: code = code.replace("output.", "input.")
    head = (program.split("#ifdef FRAGMENT")[0] if vertex else program.split("#ifdef FRAGMENT")[1]).split("void main()", 1)[0]
    uniforms = []
    skip = {"_ProjectionParams", "_Time", "_ScreenParams", "_WorldSpaceCameraPos", "unity_OrthoParams", "_ZBufferParams"}
    for match in re.finditer(r"^(?:UNITY_LOCATION\(\d+\)\s+)?uniform\s+((?:(?:mediump|highp)\s+)?(?:float|int|uint|vec[234]|sampler2D|samplerCube))\s+(\w+)(\[\d+\])?;", head, re.M):
        kind, name, array = match.groups()
        if name.startswith("hlslcc_mtx"):
            if name == "hlslcc_mtx4x4_LightInvMatrix": uniforms.append("float4x4 _LightInvMatrix;")
            elif name not in ("hlslcc_mtx4x4unity_MatrixVP", "hlslcc_mtx4x4unity_MatrixV"):
                raise ValueError("Restore original matrix: " + name)
        elif name not in skip: uniforms.append(hlsl(kind).replace("samplerCube", "samplerCUBE") + " " + name + (array or "") + ";")
    if re.search(r"hlslcc_mtx|\bin_\w+|\bvs_\w+|lessThan\(|greaterThan|textureLod\(|gl_", code):
        raise ValueError("Untranslated Renderer2D program: " + code)
    return code, uniforms


def program_structure(program):
    program = program.replace("vs_NORMAL0", "vs_TEXCOORD8").replace("vs_TANGENT0", "vs_TEXCOORD9")
    value = structure(program)
    if "gl_VertexID" in program: value = value.replace("struct Input {", "struct Input { uint vertexID:SV_VertexID;")
    return value


def render_state(state):
    value = pass_state(state).replace("ZTest Disabled", "ZTest Off")
    def item(v, names=None):
        if v.name not in ("", "<noninit>"): return "[" + v.name + "]"
        return names[int(v.val)] if names else str(int(v.val))
    compare = ("Disabled", "Never", "Less", "Equal", "LEqual", "Greater", "NotEqual", "GEqual", "Always")
    operations = ("Keep", "Zero", "Replace", "IncrSat", "DecrSat", "Invert", "IncrWrap", "DecrWrap")
    value += "\nStencil { Ref " + item(state.stencilRef) + " ReadMask " + item(state.stencilReadMask) + " WriteMask " + item(state.stencilWriteMask)
    for suffix, op in [("", state.stencilOp), ("Front", state.stencilOpFront), ("Back", state.stencilOpBack)]:
        if suffix and all(getattr(op, k).val == v and getattr(op, k).name in ("", "<noninit>")
                          for k, v in [("comp", 8), ("fail", 0), ("pass_", 0), ("zFail", 0)]): continue
        value += " Comp" + suffix + " " + item(op.comp, compare) + " Pass" + suffix + " " + item(op.pass_, operations)
        value += " Fail" + suffix + " " + item(op.fail, operations) + " ZFail" + suffix + " " + item(op.zFail, operations)
    return value + " }\nAlphaToMask " + item(state.alphaToMask, ("Off", "On"))


def build():
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    file = next(f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers.assets")
    inputs, outputs = [], []
    for pid, name in FAMILIES.items():
        obj = file.objects[pid]; shader = obj.read(); passes, programs, header = [], [], None
        if len(shader.m_ParsedForm.m_SubShaders) != 1: raise ValueError("Restore original Renderer2D subshader selection")
        for index, original in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
            sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original]
            form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
            one = copy(shader); one.m_ParsedForm = form
            text, variants = translate(one, name, program_body=program_body, program_structure=program_structure,
                                       render_state=render_state, target="4.5")
            start = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
            if header is None: header = text[:start]
            elif header != text[:start]: raise ValueError("Renderer2D pass properties differ")
            passes.append(text[start:end])
            for variant in variants:
                path = SOURCE / (name + "-p" + str(index) + "-" + str(variant["index"]) + ".glsl.txt")
                write(path, variant["source"].encode()); meta(path)
                programs.append(dict(pass_index=index, index=variant["index"], keywords=variant["keywords"], path=source_path(path), sha256=sha(path.read_bytes())))
        path = SHADERS / (name + ".shader")
        write(path, (header + "".join(passes) + "}\nFallback Off\n}\n").encode()); import_meta(path, "ShaderImporter")
        outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes()), original_shader=shader.m_ParsedForm.m_Name, path_id=pid))
        inputs.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), programs=programs))
        print(name, len(passes), "passes", len(programs), "programs")
    receipt = ROOT / "assets/reference/official-map/map13-pipeline-shaders-source-manifest.json"
    write(receipt, encode(dict(format=1, mapClassId=13, manifest=apk.version, inputs=inputs, outputs=outputs,
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__), ROOT / "scripts/prepare_unity_map13_water_shaders.py", ROOT / "scripts/prepare_battle_hud_materials.py", ROOT / "scripts/official_shader_programs.py"]],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13PipelineShaderTools.cs", ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13PipelineAcceptance.cs"]],
        operation="Convert all executable original APK Renderer2D programs, retaining every pass, its stencil state, arithmetic and explicit LOD. Preserve vertex IDs, bitfield insertion and fragment-depth outputs. Reconstruct original matrix columns from native matrices. Procedural clip depth uses the native near plane, and render-target Y orientation follows UNITY_UV_STARTS_AT_TOP; GPU fixtures check exact mip pixels and row order.",
        limitations=["Stripped HDR-only programs and post-processing dependencies are handled separately; shader compilation and scene rendering still require validation."])))


if __name__ == "__main__": build()
