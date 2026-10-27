"""Recover original entity materials and their exact native shader/texture links.

Reuse the already verified APK-first input closure. Exporter-generated font
materials are not treated as original game materials or used to infer shaders.
"""
import gc
import json
from pathlib import Path
from collections import Counter

from prepare_unity_map14_entity_inputs import CACHE, RECEIPT as INPUT_RECEIPT
from official_apk import ROOT
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map14_sources import encode, meta, sha, source_path, write
import UnityPy


OUTPUT = ROOT / "unity/Assets/Official/World/Map/14/Source/native-entity-materials.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-materials-source-manifest.json"


def pointers(fields, cab, externals, prefix=""):
    if isinstance(fields, dict):
        if set(fields) == {"m_FileID", "m_PathID"}:
            if fields["m_PathID"]:
                target_cab = cab if fields["m_FileID"] == 0 else externals[fields["m_FileID"] - 1].replace("\\", "/").rsplit("/", 1)[-1]
                yield dict(field=prefix, cab=target_cab, path_id=str(fields["m_PathID"]))
        else:
            for key, value in fields.items():
                yield from pointers(value, cab, externals, prefix + "." + key)
    elif isinstance(fields, (list, tuple)):
        for index, value in enumerate(fields):
            yield from pointers(value, cab, externals, prefix + "[" + str(index) + "]")


def build():
    receipt = json.loads(INPUT_RECEIPT.read_bytes())
    if receipt["manifest"] != "871_156598" or receipt["mapClassId"] != 14:
        raise ValueError("Wrong entity material input version")
    objects, materials, shaders, used_inputs = {}, [], {}, []
    counts = Counter()
    for index, entry in enumerate(receipt["inputs"]):
        path = CACHE / "inputs" / entry["output"]
        raw = path.read_bytes()
        if sha(raw) != entry["converter_input_sha256"]:
            raise ValueError("Verified native entity input changed: " + entry["output"])
        environment = UnityPy.load(raw)
        used = False
        for file in (child for bundle in environment.files.values() for child in serialized_files(bundle)):
            # Raw serialized files loaded from bytes receive a synthetic name.
            # External PPtrs retain the actual APK file name instead.
            cab = "unity default resources" if entry["output"] == "unity default resources" else file.name
            externals = [external.path for external in file.externals]
            for obj in file.objects.values():
                key = cab.casefold(), str(obj.path_id)
                if key in objects:
                    raise ValueError("Native entity object identity is duplicated")
                objects[key] = dict(cab=cab, path_id=str(obj.path_id), type=obj.type.name)
                counts[obj.type.name] += 1
                if obj.type.name == "Material":
                    fields = obj.read_typetree(check_read=True)
                    original = obj.get_raw_data()
                    # Verify the delivered JSON representation, including
                    # float32 values and tuple/map pairs, against native bytes.
                    obj.save_typetree(json.loads(encode(fields)))
                    if obj.get_raw_data() != original:
                        raise ValueError("Original material JSON changes native bytes: " + str(key))
                    materials.append(dict(cab=cab, path_id=str(obj.path_id),
                        payload_sha256=sha(original), fields=fields,
                        pointers=list(pointers(fields, cab, externals))))
                    used = True
                elif obj.type.name == "Shader":
                    data = obj.read()
                    shaders[key] = dict(cab=cab, path_id=str(obj.path_id), name=data.m_ParsedForm.m_Name,
                        payload_sha256=sha(obj.get_raw_data()), input=entry["output"],
                        subshader_passes=[len(sub.m_Passes) for sub in data.m_ParsedForm.m_SubShaders])
                    used = True
        if used:
            used_inputs.append(dict(name=entry["name"], origin=entry["origin"],
                raw_sha256=entry["raw_sha256"], decoded_sha256=entry["decoded_sha256"],
                converter_input_sha256=entry["converter_input_sha256"]))
        if index % 64 == 0:
            gc.collect()
    families = Counter()
    referenced_shaders = {}
    referenced_textures = set()
    unresolved = [dict(material=material["fields"]["m_Name"], owner_cab=material["cab"], **pointer)
                  for material in materials for pointer in material["pointers"]
                  if (pointer["cab"].casefold(), pointer["path_id"]) not in objects]
    if unresolved:
        raise ValueError("Original material pointers require resolution: " + str(unresolved) +
                         "; materials=" + str(len(materials)) + "; object counts=" + str(dict(counts)))
    for material in materials:
        shader_links = []
        for pointer in material["pointers"]:
            key = pointer["cab"].casefold(), pointer["path_id"]
            if key not in objects:
                raise ValueError("Original material pointer is unresolved: " + str(pointer))
            pointer["type"] = objects[key]["type"]
            if pointer["field"] == ".m_Shader":
                if pointer["type"] != "Shader" or key not in shaders:
                    raise ValueError("Original material shader is not a native Shader")
                shader_links.append(shaders[key])
                referenced_shaders[key] = shaders[key]
            if pointer["type"] in ("Texture2D", "Cubemap", "Texture2DArray", "CubemapArray", "RenderTexture"):
                referenced_textures.add(key)
        if len(shader_links) != 1:
            raise ValueError("Original material must have exactly one native shader")
        material["shader"] = shader_links[0]
        if shader_links[0]["name"] is not None:
            families[shader_links[0]["name"]] += 1
    if sum(counts.values()) != receipt["counts"]["checked_objects"]:
        raise ValueError("Original entity object inventory differs")
    output = dict(manifest=receipt["manifest"], mapClassId=14, materials=materials,
                  shaders=list(referenced_shaders.values()))
    write(OUTPUT, encode(output)); meta(OUTPUT)
    write(RECEIPT, encode(dict(format=1, manifest=receipt["manifest"], mapClassId=14,
        input_receipt=dict(path=source_path(INPUT_RECEIPT), sha256=sha(INPUT_RECEIPT.read_bytes())),
        inputs=used_inputs, counts=dict(materials=len(materials), shader_objects=len(referenced_shaders),
            shader_families=len(families), referenced_texture_objects=len(referenced_textures),
            checked_pointers=sum(len(m["pointers"]) for m in materials), checked_objects=sum(counts.values()),
            byte_exact_material_roundtrips=len(materials)),
        families=dict(sorted(families.items())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        operation="Strict native Material decoding from the byte-verified entity input closure. Preserve all serialized fields and shader/texture PPtrs, including pointers nested inside serialized map pairs. JSON-roundtrip each material and require native binary reserialization to match every original byte. Resolve every native object pointer against the full CAB/object inventory, using the APK file name for raw unity default resources. Obtain shader names from original Shader objects.",
        limitations=["Source recovery only. Windows shader conversion, native materials and texture import/binding, gameplay rendering and saves remain required."])))
    print("Original entity materials:", len(materials), "shader families:", len(families),
          "native texture targets:", len(referenced_textures), flush=True)


if __name__ == "__main__":
    build()
