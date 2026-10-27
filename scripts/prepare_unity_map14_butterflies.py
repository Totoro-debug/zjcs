"""Decode both original Map14 butterfly models for independent native animation.

Uses the pinned MIT binary-format reader only. No Spine runtime is imported,
copied, translated, used to evaluate animation, or used to generate meshes.
"""
from copy import copy
from pathlib import Path
import json
import math
import re

from UnityPy.classes import PPtr
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map14_native import ROOTS
from prepare_unity_map14_native_terrain import CACHE, EXPORTED, GUID, guid_for
from prepare_unity_map14_native_scene import decode_mips, native_texture
from prepare_unity_map14_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map13_pipeline_shaders import program_body as pipeline_body, program_structure, render_state as pipeline_state
from convert_official_skeletons import reader_type, WHEEL_SHA256
from map14_butterfly_reference import checks
from prepare_battle_hud_materials import hlsl

DEST = ROOT / "unity/Assets/Official/World/Map/14/NativeButterflies"


def program_body(program, vertex):
    program = re.sub(r"(?m)^\s*UNITY_UNIFORM\s+", "uniform ", program)
    comparisons = {"equal": "==", "notEqual": "!=", "lessThan": "<", "lessThanEqual": "<=", "greaterThan": ">", "greaterThanEqual": ">="}
    for index, name in enumerate(comparisons): program = re.sub(r"\b" + name + r"\(", "Map14Comparison" + str(index) + "(", program)
    for name in ("unity_ObjectToWorld", "unity_WorldToObject"):
        identifier = "hlslcc_mtx4x4" + name
        program = re.sub(r"(?m)^uniform [^;\n]*\b" + identifier + r"\[4\];\n", "", program)
        program = re.sub(identifier + r"\[(\d)\]", lambda m: "vec4(" +
            ",".join(name + "[" + str(i) + "][" + m[1] + "]" for i in range(4)) + ")", program)
        program = re.sub(r"unity_Builtins0Array\[0\]\." + identifier + r"Array\[(\d)\]", lambda m: "vec4(" +
            ",".join(name + "[" + str(i) + "][" + m[1] + "]" for i in range(4)) + ")", program)
    try:
        code, uniforms = pipeline_body(program, vertex)
        for index, (name, operator) in enumerate(comparisons.items()):
            token = "Map14Comparison" + str(index) + "("
            while (start := code.find(token)) >= 0:
                at = start + len(token); depth = 0; comma = None
                for end in range(at, len(code)):
                    if code[end] == "(": depth += 1
                    elif code[end] == ")":
                        if depth == 0: break
                        depth -= 1
                    elif code[end] == "," and depth == 0:
                        if comma is not None: raise ValueError("Original GLSL comparison arity differs")
                        comma = end
                if comma is None: raise ValueError("Original GLSL comparison operand is missing")
                code = code[:start] + "(" + code[at:comma] + " " + operator + code[comma+1:end] + ")" + code[end+1:]
        head = (program.split("#ifdef FRAGMENT")[0] if vertex else program.split("#ifdef FRAGMENT")[1]).split("void main()", 1)[0]
        for declaration in re.findall(r"^(?:(?:mediump|highp|lowp)\s+)?(?:vec[234]|float)\s+phase\w+;", head, re.M):
            converted = hlsl(declaration)
            if converted not in code:
                marker = "= (Varying)0;" if vertex else "half4 result = 0;"
                if marker not in code: raise ValueError("Restore original butterfly phase local placement")
                code = code.replace(marker, marker + "\n" + converted, 1)
        return code, uniforms
    except ValueError:
        write(CACHE / "butterfly-untranslated.glsl.txt", program.encode())
        raise


def render_state(state):
    text = pipeline_state(state)
    if state.rtBlend0.colMask.name not in ("", "<noninit>"):
        text = re.sub(r"ColorMask [^\n]+", "ColorMask [" + state.rtBlend0.colMask.name + "]", text)
    return text


def atlas_regions(text):
    lines = text.strip().splitlines()
    page, regions, name = {}, {}, None
    for line in lines[1:]:
        if not line.strip(): continue
        if ":" not in line:
            name = line.strip(); regions[name] = {}
        else:
            key, value = map(str.strip, line.split(":", 1))
            (regions[name] if name else page)[key] = value
    if set(page) != {"size", "filter"} or page["filter"] != "Linear, Linear":
        raise ValueError("Restore additional original butterfly atlas page settings")
    width, height = map(int, page["size"].split(","))
    for name, region in regions.items():
        if set(region) - {"bounds", "offsets", "rotate"}: raise ValueError("Unknown butterfly atlas field")
        x, y, w, h = map(int, region["bounds"].split(","))
        ox, oy, ow, oh = map(int, region.get("offsets", f"0,0,{w},{h}").split(","))
        rotation = int(region.get("rotate", "0"))
        if rotation not in (0, 90, 180, 270): raise ValueError("Unsupported original atlas rotation")
        regions[name] = dict(x=x, y=y, w=w, h=h, ox=ox, oy=oy, ow=ow, oh=oh, rotation=rotation)
    return width, height, regions


def model_geometry(skeleton, atlas):
    if set(skeleton) != {"skeleton", "bones", "slots", "skins", "animations"}:
        raise ValueError("Restore original butterfly constraints/events before expanding native conversion")
    width, height, regions = atlas_regions(atlas)
    names = {b["name"]: i for i, b in enumerate(skeleton["bones"])}
    bones = []
    for b in skeleton["bones"]:
        if set(b) - {"name", "parent", "rotation", "x", "y", "scaleX", "scaleY", "shearX", "shearY", "length", "inherit", "transform"}:
            raise ValueError("Unknown original butterfly bone field")
        inherit = b.get("inherit", b.get("transform", "normal"))
        if inherit not in ("normal", "onlyTranslation"): raise ValueError("Restore additional original butterfly inheritance")
        parent = names[b["parent"]] if "parent" in b else -1
        if parent >= len(bones): raise ValueError("Original butterfly bone parent order differs")
        bones.append(dict(name=b["name"], parent=parent, x=b.get("x", 0), y=b.get("y", 0), rotation=b.get("rotation", 0),
            scaleX=b.get("scaleX", 1), scaleY=b.get("scaleY", 1), shearX=b.get("shearX", 0), shearY=b.get("shearY", 0),
            onlyTranslation=inherit == "onlyTranslation"))
    skins = {s["name"]: s["attachments"] for s in skeleton["skins"]}
    models = []
    for skin_name in skins:
        if skin_name == "default" and len(skins) > 1: continue
        positions, uv, triangles, vertex_bones = [], [], [], []
        for slot in skeleton["slots"]:
            if set(slot) != {"name", "bone", "attachment"}: raise ValueError("Restore original butterfly slot color/blend")
            attachment = skins[skin_name].get(slot["name"], {}).get(slot["attachment"])
            if attachment is None: attachment = skins["default"][slot["name"]][slot["attachment"]]
            source = attachment
            if attachment.get("type") == "linkedmesh":
                if set(attachment) - {"type", "skin", "parent", "timelines", "name", "path"}:
                    raise ValueError("Unknown original linked butterfly mesh field")
                source = skins[attachment["skin"]][slot["name"]][attachment["parent"]]
                if source.get("type") != "mesh": raise ValueError("Resolve original linked butterfly parent")
            region = regions[attachment.get("path", attachment.get("name", slot["attachment"]))]
            def atlas_uv(u, v):
                x = u * region["ow"] - region["ox"]
                y = v * region["oh"] - (region["oh"] - region["oy"] - region["h"])
                rx, ry = {0: (x, y), 90: (y, region["w"] - x),
                    180: (region["w"] - x, region["h"] - y), 270: (region["h"] - y, x)}[region["rotation"]]
                return dict(x=(region["x"] + rx) / width, y=1 - (region["y"] + ry) / height)
            start = len(positions)
            if source.get("type", "region") == "region":
                if set(source) - {"x", "y", "rotation", "scaleX", "scaleY", "width", "height", "type", "name", "path"}:
                    raise ValueError("Restore additional original butterfly region fields")
                left, bottom = -region["ow"] / 2 + region["ox"], -region["oh"] / 2 + region["oy"]
                right, top = left + region["w"], bottom + region["h"]
                angle = math.radians(source.get("rotation", 0)); c, s = math.cos(angle), math.sin(angle)
                for x, y in ((left, bottom), (left, top), (right, top), (right, bottom)):
                    uv.append(atlas_uv((x + region["ow"] / 2) / region["ow"], 1 - (y + region["oh"] / 2) / region["oh"]))
                    x *= source["width"] / region["ow"] * source.get("scaleX", 1)
                    y *= source["height"] / region["oh"] * source.get("scaleY", 1)
                    positions.append(dict(x=c*x-s*y+source.get("x", 0), y=s*x+c*y+source.get("y", 0)))
                triangles.extend(start + i for i in (0, 1, 2, 2, 3, 0))
            elif source["type"] == "mesh":
                if set(source) - {"type", "uvs", "triangles", "vertices", "hull", "edges", "width", "height", "name", "path"}:
                    raise ValueError("Restore additional original butterfly mesh fields")
                if len(source["vertices"]) != len(source["uvs"]):
                    raise ValueError("Restore weighted butterfly geometry before expanding this unweighted model carrier")
                for i in range(0, len(source["uvs"]), 2):
                    positions.append(dict(x=source["vertices"][i], y=source["vertices"][i+1]))
                    uv.append(atlas_uv(*source["uvs"][i:i+2]))
                triangles.extend(start + i for i in source["triangles"])
            else: raise ValueError("Restore original butterfly attachment type")
            vertex_bones.extend([names[slot["bone"]]] * (len(positions) - start))
        models.append(dict(name=skin_name, vertices=positions, uv=uv, triangles=triangles, vertexBones=vertex_bones))
    animations = []
    for name, animation in skeleton["animations"].items():
        if set(animation) != {"bones"}: raise ValueError("Restore additional original butterfly animation timelines")
        tracks = []
        for bone, channels in animation["bones"].items():
            for channel, keys in channels.items():
                if channel not in ("rotate", "translate", "scale", "shear"): raise ValueError("Unknown butterfly channel")
                output = []
                for key in keys:
                    allowed = {"time", "curve", "value"} if channel == "rotate" else {"time", "curve", "x", "y"}
                    if set(key) - allowed: raise ValueError("Unknown original butterfly key field")
                    curve = key.get("curve", [])
                    if curve != "stepped" and (not isinstance(curve, list) or len(curve) not in ((0, 4) if channel == "rotate" else (0, 8))):
                        raise ValueError("Original butterfly curve arity differs")
                    default = 1 if channel == "scale" else 0
                    output.append(dict(time=key.get("time", 0), x=key.get("value", 0) if channel == "rotate" else key.get("x", default),
                        y=key.get("y", default), stepped=curve == "stepped", curve=[] if curve == "stepped" else curve))
                if any(a["time"] >= b["time"] for a, b in zip(output, output[1:])):
                    raise ValueError("Original butterfly key times are not strictly increasing")
                tracks.append(dict(bone=names[bone], channel=("rotate", "translate", "scale", "shear").index(channel), keys=output))
        animations.append(dict(name=name, duration=max(k["time"] for t in tracks for k in t["keys"]), tracks=tracks))
    return dict(bones=bones, skins=models, animations=animations), (width, height)


def build():
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Original Map14 butterfly CAB closure is incomplete")
    component_path = ROOT / "unity/Assets/Official/World/Map/14/Source/native-components.json"
    components = json.loads(component_path.read_bytes())["components"]
    objects = {(o.assets_file.name, str(o.path_id)): o for b in apk.loaded.values() for f in serialized_files(b) for o in f.objects.values()}
    records = {(c["cab"], c["path_id"]): c for c in components}
    inputs, outputs, textures, shaders, models, references = [], [], [], {}, [], []
    def read_record(record):
        obj = objects[record["cab"], record["path_id"]]
        if sha(obj.get_raw_data()) != record["payload_sha256"]: raise ValueError("Original butterfly component changed")
        inputs.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
        return obj, record["fields"]
    def pointer(obj, value): return PPtr(**value, assetsfile=obj.assets_file).deref()
    def output(path, raw, metadata=None):
        write(path, raw)
        if metadata is None and path.suffix == ".mat":
            metadata = "fileFormatVersion: 2\nguid: " + guid_for(Path("NativeButterflies") / path.name) + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        if metadata is None: meta(path)
        else: write(Path(str(path) + ".meta"), metadata.encode())
        outputs.append(dict(path=source_path(path), sha256=sha(raw), meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
    reader_class, _ = reader_type()
    source_assets = [c for c in components if c["type"] == "Spine.Unity.SkeletonDataAsset"]
    if len(source_assets) != 2: raise ValueError("Original Map14 butterfly model count differs")
    for record in source_assets:
        obj, data = read_record(record); name = data["m_Name"]
        if any(data[k] for k in ("skeletonDataModifiers", "fromAnimation", "toAnimation", "duration", "zSlots")):
            raise ValueError("Restore original butterfly modifiers/mixing/depth")
        if data["blendModeMaterials"]["requiresBlendModeMaterials"] or len(data["atlasAssets"]) != 1:
            raise ValueError("Restore original butterfly multi-atlas/blend material")
        text_obj = pointer(obj, data["skeletonJSON"]); raw = text_obj.read().m_Script.encode("utf8", "surrogateescape")
        if raw.lstrip().startswith(b"{"): skeleton = json.loads(raw)
        else:
            reader = reader_class(); skeleton = reader.convert(raw)
            if reader.inp.index != len(raw): raise ValueError("Original butterfly binary was not fully consumed")
        inputs.append(dict(source=apk.provenance(text_obj), object_sha256=sha(text_obj.get_raw_data()), raw_sha256=sha(raw)))
        atlas_obj = pointer(obj, data["atlasAssets"][0]); _, af = read_record(records[atlas_obj.assets_file.name, str(atlas_obj.path_id)])
        atlas_text_obj = pointer(atlas_obj, af["atlasFile"]); atlas = atlas_text_obj.read().m_Script
        inputs.append(dict(source=apk.provenance(atlas_text_obj), object_sha256=sha(atlas_text_obj.get_raw_data()), raw_sha256=sha(atlas.encode())))
        if len(af["materials"]) != 1: raise ValueError("Restore original butterfly multi-material atlas")
        material_obj = pointer(atlas_obj, af["materials"][0]); material = material_obj.read()
        texture_obj, = [p.m_Texture.deref() for k, p in material.m_SavedProperties.m_TexEnvs if k == "_MainTex"]
        texture = texture_obj.read(); geometry, size = model_geometry(skeleton, atlas)
        if size != (texture.m_Width, texture.m_Height): raise ValueError("Original butterfly atlas/texture size differs")
        rgba, mips, profile = decode_mips(texture)
        texture_path = DEST / (name + ".texture.asset"); texture_guid = guid_for(Path("NativeButterflies") / texture_path.name)
        metadata = "fileFormatVersion: 2\nguid: " + texture_guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        output(texture_path, native_texture((CACHE / "native-texture-template.asset").read_text(encoding="utf8"), texture, rgba), metadata)
        textures.append(dict(source=apk.provenance(texture_obj), object_sha256=sha(texture_obj.get_raw_data()), path=source_path(texture_path),
            rgba_sha256=sha(rgba), astc_sha256=sha(texture.get_image_data()), width=size[0], height=size[1], mip_count=texture.m_MipCount, mips=mips, profile=profile))
        shader_obj = material.m_Shader.deref(); key = (shader_obj.assets_file.name, str(shader_obj.path_id))
        if key not in shaders:
            shader = shader_obj.read(); header, passages, programs = None, [], []
            if len(shader.m_ParsedForm.m_SubShaders) != 1: raise ValueError("Restore original butterfly shader subshaders")
            shader_path = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeMap14/Shaders" / ("Butterfly" + str(len(shaders)) + ".shader")
            for pi, original in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
                sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original]
                form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
                one = copy(shader); one.m_ParsedForm = form
                text, variants = translate(one, "Butterfly" + str(len(shaders)), program_body=program_body,
                    program_structure=program_structure, render_state=render_state, target="4.5")
                text = text.replace("Zjcs/Official/World/Map13/", "Zjcs/Official/World/Map14/")
                begin = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
                if header is None: header = text[:begin]
                elif header != text[:begin]: raise ValueError("Original butterfly shader pass properties differ")
                passages.append(text[begin:end])
                for variant in variants:
                    path = DEST / (shader_path.stem + "-p" + str(pi) + "-" + str(variant["index"]) + ".glsl.txt")
                    output(path, variant["source"].encode())
                    programs.append(dict(pass_index=pi, index=variant["index"], keywords=variant["keywords"], path=source_path(path), sha256=sha(path.read_bytes())))
            write(shader_path, (header + "".join(passages) + "}\nFallback Off\n}\n").encode()); import_meta(shader_path, "ShaderImporter")
            shaders[key] = dict(source=apk.provenance(shader_obj), object_sha256=sha(shader_obj.get_raw_data()), path=source_path(shader_path),
                sha256=sha(shader_path.read_bytes()), original_shader=shader.m_ParsedForm.m_Name, programs=programs)
        shader_path = ROOT / shaders[key]["path"]
        shader_guid = GUID.search(Path(str(shader_path) + ".meta").read_text(encoding="utf8"))[1]
        original_materials = [p for p in EXPORTED.rglob("*.mat") if re.search(r"(?m)^  m_Name: " + re.escape(material.m_Name) + r"$", p.read_text(encoding="utf8"))]
        if len(original_materials) != 1: raise ValueError("Original butterfly material export is not unique")
        original_material = original_materials[0]; text = original_material.read_text(encoding="utf8")
        text = re.sub(r"(m_Shader: \{fileID: 4800000, guid: )\w+(, type: )[23](\})", lambda m: m[1] + shader_guid + m[2] + "3" + m[3], text)
        texture_match = re.search(r"m_Texture: \{fileID: 2800000, guid: (\w+), type: 3\}", text)
        if texture_match is None: raise ValueError("Original butterfly material texture is missing")
        text = text.replace(texture_match[1], texture_guid).replace("fileID: 2800000, guid: " + texture_guid + ", type: 3", "fileID: 2800000, guid: " + texture_guid + ", type: 2")
        material_path = DEST / (name + ".mat"); output(material_path, text.encode())
        inputs.append(dict(source=apk.provenance(material_obj), object_sha256=sha(material_obj.get_raw_data()), path=source_path(original_material), sha256=sha(original_material.read_bytes())))
        output(DEST / (name + ".skeleton-source.txt"), encode(skeleton)); output(DEST / (name + ".atlas-source.txt"), atlas.encode())
        models.append(dict(name=name, scale=data["scale"], materialPath=source_path(material_path), **geometry))
        references.append(dict(name=name, checks=checks(skeleton, geometry, data["scale"])))
    instances = [c for c in components if c["type"] == "Spine.Unity.SkeletonAnimation"]
    if len(instances) != 238: raise ValueError("Original butterfly instance count differs")
    for instance in instances:
        read_record(instance); f = instance["fields"]
        if any(f[k] for k in ("initialFlipX", "initialFlipY", "separatorSlotNames", "zSpacing", "clearStateOnDisable", "tintBlack", "addNormals", "calculateTangents", "maskInteraction")):
            raise ValueError("Restore additional original butterfly instance behavior")
        if f["updateTiming"] != 1 or f["updateWhenInvisible"] not in (0, 3) or f["unscaledTime"] or not f["loop"] or f["timeScale"] != 1:
            raise ValueError("Original butterfly update/loop settings differ")
    output(DEST / "models.json", encode(dict(manifest=apk.version, mapClassId=14, models=models)))
    output(DEST / "reference-checks.json", encode(dict(manifest=apk.version, mapClassId=14, models=references)))
    for folder in [DEST, *sorted(p for p in DEST.rglob("*") if p.is_dir())]: meta(folder, folder=True)
    receipt = ROOT / "assets/reference/official-map/map14-native-butterflies-source-manifest.json"
    write(receipt, encode(dict(format=1, manifest=apk.version, mapClassId=14, inputs=inputs, outputs=outputs, textures=textures, shaders=list(shaders.values()),
        components=dict(path=source_path(component_path), sha256=sha(component_path.read_bytes())),
        instances=[dict(cab=c["cab"], path_id=c["path_id"], payload_sha256=c["payload_sha256"], fields=c["fields"]) for c in instances],
        reader=dict(name="SkelToJson", version="1.0.1", license="MIT", wheel_sha256=WHEEL_SHA256),
        generators=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__).resolve(), ROOT / "scripts/map14_butterfly_reference.py", ROOT / "scripts/convert_official_skeletons.py", ROOT / "scripts/prepare_unity_map14_native_scene.py", ROOT / "scripts/prepare_unity_map13_pipeline_shaders.py", ROOT / "scripts/prepare_unity_map13_water_shaders.py"]],
        operation="Independently decode both original skeletons and preserve all bones, only-translation inheritance, skin fallback/linked geometry, atlas trim and quarter rotations, unweighted meshes/regions, ordered slots, and all original rotate/translate/scale/shear keys with absolute cubic controls and stepped curves. Preserve all texture mips and mechanically translate each original material shader. No Spine runtime is used.",
        limitations=["Native data/mesh component binding, clock/animation verification, complete partition rendering and gameplay are still required."])))
    print("Decoded original Map14 butterflies:", len(models), "models,", len(instances), "instances,", sum(len(m["animations"]) for m in models), "animations", flush=True)


if __name__ == "__main__": build()
