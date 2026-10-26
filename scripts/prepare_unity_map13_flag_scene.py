"""Restore the original shrine partition and bind all 46 native flag instances."""
from pathlib import Path
import io
import json
import re

from PIL import Image
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_native import ROOTS
from prepare_unity_map13_native_terrain import CACHE, EXPORTED, GUID, closure, guid_for
from prepare_unity_map13_native_scene import native_texture, decode_mips
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativeFlagScene"
ORIGINAL = Path("Partition/MapGround_13/Prefabs/2_-5.prefab")
ORIGINALS = [ORIGINAL, *[Path("Prefabs/Environments/Entity/shrine") / (name + ".prefab") for name in
    ["Entrance", "gameside", "NoFunctionalArea", "Paper", "Shrine_Left", "Shrine_Right"]]]


def build():
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Original shrine partition CAB dependencies are incomplete")
    _, _, index = closure()
    targets, external = {}, {}
    def redirect(original, target):
        if not original.is_file() or not target.is_file(): raise FileNotFoundError((original, target))
        old = GUID.search(Path(str(original) + ".meta").read_text(encoding="utf8"))[1]
        targets[old] = target; external[old] = dict(original=source_path(original), original_sha256=sha(original.read_bytes()),
            path=source_path(target), sha256=sha(target.read_bytes()), meta_sha256=sha(Path(str(target) + ".meta").read_bytes()))
    for kind in ["native-terrain", "native-scene"]:
        receipt_path = ROOT / "assets/reference/official-map" / ("map13-" + kind + "-source-manifest.json")
        receipt = json.loads(receipt_path.read_text(encoding="utf8"))
        folder = "NativeTerrain" if kind == "native-terrain" else "NativeScene"
        for record in receipt["outputs"]:
            target = ROOT / record["path"]
            if sha(target.read_bytes()) != record["sha256"]: raise ValueError("Existing original native payload differs")
            relative = target.relative_to(ROOT / "unity/Assets/Official/World/Map/13" / folder)
            if relative.name.endswith(".texture.asset"): relative = relative.with_name(relative.name.removesuffix(".texture.asset") + ".png")
            redirect(EXPORTED / relative, target)
    flag_receipt_path = ROOT / "assets/reference/official-map/map13-native-flags-binding-source-manifest.json"
    flag_receipt = json.loads(flag_receipt_path.read_text(encoding="utf8"))
    for record in flag_receipt["outputs"]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]: raise ValueError("Imported native flag data differs")
    for i in range(1, 4):
        name = "map_yuanShuiShenShe_Piaodai_" + str(i).zfill(2)
        original = EXPORTED / "AppearanceAssets/Map" / name
        native = ROOT / "unity/Assets/Official/World/Map/13/NativeFlags"
        for before, after in [(name + "_SkeletonData.asset", name + "-data.asset"), (name + "_Controller.controller", name + ".controller"),
            ("loop.anim", name + "-clock.anim"), (name + "_Material.mat", name + ".mat"), (name + ".png", name + ".texture.asset")]:
            redirect(original / before, native / after)
    script_targets = {"PartialSubmersion": ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/PartialSubmersion.cs",
        "SkeletonMecanim": ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13FlagMesh.cs"}
    shaders = {}
    for name in ["map13-water-shaders-source-manifest.json", "map13-scene-shaders-source-manifest.json"]:
        for record in json.loads((ROOT / "assets/reference/official-map" / name).read_text(encoding="utf8"))["outputs"]:
            target = ROOT / record["path"]
            if sha(target.read_bytes()) != record["sha256"]: raise ValueError("Verified original shader differs")
            shaders[record["original_shader"]] = target
    pending = [EXPORTED / p for p in ORIGINALS]; files = set(); used = set()
    while pending:
        path = pending.pop()
        old = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
        if old in targets:
            used.add(old); continue
        if path in files: continue
        if path.suffix == ".cs":
            if path.stem not in script_targets: raise ValueError("Migrate original shrine script before native binding: " + path.stem)
            redirect(path, script_targets[path.stem]); used.add(old); continue
        if path.suffix == ".shader":
            name = re.search(r'Shader\s+"([^"]+)"', path.read_text(encoding="utf8"))[1]
            redirect(path, shaders[name]); used.add(old); continue
        files.add(path)
        for source in [path, Path(str(path) + ".meta")]:
            if source.suffix not in (".asset", ".prefab", ".mat", ".meta", ".controller", ".anim"): continue
            for guid in GUID.findall(source.read_text(encoding="utf8")):
                if guid in index: pending.append(index[guid])
                elif guid not in ("00000000000000000000000000000000", "0000000000000000e000000000000000", "0000000000000000f000000000000000"):
                    raise ValueError("Original shrine partition pointer is unresolved: " + guid)
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
    def remap(text):
        text = GUID.sub(lambda m: "guid: " + guids.get(m[1], m[1]), text)
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
            if len(candidates) != 1: raise ValueError("Original shrine texture is not unique: " + path.name)
            obj, texture = candidates[0]; rgba, mips, profile = decode_mips(texture)
            raw = native_texture(template, texture, rgba)
            metadata = "fileFormatVersion: 2\nguid: " + guids[old] + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
            texture_records.append(dict(path=source_path(target), source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                rgba_sha256=sha(rgba), astc_sha256=sha(texture.get_image_data()), mips=mips, profile=profile))
        elif path.suffix in (".asset", ".prefab", ".mat", ".controller", ".anim"): raw = remap(raw.decode("utf8")).encode()
        else: raise ValueError("Restore original shrine dependency format: " + str(relative))
        metadata = re.sub(r"(?m)^timeCreated: \d+\n", "", metadata)
        write(target, raw); write(Path(str(target) + ".meta"), remap(metadata).encode())
        outputs.append(dict(path=source_path(target), sha256=sha(raw), meta_sha256=sha(Path(str(target) + ".meta").read_bytes())))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]: meta(folder, folder=True)
    write(ROOT / "assets/reference/official-map/map13-native-flag-scene-source-manifest.json", encode(dict(format=1, mapClassId=13, manifest=apk.version,
        roots=[dict(path="Assets/" + p.as_posix(), instances=(EXPORTED / p).read_text(encoding="utf8").count(next(g for g, t in targets.items() if t.name == "NativeMap13FlagMesh.cs"))) for p in ORIGINALS], exported_inputs=inputs, outputs=outputs, textures=texture_records,
        reused=[external[g] for g in sorted(used)], flag_bindings=dict(path=source_path(flag_receipt_path), sha256=sha(flag_receipt_path.read_bytes())),
        serialization_reference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Restore the unchanged original shrine partition and six shrine entity prefab hierarchies, containing 17 partition instances and 29 independent entity-prefab instances. Explicitly replace original SkeletonMecanim/data/controller/material references with the verified independent native flag conversion, preserve original instance Transform/Animator/MeshRenderer fields, and reuse verified terrain, bare-scene and water assets. Keep every original texture mip and serialized object ID.",
        limitations=["Unity import, explicit partition resource binding, original-instance animation/rendering and complete map lifecycle remain required."])))
    print("Restored original shrine partition:", len(outputs), "new assets,", len(used), "verified dependencies reused,", len(texture_records), "new textures")


if __name__ == "__main__": build()
