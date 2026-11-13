"""Restore every selected scene Low page and the exact original atlas/skin bindings."""
from __future__ import annotations

import copy
import hashlib
import json
import os
from pathlib import Path
import re
import uuid

import astc_encoder
import yaml
from UnityPy.classes import PPtr
from UnityPy.enums import TextureFormat

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_scene_weapon_sources import checked
from prepare_unity_profession_character_resources import guid
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    apk = Apk.current()
    assert apk.version == "884_156692"
    workspace = Path(os.environ["ZJCS_VALIDATION_DIR"]).resolve()
    assert workspace.parent == (ROOT / "output/validation").resolve()
    assert Path(json.loads((workspace / ".zjcs-validation.json").read_text(encoding="utf-8-sig"))["repository"]).resolve() == ROOT.resolve()
    core_path = ROOT / "assets/reference/battle-animation/scene-core-source-manifest.json"
    core = json.loads(core_path.read_bytes())
    assert core["manifest"] == apk.version
    weapons_path = ROOT / "assets/reference/battle-animation/scene-weapon-source-manifest.json"
    weapons = json.loads(weapons_path.read_bytes())
    shader = weapons["current_shader_conversions"][0]
    shader_path = checked(shader["output"])
    shader_sha = shader["source"]["payload_sha256"]
    table_raw = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    assert sha(table_raw) == core["asset_table_sha256"]
    table = {row["guid"]: row for row in read_asset_manifest(table_raw) if row["guid"]}
    by_path = {row["path"]: row for row in table.values()}
    destination = ROOT / "unity/Assets/Official/Combat/SceneCharacterTextures"
    payloads = destination / "Resources/Combat/SceneTexturePayloads"
    for folder in (destination, destination / "Bindings", destination / "Materials", destination / "Textures",
        destination / "Resources", destination / "Resources/Combat", payloads):
        folder.mkdir(parents=True, exist_ok=True)
        meta(folder, folder=True)
    outputs = [p.with_name(p.name + ".meta") for p in (destination, destination / "Bindings", destination / "Materials",
        destination / "Textures", destination / "Resources", destination / "Resources/Combat", payloads)]
    sources, textures, materials, reused = [], [], [], []
    old_receipts, known = [], {}
    for name in ("character-textures", "weapon-texture-loader"):
        path = ROOT / ("assets/reference/profession-ui/" + name + "-source-manifest.json")
        previous = json.loads(path.read_bytes())
        old_receipts.append(identity(path))
        source_hashes = {(r["source"]["bundle_hash"], r["source"]["path_id"]): r["payload_sha256"] for r in previous.get("sources", [])}
        for row in previous["textures"]:
            digest = row.get("payload_sha256") or source_hashes.get((row["source"]["bundle_hash"], row["source"]["path_id"]))
            if digest:
                known[digest] = row

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value["m_PathID"] else None

    def source(obj, values=None):
        row = dict(source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        if values is not None:
            row["fields"] = values
        sources.append(row)
        return row

    def reference(path, file_id):
        return dict(fileID=file_id, guid=guid(path), type=2)

    def native(path, kind, file_id, values):
        name = {21: "Material", 28: "Texture2D", 114: "MonoBehaviour"}[kind]
        text = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!" + str(kind) + " &" + str(file_id) + "\n"
        # Unity's binary carrier is already canonical hexadecimal. Only serialize
        # its small header through YAML; scanning hundreds of MB as Python YAML
        # scalars adds no validation and makes complete exports needlessly slow.
        binary = values.get("_typelessdata")
        serialized = dict(values, _typelessdata="ZJCS_NATIVE_TEXTURE_BYTES") if binary is not None else values
        text += yaml.dump({name: serialized}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        if binary is not None:
            marker = "_typelessdata: ZJCS_NATIVE_TEXTURE_BYTES"
            assert text.count(marker) == 1
            text = text.replace(marker, "_typelessdata: " + binary, 1)
        write_changed(path, text)
        meta(path)
        write_changed(path.with_name(path.name + ".meta"), "fileFormatVersion: 2\nguid: " + guid(path)
            + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: " + str(file_id) + "\n")
        outputs.extend((path, path.with_name(path.name + ".meta")))
        return reference(path, file_id)

    def scriptable(path, name, script, values):
        value = source_header()
        value.update(m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(script), type=3),
            m_Name=name, m_EditorClassIdentifier="")
        value.update(values)
        return native(path, 114, 11400000, value)

    def json_asset(path, value):
        write_changed(path, json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n")
        meta(path)
        outputs.extend((path, path.with_name(path.name + ".meta")))
        return dict(fileID=4900000, guid=guid(path), type=3)

    texture_cache = {}
    texture_output_owners = {}

    def texture(obj, resource=False):
        key = (obj.assets_file.name, str(obj.path_id))
        if key in texture_cache:
            row = texture_cache[key]
            assert not resource or row.get("resource"), "A lazy page was first bound as a static texture"
            return row
        assert obj.type.name == "Texture2D"
        data = obj.read()
        original = obj.read_typetree()
        original.pop("image data")
        src = source(obj)
        assert not data.m_MipsStripped and data.m_ImageCount == 1 and data.m_TextureDimension == 2
        encoded = data.get_image_data()
        if "ASTC" in TextureFormat(data.m_TextureFormat).name:
            pixels, mips, profile = decode_mips(data)
            fmt = 4
        else:
            fmt, pixels, profile = int(data.m_TextureFormat), encoded, "original-uncompressed"
            assert fmt in (1, 4)
            mips, offset = [], 0
            for level in range(data.m_MipCount):
                w, h = max(1, data.m_Width >> level), max(1, data.m_Height >> level)
                size = w * h * (1 if fmt == 1 else 4)
                mips.append(dict(level=level, width=w, height=h, rgba_offset=offset, rgba_size=size, rgba_sha256=sha(pixels[offset:offset + size])))
                offset += size
            assert offset == len(pixels)
        previous = known.get(src["payload_sha256"])
        if previous is not None and previous["encoded_sha256"] != sha(encoded):
            previous = None
        if previous is not None and not resource:
            candidate = checked(previous["output"])
            # Equal payload bytes do not imply equal Unity object identity.
            # Distinct original placeholders must remain distinct dictionary keys.
            if str(candidate) in texture_output_owners and texture_output_owners[str(candidate)] != key:
                previous = None
        if previous is not None and not resource:
            candidate = checked(previous["output"])
            header = candidate.read_text(encoding="utf-8-sig").split("  _typelessdata:", 1)[0]
            native_fields = yaml.safe_load("\n".join(header.splitlines()[3:]))["Texture2D"]
            # Unity ignores legacy sampler carriers without the native version.
            # Keep historical outputs intact and restore this current dependency.
            if native_fields["m_TextureSettings"].get("serializedVersion") != 2:
                previous = None
        reused_input = None
        if previous is not None and not resource:
            path = checked(previous["output"])
            reused_input = identity(path)
            reused.append(dict(input=reused_input, meta=identity(path.with_name(path.name + ".meta")), prior_source=previous["source"]))
        else:
            token = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/scene-texture/884/" + key[0] + ":" + key[1]).hex
            path = (payloads if resource else destination / "Textures") / (token + ".asset")
            value = source_header()
            value.update(fields(copy.deepcopy(original)))
            value.update(serializedVersion=2, m_TextureFormat=fmt, m_CompleteImageSize=len(pixels), m_PlatformBlob="",
                m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=""))
            value["m_TextureSettings"]["serializedVersion"] = 2
            value["image data"] = len(pixels)
            value["_typelessdata"] = pixels.hex()
            native(path, 28, 2800000, value)
        row = dict(**src, original_fields=original, encoded_sha256=sha(encoded), profile=profile,
            target_format=fmt, mips=mips, pixels_sha256=sha(pixels), binding=reference(path, 2800000),
            output=identity(path), reused=reused_input)
        if resource:
            row["resource"] = "Combat/SceneTexturePayloads/" + path.stem
        textures.append(row)
        texture_cache[key] = row
        texture_output_owners[str(path)] = key
        return row

    skin_source, loader_source = core["appearance_bindings"]
    skin_obj = apk.asset(skin_source["source"]["bundle"], skin_source["path"])
    loader_obj = apk.asset(loader_source["source"]["bundle"], loader_source["path"])
    assert sha(skin_obj.get_raw_data()) == skin_source["payload_sha256"]
    assert sha(loader_obj.get_raw_data()) == loader_source["payload_sha256"]
    source(skin_obj, skin_source["fields"])
    source(loader_obj, loader_source["fields"])
    mappings, addresses, mapping_fixture = [], {}, []
    for index, mapping in enumerate(loader_source["fields"]["placeholderMap"]):
        value = copy.deepcopy(mapping)
        placeholder = texture(pointer(loader_obj, mapping["placeholderTexture"]))
        value["placeholderTexture"] = placeholder["binding"]
        mapping_fixture.append(dict(path=placeholder["output"]["path"].removeprefix("unity/"),
            main=mapping["targetTextureReference"]["AssetReference"]["mAssetGuid"],
            dye=mapping["dyeTextureReference"]["AssetReference"]["mAssetGuid"]))
        for field in ("targetTextureReference", "dyeTextureReference"):
            asset_guid = mapping[field]["AssetReference"]["mAssetGuid"]
            if not asset_guid or asset_guid in addresses:
                continue
            entry = table[asset_guid]
            obj = apk.asset(entry["bundle"], entry["path"])
            row = texture(obj, resource=True)
            addresses[asset_guid] = row["resource"]
        mappings.append(value)
        if (index + 1) % 50 == 0:
            print("Restored", index + 1, "of", len(loader_source["fields"]["placeholderMap"]), "original Low mappings", flush=True)
    official = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    assert len({row["placeholderTexture"]["guid"] for row in mappings}) == len(mappings), "Distinct original placeholders were aliased"
    offline = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI"
    skins = copy.deepcopy(skin_source["fields"])
    configs = []
    for entry in skins["mSkinTextureConfigList"]:
        value = copy.deepcopy(entry)
        value["PlaceHolderTexture"] = texture(pointer(skin_obj, entry["PlaceHolderTexture"]))["binding"]
        configs.append(value)
    skin_binding = scriptable(destination / "Bindings/skin-textures.asset", skins["m_Name"], official / "SkinTextureAsset.cs",
        dict(mSkeletonDataAssets=skins["mSkeletonDataAssets"], mSkinTextureConfigList=configs))
    loader = loader_source["fields"]
    assert not loader["atlasAsset"]["m_PathID"] and not loader["skeletonDataAsset"]["m_PathID"]
    keep = {k: copy.deepcopy(loader[k]) for k in ("maxPlaceholderSize", "unloadAfterSecondsUnused", "LevelOfResolution", "PlaceholderPath", "TargetTexturePath", "DyeTexturePath")}
    keep.update(atlasAsset=dict(fileID=0), skeletonDataAsset=dict(fileID=0), placeholderMap=mappings)
    loader_binding = scriptable(destination / "Bindings/texture-loader-low.asset", loader["m_Name"], official / "YooAssetTextureLoader.cs", keep)

    material_cache, models = {}, []
    for profile in core["profiles"]:
        model_obj = apk.asset(profile["model"]["source"]["bundle"], profile["model"]["path"])
        assert sha(model_obj.get_raw_data()) == profile["model"]["payload_sha256"]
        atlas = pointer(model_obj, profile["model"]["fields"]["atlasAssets"][0])
        assert sha(atlas.get_raw_data()) == profile["atlas"]["payload_sha256"]
        atlas_fields = profile["atlas"]["fields"]
        bindings, pages = [], []
        for mat_ref in atlas_fields["materials"]:
            obj = pointer(atlas, mat_ref)
            key = (obj.assets_file.name, str(obj.path_id))
            original = obj.read_typetree()
            main_ptr = next(value["m_Texture"] for name, value in original["m_SavedProperties"]["m_TexEnvs"] if name == "_MainTex")
            page = pointer(obj, main_ptr).read().m_Name
            assert page.endswith("_placeholder")
            pages.append(page.removesuffix("_placeholder"))
            if key not in material_cache:
                src = source(obj, original)
                shader_obj = pointer(obj, original["m_Shader"])
                assert sha(shader_obj.get_raw_data()) == shader_sha
                value = copy.deepcopy(original)
                value["m_Shader"] = dict(fileID=4800000, guid=guid(shader_path), type=3)
                texture_bindings = []
                for name, environment in value["m_SavedProperties"]["m_TexEnvs"]:
                    target = pointer(obj, environment["m_Texture"])
                    row = texture(target) if target else None
                    environment["m_Texture"] = row["binding"] if row else dict(fileID=0)
                    texture_bindings.append(dict(name=name, path=row["output"]["path"].removeprefix("unity/") if row else "",
                        scale=environment["m_Scale"], offset=environment["m_Offset"]))
                value = dict(source_header(), **fields(value), serializedVersion=8)
                value["m_SavedProperties"] = dict(serializedVersion=3,
                    **{name: [{k: v} for k, v in entries] for name, entries in value["m_SavedProperties"].items()})
                token = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/scene-material/884/" + key[0] + ":" + key[1]).hex
                path = destination / "Materials" / (token + ".mat")
                material_cache[key] = native(path, 21, 2100000, value)
                materials.append(dict(**src, output=identity(path), texture_bindings=texture_bindings))
            bindings.append(material_cache[key])
        assert len(pages) == len(set(pages))
        sex = profile["sex"]
        atlas_path = profile["model"]["path"].replace("_SkeletonData.asset", "_Atlas.asset")
        original_atlas = json_asset(destination / ("Bindings/" + sex + "-atlas-original.json"), atlas_fields)
        atlas_binding = scriptable(destination / ("Bindings/" + sex + "-atlas.asset"), atlas_fields["m_Name"], offline / "ProfessionNativeTextureAtlas.cs",
            dict(sourcePath=atlas_path, sourceGuid=by_path[atlas_path]["guid"], originalMetadata=original_atlas, materials=bindings))
        native_model = ROOT / profile["binding"]["path"]
        assert identity(native_model) == profile["binding"]
        models.append(dict(Sex=sex, Guid=by_path[profile["model"]["path"]]["guid"], Asset=reference(native_model, 11400000), Atlas=atlas_binding, Pages=pages))
    runtime = [offline / (name + ".cs") for name in ("ProfessionNativeSceneAppearanceResources", "OfflineProfessionSceneTextureResources")]
    for path in runtime:
        meta(path)
    validation = ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneTextures.cs"
    # Preserve the Editor-assigned identity already imported by this workspace.
    assert guid(validation) == "ac8d874f8c538d44da5104ab4ab9aa1b"
    catalog = ROOT / "unity/Assets/Game/Resources/Combat/SceneCharacters/appearance-resources.asset"
    scriptable(catalog, "Original current scene appearance resources", runtime[0], dict(skinGuid=skin_source["guid"], loaderGuid=loader_source["guid"],
        skins=skin_binding, loader=loader_binding, models=models, textures=[dict(Guid=g, Resource=p) for g, p in sorted(addresses.items())]))
    fixture = dict(textures=[dict(path=r["output"]["path"].removeprefix("unity/"), resource=r.get("resource", ""),
        format=r["target_format"], mips=r["mips"], original_fields=r["original_fields"]) for r in textures],
        materials=[dict(path=r["output"]["path"].removeprefix("unity/"), original_fields=r["fields"], texture_bindings=r["texture_bindings"]) for r in materials],
        mappings=mapping_fixture)
    fixture_bytes = (json.dumps(fixture, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n").encode()
    (workspace / "scene-textures-fixture.json").write_bytes(fixture_bytes)
    proof = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        core=identity(core_path), weapons=identity(weapons_path), shader=identity(shader_path), previous_texture_receipts=old_receipts,
        asset_table_sha256=sha(table_raw), sources=sources, textures=textures, materials=materials, reused=reused,
        models=models, texture_addresses=[dict(Guid=g, Resource=p) for g, p in sorted(addresses.items())],
        outputs=[identity(p) for p in outputs], runtime=[identity(p) for p in runtime], validation=identity(validation),
        validation_assembly=identity(validation.parent / "Zjcs.Editor.Combat.AnimatorPlayback.asmdef"), generator=identity(Path(__file__).resolve()),
        helpers=[identity(ROOT / "scripts" / name) for name in ("prepare_battle_scene_appearance_runtime.py", "prepare_unity_profession_reward_sprites.py", "prepare_unity_map13_native_scene.py")],
        decoder=dict(version="0.1.12", binaries=[identity(p) for p in sorted(Path(astc_encoder.__file__).parent.rglob("*.pyd"))]),
        fixture_sha256=sha(fixture_bytes),
        operation="APK-first every current original scene Low GUID and placeholder mapping, complete skin configuration and both ordered atlases/materials. Decode original ASTC mips to native Windows RGBA32 without re-compression; preserve texture flags, color spaces and samplers. Bind exact current shader bytes. Lazy per-page Resources addresses retain original asset GUID requests, separate from historical High resources.",
        limits=["Production scene/Mecanim/cache, physics/mixing and battle/Windows Player integration are not supplied by these resource bindings."])
    output = ROOT / "assets/reference/battle-animation/scene-texture-source-manifest.json"
    write_changed(output, json.dumps(proof, ensure_ascii=False, indent=2) + "\n")
    print("Restored current scene resources:", len(mappings), "Low mappings,", len(addresses), "lazy texture GUIDs,", len(configs), "skin configs,", len(materials), "native original materials.", flush=True)


if __name__ == "__main__":
    main()
