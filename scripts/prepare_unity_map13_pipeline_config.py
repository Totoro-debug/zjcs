"""Decode the APK's complete renderer/pipeline settings without default guesses."""
import argparse
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from prepare_unity_bundle_inputs import serialized_files
from map13_native_schemas import metadata_types
from prepare_mainline_fog import renderer_data
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


def build(reference_project):
    schemas = ScriptSchemas.__new__(ScriptSchemas)
    schemas.generator = TypeTreeGenerator("2022.3.57f1", generator="AssetsTools")
    schemas.cache, schemas.records = {}, {}
    references = []
    folders = [Path("D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0"),
               Path("D:/Tool/Unity/Editor/Data/Managed/UnityEngine"),
               reference_project / "Library/ScriptAssemblies"]
    for folder in folders:
        for path in sorted(folder.glob("*.dll")):
            # The schema needs the matching engine and URP baseline, not game assemblies.
            if folder == folders[-1] and not path.name.startswith("Unity."): continue
            schemas.generator.load_dll(path.read_bytes())
            references.append(dict(path=source_path(path) if path.is_relative_to(ROOT) else path.as_posix(), sha256=sha(path.read_bytes())))
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    files = [f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers.assets"]
    if len(files) != 1: raise ValueError("Original pipeline assets file must be unique")
    records, outputs, values = [], [], {}
    destination = ROOT / "unity/Assets/Official/World/Map/13/Source"
    for pid, expected in [(22207, "Renderer2DData"), (22208, "Renderer2DData"), (22209, "UniversalRenderPipelineAsset"), (22211, "PostProcessData")]:
        obj = files[0].objects[pid]
        script = obj.read(check_read=False).m_Script.read()
        if script.m_ClassName != expected: raise ValueError("Original pipeline object identity changed")
        full_name = script.m_Namespace + "." + expected
        tree = schemas.tree(script.m_AssemblyName, full_name)
        allowed = {f["name"] for t in metadata_types() if t["name"] in (expected, "ScriptableRendererData") for f in t["fields"]}
        # Runtime APK strips Editor-only fields. Also reject baseline fields absent
        # in the original metadata, such as later shader-prefiltering settings.
        tree.m_Children = tree.m_Children[:4] + [n for n in tree.m_Children[4:] if n.m_Name in allowed]
        serialized = [n.m_Name for n in tree.m_Children[4:]]
        for typ in (expected, "ScriptableRendererData"):
            original = next(t for t in metadata_types() if t["name"] == typ)
            selected = [f["name"] for f in original["fields"] if f["name"] in serialized]
            if [n for n in serialized if n in selected] != selected:
                raise ValueError("Pipeline field order differs from original APK: " + typ)
        value = obj.read_typetree(nodes=tree)  # Strict full-byte consumption.
        if expected == "Renderer2DData":
            legacy = renderer_data(obj.get_raw_data())
            if legacy["name"] != value["m_Name"] or legacy["textureScale"] != value["m_LightRenderTextureScale"]:
                raise ValueError("Independent original renderer readers disagree")
        values[str(pid)] = value
        records.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                            class_name=full_name, serialized_fields=serialized))
    pipeline = values["22209"]
    if pipeline["m_RendererDataList"] != [{"m_FileID": 0, "m_PathID": 22207}, {"m_FileID": 0, "m_PathID": 22208}] or pipeline["m_DefaultRendererIndex"] != 0:
        raise ValueError("Original renderer selection changed")
    path = destination / "native-render-pipeline.json"
    write(path, encode(dict(manifest=apk.version, objects=values))); meta(path)
    outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    receipt = ROOT / "assets/reference/official-map/map13-render-config-source-manifest.json"
    write(receipt, encode(dict(format=1, mapClassId=13, manifest=apk.version, inputs=records,
        reference_assemblies=references, outputs=outputs,
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
                    [Path(__file__), ROOT / "scripts/official_unity_schema.py", ROOT / "scripts/map13_native_schemas.py", ROOT / "scripts/prepare_mainline_fog.py"]],
        operation="Generate engine serialization trees from Unity URP references, restrict to original APK metadata field order, strictly consume all bytes of both Renderer2DData, the selected pipeline asset and original PostProcessData. Cross-check renderer values with the existing independent binary reader. Preserve both renderer slots, original default index, all post-process shader references and original ordered noise/grain/SMAA textures.",
        limitations=["These are decoded source settings. Native pipeline assets, dependency bindings and rendered scene validation remain required; no graphics project setting is changed by this extraction."])))
    print("Decoded complete original APK pipeline settings: 2 renderers, default renderer 0, original post-process dependencies")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference-project", type=Path, default=ROOT / ".tools/map13-partition-validation/unity")
    build(parser.parse_args().reference_project.resolve())
