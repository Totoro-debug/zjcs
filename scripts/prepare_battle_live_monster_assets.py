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


def build():
    work = validation_directory()
    source = work / "live-monsters"
    proof_path = work / "live-monsters-source-manifest.json"
    proof = json.loads(proof_path.read_bytes())
    if proof["manifest"] != "884_156692" or proof["generator_sha256"] != sha((ROOT / "scripts/prepare_battle_live_monsters.py").read_bytes()):
        raise ValueError("Verified monster generator or source version differs")
    for file, record in proof["outputs"].items():
        if sha((source / file).read_bytes()) != record["sha256"]:
            raise ValueError("Verified monster input changed: " + file)
    catalog = json.loads((source / "catalog.json").read_bytes())
    apk = Apk.current()
    destination = ROOT / "unity/Assets/Official/Combat/NativeMonsters"
    files, outputs, textures = {}, {}, {}
    def write(relative, raw, file_id=None):
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != raw:
            if relative != "inventory.json":
                raise ValueError("Preserve changed original monster asset: " + str(target))
            previous = json.loads((ROOT / "assets/reference/battle-animation/live-monsters-source-manifest.json").read_bytes())
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
    cache = ROOT / ".tools/source-packages/current/884_156692/native-conversion/all-assets/windows-shaders"
    shader_proof = cache / "source-manifest.json"
    shader, = [row for row in json.loads(shader_proof.read_bytes())["shaders"] if row["source"]["object_sha256"] == "4aa96ea56babfdc788805e559c0c1d94e1d64b7ed36719d12e61f6a08bb850ae"]
    raw = (cache / shader["path"]).read_bytes()
    if sha(raw) != shader["sha256"]: raise ValueError("Original Windows monster shader differs")
    write("OriginalSkeletonEffect.shader", raw)
    inventory = write("inventory.json", encoded(catalog))
    script = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterAssets.cs"
    if not Path(str(script) + ".meta").exists(): meta(script)
    target = destination / "Resources/Combat/NativeMonsters/catalog.asset"
    target.parent.mkdir(parents=True, exist_ok=True)
    asset(target, "Original full-binary battle monsters", script, dict(
        inventory=dict(fileID=4900000, guid=guid(destination / inventory), type=3),
        shader=dict(fileID=4800000, guid=guid(destination / "OriginalSkeletonEffect.shader"), type=3),
        bindings=[dict(File=file, Asset=dict(fileID=kind, guid=guid(destination / file), type=3 if kind == 4900000 else 2)) for file, kind in sorted(files.items())]))
    for path in (target, Path(str(target) + ".meta")): outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for folder in sorted({parent for path in destination.rglob("*") if path.is_file() for parent in path.parents if parent.is_relative_to(destination)}):
        sidecar = Path(str(folder) + ".meta")
        if not sidecar.exists(): meta(folder, folder=True)
        outputs[sidecar.relative_to(ROOT).as_posix()] = sha(sidecar.read_bytes())
    receipt = dict(manifest=apk.version, source_extraction=proof, generator_sha256=sha(Path(__file__).read_bytes()),
        implementation={path: sha((ROOT / path).read_bytes()) for path in (
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterAssets.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Animator.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.BattleMonster.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeOrderedConstraintPose.cs")},
        serializer=dict(template_sha256=sha((work / "monster-texture-template.asset").read_bytes()), unity="2022.3.57f1c2",
            converter_sha256=sha((ROOT / "scripts/prepare_unity_map13_native_scene.py").read_bytes())),
        shader=dict(receipt_sha256=sha(shader_proof.read_bytes()), source=shader), textures=textures, outputs=outputs,
        operation="Preserve full original skeletons, controller closure, prefab bindings and materials. Decode every original ASTC mip to native Unity RGBA32, retaining source texture flags, mip count, color space and sampling. Explicitly bind local Unity assets; no sampled mesh animation.")
    (ROOT / "assets/reference/battle-animation/live-monsters-source-manifest.json").write_bytes(encoded(receipt))
    apk.archive.close()
    print("Installed original live monsters:", len(catalog["models"]), "textures:", len(textures), "files:", len(outputs))

if __name__ == "__main__": build()
