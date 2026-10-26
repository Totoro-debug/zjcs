"""Translate the original Map12 lava edge shader."""
from copy import copy
from pathlib import Path
import re

from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map12_native import ROOTS
from prepare_unity_map12_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate, SHADERS
from prepare_unity_map13_pipeline_shaders import render_state, program_body as pipeline_program_body
from prepare_battle_hud_materials import hlsl


def program_body(program, vertex):
    # Original INSTANCING_ON GLES programs explicitly read element zero and
    # contain no instance ID. Bind the same per-renderer native Unity matrix;
    # retain all column arithmetic and fail on a different array access.
    accesses = re.findall(r"unity_Builtins0Array\[(\d+)\]\.(\w+)", program)
    if any(i != "0" or n != "hlslcc_mtx4x4unity_ObjectToWorldArray" for i, n in accesses):
        raise ValueError("Restore additional original environment instancing data")
    program = program.replace("unity_Builtins0Array[0].hlslcc_mtx4x4unity_ObjectToWorldArray", "hlslcc_mtx4x4unity_ObjectToWorld")
    program = re.sub(r"(?m)^\s*UNITY_UNIFORM\s+", "uniform ", program)
    # UnityCG provides the native object transform already; the generic stage
    # reader maps these original column accesses to it without a second uniform.
    program = re.sub(r"(?m)^uniform\s+vec4\s+hlslcc_mtx4x4unity_ObjectToWorld\[4\];\s*$", "", program)
    code, uniforms = pipeline_program_body(program, vertex)
    head = (program.split("#ifdef FRAGMENT")[0] if vertex else program.split("#ifdef FRAGMENT")[1]).split("void main()", 1)[0]
    for declaration in re.findall(r"^(?:(?:mediump|highp|lowp)\s+)?(?:[biu]?vec[234]|float|int|uint|bool)\s+phase\w+(?:\[\d+\])?;", head, re.M):
        converted = hlsl(declaration)
        if converted not in code:
            marker = "= (Varying)0;" if vertex else "half4 result = 0;"
            if marker not in code: raise ValueError("Original environment phase declaration has no native stage scope")
            code = code.replace(marker, marker + "\n" + converted)
    while (start := code.find("equal(")) >= 0:
        at = start + len("equal("); depth = 0; comma = None
        for end in range(at, len(code)):
            if code[end] == "(": depth += 1
            elif code[end] == ")":
                if depth == 0: break
                depth -= 1
            elif code[end] == "," and depth == 0: comma = end
        if comma is None: raise ValueError("Original vector equality has no operands")
        code = code[:start] + "((" + code[at:comma] + ") == (" + code[comma + 1:end] + "))" + code[end + 1:]
    return code, uniforms


SOURCE = ROOT / "unity/Assets/Official/World/Map/12/Source/Shaders"


def build():
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Official environment shader CAB closure is incomplete")
    shaders = [obj for bundle in apk.loaded.values() for file in serialized_files(bundle)
               for obj in file.objects.values() if obj.type.name == "Shader" and
               obj.read().m_ParsedForm.m_Name == "Sprites/LavaEdge"]
    if len(shaders) != 1: raise ValueError("Original environment shader is not unique")
    obj = shaders[0]; shader = obj.read()
    if len(shader.m_ParsedForm.m_SubShaders) != 1: raise ValueError("Restore original environment subshaders")
    passes, programs, header = [], [], None
    for pi, original in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
        sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original]
        form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
        one = copy(shader); one.m_ParsedForm = form
        text, variants = translate(one, "LavaEdge", program_body=program_body, render_state=render_state)
        begin = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
        if header is None: header = text[:begin]
        elif header != text[:begin]: raise ValueError("Original environment shader pass properties differ")
        passes.append(text[begin:end])
        for variant in variants:
            path = SOURCE / ("LavaEdge-p" + str(pi) + "-" + str(variant["index"]) + ".glsl.txt")
            write(path, variant["source"].encode()); meta(path)
            programs.append(dict(pass_index=pi, index=variant["index"], keywords=variant["keywords"],
                                 path=source_path(path), sha256=sha(path.read_bytes())))
    target = SHADERS / "LavaEdge.shader"
    write(target, (header + "".join(passes) + "}\nFallback Off\n}\n").encode()); import_meta(target, "ShaderImporter")
    write(ROOT / "assets/reference/official-map/map12-environment-shader-source-manifest.json", encode(dict(
        format=1, mapClassId=12, manifest=apk.version,
        inputs=[dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), programs=programs)],
        outputs=[dict(path=source_path(target), sha256=sha(target.read_bytes()), original_shader="Sprites/LavaEdge")],
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__),
                    ROOT / "scripts/prepare_unity_map13_water_shaders.py",
                    ROOT / "scripts/prepare_unity_map13_pipeline_shaders.py", ROOT / "scripts/prepare_battle_hud_materials.py"]],
        operation="Mechanically translate every original environment noise shader pass and GLES variant, preserving material properties, arithmetic order, inputs, tags, blend/depth/cull/color state and original source programs.",
        limitations=["Windows compilation and native prefab runtime rendering remain required."])))
    print("Translated original environment noise shader:", len(passes), "passes,", len(programs), "variants")


if __name__ == "__main__": build()
