"""Bind the actual entity shader/fallback graph to native Windows resources."""
from copy import copy
import argparse
import json
from pathlib import Path
import re

from prepare_unity_map13_entity_graph import Inputs, OUTPUT as GRAPH, RECEIPT as GRAPH_RECEIPT
from prepare_unity_map13_entity_inputs import CACHE, RECEIPT as INPUT_RECEIPT
from prepare_unity_map13_entity_shaders import (
    RECEIPT as SHADER_RECEIPT, folder_meta, program_body, program_structure, render_state, translate)
from official_engine_shader_equivalence import prove, executable_cases
from official_platform_shader_programs import platform_shader
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from official_apk import ROOT
import UnityPy


SHADERS = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/BoundEntity"
SOURCES = ROOT / "unity/Assets/Official/World/Map/13/Source/EntityFallbackShaders"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-shader-bindings.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-shader-bindings-source-manifest.json"
CATALOG = ROOT / "unity/Assets/Game/Resources/Map13/Native/entity-shader-catalog.asset"
EDITOR_RESOURCES = Path("D:/Tool/Unity/Editor/Data/Resources/unity default resources")
RUNTIME = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13EntityAssets"
EDITOR = ROOT / "unity/Assets/Game/Editor/Map13EntityBindings"


def checked(path, expected):
    raw = path.read_bytes()
    if sha(raw) != expected:
        raise ValueError("Verified shader input changed: " + str(path))
    return raw


def build():
    graph_receipt = json.loads(GRAPH_RECEIPT.read_bytes())
    graph = json.loads(checked(GRAPH, graph_receipt["outputs"][0]["sha256"]))
    shader_receipt = json.loads(SHADER_RECEIPT.read_bytes())
    input_receipt = json.loads(checked(INPUT_RECEIPT, graph_receipt["input_receipt"]["sha256"]))
    original_inputs = Inputs(input_receipt["inputs"])
    indexes = {entry["output"]: i for i, entry in enumerate(input_receipt["inputs"])}
    originals = [o for o in graph["objects"] if o["type"] == "Shader"]
    if len(originals) != 21 or graph["manifest"] != "871_156598":
        raise ValueError("Original entity shader selection changed")
    names = {}
    for original in originals:
        name = original["name"]
        if name in names:
            raise ValueError("Ambiguous original shader name")
        names[name] = name if original["input"] == "unity default resources" else (
            "Zjcs/Official/World/Map13/BoundEntity/" + re.sub("[^A-Za-z0-9]", "", name) + "_" + sha(name.encode())[:8])
    for original in originals:
        if original["fallback"] and original["fallback"] not in names:
            raise ValueError("Original fallback is outside the verified object graph: " + original["fallback"])
    previous = {}
    for entry, output in zip(shader_receipt["inputs"], shader_receipt["outputs"]):
        previous[entry["original"]["name"]] = dict(original=entry["original"], output=output,
            receipt=source_path(SHADER_RECEIPT), receipt_sha256=sha(SHADER_RECEIPT.read_bytes()))
    for entry in shader_receipt["reused"]:
        previous[entry["original"]["name"]] = entry
    for folder, official in [(SHADERS, False), (SOURCES, True), (RUNTIME, False), (EDITOR, False)]:
        folder.mkdir(parents=True, exist_ok=True); folder_meta(folder, official=official)
    for folder in (RUNTIME, EDITOR):
        for path in folder.iterdir():
            if path.suffix in (".cs", ".asmdef"):
                import_meta(path, "MonoImporter" if path.suffix == ".cs" else "AssemblyDefinitionImporter")
    outputs, bindings, sources, reused, engines = [], [], [], [], []
    windows_environment = UnityPy.load(str(EDITOR_RESOURCES))
    for original in originals:
        native_name = names[original["name"]]
        original["input_index"] = indexes[original["input"]]
        obj = original_inputs.reader(original)
        if sha(obj.get_raw_data()) != original["payload_sha256"]:
            raise ValueError("Original shader payload changed")
        binding = dict(cab=original["cab"], path_id=original["path_id"], original_name=original["name"],
            original_fallback=original["fallback"], native_name=native_name,
            fallback_native_name=names.get(original["fallback"], ""), engine=False, path="", programs=[])
        if original["input"] == "unity default resources":
            candidates = [o for o in windows_environment.objects if o.type.name == "Shader"
                          and o.read().m_ParsedForm.m_Name == original["name"]]
            if len(candidates) != 1:
                raise ValueError("Windows engine shader is not unique")
            counterpart = candidates[0]
            state, programs = prove(obj, counterpart)
            binding.update(engine=True, programs=programs)
            cases_original = executable_cases(obj.read()); cases_windows = executable_cases(counterpart.read())
            for program in programs:
                key = program["subshader"], program["pass_index"], tuple(program["keywords"])
                for role, cases in [("apk", cases_original), ("windows", cases_windows)]:
                    path = SOURCES / ("InternalError-" + role + "-p" + str(key[1]) + "-" + str(cases[key]["index"]) + ".glsl.txt")
                    write(path, cases[key]["source"].encode()); meta(path)
                    program[role + "_source"] = dict(path=source_path(path), sha256=sha(path.read_bytes()))
            engines.append(dict(original=original, counterpart=dict(path=EDITOR_RESOURCES.as_posix(),
                file_sha256=sha(EDITOR_RESOURCES.read_bytes()), object_sha256=sha(counterpart.get_raw_data()),
                path_id=str(counterpart.path_id), unity_version=counterpart.assets_file.unity_version),
                state=state, programs=programs))
            bindings.append(binding)
            continue
        target = SHADERS / (native_name.rsplit("/", 1)[1] + ".shader")
        if original["name"] in previous:
            prior = previous[original["name"]]
            if prior["original"]["payload_sha256"] != original["payload_sha256"]:
                raise ValueError("Previous shader conversion is from another original object")
            path = ROOT / prior["output"]["path"]
            text = checked(path, prior["output"]["sha256"]).decode()
            text, count = re.subn(r'^Shader "[^"]+"', 'Shader "' + native_name + '"', text)
            if count != 1 or text.count("\nFallback Off\n") != 1:
                raise ValueError("Previous shader conversion has unexpected binding fields")
            reused.append(dict(original=original, output=prior["output"],
                receipt=prior["receipt"], receipt_sha256=prior["receipt_sha256"]))
        else:
            shader = platform_shader(obj.read(), 9, {4})
            if len(shader.m_ParsedForm.m_SubShaders) != 1:
                raise ValueError("Restore additional original fallback subshader")
            passes, records, header = [], [], None
            for pi, original_pass in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
                if original_pass.m_Type != 0:
                    raise ValueError("Restore original fallback pass kind")
                sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original_pass]
                form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
                one = copy(shader); one.m_ParsedForm = form
                text, programs = translate(one, native_name.removeprefix("Zjcs/Official/World/Map13/"),
                    program_body=program_body, program_structure=program_structure, render_state=render_state, target="4.5")
                for keyword, flags in zip(form.m_KeywordNames, form.m_KeywordFlags):
                    if flags not in (0, 1):
                        raise ValueError("Unknown original fallback keyword scope")
                    if flags == 1:
                        text = text.replace("#pragma multi_compile __ " + keyword + "\n",
                                            "#pragma multi_compile_local __ " + keyword + "\n")
                begin = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
                head = text[:begin]
                lod = sub.m_LOD
                if lod:
                    head = head.replace("SubShader {\n", "SubShader {\nLOD " + str(lod) + "\n", 1)
                if header is None: header = head
                elif header != head: raise ValueError("Original fallback pass headers differ")
                passes.append(text[begin:end])
                for program in programs:
                    path = SOURCES / (target.stem + "-p" + str(pi) + "-" + str(program["index"]) + ".glsl.txt")
                    write(path, program["source"].encode()); meta(path)
                    records.append(dict(pass_index=pi, index=program["index"], keywords=program["keywords"],
                        path=source_path(path), sha256=sha(path.read_bytes())))
            text = header + "".join(passes) + "}\nFallback Off\n}\n"
            binding["programs"] = records
            sources.append(dict(original=original, original_lod=shader.m_ParsedForm.m_SubShaders[0].m_LOD, programs=records))
        fallback = "Fallback Off" if not original["fallback"] else 'Fallback "' + names[original["fallback"]] + '"'
        text = text.replace("\nFallback Off\n", "\n" + fallback + "\n")
        write(target, text.encode()); import_meta(target, "ShaderImporter")
        binding["path"] = target.relative_to(ROOT / "unity").as_posix()
        outputs.append(dict(path=source_path(target), sha256=sha(target.read_bytes()), original_shader=original["name"],
            meta_sha256=sha(Path(str(target) + ".meta").read_bytes())))
        bindings.append(binding)
    write(OUTPUT, encode(dict(manifest=graph["manifest"], mapClassId=13, bindings=bindings))); meta(OUTPUT)
    outputs.append(dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes())))
    if CATALOG.exists():
        outputs.append(dict(path=source_path(CATALOG), sha256=sha(CATALOG.read_bytes()),
            meta_sha256=sha(Path(str(CATALOG) + ".meta").read_bytes())))
    tools = [Path(__file__), ROOT / "scripts/official_platform_shader_programs.py",
        ROOT / "scripts/official_engine_shader_equivalence.py", ROOT / "scripts/prepare_unity_map13_entity_shaders.py",
        *sorted(RUNTIME.glob("*.cs")), *sorted(EDITOR.glob("*.cs"))]
    write(RECEIPT, encode(dict(format=1, manifest=graph["manifest"], mapClassId=13,
        object_graph=dict(path=source_path(GRAPH), sha256=sha(GRAPH.read_bytes())),
        graph_receipt=dict(path=source_path(GRAPH_RECEIPT), sha256=sha(GRAPH_RECEIPT.read_bytes())),
        previous_shader_receipt=dict(path=source_path(SHADER_RECEIPT), sha256=sha(SHADER_RECEIPT.read_bytes())),
        counts=dict(bindings=len(bindings), native_shader_assets=len(outputs)-1-int(CATALOG.exists()),
            byte_verified_program_reuse=len(reused), converted_fallback_shaders=len(sources),
            converted_pass_programs=sum(len(s["programs"]) for s in sources), engine_shaders=len(engines),
            equivalent_engine_pass_programs=sum(len(s["programs"]) for s in engines)),
        converted=sources, reused=reused, engine=engines, outputs=outputs,
        binding_catalog=source_path(CATALOG), tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in tools],
        operation="Bind every shader in the native entity object graph, including the original m_Dependencies and fallback names. Preserve 18 previously verified programs byte-for-byte while changing only shader identity and fallback binding. Convert both remaining original fallback shaders with native LOD, properties, passes and GLES3 executables. Before using the Windows engine error shader, compare every original non-program state field and all 12 original GLES executable profiles; normalize only a framebuffer-fetch declaration whose output is unconditionally fully overwritten and never read. Windows adds only its API tables, editor build hashes and empty package-requirement metadata. Serialize all 21 native Shader references in a local runtime catalog to retain dependencies in the Player.",
        limitations=["Compilation and GPU checks are performed separately. This catalog binds shaders only; native entity materials, texture/mesh/animation assets, original runtime components, gameplay input, saves and the Windows map release remain required."])))
    print("Bound native entity shaders:", len(bindings), "; original program reuse", len(reused),
          "; new fallback programs", sum(len(s["programs"]) for s in sources), "; equivalent engine programs", sum(len(s["programs"]) for s in engines), flush=True)


if __name__ == "__main__":
    build()
