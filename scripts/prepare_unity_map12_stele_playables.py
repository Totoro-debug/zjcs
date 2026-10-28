"""Restore the four exact original Map12 stele performance prefab closures."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
import UnityPy
from UnityPy.classes import PPtr
from official_native_yaml_verification import documents, element, float_bits, unpack_array
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map12_stele_playable_inputs import OUTPUT, RECEIPT as INPUT_RECEIPT, record
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_water_entity_export import convert
from validation_workspace import validation_directory

DESTINATION = ROOT / "unity/Assets/Official/World/Map/12/NativeStelePlayables"
RECEIPT = ROOT / "assets/reference/official-map/map12-native-stele-playables-source-manifest.json"
SHADERS = ROOT / "assets/reference/official-map/map14-entity-shaders-source-manifest.json"
SHADER_BINDINGS = SHADERS.with_name("map14-entity-shaders-binding-source-manifest.json")
TEXTURES = ROOT / "assets/reference/official-map/map14-native-entity-textures-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")


def shape(node):
    kind = {"unsigned int": "UInt32", "unsigned short": "UInt16",
            "unsigned char": "UInt8"}.get(node.m_Type, node.m_Type)
    children = {child.m_Name: shape(child) for child in node.m_Children}
    if kind == "TypelessData":
        children = dict(Array=dict(type="Array", children=children))
    return dict(type=kind, name=node.m_Name, children=children)


def build(stage_dir):
    stage_dir = stage_dir.resolve()
    validation = validation_directory()
    if stage_dir == validation or not stage_dir.is_relative_to(validation):
        raise ValueError("Use this session's original conversion directory")
    exported = stage_dir / "export/ExportedProject/Assets"
    inputs = json.loads(INPUT_RECEIPT.read_bytes())
    source = json.loads(OUTPUT.read_bytes())
    if inputs["manifest"] != "871_156598" or source["manifest"] != inputs["manifest"]:
        raise ValueError("Wrong original stele performance version")
    for entry in inputs["tools"] + inputs["outputs"] + inputs["selection"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original stele input receipt is stale: " + entry["path"])
    environment = UnityPy.Environment()
    for entry in inputs["inputs"]:
        path = stage_dir / "inputs" / entry["output"]
        if sha(path.read_bytes()) != entry["converter_input_sha256"]:
            raise ValueError("Original stele converter input changed")
        environment.load_file(str(path))
    originals = {(obj.assets_file.name.casefold(), str(obj.path_id)): obj
        for bundle in environment.files.values() for file in serialized_files(bundle) for obj in file.objects.values()}
    index = {}
    for path in exported.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Ambiguous native stele export GUID")
            index[match[1]] = path.with_suffix("")
    parsed = {}
    def docs(path):
        if path not in parsed:
            parsed[path] = documents(path.read_text(encoding="utf8"))
        return parsed[path]
    def identity(obj):
        return obj.assets_file.name.casefold(), str(obj.path_id)
    def guid(path):
        match = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))
        if match is None:
            raise ValueError("Native stele dependency has no GUID: " + str(path))
        return match[1]
    def script_type(fields):
        return index[fields["m_Script"]["guid"]].stem

    bindings, roots, counts, runtime_defaults, builtins = {}, [], Counter(), [], []
    def bind(obj, path, file_id):
        native_kind, native_fields = docs(path)[file_id]
        original = obj.read()
        if native_kind != "GameObject" or native_fields["m_Name"] != original.m_Name:
            raise ValueError("Original stele hierarchy node differs")
        if len(original.m_Component) != len(native_fields["m_Component"]):
            raise ValueError("Original stele component count differs")
        bindings[identity(obj)] = path, file_id
        for entry, native in zip(original.m_Component, native_fields["m_Component"]):
            component = entry.component.deref()
            target_id = native["component"]["fileID"]
            kind, fields = docs(path)[target_id]
            original_kind = component.type.name
            if original_kind == "MonoBehaviour":
                original_kind = component.read(check_read=False).m_Script.read().m_ClassName
            if (script_type(fields) if kind == "MonoBehaviour" else kind) != original_kind:
                raise ValueError("Original stele component order/type differs")
            bindings[identity(component)] = path, target_id
            if component.type.name == "Transform":
                original_transform, native_transform = component.read(), fields
        if len(original_transform.m_Children) != len(native_transform["m_Children"]):
            raise ValueError("Original stele child count differs")
        for child, pointer in zip(original_transform.m_Children, native_transform["m_Children"]):
            child_go = docs(path)[pointer["fileID"]][1]["m_GameObject"]["fileID"]
            bind(child.read().m_GameObject.deref(), path, child_go)
    for prefab in source["prefabs"]:
        obj = originals[prefab["cab"].casefold(), prefab["path_id"]]
        if sha(obj.get_raw_data()) != prefab["object_raw_sha256"]:
            raise ValueError("Original stele performance root changed")
        candidates = list(exported.rglob(prefab["name"] + ".prefab"))
        if len(candidates) != 1:
            raise ValueError("Original stele native prefab is not unique")
        path = candidates[0]
        root_ids = [file_id for file_id, (kind, fields) in docs(path).items()
            if kind == "GameObject" and fields["m_Name"] == prefab["name"]
            and docs(path)[fields["m_Component"][0]["component"]["fileID"]][1]["m_Father"]["fileID"] == 0]
        if len(root_ids) != 1:
            raise ValueError("Original stele native root is ambiguous")
        bind(obj, path, root_ids[0]); roots.append((prefab, path, root_ids[0]))

    dependencies, pending, seen = set(), list(bindings), set()
    # Source identities remain tied to actual original pointers throughout
    # validation. Exporter names alone never establish source provenance.
    def compare(expected, actual, owner, path, schema, label):
        if isinstance(expected, dict) and set(expected) == {"m_FileID", "m_PathID"}:
            counts["pointers"] += 1
            if not expected["m_PathID"]:
                if actual.get("fileID", 0) != 0:
                    raise ValueError("Original null pointer changed: " + label)
                return
            target = PPtr(**expected, assetsfile=owner.assets_file).deref()
            if actual.get("guid") == "0000000000000000e000000000000000":
                if (target.assets_file.name != "unity default resources"
                        or actual["fileID"] != target.path_id):
                    raise ValueError("Original built-in stele pointer differs: " + label + " " + repr((identity(target), actual)))
                builtins.append(dict(source=identity(target), name=target.peek_name(), pointer=actual))
                return
            destination = index[actual["guid"]] if actual.get("guid") else path
            key, pair = identity(target), (destination, actual["fileID"])
            if key in bindings:
                if bindings[key] != pair:
                    raise ValueError("Original stele pointer identity changed: " + label)
            else:
                if not destination.is_file() or actual["fileID"] == 0:
                    raise ValueError("Unresolved original stele dependency")
                if target.type.name == "MonoScript":
                    if destination.stem != target.read().m_ClassName:
                        raise ValueError("Original stele script type differs")
                elif target.type.name == "Texture2D":
                    if destination.suffix != ".png" or destination.stem != target.peek_name():
                        raise ValueError("Inspect original stele texture export identity")
                elif target.type.name == "Shader":
                    if destination.suffix != ".shader":
                        raise ValueError("Inspect original stele shader export identity")
                else:
                    kind, fields = docs(destination)[actual["fileID"]]
                    if kind != target.type.name or fields.get("m_Name") != target.peek_name():
                        raise ValueError("Original stele external object identity differs: " + label)
                bindings[key] = pair; pending.append(key)
            dependencies.add(destination)
            return
        if isinstance(expected, dict):
            if not isinstance(actual, dict):
                raise ValueError("Original stele field shape differs: " + label)
            for field, value in expected.items():
                if field not in actual:
                    raise ValueError("Original stele field missing: " + label + "." + field)
                field_schema = schema["children"][field]
                if field_schema["type"] == "TypelessData" and "_typelessdata" in actual:
                    if actual[field] != len(value):
                        raise ValueError("Original typeless stele byte count differs: " + label)
                    compare(value, actual["_typelessdata"], owner, path, field_schema, label + "._typelessdata")
                else:
                    compare(value, actual[field], owner, path, field_schema, label + "." + field)
        elif isinstance(expected, (list, tuple, bytes, bytearray)):
            if schema["type"] == "pair":
                if not isinstance(actual, dict) or len(actual) != 1 or len(expected) != 2:
                    raise ValueError("Original stele map pair differs: " + label)
                key, value = next(iter(actual.items()))
                compare(expected[0], key, owner, path, schema["children"]["first"], label + ".first")
                compare(expected[1], value, owner, path, schema["children"]["second"], label + ".second")
                return
            if schema["type"] == "map" and isinstance(actual, dict):
                actual = [{key: value} for key, value in actual.items()]
            if not isinstance(actual, list):
                try:
                    actual = unpack_array(actual, schema, len(expected))
                except (KeyError, ValueError) as error:
                    raise ValueError("Inspect original packed stele array: " + label + " " + repr((schema, actual))[:1000]) from error
            if len(expected) != len(actual):
                raise ValueError("Original stele array count differs: " + label)
            for i, (left, right) in enumerate(zip(expected, actual)):
                compare(left, right, owner, path, element(schema), label + "[" + str(i) + "]")
        elif isinstance(expected, float):
            if float_bits(expected, schema) != float_bits(float(getattr(actual, "scalar", actual)), schema):
                raise ValueError("Original stele float bits differ: " + label + " " + repr((expected, actual)))
        elif expected != ("" if actual is None and expected == "" else actual):
            raise ValueError("Original stele value differs: " + label + " " + repr((expected, actual)))
        counts["values"] += 1

    while pending:
        key = pending.pop()
        if key in seen:
            continue
        seen.add(key)
        obj, (path, file_id) = originals[key], bindings[key]
        kind = obj.type.name
        if kind in ("MonoScript", "Texture2D", "Shader"):
            continue
        fields, native = obj.read_typetree(), docs(path)[file_id][1]
        tree = shape(obj.serialized_type.node)
        if kind == "GameObject":
            for field in ("m_Name", "m_Layer", "m_IsActive"):
                if fields[field] != native[field]:
                    raise ValueError("Original stele GameObject state differs")
            if fields["m_Tag"] != 0 or native["m_TagString"] != "Untagged":
                raise ValueError("Inspect original stele non-default tag")
        else:
            for field, value in fields.items():
                if kind == "ParticleSystemRenderer" and field not in native:
                    defaults = dict(m_LightmapIndex=65535, m_LightmapIndexDynamic=65535,
                        m_LightmapTilingOffset=dict(x=1.0, y=1.0, z=0.0, w=0.0),
                        m_LightmapTilingOffsetDynamic=dict(x=1.0, y=1.0, z=0.0, w=0.0))
                    if field not in defaults or value != defaults[field]:
                        raise ValueError("Restore original non-default runtime-only renderer field: " + field)
                    # Unity's editor renderer schema omits the player's baked
                    # lightmap indices/UVs. These are strictly unbaked defaults;
                    # the Editor validator must also check the loaded values.
                    runtime_defaults.append(dict(cab=key[0], path_id=key[1], field=field, value=value))
                    continue
                compare(value, native[field], obj, path, tree["children"][field], kind + "." + field)
        counts[kind] += 1
    if (counts["GameObject"], counts["Transform"], counts["ParticleSystem"],
            counts["ParticleSystemRenderer"], counts["MonoBehaviour"]) != (84, 84, 64, 72, 20):
        raise ValueError("Original stele hierarchy/particle coverage differs: " + repr(counts))
    print("Original stele native fields verified:", dict(counts), flush=True)

    shader_source = json.loads(SHADERS.read_bytes())
    shader_bindings = json.loads(SHADER_BINDINGS.read_bytes())
    texture_source = json.loads(TEXTURES.read_bytes())
    if any(r["manifest"] != inputs["manifest"] for r in (shader_source, shader_bindings, texture_source)):
        raise ValueError("Shared original native dependency version differs")
    replacements, shared, scripts, native_files, texture_guids = {}, [], [], [], set()
    for key, (path, file_id) in bindings.items():
        obj = originals[key]
        kind = obj.type.name
        if kind == "Shader":
            candidates = [r for r in shader_source["inputs"] + shader_source["reused"]
                if (r["original"]["cab"].casefold(), r["original"]["path_id"]) == key]
            if not candidates or any(r["original"]["payload_sha256"] != sha(obj.get_raw_data()) for r in candidates):
                raise ValueError("Stele shader has no exact verified original Windows source: " + repr((key,
                    obj.read().m_ParsedForm.m_Name, sha(obj.get_raw_data()), [r["original"] for r in candidates])))
            candidates = [r for r in shader_bindings["bindings"]
                if r["original_shader"] == obj.read().m_ParsedForm.m_Name and not r["engine"]]
            if len(candidates) != 1:
                raise ValueError("Stele shader has no unique original Windows binding")
            row = candidates[0]; target = ROOT / row["path"]
            if sha(target.read_bytes()) != row["sha256"] or sha(Path(str(target) + ".meta").read_bytes()) != row["meta_sha256"]:
                raise ValueError("Shared stele Windows shader binding changed")
            if guid(target) != row["guid"] or file_id != row["file_id"]:
                raise ValueError("Shared stele Windows shader identity differs")
            replacements[guid(path)] = row["guid"]; shared.append(dict(source=key, target=record(target)))
        elif kind == "Texture2D":
            candidates = [r for r in texture_source["textures"] if (r["cab"].casefold(), r["path_id"]) == key]
            if len(candidates) != 1 or candidates[0]["object_sha256"] != sha(obj.get_raw_data()):
                raise ValueError("Stele texture has no exact original native mip conversion: " + repr(key))
            row = candidates[0]; target = ROOT / row["path"]
            expected = next(r for r in texture_source["outputs"] if r["path"] == row["path"])
            if sha(target.read_bytes()) != expected["sha256"] or sha(Path(str(target) + ".meta").read_bytes()) != expected["meta_sha256"]:
                raise ValueError("Shared stele native texture conversion changed")
            if file_id != 2800000:
                raise ValueError("Inspect stele texture file ID conversion")
            replacements[guid(path)] = guid(target); texture_guids.add(guid(target))
            shared.append(dict(source=key, target=record(target)))
        elif kind == "MonoScript":
            name = obj.read().m_ClassName
            candidates = [p for folder in ("StoryPlayables", "StoryAttachment")
                for p in (ROOT / "unity/Assets/Game/Runtime/Official" / folder).rglob(name + ".cs")]
            if len(candidates) != 1:
                raise ValueError("Original stele runtime script has not been migrated: " + name)
            target = candidates[0]
            replacements[guid(path)] = guid(target)
            scripts.append(dict(source=key, original=record(path), target=record(target), meta=record(Path(str(target) + ".meta"))))
        else:
            if path not in native_files:
                native_files.append(path)
                replacements[guid(path)] = hashlib.md5(("zjcs-map12-original-stele-playable:" + path.relative_to(exported).as_posix()).encode()).hexdigest()
    outputs, templates = [], []
    for path in sorted(native_files):
        text = path.read_text(encoding="utf8")
        for value in REFERENCE.findall(text):
            if value == "0000000000000000e000000000000000":
                continue
            if value not in replacements:
                raise ValueError("Unverified original stele native reference: " + value)
        target = DESTINATION / path.relative_to(exported)
        text = REFERENCE.sub(lambda match: "guid: " + replacements.get(match[1], match[1]), text)
        for texture_guid in texture_guids:
            text = re.sub(r"(\{fileID: 2800000, guid: " + texture_guid + r", type: )3(\})", r"\g<1>2\2", text)
        write(target, text.encode())
        metadata = Path(str(path) + ".meta")
        write(Path(str(target) + ".meta"), GUID.sub(lambda match: "guid: " + replacements[guid(path)], metadata.read_text(encoding="utf8")).encode())
        outputs.extend((target, Path(str(target) + ".meta"))); templates.extend((record(path), record(metadata)))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]:
        meta(folder, folder=True); outputs.append(Path(str(folder) + ".meta"))
    catalog_class = ROOT / "unity/Assets/Game/Runtime/Official/StelePlayables/OfficialStelePlayableCatalog.cs"
    catalog = ROOT / "unity/Assets/Game/Resources/Map12/Native/stele-playables-catalog.asset"
    catalog_text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: " + guid(catalog_class) + ", type: 3}\n"
        "  m_Name: stele-playables-catalog\n  m_EditorClassIdentifier:\n  entries:\n")
    for entity_class_id in (6650, 6655):
        catalog_text += "  - entityClassId: " + str(entity_class_id) + "\n"
        for field in ("mCanInteractionPlayable", "mEndInteractionPlayable"):
            prefab, path, file_id = next(r for r in roots if r[0]["entityClassId"] == entity_class_id and r[0]["field"] == field)
            catalog_text += "    " + field + ": {fileID: " + str(file_id) + ", guid: " + replacements[guid(path)] + ", type: 3}\n"
    write(catalog, catalog_text.encode())
    catalog_guid = hashlib.md5(b"zjcs-map12-original-stele-playable-catalog").hexdigest()
    write(Path(str(catalog) + ".meta"), ("fileFormatVersion: 2\nguid: " + catalog_guid +
        "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
    outputs.extend((catalog, Path(str(catalog) + ".meta")))
    adapter_paths = []
    for folder in (ROOT / "unity/Assets/Game/Runtime/Official/StelePlayables",
                   ROOT / "unity/Assets/Game/Editor/Map12StelePlayables"):
        adapter_paths.extend(p for p in folder.rglob("*") if p.is_file())
        adapter_paths.append(Path(str(folder) + ".meta"))
    result = dict(format=1, manifest=inputs["manifest"], mapClassId=12,
        inputs=[record(INPUT_RECEIPT), record(OUTPUT)],
        original_code=record(ROOT / ".tools/source-packages/current/871_156598/official-code/client-source/ECPeriodicityInteractionStateView.cs"),
        adapters=[record(p) for p in sorted(adapter_paths)], catalog=record(catalog),
        dependencies=[record(SHADERS), record(SHADER_BINDINGS), record(TEXTURES)],
        tools=[record(Path(__file__)), record(ROOT / "scripts/official_native_yaml_verification.py")],
        converter=record(stage_dir / "runner/AssetRipper.GUI.Free.exe"),
        settings=record(stage_dir / "runner/AssetRipper.Settings.json"), templates=templates,
        scripts=scripts, shared=shared, builtins=builtins, runtime_only_renderer_defaults=runtime_defaults,
        counts=dict(counts, prefabs=4, nativeFiles=len(native_files)),
        prefabs=[dict(**prefab, nativePath=source_path(DESTINATION / path.relative_to(exported)),
            guid=replacements[guid(path)], fileId=file_id) for prefab, path, file_id in roots],
        bindings=[dict(cab=key[0], path_id=key[1], type=originals[key].type.name,
            path=source_path(path), fileId=file_id) for key, (path, file_id) in bindings.items()],
        outputs=[record(p) for p in sorted(set(outputs))],
        operation="Bind four exact original roots recursively by GameObject, child and component order. Verify all original transforms, activation, layers, tags, MonoBehaviour fields, particle systems/renderers, material and mesh values using original type trees, exact float bits and native pointer identities. Preserve original YAML and local file IDs; remap GUIDs to migrated original scripts, verified Windows shader and complete original native texture mip chains, changing texture reference type from PNG importer to NativeFormatImporter. Verify the original unbaked defaults for player-only renderer lightmap fields omitted by the editor schema. Bind the four exact roots in a local Resources catalog. Port the original periodic-interaction view with local bridge/player/parser/play-manager interfaces while retaining refresh, event filtering, stop, transition and unload order. Exporter dummy shaders and placeholder scripts are never installed.",
        limitations=["Native conversion, explicit catalog and source/field checks only. Unity import and Editor validation, original entity attachment nodes, actual map dispatch, periodicity event integration, rendering, live completion and cold restore remain required; automatic runtime acceptance is paused by the user."])
    write(RECEIPT, encode(result))
    print("Original stele native closure restored:", len(native_files), "files;", len(shared), "shared original dependencies", flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage-dir", type=Path, required=True)
    parser.add_argument("--convert", action="store_true")
    args = parser.parse_args()
    if args.convert:
        convert(args.stage_dir.resolve())
    build(args.stage_dir)
