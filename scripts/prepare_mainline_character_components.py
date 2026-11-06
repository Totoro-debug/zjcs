"""Recover original character script identities and migrate its empty-info bindings."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT, UnityPy
from official_native_yaml_verification import documents
from prepare_unity_map13_sources import encode, write
from prepare_unity_story_sound_runtime import runtime_meta

NAMES = ("MyLevel", "MyProp", "MyMapRegionStatus", "MyMapStatusAgent")
RECEIPT = ROOT / "assets/reference/official-map/mainline-character-components-source-manifest.json"


def record(path):
    return {"path": path.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def recover(bundle):
    input_path = ROOT / "assets/reference/official-map/map14-native-entity-input-source-manifest.json"
    metadata_path = ROOT / "unity/Assets/Official/World/Map/14/Source/native-entity-components.json"
    source = json.loads(input_path.read_bytes())
    selected = next(row for row in source["selection"]["prefabs"]
                    if row["path"].endswith("/WorldMapCharacter/3001.prefab"))
    proof = selected["source"]
    raw = bundle.read_bytes()
    if (len(raw) != proof["bundle_size"] or hashlib.md5(raw).hexdigest() != proof["bundle_hash"]
            or hashlib.sha256(raw).hexdigest() != proof["bundle_sha256"]):
        raise ValueError("Character bundle differs from the historical prefab input; do not mix current bundles")
    environment = UnityPy.Environment()
    environment.path = str(bundle.parent)

    def resolve(name, *args, **kwargs):
        cab = environment.get_cab(name.rsplit("/", 1)[-1].lower())
        if cab is not None:
            return cab
        # All hierarchy objects must be in this recorded CAB. Never recursively
        # scan the repository or silently load a dependency from another version.
        raise FileNotFoundError("Unresolved original character CAB: " + name)

    environment.find_file = resolve
    environment.load_file(Apk.decode(raw), name=proof["bundle"])
    matches = [pointer for path, pointer in environment.container.items()
               if path.lower() == selected["path"].lower()]
    if len(matches) != 1:
        raise ValueError("Original character root is not unique")
    original = matches[0].deref()
    if (str(original.path_id) != str(proof["path_id"])
            or hashlib.sha256(original.get_raw_data()).hexdigest() != selected["object_sha256"]):
        raise ValueError("Original character root payload changed")
    prefab = ROOT / "unity/Assets/Official/World/Map/14/NativeEntityPrefabs/Prefabs/EcEntity/MapWorld/WorldMapCharacter/3001.prefab"
    export_receipt_path = ROOT / "assets/reference/official-map/map14-native-entity-export-source-manifest.json"
    export_receipt = json.loads(export_receipt_path.read_bytes())
    export_rows = [row for row in export_receipt["outputs"]
                   if row["input"].endswith("/" + selected["path"])]
    if (export_receipt["manifest"] != "871_156598" or len(export_rows) != 1
            or hashlib.sha256(prefab.read_bytes().replace(b"\r\n", b"\n")).hexdigest() != export_rows[0]["sha256"]):
        raise ValueError("Installed character differs from the verified original native export")
    exported = documents(prefab.read_text("utf8"))
    roots = [identity for identity, (kind, data) in exported.items()
             if kind == "GameObject" and str(data.get("m_Name")) == "3001"]
    if len(roots) != 1:
        raise ValueError("Exported character root is not unique")
    rows = json.loads(metadata_path.read_bytes())["components"]
    components = {(row["cab"], str(row["path_id"])): row for row in rows}
    identities = []
    visited_source, visited_export = set(), set()

    def visit(native, identity, hierarchy):
        key = native.assets_file.name, native.path_id
        if key in visited_source or identity in visited_export:
            raise ValueError("Character hierarchy repeats an object")
        visited_source.add(key)
        visited_export.add(identity)
        game_object = native.read()
        kind, data = exported[identity]
        if kind != "GameObject" or str(data["m_Name"]) != game_object.m_Name:
            raise ValueError("Character hierarchy name changed: " + hierarchy)
        if len(game_object.m_Component) != len(data["m_Component"]):
            raise ValueError("Character component count changed: " + hierarchy)
        native_transform = exported_transform = None
        for source_component, exported_component in zip(game_object.m_Component, data["m_Component"]):
            obj = source_component.component.deref()
            file_id = exported_component["component"]["fileID"]
            component_kind, fields = exported[file_id]
            if obj.type.name != component_kind:
                raise ValueError("Character component order/type changed: " + hierarchy)
            if component_kind == "MonoBehaviour":
                row = components[obj.assets_file.name, str(obj.path_id)]
                if hashlib.sha256(obj.get_raw_data()).hexdigest() != row["payload_sha256"]:
                    raise ValueError("Original character component payload changed: " + row["type"])
                identities.append(dict(hierarchy=hierarchy, type=row["type"], assembly=row["assembly"],
                                       source_cab=row["cab"], source_path_id=str(obj.path_id),
                                       export_file_id=str(file_id), export_script_guid=fields["m_Script"]["guid"]))
            if component_kind in ("Transform", "RectTransform"):
                native_transform, exported_transform = obj.read(), fields
        if native_transform is None or exported_transform is None:
            raise ValueError("Character transform is absent")
        if len(native_transform.m_Children) != len(exported_transform["m_Children"]):
            raise ValueError("Character child count changed: " + hierarchy)
        for child, target in zip(native_transform.m_Children, exported_transform["m_Children"]):
            child_object = child.deref().read().m_GameObject.deref()
            target_object = exported[target["fileID"]][1]["m_GameObject"]["fileID"]
            visit(child_object, target_object, hierarchy + "/" + child_object.read().m_Name)

    visit(original, roots[0], "3001")
    by_guid = {}
    for identity in identities:
        kind = identity["assembly"], identity["type"]
        previous = by_guid.setdefault(identity["export_script_guid"], kind)
        if previous != kind:
            raise ValueError("One exported script GUID resolves to different official types")
    return selected, identities, [input_path, metadata_path, prefab, export_receipt_path]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True,
                        help="Exact historical bundle recorded by the installed 3001 prefab input")
    args = parser.parse_args()
    selected, identities, inputs = recover(args.bundle)
    code = ROOT / ".tools/source-packages/official-code"
    code_receipt = ROOT / ".tools/source-packages/current/884_156692/code-manifest.json"
    code_proof = json.loads(code_receipt.read_bytes())
    assemblies = [code / "managed/decoded" / name for name in ("Assembly-CSharp.dll", "Console.dll")]
    if code_proof["manifest"] != "884_156692":
        raise ValueError("Character client source version differs")
    for assembly in assemblies:
        if record(assembly)["sha256"] != code_proof["assemblies"][assembly.name]["decoded_sha256"]:
            raise ValueError("Character client assembly differs: " + assembly.name)
    inputs.append(code_receipt)
    output = ROOT / "unity/Assets/Game/Runtime/Official/EntityClientComponents"
    outputs = []
    for name in NAMES:
        source = code / "client-source" / (name + ".cs")
        text = source.read_text("utf-8-sig")
        component = name.removeprefix("My") + "Component"
        expected = "using Common;\n\npublic class " + name + " : MyComponent<" + component + ">\n{\n}\n"
        if text != expected:
            raise ValueError("Review changed original client component before migration: " + name)
        text = text.replace("using Common;", "using Zjcs.Official.EntityLifecycle;\nnamespace Zjcs.Official.EntityClientComponents\n{")
        text = text.replace("MyComponent<" + component + ">", "MyComponent<" + component + "Data>") + "\n}\n"
        target = output / (name + ".cs")
        write(target, ("// Original client component; see mainline character component source receipt.\n" + text).encode())
        runtime_meta(target)
        inputs.extend((source, code / "common-source/Common" / (component + ".cs")))
        outputs.extend((target, Path(str(target) + ".meta")))
    fixture = ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterComponents.cs"
    runtime_meta(fixture)
    outputs.extend((fixture, Path(str(fixture) + ".meta")))
    outputs.extend(ROOT / path for path in (
        "unity/Assets/Game/Runtime/Official/EntityClientComponents/EntityClientComponentData.cs",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/NativeMap14ClientComponentBindings.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.ClientContext.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/Zjcs.Editor.MainlineWorldStartup.asmdef"))
    write(RECEIPT, encode(dict(format=1, prefab_manifest="871_156598", source_manifest="884_156692",
        prefab_source=selected, script_identities=identities, inputs=[record(path) for path in inputs],
        assemblies=[record(path) for path in assemblies],
        tools=[record(Path(__file__).resolve())], outputs=[record(path) for path in outputs],
        operation="Verify the exact historical bundle, root and all script payload hashes. Pair native/exported hierarchy and component order to recover original script identities without dummy scripts or cross-version bundles. Copy four complete original empty-info MyComponent classes; bind their original shared instances through the existing component cache. No gameplay formula or initial state is reimplemented.",
        validation=dict(entry="Zjcs.Editor.MainlineWorldStartup.OfficialMainlineWorldStartupAcceptance.RunClientContext",
                        checks=["Seven original map characters", "Four original components and empty-info identity", "Lifecycle rebind and retained identity across mixed-region transitions"]),
        limitations=["Original 3001 prefab is unchanged; recovered script identities do not make it a fully bound native character.",
                     "Character rendering/input/effects and formal native minimap wiring remain unfinished.",
                     "Historical prefab and current source provenance stay separate; no whole-assembly version equivalence is claimed."])))
    print("Verified original character scripts:", len(identities), "; migrated components:", len(NAMES), flush=True)


if __name__ == "__main__":
    main()
