"""Capture the original Map13 WorldCamera hierarchy and current camera settings."""
import argparse
import json
from unittest.mock import patch
import map13_native_schemas as native_schema_module
import map13_entity_native_schemas as entity_schema_module
from pathlib import Path
from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_map13_story_playables import serializable
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/world-camera.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-world-camera-source-manifest.json"


def record(path): return dict(path=source_path(path) if path.is_relative_to(ROOT) else path.as_posix(), sha256=sha(path.read_bytes()))


def build(script_assemblies=None):
    apk = Apk.current()
    if apk.version != "871_156598": raise ValueError("Unexpected official WorldCamera version")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    base_receipt = ROOT / "assets/reference/official-map/map13-source-manifest.json"
    base = json.loads(base_receipt.read_bytes())
    if base["manifest"] != apk.version: raise ValueError("Original camera assembly source version differs")
    assemblies = []
    for assembly in base["assemblies"].values():
        path = ROOT / assembly["path"]
        if sha(path.read_bytes()) != assembly["decoded_sha256"]: raise ValueError("Pinned official camera assembly differs")
        assemblies.append(record(path))
    tools = [Path(__file__).resolve()] + [ROOT / "scripts" / name for name in (
        "map13_native_schemas.py", "map13_entity_native_schemas.py", "official_unity_schema.py", "prepare_unity_map13_story_playables.py",
        "official_apk.py", "official_manifest.py", "official_metadata.py", "read_official_native.py", "official_managed_reference_conversion.py",
        "unity_schema_types.cs", "prepare_unity_map13_sources.py")]
    tool_records = [record(p) for p in tools]
    if script_assemblies is None:
        schemas = NativeSchemas(code); schema_proof = install(schemas, code)
    else:
        assembly_folder = Path(script_assemblies).resolve()
        for name in ("Unity.TextMeshPro", "UnityEngine.UI", "Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime"):
            if not (assembly_folder / (name + ".dll")).is_file(): raise FileNotFoundError("Required Unity reference DLL is unavailable: " + name)
        class ReferenceRoot:
            def __truediv__(self, relative):
                if relative == "unity/Library/ScriptAssemblies": return assembly_folder
                if relative == "unity": return assembly_folder.parent.parent
                return ROOT / relative
        # Isolated imports have the same pinned Unity/package types. Redirect
        # reference-file locations only; the shared decoder stays unchanged.
        reference_root = ReferenceRoot()
        with patch.object(native_schema_module, "ROOT", reference_root), patch.object(entity_schema_module, "ROOT", reference_root):
            schemas = NativeSchemas(code); schema_proof = install(schemas, code)
    objects = {}
    def capture(obj):
        key = obj.assets_file.name.lower() + ":" + str(obj.path_id)
        if key not in objects:
            if obj.type.name == "MonoBehaviour":
                script = obj.read(check_read=False).m_Script.read()
                kind = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
                fields, _ = schemas.read(obj)
            else:
                kind, fields = obj.type.name, obj.read_typetree()
            objects[key] = dict(identity=key, type=kind, source=apk.provenance(obj),
                                object_sha256=sha(obj.get_raw_data()), fields=serializable(fields))
        return key
    def hierarchy(obj):
        go = obj.read(); components, children = [], []
        for item in go.m_Component:
            component = item.component.deref(); components.append(capture(component))
            if component.type.name in ("Transform", "RectTransform"):
                children = [hierarchy(p.read().m_GameObject.deref()) for p in component.read().m_Children]
        return dict(object=capture(obj), components=components, children=children)
    root = apk.asset("prefabs_map_common_entityui", "/WorldCamera.prefab")
    tree = hierarchy(root)
    nodes = []
    def flatten(node, parent=""):
        path = parent + "/" + objects[node["object"]]["fields"]["m_Name"]
        nodes.append(dict(path=path, object=node["object"], components=node["components"]))
        for child in node["children"]: flatten(child, path)
    flatten(tree)
    settings = apk.asset("defaultpackage_assets_scriptableconfig_modules_camera.bundle", "/CameraControlSettings.asset")
    settings_key = capture(settings)
    # The legacy disabled followVCam and the current CameraControlBrain/freeVCam
    # are both preserved. Do not infer the active controller from prefab fields.
    data = dict(manifest=apk.version, mapClassId=13, prefab=dict(source=apk.provenance(root), hierarchy=tree),
                settings=settings_key, nodes=nodes, objects=list(objects.values()), schemaProof=schema_proof)
    write(OUTPUT, encode(data)); meta(OUTPUT)
    code_paths = [code / "client-source" / (name + ".cs") for name in (
        "WorldCameraIniter", "MapCameraManager", "CameraControlBrain", "CameraBaseController", "CameraMonoProxy",
        "WorldCameraBaseController", "WorldCameraFollowController", "WorldCameraDragController", "WorldCameraComplexController",
        "WorldCameraLookAheadController", "FollowCameraController", "OrthographicCameraResolutionAdaptor", "CameraControlUtils",
        "IgnoreUIDragController", "DragControllerBase", "DragControllerData")]
    code_paths += [code / "common-source" / (name + ".cs") for name in (
        "CameraControlSettings", "CameraBaseSetting", "WorldCameraBaseSetting", "WorldCameraControlSetting", "WorldCameraComplexSetting", "WorldCameraLookAheadSettings")]
    unresolved = [source_path(p) for p in code_paths if not p.is_file()]
    if unresolved: raise FileNotFoundError("Required original camera code has not been located: " + repr(unresolved))
    for path, original in zip(tools, tool_records):
        if sha(path.read_bytes()) != original["sha256"]: raise ValueError("Camera extraction source changed during conversion: " + str(path))
    counts = dict(objects=len(objects), gameObjects=sum(o["type"] == "GameObject" for o in objects.values()), prefabComponents=sum(o["type"] != "GameObject" for o in objects.values()) - 1, configurationObjects=1)
    proof = dict(manifest=apk.version, mapClassId=13, operation="Strictly decode the complete original WorldCamera prefab and CameraControlSettings with original hierarchy, component order, source pointers and raw object hashes. Both legacy and current controller paths are retained; native prefab/controller/Windows input and gameplay binding remain required.",
                 prefab=apk.provenance(root), settings=apk.provenance(settings), schemaProof=schema_proof,
                 inputs=[record(base_receipt)], assemblies=assemblies, referenceAssemblies=[record(p) for p in schemas.reference_inputs], officialCode=[record(p) for p in code_paths], tools=tool_records, outputs=[record(OUTPUT), record(Path(str(OUTPUT) + ".meta"))], counts=counts)
    write(RECEIPT, encode(proof)); apk.archive.close(); print("Original WorldCamera source:", counts, flush=True)
    return data


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--script-assemblies", type=Path, help="Use the matching pinned Unity reference DLLs from an isolated import")
    args = parser.parse_args()
    build(args.script_assemblies)
