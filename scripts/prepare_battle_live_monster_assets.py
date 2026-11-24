"""Install verified full-binary monster inputs and Windows texture assets."""
import copy
import hashlib
import json
from pathlib import Path
import shutil
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / ".tools/python-packages"))
from official_apk import Apk, ROOT
from validation_workspace import validation_directory
from prepare_unity_profession_character_resources import asset, guid, meta
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_battle_live_monsters import sha, encoded


def build(*, destination=None, resource_path="Combat/NativeMonsters/catalog", receipt_path=None,
          reuse_verified_shader=False, presentation_resource_root=None):
    work = validation_directory()
    source = work / "live-monsters"
    proof_path = work / "live-monsters-source-manifest.json"
    proof = json.loads(proof_path.read_bytes())
    if proof["generator_sha256"] != sha((ROOT / "scripts/prepare_battle_live_monsters.py").read_bytes()):
        raise ValueError("Verified monster generator differs")
    for file, record in proof["outputs"].items():
        if sha((source / file).read_bytes()) != record["sha256"]:
            raise ValueError("Verified monster input changed: " + file)
    catalog = json.loads((source / "catalog.json").read_bytes())
    apk = Apk.current()
    if proof["manifest"] != apk.version or catalog["manifest"] != apk.version:
        raise ValueError("Verified monster catalog and source versions differ")
    destination = Path(destination) if destination is not None else ROOT / "unity/Assets/Official/Combat/NativeMonsters"
    receipt_path = Path(receipt_path) if receipt_path is not None else ROOT / "assets/reference/battle-animation/live-monsters-source-manifest.json"
    files, outputs, textures = {}, {}, {}
    def write(relative, raw, file_id=None):
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != raw:
            if relative != "inventory.json":
                raise ValueError("Preserve changed original monster asset: " + str(target))
            previous = json.loads(receipt_path.read_bytes())
            if previous["outputs"][target.relative_to(ROOT).as_posix()] != sha(target.read_bytes()):
                raise ValueError("Preserve modified original monster inventory")
            before, after = json.loads(target.read_bytes()), json.loads(raw)
            retained = {row["entityId"]: row for row in after["models"]}
            if before["manifest"] != after["manifest"] or any(
                    retained.get(row["entityId"]) != row for row in before["models"]):
                raise ValueError("An incremental monster install must preserve every installed source binding")
        target.write_bytes(raw)
        metadata = Path(str(target) + ".meta")
        if not metadata.exists(): meta(target)
        for path in (target, metadata): outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        if file_id is not None: files[relative] = file_id
        return relative
    for model in catalog["models"]:
        write(model["package"], (source / model["package"]).read_bytes(), 4900000)
        for identity, row in model["textures"].items():
            if identity not in textures:
                original = proof["sources"][identity]
                provenance = original["source"]
                cab, path_id = identity.split(":")
                obj, = [obj for obj in apk.objects(provenance["bundle"])
                    if obj.assets_file.name == cab and obj.path_id == int(path_id)]
                if sha(obj.get_raw_data()) != original["payload_sha256"]:
                    raise ValueError("Original monster texture object changed")
                texture = obj.read()
                rgba, mips, profile = decode_mips(texture)
                template = (work / "monster-texture-template.asset").read_text("utf-8")
                relative = "textures/" + sha(obj.get_raw_data()) + ".asset"
                write(relative, native_texture(template, texture, rgba), 2800000)
                textures[identity] = dict(file=relative, mips=mips, profile=profile, rgba_sha256=sha(rgba), source=original)
            row["file"] = textures[identity]["file"]
            row.pop("imageBytes")
    for file in proof["outputs"]:
        if file.startswith("animators/") and not file.endswith(".meta"):
            target = destination / file
            target.parent.mkdir(parents=True, exist_ok=True)
            for item in (file, file + ".meta"):
                raw = (source / item).read_bytes()
                installed = destination / item
                if installed.exists() and installed.read_bytes() != raw: raise ValueError("Preserve changed original monster Animator")
                installed.write_bytes(raw)
                outputs[installed.relative_to(ROOT).as_posix()] = sha(raw)
            files[file] = 9100000 if file.endswith(".controller") else 7400000
    shader_bindings, additional_shaders = [], []
    required_shaders = {(material["shader"], proof["sources"][material["shaderSource"]]["payload_sha256"])
        for model in catalog["models"] for material in model["materials"]}
    if reuse_verified_shader:
        shader_proof = ROOT / "assets/reference/battle-animation/live-monsters-source-manifest.json"
        installed = json.loads(shader_proof.read_bytes())
        original = installed["shader"]["source"]
        if required_shaders != {(original["source"]["name"], original["source"]["object_sha256"])}:
            raise ValueError("Selected monster materials require different native shader programs")
        shader_asset = ROOT / "unity/Assets/Official/Combat/NativeMonsters/OriginalSkeletonEffect.shader"
        shader_meta = Path(str(shader_asset) + ".meta")
        for path in (shader_asset, shader_meta):
            if sha(path.read_bytes()) != installed["outputs"][path.relative_to(ROOT).as_posix()]:
                raise ValueError("Verified shared monster shader differs: " + str(path))
        shader = dict(source=original["source"], path=shader_asset.relative_to(ROOT).as_posix(),
                      sha256=sha(shader_asset.read_bytes()), meta_sha256=sha(shader_meta.read_bytes()))
    else:
        cache = ROOT / ".tools/source-packages/current" / apk.version / "native-conversion/all-assets/windows-shaders"
        shader_proof = cache / "source-manifest.json"
        shader_rows = json.loads(shader_proof.read_bytes())["shaders"]
        shader, = [row for row in shader_rows if row["source"]["name"] == "Universal Render Pipeline/Spine/SkeletonEffect-2D"]
        raw = (cache / shader["path"]).read_bytes()
        if sha(raw) != shader["sha256"]:
            raise ValueError("Original Windows monster shader differs")
        shader_asset = destination / write("OriginalSkeletonEffect.shader", raw)
        for name, expected in sorted(required_shaders):
            original, = [row for row in shader_rows if row["source"]["name"] == name and row["source"]["object_sha256"] == expected]
            if original == shader:
                continue
            raw = (cache / original["path"]).read_bytes()
            if sha(raw) != original["sha256"]:
                raise ValueError("Original Windows monster material shader differs: " + name)
            relative = write("shaders/" + original["sha256"] + ".shader", raw)
            shader_bindings.append(dict(Name=name, Asset=dict(fileID=4800000, guid=guid(destination / relative), type=3)))
            additional_shaders.append(original)
    inventory = write("inventory.json", encoded(catalog))
    script = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterAssets.cs"
    if not Path(str(script) + ".meta").exists(): meta(script)
    target = destination / "Resources" / (resource_path + ".asset")
    target.parent.mkdir(parents=True, exist_ok=True)
    payload = dict(
        inventory=dict(fileID=4900000, guid=guid(destination / inventory), type=3),
        shader=dict(fileID=4800000, guid=guid(shader_asset), type=3),
        shaderBindings=shader_bindings,
        bindings=[dict(File=file, Asset=dict(fileID=kind, guid=guid(destination / file), type=3 if kind == 4900000 else 2)) for file, kind in sorted(files.items())])
    if presentation_resource_root is not None:
        payload["presentationResourceRoot"] = presentation_resource_root
    asset(target, "Original full-binary battle monsters", script, payload)
    for path in (target, Path(str(target) + ".meta")): outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    folders = {parent for name in outputs for parent in (ROOT / name).parents
               if parent.is_relative_to(destination)}
    for folder in sorted(folders):
        sidecar = Path(str(folder) + ".meta")
        if not sidecar.exists(): meta(folder, folder=True)
        outputs[sidecar.relative_to(ROOT).as_posix()] = sha(sidecar.read_bytes())
    receipt = dict(manifest=apk.version, source_extraction=proof, generator_sha256=sha(Path(__file__).read_bytes()),
        implementation={path: sha((ROOT / path).read_bytes()) for path in (
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterAssets.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Animator.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.MeshCopies.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.BattleMonster.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeOrderedConstraintPose.cs")},
        serializer=dict(template_sha256=sha((work / "monster-texture-template.asset").read_bytes()), unity="2022.3.57f1c2",
            converter_sha256=sha((ROOT / "scripts/prepare_unity_map13_native_scene.py").read_bytes())),
        shader=dict(receipt_sha256=sha(shader_proof.read_bytes()), source=shader, additional_sources=additional_shaders), textures=textures, outputs=outputs,
        operation="Preserve full original skeletons, controller closure, prefab bindings and materials. Decode every original ASTC mip to native Unity RGBA32, retaining source texture flags, mip count, color space and sampling. Explicitly bind local Unity assets; no sampled mesh animation.")
    receipt_path.write_bytes(encoded(receipt))
    apk.archive.close()
    print("Installed original live monsters:", len(catalog["models"]), "textures:", len(textures), "files:", len(outputs))

if __name__ == "__main__": build()
