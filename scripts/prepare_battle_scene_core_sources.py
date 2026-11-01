"""Package the current original scene hierarchy and lossless character inputs."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import struct

from UnityPy.classes import PPtr
from convert_official_skeletons import convert, WHEEL_SHA256
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_native_character_geometry import atlas_regions
from prepare_unity_profession_character_resources import asset, guid, meta
from prepare_unity_profession_skeleton_data import block, string


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def payload(obj):
    return dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())


def dereference(owner, pointer):
    return PPtr(**pointer, assetsfile=owner.assets_file).deref()


def decoded_input(sex, workspace):
    folder = workspace / sex
    receipt = folder / "source-manifest.json"
    converter = ROOT / "scripts/convert_official_skeletons.py"
    if not receipt.exists():
        convert(apk.version, sex, folder)
    source = json.loads(receipt.read_bytes())
    assert source["manifest"] == apk.version
    assert source["converter_sha256"] == identity(converter)["sha256"]
    assert source["tool"]["wheel_sha256"] == WHEEL_SHA256
    raw = (folder / "skeleton.json").read_bytes()
    assert hashlib.sha256(raw).hexdigest() == source["output_sha256"]
    for row in source["inputs"]:
        assert hashlib.sha256((folder / row["file"]).read_bytes()).hexdigest() == row["sha256"]
    # Temporary conversion paths do not become persistent source references.
    return raw, [{key: value for key, value in row.items() if key != "file"} for row in source["inputs"]]


def main():
    global apk
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review the scene character source version before reproduction")
    code_base = ROOT / ".tools/source-packages/current" / apk.version
    code_receipt = code_base / "code-manifest.json"
    code = json.loads(code_receipt.read_bytes())
    assembly = code_base / "official-code/managed/decoded/Assembly-CSharp.dll"
    assert identity(assembly)["sha256"] == code["assemblies"]["Assembly-CSharp.dll"]["decoded_sha256"]
    workspace = Path(os.environ["ZJCS_VALIDATION_DIR"]).resolve()
    assert workspace.parent == (ROOT / "output/validation").resolve()
    marker = json.loads((workspace / ".zjcs-validation.json").read_text(encoding="utf-8-sig"))
    assert Path(marker["repository"]).resolve() == ROOT.resolve()
    schemas = CurrentSchemas()
    source = ROOT / "unity/Assets/Official/Combat/SceneCharacterSources"
    resources = ROOT / "unity/Assets/Game/Resources/Combat/SceneCharacters"
    source.mkdir(parents=True, exist_ok=True)
    resources.mkdir(parents=True, exist_ok=True)
    for folder in (source, resources):
        meta(folder, folder=True)
    outputs = [source.with_name(source.name + ".meta"), resources.with_name(resources.name + ".meta")]
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneCharacterAsset.cs"
    meta(runtime)
    validation = ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneCoreSources.cs"
    meta(validation)
    hierarchy_path = "Assets/Prefabs/Appearance/Player/PlayerModel_Game.prefab"
    root = next(ref.deref() for name, ref in apk.container("defaultpackage_assets_prefabs_appearance_player.bundle") if name == hierarchy_path)
    nodes = []

    def visit(obj, parent):
        go = obj.read()
        components, children = [], []
        for reference in go.m_Component:
            component = reference.component.deref()
            kind = component.type.name
            if kind == "MonoBehaviour":
                script = component.read(check_read=False).m_Script.read()
                kind = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
                fields = schemas.read(component)
            else:
                fields = component.read_typetree()
            components.append(dict(type=kind, fields=fields, **payload(component)))
            if component.type.name == "Transform":
                children.extend(component.read().m_Children)
        nodes.append(dict(parent=parent, name=go.m_Name,
            game_object=dict(fields=obj.read_typetree(), **payload(obj)), components=components))
        for child in children:
            visit(child.read().m_GameObject.deref(), str(obj.path_id))

    visit(root, None)
    assert len(nodes) == 3
    table_bytes = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    table = {row["guid"]: row for row in read_asset_manifest(table_bytes)}
    appearance = next(row["fields"] for row in nodes[0]["components"]
        if row["type"] == "AppearanceSystem.Runtime.AppearanceMecanim")
    bindings = []
    for field in ("SkinTextureAssetGuid", "TextureLoaderGuid"):
        source_guid = appearance[field]["mAssetGuid"]
        entry = table[source_guid]
        obj = next(ref.deref() for name, ref in apk.container(entry["bundle"]) if name == entry["path"])
        bindings.append(dict(field=field, guid=source_guid, path=entry["path"], fields=schemas.read(obj), **payload(obj)))
    assert bindings[1]["path"].endswith("/character_model_TextureLoader_Low.asset")
    hierarchy = source / "PlayerModel_Game-source.json"
    hierarchy.write_text(json.dumps(dict(path=hierarchy_path, nodes=nodes), ensure_ascii=False,
        separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8", newline="\n")
    meta(hierarchy)
    outputs.extend((hierarchy, hierarchy.with_name(hierarchy.name + ".meta")))
    historical_path = ROOT / "assets/reference/profession-ui/skeleton-data-source-manifest.json"
    historical = json.loads(historical_path.read_bytes())
    profiles = []
    for sex in ("male", "female"):
        raw, original_binaries = decoded_input(sex, workspace)
        decoded = json.loads(raw)
        model_path = f"Assets/AppearanceAssets/Character/Model/{sex.title()}_1/character_{sex.title()}_1_SkeletonData.asset"
        model = next(ref.deref() for name, ref in apk.container(f"defaultpackage_assets_appearanceassets_character_model_{sex}_1.bundle") if name == model_path)
        fields = schemas.read(model)
        assert len(fields["atlasAssets"]) == 1
        atlas_asset = dereference(model, fields["atlasAssets"][0])
        atlas_fields = schemas.read(atlas_asset)
        atlas_text = dereference(atlas_asset, atlas_fields["atlasFile"])
        atlas_raw = atlas_text.read().m_Script.encode("utf-8", "surrogateescape")
        atlas = atlas_regions(atlas_raw.decode("utf-8"))
        projected = json.dumps(atlas, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        skeleton_hash, atlas_hash = hashlib.sha256(raw).hexdigest(), hashlib.sha256(projected).hexdigest()
        target = source / (sex + ".bytes")
        target.write_bytes(b"ZJCSPROF" + struct.pack("<I", 1) + string(apk.version) + string(sex)
            + struct.pack("<f", fields["scale"]) + block(raw) + block(projected))
        meta(target)
        binding = resources / (sex + ".asset")
        asset(binding, "Original current scene character " + sex, runtime, dict(sex=sex,
            skeletonSha256=skeleton_hash, atlasSha256=atlas_hash,
            payload=dict(fileID=4900000, guid=guid(target), type=3),
            hierarchy=dict(fileID=4900000, guid=guid(hierarchy), type=3)))
        outputs.extend((target, target.with_name(target.name + ".meta"), binding, binding.with_name(binding.name + ".meta")))
        counts = {key: len(decoded.get(key, [])) for key in ("bones", "slots", "skins", "animations", "ik", "transform", "path", "physics")}
        counts.update(regions=len(atlas), attachments=sum(len(entries) for skin in decoded["skins"]
            for entries in skin.get("attachments", {}).values()))
        previous = next(row for row in historical["outputs"] if row["sex"] == sex)
        assert skeleton_hash != previous["skeleton_sha256"], "Review an unexpectedly identical current character projection"
        profiles.append(dict(sex=sex, counts=counts, skeleton_sha256=skeleton_hash, atlas_sha256=atlas_hash,
            scale=fields["scale"], model=dict(path=model_path, fields=fields, **payload(model)),
            atlas=dict(fields=atlas_fields, **payload(atlas_asset)), atlas_text=payload(atlas_text),
            original_binaries=original_binaries, historical=dict(manifest=historical["manifest"],
                skeleton_sha256=previous["skeleton_sha256"], counts=previous["counts"]),
            packaged=identity(target), binding=identity(binding)))
    proof = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        code_receipt=identity(code_receipt), assembly=identity(assembly),
        schema_reader=identity(ROOT / "scripts/current_component_schemas.py"),
        asset_table_sha256=hashlib.sha256(table_bytes).hexdigest(), appearance_bindings=bindings,
        hierarchy=identity(hierarchy), profiles=profiles, historical_receipt=identity(historical_path),
        converter=identity(ROOT / "scripts/convert_official_skeletons.py"), reader_wheel_sha256=WHEEL_SHA256,
        atlas_projection=identity(ROOT / "scripts/prepare_native_character_geometry.py"),
        generator=identity(Path(__file__).resolve()), runtime=identity(runtime),
        validation=identity(validation),
        loader=identity(ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs"),
        outputs=[identity(path) for path in outputs],
        operation="Decode the selected APK-first complete character binary and split animations with the pinned independent MIT format reader; losslessly package every source field and the complete original atlas projection under the actual current identity. Preserve the original three-node scene hierarchy and component fields.",
        limits=["Source hierarchy records are not production scene prefabs; original texture/material resources, renderer/Mecanim backends, complete physics/mixing and formal gameplay/Windows Player integration remain required."])
    target = ROOT / "assets/reference/battle-animation/scene-core-source-manifest.json"
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Packaged current scene character inputs:", [(row["sex"], row["counts"]) for row in profiles])


if __name__ == "__main__":
    main()
