"""Restore native Map13 UI particle carriers and exact original dependency bindings."""
import hashlib
import json
from pathlib import Path
import re
import struct
from official_apk import Apk, ROOT
from UnityPy.classes import PPtr
from official_native_yaml_verification import documents
from prepare_unity_map13_native_scene import native_texture, decode_mips
from UnityPy.enums import TextureFormat
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_ui_particles import EXPORT, PROOF
from prepare_unity_map13_ui_particle_inputs import SOURCE

DEST = ROOT / "unity/Assets/Official/World/Map/13/UIParticles"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-ui-particle-assets-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})")


def build():
    proof = json.loads(PROOF.read_bytes()); source = json.loads(SOURCE.read_bytes())
    if proof["inputs"][0]["sha256"] != sha(SOURCE.read_bytes()):
        raise ValueError("Original UI particle source differs")
    apk = Apk.current(); objects = {o["identity"]: o for o in source["objects"]}
    originals, original_objects, natives, outputs, texture_records, material_records, mesh_records = {}, {}, {}, [], [], [], []
    selected_guids = {r["exported"]["guid"] for p in proof["exports"] for r in p["external_references"]}
    index = {}
    for suffix in ("*.mat.meta", "*.asset.meta"):
        for path in EXPORT.rglob(suffix):
            match = GUID.search(Path("\\\\?\\" + str(path.resolve())).read_text(encoding="utf8"))
            if match and match[1] in selected_guids:
                index[match[1]] = Path(str(path)[:-5])
    if set(index) != selected_guids:
        raise ValueError("Missing original exported material or mesh reference")
    shader_sources = json.loads((ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-materials.json").read_bytes())["shaders"]
    shader_binding_source = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-shader-bindings.json"
    shader_bindings = json.loads(shader_binding_source.read_bytes())["bindings"]
    shader = next(r for r in shader_sources if r["name"] == "Effect/Noise_Mask_Pro")
    binding = next(r for r in shader_bindings if r["original_name"] == shader["name"])
    shader_path = ROOT / "unity" / binding["path"]
    shader_guid = GUID.search(Path(str(shader_path) + ".meta").read_text(encoding="utf8"))[1]
    template_path = next((ROOT / "unity/Assets/Official/World/Map/13/NativeScene").rglob("*.texture.asset"))
    template = template_path.read_text(encoding="utf8").replace("--- !u!28 &2800000\n", "--- !u!28 &1\n")
    def identity(obj):
        return obj.assets_file.name.lower() + ":" + str(obj.path_id)
    def save(path, raw, file_id, prefab=False):
        guid = hashlib.md5(("zjcs-map13-original-ui-particle:" + source_path(path)).encode()).hexdigest()
        write(path, raw)
        metadata = "fileFormatVersion: 2\nguid: " + guid + ("\nPrefabImporter:\n  externalObjects: {}\n" if prefab else
            "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: " + str(file_id) + "\n") + "  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        write(Path(str(path) + ".meta"), metadata.encode())
        outputs.append(dict(path=source_path(path), sha256=sha(raw), meta_sha256=sha(metadata.encode())))
        return dict(fileID=file_id, guid=guid, type=3 if prefab else 2)
    def original(record):
        bundle = record["source"]["bundle"]
        if bundle not in originals:
            originals[bundle] = {str(o.path_id): o for o in apk.objects(bundle)}
        obj = originals[bundle][record["source"]["path_id"]]
        if sha(obj.get_raw_data()) != record["object_sha256"]:
            raise ValueError("Original UI particle component bytes changed")
        return obj
    def texture_asset(obj):
        key = identity(obj)
        if key in natives:
            return natives[key]
        texture = obj.read(check_read=True)
        if not TextureFormat(texture.m_TextureFormat).name.startswith("ASTC_"):
            raise ValueError("Restore the original UI particle texture format explicitly")
        raw, mips, profile = decode_mips(texture)
        format_id = 4
        text = native_texture(template, texture, raw).decode("utf8")
        for name, value in dict(m_TextureFormat=format_id, m_Name=json.dumps(texture.m_Name, ensure_ascii=False),
            m_ForcedFallbackFormat=texture.m_ForcedFallbackFormat, m_DownscaleFallback=int(texture.m_DownscaleFallback),
            m_IsAlphaChannelOptional=int(texture.m_IsAlphaChannelOptional)).items():
            text, count = re.subn(r"(?m)^(  " + name + r":)[^\n]*", lambda m: m[1] + " " + str(value), text)
            if count != 1:
                raise ValueError("Original native texture schema differs: " + name)
        path = DEST / "Textures" / (sha(key.encode())[:16] + ".texture.asset")
        result = save(path, text.encode(), 2800000); natives[key] = result
        texture_records.append(dict(identity=key, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
            path=source_path(path), width=texture.m_Width, height=texture.m_Height, native_format=format_id,
            profile=profile, mips=mips, raw_sha256=sha(raw), source_pixels_sha256=sha(texture.get_image_data())))
        return result
    def pointer_text(value):
        return "{fileID: " + str(value["fileID"]) + ", guid: " + value["guid"] + ", type: " + str(value["type"]) + "}"
    def scalar_compare(expected, actual, field):
        if isinstance(expected, dict):
            if not isinstance(actual, dict) or set(expected) - set(actual):
                raise ValueError("Original dependency fields differ: " + field)
            for key, value in expected.items(): scalar_compare(value, actual[key], field + "." + key)
        elif isinstance(expected, (list, tuple)):
            if isinstance(actual, dict) and (not expected or all(isinstance(x, (list, tuple)) and len(x) == 2 and isinstance(x[0], str) for x in expected)):
                scalar_compare(dict(expected), actual, field); return
            if not isinstance(actual, list) or len(expected) != len(actual): raise ValueError("Original dependency array differs: " + field)
            for i, (a, b) in enumerate(zip(expected, actual)): scalar_compare(a, b, field + "[" + str(i) + "]")
        elif isinstance(expected, float):
            if struct.pack("<f", expected) != struct.pack("<f", actual): raise ValueError("Original dependency float differs: " + field + " " + repr((expected, actual)))
        elif expected != actual: raise ValueError("Original dependency scalar differs: " + field + " " + repr((expected, actual)))
    def material_asset(obj, exported):
        key = identity(obj)
        if key in natives: return natives[key]
        data = obj.read(check_read=True); fields = obj.read_typetree(check_read=True)
        shader_obj = data.m_Shader.deref()
        if identity(shader_obj) != shader["cab"].lower() + ":" + shader["path_id"] or sha(shader_obj.get_raw_data()) != shader["payload_sha256"]:
            raise ValueError("Original UI particle shader differs from the verified Windows shader")
        text = exported.read_text(encoding="utf8"); doc = documents(text)[2100000][1]
        if doc["m_Name"] != data.m_Name: raise ValueError("Original UI particle material identity differs")
        for name, value in fields.items():
            if name not in ("m_Shader", "m_SavedProperties"): scalar_compare(value, doc[name], "Material." + name)
        props = fields["m_SavedProperties"]; exported_props = doc["m_SavedProperties"]
        for name, value in props.items():
            if name != "m_TexEnvs": scalar_compare(value, exported_props[name], "Material.m_SavedProperties." + name)
        expected_names = [name for name, _ in props["m_TexEnvs"]]
        if set(expected_names) != set(exported_props["m_TexEnvs"]): raise ValueError("Original material texture property names differ")
        for name, value in props["m_TexEnvs"]:
            before = exported_props["m_TexEnvs"][name]
            for k in ("m_Scale", "m_Offset"): scalar_compare(value[k], before[k], "Material." + name + "." + k)
            ptr = value["m_Texture"]
            replacement = "{fileID: 0}" if not ptr["m_PathID"] else pointer_text(texture_asset(PPtr(**ptr, assetsfile=obj.assets_file).deref()))
            pattern = r"(?m)(^      " + re.escape(name) + r":\n        m_Texture: )[^\n]*"
            text, count = re.subn(pattern, lambda m: m[1] + replacement, text)
            if count != 1: raise ValueError("Cannot bind original particle texture: " + name)
        text, count = re.subn(r"(?m)^(  m_Shader: )[^\n]*", lambda m: m[1] + pointer_text(dict(fileID=4800000, guid=shader_guid, type=3)), text)
        if count != 1: raise ValueError("Cannot bind original particle shader")
        path = DEST / "Materials" / (sha(key.encode())[:16] + ".mat")
        result = save(path, text.encode(), 2100000); natives[key] = result
        material_records.append(dict(identity=key, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
            fields=fields, path=source_path(path), export=dict(path=source_path(exported), sha256=sha(exported.read_bytes()))))
        return result
    def mesh_asset(obj, exported):
        key = identity(obj)
        if key in natives: return natives[key]
        fields = obj.read_typetree(check_read=True); text = exported.read_text(encoding="utf8"); docs = documents(text)
        file_id, = [i for i, (kind, _) in docs.items() if kind == "Mesh"]
        data = docs[file_id][1]
        for name in ("m_SubMeshes", "m_LocalAABB", "m_BindPose"):
            scalar_compare(fields[name], data[name], "Mesh." + name)
        for name in ("m_IndexBuffer",):
            if bytes(fields[name]) != bytes.fromhex(str(data[name])): raise ValueError("Original particle mesh index buffer differs")
        vertex = fields["m_VertexData"]; actual = data["m_VertexData"]
        scalar_compare(vertex["m_Channels"], actual["m_Channels"], "Mesh.channels")
        if vertex["m_VertexCount"] != actual["m_VertexCount"] or len(vertex["m_DataSize"]) != actual["m_DataSize"] or bytes(vertex["m_DataSize"]) != bytes.fromhex(str(actual["_typelessdata"])):
            raise ValueError("Original particle mesh vertex buffer differs")
        path = DEST / "Meshes" / (sha(key.encode())[:16] + ".asset")
        result = save(path, text.encode(), file_id); natives[key] = result
        mesh_records.append(dict(identity=key, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
            path=source_path(path), vertex_count=vertex["m_VertexCount"], vertices_sha256=sha(bytes(vertex["m_DataSize"])), indices_sha256=sha(bytes(fields["m_IndexBuffer"]))))
        return result
    exported = {p["path"]: p for p in proof["exports"]}
    loaded = {}; carriers = []
    for selected in proof["bindings"]:
        if objects[selected["original_identity"]]["type"] != "ParticleSystemRenderer": continue
        source_obj = original(objects[selected["original_identity"]])
        if selected["export_path"] not in loaded:
            path = ROOT / exported[selected["export_path"]]["restored"]["path"]
            loaded[selected["export_path"]] = (path.read_text(encoding="utf8"), documents(path.read_text(encoding="utf8")))
        text, docs = loaded[selected["export_path"]]; renderer = docs[selected["file_id"]][1]
        replacements = {}
        for ptr, target in zip(source_obj.read().m_Materials, renderer["m_Materials"]):
            replacements[target["guid"]] = material_asset(ptr.deref(), index[target["guid"]])
        mesh = source_obj.read().m_Mesh
        if mesh.path_id:
            replacements[renderer["m_Mesh"]["guid"]] = mesh_asset(mesh.deref(), index[renderer["m_Mesh"]["guid"]])
        go_id = renderer["m_GameObject"]["fileID"]
        ps_id, = [r["component"]["fileID"] for r in docs[go_id][1]["m_Component"] if docs[r["component"]["fileID"]][0] == "ParticleSystem"]
        body = []
        for file_id, new_id in [(ps_id, 1980000000000001), (selected["file_id"], 1990000000000001)]:
            block = re.search(r"(?ms)^--- !u!\d+ &" + str(file_id) + r"\n.*?(?=^--- !u!|\Z)", text)[0]
            block = re.sub(r"(?m)^(--- !u!\d+ &)-?\d+", lambda m: m[1] + str(new_id), block)
            block = block.replace("m_GameObject: {fileID: " + str(go_id) + "}", "m_GameObject: {fileID: 1000000000000001}")
            for guid, target in replacements.items():
                block = re.sub(r"\{fileID: -?\d+, guid: " + guid + r", type: \d+\}", lambda m: pointer_text(target), block)
            body.append(block)
        prefix = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &1\nGameObject:\n  serializedVersion: 6\n  m_ObjectHideFlags: 0\n  m_Component:\n  - component: {fileID: 4}\n  - component: {fileID: 198}\n  - component: {fileID: 199}\n  m_Layer: 5\n  m_Name: NativeParticleCarrier\n  m_TagString: Untagged\n  m_IsActive: 1\n--- !u!4 &4\nTransform:\n  serializedVersion: 2\n  m_ObjectHideFlags: 0\n  m_GameObject: {fileID: 1}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        for old, new in [(198, 1980000000000001), (199, 1990000000000001), (1, 1000000000000001), (4, 4000000000000001)]:
            prefix = prefix.replace("&" + str(old) + "\n", "&" + str(new) + "\n").replace("fileID: " + str(old) + "}", "fileID: " + str(new) + "}")
        path = DEST / "Carriers" / (sha(selected["original_identity"].encode())[:16] + ".prefab")
        save(path, (prefix + "".join(body)).encode(), 100100000, prefab=True)
        carriers.append(dict(panel=selected["panel"], hierarchy_path=selected["hierarchy_path"],
            original_renderer_identity=selected["original_identity"], path=source_path(path)))
    node_maps, coffee = {}, []
    selected_panels = {r["panel"] for r in carriers}
    for panel in source["prefabs"]:
        if panel["key"] not in selected_panels: continue
        by_component = {}
        def walk(node, indices):
            for component in node["components"]: by_component[component] = indices
            for i, child in enumerate(node["children"]): walk(child, indices + [i])
        walk(panel["hierarchy"], [])
        node_maps[panel["key"]] = by_component
        for component, indices in by_component.items():
            record = objects[component]
            if record["type"] != "Coffee.UIExtensions.UIParticle": continue
            fields = record["fields"]
            if fields["m_Material"]["m_PathID"] or fields["m_AnimatableProperties"] or fields["m_OnCullStateChanged"]["m_PersistentCalls"]["m_Calls"]:
                raise ValueError("Restore additional original UI particle material, animation or cull callbacks")
            particle_nodes = []
            for pointer in fields["m_Particles"]:
                target = component.split(":")[0] + ":" + str(pointer["m_PathID"])
                if pointer["m_FileID"] or target not in by_component or objects[target]["type"] != "ParticleSystem":
                    raise ValueError("Unresolved original Coffee particle reference")
                particle_nodes.append(dict(indices=by_component[target]))
            excluded = {"m_GameObject", "m_Script", "m_Name", "m_Material", "m_Particles", "m_OnCullStateChanged", "m_AnimatableProperties"}
            canvas, = [o for o in objects.values() if o["type"] == "CanvasRenderer" and o["identity"].split(":")[0] == component.split(":")[0] and o["fields"]["m_GameObject"] == fields["m_GameObject"]]
            coffee.append(dict(panel=panel["key"], indices=indices, source_identity=component, canvas_cull_transparent=canvas["fields"]["m_CullTransparentMesh"],
                fields_json=json.dumps({k: v for k, v in fields.items() if k not in excluded}, ensure_ascii=False), particles=particle_nodes))
    for carrier in carriers:
        carrier["indices"] = node_maps[carrier["panel"]][carrier["original_renderer_identity"]]
    if len(coffee) != 32 or sum(len(c["particles"]) for c in coffee) != 80:
        raise ValueError("Original Coffee particle coverage differs")
    bindings_path = DEST / "bindings.json"
    write(bindings_path, encode(dict(manifest="871_156598", mapClassId=13, carriers=carriers, coffee=coffee)))
    meta(bindings_path)
    outputs.append(dict(path=source_path(bindings_path), sha256=sha(bindings_path.read_bytes()), meta_sha256=sha(Path(str(bindings_path) + ".meta").read_bytes())))
    for folder in [DEST, *sorted(p for p in DEST.rglob("*") if p.is_dir())]: meta(folder, folder=True)
    result = dict(manifest="871_156598", mapClassId=13, source=dict(path=source_path(PROOF), sha256=sha(PROOF.read_bytes())),
        shader=dict(original=shader, binding=binding, sha256=sha(shader_path.read_bytes())),
        serialization_reference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        carriers=carriers, coffee=coffee, textures=texture_records, materials=material_records, meshes=mesh_records, outputs=outputs,
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in (Path(__file__).resolve(), ROOT / "scripts/prepare_unity_map13_ui_particles.py", ROOT / "scripts/prepare_unity_map13_native_scene.py")],
        operation="Copy the verified original engine particle components into editor-only carrier prefabs; bind the exact original materials, Windows shader, complete mip payloads and mesh render buffers. Carrier transforms are only transfer containers; original UI transforms remain in the restored UI prefab. Coffee component binding and actual Windows rendering are still required.")
    write(RECEIPT, encode(result)); print("Map13 UI native particle assets:", len(carriers), "carriers", len(material_records), "materials", len(texture_records), "textures", len(mesh_records), "meshes", flush=True)
    return result


if __name__ == "__main__": build()
