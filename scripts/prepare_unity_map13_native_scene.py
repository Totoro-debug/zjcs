"""Bind the original bare prefab and its exact native dependency closure."""
from pathlib import Path
import json
import re
import io

from official_apk import Apk, ROOT
import astc_encoder
from PIL import Image
from UnityPy.enums import TextureFormat
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_native import ROOTS
from prepare_unity_map13_native_terrain import CACHE, EXPORTED, GUID, closure, guid_for
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativeScene"


def scene_closure(index):
    pending = [EXPORTED / "Partition/MapGround_13/MapGround_13_bare.prefab"]
    found = set()
    while pending:
        path = pending.pop()
        if path in found: continue
        if not path.is_file(): raise FileNotFoundError(path)
        found.add(path)
        if path.suffix in (".cs", ".shader"): continue
        for source in [path, Path(str(path) + ".meta")]:
            if source.suffix not in (".asset", ".mat", ".prefab", ".meta", ".controller", ".anim"): continue
            for guid in GUID.findall(source.read_text(encoding="utf8")):
                if guid in index: pending.append(index[guid])
                elif guid not in ("00000000000000000000000000000000", "0000000000000000e000000000000000", "0000000000000000f000000000000000"):
                    raise ValueError("Unresolved original bare prefab reference: " + guid)
    return sorted(found)


def decode_mips(texture):
    name = TextureFormat(texture.m_TextureFormat).name
    match = re.fullmatch(r"ASTC_(?:RGB_|RGBA_)?(\d+)x(\d+)", name)
    if not match: raise ValueError("Recheck original native scene texture format: " + name)
    bx, by = map(int, match.groups())
    profile = astc_encoder.ASTCProfile.LDR_SRGB if texture.m_ColorSpace == 1 else astc_encoder.ASTCProfile.LDR
    config = astc_encoder.ASTCConfig(profile, bx, by, block_z=1, quality=100,
        flags=astc_encoder.ASTCConfigFlags.USE_DECODE_UNORM8 | astc_encoder.ASTCConfigFlags.DECOMPRESS_ONLY)
    decoder = astc_encoder.ASTCContext(config)
    original = texture.get_image_data(); offset = 0
    raw, records = bytearray(), []
    for level in range(texture.m_MipCount):
        width, height = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
        length = ((width + bx - 1) // bx) * ((height + by - 1) // by) * 16
        block = original[offset:offset + length]
        if len(block) != length: raise ValueError("Truncated original ASTC mip")
        image = astc_encoder.ASTCImage(astc_encoder.ASTCType.U8, width, height, 1)
        decoder.decompress(block, image, astc_encoder.ASTCSwizzle.from_str("RGBA"))
        rgba = bytes(image.data)
        if len(rgba) != width * height * 4: raise ValueError("Decoded original mip size changed")
        records.append(dict(level=level, width=width, height=height, astc_offset=offset,
                            astc_size=length, astc_sha256=sha(block), rgba_offset=len(raw), rgba_size=len(rgba), rgba_sha256=sha(rgba)))
        raw.extend(rgba); offset += length
    if offset != len(original): raise ValueError("Original texture has unconsumed mip bytes")
    return bytes(raw), records, profile.name


def native_texture(template, texture, rgba):
    if template.count("--- !u!28 &1\n") != 1: raise ValueError("Unity native texture serialization reference changed")
    text = template.replace("--- !u!28 &1\n", "--- !u!28 &2800000\n")
    values = {"m_Name": texture.m_Name, "m_Width": texture.m_Width, "m_Height": texture.m_Height,
        "m_CompleteImageSize": len(rgba), "m_MipCount": texture.m_MipCount,
        "m_IsReadable": int(texture.m_IsReadable), "m_IsPreProcessed": int(texture.m_IsPreProcessed),
        "m_IgnoreMipmapLimit": int(texture.m_IgnoreMipmapLimit), "m_MipmapLimitGroupName": texture.m_MipmapLimitGroupName,
        "m_StreamingMipmaps": int(texture.m_StreamingMipmaps), "m_StreamingMipmapsPriority": texture.m_StreamingMipmapsPriority,
        "m_ImageCount": texture.m_ImageCount, "m_TextureDimension": texture.m_TextureDimension,
        "m_FilterMode": texture.m_TextureSettings.m_FilterMode, "m_Aniso": texture.m_TextureSettings.m_Aniso,
        "m_MipBias": texture.m_TextureSettings.m_MipBias, "m_WrapU": texture.m_TextureSettings.m_WrapU,
        "m_WrapV": texture.m_TextureSettings.m_WrapV, "m_WrapW": texture.m_TextureSettings.m_WrapW,
        "m_LightmapFormat": texture.m_LightmapFormat, "m_ColorSpace": texture.m_ColorSpace,
        "image data": len(rgba), "_typelessdata": rgba.hex()}
    if texture.m_MipsStripped or texture.m_ImageCount != 1 or texture.m_TextureDimension != 2:
        raise ValueError("Recheck original native texture topology")
    for key, value in values.items():
        text, count = re.subn(r"(?m)^(\s*" + re.escape(key) + r":)[^\n]*", lambda m: m[1] + " " + str(value), text)
        if count != 1: raise ValueError("Native texture reference field is not unique: " + key)
    return text.encode()


def build():
    input_receipt_path = CACHE / "inputs/source-manifest.json"
    input_receipt = json.loads(input_receipt_path.read_text(encoding="utf8"))
    for record in input_receipt["inputs"]:
        if sha((CACHE / "inputs" / record["output"]).read_bytes()) != record["converter_input_sha256"]:
            raise ValueError("Native scene converter input changed: " + record["name"])
    apk = Apk.current(); _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Original Map13 CAB closure is incomplete")
    _, _, index = closure(); files = scene_closure(index)
    bindings = {r["original_type"]: r for r in json.loads((CACHE / "scene-script-bindings.json").read_text(encoding="utf8"))["scripts"]}
    terrain = json.loads((ROOT / "assets/reference/official-map/map13-native-terrain-source-manifest.json").read_text(encoding="utf8"))
    reused = {Path(r["path"]).relative_to("unity/Assets/Official/World/Map/13/NativeTerrain").as_posix(): r for r in terrain["outputs"]}
    shaders = {}
    for receipt_name in ["map13-water-shaders-source-manifest.json", "map13-scene-shaders-source-manifest.json"]:
        receipt = json.loads((ROOT / "assets/reference/official-map" / receipt_name).read_text(encoding="utf8"))
        for output in receipt["outputs"]:
            path = ROOT / output["path"]
            if sha(path.read_bytes()) != output["sha256"]: raise ValueError("Translated shader output changed")
            shaders[output["original_shader"]] = path
    replacements, script_records = {}, []
    for path in files:
        old = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
        if path.suffix == ".cs":
            binding = bindings[path.stem]
            if binding["script_file_id"] != 11500000: raise ValueError("Original scene script file ID changed")
            target = ROOT / "unity" / (binding["script_asset"] + ".meta")
            if GUID.search(target.read_text(encoding="utf8"))[1] != binding["script_guid"]:
                raise ValueError("Actual scene MonoScript identity changed")
            replacements[old] = binding["script_guid"];script_records.append(binding)
        elif path.suffix == ".shader":
            name = re.search(r'Shader\s+"([^"]+)"', path.read_text(encoding="utf8"))[1]
            target = shaders[name]
            replacements[old] = GUID.search(Path(str(target) + ".meta").read_text(encoding="utf8"))[1]
        else: replacements[old] = guid_for(path.relative_to(EXPORTED))
    textures = {}
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                if obj.type.name == "Texture2D":
                    texture = obj.read();textures.setdefault((texture.m_Name, texture.m_Width, texture.m_Height), []).append((obj, texture))
    template_path = CACHE / "native-texture-template.asset";template = template_path.read_text(encoding="utf8")
    native_texture_guids = {guid_for(p.relative_to(EXPORTED)) for p in files
                            if p.suffix == ".png" and p.relative_to(EXPORTED).as_posix() not in reused}
    def remap(text):
        text = GUID.sub(lambda m: "guid: " + replacements.get(m[1], m[1]), text)
        # PNG importer references use type 3; native serialized Texture2D assets
        # use type 2. Retain the original object ID and exact texture GUID.
        return re.sub(r"(\{fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3(\})",
                      lambda m: m[1] + m[2] + m[3] + ("2" if m[2] in native_texture_guids else "3") + m[4], text)
    outputs, reused_records, exported, texture_records = [], [], [], []
    for path in files:
        if path.suffix in (".cs", ".shader"): continue
        relative = path.relative_to(EXPORTED)
        if relative.as_posix() in reused:
            record = reused[relative.as_posix()]
            if sha((ROOT / record["path"]).read_bytes()) != record["sha256"]: raise ValueError("Existing native terrain payload changed")
            reused_records.append(record);continue
        raw = path.read_bytes();metadata = Path(str(path) + ".meta").read_text(encoding="utf8")
        exported.append(dict(path=relative.as_posix(), sha256=sha(raw), meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
        target = DESTINATION / relative
        if path.suffix == ".png":
            size = Image.open(io.BytesIO(raw)).size;candidates = textures.get((path.stem, *size), [])
            if len(candidates) != 1: raise ValueError("Original native scene texture is not unique: " + path.name)
            obj, texture = candidates[0];rgba, mips, profile = decode_mips(texture)
            # Keep native Sprite .asset files with the same original image stem.
            target = target.with_name(target.stem + ".texture.asset");raw = native_texture(template, texture, rgba)
            metadata = "fileFormatVersion: 2\nguid: " + guid_for(relative) + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
            texture_records.append(dict(path=source_path(target), source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                astc_sha256=sha(texture.get_image_data()), rgba_sha256=sha(rgba), profile=profile, mips=mips,
                width=texture.m_Width, height=texture.m_Height, mip_count=texture.m_MipCount, sRGB=texture.m_ColorSpace == 1,
                filter=texture.m_TextureSettings.m_FilterMode, wrapU=texture.m_TextureSettings.m_WrapU,
                wrapV=texture.m_TextureSettings.m_WrapV, wrapW=texture.m_TextureSettings.m_WrapW,
                aniso=texture.m_TextureSettings.m_Aniso, mipBias=texture.m_TextureSettings.m_MipBias, readable=texture.m_IsReadable))
        elif path.suffix in (".asset", ".prefab", ".mat"): raw = remap(raw.decode("utf8")).encode()
        else: raise ValueError("Restore original bare prefab dependency: " + str(relative))
        metadata = re.sub(r"(?m)^timeCreated: \d+\n", "", metadata)
        write(target, raw);write(Path(str(target) + ".meta"), remap(metadata).encode())
        outputs.append(dict(path=source_path(target), sha256=sha(raw), meta_sha256=sha(Path(str(target) + ".meta").read_bytes())))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]: meta(folder, folder=True)
    if len({o["path"] for o in outputs}) != len(outputs): raise ValueError("Native scene output paths collide")
    write(ROOT / "assets/reference/official-map/map13-native-scene-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version, root="Assets/Partition/MapGround_13/MapGround_13_bare.prefab",
        exported_inputs=exported, reused_terrain=reused_records, script_bindings=script_records, textures=texture_records, outputs=outputs,
        converter_input_receipt_sha256=sha(input_receipt_path.read_bytes()),
        decoder=dict(package="astc_encoder", version="0.1.12", binaries=[dict(path=source_path(p), sha256=sha(p.read_bytes()))
            for p in sorted(Path(astc_encoder.__file__).parent.rglob("*.pyd"))]),
        serialization_reference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Restore original bare prefab dependency closure, reuse byte-verified native terrain, rebind actual official MonoScripts and mechanically translated shaders. Decode every original ASTC mip with its exact linear/sRGB profile into native RGBA32 Texture2D assets using Unity's actual serialization schema. Raw mip rows retain Unity's original bottom-left order, with no PNG flip, resampling, color dilation or additional compression.",
        limitations=["Unity import/reference validation, original render pipeline binding, native partition scenes/extensions, actual map rendering, UI, input and saves remain required."])))
    print("Prepared native Map13 bare scene:", len(outputs), "new assets,", len(reused_records), "reused terrain assets,", len(texture_records), "complete textures")


if __name__ == "__main__": build()
