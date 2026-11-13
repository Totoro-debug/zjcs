"""Restore original story UI registration lists omitted by disabled APK DLLs."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from map13_native_schemas import fields_tree, node
from official_metadata import Metadata
from UnityPy.classes.PPtr import PPtr
from prepare_dungeon_ui_assets import convert_runtime_prefab_text

VERSION = "884_156692"
OUTPUT = ROOT / "unity/Assets/Game/Resources/StoryUI/native-registrations.json"
RECEIPT = ROOT / "assets/reference/official-map/story-ui-registration-source-manifest.json"
PREFABS = (
    ("DailyDungeonDialogPrivateMap", "prefabs_ui_dailydungeon"),
    ("TeamPanelPrivateMap", "prefabs_ui_team"),
    ("TeamInvitePanelPrivateMap", "prefabs_ui_team"),
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def record(path):
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())}


def extract(apk, schemas, name, bundle):
    prefab = apk.asset(bundle, "/" + name + ".prefab")
    identities, components = {}, []

    def visit(obj, indices, names):
        go = obj.read()
        names = names + [go.m_Name]
        address = dict(path="/".join(map(str, indices)), objects=names)
        identities[(obj.assets_file.name, obj.path_id)] = address
        transform = None
        for pair in go.m_Component:
            component = pair.component.deref()
            identities[(component.assets_file.name, component.path_id)] = address
            if component.type.name in ("RectTransform", "Transform"):
                transform = component.read()
            elif component.type.name == "MonoBehaviour":
                if component.read(check_read=False).m_Script.read().m_ClassName in ("StoryNodeRegister", "GraphicRaycaster"):
                    components.append((component, address))
        for i, child in enumerate(transform.m_Children):
            visit(child.read().m_GameObject.deref(), indices + [i], names)

    visit(prefab, [], [])
    registrations, raycasters, sources = [], [], []
    for component, address in components:
        if component.read(check_read=False).m_Script.read().m_ClassName == "GraphicRaycaster":
            names = ("m_IgnoreReversedGraphics", "m_BlockingObjects", "m_BlockingMask")
            code = ROOT / ".tools/source-packages/current" / VERSION
            metadata = (code / "official-code/global-metadata.dat").read_bytes()
            if sha(metadata) != json.loads((code / "code-manifest.json").read_text())["metadata_sha256"]:
                raise ValueError("Pinned APK metadata differs")
            fields = next(t["fields"] for t in Metadata(metadata).types
                if t["name"] == "GraphicRaycaster" and t["namespace"] == "UnityEngine.UI")
            if [f["name"] for f in fields if f["name"] in names] != list(names):
                raise ValueError("Original GraphicRaycaster field order differs")
            tree = fields_tree(schemas, [(names[0], "bool"), (names[1], "int")])
            mask = node("LayerMask", names[2], 1)
            mask.m_Children = [node("UInt32", "m_Bits", 2)]
            tree.m_Children.append(mask)
            fields = component.read_typetree(nodes=tree, check_read=True)
            raycasters.append(dict(**address, enabled=bool(fields["m_Enabled"]),
                ignoreReversedGraphics=fields[names[0]], blockingObjects=fields[names[1]],
                blockingMask=fields[names[2]]["m_Bits"]))
            sources.append(dict(source=apk.provenance(component), raw_sha256=sha(component.get_raw_data()), fields=fields))
            continue
        fields = schemas.read(component)
        nodes = []
        for pointer in fields["StoryNodes"]:
            target = PPtr(**pointer, assetsfile=component.assets_file).deref() if pointer["m_PathID"] else None
            nodes.append(None if target is None else identities[(target.assets_file.name, target.path_id)])
        registrations.append(dict(**address, enabled=bool(fields["m_Enabled"]), keys=fields["StoryNodeKeys"], nodes=nodes))
        sources.append(dict(source=apk.provenance(component), raw_sha256=sha(component.get_raw_data()), fields=fields))
    if not registrations:
        raise ValueError("No original StoryNodeRegister found: " + name)
    return dict(name=name, registrations=registrations, raycasters=raycasters), dict(name=name, prefab=apk.provenance(prefab), components=sources)


def write(path, data, folder=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not folder:
        path.write_bytes(data)
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        guid = hashlib.md5(("zjcs/story-ui/" + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        meta.write_text("fileFormatVersion: 2\nguid: " + guid + ("\nfolderAsset: yes\n" if folder else "\n"), encoding="utf-8")


def build():
    apk = Apk(VERSION)
    try:
        code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
        schemas = ScriptSchemas(code_root=code)
        prefabs, sources = [], []
        for name, bundle in PREFABS:
            prefab, source = extract(apk, schemas, name, bundle)
            prefabs.append(prefab)
            sources.append(source)
        encoded = lambda value: (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
        write(OUTPUT.parent, b"", folder=True)
        write(OUTPUT, encoded(dict(manifest=VERSION, prefabs=prefabs)))
        # Preserve the existing resource/material remapping; repair only exact
        # official package script references in these already imported windows.
        dungeon_receipt = ROOT / "assets/reference/dungeon-ui" / VERSION / "source-manifest.json"
        dungeon = json.loads(dungeon_receipt.read_text(encoding="utf-8"))
        repaired = []
        for name, _ in PREFABS:
            target = ROOT / "unity/Assets/Game/Resources/Dungeons/UI" / VERSION / (name + ".prefab")
            if not target.exists():
                continue  # TeamPanel currently has no packaged native window.
            path = target.relative_to(ROOT).as_posix()
            entry = next(p for p in dungeon["runtime_prefabs"] if p["path"] == path)
            if sha(target.read_bytes()) != entry["sha256"]:
                raise ValueError("Refusing to overwrite changed native story window: " + path)
            target.write_bytes(convert_runtime_prefab_text(target.read_text(encoding="utf-8")))
            entry["sha256"] = sha(target.read_bytes())
            repaired.append(dict(path=path, source=entry["source"], source_sha256=entry["source_sha256"], sha256=entry["sha256"]))
        for binding in dungeon["runtime_script_bindings"]:
            if binding["type"] == "UIMesh":
                binding["runtime_code_sha256"] = sha((ROOT / binding["runtime_code"]).read_bytes())
        dungeon_receipt.write_text(json.dumps(dungeon, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        RECEIPT.parent.mkdir(parents=True, exist_ok=True)
        RECEIPT.write_bytes(encoded(dict(format=1, manifest=VERSION, inputs=sources,
            tools=[record(Path(__file__).resolve()), record(ROOT / "scripts/official_unity_schema.py"), record(ROOT / "scripts/map13_native_schemas.py"), record(ROOT / "scripts/prepare_dungeon_ui_assets.py"), record(ROOT / "scripts/prepare_daily_dungeon_result_ui.py")],
            metadata=record(code / "global-metadata.dat"),
            code_manifest=record(code.parent / "code-manifest.json"), outputs=[record(OUTPUT)], native_windows=repaired,
            original_ui_bindings=record(ROOT / "unity/Assets/StreamingAssets/Dungeons/UI" / VERSION / "official-bindings.json"),
            runtime_bindings=[record(ROOT / "unity/Assets/Game/Runtime/Official/WorldUI" / name) for name in ("UIMesh.cs", "WorldUiMesh.cs")],
            operation="Read verified APK-first/local-cloud prefab components with pinned managed schemas and APK GraphicRaycaster field order; consume complete components. Preserve original key/node order, nulls, enable state and exact indexed hierarchy references. Bind migrated original StoryNodeRegister and raycaster fields; remap exact native UGUI DLL/fileID references without changing prefab parameters/material remapping. The WorldUiMesh compatibility GUID uses the migrated launch UIMesh body with a local serialized-reference Configure adapter; recovered APK DLLs stay disabled.")))
        print("Original story UI registrations:", [(p["name"], len(p["registrations"])) for p in prefabs])
    finally:
        apk.reader.close()


if __name__ == "__main__":
    build()
