"""Convert the exact GLES programs used by native Map13 entity materials."""
from copy import copy
import json
from pathlib import Path
import re

from prepare_unity_map13_entity_materials import CACHE, OUTPUT as MATERIALS, RECEIPT as MATERIAL_RECEIPT
from official_apk import ROOT
from official_shader_programs import gles_programs
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map13_environment_shader import program_body as environment_body, render_state
from prepare_unity_map13_pipeline_shaders import program_structure
from prepare_battle_hud_materials import hlsl
import UnityPy


SHADERS = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders"
SOURCES = ROOT / "unity/Assets/Official/World/Map/13/Source/EntityShaders"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-shaders-source-manifest.json"


def folder_meta(path, official=False):
    target = Path(str(path) + ".meta")
    if target.exists():
        text = target.read_text()
        if "folderAsset: yes" not in text or "DefaultImporter:" not in text or not re.search(r"(?m)^guid: [a-f0-9]{32}$", text):
            raise ValueError("Unexpected entity shader folder metadata: " + str(path))
        return
    if official:
        meta(path, folder=True)
    else:
        import_meta(path, "DefaultImporter", folder=True)


def program_body(program, vertex):
    projection = "hlslcc_mtx4x4glstate_matrix_projection" in program
    light_matrix = "hlslcc_mtx4x4_MainLightWorldToLight" in program
    program = re.sub(r"(?m)^\s*(?:UNITY_UNIFORM|uniform)\s+(?:(?:mediump|highp)\s+)?vec4\s+(?:unity_LODFade|_WorldSpaceLightPos0|unity_SH[AB][rgb]|unity_SHC|unity_SpecCube0_HDR);\s*$", "", program)
    cube0 = "textureLod(unity_SpecCube0," in program
    cube_helper = "Map13SampleSpecCube0" + ("Vertex" if vertex else "Fragment")
    if cube0:
        program = re.sub(r"(?m)^(?:UNITY_LOCATION\(\d+\)\s+)?uniform\s+(?:(?:mediump|highp)\s+)?samplerCube\s+unity_SpecCube0;\s*$", "", program)
        program = program.replace("textureLod(unity_SpecCube0,", cube_helper + "(")
    # UnityCG supplies these original per-draw matrices. Keep column access
    # order, rather than converting the original expressions to mul().
    program = re.sub(r"(?m)^(?:UNITY_UNIFORM|uniform)\s+vec4\s+hlslcc_mtx4x4(?:glstate_matrix_projection|unity_WorldToObject)\[4\];\s*$", "", program)
    program = re.sub(r"hlslcc_mtx4x4unity_WorldToObject\[(\d)\]",
        lambda m: "vec4(" + ",".join("unity_WorldToObject[" + str(i) + "][" + m[1] + "]" for i in range(4)) + ")", program)
    program = re.sub(r"(?m)^(?:UNITY_UNIFORM|uniform)\s+vec4\s+hlslcc_mtx4x4_MainLightWorldToLight\[4\];\s*$", "", program)
    program = re.sub(r"hlslcc_mtx4x4_MainLightWorldToLight\[(\d)\]",
        lambda m: "vec4(" + ",".join("_MainLightWorldToLight[" + str(i) + "][" + m[1] + "]" for i in range(4)) + ")", program)
    # SV_POSITION supplies the native pixel/depth/reciprocal-W conventions.
    # Original DepthOnly writes this same native depth to its depth texture.
    program = program.replace("gl_FragCoord", "Map13NativeFragmentPosition")
    code, uniforms = environment_body(program, vertex)
    if cube0 and cube_helper + "(" in code:
        code = "float4 " + cube_helper + "(float3 direction, float lod) { return UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, direction, lod); }\n" + code
    head = (program.split("#ifdef FRAGMENT")[0] if vertex else program.split("#ifdef FRAGMENT")[1]).split("void main()", 1)[0]
    # HLSLcc places immediate constant arrays before uniforms, outside the
    # generic stage reader's trailing temporary-declaration range.
    for declaration in re.findall(r"^(?:(?:mediump|highp|lowp)\s+)?(?:[biu]?vec[234]|float|int|uint|bool)\s+\w+(?:\[\d+\])?;", head, re.M):
        converted = hlsl(declaration)
        if converted not in code:
            marker = "= (Varying)0;" if vertex else "half4 result = 0;"
            if marker not in code:
                raise ValueError("Original entity temporary has no native stage scope")
            code = code.replace(marker, marker + "\n" + converted)
    if light_matrix:
        uniforms.append("float4x4 _MainLightWorldToLight;")
    if projection and vertex and "hlslcc_mtx4x4unity_MatrixVP" not in program.split("#ifdef FRAGMENT")[0]:
        # Projection-based UI vertices already use the native projection. The
        # shared pipeline converter's procedural-blit adjustment does not apply.
        adjustment = ("output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;\n"
            "#if UNITY_UV_STARTS_AT_TOP\noutput.position.y = -output.position.y;\n#endif\n")
        if code.count(adjustment) != 1:
            raise ValueError("Projection vertex has an unexpected native adjustment")
        code = code.replace(adjustment, "")
    if "Map13NativeFragmentPosition" in code:
        if vertex:
            raise ValueError("Fragment position used by an original vertex program")
        code = code.replace("Map13NativeFragmentPosition", "input.position")
    return code, uniforms


def referenced_programs(pass_):
    return [p for stage_name in ("progVertex", "progFragment", "progGeometry", "progHull", "progDomain", "progRayTracing")
            for stage in [getattr(pass_, stage_name, None)] if stage is not None
            for p in [*stage.m_SubPrograms, *(p for platform in (stage.m_PlayerSubPrograms or []) for p in platform)]]


def reuse_candidates():
    result = {}
    for path in sorted((ROOT / "assets/reference/official-map").glob("map13-*-source-manifest.json")):
        if not any(key in path.name for key in ("shader", "flag")) or path == RECEIPT:
            continue
        receipt = json.loads(path.read_bytes())
        for output in receipt.get("outputs", []):
            original = output.get("original_shader")
            if not original:
                continue
            inputs = receipt.get("inputs", [])
            matching = [i for i in inputs if i.get("object_sha256") and
                        (i.get("shader") == original or i.get("original_shader") == original)]
            # Earlier receipts pair one input and one output in stable order.
            if not matching and len(inputs) == len(receipt["outputs"]):
                matching = [inputs[receipt["outputs"].index(output)]]
            for input_ in matching:
                if input_.get("object_sha256"):
                    result.setdefault((original, input_["object_sha256"]), []).append(
                        dict(receipt=source_path(path), receipt_sha256=sha(path.read_bytes()),
                             output=output, input=input_))
    return result


def build():
    material_receipt = json.loads(MATERIAL_RECEIPT.read_bytes())
    if sha(MATERIALS.read_bytes()) != material_receipt["outputs"][0]["sha256"]:
        raise ValueError("Original material source changed")
    materials = json.loads(MATERIALS.read_bytes())
    if sha((ROOT / material_receipt["input_receipt"]["path"]).read_bytes()) != material_receipt["input_receipt"]["sha256"]:
        raise ValueError("Original entity input receipt changed")
    for path in [SHADERS.parent, SHADERS]:
        folder_meta(path)
    folder_meta(SOURCES, official=True)
    candidates = reuse_candidates()
    sources, outputs, reused, engine = [], [], [], []
    loaded = {}
    for entry in materials["shaders"]:
        if entry["input"] == "unity default resources":
            engine.append(entry)
            continue
        key = entry["name"], entry["payload_sha256"]
        if key in candidates:
            candidate = candidates[key][0]
            path = ROOT / candidate["output"]["path"]
            if sha(path.read_bytes()) != candidate["output"]["sha256"]:
                raise ValueError("Previously migrated original entity shader changed")
            reused.append(dict(original=entry, **candidate))
            continue
        input_path = CACHE / "inputs" / entry["input"]
        if entry["input"] not in loaded:
            input_receipt = json.loads((ROOT / material_receipt["input_receipt"]["path"]).read_bytes())
            expected = next(i for i in input_receipt["inputs"] if i["output"] == entry["input"])
            raw = input_path.read_bytes()
            if sha(raw) != expected["converter_input_sha256"]:
                raise ValueError("Original entity shader input changed")
            loaded[entry["input"]] = UnityPy.load(raw)
        obj = next(o for o in loaded[entry["input"]].objects
                   if o.type.name == "Shader" and o.path_id == int(entry["path_id"]))
        if sha(obj.get_raw_data()) != entry["payload_sha256"]:
            raise ValueError("Original entity shader payload changed")
        shader = obj.read()
        if len(shader.m_ParsedForm.m_SubShaders) != 1:
            raise ValueError("Restore original entity subshader selection")
        name = re.sub(r"[^A-Za-z0-9]", "", entry["name"]) + "_" + sha(entry["name"].encode())[:8]
        passes, records, stripped, header = [], [], [], None
        for pi, original in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
            if original.m_Type != 0:
                raise ValueError("Restore original entity UsePass/GrabPass")
            # Empty stripped passes have no executable entries. Never let the
            # generic program reader fall back to unrelated shader programs.
            if not referenced_programs(original):
                passes.append("Pass {\n" + render_state(original.m_State) + "\n}\n")
                stripped.append(pi)
                continue
            sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original]
            form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
            one = copy(shader); one.m_ParsedForm = form
            text, programs = translate(one, "Entity/" + name, program_body=program_body,
                program_structure=program_structure, render_state=render_state, target="4.5")
            for keyword, flags in zip(form.m_KeywordNames, form.m_KeywordFlags):
                if flags not in (0, 1):
                    raise ValueError("Unknown original entity keyword scope")
                if flags == 1:
                    text = text.replace("#pragma multi_compile __ " + keyword + "\n",
                                        "#pragma multi_compile_local __ " + keyword + "\n")
            # Original KeywordEnum properties select one alternative, not an
            # independent set of Boolean flags. Keep that original choice group.
            for property_ in form.m_PropInfo.m_Props:
                for attribute in property_.m_Attributes:
                    if attribute.startswith("KeywordEnum(") and attribute.endswith(")"):
                        choices = [property_.m_Name.upper() + "_" + item.strip().upper().replace(" ", "_")
                                   for item in attribute[len("KeywordEnum("):-1].split(",")]
                        available = {keyword for program in programs for keyword in program["keywords"]}
                        choices = [keyword for keyword in choices if keyword in available]
                        # Some retained inspector enums have no executable
                        # keyword usage in the stripped original programs.
                        if not choices or any(len(set(program["keywords"]) & set(choices)) != 1 for program in programs):
                            continue
                        scopes = []
                        for keyword in choices:
                            text, count = re.subn(r"(?m)^#pragma (multi_compile(?:_local)?) __ " + re.escape(keyword) + r"\n",
                                lambda match: scopes.append(match[1]) or "", text)
                            if count != 1:
                                raise ValueError("Original KeywordEnum alternative has no unique shader pragma")
                        if len(set(scopes)) != 1:
                            raise ValueError("Original KeywordEnum alternatives have different scopes")
                        text = text.replace('#include "UnityCG.cginc"', "#pragma " + scopes[0] + " " + " ".join(choices) + '\n#include "UnityCG.cginc"')
            fixed = set.intersection(*(set(program["keywords"]) for program in programs))
            for keyword in sorted(fixed):
                text, count = re.subn(r"(?m)^#pragma multi_compile(?:_local)? (?:__ )?" + re.escape(keyword) + r"$",
                    "#define " + keyword + " 1", text)
                if count != 1:
                    raise ValueError("Original pass-fixed keyword has no unique pragma: " + keyword)
            begin = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
            pass_header = text[:begin]
            lod = shader.m_ParsedForm.m_SubShaders[0].m_LOD
            if lod:
                pass_header = pass_header.replace("SubShader {\n", "SubShader {\nLOD " + str(lod) + "\n", 1)
            if header is None:
                header = pass_header
            elif header != pass_header:
                raise ValueError("Original entity pass properties differ")
            passes.append(text[begin:end])
            for program in programs:
                path = SOURCES / (name + "-p" + str(pi) + "-" + str(program["index"]) + ".glsl.txt")
                write(path, program["source"].encode()); meta(path)
                records.append(dict(pass_index=pi, index=program["index"], keywords=program["keywords"],
                                    path=source_path(path), sha256=sha(path.read_bytes())))
        if header is None:
            raise ValueError("Original entity shader has no executable pass")
        output = SHADERS / (name + ".shader")
        write(output, (header + "".join(passes) + "}\nFallback Off\n}\n").encode())
        import_meta(output, "ShaderImporter")
        outputs.append(dict(path=source_path(output), sha256=sha(output.read_bytes()), original_shader=entry["name"]))
        sources.append(dict(original=entry, programs=records, stripped_passes=stripped,
                            original_lod=shader.m_ParsedForm.m_SubShaders[0].m_LOD,
                            original_fallback=shader.m_ParsedForm.m_FallbackName))
        print("Converted", entry["name"], len(passes), "passes", len(records), "GLES programs", flush=True)
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=materials["manifest"],
        material_source=dict(path=source_path(MATERIALS), sha256=sha(MATERIALS.read_bytes())),
        inputs=sources, outputs=outputs, reused=reused, engine_shaders=engine,
        validation=dict(path="unity/Assets/Game/Editor/Map13EntityShaders/OfficialMap13EntityShaderTools.cs",
            sha256=sha((ROOT / "unity/Assets/Game/Editor/Map13EntityShaders/OfficialMap13EntityShaderTools.cs").read_bytes())),
        gpu_validation=dict(path="unity/Assets/Game/Editor/Map13EntityShaders/OfficialMap13EntityShaderGpuAcceptance.cs",
            sha256=sha((ROOT / "unity/Assets/Game/Editor/Map13EntityShaders/OfficialMap13EntityShaderGpuAcceptance.cs").read_bytes())),
        folder_metadata=[dict(path=source_path(Path(str(p) + ".meta")), sha256=sha(Path(str(p) + ".meta").read_bytes()))
                         for p in [SHADERS.parent, SHADERS, SOURCES]],
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__),
            ROOT / "scripts/prepare_unity_map13_water_shaders.py", ROOT / "scripts/prepare_unity_map13_environment_shader.py",
            ROOT / "scripts/prepare_unity_map13_pipeline_shaders.py", ROOT / "scripts/prepare_battle_hud_materials.py",
            ROOT / "scripts/official_shader_programs.py"]],
        operation="Mechanically convert every original executable GLES pass/variant of the selected entity shaders, preserving properties, keyword scope, column arithmetic, precision and pass state. Reuse existing conversions only when original shader payload and output hashes match. Retain originally stripped passes without borrowing programs from another pass.",
        limitations=["The APK engine shaders listed separately require Windows binding verification.",
            "The original fallback names are recorded; their complete native shader dependency bindings remain required before formal runtime use.",
            "Windows compilation, rendered comparison, native materials/texture bindings and actual gameplay remain required.",
            "Originally stripped passes have no executable in the APK and are retained with their original state only."])))
    print("Entity shaders:", len(outputs), "converted,", len(reused), "verified reuse,", len(engine), "engine", flush=True)


if __name__ == "__main__":
    build()
