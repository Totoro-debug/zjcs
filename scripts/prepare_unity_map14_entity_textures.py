"""Restore original entity-material textures by CAB/PathID, including every mip.

Empty dynamic font atlases retain their original descriptors; they are not
replaced with fabricated pixels. This does not install entity prefabs.
"""
from pathlib import Path
from collections import Counter
import argparse
import gc
import hashlib
import json
import re

from prepare_unity_map14_entity_inputs import CACHE, RECEIPT as INPUT_RECEIPT
from prepare_unity_map14_entity_materials import OUTPUT as MATERIALS, RECEIPT as MATERIAL_RECEIPT
from prepare_unity_map14_sources import encode, sha, source_path, write
from prepare_unity_map13_native_scene import native_texture, decode_mips as decode_astc
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_water_components import import_meta
from official_apk import ROOT
from UnityPy.enums import TextureFormat
import UnityPy
import astc_encoder
import texture2ddecoder
from PIL import Image


DESTINATION = ROOT / "unity/Assets/Official/World/Map/14/NativeEntityTextures"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-textures-source-manifest.json"
PREPARED = CACHE / "native-material-textures"
PREPARED_RECEIPT = CACHE / "native-material-textures-source-manifest.json"


def decode(texture):
    format_ = TextureFormat(texture.m_TextureFormat).name
    if format_.startswith("ASTC_"):
        raw, mips, profile = decode_astc(texture)
        return raw, mips, profile, 4
    if format_ not in ("Alpha8", "RGBA32", "ETC2_RGBA8"):
        raise ValueError("Restore this original material texture format explicitly: " + format_)
    original = texture.get_image_data()
    raw, mips, offset = bytearray(), [], 0
    for level in range(texture.m_MipCount):
        width, height = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
        size = ((width + 3) // 4) * ((height + 3) // 4) * 16 if format_ == "ETC2_RGBA8" else width * height * (1 if format_ == "Alpha8" else 4)
        block = original[offset:offset + size]
        if len(block) != size:
            raise ValueError("Truncated original material texture mip")
        if format_ == "ETC2_RGBA8":
            bgra = texture2ddecoder.decode_etc2a8(block, width, height)
            rgba = bytearray(bgra)
            rgba[0::4], rgba[2::4] = bgra[2::4], bgra[0::4]
            pixels = bytes(rgba)
        else:
            pixels = block
        if len(pixels) != width * height * (1 if format_ == "Alpha8" else 4):
            raise ValueError("Decoded original material texture mip size differs")
        mips.append(dict(level=level, width=width, height=height, source_offset=offset,
            source_size=size, source_sha256=sha(block), rgba_offset=len(raw), rgba_size=len(pixels), rgba_sha256=sha(pixels)))
        raw.extend(pixels); offset += size
    if offset != len(original):
        raise ValueError("Original material texture has unconsumed mip bytes")
    return bytes(raw), mips, "ETC2_RGBA8_UNORM" if format_ == "ETC2_RGBA8" else "Original" + format_, 1 if format_ == "Alpha8" else 4


def build(destination=DESTINATION, receipt_path=RECEIPT):
    inputs = json.loads(INPUT_RECEIPT.read_bytes())
    material_proof = json.loads(MATERIAL_RECEIPT.read_bytes())
    materials = json.loads(MATERIALS.read_bytes())
    if any(data["mapClassId"] != 14 or data["manifest"] != "871_156598" for data in [inputs, material_proof, materials]):
        raise ValueError("Wrong original Map14 texture source version")
    for record in material_proof["outputs"]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]:
            raise ValueError("Original material texture pointer source changed")
    wanted = {(p["cab"].casefold(), p["path_id"]) for m in materials["materials"] for p in m["pointers"] if p["type"] == "Texture2D"}
    if len(wanted) != 1650:
        raise ValueError("Original material texture inventory differs")
    template_path = ROOT / ".tools/map14-native-cache-871_156598/native-texture-template.asset"
    template = template_path.read_text(encoding="utf8")
    outputs, textures, empty, used = [], [], [], []
    seen = set(); counts = Counter()
    for index, entry in enumerate(inputs["inputs"]):
        path = CACHE / "inputs" / entry["output"]
        data = path.read_bytes()
        if sha(data) != entry["converter_input_sha256"]:
            raise ValueError("Original texture input changed: " + entry["output"])
        environment = UnityPy.load(data)
        used_input = False
        for file in (f for bundle in environment.files.values() for f in serialized_files(bundle)):
            cab = "unity default resources" if entry["output"] == "unity default resources" else file.name
            for obj in file.objects.values():
                key = cab.casefold(), str(obj.path_id)
                if key not in wanted:
                    continue
                if key in seen or obj.type.name != "Texture2D":
                    raise ValueError("Original material texture identity is invalid or duplicated")
                seen.add(key); used_input = True
                texture = obj.read(check_read=True)
                fields = obj.read_typetree(check_read=True)
                header = {k: v for k, v in fields.items() if k != "image data"}
                original_pixels = bytes(fields["image data"])
                counts[TextureFormat(texture.m_TextureFormat).name] += 1
                identity = dict(cab=cab, path_id=str(obj.path_id), name=texture.m_Name,
                    object_sha256=sha(obj.get_raw_data()), input=entry["output"], fields=header)
                if texture.m_Width == 0 or texture.m_Height == 0:
                    if texture.m_Width != 0 or texture.m_Height != 0 or original_pixels or texture.m_StreamData.size:
                        raise ValueError("Original empty dynamic atlas has unexpected pixel data")
                    empty.append(identity)
                    continue
                if texture.m_PlatformBlob:
                    raise ValueError("Resolve original platform-specific texture layout before conversion")
                raw, mips, profile, native_format = decode(texture)
                if TextureFormat(texture.m_TextureFormat).name == "ETC2_RGBA8":
                    reference = CACHE / "export/ExportedProject/Assets/Effect/textures/Fx_Guangyun_LX_038.png"
                    image = Image.open(reference).convert("RGBA")
                    if image.size != (texture.m_Width, texture.m_Height) or image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes() != raw[:texture.m_Width * texture.m_Height * 4]:
                        raise ValueError("Independent original ETC2 export and native base mip differ")
                    identity["independent_base_mip_reference"] = dict(path=source_path(reference), sha256=sha(reference.read_bytes()))
                text = native_texture(template, texture, raw).decode("utf8")
                values = dict(m_TextureFormat=native_format, m_Name=json.dumps(texture.m_Name, ensure_ascii=False),
                    m_ForcedFallbackFormat=texture.m_ForcedFallbackFormat,
                    m_DownscaleFallback=int(texture.m_DownscaleFallback), m_IsAlphaChannelOptional=int(texture.m_IsAlphaChannelOptional))
                for name, value in values.items():
                    text, n = re.subn(r"(?m)^(  " + name + r":)[^\n]*", lambda match: match[1] + " " + str(value), text)
                    if n != 1: raise ValueError("Native texture schema field is not unique: " + name)
                token = hashlib.md5(cab.casefold().encode()).hexdigest()
                target = destination / token / (str(obj.path_id) + ".texture.asset")
                guid = hashlib.md5(("zjcs-map14-original-material-texture:" + cab.casefold() + ":" + str(obj.path_id)).encode()).hexdigest()
                write(target, text.encode("utf8"))
                write(Path(str(target) + ".meta"), ("fileFormatVersion: 2\nguid: " + guid +
                    "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
                record = dict(path=source_path(target), sha256=sha(target.read_bytes()), meta_sha256=sha(Path(str(target) + ".meta").read_bytes()))
                outputs.append(record)
                textures.append(dict(**identity, path=record["path"], native_format=native_format,
                    raw_sha256=sha(raw), source_pixels_sha256=sha(texture.get_image_data()), profile=profile, mips=mips))
        if used_input:
            used.append(dict(name=entry["name"], origin=entry["origin"], output=entry["output"],
                raw_sha256=entry["raw_sha256"], decoded_sha256=entry["decoded_sha256"], converter_input_sha256=entry["converter_input_sha256"]))
        if index % 64 == 0:
            gc.collect()
            print("Original material texture inputs", index, "native textures", len(textures), flush=True)
    if seen != wanted or len(textures) != 1644 or len(empty) != 6:
        raise ValueError("Original material texture dependency closure is incomplete")
    for folder in [destination, *sorted(p for p in destination.rglob("*") if p.is_dir())]:
        import_meta(folder, "DefaultImporter", folder=True)
    write(receipt_path, encode(dict(format=1, mapClassId=14, manifest=inputs["manifest"], inputs=used,
        input_receipt=dict(path=source_path(INPUT_RECEIPT), sha256=sha(INPUT_RECEIPT.read_bytes())),
        material_receipt=dict(path=source_path(MATERIAL_RECEIPT), sha256=sha(MATERIAL_RECEIPT.read_bytes())),
        material_source=dict(path=source_path(MATERIALS), sha256=sha(MATERIALS.read_bytes())),
        serialization_reference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        counts=dict(original_textures=len(wanted), native_textures=len(textures), empty_dynamic_atlases=len(empty),
            mips=sum(len(t["mips"]) for t in textures), source_formats=dict(counts)),
        textures=textures, empty_dynamic_atlases=empty, outputs=outputs,
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sorted(
            (ROOT / "unity/Assets/Game/Editor/Map14/EntityTextures").glob("*.cs"))],
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "scripts/prepare_unity_map13_native_scene.py", Path(texture2ddecoder.__file__)]] +
            [dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sorted(Path(astc_encoder.__file__).parent.rglob("*.pyd"))] +
            [dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sorted(Path(texture2ddecoder.__file__).parent.rglob("*.pyd"))],
        operation="Resolve original material texture pointers by exact CAB/PathID. Decode every ASTC mip with the original linear/sRGB profile, decode ETC2_RGBA8 into RGBA32, and preserve native Alpha8/RGBA32 bytes. Keep original bottom-left row order, dimensions, mips, color space, sampler, readability and fallback settings. No PNG resampling, row flip, generated mips, color dilation or lossy recompression. Preserve original zero-size dynamic font atlas descriptors instead of fabricating image data.",
        limitations=["Native import, complete GPU mip readback, material/prefab binding and actual entity rendering remain required.",
            "The six empty original dynamic font atlases require their original font initialization chain before material binding.",
            "Dynamic texture-loader variants not referenced by original serialized material pointers are outside this projection."])))
    print("Restored original material textures:", len(textures), "native assets;", len(empty), "empty original font atlas descriptors", flush=True)


def install_prepared():
    proof = json.loads(PREPARED_RECEIPT.read_bytes())
    if proof["mapClassId"] != 14 or proof["manifest"] != "871_156598":
        raise ValueError("Wrong prepared native material texture source")
    for record in [proof["input_receipt"], proof["material_receipt"], proof["material_source"], proof["generator"],
                   proof["serialization_reference"], *proof["validation"], *proof["converters"]]:
        if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]:
            raise ValueError("Prepared native material texture dependency changed: " + record["path"])
    for record in proof["outputs"]:
        source = ROOT / record["path"]
        relative = source.relative_to(PREPARED)
        if sha(source.read_bytes()) != record["sha256"] or sha(Path(str(source) + ".meta").read_bytes()) != record["meta_sha256"]:
            raise ValueError("Prepared native material texture bytes changed")
        target = DESTINATION / relative
        write(target, source.read_bytes()); write(Path(str(target) + ".meta"), Path(str(source) + ".meta").read_bytes())
        record["path"] = source_path(target)
    for record in proof["textures"]:
        record["path"] = source_path(DESTINATION / (ROOT / record["path"]).relative_to(PREPARED))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]:
        import_meta(folder, "DefaultImporter", folder=True)
    proof["prepared_source"] = dict(path=source_path(PREPARED_RECEIPT), sha256=sha(PREPARED_RECEIPT.read_bytes()))
    write(RECEIPT, encode(proof))
    print("Installed", len(proof["outputs"]), "byte-verified prepared native material textures", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--prepare-only", action="store_true")
    mode.add_argument("--install-prepared", action="store_true")
    args = parser.parse_args()
    if args.prepare_only: build(PREPARED, PREPARED_RECEIPT)
    elif args.install_prepared: install_prepared()
    else: build()
