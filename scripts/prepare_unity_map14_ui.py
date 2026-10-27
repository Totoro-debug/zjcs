"""Recover the current official Dragon Country HUD and common world UI."""
import argparse

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from map11_support_ui import load_support_ui
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from home_particle_schema import load as load_particle_schema


def prepare_context():
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Unexpected Map14 world UI source version")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = ScriptSchemas(code_root=code)
    load_particle_schema(schemas)
    support = load_support_ui(apk, schemas, 14, code)
    extra_trees, extra_sources, extra_resources, _, _, _, _, exporter = support
    source, prefabs = dict(extra_trees), dict(extra_sources)
    for key in ("GoddessTouchDialog", "LostGoddessTouchDialog", "6010_14_DialogIcon",
                "6011_14_DialogIcon", "6620_14_DialogIcon", "WorldCanvas",
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
            extra_trees[key], extra_sources[key] = nodes[0], prefabs[key]
    base = ROOT / "unity/Assets/Official/World/Map/14/Source"
    path = base / "ui-prefabs.json"
    receipt_path = base / "ui-prefabs-source-manifest.json"
    raw = encode(source)
    manifest = dict(manifest=apk.version,
        operation="Decode original Map14 world UI hierarchy and component bindings. Native Windows import, behavior and interaction require validation.",
        prefabs=prefabs, resources=dict(extra_resources, **exporter.resources), output_sha256=sha(raw),
        assemblies={name: dict(path=source_path(code / "managed/decoded" / name),
            sha256=sha((code / "managed/decoded" / name).read_bytes()))
            for name in ("Assembly-CSharp.dll", "Framework.dll", "Console.dll")})
    write(path, raw); write(receipt_path, encode(manifest)); meta(path); meta(receipt_path)
    print("Map14 original world UI:", len(source), "prefab hierarchies", flush=True)
    return path, receipt_path, raw, manifest, source, apk, schemas, support


def build():
    from prepare_unity_map11_ui import build as restore
    restore(14)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources-only", action="store_true")
    if parser.parse_args().sources_only:
        prepare_context()
    else:
        build()
