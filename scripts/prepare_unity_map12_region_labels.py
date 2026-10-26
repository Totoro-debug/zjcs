"""Restore the 80 original Map12 region-label partition prefabs and exact references."""
from pathlib import Path
import io
import json
import re

from PIL import Image
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map12_native import ROOTS
from prepare_unity_map12_native_terrain import CACHE, EXPORTED, GUID, closure, guid_for
from prepare_unity_map13_native_scene import native_texture, decode_mips
from prepare_unity_map12_native_scene import shader_name
from prepare_unity_map12_sources import encode, sha, source_path, write, meta


DESTINATION = ROOT / "unity/Assets/Official/World/Map/12/NativeRegionLabels"


def build():
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Original mountain partition CAB dependencies are incomplete")
    _, _, index = closure()
    multilingual = next(g for g, p in index.items() if p.name == "MultiLanguageText.cs")
    original_roots = sorted(p for p in (EXPORTED / "Partition/MapGround_12").rglob("*.prefab")
        if p.name != "MapGround_12_bare.prefab" and multilingual in p.read_text(encoding="utf8"))
    if len(original_roots) != 80: raise ValueError("Original region-label partition prefab count differs")
    targets, external = {}, {}
    def redirect(original, target):
        if not original.is_file() or not target.is_file(): raise FileNotFoundError((original, target))
        old = GUID.search(Path(str(original) + ".meta").read_text(encoding="utf8"))[1]
        targets[old] = target; external[old] = dict(original=source_path(original), original_sha256=sha(original.read_bytes()),
            path=source_path(target), sha256=sha(target.read_bytes()), meta_sha256=sha(Path(str(target) + ".meta").read_bytes()))
    for kind, folder in [("native-terrain", "NativeTerrain"), ("native-scene", "NativeScene"), ("native-environment-scene", "NativeEnvironmentScene")]:
        receipt_path = ROOT / "assets/reference/official-map" / ("map12-" + kind + "-source-manifest.json")
        receipt = json.loads(receipt_path.read_text(encoding="utf8"))
        for record in receipt["outputs"]:
            target = ROOT / record["path"]
            if sha(target.read_bytes()) != record["sha256"]: raise ValueError("Existing original native payload differs")
            relative = target.relative_to(ROOT / "unity/Assets/Official/World/Map/12" / folder)
            if relative.name.endswith(".texture.asset"): relative = relative.with_name(relative.name.removesuffix(".texture.asset") + ".png")
            redirect(EXPORTED / relative, target)
    bindings = [b for b in json.loads((CACHE / "scene-script-bindings.json").read_text(encoding="utf8"))["scripts"] if b["script_file_id"] == 11500000]
    script_targets = {b["original_type"]: ROOT / "unity" / b["script_asset"] for b in bindings}
    for binding in bindings:
        target = script_targets[binding["original_type"]]
        if binding["script_file_id"] != 11500000 or GUID.search(Path(str(target) + ".meta").read_text(encoding="utf8"))[1] != binding["script_guid"]:
            raise ValueError("Actual environment MonoScript binding differs")
    # Current selected-version original font/material/atlas objects must match the
    # shared native font payload before any Map13-named asset is reused.
    font_proof_path = ROOT / "assets/reference/official-map/map13-native-text-font-source-manifest.json"
    font_proof = json.loads(font_proof_path.read_text(encoding="utf8"))
    if font_proof["manifest"] != apk.version: raise ValueError("Original font version differs")
    for record in font_proof["inputs"]:
        src = record["source"]
        obj = next(o for o in apk.objects(src["bundle"]) if str(o.path_id) == src["path_id"])
        if sha(obj.get_raw_data()) != record["object_sha256"]: raise ValueError("Current original font object differs")
    for record in font_proof["outputs"]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]: raise ValueError("Shared native font changed")
    original_font = EXPORTED / "TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset"
    original_text = original_font.read_text(encoding="utf8")
    shared_font = ROOT / "unity/Assets/Official/World/Map/13/NativeText"
    redirect(original_font, shared_font / "FZY4K-Dynamic.asset")
    redirect(index[re.search(r"(?m)^  material: \{fileID: 2100000, guid: (\w+)", original_text)[1]], shared_font / "FZY4K-Dynamic.mat")
    redirect(index[re.search(r"(?m)^  - \{fileID: 2800000, guid: (\w+)", original_text)[1]], shared_font / "FZY4K-Dynamic-Atlas.texture.asset")
    label_bindings = json.loads((CACHE / "label-script-bindings.json").read_text(encoding="utf8"))["scripts"]
    for binding in label_bindings:
        target = ROOT / "unity" / binding["script_asset"]
        if not target.is_file() and binding["script_asset"].startswith("Packages/"):
            package, relative = binding["script_asset"][9:].split("/", 1)
            candidates = list((ROOT / "unity/Library/PackageCache").glob(package + "@*/" + relative))
            if len(candidates) != 1: raise ValueError("Original label engine package binding is not unique")
            target = candidates[0]
        if binding["script_file_id"] != 11500000 or GUID.search(Path(str(target) + ".meta").read_text(encoding="utf8"))[1] != binding["script_guid"]:
            raise ValueError("Actual world-label MonoScript binding differs")
        script_targets[binding["original_type"]] = target
    shaders = {}
    for name in ["map13-water-shaders-source-manifest.json", "map13-scene-shaders-source-manifest.json", "map13-environment-shader-source-manifest.json", "map12-environment-shader-source-manifest.json"]:
        proof = json.loads((ROOT / "assets/reference/official-map" / name).read_text(encoding="utf8"))
        for source, record in zip(proof["inputs"], proof["outputs"]):
            obj = next(o for o in apk.objects(source["source"]["bundle"]) if str(o.path_id) == source["source"]["path_id"])
            if sha(obj.get_raw_data()) != source["object_sha256"] or obj.read().m_ParsedForm.m_Name != record["original_shader"]:
                raise ValueError("Original mountain shader differs from the verified translation")
            target = ROOT / record["path"]
            if sha(target.read_bytes()) != record["sha256"]: raise ValueError("Verified original shader differs")
            shaders[record["original_shader"]] = target
    pending = original_roots[:]; files = set(); used = set()
    while pending:
        path = pending.pop()
        old = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
        if old in targets:
            used.add(old); continue
        if path in files: continue
        if path.suffix == ".cs":
            if path.stem not in script_targets: raise ValueError("Migrate original mountain script before native binding: " + path.stem)
            redirect(path, script_targets[path.stem]); used.add(old); continue
        if path.suffix == ".shader" or shader_name(path) is not None:
            name = shader_name(path) if path.suffix != ".shader" else re.search(r'Shader\s+"([^"]+)"', path.read_text(encoding="utf8"))[1]
            redirect(path, shaders[name]); used.add(old); continue
        files.add(path)
        for source in [path, Path(str(path) + ".meta")]:
            if source.suffix not in (".asset", ".prefab", ".mat", ".meta", ".controller", ".anim"): continue
            for guid in GUID.findall(source.read_text(encoding="utf8")):
                if guid in index: pending.append(index[guid])
                elif guid not in ("00000000000000000000000000000000", "0000000000000000e000000000000000", "0000000000000000f000000000000000"):
                    raise ValueError("Original mountain partition pointer is unresolved: " + guid)
    for path in files:
        old = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
        target = DESTINATION / path.relative_to(EXPORTED)
        if target.suffix == ".png": target = target.with_name(target.stem + ".texture.asset")
        targets[old] = target
    texture_objects = {}
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                if obj.type.name == "Texture2D":
                    texture = obj.read(); texture_objects.setdefault((texture.m_Name, texture.m_Width, texture.m_Height), []).append((obj, texture))
    guids = {old: GUID.search(Path(str(target) + ".meta").read_text(encoding="utf8"))[1] if old in external else
        guid_for(index[old].relative_to(EXPORTED)) for old, target in targets.items()}
    native_textures = {guids[old] for old, target in targets.items() if target.name.endswith(".texture.asset")}
    shader_guids = {GUID.search(Path(str(t) + ".meta").read_text(encoding="utf8"))[1] for t in shaders.values()}
    def remap(text):
        text = GUID.sub(lambda m: "guid: " + guids.get(m[1], m[1]), text)
        text = re.sub(r"(\{fileID: 4800000, guid: )([a-f0-9]{32})(, type: )2(\})",
            lambda m: m[1] + m[2] + m[3] + ("3" if m[2] in shader_guids else "2") + m[4], text)
        text = re.sub(r"        class: StateGameObjectConfig\n        ns:[ \t]*\n        asm: Framework\b",
            "        class: StateGameObjectConfig\n        ns: Zjcs.Official.World.NativeLabelUI\n        asm: Zjcs.Official.World.NativeLabelUI", text)
        return re.sub(r"(\{fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3(\})",
            lambda m: m[1] + m[2] + m[3] + ("2" if m[2] in native_textures else "3") + m[4], text)
    template_path = CACHE / "native-texture-template.asset"; template = template_path.read_text(encoding="utf8")
    outputs, inputs, texture_records = [], [], []
    for path in sorted(files):
        relative = path.relative_to(EXPORTED); old = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
        target = targets[old]; raw = path.read_bytes(); metadata = Path(str(path) + ".meta").read_text(encoding="utf8")
        inputs.append(dict(path=source_path(path), sha256=sha(raw), meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
        if path.suffix == ".png":
            size = Image.open(io.BytesIO(raw)).size; candidates = texture_objects.get((path.stem, *size), [])
            if len(candidates) != 1: raise ValueError("Original mountain texture is not unique: " + path.name)
            obj, texture = candidates[0]; rgba, mips, profile = decode_mips(texture)
            raw = native_texture(template, texture, rgba)
            metadata = "fileFormatVersion: 2\nguid: " + guids[old] + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
            texture_records.append(dict(path=source_path(target), source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                rgba_sha256=sha(rgba), astc_sha256=sha(texture.get_image_data()), mips=mips, profile=profile,
                width=texture.m_Width, height=texture.m_Height, mip_count=texture.m_MipCount, sRGB=texture.m_ColorSpace == 1,
                filter=texture.m_TextureSettings.m_FilterMode, wrapU=texture.m_TextureSettings.m_WrapU,
                wrapV=texture.m_TextureSettings.m_WrapV, wrapW=texture.m_TextureSettings.m_WrapW,
                aniso=texture.m_TextureSettings.m_Aniso, mipBias=texture.m_TextureSettings.m_MipBias, readable=texture.m_IsReadable))
        elif path.suffix in (".asset", ".prefab", ".mat", ".controller", ".anim"): raw = remap(raw.decode("utf8")).encode()
        else: raise ValueError("Restore original mountain dependency format: " + str(relative))
        metadata = re.sub(r"(?m)^timeCreated: \d+\n", "", metadata)
        write(target, raw); write(Path(str(target) + ".meta"), remap(metadata).encode())
        outputs.append(dict(path=source_path(target), sha256=sha(raw), meta_sha256=sha(Path(str(target) + ".meta").read_bytes())))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]: meta(folder, folder=True)
    write(ROOT / "assets/reference/official-map/map12-native-region-labels-source-manifest.json", encode(dict(format=1, mapClassId=12, manifest=apk.version,
        roots=[dict(path="Assets/" + p.relative_to(EXPORTED).as_posix(), native_path=source_path(targets[GUID.search(Path(str(p) + ".meta").read_text(encoding="utf8"))[1]])) for p in original_roots], exported_inputs=inputs, outputs=outputs, textures=texture_records,
        reused=[external[g] for g in sorted(used)],
        serialization_reference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        original_input_manifest=dict(path="assets/reference/official-map/map12-native-input-source-manifest.json", sha256=sha((ROOT / "assets/reference/official-map/map12-native-input-source-manifest.json").read_bytes())),
        shared_font_proof=dict(path=source_path(font_proof_path), sha256=sha(font_proof_path.read_bytes())),
        shared_shader_proofs=[dict(path="assets/reference/official-map/" + name, sha256=sha((ROOT / "assets/reference/official-map" / name).read_bytes())) for name in ["map13-water-shaders-source-manifest.json", "map13-scene-shaders-source-manifest.json", "map13-environment-shader-source-manifest.json", "map12-environment-shader-source-manifest.json"]],
        adapters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13EnvironmentBinding.cs",
            ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/Zjcs.Offline.World.NativeMap13.asmdef"]],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12LabelTools.cs",
            ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12LabelAcceptance.cs"]],
        operation="Restore all 80 original world-space region-label partition prefabs. Preserve every original hierarchy, component field, serialized object ID, mesh, animation and texture mip. Bind verified migrated original environment/water scripts, original shaders; reuse the exact native terrain and bare-scene assets. Preserve all 400 original StateGameObjectConfig managed-reference records; redirect only their namespace/assembly to the verified original port. Preserve all serialized hierarchy, coordinates, text/effects and local file IDs.",
        limitations=["Formal local player/save services, normal progression and published Windows Player remain required; complete custom TMP engine equivalence is not established."])))
    print("Restored original region-label partitions:", len(outputs), "new assets,", len(used), "verified dependencies reused,", len(texture_records), "new textures")


if __name__ == "__main__": build()
