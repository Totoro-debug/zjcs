"""Restore original map-root cloud subtrees with explicit native Windows bindings."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re

import yaml
from UnityPy.classes import PPtr

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_map13_native_sprite_yaml import Dumper, source_header
from prepare_unity_map13_sources import encode, write
from prepare_unity_map13_water_shaders import translate

DEST = ROOT / "unity/Assets/Official/World/Mainline/Fog"
CONTROLLER = ROOT / "unity/Assets/Game/Runtime/Official/MapRootFog/CloudController2.cs"
AGENT = CONTROLLER.with_name("LocalGridVisibilityAgent.cs")


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def guid(path):
    return hashlib.md5(("zjcs-mainline-grid-fog:" + path.relative_to(ROOT).as_posix()).encode()).hexdigest()


def meta(path, importer, file_id=None, folder=False):
    text = "fileFormatVersion: 2\nguid: " + guid(path) + "\n"
    if folder:
        text += "folderAsset: yes\n"
    text += importer + ":\n  externalObjects: {}\n"
    if file_id:
        text += "  mainObjectFileID: " + str(file_id) + "\n"
    write(Path(str(path) + ".meta"), text.encode())


def build(map_ids):
    apk = Apk.current()
    schemas = ScriptSchemas()
    source_code = ROOT / ".tools/source-packages/official-code/client-source/CloudController2.cs"
    original_receipt = ROOT / "assets/reference/official-map/map14-root-fog-source-manifest.json"
    old = json.loads(original_receipt.read_bytes())
    expected = next(v["sha256"] for v in old["inputs"] if v["path"].endswith("/CloudController2.cs"))
    if sha(source_code.read_bytes().replace(b"\r\n", b"\n")) != expected:
        raise ValueError("Selected original cloud code changed; re-migrate its full implementation first")
    # Keep prior original implementation provenance separate from the currently
    # selected native resource version. No SDK implementation is copied.
    proof = dict(format=1, manifest=apk.version, code_identity=dict(current=record(source_code),
        migrated=record(CONTROLLER), prior_receipt=record(original_receipt)), maps=[], objects=[], outputs=[])
    agent_source = source_code.with_name("MyGridVisibilityAgent.cs")
    agent = agent_source.read_text(encoding="utf-8-sig")
    # Port the complete original client agent. Replace its network/entity/DI
    # services with already-initialized local inputs; retain every event branch,
    # reveal queue, source override and target operation in its original order.
    agent = agent.replace("using Common;", "using Zjcs.Official.World.NativeTerrain.Common;\nusing Zjcs.Official.World.Map13GridVisibility;")
    agent = agent.replace("using Framework.DependencyInjection;\n", "")
    agent = agent.replace("using UnityEngine;", "using UnityEngine;\nusing Object = UnityEngine.Object;")
    agent = agent.replace("public class MyGridVisibilityAgent : MyComponent<GridVisibilityAgentComponent, GridVisibilityAgentComponentInfo>",
        "namespace Zjcs.Official.MapRootFog\n{\npublic sealed class MyGridVisibilityAgent : IDisposable")
    agent = agent.replace("private GridGroundComponent mGridGroundComponent;", "private readonly FogGroundData mGridGroundComponent;\n\tprivate readonly FogVisibilityData visibility;\n\tprivate readonly GridVisibilityAgentComponent Component;\n\tprivate readonly Func<Pos2d?> transferPosition;\n\tprivate readonly Func<Pos2d> characterPosition;\n\n\tpublic MyGridVisibilityAgent(GridVisibilityAgentComponent component, FogVisibilityData visibility, FogGroundData ground, CloudController2 cloud, Func<Pos2d?> transferPosition, Func<Pos2d> characterPosition)\n\t{\n\t\tComponent = component; this.visibility = visibility; mGridGroundComponent = ground; CloudController = cloud;\n\t\tthis.transferPosition = transferPosition; this.characterPosition = characterPosition;\n\t}")
    agent = re.sub(r"\t\[Inject\]\n\tprivate IMapRootManagerProxy.*?\n\tpublic event", "\tpublic event", agent, flags=re.S)
    agent = agent.replace("public override void OnStart(bool newEntity)", "public void Start()")
    agent = agent.replace("\t\tbase.OnStart(newEntity);\n\t\tmGridGroundComponent = Component.GetComponent<GridGroundComponent>();\n", "")
    agent = re.sub(r"\t\tif \(!myContainer.Inited\).*?\n\t\tComponent.OnPosVisibilityUpdate", "\t\tOnInited();\n\t\tComponent.OnPosVisibilityUpdate", agent, flags=re.S)
    agent = agent.replace("public override void OnEnd(MySerializable userData)", "public void Dispose()")
    agent = agent.replace("\t\t((MonoBehaviour)this).StopAllCoroutines();\n\t\tbase.OnEnd(userData);\n", "")
    agent = re.sub(r"\t\tCharacterComponent character =.*?return MapMyCharacterManager.MyMeUiCharacter\?\.GridPos \?\? character.GridTransform.Pos;",
        "\t\treturn transferPosition?.Invoke() ?? characterPosition();", agent, flags=re.S)
    agent = agent.replace("GridGroundComponentInfo info = mGridGroundComponent.Info;", "FogGroundInfo info = mGridGroundComponent.Info;")
    agent = agent.replace("cloudController.Initialize(Component, mGridGroundComponent);", "cloudController.Initialize(visibility, mGridGroundComponent);")
    agent = re.sub(r"\t\tif \(\(Object\)\(object\)myContainer != \(Object\)null\).*?\n\t\tif \(Component != null\)", "\t\tif (Component != null)", agent, flags=re.S)
    agent += "\n}\n"
    agent = agent.replace("MyGridVisibilityAgent", "LocalGridVisibilityAgent")
    if any(token in agent for token in ("myContainer", "base.", "[Inject]", "MapMyCharacterManager", "CharacterComponent")):
        raise ValueError("Original agent platform adaptation is incomplete")
    write(AGENT, agent.encode())
    if not Path(str(AGENT) + ".meta").exists():
        meta(AGENT, "MonoImporter")
    proof["agent"] = dict(original=record(agent_source), migrated=record(AGENT), adaptation="Replace only DI/entity lifecycle, original ground/visibility wrappers and transfer/UI-character lookups with local inputs; complete original agent event/reveal/ban/target implementation retained.")
    exported = {}

    def key(obj):
        return obj.assets_file.name.lower() + ":" + str(obj.path_id)

    def resolve(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref()

    def native(path, kind, data, file_id):
        raw = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!" + str(kind) + " &" + str(file_id) +
            "\n" + yaml.dump({data[0]: data[1]}, Dumper=Dumper, sort_keys=False, allow_unicode=True)).encode()
        write(path, raw)
        meta(path, "NativeFormatImporter", file_id)

    def emit_asset(obj):
        identity = key(obj)
        if identity in exported:
            return exported[identity]
        kind = obj.type.name
        file_id = {"Material": 2100000, "Shader": 4800000, "Texture2D": 2800000, "Sprite": 21300000}.get(kind)
        if not file_id:
            raise ValueError("Unbound original fog resource: " + kind + " " + identity)
        value = obj.read()
        name = value.m_ParsedForm.m_Name if kind == "Shader" else value.m_Name
        suffix = ".shader" if kind == "Shader" else ".mat" if kind == "Material" else ".asset"
        path = DEST / "Resources/World/Mainline/Fog/Assets" / (re.sub(r"[^\w.-]", "_", name) + "-" + sha(identity.encode())[:12] + suffix)
        reference = dict(fileID=file_id, guid=guid(path), type=3 if kind == "Shader" else 2)
        exported[identity] = reference
        evidence = dict(identity=identity, type=kind, name=name, source=apk.provenance(obj),
            object_sha256=sha(obj.get_raw_data()), output=path.relative_to(ROOT).as_posix())
        proof["objects"].append(evidence)
        if kind == "Shader":
            text, programs = translate(value, "MainlineCloud" + name.split("/")[-1])
            write(path, text.encode())
            meta(path, "ShaderImporter")
            evidence["programs"] = [dict(type=p["type"], keywords=p["keywords"], source_sha256=sha(p["source"].encode())) for p in programs]
        elif kind == "Texture2D":
            rgba, mips, profile = decode_mips(value)
            template = ROOT / "unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset"
            text = template.read_text(encoding="utf-8-sig").replace("--- !u!28 &2800000\n", "--- !u!28 &1\n")
            write(path, native_texture(text, value, rgba))
            meta(path, "NativeFormatImporter", file_id)
            evidence.update(compressed_sha256=sha(value.get_image_data()), rgba_sha256=sha(rgba), mips=mips, profile=profile)
        elif kind == "Material":
            data = convert(obj.read_typetree(check_read=True), obj, {})
            data["stringTagMap"] = dict(data["stringTagMap"])
            props = data["m_SavedProperties"]
            for field in ("m_TexEnvs", "m_Ints", "m_Floats", "m_Colors"):
                props[field] = dict(props.get(field, []))
            props["serializedVersion"] = 3
            data.update(source_header(), serializedVersion=8)
            native(path, 21, (kind, data), file_id)
        else:
            data = copy.deepcopy(obj.read_typetree(check_read=True))
            rd = data["m_RD"]
            # Root cloud mask sprites must retain their own source geometry.
            # An atlas introduces packing/rotation dependencies and needs the
            # existing atlas converter rather than silently treating it as raw.
            if data["m_SpriteAtlas"]["m_PathID"]:
                raise ValueError("Original root cloud Sprite now uses an atlas; restore its exact atlas binding")
            rd["m_IndexBuffer"] = bytes(rd["m_IndexBuffer"]).hex()
            vd = rd["m_VertexData"]
            vertices = bytes(vd["m_DataSize"])
            vd.update(serializedVersion=3, m_DataSize=len(vertices), _typelessdata=vertices.hex())
            for sub in rd["m_SubMeshes"]:
                sub["serializedVersion"] = 2
            rd["serializedVersion"] = 3
            rd["textureRect"]["serializedVersion"] = 2
            data["m_Rect"]["serializedVersion"] = 2
            render_key, local_id = data["m_RenderDataKey"]
            data["m_RenderDataKey"] = {"".join(f"{render_key['data['+str(i)+']']:08x}"[::-1] for i in range(4)): local_id}
            data = convert(data, obj, {})
            data.update(source_header())
            native(path, 213, (kind, data), file_id)
        return reference

    def convert(value, owner, local):
        if isinstance(value, dict):
            if set(value) == {"m_FileID", "m_PathID"}:
                if not value["m_PathID"]:
                    return dict(fileID=0)
                target = resolve(owner, value)
                if key(target) in local:
                    return dict(fileID=local[key(target)])
                if target.type.name == "Mesh" and target.read().m_Name == "Quad":
                    return dict(fileID=10210, guid="0000000000000000e000000000000000", type=0)
                if target.type.name == "MonoScript" and target.read().m_ClassName == "CloudController2":
                    script_guid = re.search(r"(?m)^guid: (\w+)", Path(str(CONTROLLER) + ".meta").read_text())[1]
                    return dict(fileID=11500000, guid=script_guid, type=3)
                return emit_asset(target)
            return {k: convert(v, owner, local) for k, v in value.items()}
        if isinstance(value, (list, tuple)):
            return [convert(v, owner, local) for v in value]
        return int(value) if isinstance(value, bool) else value

    for map_id in map_ids:
        bundle = f"defaultpackage_assets_prefabs_ecentity_map_maproot_maproot_{map_id}.bundle"
        roots = list(apk.container(bundle))
        address = f"Assets/Prefabs/EcEntity/Map/MapRoot/MapRoot_{map_id}/{map_id}.prefab"
        root = next(ptr.deref() for name, ptr in roots if name.casefold() == address.casefold())
        controllers = []
        for obj in root.assets_file.objects.values():
            if obj.type.name == "MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName == "CloudController2":
                controllers.append(obj)
        if len(controllers) != 1:
            agents = [obj for obj in root.assets_file.objects.values() if obj.type.name == "MonoBehaviour"
                and obj.read(check_read=False).m_Script.read().m_ClassName == "MyGridVisibilityAgent"]
            if not controllers and len(agents) == 1:
                agent_fields = schemas.read(agents[0])
                if agent_fields["CloudController"]["m_PathID"] == 0:
                    proof["maps"].append(dict(mapClassId=map_id, root=apk.provenance(root), root_sha256=sha(root.get_raw_data()),
                        cloudEnabled=False, agent=apk.provenance(agents[0]), agent_sha256=sha(agents[0].get_raw_data()),
                        originalCloudController=agent_fields["CloudController"], reason="Original root visibility agent explicitly binds no CloudController."))
                    continue
            raise ValueError(f"Map{map_id} must have one original root cloud controller, found {len(controllers)}")
        controller = controllers[0]
        fields = schemas.read(controller)
        go = resolve(controller, fields["m_GameObject"])
        objects = {}
        pending = [go]
        while pending:
            obj = pending.pop()
            if key(obj) in objects:
                continue
            objects[key(obj)] = obj
            for entry in obj.read().m_Component:
                component = entry.component.deref() if hasattr(entry, "component") else entry[1].deref()
                objects[key(component)] = component
                if component.type.name == "Transform":
                    for child in component.read().m_Children:
                        pending.append(child.read().m_GameObject.deref())
        local = {identity: obj.path_id for identity, obj in objects.items()}
        parts = []
        for obj in objects.values():
            data = schemas.read(obj) if obj.type.name == "MonoBehaviour" else obj.read_typetree(check_read=True)
            if obj.type.name == "Transform" and resolve(obj, data["m_GameObject"]).path_id == go.path_id:
                data["m_Father"] = dict(m_FileID=0, m_PathID=0)
            data = convert(data, obj, local)
            data.update(source_header())
            if obj.type.name in ("GameObject", "Transform"):
                data["serializedVersion"] = 6 if obj.type.name == "GameObject" else 2
            parts.append("--- !u!" + str(obj.type.value) + " &" + str(obj.path_id) + "\n" +
                yaml.dump({obj.type.name: data}, Dumper=Dumper, sort_keys=False, allow_unicode=True))
            proof["objects"].append(dict(mapClassId=map_id, identity=key(obj), type=obj.type.name,
                source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
        target = DEST / f"Resources/World/Mainline/Fog/Map{map_id}.prefab"
        write(target, ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "".join(parts)).encode())
        meta(target, "PrefabImporter")
        proof["maps"].append(dict(mapClassId=map_id, cloudEnabled=True, root=apk.provenance(root), root_sha256=sha(root.get_raw_data()),
            prefab=target.relative_to(ROOT).as_posix(), object_count=len(objects), diffusion_speed=fields["DiffusionSpeed"]))
    for directory in [DEST.parent, DEST, *sorted(p for p in DEST.rglob("*") if p.is_dir())]:
        meta(directory, "DefaultImporter", folder=True)
    proof["outputs"] = [record(p) for p in sorted(DEST.rglob("*")) if p.is_file()]
    proof["operation"] = "Extract the exact original map-root cloud GameObject subtree and component order; detach only its outer parent. Restore all pointer dependencies, native Sprite geometry, ASTC mip chains, original material properties/pass programs and explicit migrated CloudController2 MonoScript binding. Current resources and historical code identity are recorded separately."
    proof["tool"] = record(Path(__file__).resolve())
    proof["conversion_tools"] = [record(ROOT / "scripts" / name) for name in
        ("official_apk.py", "official_unity_schema.py", "prepare_unity_map13_native_scene.py",
         "prepare_unity_map13_native_sprite_yaml.py", "prepare_unity_map13_water_shaders.py")]
    receipt = ROOT / "assets/reference/official-map/mainline-grid-fog-source-manifest.json"
    write(receipt, encode(proof))
    apk.archive.close()
    print("Original mainline fog subtrees:", len(map_ids), "maps;", len(proof["objects"]), "source objects", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", type=int, nargs="+", default=[13])
    build(parser.parse_args().maps)
