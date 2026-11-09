"""Restore the original TravelNotesPanel layout omitted by the native export.

Run with the existing character-art Python environment, after the dungeon binding
generator. Only the unchanged, previously recorded home bundle is accepted.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_dungeon_ui_bindings import relative_transform_path

BINDING = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI/884_156692/official-bindings.json"
RECEIPT = ROOT / "assets/reference/dungeon-ui/884_156692/prefab-binding-source-manifest.json"


def fingerprint(path):
    payload = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else path.as_posix(),
                bytes=len(payload), sha256=hashlib.sha256(payload).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
    original = next(entry["source"] for entry in receipt["prefabs"] if entry["name"] == "TravelNotesPanel")
    code = ROOT / ".tools/source-packages/official-code/framework-firstpass-source/SafeArea.cs"
    expected = next(entry["sha256"] for entry in receipt["code_sources"] if entry["path"].endswith("/SafeArea.cs"))
    if fingerprint(code)["sha256"] != expected:
        raise ValueError("SafeArea source differs from the previously recorded original implementation")
    apk = Apk.current()
    bundle = "defaultpackage_assets_prefabs_ui_home.bundle"
    if apk.index[bundle] != original["bundle_hash"]:
        raise ValueError("The selected home bundle differs from the recorded original bundle")
    root = apk.asset("prefabs_ui_home", "/TravelNotesPanel.prefab")
    source = apk.provenance(root)
    for key in ("raw_size", "raw_sha256", "path_id"):
        if source[key] != original[key]:
            raise ValueError("The original home prefab input identity differs: " + key)
    # Unity's pinned UGUI assembly supplies its real serialized layout schema.
    # No UGUI implementation or art is copied into the output.
    engine = Path(os.environ.get("ZJCS_UNITY_EDITOR_DATA", "D:/Tool/Unity/Editor/Data"))
    ugui = ROOT / "unity/Library/ScriptAssemblies/UnityEngine.UI.dll"
    netstandard = engine / "NetStandard/ref/2.1.0/netstandard.dll"
    schemas = ScriptSchemas(assembly_overrides={"UnityEngine.UI": ugui})
    schemas.generator.load_dll(netstandard.read_bytes())
    safe, fitters, raycasts, entries, objects = [], [], [], [], []
    nodes = list(relative_transform_path(root.read()))
    paths = {go.object_reader.path_id: "/".join(map(str, path)) for go, path, _, _ in nodes}

    def reference_path(pointer):
        if pointer["m_FileID"] or not pointer["m_PathID"]:
            raise ValueError("Travel entry requires a local, non-null Button reference")
        target = root.assets_file.objects[pointer["m_PathID"]].read(check_read=False)
        return paths[target.m_GameObject.path_id]

    for go, path, names, _ in nodes:
        for pair in go.m_Component:
            reader = pair.component.deref()
            if reader.type.name != "MonoBehaviour":
                continue
            basic = reader.read(check_read=False)
            script = basic.m_Script.read().m_Name
            if script not in ("SafeArea", "AspectRatioFitter", "NoDrawingRayCast",
                              "DailyDungeonEntry", "ArenaAreaEntry", "DemonBattleEntry"):
                continue
            fields = schemas.read(reader)  # Strict consumed-byte check.
            common = dict(path="/".join(map(str, path)), objects=list(names), enabled=bool(fields["m_Enabled"]))
            if script == "SafeArea":
                safe.append(dict(common, paddingTop=float(fields["PaddingTop"]),
                                 paddingBottom=float(fields["PaddingBottom"]),
                                 disableTopAdapt=bool(fields["DisableTopAdapt"]),
                                 disableBottomAdapt=bool(fields["DisableBottomAdapt"])))
            elif script == "AspectRatioFitter":
                fitters.append(dict(common, aspectMode=int(fields["m_AspectMode"]),
                                    aspectRatio=float(fields["m_AspectRatio"])))
            elif script == "NoDrawingRayCast":
                if fields["m_Material"]["m_PathID"]:
                    raise ValueError("Original travel raycast unexpectedly requires a material")
                raycasts.append(dict(common, color=[fields["m_Color"][key] for key in ("r", "g", "b", "a")],
                                     raycastTarget=bool(fields["m_RaycastTarget"]),
                                     raycastPadding=[fields["m_RaycastPadding"][key] for key in ("x", "y", "z", "w")]))
            else:
                entries.append(dict(common, type=script, button=reference_path(fields["EntryButton"])))
            raw = reader.get_raw_data()
            objects.append(dict(type=script, objects=list(names), path_id=str(reader.path_id),
                                bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest()))
    if len(safe) != 3 or len(fitters) != 1 or not raycasts or len(entries) != 3:
        raise ValueError("Original travel layout component coverage changed")
    document = json.loads(BINDING.read_text(encoding="utf-8-sig"))
    prefab = next(entry for entry in document["prefabs"] if entry["name"] == "TravelNotesPanel")
    if args.check:
        if (prefab.get("safeAreas") != safe or prefab.get("aspectRatioFitters") != fitters
                or prefab.get("raycastGraphics") != raycasts or prefab.get("entryButtons") != entries):
            raise ValueError("Travel layout bindings differ from the original component fields")
        if receipt.get("travel_entry_layout", {}).get("output") != fingerprint(BINDING):
            raise ValueError("Travel layout output receipt differs")
        print(f"Original travel bindings verified: 3 SafeArea, 1 AspectRatioFitter, {len(raycasts)} raycast graphics, 3 entry Buttons")
        return
    previous = fingerprint(BINDING)
    prefab["safeAreas"], prefab["aspectRatioFitters"] = safe, fitters
    prefab["raycastGraphics"], prefab["entryButtons"] = raycasts, entries
    BINDING.write_text(json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n",
                       encoding="utf-8", newline="\n")
    section = receipt.get("travel_entry_layout", {})
    section.setdefault("previous_output", previous)
    section.update(operation="Restore omitted original SafeArea, AspectRatioFitter, NoDrawingRayCast and entry Button references. "
                   "Selected bundle bytes and SafeArea implementation match the recorded original input. "
                   "Historical input provenance remains unchanged; current identical bundle location is recorded separately.",
                   identical_input_location=source, objects=objects, safe_area_code=fingerprint(code),
                   raycast_code=fingerprint(ROOT / ".tools/source-packages/official-code/framework-firstpass-source/NoDrawingRayCast.cs"),
                   schema_assemblies=[fingerprint(ugui), fingerprint(netstandard)],
                   extractor=fingerprint(Path(__file__).resolve()), output=fingerprint(BINDING))
    receipt["travel_entry_layout"] = section
    # Retain all historical inputs; update the existing output fingerprint too.
    receipt["bindings"]["sha256"] = fingerprint(BINDING)["sha256"]
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, sort_keys=True, indent=2) + "\n",
                      encoding="utf-8", newline="\n")
    print(f"Restored original travel bindings: 3 SafeArea, 1 AspectRatioFitter, {len(raycasts)} raycast graphics, 3 entry Buttons")


if __name__ == "__main__":
    main()
