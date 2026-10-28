"""Prove the APK material engine shaders against their explicit Windows identities.

All state and all GLES profiles must match, with only the documented desktop
sample-precision widening/register coalescing accepted at two exact shader IDs.
This is a platform adaptation proof, not a claim of identical Android rounding.
"""
from copy import deepcopy
from pathlib import Path
import json
import re

from official_apk import ROOT
import UnityPy
from official_engine_shader_equivalence import executable_cases, project_original, without_framebuffer_fetch
from prepare_unity_map13_entity_graph import Inputs
from prepare_unity_map13_entity_inputs import RECEIPT as INPUT_RECEIPT, CACHE
from prepare_unity_map13_entity_materials import OUTPUT as MATERIALS, RECEIPT as MATERIAL_RECEIPT
from prepare_unity_map13_sources import encode, meta, sha, source_path

SOURCES = ROOT / "unity/Assets/Official/World/Map/13/Source/EntityMaterialShaders"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-material-shaders-source-manifest.json"
PRECISION_REFERENCE = "https://docs.unity3d.com/2022.3/Documentation/Manual/SL-Use16BitPrecisionInShaders.html"


def write(path, raw):
    import os, tempfile
    path.parent.mkdir(parents=True, exist_ok=True)
    old = path.read_bytes() if path.exists() else None
    if old == raw: return
    fd, tmp = tempfile.mkstemp(prefix="map13-material-shader-", dir=ROOT / ".tools")
    with os.fdopen(fd, "wb") as stream: stream.write(raw)
    if (path.read_bytes() if path.exists() else None) != old:
        raise ValueError("Concurrent material shader source modification: " + str(path))
    os.replace(tmp, path)


def desktop_sample_precision(source, shader_id):
    """Only two byte-exact instruction pairs can widen a sampled temporary."""
    vertex, fragment = source.split("#ifdef FRAGMENT", 1)
    if shader_id == "10101":
        declaration = "mediump float u_xlat16_0;\n"
        original = ("    u_xlat16_0 = texture(_MainTex, vs_TEXCOORD0.xy).w;\n"
                    "    u_xlat0 = u_xlat16_0 * vs_COLOR0.w;")
        target = ("    u_xlat0 = texture(_MainTex, vs_TEXCOORD0.xy).w;\n"
                  "    u_xlat0 = u_xlat0 * vs_COLOR0.w;")
    elif shader_id == "10755":
        declaration = "mediump vec4 u_xlat16_0;\n"
        original = ("    u_xlat16_0 = texture(_MainTex, u_xlat0.xyz);\n"
                    "    u_xlat0 = u_xlat16_0 + (-_Levels.xxxx);")
        target = ("    u_xlat0 = texture(_MainTex, u_xlat0.xyz);\n"
                  "    u_xlat0 = u_xlat0 + (-_Levels.xxxx);")
    else: raise ValueError("Unreviewed engine shader precision adaptation")
    if fragment.count(declaration) != 1 or fragment.count(original) != 1:
        raise ValueError("Engine shader sample/consumer instruction pair differs")
    normalized = fragment.replace(declaration, "").replace(original, target)
    if re.search(r"\bu_xlat16_0\b", normalized):
        raise ValueError("Sampled temporary has an additional use")
    return vertex + "#ifdef FRAGMENT" + normalized


def build():
    inputs = json.loads(INPUT_RECEIPT.read_bytes())
    materials = json.loads(MATERIALS.read_bytes())
    proof = json.loads(MATERIAL_RECEIPT.read_bytes())
    template_path = CACHE / "native-material-template-source.json"
    template = json.loads(template_path.read_bytes())
    if inputs["manifest"] != "871_156598" or inputs["mapClassId"] != 13 or template["unity_version"] != "2022.3.57f1c2":
        raise ValueError("Wrong original engine material shader source")
    for record in proof["outputs"]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]:
            raise ValueError("Original engine material source changed")
    selected = [entry for entry in materials["shaders"] if entry["input"] == "unity default resources"]
    if {entry["name"] for entry in selected} != {"GUI/Text Shader", "Hidden/FrameDebuggerRenderTargetDisplay"}:
        raise ValueError("Original engine material shader inventory differs")
    engine = Path("D:/Tool/Unity/Editor/Data/Resources/unity default resources")
    native = UnityPy.load(str(engine))
    engine_objects = {str(obj.path_id): obj for obj in native.objects if obj.type.name == "Shader"}
    bindings = {entry["original_name"]: entry for entry in template["engine_shaders"]}
    source = Inputs(inputs["inputs"])
    indexes = {entry["output"]: index for index, entry in enumerate(inputs["inputs"])}
    SOURCES.mkdir(parents=True, exist_ok=True); meta(SOURCES, folder=True)
    records, outputs = [], []
    precision_cases = 0
    for entry in selected:
        original = dict(entry, input_index=indexes[entry["input"]], type="Shader")
        obj = source.reader(original)
        if sha(obj.get_raw_data()) != entry["payload_sha256"]: raise ValueError("Original engine shader bytes changed")
        binding = bindings[entry["name"]]
        windows = engine_objects[str(binding["file_id"])]
        if binding["guid"] != "0000000000000000e000000000000000" or windows.read_typetree()["m_ParsedForm"]["m_Name"] != entry["name"]:
            raise ValueError("Windows engine identity differs")
        original_state = obj.read_typetree()["m_ParsedForm"]
        windows_state = deepcopy(windows.read_typetree()["m_ParsedForm"])
        state_adaptations = []
        if entry["path_id"] == "10101":
            original_pass = original_state["m_SubShaders"][0]["m_Passes"][0]
            windows_pass = windows_state["m_SubShaders"][0]["m_Passes"][0]
            if original_pass["m_NameIndices"] != [] or windows_pass["m_NameIndices"] != [("_MainTex", 0)]:
                raise ValueError("Reviewed backend name-index table differs")
            windows_pass["m_NameIndices"] = []
            state_adaptations.append(dict(field="m_SubShaders[0].m_Passes[0].m_NameIndices", original=[], windows=[["_MainTex", 0]],
                meaning="Windows backend texture-name index; same _MainTex property, sampler and program use"))
        state = project_original(original_state, windows_state)
        cases = executable_cases(obj.read()); counterparts = executable_cases(windows.read())
        if set(cases) != set(counterparts): raise ValueError("Engine shader executable profile differs")
        programs = []
        for key, program in cases.items():
            counterpart = counterparts[key]
            canonical = without_framebuffer_fetch(program["source"])
            comparison = without_framebuffer_fetch(counterpart["source"])
            precision = canonical != comparison
            if precision:
                expected = entry["path_id"] == "10101" or (entry["path_id"] == "10755" and key[2] == ("_TEX2DARRAY",))
                if not expected or desktop_sample_precision(canonical, entry["path_id"]) != comparison:
                    raise ValueError("Windows engine executable has an unreviewed difference: " + str(key))
                precision_cases += 1
            output_files = {}
            for label, text in (("apk", program["source"]), ("windows", counterpart["source"])):
                path = SOURCES / (entry["path_id"] + "-" + str(program["index"]) + "-" + label + ".glsl.txt")
                write(path, text.encode()); meta(path)
                record = dict(path=source_path(path), sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + ".meta").read_bytes()))
                outputs.append(record); output_files[label] = record
            programs.append(dict(subshader=key[0], pass_index=key[1], keywords=list(key[2]),
                original_index=program["index"], windows_index=counterpart["index"], sources=output_files,
                desktop_sample_precision_widening=precision, compared_source_sha256=sha(comparison.encode())))
        state_path = SOURCES / (entry["path_id"] + "-state.json")
        write(state_path, encode(dict(original=original_state, windows=windows.read_typetree()["m_ParsedForm"],
            checked_projection=state, backend_metadata_adaptations=state_adaptations))); meta(state_path)
        outputs.append(dict(path=source_path(state_path), sha256=sha(state_path.read_bytes()), meta_sha256=sha(Path(str(state_path) + ".meta").read_bytes())))
        records.append(dict(original=entry, windows_payload_sha256=sha(windows.get_raw_data()), binding=binding,
            backend_metadata_adaptations=state_adaptations, programs=programs))
        print("Proved original material engine shader", entry["name"], len(programs), "programs", flush=True)
    if sum(len(r["programs"]) for r in records) != 13 or precision_cases != 5:
        raise ValueError("Reviewed engine shader profile inventory differs")
    tools = [Path(__file__), ROOT / "scripts/official_engine_shader_equivalence.py", ROOT / "scripts/official_platform_shader_programs.py"]
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=inputs["manifest"],
        input_receipt=dict(path=source_path(INPUT_RECEIPT), sha256=sha(INPUT_RECEIPT.read_bytes())),
        material_source=dict(path=source_path(MATERIALS), sha256=sha(MATERIALS.read_bytes())),
        material_receipt=dict(path=source_path(MATERIAL_RECEIPT), sha256=sha(MATERIAL_RECEIPT.read_bytes())),
        native_engine=dict(path=str(engine), sha256=sha(engine.read_bytes()), unity_version=template["unity_version"]),
        native_bindings=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        inputs=records, outputs=outputs, desktop_sample_precision_cases=precision_cases,
        precision_reference=PRECISION_REFERENCE,
        tools=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in tools],
        operation="Verify both APK engine shader objects, all original properties and render state, all 13 GLES profiles and explicit Windows builtin GUID/file IDs. Eight programs match after safe output-only framebuffer-fetch normalization. Five match after only exact sample-temporary precision widening/register coalescing at a verified instruction pair. Record the one _MainTex backend name-index addition. This preserves ordered math with Unity's documented desktop float precision; it does not assert identical Android mediump rounding.",
        limitations=["Unity Windows import and actual GPU output remain required; source comparison does not execute XR or MSAA variants."])))


if __name__ == "__main__": build()
