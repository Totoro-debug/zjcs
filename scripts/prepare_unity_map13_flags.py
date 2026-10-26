"""Decode the three official map flags for an independent Unity-native animation path.

Only the pinned MIT binary format reader is used. No Spine runtime is loaded,
copied, translated or used to sample animation or deform geometry.
"""
from copy import copy
from pathlib import Path
import json
import re

from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_native import ROOTS
from prepare_unity_map13_native_terrain import EXPORTED, GUID, guid_for
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate, body, SHADERS, SOURCE
from prepare_unity_map13_pipeline_shaders import vector_comparisons
from convert_official_skeletons import reader_type, WHEEL_SHA256
from official_metadata import Metadata


DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativeFlags"
PREFIX = "map_yuanShuiShenShe_Piaodai_"
SHADER_NAME = "Universal Render Pipeline/2D/Spine/Skeleton-Lit-Atten"


def program_body(program, vertex):
    # Preserve original UnityPerMaterial declarations as individual Windows
    # uniforms. The source UBO macro is absent from the generic GLES extractor.
    expanded = re.sub(r"(?m)^\s*UNITY_UNIFORM\s+", "uniform ", program)
    code, uniforms = body(expanded, vertex)
    return vector_comparisons(code), uniforms


def build():
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Official map flag CAB dependencies are incomplete")
    objects = [obj for bundle in apk.loaded.values() for file in serialized_files(bundle) for obj in file.objects.values()]
    texts, textures, shaders = {}, {}, {}
    for obj in objects:
        if obj.type.name == "TextAsset":
            data = obj.read()
            if data.m_Name.startswith(PREFIX): texts.setdefault(data.m_Name, []).append(obj)
        elif obj.type.name == "Texture2D":
            data = obj.read()
            if data.m_Name.startswith(PREFIX): textures.setdefault(data.m_Name, []).append(obj)
        elif obj.type.name == "Shader":
            data = obj.read()
            if data.m_ParsedForm.m_Name == SHADER_NAME: shaders[obj.assets_file.name, obj.path_id] = obj
    if len(shaders) != 1: raise ValueError("Original flag shader is not unique")
    shader_obj = next(iter(shaders.values())); shader = shader_obj.read()
    passages, programs, header = [], [], None
    if len(shader.m_ParsedForm.m_SubShaders) != 1: raise ValueError("Restore original flag subshader structure")
    for pi, original in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
        sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original]
        form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
        one = copy(shader); one.m_ParsedForm = form
        text, variants = translate(one, "NativeFlag", program_body=program_body)
        begin = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
        if header is None: header = text[:begin]
        elif header != text[:begin]: raise ValueError("Original flag shader properties differ across passes")
        passages.append(text[begin:end])
        for variant in variants:
            path = SOURCE / ("NativeFlag-p" + str(pi) + "-" + str(variant["index"]) + ".glsl.txt")
            write(path, variant["source"].encode()); meta(path)
            programs.append(dict(pass_index=pi, index=variant["index"], keywords=variant["keywords"], path=source_path(path), sha256=sha(path.read_bytes())))
    shader_path = SHADERS / "NativeFlag.shader"
    write(shader_path, (header + "".join(passages) + "}\nFallback Off\n}\n").encode()); import_meta(shader_path, "ShaderImporter")
    shader_guid = GUID.search(Path(str(shader_path) + ".meta").read_text(encoding="utf8"))[1]
    reader_class, _ = reader_type()
    template_path = ROOT / ".tools/map13-native-cache-871_156598/native-texture-template.asset"
    template = template_path.read_text(encoding="utf8")
    component_source_path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-components.json"
    components = json.loads(component_source_path.read_text(encoding="utf8"))["components"]
    originals = [c for c in components if c["type"] == "Spine.Unity.SkeletonDataAsset"]
    instances = [c for c in components if c["type"] == "Spine.Unity.SkeletonMecanim"]
    instance_settings = [{k: v for k, v in c["fields"].items() if k not in
        ("m_GameObject", "m_Script", "m_Name", "skeletonDataAsset")} for c in instances]
    if len(instances) != 46 or any(settings != instance_settings[0] for settings in instance_settings):
        raise ValueError("Restore differing original flag instance animation/rendering settings")
    settings = instance_settings[0]
    metadata_path = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    if sha(metadata_path.read_bytes()) != "58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4":
        raise ValueError("Original flag enum metadata differs")
    metadata = Metadata(metadata_path.read_bytes())
    enums = {t["name"]: {f["name"]: metadata.default_int(f["index"]) for f in t["fields"] if f["index"] in metadata.defaults}
        for t in metadata.types if t["namespace"] == "Spine.Unity" and t["name"] in ("UpdateTiming", "UpdateMode")}
    if settings["updateTiming"] != enums["UpdateTiming"]["InUpdate"] or settings["updateWhenInvisible"] != enums["UpdateMode"]["Nothing"]:
        raise ValueError("Restore original flag update/visibility scheduling")
    if any(settings[k] for k in ["initialSkinName", "initialFlipX", "initialFlipY", "separatorSlotNames", "zSpacing",
        "clearStateOnDisable", "tintBlack", "addNormals", "calculateTangents", "maskInteraction"]):
        raise ValueError("Restore additional original flag skin/flip/depth/rendering behavior")
    models, inputs, outputs, source_textures = [], [], [], []
    def output(path, raw, metadata=None):
        write(path, raw)
        if metadata is not None: write(Path(str(path) + ".meta"), metadata.encode())
        else: meta(path)
        outputs.append(dict(path=source_path(path), sha256=sha(raw), meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
    for index in range(1, 4):
        name = PREFIX + str(index).zfill(2)
        records = texts[name + ".skel"]
        if len(records) != 1 or len(texts[name + ".atlas"]) != 1 or len(textures[name]) != 1:
            raise ValueError("Original flag payload is not unique: " + name)
        skeleton_obj = records[0]; raw = skeleton_obj.read().m_Script.encode("utf8", "surrogateescape")
        exported = EXPORTED / "AppearanceAssets/Map" / name / (name + ".skel.bytes")
        if exported.read_bytes() != raw: raise ValueError("Native export flag binary differs from official TextAsset")
        reader = reader_class(); skeleton = reader.convert(raw)
        if reader.inp.index != len(raw): raise ValueError("Flag binary was not fully consumed")
        if set(skeleton) != {"skeleton", "bones", "slots", "skins", "animations"} or skeleton["skeleton"]["spine"] != "4.2.28":
            raise ValueError("Restore additional original flag features before conversion")
        if len(skeleton["slots"]) != 1 or len(skeleton["skins"]) != 1 or set(skeleton["animations"]) != {"loop"}:
            raise ValueError("Restore original multi-slot/skin/animation flag behavior")
        slot = skeleton["slots"][0]; attachment = skeleton["skins"][0]["attachments"][slot["name"]][slot["attachment"]]
        if set(slot) != {"name", "bone", "attachment"} or set(skeleton["animations"]["loop"]) != {"bones"}:
            raise ValueError("Restore original flag slot color/blend/draw-order/event behavior")
        if attachment["type"] != "mesh": raise ValueError("Restore original flag attachment type")
        bone_names = {b["name"]: i for i, b in enumerate(skeleton["bones"])}
        bones = []
        for b in skeleton["bones"]:
            if set(b) - {"name", "parent", "rotation", "x", "y", "length"}: raise ValueError("Restore original flag inheritance/scale/shear")
            bones.append(dict(name=b["name"], parent=bone_names[b["parent"]] if "parent" in b else -1,
                x=b.get("x", 0), y=b.get("y", 0), rotation=b.get("rotation", 0)))
        tracks = []
        for bone, channels in skeleton["animations"]["loop"]["bones"].items():
            if set(channels) != {"rotate"}: raise ValueError("Restore original flag animation channel")
            keys = []
            for key in channels["rotate"]:
                if set(key) - {"time", "value", "curve"}: raise ValueError("Restore original flag timeline fields")
                curve = key.get("curve", [])
                if not isinstance(curve, list) or len(curve) not in (0, 4): raise ValueError("Restore original flag stepped/multi-value curve")
                keys.append(dict(time=key.get("time", 0), value=key.get("value", 0), curve=curve))
            tracks.append(dict(bone=bone_names[bone], keys=keys))
        cursor, influences, vertices = 0, [], []
        weights = attachment["vertices"]
        for vertex in range(len(attachment["uvs"]) // 2):
            count = int(weights[cursor]); cursor += 1
            vertices.append(dict(first=len(influences), count=count))
            for _ in range(count):
                bone, x, y, weight = weights[cursor:cursor + 4]; cursor += 4
                if bone != int(bone) or not 0 <= bone < len(bones): raise ValueError("Original flag weight bone differs")
                influences.append(dict(bone=int(bone), x=x, y=y, weight=weight))
        if cursor != len(weights): raise ValueError("Original flag influence data was not fully consumed")
        atlas_obj = texts[name + ".atlas"][0]; atlas = atlas_obj.read().m_Script
        match = re.fullmatch(re.escape(name) + r"\.png\s+size: (\d+), (\d+)\s+filter: Linear, Linear\s+" + re.escape(slot["attachment"]) + r"\s+bounds: (\d+), (\d+), (\d+), (\d+)\s*", atlas)
        if not match: raise ValueError("Restore original flag rotated/trimmed atlas region")
        width, height, left, top, region_width, region_height = map(int, match.groups())
        uv = [dict(x=(left + attachment["uvs"][i] * region_width) / width,
            y=1 - (top + attachment["uvs"][i + 1] * region_height) / height) for i in range(0, len(attachment["uvs"]), 2)]
        original = next(o for o in originals if o["fields"]["m_Name"] == name + "_SkeletonData"); scale = original["fields"]["scale"]
        if any(original["fields"][k] for k in ["skeletonDataModifiers", "fromAnimation", "toAnimation", "duration", "zSlots"]):
            raise ValueError("Restore original flag modifiers/mixing/depth")
        texture_obj = textures[name][0]; texture = texture_obj.read()
        if (texture.m_Width, texture.m_Height) != (width, height): raise ValueError("Original flag atlas dimensions differ")
        rgba, mips, profile = decode_mips(texture)
        texture_path = DESTINATION / (name + ".texture.asset"); texture_guid = guid_for(Path("NativeFlags") / (name + ".texture.asset"))
        metadata = "fileFormatVersion: 2\nguid: " + texture_guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        output(texture_path, native_texture(template, texture, rgba), metadata)
        source_textures.append(dict(path=source_path(texture_path), source=apk.provenance(texture_obj), object_sha256=sha(texture_obj.get_raw_data()),
            astc_sha256=sha(texture.get_image_data()), rgba_sha256=sha(rgba), profile=profile, mips=mips,
            width=width, height=height, mip_count=texture.m_MipCount, sRGB=texture.m_ColorSpace == 1))
        material = EXPORTED / "AppearanceAssets/Map" / name / (name + "_Material.mat")
        if not material.exists():
            candidates = list((EXPORTED / "AppearanceAssets/Map" / name).glob("*.mat"))
            if len(candidates) != 1: raise ValueError("Original flag material is not unique")
            material = candidates[0]
        text = material.read_text(encoding="utf8")
        old_texture = GUID.search(Path(str(EXPORTED / "AppearanceAssets/Map" / name / (name + ".png")) + ".meta").read_text(encoding="utf8"))[1]
        old_shader = re.search(r"m_Shader: \{fileID: 4800000, guid: (\w+), type: 3\}", text)[1]
        text = text.replace(old_texture, texture_guid).replace(old_shader, shader_guid)
        text = text.replace("fileID: 2800000, guid: " + texture_guid + ", type: 3", "fileID: 2800000, guid: " + texture_guid + ", type: 2")
        material_path = DESTINATION / (name + ".mat")
        output(material_path, text.encode())
        material_obj = next(o for o in objects if o.type.name == "Material" and o.read().m_Name == name + "_Material")
        inputs.append(dict(source=apk.provenance(material_obj), object_sha256=sha(material_obj.get_raw_data()),
            exported_path=source_path(material), exported_sha256=sha(material.read_bytes())))
        decoded_path = ROOT / "unity/Assets/Official/World/Map/13/Source" / (name + ".skeleton.json")
        output(decoded_path, encode(skeleton))
        models.append(dict(name=name, skeleton_path=source_path(decoded_path), scale=scale, bones=bones, vertices=vertices, influences=influences,
            uv=uv, triangles=attachment["triangles"], tracks=tracks, duration=max(k["time"] for t in tracks for k in t["keys"]),
            material_path=source_path(material_path), texture_path=source_path(texture_path),
            original_controller=source_path(EXPORTED / "AppearanceAssets/Map" / name / (name + "_Controller.controller")),
            original_clip=source_path(EXPORTED / "AppearanceAssets/Map" / name / "loop.anim")))
        for input_path in [ROOT / models[-1]["original_controller"], ROOT / models[-1]["original_clip"]]:
            inputs.append(dict(path=source_path(input_path), sha256=sha(input_path.read_bytes())))
        inputs.extend([dict(source=apk.provenance(skeleton_obj), raw_sha256=sha(raw), object_sha256=sha(skeleton_obj.get_raw_data())),
                       dict(source=apk.provenance(atlas_obj), raw_sha256=sha(atlas.encode()), object_sha256=sha(atlas_obj.get_raw_data()))])
    data_path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-flags.json"
    output(data_path, encode(dict(manifest=apk.version, mapClassId=13, models=models)))
    import_meta(DESTINATION, "DefaultImporter", folder=True)
    write(ROOT / "assets/reference/official-map/map13-native-flags-source-manifest.json", encode(dict(format=1, mapClassId=13, manifest=apk.version,
        inputs=inputs, textures=source_textures, outputs=outputs,
        shader=dict(source=apk.provenance(shader_obj), object_sha256=sha(shader_obj.get_raw_data()), path=source_path(shader_path), sha256=sha(shader_path.read_bytes()), programs=programs),
        components=dict(path=source_path(component_source_path), sha256=sha(component_source_path.read_bytes())),
        instance_settings=settings, instances=[dict(cab=c["cab"], path_id=c["path_id"], payload_sha256=c["payload_sha256"]) for c in instances],
        scheduling_metadata=dict(path=source_path(metadata_path), sha256=sha(metadata_path.read_bytes()), enums=enums),
        reader=dict(name="SkelToJson", version="1.0.1", license="MIT", wheel_sha256=WHEEL_SHA256),
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__), ROOT / "scripts/convert_official_skeletons.py", ROOT / "scripts/prepare_unity_map13_native_scene.py", ROOT / "scripts/prepare_unity_map13_water_shaders.py", ROOT / "scripts/prepare_unity_map13_pipeline_shaders.py"]],
        adapters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13FlagData.cs", ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13FlagMesh.cs"]],
        operation="Decode every byte of three original 4.2.28 flag skeletons using only the pinned MIT format reader. Preserve original hierarchy, weighted influence coordinates/order, triangles, atlas UVs, rotations and absolute cubic-Bezier control points for native Unity bone animation. Restore original complete texture mips/materials and mechanically translate the game's original flag shader. No Spine runtime is loaded, copied, translated or used for sampling.",
        limitations=["Native animation assets, complete scene-instance binding, GPU rendering and animation validation remain required."])))
    print("Decoded three original map flags:", sum(len(m["bones"]) for m in models), "bones,", sum(len(m["vertices"]) for m in models), "vertices,", len(programs), "shader variants")


if __name__ == "__main__": build()
