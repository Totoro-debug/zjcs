"""Recover exact historical clothing pages without substituting current art."""
import copy
import hashlib
import json
from pathlib import Path
import re
import textwrap
import urllib.request
import uuid

from official_apk import Apk
from official_manifest import read_asset_manifest
from prepare_battle_constraint_channel_fixtures import ROOT, sha
from prepare_unity_profession_character_textures import meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
from validation_workspace import validation_directory
import UnityPy
from UnityPy.enums import TextureFormat
import yaml


def records(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from records(child)
    elif isinstance(value, list):
        for child in value:
            yield from records(child)


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def build():
    apk = Apk.current()
    workspace = validation_directory()
    receipt = ROOT / "assets/reference/official-map/map13-native-entity-input-source-manifest.json"
    old = json.loads(receipt.read_bytes())
    core_path = ROOT / "assets/reference/profession-ui/character-textures-source-manifest.json"
    core = json.loads(core_path.read_bytes())
    if old["manifest"] != "871_156598" or core["manifest"] != old["manifest"]:
        raise ValueError("Verified historical clothing source version changed")
    objects = list(records(old))
    default_name = "defaultpackage_assets_appearanceassets_character_model_common_atlas_default.bundle"
    pinned = next(row for row in objects if row.get("name") == default_name and "raw_sha256" in row)
    filename = pinned["raw_md5"] + ".bundle"
    cached = workspace / filename
    url = "https://zhangjcsdl.leiting.com/patch/202610211330871/Android/DefaultPackage/" + filename
    if not cached.exists():
        apk_entry = "assets/yoo/DefaultPackage/" + filename
        if apk_entry in apk.available:
            cached.write_bytes(apk.archive.read(apk_entry))
        else:
            with urllib.request.urlopen(url, timeout=30) as response:
                cached.write_bytes(response.read())
    raw = cached.read_bytes()
    if len(raw) != pinned["raw_size"] or sha(raw) != pinned["raw_sha256"] or hashlib.md5(raw).hexdigest() != pinned["raw_md5"]:
        raise ValueError("Original historical placeholder bundle differs")
    environment = UnityPy.load(Apk.decode(raw))
    table = {row["guid"]: row for row in read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive)) if row["guid"]}
    exported, pages = {}, []

    def export(obj, role, source):
        identity = obj.assets_file.name + ":" + str(obj.path_id)
        if identity in exported:
            return exported[identity]
        texture = obj.read()
        if texture.m_MipsStripped or texture.m_ImageCount != 1 or texture.m_TextureDimension != 2:
            raise ValueError("Inspect original clothing texture topology")
        encoded = texture.get_image_data()
        if "ASTC" in TextureFormat(texture.m_TextureFormat).name:
            pixels, mips, profile = decode_mips(texture)
            target_format = 4
        else:
            target_format = int(texture.m_TextureFormat)
            if target_format not in (1, 4):
                raise ValueError("Inspect original clothing pixel format")
            pixels, mips, profile, offset = encoded, [], "original-uncompressed", 0
            for level in range(texture.m_MipCount):
                width, height = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
                length = width * height * (1 if target_format == 1 else 4)
                chunk = pixels[offset:offset + length]
                if len(chunk) != length:
                    raise ValueError("Original clothing mip is truncated")
                mips.append(dict(level=level, width=width, height=height, rgba_size=length, rgba_sha256=sha(chunk)))
                offset += length
            if offset != len(pixels):
                raise ValueError("Original clothing pixel bytes remain unconsumed")
        original = obj.read_typetree()
        original.pop("image data")
        native = source_header()
        native.update(fields(copy.deepcopy(original)))
        native.update(serializedVersion=2, m_TextureFormat=target_format, m_CompleteImageSize=len(pixels), m_PlatformBlob="",
            m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=""))
        native["image data"] = len(pixels)
        native["_typelessdata"] = pixels.hex()
        suffix = {"main": "main", "dye": "dye", "placeholder": "placeholder"}[role]
        target = ROOT / "unity/Assets/Official/ProfessionUI/CharacterTextures" / (texture.m_Name + "." + suffix + ".texture2d.asset")
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/profession/native-texture/" + identity).hex
        output = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n" +
            yaml.dump({"Texture2D": native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode()
        if target.exists() and target.read_bytes() != output:
            raise ValueError("Existing original clothing texture differs: " + str(target))
        if not target.exists():
            target.write_bytes(output)
            meta(target, guid, 2800000)
        if re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(target) + ".meta").read_text())[1] != guid:
            raise ValueError("Original clothing texture GUID differs")
        row = dict(role=role, source=source, payload_sha256=sha(obj.get_raw_data()), encoded_sha256=sha(encoded),
            original_fields=original, profile=profile, target_format=target_format, mips=mips,
            output=record(target), meta=record(Path(str(target) + ".meta")), guid=guid)
        exported[identity] = row
        return row

    for model in core["models"]:
        sex = "male" if model["fields"]["m_Name"] == "character_Male_1_TextureLoader_High" else "female"
        for name in ("body_0_G_012", "body_0_G_014"):
            prefix = "Assets/AppearanceAssets/Character/Model/Common/"
            original = next(row for row in objects if row.get("asset", {}).get("path") == prefix + "Atlas/High/" + name + ".png")
            main_guid = original["asset"]["guid"]
            mapping = next(row for row in model["fields"]["placeholderMap"] if row["targetTextureReference"]["AssetReference"]["mAssetGuid"] == main_guid)
            placeholder, = [obj for obj in environment.objects if obj.path_id == mapping["placeholderTexture"]["m_PathID"]]
            if placeholder.read().m_Name != name + "_placeholder":
                raise ValueError("Original high-resolution placeholder identity differs")
            bound = dict(sex=sex, page=name, main_guid=main_guid,
                dye_guid=mapping["dyeTextureReference"]["AssetReference"]["mAssetGuid"])
            bound["placeholder"] = export(placeholder, "placeholder", dict(manifest=old["manifest"], bundle=default_name,
                bundle_hash=pinned["raw_md5"], bundle_sha256=sha(raw), url=url, path_id=str(placeholder.path_id)))
            for role, resource_guid in (("main", bound["main_guid"]), ("dye", bound["dye_guid"])):
                historical = next(row for row in objects if row.get("asset", {}).get("guid") == resource_guid)
                current = table[resource_guid]
                source = historical["source"]
                bundle = apk.bundle_records[current["bundle"]]
                if bundle["hash"] != source["bundle_hash"] or bundle["size"] != source["bundle_size"]:
                    raise ValueError("Current clothing bundle differs from historical source")
                entry = "assets/yoo/DefaultPackage/" + bundle["hash"] + ".bundle"
                retained = apk.archive.read(entry) if entry in apk.available else apk.cloud.read("DefaultPackage", bundle)[0]
                if sha(retained) != source["bundle_sha256"]:
                    raise ValueError("Retained clothing bytes differ from original historical bundle")
                obj = apk.asset(current["bundle"], current["path"])
                if sha(obj.get_raw_data()) != historical["payload_sha256"]:
                    raise ValueError("Original high-resolution clothing object differs")
                bound[role] = export(obj, role, dict(historical=source, retained=apk.provenance(obj), asset=historical["asset"]))
            pages.append(bound)
    catalog = ROOT / core["catalog"]["path"]
    baseline = catalog.read_text(encoding="utf8").split("  - Sex: male\n    Name: body_0_G_012\n")[0]
    if sha(baseline.encode()) != core["catalog"]["sha256"]:
        raise ValueError("Existing original character catalog changed")
    pointer = lambda row: dict(fileID=2800000, guid=row["guid"], type=2)
    entries = [dict(Sex=row["sex"], Name=row["page"], MainGuid=row["main_guid"], DyeGuid=row["dye_guid"],
        Placeholder=pointer(row["placeholder"]), Main=pointer(row["main"]), Dye=pointer(row["dye"])) for row in pages]
    catalog.write_text(baseline + textwrap.indent(yaml.dump(entries, Dumper=Dumper, sort_keys=False), "  "), "utf8", newline="\n")
    fixture = dict(total_pages=len(core["pages"]) + len(pages), pages=[dict(sex=row["sex"], page=row["page"], main_guid=row["main_guid"], dye_guid=row["dye_guid"]) for row in pages],
        textures=[dict(path=row["output"]["path"].removeprefix("unity/"), guid=row["guid"], width=row["original_fields"]["m_Width"],
            height=row["original_fields"]["m_Height"], mip_count=row["original_fields"]["m_MipCount"], texture_format=row["target_format"],
            settings=row["original_fields"]["m_TextureSettings"], mips=row["mips"]) for row in exported.values()])
    fixture_raw = (json.dumps(fixture, ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    (workspace / "local-clothing-textures.json").write_bytes(fixture_raw)
    evidence = dict(manifest=old["manifest"], historical_receipt=record(receipt), high_loader_receipt=record(core_path),
        historical_placeholder_bundle=dict(**pinned, recovered_url=url), textures=list(exported.values()),
        pages=[dict(sex=row["sex"], page=row["page"], main_guid=row["main_guid"], dye_guid=row["dye_guid"]) for row in pages],
        catalog=record(catalog), generator=record(Path(__file__).resolve()), decoder=record(ROOT / "scripts/prepare_unity_map13_native_scene.py"),
        fixture_sha256=sha(fixture_raw), operation="Recover exact original 871_156598 high-resolution main/dye/placeholder pages. Check historical bundle MD5, size, SHA256 and original high-resolution object payloads before reusing identical retained current bytes. Recover the old placeholder bundle by its original pinned SHA256 rather than substituting the changed current bundle. Preserve original mip pixels, sampler fields and high-resolution loader GUIDs; append four explicit local catalog bindings without changing existing entries.")
    output = ROOT / "assets/reference/battle-animation/local-clothing-textures-source-manifest.json"
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Recovered original local-constraint clothing:", len(pages), "page bindings,", len(exported), "textures")


if __name__ == "__main__":
    build()
