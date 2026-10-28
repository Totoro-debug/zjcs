"""Bind original profession shader objects to verified existing Windows translations."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from prepare_battle_hud_materials import shader_text
from prepare_unity_profession_native_inputs import CACHE
from prepare_unity_profession_native_bindings import guid, build as native_plan

RECEIPT = ROOT / "assets/reference/profession-ui/shader-bindings-source-manifest.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(relative):
    path = ROOT / relative
    return path, json.loads(path.read_bytes())


def checked_output(output):
    path = ROOT / output["path"]
    if sha(path) != output["sha256"]:
        raise ValueError("Existing official shader translation changed: " + str(path))
    name = re.search(r'^Shader "([^"]+)"', path.read_text(encoding="utf-8"), re.M)
    if not name or "DummyShaderTextExporter" in path.read_text(encoding="utf-8"):
        raise ValueError("Shader has no translated executable programs: " + str(path))
    return dict(path=path.relative_to(ROOT / "unity").as_posix(), sha256=sha(path),
                guid=guid(path), name=name[1])


def checked_programs(programs):
    result = []
    for program in programs:
        path = ROOT / program["path"]
        if sha(path) != program["sha256"]:
            raise ValueError("Original GLES proof changed: " + str(path))
        result.append(dict(pass_index=program.get("pass_index", 0), keywords=program["keywords"]))
    return result


def build():
    plan = native_plan(bind_shaders=False)
    if plan["manifest"] != "871_156598":
        raise ValueError("Unexpected original profession version")
    entity_path, entity = load("assets/reference/official-map/map13-entity-shaders-source-manifest.json")
    text_path, text = load("assets/reference/official-map/map13-text-shader-source-manifest.json")
    gray_path, gray_source = load("unity/Assets/Official/World/Map/14/UI/HandIn/handin-ui.json")
    apk = Apk.current()
    originals = {}
    for obj in apk.objects("defaultpackage_unityshaders.bundle"):
        if obj.type.name != "Shader":
            continue
        name = obj.read().m_ParsedForm.m_Name
        if name in originals:
            raise ValueError("Original shader name is not unique: " + name)
        originals[name] = obj
    translations = {}
    for record in entity["inputs"]:
        original = record["original"]
        outputs = [row for row in entity["outputs"] if row["original_shader"] == original["name"]]
        if len(outputs) != 1:
            raise ValueError("Ambiguous existing shader translation")
        translations[original["name"]] = (original, outputs[0], record["programs"], entity_path)
    for record in entity["reused"]:
        original = record["original"]
        translations[original["name"]] = (original, record["output"], record["input"]["programs"], ROOT / record["receipt"])
    original = text["inputs"][0]
    obj = originals["Hidden/JSZX/TextMeshPro"]
    translations["Hidden/JSZX/TextMeshPro"] = (
        dict(cab=obj.assets_file.name, path_id=str(obj.path_id), payload_sha256=original["object_sha256"]),
        text["outputs"][0], original["programs"], text_path)
    gray = gray_source["grayMaterial"]
    gray_obj = next(obj for obj in originals.values() if str(obj.path_id) == gray["shaderSource"]["path_id"])
    name, native, programs = shader_text(gray_obj.read(), gray["keywords"])
    native = native.replace('Shader "Zjcs/Combat/HudGray"', 'Shader "Zjcs/Official/UI/Gray"', 1)
    output = dict(path="unity/" + gray["shaderPath"], sha256=gray["shaderSha256"])
    if name != "Gray" or sha(ROOT / output["path"]) != hashlib.sha256(native.encode()).hexdigest():
        raise ValueError("Existing UI gray shader differs from original program translation")
    gray_name = gray_obj.read().m_ParsedForm.m_Name
    translations[gray_name] = (dict(cab=gray_obj.assets_file.name, path_id=str(gray_obj.path_id),
        payload_sha256=hashlib.sha256(gray_obj.get_raw_data()).hexdigest()), output, [], gray_path)
    bindings = []
    for relative in sorted(plan["shaders"]):
        source = ROOT / plan["assets"][relative]["path"]
        if sha(source) != plan["assets"][relative]["sha256"]:
            raise ValueError("Native shader export changed")
        original_name = re.search(r'^Shader "([^"]+)"', source.read_text(encoding="utf-8"), re.M)[1]
        original, output, glsl, proof_path = translations[original_name]
        obj = originals[original_name]
        if obj.assets_file.name.lower() != original["cab"].lower() or str(obj.path_id) != original["path_id"] or hashlib.sha256(obj.get_raw_data()).hexdigest() != original["payload_sha256"]:
            raise ValueError("Existing shader proof does not match selected original object: " + original_name)
        native = checked_output(output)
        variants = checked_programs(glsl) if glsl else [dict(pass_index=0, keywords=x["keywords"]) for x in programs]
        if not variants:
            raise ValueError("Original executable variants were not recorded")
        bindings.append(dict(original_name=original_name, source=apk.provenance(obj),
            cab=obj.assets_file.name, path_id=str(obj.path_id), payload_sha256=original["payload_sha256"],
            native_relative_path=relative, export_guid=guid(source), export_sha256=sha(source),
            native=native, programs=variants,
            translation_proof=dict(path=proof_path.relative_to(ROOT).as_posix(), sha256=sha(proof_path))))
    if len(bindings) != 7:
        raise ValueError("Original profession shader closure differs")
    result = dict(format=1, manifest=apk.version, bindings=bindings,
        inputs=[dict(path=entity_path.relative_to(ROOT).as_posix(), sha256=sha(entity_path)),
                dict(path=text_path.relative_to(ROOT).as_posix(), sha256=sha(text_path))],
        validation=dict(path="unity/Assets/Game/Editor/ProfessionUI/ProfessionShaderValidation.cs",
            sha256=sha(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionShaderValidation.cs"),
            scope="Native GUID/source hash/load/support checks and compilation of both stages for every recorded original variant to Windows D3D. Actual profession UI rendering is separate."),
        generator=dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(), sha256=sha(Path(__file__))),
        operation="Follow the recovered profession dependency closure; match each selected original shader CAB/path ID and raw SHA-256 to an existing official GLES-to-HLSL translation. Reuse existing native shader GUIDs and preserve every original material reference, property and keyword. No substitute shader or generated UI.",
        limitations=["This source check does not prove actual profession UI rendering. Native prefab installation, appearance animation and interactive validation remain pending."])
    RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    native_plan()
    print("Verified profession shader bindings:", len(bindings), "original objects;", sum(len(x["programs"]) for x in bindings), "program variants")
    return result


if __name__ == "__main__":
    build()
