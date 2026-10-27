"""Bind converted original Map14 materials to exact native shader/texture IDs."""
import collections
import hashlib
import json
import pathlib
import re
import struct
import yaml

from official_native_yaml_verification import NativeLoader
from prepare_unity_map14_entity_inputs import CACHE, RECEIPT as INPUT_RECEIPT
from prepare_unity_map14_entity_materials import OUTPUT as MATERIAL_SOURCE, RECEIPT as MATERIAL_RECEIPT
from prepare_unity_map14_sources import encode, meta, sha, source_path, write
from official_apk import ROOT


EXPORT = CACHE / "export/ExportedProject/Assets"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/14/NativeEntityMaterials"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-materials-native-source-manifest.json"
TEXTURE_RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-textures-source-manifest.json"
SHADER_RECEIPT = ROOT / "assets/reference/official-map/map14-entity-shaders-source-manifest.json"
SHADER_BINDING_RECEIPT = ROOT / "assets/reference/official-map/map14-entity-shaders-binding-source-manifest.json"
MATERIAL_BINDING_RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-materials-binding-source-manifest.json"
MATERIAL_CATALOG = ROOT / "unity/Assets/Game/Resources/Map14/Native/native-entity-materials-catalog.asset"
GUID = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")


def clean(value):
    if isinstance(value, dict):
        if "m_PathID" in value or "fileID" in value:
            return ("pointer",)
        return {key: clean(child) for key, child in value.items()}
    if isinstance(value, list):
        if all(isinstance(child, list) and len(child) == 2 and isinstance(child[0], str) for child in value):
            return {child[0]: clean(child[1]) for child in value}
        return [clean(child) for child in value]
    if isinstance(value, float):
        return struct.unpack("<f", struct.pack("<f", value))[0]
    if value in ("inf", ".inf", "Infinity", "infinity"):
        return float("inf")
    return value


def comparable(source):
    result = {key: value for key, value in source.items() if key != "m_SavedProperties"}
    saved = source["m_SavedProperties"]
    properties = {key: value for key, value in saved.items() if key not in ("m_TexEnvs", "serializedVersion")}
    properties["m_TexEnvs"] = {row[0]: row[1] for row in saved["m_TexEnvs"]}
    result["m_SavedProperties"] = properties
    return clean(result)


def parse_material(path):
    raw = path.read_text(encoding="utf8")
    normalized = re.sub(r"(?m)^--- !u!\d+ &-?\d+$", "---", raw)
    documents = list(yaml.load_all(normalized, Loader=NativeLoader))
    if len(documents) != 1:
        raise ValueError("Original exported native material document count differs: " + str(path))
    kind, fields = next(iter(documents[0].items()))
    if kind != "Material":
        raise ValueError("Original exported material type differs: " + str(path))
    fields["m_SavedProperties"].pop("serializedVersion", None)
    fields["m_DoubleSidedGI"] = bool(fields["m_DoubleSidedGI"])
    fields["m_EnableInstancingVariants"] = bool(fields["m_EnableInstancingVariants"])
    return raw, fields


def build():
    source = json.loads(MATERIAL_SOURCE.read_bytes())
    source_receipt = json.loads(MATERIAL_RECEIPT.read_bytes())
    input_receipt = json.loads(INPUT_RECEIPT.read_bytes())
    texture_source = json.loads(TEXTURE_RECEIPT.read_bytes())
    shader_source = json.loads(SHADER_RECEIPT.read_bytes())
    shader_binding = json.loads(SHADER_BINDING_RECEIPT.read_bytes())
    for record in source_receipt["outputs"]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]:
            raise ValueError("Original entity material source changed")
    if source_receipt["input_receipt"]["path"] != "assets/reference/official-map/map14-native-entity-input-source-manifest.json":
        raise ValueError("Map14 material source uses an unexpected entity input closure")
    for record in (input_receipt, shader_binding):
        if record.get("manifest") != "871_156598" or record.get("mapClassId") != 14:
            raise ValueError("Map14 material/native conversion versions differ")
    texture_by_id = {(record["cab"].casefold(), record["path_id"]): record for record in texture_source["textures"]}
    empty_texture_ids = {(record["cab"].casefold(), record["path_id"]) for record in texture_source["empty_dynamic_atlases"]}
    if len(texture_by_id) != 1644 or texture_source["counts"]["empty_dynamic_atlases"] != 6:
        raise ValueError("Original material texture bindings are incomplete")
    shader_by_id = {}
    for record, output in zip(shader_source["inputs"], shader_source["outputs"], strict=True):
        for original, converted in zip(record["programs"], record["programs"]):
            if sha((ROOT / output["path"]).read_bytes()) != output["sha256"]:
                raise ValueError("Original shader conversion changed")
        shader_by_id[record["original"]["cab"].casefold(), record["original"]["path_id"]] = output
    for record in shader_source["reused"]:
        original, output = record["original"], record["output"]
        if sha((ROOT / record["receipt"]).read_bytes()) != record["receipt_sha256"] or sha((ROOT / output["path"]).read_bytes()) != output["sha256"]:
            raise ValueError("Original reused entity shader conversion changed")
        shader_by_id[original["cab"].casefold(), original["path_id"]] = output
    shader_bindings = {(item["path"].casefold(), item["engine"]): item for item in shader_binding["bindings"]}

    by_name = collections.defaultdict(list)
    for material in source["materials"]:
        by_name[material["fields"]["m_Name"]].append(material)
    exported = []
    matched_source_ids = set()
    candidate_usage = collections.Counter()
    for path in sorted(EXPORT.rglob("*.mat")):
        raw, fields = parse_material(path)
        candidates = []
        for candidate in by_name.get(fields["m_Name"], []):
            normalized = comparable(candidate["fields"])
            projected = clean({key: value for key, value in fields.items() if key in normalized})
            projected.get("m_SavedProperties", {}).pop("serializedVersion", None)
            projected["m_DoubleSidedGI"] = bool(projected["m_DoubleSidedGI"])
            projected["m_EnableInstancingVariants"] = bool(projected["m_EnableInstancingVariants"])
            if normalized == projected:
                candidates.append(candidate)
        if not candidates:
            reference = by_name.get(fields["m_Name"], [None])[0]
            reference_fields = comparable(reference["fields"]) if reference else {}
            projected = clean({key: value for key, value in fields.items() if key in reference_fields})
            projected.get("m_SavedProperties", {}).pop("serializedVersion", None)
            for key in ("m_DoubleSidedGI", "m_EnableInstancingVariants"):
                if key in projected:
                    projected[key] = bool(projected[key])
            differences = [(key, reference_fields.get(key), projected.get(key)) for key in set(reference_fields) | set(projected)
                           if reference_fields.get(key) != projected.get(key)]
            raise ValueError("Original exported material fields do not match APK CAB/PathID: " + str(path) + " " + repr(differences[:8]))
        shader_name = candidates[0]["shader"]["name"]
        candidates = [record for record in candidates if record["shader"]["name"] == shader_name]
        if not candidates:
            raise ValueError("Original material shader identity is ambiguous: " + fields["m_Name"])
        candidates.sort(key=lambda record: (candidate_usage[(record["cab"].casefold(), record["path_id"])], record["cab"], record["path_id"]))
        material = candidates[0]
        candidate_usage[(material["cab"].casefold(), material["path_id"])] += 1
        source_shader = material["shader"]
        shader = shader_by_id.get((source_shader["cab"].casefold(), source_shader["path_id"]))
        if shader is None:
            engine_matches = [item for item in shader_binding["bindings"]
                              if item["engine"] and item["original_shader"] == source_shader["name"]]
            if len(engine_matches) != 1:
                raise ValueError("Original material shader has no exact Windows binding: " + source_shader["name"])
            shader_binding_record = engine_matches[0]
            shader_guid, shader_file_id = shader_binding_record["guid"], shader_binding_record["file_id"]
        else:
            shader_record = shader_bindings.get((shader["path"].casefold(), False))
            if shader_record is None:
                raise ValueError("Original material shader conversion has no explicit binding: " + shader["original_shader"])
            shader_guid, shader_file_id = shader_record["guid"], shader_record["file_id"]
        raw, count = re.subn(r"(?m)^(  m_Shader: \{fileID: )-?\d+(, guid: )[0-9a-f]{32}(, type: )\d+(\})$",
            lambda match: match[1] + str(shader_file_id) + match[2] + shader_guid + match[3] + "3" + match[4], raw)
        if count != 1:
            raise ValueError("Original material shader pointer is not unique: " + str(path))
        pointer_records = {record["field"]: record for record in material["pointers"]}
        texture_count = 0
        for property_index, (property_name, _) in enumerate(material["fields"]["m_SavedProperties"]["m_TexEnvs"]):
            field = ".m_SavedProperties.m_TexEnvs[" + str(property_index) + "][1].m_Texture"
            pointer_record = pointer_records.get(field)
            if pointer_record is None:
                continue
            target = texture_by_id.get((pointer_record["cab"].casefold(), pointer_record["path_id"]))
            if target is None:
                if (pointer_record["cab"].casefold(), pointer_record["path_id"]) in empty_texture_ids:
                    pattern = re.compile(r"(?ms)^(      " + re.escape(property_name) + r":\n)(.*?)(?=^      \S|\Z)")
                    def clear_dynamic_atlas(match):
                        body, changed = re.subn(r"(?m)^(        m_Texture: )\{fileID: -?\d+, guid: [0-9a-f]{32}, type: \d+\}$",
                            lambda row: row[1] + "{fileID: 0}", match[2])
                        if changed != 1:
                            raise ValueError("Original empty dynamic texture pointer differs: " + property_name)
                        return match[1] + body
                    raw, count = pattern.subn(clear_dynamic_atlas, raw)
                    if count != 1:
                        raise ValueError("Original empty dynamic texture property is not unique: " + property_name)
                    continue
                raise ValueError("Original material texture has no native CAB/PathID asset: " + str((pointer_record["cab"], pointer_record["path_id"])))
            texture_guid = hashlib.md5(("zjcs-map14-original-material-texture:" + pointer_record["cab"].casefold() + ":" + pointer_record["path_id"]).encode()).hexdigest()
            pattern = re.compile(r"(?ms)^(      " + re.escape(property_name) + r":\n)(.*?)(?=^      \S|\Z)")
            def replace_pointer(match):
                body, changed = re.subn(r"(?m)^(        m_Texture: \{fileID: )-?\d+(, guid: )[0-9a-f]{32}(, type: )\d+(\})$",
                    lambda row: row[1] + "2800000" + row[2] + texture_guid + row[3] + "2" + row[4], match[2])
                if changed != 1:
                    raise ValueError("Original material texture property differs: " + property_name)
                return match[1] + body
            raw, count = pattern.subn(replace_pointer, raw)
            if count != 1:
                raise ValueError("Original material texture binding is not unique: " + property_name)
            texture_count += 1
        exported.append(dict(path=path, raw=raw, original=material, shader=source_shader, textures=texture_count,
            source_export_sha256=sha(path.read_bytes())))
        matched_source_ids.add((material["cab"].casefold(), material["path_id"]))

    matched = collections.Counter((record["original"]["cab"].casefold(), record["original"]["path_id"]) for record in exported)
    source_exported = [record for record in source["materials"] if record["cab"] != "unity default resources"]
    if len(exported) != 2034 or len(source_exported) != 2038:
        raise ValueError("Original APK and Unity material source inventory differs: " + repr((len(exported), len(source_exported))))
    missing = [record for record in source_exported if (record["cab"].casefold(), record["path_id"]) not in matched_source_ids]
    if len(missing) != 4 or any(record["fields"]["m_Name"] not in ("FrameDebuggerRenderTargetDisplay", "FZY4K SDF Material", "Sprites-Default", "Default-Particle", "Default-Skybox", "Font Material") for record in missing):
        raise ValueError("Original native material templates do not cover the exact default-resource exceptions: " +
                         repr([(record["cab"], record["path_id"], record["fields"]["m_Name"]) for record in missing]))
    outputs, entries = [], []
    for record in exported:
        original = record["original"]
        token = hashlib.md5(original["cab"].casefold().encode()).hexdigest()
        path = DESTINATION / token / (original["path_id"] + ".mat")
        guid = hashlib.md5(("zjcs-map14-original-material:" + original["cab"].casefold() + ":" + original["path_id"]).encode()).hexdigest()
        write(path, record["raw"].encode("utf8"))
        write(pathlib.Path(str(path) + ".meta"), ("fileFormatVersion: 2\nguid: " + guid +
            "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
        output = dict(path=source_path(path), sha256=sha(path.read_bytes()), meta_sha256=sha(pathlib.Path(str(path) + ".meta").read_bytes()))
        outputs.append(output)
        entries.append(dict(cab=original["cab"], path_id=original["path_id"], name=original["fields"]["m_Name"],
            payload_sha256=original["payload_sha256"], exported_template=dict(path=source_path(record["path"]), sha256=record["source_export_sha256"]),
            shader=dict(cab=record["shader"]["cab"], path_id=record["shader"]["path_id"], name=record["shader"]["name"]),
            native_texture_references=record["textures"], output=output))
    for folder in [DESTINATION, *sorted(path for path in DESTINATION.rglob("*") if path.is_dir())]:
        meta(folder, folder=True)
    write(RECEIPT, encode(dict(format=1, mapClassId=14, manifest="871_156598",
        material_source=dict(path=source_path(MATERIAL_SOURCE), sha256=sha(MATERIAL_SOURCE.read_bytes())),
        material_receipt=dict(path=source_path(MATERIAL_RECEIPT), sha256=sha(MATERIAL_RECEIPT.read_bytes())),
        texture_receipt=dict(path=source_path(TEXTURE_RECEIPT), sha256=sha(TEXTURE_RECEIPT.read_bytes())),
        shader_source=dict(path=source_path(SHADER_RECEIPT), sha256=sha(SHADER_RECEIPT.read_bytes())),
        shader_binding=dict(path=source_path(SHADER_BINDING_RECEIPT), sha256=sha(SHADER_BINDING_RECEIPT.read_bytes())),
        generator=dict(path=source_path(pathlib.Path(__file__)), sha256=sha(pathlib.Path(__file__).read_bytes())),
        counts=dict(original_materials=2040, native_materials=len(outputs), native_texture_references=sum(e["native_texture_references"] for e in entries),
            converted_shader_references=sum(e["shader"]["cab"] != "unity default resources" for e in entries), engine_shader_references=sum(e["shader"]["cab"] == "unity default resources" for e in entries),
            engine_materials=len(missing), empty_dynamic_atlases=6),
        default_resource_materials=[dict(cab=record["cab"], path_id=record["path_id"], name=record["fields"]["m_Name"], payload_sha256=record["payload_sha256"]) for record in missing],
        materials=entries, outputs=outputs,
        operation="Match all AssetRipper-exported material scalar fields to the original APK material object inventory before use. Ignore only pointers during that structural match; then replace every shader and nonempty texture PPtr from the exact original CAB/PathID record with verified Windows shader/native texture identities. Preserve original exported field serialization, values, keywords, queues and per-property sampler transforms. Do not use exporter-generated font material payloads as source.",
        limitations=["Four Unity-default-resource material objects have no standalone AssetRipper .mat template and remain engine supplied.",
            "Original zero-size dynamic font atlases await their verified font initialization chain.",
            "Unity import, runtime renderer binding, full entity scene and gameplay remain unverified."])))
    if MATERIAL_CATALOG.is_file() and pathlib.Path(str(MATERIAL_CATALOG) + ".meta").is_file():
        catalog_record = dict(path=source_path(MATERIAL_CATALOG), sha256=sha(MATERIAL_CATALOG.read_bytes()),
            meta_sha256=sha(pathlib.Path(str(MATERIAL_CATALOG) + ".meta").read_bytes()))
        material_bindings = []
        for entry in entries:
            output_path = ROOT / entry["output"]["path"]
            match = GUID.search(pathlib.Path(str(output_path) + ".meta").read_text(encoding="utf8"))
            if not match or sha(output_path.read_bytes()) != entry["output"]["sha256"]:
                raise ValueError("Imported native material identity changed: " + entry["output"]["path"])
            material_bindings.append(dict(cab=entry["cab"], path_id=entry["path_id"], name=entry["name"],
                asset_path=source_path(output_path).removeprefix("unity/"), guid=match.group(1), file_id=2100000))
        material_tools = ROOT / "unity/Assets/Game/Editor/Map14/EntityTextures/OfficialMap14EntityMaterialTools.cs"
        write(MATERIAL_BINDING_RECEIPT, encode(dict(mapClassId=14, manifest="871_156598",
            source_sha256=sha(RECEIPT.read_bytes()), importer_sha256=sha(material_tools.read_bytes()),
            catalog=catalog_record, materials=material_bindings,
            operation="Bind imported native Unity Materials by exact original CAB/PathID. GUID and serialized asset identity come from the installed .meta and native material importer; catalog hash is from the verified main Unity import.")))
    print("Bound original Map14 native materials:", len(outputs), "exact APK CAB/PathID entries and", sum(e["native_texture_references"] for e in entries), "native texture references", flush=True)


if __name__ == "__main__":
    build()
