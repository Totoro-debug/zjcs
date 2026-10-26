"""Prepare verified Map13 native converter inputs with restored schemas.

Adds stripped serialization descriptions; every original object payload must
remain byte-identical. These Android inputs are not Windows runtime assets.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas, read
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from UnityPy.helpers.Tpk import get_typetree_node
from UnityPy.helpers import TypeTreeHelper
import UnityPy


ROOTS = ["defaultpackage_assets_partition_mapground_13_" + suffix + ".bundle"
         for suffix in ("mapground_13_bare", "prefabs", "chunks", "extensions", "prefabs_n")]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/native-components.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-input-source-manifest.json"


def converter_references(tree, value, schemas):
    registry = value.get("references")
    if not registry:
        return tree
    entries = registry["RefIds"]
    identities = {(e["type"]["class"], e["type"]["ns"], e["type"]["asm"]) for e in entries}
    if registry["version"] != 2 or identities != {("StateGameObjectConfig", "", "Framework")}:
        raise ValueError("Recheck the original managed-reference registry before converter flattening")
    # AssetRipper rejects SerializeReference even when the original schema is
    # restored. Describe these same bytes as ordinary structs for conversion;
    # retain every field name/rid/type/data so exported Unity YAML is unchanged.
    renames = dict(managedReference="Map13ReferenceId", ManagedReferencesRegistry="Map13ReferenceRegistry",
                   ReferencedObject="Map13ReferenceRecord", ReferencedManagedType="Map13ReferenceType")
    for node in list(tree.traverse()):
        if node.m_Type in renames:
            node.m_Type = renames[node.m_Type]
        elif node.m_Type == "ReferencedObjectData":
            target = schemas.tree("Framework.dll", "StateGameObjectConfig")
            node.m_Type = "StateGameObjectConfig"
            node.m_Children = [c for c in target.m_Children if c.m_Name not in
                ("m_GameObject", "m_Enabled", "m_Script", "m_Name")]
            for child in node.m_Children:
                for item in child.traverse(): item.m_Level += node.m_Level
    return tree


def hydrate(bundle, schemas):
    components, originals, tree_records = [], {}, []
    for file in serialized_files(bundle):
        originals[file.name] = {obj.path_id: sha(obj.get_raw_data()) for obj in file.objects.values()}
        pending, reference_names = [], set()
        for obj in file.objects.values():
            if obj.type.name != "MonoBehaviour":
                continue
            script = obj.read(check_read=False).m_Script.read()
            name = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
            value, tree = read(schemas, obj)
            tree = converter_references(tree, value, schemas)
            def references(value):
                if isinstance(value, dict):
                    typ = value.get("type")
                    if isinstance(typ, dict) and set(typ) == {"class", "ns", "asm"} and typ["class"]:
                        reference_names.add((typ["class"], typ["ns"], typ["asm"]))
                    for child in value.values(): references(child)
                elif isinstance(value, list):
                    for child in value: references(child)
            references(value)
            sizes = {"bool": 1, "UInt8": 1, "SInt8": 1, "char": 1, "SInt16": 2, "UInt16": 2,
                     "SInt32": 4, "UInt32": 4, "int": 4, "float": 4, "SInt64": 8, "UInt64": 8, "double": 8}
            for index, node in enumerate(tree.traverse()):
                node.m_Index = index
                node.m_Version = max(node.m_Version, 1)
                node.m_TypeFlags = node.m_TypeFlags or 0
                node.m_RefTypeHash = node.m_RefTypeHash or 0
                node.m_MetaFlag = node.m_MetaFlag or 0
                if node.m_ByteSize == 0:
                    node.m_ByteSize = sizes.get(node.m_Type, -1)
                if node.m_Type == "Array":
                    node.m_TypeFlags = 1
            pending.append((obj.serialized_type, tree))
            components.append(dict(cab=file.name, path_id=str(obj.path_id), assembly=script.m_AssemblyName,
                                   type=name, payload_sha256=sha(obj.get_raw_data()), fields=value))
            tree_records.append(dict(type=name, schema=[dict(level=node.m_Level, type=node.m_Type,
                name=node.m_Name, byte_size=node.m_ByteSize, version=node.m_Version,
                type_flags=node.m_TypeFlags, flags=node.m_MetaFlag) for node in tree.traverse()]))
        for typ, tree in pending:
            typ.node = tree
        for typ in file.types:
            if typ.node is None:
                if typ.class_id == 114:
                    raise ValueError("A stripped MonoBehaviour schema was not restored")
                typ.node = get_typetree_node(typ.class_id, file.version)
            if typ.type_dependencies is None:
                typ.type_dependencies = ()
        for typ in file.ref_types or []:
            if typ.node is None:
                if not typ.m_ClassName:
                    if len(file.ref_types) != 1 or len(reference_names) != 1:
                        raise ValueError("Cannot uniquely restore stripped managed-reference type identities")
                    typ.m_ClassName, typ.m_NameSpace, typ.m_AssemblyName = next(iter(reference_names))
                name = (typ.m_NameSpace + "." if typ.m_NameSpace else "") + typ.m_ClassName
                typ.node = schemas.tree(typ.m_AssemblyName + ".dll", name)
                typ.node.m_Children = [c for c in typ.node.m_Children if c.m_Name not in
                    ("m_GameObject", "m_Enabled", "m_Script", "m_Name")]
        for typ in [*file.types, *(file.ref_types or [])]:
            for index, node in enumerate(typ.node.traverse()):
                node.m_Index = index
                node.m_TypeFlags = node.m_TypeFlags or 0
                node.m_RefTypeHash = node.m_RefTypeHash or 0
                node.m_MetaFlag = node.m_MetaFlag or 0
        file._enable_type_tree = True
    raw = bundle.save(packer="lz4")
    rebuilt = UnityPy.load(raw)
    for cab, file in bundle.environment.cabs.items():
        if cab not in rebuilt.cabs:
            rebuilt.register_cab(cab, file)
    restored = {file.name: {obj.path_id: sha(obj.get_raw_data()) for obj in file.objects.values()}
                for file in serialized_files(next(iter(rebuilt.files.values())))}
    if restored != originals:
        changed = [(cab, key) for cab, values in originals.items() for key, value in values.items()
                   if restored.get(cab, {}).get(key) != value]
        raise ValueError("Schema restoration modified original object payload bytes: " + str(changed[:5]))
    # Re-read each restored schema independently, including every trailing byte.
    lookup = {(c["cab"], c["path_id"]): c["fields"] for c in components}
    original_resolver, original_boost = TypeTreeHelper.get_ref_type_node, TypeTreeHelper.read_typetree_boost
    def resolve(reference, assetsfile):
        typ = reference["type"]
        if not typ["class"]: return None
        name = (typ["ns"] + "." if typ["ns"] else "") + typ["class"]
        tree = schemas.tree(typ["asm"] + ".dll", name)
        tree.m_Children = [c for c in tree.m_Children if c.m_Name not in ("m_GameObject", "m_Enabled", "m_Script", "m_Name")]
        return tree
    try:
        TypeTreeHelper.get_ref_type_node = resolve
        TypeTreeHelper.read_typetree_boost = None
        for obj in rebuilt.objects:
            if obj.type.name == "MonoBehaviour":
                value = obj.read_typetree()
                if value != lookup[(obj.assets_file.name, str(obj.path_id))]:
                    raise ValueError("Embedded schema changed decoded component fields")
    finally:
        TypeTreeHelper.get_ref_type_node = original_resolver
        TypeTreeHelper.read_typetree_boost = original_boost
    return raw, components, tree_records, sum(map(len, originals.values()))


def build(destination):
    destination = destination.resolve()
    if not destination.is_relative_to(ROOT / "output") or destination == ROOT / "output":
        raise ValueError("Converter inputs must stay in this repository's output directory")
    if destination.exists():
        raise FileExistsError("Refusing to overwrite an existing converter input directory")
    apk = Apk.current()
    assets, missing = collect(apk, ROOTS)
    if missing:
        raise ValueError("Map13 CAB dependency closure is incomplete: " + str(missing))
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_manifest = json.loads((code.parent / "code-manifest.json").read_text())
    if code_manifest["manifest"] != apk.version or code_manifest["apk_sha256"] != apk.source.apk_sha256:
        raise ValueError("Native schemas do not belong to the selected official version")
    assemblies = []
    for name, record in code_manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != record["decoded_sha256"]:
            raise ValueError("Official schema assembly differs from its receipt: " + name)
        assemblies.append(dict(path=source_path(path), sha256=record["decoded_sha256"]))
    schemas = NativeSchemas(code)
    destination.mkdir(parents=True)
    inputs, components, trees, object_count = [], [], {}, 0
    for name in sorted(apk.loaded):
        decoded, record = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            # APK built-in resources already contain native schemas. Keep the
            # entire serialized file unchanged, including its version/header.
            content, fields, descriptions = decoded, [], []
            count = sum(len(f.objects) for f in serialized_files(apk.loaded[name]))
        else:
            content, fields, descriptions, count = hydrate(apk.loaded[name], schemas)
        components.extend(fields)
        object_count += count
        for description in descriptions:
            key = encode(description)
            trees[key] = description
        record["output"] = Path(name).name
        record["converter_input_sha256"] = sha(content)
        record["converter_input_bytes"] = len(content)
        write(destination / record["output"], content)
        inputs.append(record)
        print("Restored", name, len(fields), "components,", count, "unchanged objects", flush=True)
    builtin = "assets/bin/Data/unity default resources"
    builtin_raw = apk.archive.read(builtin)
    builtin_input = next((r for r in inputs if r["name"] == builtin), None)
    if builtin_input and builtin_input["converter_input_sha256"] != sha(builtin_raw):
        raise ValueError("Built-in resources were modified")
    if not builtin_input:
        write(destination / Path(builtin).name, builtin_raw)
    write(OUTPUT, encode(dict(manifest=apk.version, mapClassId=13, components=components)))
    meta(OUTPUT)
    files = [Path(__file__), ROOT / "scripts/map13_native_schemas.py",
             ROOT / "scripts/official_unity_schema.py", ROOT / "scripts/unity_schema_types.cs"]
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    result = dict(format=1, manifest=apk.version, mapClassId=13, roots=ROOTS, inputs=inputs,
        builtins=[dict(apk_entry=builtin, apk_sha256=apk.source.apk_sha256, sha256=sha(builtin_raw))],
        serialized_files=list(assets.values()), schemas=list(trees.values()),
        metadata=dict(path=source_path(metadata), sha256=sha(metadata.read_bytes())),
        code_manifest=dict(path=source_path(code.parent / "code-manifest.json"),
                           sha256=sha((code.parent / "code-manifest.json").read_bytes())),
        assemblies=assemblies,
        reference_assemblies=[dict(path=path.as_posix(), sha256=sha(path.read_bytes()))
                              for path in schemas.reference_inputs],
        tools=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in files],
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        counts=dict(bundles=len(inputs), restored_components=len(components), unchanged_objects=object_count),
        operation="Restore stripped type trees throughout the complete CAB dependency closure from selected official assemblies, compatible Unity/TMP/UGUI reference assemblies and pinned APK native metadata. Describe the uniquely verified StateGameObjectConfig reference registries as ordinary converter structs to avoid AssetRipper's unsupported SerializeReference path; retain original field names, IDs, type identities and data. Consume every component byte and compare decoded values as well as every object payload byte before/after rewriting. Preserve Unity version, Android target, CAB identities and object pointers.",
        limitations=["Converter inputs only. Native C# bindings, shader translation, Windows import and rendered gameplay remain required."])
    write(RECEIPT, encode(result))
    write(destination / "source-manifest.json", encode(result))
    apk.archive.close()
    print("Map13 native converter inputs:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, required=True)
    build(parser.parse_args().inputs)
