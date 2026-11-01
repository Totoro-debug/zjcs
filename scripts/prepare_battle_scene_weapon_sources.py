"""Recover exact battle weapon scene hierarchies; reuse only byte-verified native assets."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk
from official_manifest import read_asset_manifest
from prepare_unity_profession_character_resources import asset, guid, meta
from UnityPy.classes import PPtr

ROOT = Path(__file__).resolve().parents[1]


def identity(path: Path) -> dict:
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def checked(record: dict) -> Path:
    path = ROOT / record["path"]
    if identity(path) != record:
        raw = path.read_bytes()
        # Git's current Windows checkout may use CRLF for these text carriers.
        # Verify the complete original bytes after only this reversible encoding
        # change, retaining both hashes in the new receipt; never relax APK checks.
        if not raw.startswith((b"%YAML", b"Shader ")) or hashlib.sha256(raw.replace(b"\r\n", b"\n")).hexdigest() != record["sha256"]:
            raise ValueError("Previously converted original asset changed: " + record["path"])
    return path


def build():
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review scene weapon version before reproduction")
    schemas = CurrentSchemas()
    base = ROOT / ".tools/source-packages/current" / apk.version
    code_manifest = base / "code-manifest.json"
    code = json.loads(code_manifest.read_bytes())
    assembly = base / "official-code/managed/decoded/Assembly-CSharp.dll"
    if identity(assembly)["sha256"] != code["assemblies"]["Assembly-CSharp.dll"]["decoded_sha256"]:
        raise ValueError("Current original application assembly differs")
    receipts = [ROOT / "assets/reference/profession-ui" / name for name in (
        "weapon-resources-source-manifest.json", "weapon-texture-loader-source-manifest.json")]
    old, loader = [json.loads(path.read_bytes()) for path in receipts]
    known = {row["payload_sha256"]: row for row in old["sources"] + loader["sources"]}
    shader_outputs = {row["payload_sha256"]: row for row in loader["shaders"]}
    shader_cache_receipt = base / "native-conversion/all-assets/windows-shaders/source-manifest.json"
    shader_cache = json.loads(shader_cache_receipt.read_bytes())
    cached_shaders = {row["source"]["object_sha256"]: row for row in shader_cache["shaders"]}
    shader_conversions = []
    output_records = {row["path"]: row for row in old["outputs"] + loader["outputs"]}
    native_materials = {row["original_fields"]["m_Name"]: row for row in loader["materials"]}
    table_bytes = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    table = {row["path"]: row for row in read_asset_manifest(table_bytes)}
    destination = ROOT / "unity/Assets/Official/ProfessionUI/SceneWeaponSources"
    destination.mkdir(parents=True, exist_ok=True)
    meta(destination, folder=True)
    outputs = [destination.with_name(destination.name + ".meta")]
    sources, reused, entries = [], {}, []

    def source(obj, fields=None, require_old=False):
        digest = hashlib.sha256(obj.get_raw_data()).hexdigest()
        if require_old and digest not in known:
            raise ValueError("Current model/atlas/loader dependency differs from original converted bytes: " + obj.type.name + ":" + str(obj.path_id) + " " + digest)
        record = dict(source=apk.provenance(obj), payload_sha256=digest)
        if fields is not None:
            record["fields"] = fields
        if digest in known:
            record["historical_source"] = known[digest]["source"]
        sources.append(record)
        return record

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref()

    def reuse(record):
        path = checked(record)
        actual = identity(path)
        if actual != record:
            actual.update(prior_expected_sha256=record["sha256"], adaptation="Working-tree CRLF only; full LF-normalized bytes exactly match the prior receipt.")
        reused[record["path"]] = actual
        return path

    for skin in ("pWeapon_100_G_007", "pWeapon_200_G_007"):
        path = f"Assets/Prefabs/Appearance/PlayerPart/{skin}/{skin}_Integrated.prefab"
        row = table[path]
        candidates = [ref.deref() for name, ref in apk.container(row["bundle"]) if name == path and ref.deref().type.name == "GameObject"]
        if len(candidates) != 1:
            raise ValueError("Original scene prefab root is not unique")
        nodes, skeletons, materials = [], [], []

        def visit(obj, parent):
            go = obj.read()
            components, children = [], []
            for ref in go.m_Component:
                component = ref.component.deref()
                kind = component.type.name
                if kind == "MonoBehaviour":
                    script = component.read(check_read=False).m_Script.read()
                    kind = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
                    fields = schemas.read(component)
                    if kind == "Spine.Unity.SkeletonAnimation":
                        skeletons.append((component, fields))
                else:
                    fields = component.read_typetree()
                if kind == "MeshRenderer":
                    materials.extend(pointer(component, value) for value in fields["m_Materials"])
                normalized = dict(fields)
                for name in ("FollowZSpacing", "ZFront", "FollowLocalScale", "FollowBoneWorldFlip", "FollowRotation",
                             "FollowSkeletonTransformFlipX", "InitializeOnAwake", "loop", "useClipping", "pmaVertexColors"):
                    if name in normalized:
                        normalized[name] = bool(normalized[name])
                components.append(dict(type=kind, runtime_fields=normalized, **source(component, fields)))
                if component.type.name == "Transform":
                    children.extend(component.read().m_Children)
            nodes.append(dict(parent=parent, name=go.m_Name, game_object=source(obj, obj.read_typetree()), components=components))
            for child in children:
                visit(child.read().m_GameObject.deref(), str(obj.path_id))

        visit(candidates[0], None)
        if len(skeletons) != 1 or len(materials) != 1 or len(nodes) != 4:
            raise ValueError("Review changed original weapon hierarchy")
        component, fields = skeletons[0]
        model = pointer(component, fields["skeletonDataAsset"])
        model_fields = schemas.read(model)
        source(model, model_fields, True)
        source(pointer(model, model_fields["skeletonJSON"]), require_old=True)
        for atlas_ref in model_fields["atlasAssets"]:
            atlas = pointer(model, atlas_ref)
            atlas_fields = schemas.read(atlas)
            source(atlas, atlas_fields, True)
            source(pointer(atlas, atlas_fields["atlasFile"]), require_old=True)
        material = materials[0]
        material_fields = material.read_typetree()
        material_source = source(material, material_fields, True)
        old_material = native_materials[material_fields["m_Name"]]
        if material_source["source"]["raw_sha256"] != old_material["source"]["raw_sha256"]:
            raise ValueError("Original native material source bundle differs")
        native_material = reuse(old_material["output"])
        shader = pointer(material, material_fields["m_Shader"])
        shader_source = source(shader)
        shader_digest = shader_source["payload_sha256"]
        if shader_digest in shader_outputs:
            shader_path = reuse(shader_outputs[shader_digest]["output"])
        else:
            cached = cached_shaders[shader_digest]
            cached_path = shader_cache_receipt.parent / cached["path"]
            if identity(cached_path)["sha256"] != cached["sha256"]:
                raise ValueError("Current original shader conversion cache changed")
            shader_path = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI/SceneWeaponSkeletonDye2D.shader"
            text = cached_path.read_text(encoding="utf-8")
            original_name = shader.read().m_ParsedForm.m_Name
            if not text.startswith('Shader "' + original_name + '"'):
                raise ValueError("Current original shader conversion name differs")
            text = text.replace('Shader "' + original_name + '"', 'Shader "Zjcs/Official/ProfessionUI/SceneWeaponSkeletonDye2D"', 1)
            shader_path.write_text(text, encoding="utf-8", newline="\n")
            meta(shader_path)
            if shader_path not in outputs:
                outputs.extend((shader_path, shader_path.with_name(shader_path.name + ".meta")))
                shader_conversions.append(dict(source=shader_source, cache=identity(cached_path), cache_receipt=identity(shader_cache_receipt),
                    output=identity(shader_path), programs=cached["programs"], stripped_passes=cached["stripped_passes"],
                    adaptation="Only the native ShaderLab asset name changes; the current cached Windows program/state/property conversion is retained byte-for-byte otherwise."))
        for _, texture in material_fields["m_SavedProperties"]["m_TexEnvs"]:
            if texture["m_Texture"]["m_PathID"]:
                source(pointer(material, texture["m_Texture"]), require_old=True)
        original_native = native_material
        native_material = destination / original_native.name
        text = original_native.read_text(encoding="utf-8")
        text, count = re.subn(r"(?m)^(  m_Shader: )\{[^\n]+\}$", lambda match: match[1] + "{fileID: 4800000, guid: " + guid(shader_path) + ", type: 3}", text)
        if count != 1:
            raise ValueError("Original converted material shader pointer is not unique")
        native_material.write_text(text, encoding="utf-8", newline="\n")
        meta(native_material, native=True)
        native_material.with_name(native_material.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid(native_material) + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n", encoding="utf-8", newline="\n")
        outputs.extend((native_material, native_material.with_name(native_material.name + ".meta")))
        target = destination / (skin + "_Integrated-source.json")
        target.write_text(json.dumps(dict(path=path, guid=row["guid"], nodes=nodes), ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8", newline="\n")
        meta(target)
        outputs.extend((target, target.with_name(target.name + ".meta")))
        entries.append(dict(Path=path, Guid=row["guid"], InitialSkin=fields["initialSkinName"], InitialAnimation=fields["_animationName"],
            Hierarchy=dict(fileID=4900000, guid=guid(target), type=3), OriginalMaterial=dict(fileID=2100000, guid=guid(native_material), type=2)))
    for name in ("primaryWeapon_TextureLoader_High",):
        loader_obj = next(obj for obj in apk.objects("appearanceassets_character_pweapon") if obj.type.name == "MonoBehaviour" and obj.read(check_read=False).m_Name == name)
        source(loader_obj, schemas.read(loader_obj), True)
    weapon_path = ROOT / "unity/Assets/Game/Resources/ProfessionUI/weapon-resources.asset"
    textures_path = ROOT / "unity/Assets/Game/Resources/ProfessionUI/weapon-texture-resources.asset"
    for path in (weapon_path, textures_path, ROOT / "unity/Assets/Official/ProfessionUI/WeaponResources/primary-weapon.bytes",
                 ROOT / "unity/Assets/Official/ProfessionUI/WeaponTextureLoader/primary-weapon-atlas.asset",
                 ROOT / "unity/Assets/Official/ProfessionUI/WeaponTextureLoader/primary-weapon-texture-loader-high.asset"):
        reuse(output_records[path.relative_to(ROOT).as_posix()])
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneWeaponCatalog.cs"
    meta(runtime)
    catalog = ROOT / "unity/Assets/Game/Resources/ProfessionUI/scene-weapon-sources.asset"
    asset(catalog, "Original integrated scene weapon sources", runtime, dict(
        weapon=dict(fileID=11400000, guid=guid(weapon_path), type=2), textures=dict(fileID=11400000, guid=guid(textures_path), type=2), entries=entries))
    outputs.extend((catalog, catalog.with_name(catalog.name + ".meta")))
    proof = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        code_receipt=identity(code_manifest), assembly=identity(assembly), asset_table_sha256=hashlib.sha256(table_bytes).hexdigest(),
        prior_receipts=[identity(path) for path in receipts], sources=sources, reused_outputs=list(reused.values()),
        current_shader_conversions=shader_conversions,
        outputs=[identity(path) for path in outputs], runtime=identity(runtime), generator=identity(Path(__file__).resolve()),
        schema_reader=identity(ROOT / "scripts/current_component_schemas.py"),
        scope="APK-first complete integrated sword/staff scene source hierarchy and component fields; exact byte identity permits reuse of the original model, atlas and loader. Materials retain their original properties with an explicit shader binding to the independently identified current Windows conversion.",
        limits=["Source hierarchies are not production scene prefabs; renderer/lite component backend and complete battle integration remain required."])
    receipt = ROOT / "assets/reference/battle-animation/scene-weapon-source-manifest.json"
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recovered", len(entries), "original scene weapon hierarchies;", len(sources), "source objects;", len(reused), "verified existing native outputs")
    return proof


if __name__ == "__main__":
    build()
