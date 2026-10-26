"""Translate the original APK shape-light programs, including recession variants."""
import argparse
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate
from prepare_unity_bundle_inputs import serialized_files


def build(project):
    apk = Apk.current()
    apk.load_builtin("assets/bin/Data/data.unity3d")
    objects = [o for bundle in apk.loaded.values() for file in serialized_files(bundle)
               for o in file.objects.values() if o.type.name == "Shader"
               and o.read().m_ParsedForm.m_Name == "Hidden/Light2D-Shape"]
    if len(objects) != 1: raise ValueError("Original APK shape-light shader must be unique")
    obj = objects[0]
    text, programs = translate(obj.read(), "LightShape")
    if len(programs) != 32: raise ValueError("Restore all original shape-light variants")
    source = project / "Assets/Official/World/Map/13/Source/Shaders"
    target = project / "Assets/Game/Runtime/Official/World/NativeWater/Shaders/LightShape.shader"
    write(target, text.encode()); import_meta(target, "ShaderImporter")
    records = []
    for p in programs:
        path = source / ("LightShape-" + str(p["index"]) + ".glsl.txt")
        write(path, p["source"].encode()); meta(path)
        records.append(dict(index=p["index"], keywords=p["keywords"],
                            path=source_path(path), sha256=sha(path.read_bytes())))
    receipt = (ROOT / "assets/reference/official-map/map13-light-shader-source-manifest.json"
               if project == ROOT / "unity" else ROOT / ".tools/map13-native-cache-871_156598/light-shader-source-manifest.json")
    converters = [Path(__file__), ROOT / "scripts/prepare_unity_map13_water_shaders.py",
                  ROOT / "scripts/prepare_battle_hud_materials.py", ROOT / "scripts/official_shader_programs.py"]
    write(receipt, encode(dict(format=1, mapClassId=13, manifest=apk.version,
        input=dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())), programs=records,
        output=dict(path=source_path(target), sha256=sha(target.read_bytes())),
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in converters],
        validation=dict(path="unity/Assets/Game/Editor/Map13/OfficialMap13LightShaderTools.cs",
                        sha256=sha((ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13LightShaderTools.cs").read_bytes())),
        operation="Mechanical translation of all 32 original APK shape-light GLES variants, including original USE_RECESSION arithmetic, original shader properties, pass tags and blend/depth/cull/color-mask state. Matrix columns explicitly reconstructed from HLSL row indexing.",
        limitations=["Windows compilation and original pipeline binding are validated separately; this conversion does not prove rendered equivalence."])))
    print("Translated original APK shape-light shader:", len(programs), "variants")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, default=ROOT / "unity")
    build(parser.parse_args().project.resolve())
