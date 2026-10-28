"""Preserve original UI texture pixels and samplers without PNG re-compression."""
import copy
import hashlib
import json
from pathlib import Path
import re
import yaml
from official_apk import Apk, ROOT
from prepare_unity_profession_native_inputs import CACHE
from UnityPy.enums import TextureFormat
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def build():
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Wrong native UI texture version")
    exports = CACHE / "typed/export/ExportedProject/Assets"
    sources = []
    for bundle, suffix in (("common", "/ImgItemTipBg.png"), ("itemquality", "/ImgQuality_White.png")):
        sprite = apk.asset("defaultpackage_assets_image_ui_" + bundle + ".bundle", suffix).read()
        rd = dict(sprite.m_SpriteAtlas.read().m_RenderDataMap)[sprite.m_RenderDataKey] if sprite.m_SpriteAtlas.path_id else sprite.m_RD
        if rd.alphaTexture.path_id:
            raise ValueError("Original atlas has a separate alpha dependency")
        sources.append(rd.texture.deref())
    fonts = json.loads((ROOT / "assets/reference/profession-ui/fonts-source-manifest.json").read_bytes())
    font_atlas = fonts["fonts"][0]["existing_atlas"]
    identity = font_atlas["source"]
    candidates = [obj for obj in apk.objects(identity["bundle"]) if str(obj.path_id) == str(identity["path_id"])]
    font_obj, = candidates
    if sha(font_obj.get_raw_data()) != font_atlas["source_payload_sha256"]:
        raise ValueError("Original font atlas changed")
    sources.extend((font_obj, apk.asset("defaultpackage_assets_image_ui_common.bundle", "/ImgMask.png")))
    outputs = []
    for obj in sources:
        texture = obj.read()
        if texture.m_MipsStripped or texture.m_ImageCount != 1 or texture.m_TextureDimension != 2:
            raise ValueError("Inspect original UI texture topology")
        original = obj.read_typetree()
        original.pop("image data")
        encoded = texture.get_image_data()
        if "ASTC" in TextureFormat(texture.m_TextureFormat).name:
            pixels, mips, profile = decode_mips(texture)
            target_format = 4
        else:
            target_format = int(texture.m_TextureFormat)
            if target_format not in (1, 4):
                raise ValueError("Inspect original uncompressed texture format")
            pixels, profile = encoded, "original-uncompressed"
            offset, mips = 0, []
            for level in range(texture.m_MipCount):
                width, height = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
                size = width * height * (1 if target_format == 1 else 4)
                mip = pixels[offset:offset + size]
                mips.append(dict(level=level, width=width, height=height, rgba_offset=offset, rgba_size=size, rgba_sha256=sha(mip)))
                offset += size
            if offset != len(pixels):
                raise ValueError("Original uncompressed mip payload was not consumed")
        matches = list(exports.rglob(texture.m_Name + ".png"))
        old, = matches
        old_meta = old.with_name(old.name + ".meta")
        asset_guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", old_meta.read_text(encoding="utf-8"))[1]
        # Every incoming native reference must remain a Texture2D reference.
        incoming = []
        for owner in exports.rglob("*"):
            if owner.suffix not in (".asset", ".mat", ".prefab"):
                continue
            for match in re.finditer(r"\{fileID: (-?\d+), guid: " + asset_guid + r", type: \d+\}", owner.read_text(encoding="utf-8")):
                if int(match[1]) != 2800000:
                    raise ValueError("PNG importer also provides a referenced Sprite: " + str(owner))
                incoming.append(owner.relative_to(exports).as_posix())
        if not incoming:
            raise ValueError("Native texture has no verified incoming reference")
        native = source_header()
        native.update(fields(copy.deepcopy(original)))
        native.update(serializedVersion=2, m_TextureFormat=target_format, m_CompleteImageSize=len(pixels),
            m_PlatformBlob="", m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=""))
        native["m_TextureSettings"]["serializedVersion"] = 2
        native["image data"] = len(pixels)
        native["_typelessdata"] = pixels.hex()
        relative = "ProfessionNativeTextures/" + texture.m_Name + ".texture2d.asset"
        target = CACHE / "typed/native-textures" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        body = yaml.dump({"Texture2D": native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        target.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n" + body, encoding="utf-8", newline="\n")
        meta = target.with_name(target.name + ".meta")
        meta.write_text("fileFormatVersion: 2\nguid: " + asset_guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n", encoding="utf-8", newline="\n")
        outputs.append(dict(**record(target), native_relative_path=relative, guid=asset_guid, meta_sha256=sha(meta.read_bytes()),
            replaces_import=dict(native_relative_path=old.relative_to(exports).as_posix(), **record(old), meta=record(old_meta)),
            source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()), encoded_sha256=sha(encoded),
            original_fields=original, profile=profile, mips=mips, pixels_sha256=sha(pixels),
            texture_format=target_format, incoming=sorted(set(incoming))))
    result = dict(format=1, manifest=apk.version, native_assets=outputs, generator=record(Path(__file__)),
        operation="Preserve original native Texture2D GUID, dimensions, mip order, color space and sampler fields. Decode original ASTC with UNORM8; keep Alpha8/RGBA32 source bytes. Replace only the converter PNG importer, avoiding a second lossy compression. Incoming fileID 2800000 references remain unchanged.")
    proof = ROOT / "assets/reference/profession-ui/native-textures-source-manifest.json"
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    fixture = dict(textures=[dict(path="Assets/Official/ProfessionUI/Native/" + row["native_relative_path"],
        width=row["original_fields"]["m_Width"], height=row["original_fields"]["m_Height"],
        mip_count=row["original_fields"]["m_MipCount"], texture_format=row["texture_format"],
        settings=row["original_fields"]["m_TextureSettings"], mips=row["mips"]) for row in outputs])
    (CACHE / "typed/native-textures-fixture.json").write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Recovered", len(outputs), "lossless native UI textures")


if __name__ == "__main__":
    build()
