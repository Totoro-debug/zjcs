"""Recover mountain UI from the selected APK/cloud objects, preserving native bindings."""
import argparse
import json

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from map11_support_ui import load_support_ui
from prepare_unity_map12_sources import encode, meta, sha, source_path, write
from home_particle_schema import load as load_particle_schema


def prepare_context():
    apk = Apk.current()
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = ScriptSchemas(code_root=code)
    load_particle_schema(schemas)
    support = load_support_ui(apk, schemas, 12, code)
    extra_trees, extra_sources, extra_resources, _, _, _, _, exporter = support
    source, prefabs = dict(extra_trees), dict(extra_sources)
    for key in ("GoddessTouchDialog", "LostGoddessTouchDialog", "6010_12_DialogIcon",
                "6011_12_DialogIcon", "6620_12_DialogIcon", "WorldCanvas",
                "WorldFantasyLadderDialog", "6103_DialogIcon", "6302_DialogIcon"):
        bundle = "prefabs_ui_map_world" if key == "WorldCanvas" else "prefabs_ui_worldmap"
        native, _ = exporter.object_tree(key, bundle)
        nodes = native["nodes"]
        for node in nodes:
            node["children"] = []
        for node in nodes[1:]:
            nodes[node["parentIndex"]]["children"].append(node)
        source[key] = nodes[0]
        prefabs[key] = dict(native["source"], path=native["source_path"])
        if key in ("WorldFantasyLadderDialog", "6103_DialogIcon", "6302_DialogIcon"):
            extra_trees[key] = nodes[0]
            extra_sources[key] = prefabs[key]
    base = ROOT / "unity/Assets/Official/World/Map/12/Source"
    path = base / "ui-prefabs.json"
    receipt_path = base / "ui-prefabs-source-manifest.json"
    raw = encode(source)
    manifest = dict(manifest=apk.version, operation="Decode original UI prefab hierarchy from selected native objects.",
        prefabs=prefabs, resources=dict(extra_resources, **exporter.resources), output_sha256=sha(raw),
        assemblies={name: dict(path=source_path(code / "managed/decoded" / name),
            sha256=sha((code / "managed/decoded" / name).read_bytes()))
            for name in ("Assembly-CSharp.dll", "Framework.dll", "Console.dll")})
    write(path, raw)
    write(receipt_path, encode(manifest))
    meta(path)
    meta(receipt_path)
    print(f"Map12 native UI source: {len(source)} prefab hierarchies", flush=True)
    return path, receipt_path, raw, manifest, source, apk, schemas, support


def build():
    from prepare_unity_map11_ui import build as restore
    restore(12)


def record_native_bindings():
    """Record the imported Windows assets after Unity has restored native bindings."""
    source = ROOT / "unity/Assets/Official/World/Map/12/UI"
    native = ROOT / "unity/Assets/Game/Resources/Map12/UI"
    data = json.loads((source / "map12-ui.json").read_bytes())
    if data["manifest"] != "871_156598" or data["mapClassId"] != 12:
        raise ValueError("Unexpected mountain UI source")
    for panel in data["panels"]:
        if not (native / (panel["key"] + ".prefab")).is_file():
            raise FileNotFoundError("Unity has not restored " + panel["key"])
    if (native / "catalog.json").read_bytes() != (source / "map12-ui.json").read_bytes():
        raise ValueError("Runtime UI catalog differs from selected source")
    inputs = [source / "source-manifest.json", source / "map12-ui.json"]
    inputs.append(ROOT / "unity/Assets/Official/World/Map/12/Resources/Map12/chests.json")
    inputs += [ROOT / name for name in (
        "scripts/prepare_unity_map12_ui.py", "scripts/prepare_unity_map11_ui.py",
        "scripts/map11_support_ui.py", "scripts/map11_exploration_ui.py", "scripts/map11_minimap_ui.py",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Support.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Stage.cs",
        "unity/Assets/Game/Editor/OfficialMap12BuildingPreviewTools.cs",
        "unity/Assets/Game/Runtime/Presentation/World/NativeMap12BuildingGraphic.cs",
        "unity/Assets/Official/World/Map/12/Source/BuildingPreview/preview.json",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Animation.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.MiniMap.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Sprites.cs")]
    outputs = sorted(path for path in native.rglob("*") if path.is_file())
    outputs += sorted(source.rglob("*.meta"))
    record = lambda path: dict(path=source_path(path), sha256=sha(path.read_bytes()), bytes=path.stat().st_size)
    receipt = dict(manifest=data["manifest"], mapClassId=12,
        operation="Restore original hierarchy, serialized UI bindings, JSZX layout, sprite import settings and animation curves as Windows Unity assets.",
        inputs=[record(path) for path in inputs], outputs=[record(path) for path in outputs],
        bindings=dict(resourceRoot="Map12/UI/", panels=[panel["key"] for panel in data["panels"]],
            sprites=len(data["sprites"])),
        limitations=["UI source/import is separate from full map gameplay, payments and save recovery.",
            "Native UIParticle references are retained in source; complete particle simulation and effects remain pending."])
    target = ROOT / "assets/reference/official-map/map12-ui-native-source-manifest.json"
    write(target, encode(receipt))
    print(f"Map12 native UI receipt: {len(outputs)} imported asset files", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--record-native-bindings", action="store_true")
    args = parser.parse_args()
    record_native_bindings() if args.record_native_bindings else build()
