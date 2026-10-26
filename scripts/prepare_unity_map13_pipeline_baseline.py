"""Prove installed URP post-processing source against the APK's GLES programs.

The generated cases are consumed by the Unity editor. Matching compiled GLES
allows reuse of the original engine HLSL and its Windows platform branches.
This generator alone never marks a shader source as verified.
"""
from copy import copy
from pathlib import Path
import argparse
import json
import re
import attrs

from official_apk import Apk, ROOT
import UnityPy
from official_shader_programs import gles_programs
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


OBJECTS = [51, 61, 59, 54, 53, 68, 62, 81, 60, 52, 80, 79, 70, 69, 58, 63, 57,
           65, 77, 71, 66, 74, 50, 72, 56, 76, 64, 78, 73]
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source"


def stages(text):
    vertex, fragment = text.split("#ifdef FRAGMENT\n", 1)
    vertex = vertex.removeprefix("#ifdef VERTEX\n").rsplit("#endif", 1)[0].strip()
    fragment = fragment.rsplit("#endif", 1)[0].strip()
    return vertex, fragment


def build():
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    file = next(f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers.assets")
    settings_file = next(f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers")
    settings = settings_file.objects[1]
    if settings.type.name != "PlayerSettings": raise ValueError("Original graphics settings identity changed")
    color_space = settings.read_typetree()["m_ActiveColorSpace"]
    if color_space != 1: raise ValueError("Recheck original linear rendering convention")
    package = ROOT / "unity/Packages/com.unity.render-pipelines.universal"
    shaders = {}
    for path in package.rglob("*.shader"):
        match = re.search(r'\bShader\s+"([^"]+)"', path.read_text(encoding="utf-8-sig"))
        if match:
            if match[1] in shaders: raise ValueError("Baseline shader identity is ambiguous: " + match[1])
            shaders[match[1]] = path
    records = []
    for pid in OBJECTS:
        obj = file.objects[pid]; original = obj.read(); name = original.m_ParsedForm.m_Name
        if name not in shaders: raise ValueError("Find original engine shader source: " + name)
        baseline = shaders[name]
        cases, stripped = [], []
        for si, source_sub in enumerate(original.m_ParsedForm.m_SubShaders):
            for pi, source_pass in enumerate(source_sub.m_Passes):
                executable = any(stage.m_SubPrograms or any(stage.m_PlayerSubPrograms or []) for stage in [source_pass.progVertex, source_pass.progFragment])
                if not executable:
                    stripped.append(dict(subshader_index=si, pass_index=pi)); continue
                sub = copy(source_sub); sub.m_Passes = [source_pass]
                form = copy(original.m_ParsedForm); form.m_SubShaders = [sub]
                one = copy(original); one.m_ParsedForm = form
                try: programs = gles_programs(one, with_keywords=True)
                except ValueError as error:
                    if str(error) != "Original shader has no GLES program": raise
                    stripped.append(dict(subshader_index=si, pass_index=pi, reason="Original pass has no executable GLES entry in the APK program table")); continue
                for program in programs:
                    path = SOURCE / "Shaders" / ("PostBaseline-" + str(pid) + "-s" + str(si) + "-p" + str(pi) + "-" + str(program["index"]) + ".glsl.txt")
                    write(path, program["source"].encode()); meta(path)
                    vertex, fragment = stages(program["source"])
                    cases.append(dict(subshader_index=si, pass_index=pi, index=program["index"], keywords=program["keywords"],
                        path=source_path(path), sha256=sha(path.read_bytes()), vertex_sha256=sha(vertex.encode()), fragment_sha256=sha(fragment.encode())))
        records.append(dict(path_id=pid, name=name, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
            baseline_path=source_path(baseline), baseline_sha256=sha(baseline.read_bytes()), cases=cases, stripped_passes=stripped))
        print(name, len(cases), "executable cases", len(stripped), "stripped passes")
    path = SOURCE / "native-pipeline-baseline-cases.json"
    write(path, encode(dict(manifest=apk.version, color_space=color_space, shaders=records))); meta(path)
    write(ROOT / "assets/reference/official-map/map13-pipeline-baseline-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version, cases=dict(path=source_path(path), sha256=sha(path.read_bytes())),
        color_space=dict(value=color_space, source=apk.provenance(settings), object_sha256=sha(settings.get_raw_data())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Select original APK post-process/HDR shader objects and every executable original subshader/pass/keyword case; preserve original GLES programs and normalized per-stage hashes. Identify matching installed URP 14.0.11 source by its original shader name. The Unity compiler must prove each executable stage matches before source reuse.",
        limitations=["This extraction is not an equivalence result. Platform-stripped shaders contain no executable APK GLES stages and cannot be marked equivalent by this comparison."])))


def verify():
    path = SOURCE / "native-pipeline-baseline-cases.json"
    cases = json.loads(path.read_text(encoding="utf8"))
    cache = ROOT / ".tools/map13-native-cache-871_156598"
    results_path = cache / "baseline-shader-comparison/results.json"
    results = json.loads(results_path.read_text(encoding="utf8"))
    validator = ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13PipelineBaselineTools.cs"
    builder = ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13PipelineStateTools.cs"
    if results["cases_sha256"] != sha(path.read_bytes()) or results["validator_sha256"] != sha(validator.read_bytes()):
        raise ValueError("Recompile the current source comparison cases and validator")
    compiled = {(r["path_id"], r["subshader_index"], r["pass_index"], r["index"], r["stage"]): r for r in results["stages"]}
    expected_count = sum(len(s["cases"]) * 2 for s in cases["shaders"])
    if len(compiled) != expected_count or len(results["stages"]) != expected_count:
        raise ValueError("Compiled GLES stage closure differs")
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    file = next(f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers.assets")
    bundle_path = cache / "baseline-shader-state-bundle/baseline-shaders"
    bundle = UnityPy.load(str(bundle_path))
    baseline = {o.read().m_ParsedForm.m_Name: o for o in bundle.objects if o.type.name == "Shader"}
    verified = []
    for source in cases["shaders"]:
        pid = source["path_id"]; obj = file.objects[pid]
        if sha(obj.get_raw_data()) != source["object_sha256"]: raise ValueError("Original shader source object changed")
        original = obj.read(); native = baseline[source["name"]].read()
        if sha((ROOT / source["baseline_path"]).read_bytes()) != source["baseline_sha256"]:
            raise ValueError("Engine shader source changed after compilation")
        properties = attrs.asdict(original.m_ParsedForm.m_PropInfo)
        if properties != attrs.asdict(native.m_ParsedForm.m_PropInfo): raise ValueError("Engine shader properties differ: " + source["name"])
        for case in source["cases"]:
            for stage, key in [("Vertex", "vertex_sha256"), ("Fragment", "fragment_sha256")]:
                record = compiled[pid, case["subshader_index"], case["pass_index"], case["index"], stage]
                if not record["success"] or record["expected_sha256"] != case[key] or record["actual_sha256"] != case[key] or record["keywords"] != case["keywords"]:
                    raise ValueError("Engine source executable program differs: " + source["name"])
        states = []
        for si, sub in enumerate(original.m_ParsedForm.m_SubShaders):
            other = native.m_ParsedForm.m_SubShaders[si]
            if attrs.asdict(sub.m_Tags) != attrs.asdict(other.m_Tags) or sub.m_LOD != other.m_LOD:
                raise ValueError("Engine shader subshader tags/LOD differ: " + source["name"])
            for pi, original_pass in enumerate(sub.m_Passes):
                before = attrs.asdict(original_pass.m_State)
                after = attrs.asdict(other.m_Passes[pi].m_State)
                # The compiler allocates this ID independently in each build;
                # every actual render-state property must remain equal.
                before.pop("gpuProgramID"); after.pop("gpuProgramID")
                if before != after: raise ValueError("Engine shader pass state differs: " + source["name"] + ":" + str(si) + ":" + str(pi))
                states.append(dict(subshader_index=si, pass_index=pi, state_sha256=sha(encode(before))))
        verified.append(dict(path_id=pid, name=source["name"], baseline_path=source["baseline_path"], baseline_sha256=source["baseline_sha256"],
            executable_stage_count=2 * len(source["cases"]), executable_equivalent=bool(source["cases"]),
            properties_sha256=sha(encode(properties)), pass_states=states,
            original_pass_counts=[len(s.m_Passes) for s in original.m_ParsedForm.m_SubShaders],
            baseline_pass_counts=[len(s.m_Passes) for s in native.m_ParsedForm.m_SubShaders]))
    output = ROOT / "assets/reference/official-map/map13-pipeline-baseline-binding-source-manifest.json"
    write(output, encode(dict(format=1, mapClassId=13, manifest=apk.version,
        cases=dict(path=source_path(path), sha256=sha(path.read_bytes())),
        compiler=dict(path=source_path(validator), sha256=sha(validator.read_bytes()), platform="GLES3x", build_target="Android", tier="Tier2", platform_defines=results["platform_defines"], original_color_space=cases["color_space"]),
        state_probe=dict(path=source_path(bundle_path), sha256=sha(bundle_path.read_bytes()), builder=dict(path=source_path(builder), sha256=sha(builder.read_bytes())),
                         operation="Build only these shader resources for Windows using a temporary URP configuration, keeping one variant per pass solely to inspect serialized ShaderLab state. Restore the prior project pipeline and remove temporary assets afterwards."),
        shaders=verified, executable_stages=results["stages"],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Verify exact normalized compiled GLES bytes against every original APK executable stage, original properties, subshader tags/LOD and all original pass render-state fields except the per-build compiler program ID. Proven sources can be reused directly with their native Windows API branches.",
        limitations=["Four platform-stripped shaders have no APK GLES executable stages to compare. Their original properties and available pass states are checked, but executable equivalence is not asserted. Complete scene and Windows Player rendering still require validation."])))
    print("Verified engine shader sources:", len(verified), "assets,", expected_count, "exact executable GLES stages and all original ShaderLab state")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    verify() if args.verify else build()
