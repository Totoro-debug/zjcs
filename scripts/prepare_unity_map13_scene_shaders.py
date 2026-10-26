"""Translate original Map13 terrain/lava shader passes without flattening them."""
from copy import copy
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_native import ROOTS
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map13_water_shaders import translate, SHADERS, SOURCE


FAMILIES = {"Universal Render Pipeline/2D/Sprite-Lit-Default": "TerrainLit", "Sprites/Lava": "Lava"}


def build():
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Original Map13 shader CAB closure is incomplete")
    selected = {}
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                if obj.type.name != "Material": continue
                original = obj.read().m_Shader.deref(); shader = original.read()
                if shader.m_ParsedForm.m_Name in FAMILIES: selected[shader.m_ParsedForm.m_Name] = original
    if set(selected) != set(FAMILIES): raise ValueError("Original terrain/lava shader closure changed")
    inputs, outputs = [], []
    for original_name, obj in selected.items():
        shader = obj.read(); name = FAMILIES[original_name]
        if len(shader.m_ParsedForm.m_SubShaders) != 1: raise ValueError("Restore original terrain subshader structure")
        passages, programs = [], []
        header = None
        for index, original_pass in enumerate(shader.m_ParsedForm.m_SubShaders[0].m_Passes):
            # These source passes use the default disabled stencil state.
            stencil = original_pass.m_State.stencilOp
            if [getattr(stencil, k).val for k in ["comp", "fail", "pass_", "zFail"]] != [8, 0, 0, 0]:
                raise ValueError("Restore original terrain stencil state before converting")
            sub = copy(shader.m_ParsedForm.m_SubShaders[0]); sub.m_Passes = [original_pass]
            form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
            one = copy(shader); one.m_ParsedForm = form
            text, variants = translate(one, name)
            begin = text.index("Pass {\n"); end = text.index("ENDCG\n}\n") + len("ENDCG\n}\n")
            if header is None: header = text[:begin]
            elif header != text[:begin]: raise ValueError("Original shader pass properties disagree")
            passages.append(text[begin:end])
            for p in variants:
                path = SOURCE / (name + "-p" + str(index) + "-" + str(p["index"]) + ".glsl.txt")
                write(path, p["source"].encode()); meta(path)
                programs.append(dict(pass_index=index, index=p["index"], keywords=p["keywords"],
                                     path=source_path(path), sha256=sha(path.read_bytes())))
        target = SHADERS / (name + ".shader")
        write(target, (header + "".join(passages) + "}\nFallback Off\n}\n").encode()); import_meta(target, "ShaderImporter")
        outputs.append(dict(path=source_path(target), sha256=sha(target.read_bytes()), original_shader=original_name))
        inputs.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), programs=programs))
    write(ROOT / "assets/reference/official-map/map13-scene-shaders-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version, inputs=inputs, outputs=outputs,
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
                    [Path(__file__), ROOT / "scripts/prepare_unity_map13_water_shaders.py", ROOT / "scripts/prepare_battle_hud_materials.py", ROOT / "scripts/official_shader_programs.py"]],
        validation=dict(path="unity/Assets/Game/Editor/Map13/OfficialMap13NativeSceneTools.cs",
                        sha256=sha((ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13NativeSceneTools.cs").read_bytes())),
        operation="Mechanical GLES to HLSL conversion preserving all original terrain/lava passes, their LightMode tags, exact arithmetic, original material properties and pass state. Each pass selects only its executable original GPU programs.",
        limitations=["Windows shader compilation, material/prefab binding and native scene rendering require separate validation."])))
    print("Translated original terrain/lava shaders:", len(outputs), "families,", sum(len(i["programs"]) for i in inputs), "variants")


if __name__ == "__main__": build()
