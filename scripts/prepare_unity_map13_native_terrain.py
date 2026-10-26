"""Install original native Map13 tile/chunk assets with explicit C# bindings."""
from pathlib import Path
from collections import Counter
import io
import json
import re

from official_apk import Apk, ROOT
import astc_encoder
from PIL import Image
from UnityPy.enums import TextureFormat
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_native import ROOTS
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


CACHE = ROOT / ".tools/map13-native-cache-871_156598"
EXPORTED = CACHE / "export/ExportedProject/Assets"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativeTerrain"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-terrain-source-manifest.json"
GUID = re.compile(r"guid:\s*([a-f0-9]{32})")


def guid_for(path):
    return __import__("hashlib").md5(("zjcs-map13-native-terrain:" + path.as_posix()).encode()).hexdigest()


def closure():
    index = {}
    for path in EXPORTED.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index: raise ValueError("Duplicate native export GUID")
            index[match[1]] = path.with_suffix("")
    roots = sorted((EXPORTED / "Partition/MapGround_13/Chunks").glob("*.asset"))
    if len(roots) != 200: raise ValueError("Original Map13 tile chunk count differs")
    roots.append(EXPORTED / "MonoBehaviour/partition_data.asset")
    pending, files = roots[:], set()
    while pending:
        path = pending.pop()
        if path in files: continue
        if not path.is_file(): raise FileNotFoundError(path)
        files.add(path)
        if path.suffix in (".cs", ".dll"): continue
        for source in [path, Path(str(path) + ".meta")]:
            if source.suffix not in (".meta", ".asset", ".prefab", ".mat", ".controller", ".anim"): continue
            for guid in GUID.findall(source.read_text(encoding="utf8")):
                dependency = index.get(guid)
                if dependency is not None: pending.append(dependency)
                elif guid not in ("00000000000000000000000000000000", "0000000000000000e000000000000000", "0000000000000000f000000000000000"):
                    raise ValueError("Unresolved native terrain GUID: " + guid)
    return roots, sorted(files), index


def decode_texture(texture):
    if texture.m_MipCount != 1: raise ValueError("Restore the original mip chain before converting this texture")
    name = TextureFormat(texture.m_TextureFormat).name
    match = re.search(r"ASTC_(?:RGB_|RGBA_)?(\d+)x(\d+)$", name)
    if not match: raise ValueError("Recheck native terrain texture format: " + name)
    block_x, block_y = map(int, match.groups())
    profile = astc_encoder.ASTCProfile.LDR_SRGB if texture.m_ColorSpace == 1 else astc_encoder.ASTCProfile.LDR
    config = astc_encoder.ASTCConfig(profile, block_x, block_y, block_z=1, quality=100,
        flags=astc_encoder.ASTCConfigFlags.USE_DECODE_UNORM8 | astc_encoder.ASTCConfigFlags.DECOMPRESS_ONLY)
    context = astc_encoder.ASTCContext(config)
    image = astc_encoder.ASTCImage(astc_encoder.ASTCType.U8, texture.m_Width, texture.m_Height, 1)
    raw = texture.get_image_data()
    size = ((texture.m_Width + block_x - 1) // block_x) * ((texture.m_Height + block_y - 1) // block_y) * 16
    if len(raw) != size: raise ValueError("Unexpected ASTC bytes beyond the original mip")
    context.decompress(raw, image, astc_encoder.ASTCSwizzle.from_str("RGBA"))
    output = Image.frombytes("RGBA", (texture.m_Width, texture.m_Height), image.data).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    stream = io.BytesIO(); output.save(stream, format="PNG")
    return stream.getvalue(), sha(output.tobytes()), profile.name


def texture_meta(text, texture):
    settings = texture.m_TextureSettings
    values = {"enableMipMap": 0, "sRGBTexture": int(texture.m_ColorSpace == 1),
        "isReadable": int(texture.m_IsReadable), "alphaIsTransparency": 0,
        "filterMode": int(settings.m_FilterMode), "aniso": int(settings.m_Aniso),
        "mipBias": settings.m_MipBias, "wrapU": int(settings.m_WrapU),
        "wrapV": int(settings.m_WrapV), "wrapW": int(settings.m_WrapW),
        "textureCompression": 0}
    for key, value in values.items():
        text, count = re.subn(r"(?m)^(\s*" + key + r": )[^\n]*", lambda m: m[1] + str(value), text)
        if not count: raise ValueError("Native texture importer is missing " + key)
    minimum = 1 << (max(texture.m_Width, texture.m_Height) - 1).bit_length()
    text = re.sub(r"(?m)^(\s*maxTextureSize: )(\d+)", lambda m: m[1] + str(max(int(m[2]), minimum)), text)
    return text


def build():
    apk = Apk.current()
    if apk.version != "871_156598": raise ValueError("Rebuild the native export for the newly selected version")
    input_receipt = json.loads((CACHE / "inputs/source-manifest.json").read_text(encoding="utf8"))
    for record in input_receipt["inputs"]:
        if sha((CACHE / "inputs" / record["output"]).read_bytes()) != record["converter_input_sha256"]:
            raise ValueError("Native export source bytes differ: " + record["name"])
    roots, files, index = closure()
    bindings_path = CACHE / "script-bindings.json"
    bindings = {r["original_type"]: r for r in json.loads(bindings_path.read_text())["scripts"]}
    replacements, scripts = {}, {}
    for path in files:
        old_guid = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]
        if path.suffix == ".cs":
            name = path.stem
            if name not in bindings: raise ValueError("Missing actual Unity script binding: " + name)
            binding = bindings[name]
            if binding["script_file_id"] != 11500000: raise ValueError("Unexpected native terrain script file ID")
            native_meta = ROOT / "unity" / (binding["script_asset"] + ".meta")
            if GUID.search(native_meta.read_text())[1] != binding["script_guid"]:
                raise ValueError("Native MonoScript identity changed")
            replacements[old_guid] = binding["script_guid"]
            scripts[old_guid] = binding
        else: replacements[old_guid] = guid_for(path.relative_to(EXPORTED))
    _, missing = collect(apk, ROOTS)
    if missing: raise ValueError("Original native terrain CAB closure is incomplete")
    textures = {}
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                if obj.type.name != "Texture2D": continue
                texture = obj.read()
                textures.setdefault((texture.m_Name, texture.m_Width, texture.m_Height), []).append((obj, texture))
    def remap(text):
        return GUID.sub(lambda m: "guid: " + replacements.get(m[1], m[1]), text)
    outputs, texture_records, exports = [], [], []
    for path in files:
        if path.suffix == ".cs": continue
        relative = path.relative_to(EXPORTED)
        target = DESTINATION / relative
        raw = path.read_bytes()
        metadata = Path(str(path) + ".meta").read_text(encoding="utf8")
        exports.append(dict(path=relative.as_posix(), sha256=sha(raw), meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
        if path.suffix == ".png":
            size = Image.open(io.BytesIO(raw)).size
            candidates = textures.get((path.stem, *size), [])
            if len(candidates) != 1: raise ValueError("Original texture object is not uniquely identified: " + path.name)
            obj, texture = candidates[0]
            raw, pixels, profile = decode_texture(texture)
            metadata = texture_meta(metadata, texture)
            texture_records.append(dict(path=source_path(target), source=apk.provenance(obj),
                object_sha256=sha(obj.get_raw_data()), astc_sha256=sha(texture.get_image_data()),
                rgba_sha256=pixels, profile=profile, width=size[0], height=size[1],
                mip_count=texture.m_MipCount, sRGB=texture.m_ColorSpace == 1,
                filter=int(texture.m_TextureSettings.m_FilterMode), wrapU=int(texture.m_TextureSettings.m_WrapU),
                wrapV=int(texture.m_TextureSettings.m_WrapV), aniso=int(texture.m_TextureSettings.m_Aniso),
                mipBias=texture.m_TextureSettings.m_MipBias))
        elif path.suffix == ".asset": raw = remap(raw.decode("utf8")).encode()
        else: raise ValueError("Unexpected native tile dependency: " + str(path))
        metadata = re.sub(r"(?m)^timeCreated: \d+\n", "", metadata)
        write(target, raw)
        write(Path(str(target) + ".meta"), remap(metadata).encode())
        outputs.append(dict(path=source_path(target), sha256=sha(raw), meta_sha256=sha(Path(str(target) + ".meta").read_bytes())))
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]: meta(folder, folder=True)
    backend = Path(astc_encoder.__file__).parent
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=apk.version,
        converter_input_receipt_sha256=sha((CACHE / "inputs/source-manifest.json").read_bytes()),
        roots=[p.relative_to(EXPORTED).as_posix() for p in roots],
        exported_inputs=exports, script_bindings=list(scripts.values()), textures=texture_records, outputs=outputs,
        counts=dict(chunks=200, assets=len(outputs), textures=len(texture_records)),
        decoder=dict(package="astc_encoder", version="0.1.12", reference="https://github.com/ARM-software/astc-encoder/blob/main/Source/astcenc.h",
            binaries=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sorted(backend.rglob("*.pyd"))]),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Original native tile/chunk/partition assets and exact GUID dependency closure. Rebind only selected official C# MonoScripts to imported equivalents; retain actual Unity built-in Tile script 13312. Decode original ASTC with its original linear/sRGB profile to RGBA PNG, disable further texture compression/downscaling/alpha color dilation, and retain source filter/wrap/mip settings. Preserve original Sprite meshes, pivots, UVs and tile references.",
        limitations=["Native scene, partition/environment runtime and rendering bindings remain required. Imported asset loading, Play Mode and rendered gameplay must be validated separately."])))
    print("Installed original Map13 native terrain:", len(outputs), "assets;", len(texture_records), "textures")


if __name__ == "__main__": build()
