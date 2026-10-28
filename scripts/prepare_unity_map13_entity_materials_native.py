"""Restore Map13 materials by exact CAB/PathID, using actual editor serialization."""
from copy import deepcopy
from pathlib import Path
import hashlib
import json
import math
import re
import struct
import yaml

from official_apk import ROOT
from prepare_unity_map13_entity_inputs import CACHE
from prepare_unity_map13_entity_inputs import RECEIPT as INPUT_RECEIPT
from prepare_unity_map13_entity_graph import Inputs
from official_engine_shader_equivalence import executable_cases
from prepare_unity_map13_entity_materials import OUTPUT as MATERIALS, RECEIPT as MATERIAL_RECEIPT
from prepare_unity_map13_entity_material_shaders import RECEIPT as ENGINE_RECEIPT, write
from prepare_unity_map13_native_pipeline import Pointer, Dumper, asset_pointer
from prepare_unity_map13_sources import encode, meta, sha, source_path

DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativeEntityMaterials"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-entity-materials-native-source-manifest.json"
TEXTURE_RECEIPT = ROOT / "assets/reference/official-map/map13-native-entity-textures-source-manifest.json"
SHADER_RECEIPT = ROOT / "assets/reference/official-map/map13-entity-shaders-source-manifest.json"
BINDINGS = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-material-bindings.json"


class MaterialDumper(Dumper): pass


def represent_float(dumper, value):
    # Unity's actual scalar grammar uses Infinity/-Infinity. YAML's .inf is
    # imported as NaN by this editor and therefore cannot preserve source bits.
    if math.isinf(value):
        return dumper.represent_scalar("tag:yaml.org,2002:str", "Infinity" if value > 0 else "-Infinity")
    return yaml.SafeDumper.represent_float(dumper, value)


MaterialDumper.add_representer(float, represent_float)


def key(record):
    return record["cab"].casefold(), str(record["path_id"])


def checked(record):
    path = Path(record["path"])
    if not path.is_absolute(): path = ROOT / path
    if sha(path.read_bytes()) != record["sha256"]:
        raise ValueError("Original material dependency changed: " + str(path))
    if "meta_sha256" in record and sha(Path(str(path) + ".meta").read_bytes()) != record["meta_sha256"]:
        raise ValueError("Original material dependency identity changed: " + str(path))
    return path


def native_number(value):
    if value == "inf": return math.inf
    if value == "-inf": return -math.inf
    if value == "nan": raise ValueError("Review original material NaN")
    return value


def float_bits(value):
    return struct.unpack("<i", struct.pack("<f", native_number(value)))[0]


def build():
    source = json.loads(MATERIALS.read_bytes())
    material_receipt = json.loads(MATERIAL_RECEIPT.read_bytes())
    textures = json.loads(TEXTURE_RECEIPT.read_bytes())
    shaders = json.loads(SHADER_RECEIPT.read_bytes())
    engine = json.loads(ENGINE_RECEIPT.read_bytes())
    template_source = CACHE / "native-material-template-source.json"
    template = json.loads(template_source.read_bytes())
    if source["manifest"] != "871_156598" or source["mapClassId"] != 13 or len(source["materials"]) != 1843:
        raise ValueError("Original Map13 material inventory differs")
    for record in material_receipt["outputs"]: checked(record)
    for record in textures["outputs"]: checked(record)
    checked(engine["material_source"])
    checked(engine["native_engine"])
    for record in engine["outputs"]: checked(record)
    texture_by_id = {key(record): record for record in textures["textures"]}
    empty = {key(record): record for record in textures["empty_dynamic_atlases"]}
    shader_by_id = {}
    for original, output in zip(shaders["inputs"], shaders["outputs"], strict=True):
        checked(output)
        shader_by_id[key(original["original"])] = dict(output, payload_sha256=original["original"]["payload_sha256"])
    for reused in shaders["reused"]:
        checked(dict(path=reused["receipt"], sha256=reused["receipt_sha256"]))
        checked(reused["output"])
        shader_by_id[key(reused["original"])] = dict(reused["output"], payload_sha256=reused["original"]["payload_sha256"])
    for record in engine["inputs"]:
        binding = record["binding"]
        shader_by_id[key(record["original"])] = dict(path="", guid=binding["guid"], file_id=binding["file_id"],
            native_name=binding["native_name"], payload_sha256=record["original"]["payload_sha256"])
    if set(shader_by_id) != {key(record) for record in source["shaders"]}:
        raise ValueError("Exact original shader closure is incomplete")
    template_path = ROOT / template["template_path"]
    if sha(template_path.read_bytes()) != template["template_sha256"]: raise ValueError("Native material template changed")
    nonfinite_template = CACHE / "native-nonfinite-material-template.asset"
    if "g: Infinity, b: -Infinity" not in nonfinite_template.read_text(encoding="utf8"):
        raise ValueError("Actual Unity nonfinite scalar serialization reference differs")
    raw = "\n".join(line for line in template_path.read_text(encoding="utf8").splitlines() if not line.startswith(("%", "---")))
    base = yaml.safe_load(raw)["Material"]
    source_inputs = json.loads(INPUT_RECEIPT.read_bytes())
    input_reader = Inputs(source_inputs["inputs"])
    input_indexes = {entry["output"]: index for index, entry in enumerate(source_inputs["inputs"])}
    outputs, entries = [], []
    bound_textures, deferred = 0, 0
    for original in source["materials"]:
        fields = deepcopy(base)
        source_fields = original["fields"]
        if source_fields["m_BuildTextureStacks"]: raise ValueError("Review original material texture stack")
        unknown = set(source_fields) - set(fields)
        if unknown: raise ValueError("Original material fields absent from actual Unity template: " + str(unknown))
        shader = shader_by_id[key(original["shader"])]
        if shader["payload_sha256"] != original["shader"]["payload_sha256"]: raise ValueError("Original shader payload differs")
        shader_pointer = asset_pointer(ROOT / shader["path"], 4800000) if shader["path"] else Pointer(
            fileID=shader["file_id"], guid=shader["guid"], type=0)
        fields.update(deepcopy(source_fields))
        fields["m_Shader"] = shader_pointer
        for name in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_Parent"):
            fields[name] = Pointer(fileID=0)
        for name in ("m_EnableInstancingVariants", "m_DoubleSidedGI"): fields[name] = int(fields[name])
        fields["stringTagMap"] = dict(source_fields["stringTagMap"])
        if len(fields["stringTagMap"]) != len(source_fields["stringTagMap"]): raise ValueError("Duplicate material override tag")
        pointers = {record["field"]: record for record in original["pointers"]}
        saved = dict(serializedVersion=3)
        expected = dict(textures=[], ints=[], floats=[], colors=[])
        for category, pairs in source_fields["m_SavedProperties"].items():
            restored = []
            for index, (name, value) in enumerate(pairs):
                if category == "m_TexEnvs":
                    value = deepcopy(value)
                    pointer = value["m_Texture"]
                    target, state = None, "null"
                    if pointer["m_PathID"]:
                        record = pointers[".m_SavedProperties.m_TexEnvs[" + str(index) + "][1].m_Texture"]
                        target = texture_by_id.get(key(record))
                        if target is not None:
                            value["m_Texture"] = asset_pointer(ROOT / target["path"], 2800000)
                            state = "native"; bound_textures += 1
                        elif key(record) in empty:
                            # Original zero-sized font descriptors have no pixels.
                            # Preserve their identities for the original font initialization.
                            value["m_Texture"] = Pointer(fileID=0); state = "empty_dynamic_atlas"; deferred += 1
                        else: raise ValueError("Material texture has no exact native identity: " + str(record))
                    else: value["m_Texture"] = Pointer(fileID=0)
                    expected["textures"].append(dict(name=name, path=target["path"] if target else "", state=state,
                        original=record if pointer["m_PathID"] else None, scale=value["m_Scale"], offset=value["m_Offset"]))
                elif category == "m_Colors":
                    value = {channel: native_number(number) for channel, number in value.items()}
                    expected["colors"].append(dict(name=name, **value, **{channel + "_bits": float_bits(number) for channel, number in value.items()}))
                elif category in ("m_Ints", "m_Floats"):
                    value = native_number(value)
                    expected["ints" if category == "m_Ints" else "floats"].append(dict(name=name, value=value,
                        value_bits=float_bits(value), int_value=int(value) if category == "m_Ints" else 0))
                else: raise ValueError("Review original material saved property category: " + category)
                restored.append({name: value})
            saved[category] = restored
        fields["m_SavedProperties"] = saved
        keyword_normalization = None
        if key(original) == ("cab-a4948dbbfef837f4a78843af204c8064", "1419505037369228759"):
            if fields["m_ValidKeywords"] != ["ETC1_EXTERNAL_ALPHA"] or fields["m_InvalidKeywords"] != []:
                raise ValueError("Reviewed original inactive keyword source differs")
            source_shader = original["shader"]
            shader_record = next(s for s in source["shaders"] if key(s) == key(source_shader))
            reader = input_reader.reader(dict(cab=source_shader["cab"], path_id=source_shader["path_id"],
                input_index=input_indexes[shader_record["input"]]))
            if sha(reader.get_raw_data()) != source_shader["payload_sha256"]: raise ValueError("Original keyword proof shader changed")
            cases = executable_cases(reader.read())
            if len(cases) != 18 or any("ETC1_EXTERNAL_ALPHA" in case["keywords"] or "ETC1_EXTERNAL_ALPHA" in case["source"] for case in cases.values()):
                raise ValueError("Original GLES program uses the reviewed inactive keyword")
            keyword_normalization = dict(native_valid=[], native_invalid=["ETC1_EXTERNAL_ALPHA"],
                shader=source_shader, gles_programs=[dict(subshader=k[0], pass_index=k[1], keywords=list(k[2]),
                    source_sha256=sha(case["source"].encode())) for k, case in cases.items()],
                reason="Unity reclassifies the one serialized keyword absent from all 18 original GLES programs; original YAML classification and the complete keyword union remain intact.")
        token = hashlib.md5(original["cab"].casefold().encode()).hexdigest()
        path = DESTINATION / token / (original["path_id"] + ".mat")
        payload = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\n" + yaml.dump(
            {"Material": fields}, Dumper=MaterialDumper, allow_unicode=True, sort_keys=False, width=1000)
        write(path, payload.encode())
        guid = hashlib.md5(("zjcs-map13-original-material:" + original["cab"].casefold() + ":" + original["path_id"]).encode()).hexdigest()
        write(Path(str(path) + ".meta"), ("fileFormatVersion: 2\nguid: " + guid +
            "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
        output = dict(path=source_path(path), sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + ".meta").read_bytes()))
        outputs.append(output)
        entries.append(dict(cab=original["cab"], path_id=original["path_id"], name=fields["m_Name"],
            payload_sha256=original["payload_sha256"], output=output, shader=shader,
            valid_keywords=fields["m_ValidKeywords"], invalid_keywords=fields["m_InvalidKeywords"],
            keyword_normalization=keyword_normalization,
            lightmap_flags=fields["m_LightmapFlags"], instancing=bool(fields["m_EnableInstancingVariants"]),
            double_sided=bool(fields["m_DoubleSidedGI"]), render_queue=fields["m_CustomRenderQueue"],
            tags=[dict(name=k, value=v) for k, v in source_fields["stringTagMap"]],
            disabled_passes=fields["disabledShaderPasses"], **expected))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]:
        existing = Path(str(folder) + ".meta")
        if not existing.exists(): meta(folder, folder=True)
        elif b"folderAsset: yes" not in existing.read_bytes(): raise ValueError("Native material folder metadata type differs")
    write(BINDINGS, encode(dict(mapClassId=13, manifest="871_156598", entries=entries))); meta(BINDINGS)
    references = [MATERIALS, MATERIAL_RECEIPT, TEXTURE_RECEIPT, SHADER_RECEIPT, ENGINE_RECEIPT, template_source, template_path, nonfinite_template,
        CACHE / "native-nonfinite-material-template.binary.asset"]
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest="871_156598",
        dependencies=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in references],
        serialization_reference=template, counts=dict(materials=len(entries), shaders=len(shader_by_id),
            native_texture_references=bound_textures, deferred_empty_dynamic_atlases=deferred,
            keyword_reclassifications=sum(e["keyword_normalization"] is not None for e in entries),
            nonfinite_color_components=sum(not math.isfinite(c[channel]) for e in entries for c in e["colors"] for channel in ("r","g","b","a"))),
        bindings=dict(path=source_path(BINDINGS), sha256=sha(BINDINGS.read_bytes())), outputs=outputs,
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        serialization_exporter=dict(path="unity/Assets/Game/Editor/Map13EntityMaterials/OfficialMap13EntityMaterialSourceTools.cs",
            sha256=sha((ROOT / "unity/Assets/Game/Editor/Map13EntityMaterials/OfficialMap13EntityMaterialSourceTools.cs").read_bytes())),
        operation="Restore all original material fields directly from each exact CAB/PathID source onto actual Unity 2022.3.57f1c2 native serialization. Bind every shader and nonempty texture by verified identity. Retain saved-property order, keywords, override tags, disabled passes, queue, flags, colors, values, scales and offsets; never match ambiguous names or substitute scalar-only exported templates.",
        limitations=["Six original zero-sized font atlas identities remain explicitly deferred until original font initialization; their material references are null without fabricated pixels. Unity import, field validation, GPU rendering, entity prefab binding and full-map validation remain required."])))
    print("Restored native Map13 materials:", len(entries), "shaders", len(shader_by_id), "texture references", bound_textures, "empty atlas slots", deferred, flush=True)


if __name__ == "__main__": build()
