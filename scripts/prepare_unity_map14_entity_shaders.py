"""Bind Map14 entity shader programs only to byte-identical original conversions.

No shaders are selected by name alone. The material's original shader payload,
the conversion receipt, all GLES sources and native output must match.
"""
import json
from pathlib import Path

from prepare_unity_map14_entity_materials import OUTPUT as MATERIALS, RECEIPT as MATERIAL_RECEIPT
from prepare_unity_map14_sources import encode, sha, source_path, write
from official_apk import ROOT


RECEIPT = ROOT / "assets/reference/official-map/map14-entity-shaders-source-manifest.json"


def verify(record):
    if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]:
        raise ValueError("Previously recovered original shader bytes changed: " + record["path"])


def build():
    material_receipt = json.loads(MATERIAL_RECEIPT.read_bytes())
    if material_receipt["mapClassId"] != 14 or material_receipt["manifest"] != "871_156598":
        raise ValueError("Wrong original Map14 material source")
    for record in material_receipt["outputs"]:
        verify(record)
    materials = json.loads(MATERIALS.read_bytes())
    shared_path = ROOT / "assets/reference/official-map/map13-entity-shaders-source-manifest.json"
    shared = json.loads(shared_path.read_bytes())
    if shared["manifest"] != materials["manifest"]:
        raise ValueError("Entity shader source versions differ")
    candidates = {}
    for input_, output in zip(shared["inputs"], shared["outputs"], strict=True):
        original = input_["original"]
        candidates[original["name"], original["payload_sha256"]] = dict(
            input=input_, output=output, receipt=source_path(shared_path), receipt_sha256=sha(shared_path.read_bytes()))
    for reused in shared["reused"]:
        original = reused["original"]
        proof_path = ROOT / reused["receipt"]
        if sha(proof_path.read_bytes()) != reused["receipt_sha256"]:
            raise ValueError("Original reused shader receipt changed")
        proof = json.loads(proof_path.read_bytes())
        matches = [input_ for input_, output in zip(proof["inputs"], proof["outputs"], strict=True)
                   if output == reused["output"] and input_.get("object_sha256") == original["payload_sha256"]]
        if len(matches) != 1:
            raise ValueError("Original reused shader identity is not unique")
        candidates[original["name"], original["payload_sha256"]] = dict(
            input=matches[0], output=reused["output"], receipt=reused["receipt"], receipt_sha256=reused["receipt_sha256"])
    inputs, outputs, bindings, engine = [], [], [], []
    for original in materials["shaders"]:
        if original["input"] == "unity default resources":
            engine.append(original)
            continue
        candidate = candidates.get((original["name"], original["payload_sha256"]))
        if candidate is None:
            raise ValueError("Convert this exact original Map14 shader before binding: " + original["name"])
        verify(candidate["output"])
        programs = []
        for program in candidate["input"]["programs"]:
            verify(program)
            programs.append(dict(program, pass_index=program.get("pass_index", 0)))
        inputs.append(dict(original=original, programs=programs,
                           stripped_passes=candidate["input"].get("stripped_passes", [])))
        outputs.append(candidate["output"])
        bindings.append(dict(original=original, receipt=candidate["receipt"], receipt_sha256=candidate["receipt_sha256"],
                             output=candidate["output"]))
    if len(outputs) != 34 or {r["name"] for r in engine} != {"GUI/Text Shader", "Hidden/FrameDebuggerRenderTargetDisplay"}:
        raise ValueError("Original Map14 shader inventory differs")
    validation = ROOT / "unity/Assets/Game/Editor/Map14/EntityShaders/OfficialMap14EntityShaderTools.cs"
    result = dict(format=1, manifest=materials["manifest"], mapClassId=14,
        material_source=dict(path=source_path(MATERIALS), sha256=sha(MATERIALS.read_bytes())),
        material_receipt=dict(path=source_path(MATERIAL_RECEIPT), sha256=sha(MATERIAL_RECEIPT.read_bytes())),
        inputs=inputs, outputs=outputs, reused=bindings, engine_shaders=engine,
        counts=dict(native_shader_objects=len(outputs), engine_shader_objects=len(engine),
                    original_pass_variants=sum(len(i["programs"]) for i in inputs)),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        validation=dict(path=source_path(validation), sha256=sha(validation.read_bytes())),
        gpu_validation=dict(path="unity/Assets/Game/Editor/Map14/EntityShaders/OfficialMap14EntityShaderGpuAcceptance.cs",
            sha256=sha((validation.parent / "OfficialMap14EntityShaderGpuAcceptance.cs").read_bytes())),
        operation="Bind every Map14 non-engine shader to an existing conversion of its identical original payload. Verify conversion receipt, native shader and all original GLES sources before reuse; preserve original pass indices and stripped-pass records. No name-only fallback or source-version mixing.",
        limitations=["Windows compilation and rendered verification remain required for this map.",
                     "The two original APK engine shaders require explicit native Windows binding.",
                     "Native materials, texture bindings, entity runtime and gameplay are not installed by this step."])
    write(RECEIPT, encode(result))
    print("Original Map14 shader bindings:", result["counts"], flush=True)


if __name__ == "__main__":
    build()
