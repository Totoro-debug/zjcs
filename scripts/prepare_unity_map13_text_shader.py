"""Translate every original world-label TMP shader program, without substitutions."""
from pathlib import Path
import re

from official_apk import Apk, ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate, structure, SHADERS, SOURCE
from prepare_unity_map13_pipeline_shaders import program_body as pipeline_body, render_state as pipeline_state


def program_body(program, vertex):
    # TMP projects transformed normals using the original inverse object matrix.
    # Reconstruct its columns from Unity's native matrix, as for ObjectToWorld.
    program = re.sub(r"(?m)^uniform\s+vec4\s+hlslcc_mtx4x4(?:unity_WorldToObject|unity_ObjectToWorld|glstate_matrix_projection)\[4\];\s*$", "", program)
    program = re.sub(r"hlslcc_mtx4x4unity_WorldToObject\[(\d)\]",
        lambda m: "vec4(" + ",".join("unity_WorldToObject[" + str(i) + "][" + m[1] + "]" for i in range(4)) + ")", program)
    program = semantics(program)
    code, uniforms = pipeline_body(program, vertex)
    if "float3(dot(" in code:
        helper = "Map13TextSplatDot" + ("Vertex" if vertex else "Fragment")
        code = "float3 " + helper + "(float value) { return float3(value, value, value); }\n" + code.replace("float3(dot(", helper + "(dot(")
    return code, uniforms


def semantics(program):
    # The original extra outline colors need independent interpolators. COLOR0
    # stays COLOR; the other colors use otherwise unused native TEXCOORD slots.
    if re.search(r"\bvs_TEXCOORD[67]\b", program): raise ValueError("TMP varying collision")
    return program.replace("vs_COLOR1", "vs_TEXCOORD6").replace("vs_COLOR2", "vs_TEXCOORD7")


def render_state(state):
    text = pipeline_state(state)
    mask = state.rtBlend0.colMask
    if mask.name not in ("", "<noninit>"):
        text, count = re.subn(r"(?m)^ColorMask \w+$", "ColorMask [" + mask.name + "]", text)
        if count != 1: raise ValueError("Original text color mask binding differs")
    return text


def build():
    apk = Apk.current()
    font = apk.asset("textmesh_pro_assets_fonts_&_materials", "/FZY4K SDF - dynamic.asset")
    # Follow the original serialized material pointer, not a shader-name search.
    import struct
    from UnityPy.classes.PPtr import PPtr
    file_id, path_id = struct.unpack_from("<iq", font.get_raw_data(), 56)
    material = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=font.assets_file).deref()
    obj = material.read().m_Shader.deref()
    if obj.read().m_ParsedForm.m_Name != "Hidden/JSZX/TextMeshPro":
        raise ValueError("Original world-label shader binding differs")
    text, programs = translate(obj.read(), "WorldText", program_body=program_body,
                               program_structure=lambda p: structure(semantics(p)), render_state=render_state)
    if len(programs) != 128: raise ValueError("Original text shader variant count differs")
    target = SHADERS / "WorldText.shader"
    write(target, text.encode()); import_meta(target, "ShaderImporter")
    records = []
    for program in programs:
        path = SOURCE / ("WorldText-" + str(program["index"]) + ".glsl.txt")
        write(path, program["source"].encode()); meta(path)
        records.append(dict(index=program["index"], keywords=program["keywords"],
                            path=source_path(path), sha256=sha(path.read_bytes())))
    write(ROOT / "assets/reference/official-map/map13-text-shader-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version,
        inputs=[dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), programs=records)],
        bindings=[dict(source=apk.provenance(x), object_sha256=sha(x.get_raw_data())) for x in [font, material]],
        outputs=[dict(path=source_path(target), sha256=sha(target.read_bytes()), original_shader="Hidden/JSZX/TextMeshPro")],
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__),
            ROOT / "scripts/prepare_unity_map13_water_shaders.py", ROOT / "scripts/prepare_unity_map13_pipeline_shaders.py",
            ROOT / "scripts/prepare_battle_hud_materials.py", ROOT / "scripts/official_shader_programs.py"]],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13TextTools.cs",
            ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13TextAcceptance.cs"]],
        operation="Mechanically translate all 128 original TMP GLES programs and both GPU stages. Preserve original property defaults, tags, stencil/blend/depth/cull/color state, dual outline, underlay, glow, gray and UI clipping. Reconstruct original object/inverse/projection matrix columns from native Unity matrices. Keep the original arithmetic and source programs.",
        limitations=["Windows compilation, font/native material binding and actual text rendering require separate validation."])))
    print("Translated original world-label text shader: 128 variants, both GPU stages")


if __name__ == "__main__": build()
