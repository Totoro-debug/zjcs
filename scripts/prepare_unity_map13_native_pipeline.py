"""Bind original pipeline fields to verified engine source and native textures."""
from pathlib import Path
import hashlib
import json
import re
import yaml

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta


DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativePipeline"
ASSETS = {22207: ("Renderer2DData", "Renderer2D.asset"), 22208: ("Renderer2DData", "Simple2DRenderer.asset"),
          22209: ("UniversalRenderPipelineAsset", "UniversalRenderPipeline.asset"), 22211: ("PostProcessData", "PostProcessData.asset")}


class Pointer(dict): pass
class Dumper(yaml.SafeDumper): pass
Dumper.add_representer(Pointer, lambda dumper, value: dumper.represent_mapping("tag:yaml.org,2002:map", value, flow_style=True))


def identity(path):
    return hashlib.md5(("zjcs-map13-native:" + source_path(path)).encode()).hexdigest()


def native_meta(path, file_id):
    target = Path(str(path) + ".meta"); guid = identity(path)
    if target.exists() and re.search(r"(?m)^guid: (\w+)$", target.read_text(encoding="utf8"))[1] != guid:
        raise ValueError("Refusing unrelated native pipeline asset identity")
    write(target, ("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: " + str(file_id) + "\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())


def asset_pointer(path, file_id):
    guid = re.search(r"(?m)^guid: (\w+)$", Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
    return Pointer(fileID=file_id, guid=guid, type=3 if path.suffix == ".shader" else 2)


def build():
    config_path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-render-pipeline.json"
    config = json.loads(config_path.read_text(encoding="utf8"))["objects"]
    proof_path = ROOT / "assets/reference/official-map/map13-pipeline-baseline-binding-source-manifest.json"
    proof = json.loads(proof_path.read_text(encoding="utf8"))
    if len(proof["executable_stages"]) != 434 or any(not s["success"] for s in proof["executable_stages"]):
        raise ValueError("Verify the complete engine shader source before native binding")
    references, targets, source_shaders = {}, {}, []
    cases_path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-pipeline-baseline-cases.json"
    cases = {s["path_id"]: s for s in json.loads(cases_path.read_text(encoding="utf8"))["shaders"]}
    for record in proof["shaders"]:
        path = ROOT / record["baseline_path"]
        if sha(path.read_bytes()) != record["baseline_sha256"]: raise ValueError("Verified engine shader changed")
        original_path = path
        # Preserve the Android algorithm/target and texture sample order instead
        # of selecting the desktop-only higher-target gather/FSR branch.
        indices = {case["subshader_index"] for case in cases[record["path_id"]]["cases"]}
        if indices == {1}:
            if record["path_id"] not in (54, 70): raise ValueError("Review original device subshader selection")
            text = path.read_text(encoding="utf8")
            blocks = list(re.finditer(r"(?ms)^    SubShader\s*\n    \{.*?^    \}", text))
            if len(blocks) != 2: raise ValueError("Verified original device subshader boundaries changed")
            selected = blocks[0]
            text = text[:selected.start()] + text[selected.end():]
            path = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders/Pipeline" / ("Original" + path.name)
            write(path, text.encode()); import_meta(path, "ShaderImporter")
            import_meta(path.parent, "DefaultImporter", folder=True)
        references[record["path_id"]] = asset_pointer(path, 4800000); targets[record["path_id"]] = path
        source_shaders.append(dict(path_id=record["path_id"], path=source_path(path), sha256=sha(path.read_bytes()), baseline_path=source_path(original_path),
            original_subshader_index=1 if indices == {1} else 0, executable_equivalent=record["executable_equivalent"]))
    light = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders/LightShape.shader"
    light_receipt = json.loads((ROOT / "assets/reference/official-map/map13-light-shader-source-manifest.json").read_text(encoding="utf8"))
    if sha(light.read_bytes()) != light_receipt["output"]["sha256"]: raise ValueError("Original custom shape-light shader differs")
    references[75] = asset_pointer(light, 4800000); targets[75] = light
    textures_path = ROOT / "assets/reference/official-map/map13-pipeline-textures-source-manifest.json"
    textures = json.loads(textures_path.read_text(encoding="utf8"))
    for record in textures["outputs"]:
        path = ROOT / record["path"]
        if sha(path.read_bytes()) != record["sha256"]: raise ValueError("Original pipeline texture differs")
        references[record["path_id"]] = asset_pointer(path, 2800000); targets[record["path_id"]] = path
    for pid, (_, name) in ASSETS.items():
        path = DESTINATION / name; native_meta(path, 11400000)
        references[pid] = asset_pointer(path, 11400000); targets[pid] = path
    templates_path = ROOT / ".tools/map13-native-cache-871_156598/native-pipeline-templates/templates.json"
    templates = {t["type"]: t for t in json.loads(templates_path.read_text(encoding="utf8"))["templates"]}
    bindings, outputs = [], []
    def resolve(value):
        if isinstance(value, dict):
            if set(value) == {"m_FileID", "m_PathID"}:
                if value["m_FileID"] != 0: raise ValueError("Resolve original external native pipeline pointer")
                if not value["m_PathID"]: return Pointer(fileID=0)
                return references[value["m_PathID"]]
            return {k: resolve(v) for k, v in value.items()}
        if isinstance(value, list): return [resolve(v) for v in value]
        return value
    def properties(value, path, fields):
        if isinstance(value, Pointer):
            target = next((source_path(targets[pid]) for pid, ref in references.items() if ref == value), "") if value["fileID"] else ""
            fields.append(dict(path=path, kind="ObjectReference", target_path=target, file_id=value["fileID"], guid=value.get("guid", "")))
        elif isinstance(value, dict):
            for key, child in value.items(): properties(child, path + "." + key, fields)
        elif isinstance(value, list):
            fields.append(dict(path=path, kind="Array", length=len(value)))
            for i, child in enumerate(value): properties(child, path + ".Array.data[" + str(i) + "]", fields)
        else: fields.append(dict(path=path, kind="Scalar", value=str(value)))
    for pid, (kind, name) in ASSETS.items():
        template = templates[kind]; template_path = ROOT / template["path"]
        if sha(template_path.read_bytes()) != template["sha256"]: raise ValueError("Native serialization template differs")
        text = template_path.read_text(encoding="utf8")
        payload = yaml.safe_load("\n".join(line for line in text.splitlines() if not line.startswith(("%", "---"))))["MonoBehaviour"]
        source = config[str(pid)]; fields = []
        for key, value in source.items():
            if key == "m_Script": continue
            payload[key] = resolve(value)
            if key not in ("m_GameObject", "m_Enabled", "m_Name"): properties(payload[key], key, fields)
        payload["m_Script"] = Pointer(fileID=template["script_file_id"], guid=template["script_guid"], type=3)
        for key in ["m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset"]:
            if key in payload: payload[key] = Pointer(payload[key])
        path = DESTINATION / name
        output = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n" + yaml.dump({"MonoBehaviour": payload}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=1000)
        write(path, output.encode())
        outputs.append(dict(path_id=pid, path=source_path(path), sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
        bindings.append(dict(path_id=pid, path=source_path(path), type=kind, name=source["m_Name"], script_guid=template["script_guid"], script_file_id=template["script_file_id"], fields=fields))
    source_path_json = ROOT / "unity/Assets/Official/World/Map/13/Source/native-pipeline-bindings.json"
    catalog = [dict(path_id=pid, path=source_path(target), file_id=references[pid]["fileID"], guid=references[pid]["guid"]) for pid, target in sorted(targets.items())]
    if len(catalog) != 81: raise ValueError("Native pipeline dependency binding closure changed")
    write(source_path_json, encode(dict(manifest=proof["manifest"], assets=bindings, catalog=catalog))); meta(source_path_json)
    write(ROOT / "assets/reference/official-map/map13-native-pipeline-source-manifest.json", encode(dict(format=1, mapClassId=13, manifest=proof["manifest"], outputs=outputs,
        configuration=dict(path=source_path(config_path), sha256=sha(config_path.read_bytes())),
        shader_proof=dict(path=source_path(proof_path), sha256=sha(proof_path.read_bytes())),
        texture_source=dict(path=source_path(textures_path), sha256=sha(textures_path.read_bytes())),
        serialization_templates=templates, engine_shaders=source_shaders,
        bindings=dict(path=source_path(source_path_json), sha256=sha(source_path_json.read_bytes())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Restore both original Renderer2DData objects, original pipeline/default renderer selection and original PostProcessData. Replace every original object pointer with an explicit verified engine shader, original custom shape-light shader, raw native texture or original pipeline object. Preserve every original runtime field; new baseline/editor-only fields retain the actual Unity template defaults. Produce exhaustive field and 81-object binding records for editor validation.",
        limitations=["Project graphics/color-space activation, native import, GPU rendering and full world scene validation remain required. Four platform-stripped library shaders use the same engine version's native Windows source; no APK GLES executable equivalence is asserted for those four."])))
    print("Bound original native render pipeline: 2 renderers, pipeline, post-process data, 81 explicit dependencies")


if __name__ == "__main__": build()
