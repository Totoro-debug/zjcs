"""Bind verified full-binary partner packages and original Windows shader locally."""
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_unity_profession_character_resources import asset, guid, meta


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def verify_records(value):
    if isinstance(value, dict):
        if "path" in value and "sha256" in value:
            path = ROOT / value["path"]
            if identity(path)["sha256"] != value["sha256"]:
                raise ValueError("Verified partner input changed: " + str(path))
        for child in value.values():
            verify_records(child)
    elif isinstance(value, list):
        for child in value:
            verify_records(child)


def build():
    data_proof = ROOT / "assets/reference/partner-native-battle-source-manifest.json"
    animator_proof = ROOT / "assets/reference/partner-native-battle-animators-source-manifest.json"
    for path in (data_proof, animator_proof):
        proof = json.loads(path.read_bytes())
        if proof["manifest"] != "884_156692":
            raise ValueError("Original partner battle version differs")
        verify_records(proof)
    source = ROOT / "unity/Assets/Official/Combat/NativePartners"
    inventory = source / "catalog.json"
    rows = json.loads(inventory.read_bytes())["fighters"]
    destination = ROOT / "unity/Assets/Official/Combat/NativePartnerAssets"
    resources = destination / "Resources/Combat/NativePartnerAssets"
    resources.mkdir(parents=True, exist_ok=True)
    outputs = []
    for folder in (destination, destination / "Resources", destination / "Resources/Combat", resources):
        metadata = Path(str(folder) + ".meta")
        if not metadata.exists():
            meta(folder, folder=True)
        outputs.append(metadata)
    cache = ROOT / ".tools/source-packages/current/884_156692/native-conversion/all-assets/windows-shaders"
    shader_receipt = cache / "source-manifest.json"
    shader, = [row for row in json.loads(shader_receipt.read_bytes())["shaders"]
               if row["source"]["object_sha256"] == "4aa96ea56babfdc788805e559c0c1d94e1d64b7ed36719d12e61f6a08bb850ae"]
    shader_path = cache / shader["path"]
    if identity(shader_path)["sha256"] != shader["sha256"] or identity(Path(str(shader_path) + ".meta"))["sha256"] != shader["meta_sha256"]:
        raise ValueError("Original partner Windows shader conversion changed")
    installed_shader = destination / "OriginalSkeletonEffect.shader"
    for original, target in ((shader_path, installed_shader), (Path(str(shader_path) + ".meta"), Path(str(installed_shader) + ".meta"))):
        if target.exists() and target.read_bytes() != original.read_bytes():
            raise ValueError("Preserve changed partner shader: " + str(target))
        shutil.copy2(original, target)
        outputs.append(target)
    files = {}
    for fighter in rows:
        for part in [fighter] + fighter["parts"]:
            for field in ("package", "materials"):
                files[part[field]] = 4900000
            materials = json.loads((source / part["materials"]).read_bytes())
            if len(materials) != 1:
                raise ValueError("Review original partner multipage material")
            for material in materials:
                fields = material["fields"]
                if fields["stringTagMap"] or fields["disabledShaderPasses"] or fields["m_BuildTextureStacks"]:
                    raise ValueError("Bind additional original partner material branches")
                for texture in material["runtimeTextures"]:
                    files[texture["file"]] = 2800000
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI"
    catalog_script = runtime / "PartnerNativeBattleAssetCatalog.cs"
    actor_script = runtime / "PartnerNativeBattleActor.cs"
    validation_script = ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleRuntimeAcceptance.cs"
    for script in (catalog_script, actor_script, validation_script):
        metadata = Path(str(script) + ".meta")
        if not metadata.exists():
            meta(script)
        metadata.write_text("\n".join(line.rstrip() for line in metadata.read_text("utf-8").splitlines()) + "\n",
                            encoding="utf-8", newline="\n")
    controllers = ROOT / "unity/Assets/Official/Combat/NativePartnerAnimators/Resources/Combat/NativePartnerAnimators/catalog.asset"
    catalog = resources / "catalog.asset"
    asset(catalog, "Current original complete-skeleton partner battle assets", catalog_script,
          dict(inventory=dict(fileID=4900000, guid=guid(inventory), type=3),
               shader=dict(fileID=4800000, guid=guid(installed_shader), type=3),
               controllers=dict(fileID=11400000, guid=guid(controllers), type=2),
               bindings=[dict(File=file, Asset=dict(fileID=files[file], guid=guid(source / file), type=3))
                         for file in sorted(files)]))
    outputs += [catalog, Path(str(catalog) + ".meta")]
    inputs = [data_proof, animator_proof, shader_receipt, shader_path, Path(str(shader_path) + ".meta"), inventory, controllers,
              catalog_script, Path(str(catalog_script) + ".meta"), actor_script, Path(str(actor_script) + ".meta"),
              validation_script, Path(str(validation_script) + ".meta"), ROOT / "scripts/prepare_partner_native_battle_validation.py",
              runtime / "ProfessionNativeAnimatorPose.cs", runtime / "ProfessionNativeSceneMeshPart.cs"]
    proof = dict(manifest="884_156692", generator=identity(Path(__file__).resolve()),
                 inputs=[identity(path) for path in inputs], bindings=[dict(file=file, asset=identity(source / file),
                    meta=identity(Path(str(source / file) + ".meta"))) for file in sorted(files)],
                 shader_source=shader["source"], outputs=[identity(path) for path in outputs],
                 binding="Combat/NativePartnerAssets/catalog: original inventory, 35 packages, 35 material field payloads, 28 runtime textures, current original Windows shader and 34 original controllers.",
                 operation="Bind local Unity object references; retain original payloads, transforms, renderer settings, material fields and shader bytes. No new image or substitute shader.",
                 limitations=["Formal battle candidates and full battle playback remain unbound until the native actor is integrated and validated.",
                              "Eight original 11015-11022 Low texture objects are 24x24. Preserve the selected original resources; do not infer a completed appearance from asset binding alone."])
    path = ROOT / "assets/reference/partner-native-battle-assets-source-manifest.json"
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Bound original partner packages/materials/textures:", len(files), "files;", len(rows), "controllers.")


if __name__ == "__main__":
    build()
