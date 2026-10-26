"""Restore the original uncompressed renderer/post-process textures as native assets."""
from pathlib import Path
import hashlib
import json
import re

from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_native_scene import native_texture
from prepare_unity_map13_water_components import import_meta


DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/NativePipeline"


def build():
    config_path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-render-pipeline.json"
    config = json.loads(config_path.read_text(encoding="utf8"))["objects"]
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    file = next(f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers.assets")
    template_path = ROOT / ".tools/map13-native-cache-871_156598/native-texture-template.asset"
    template = template_path.read_text(encoding="utf8")
    ids = {16, 40, 45}
    def collect(value):
        if isinstance(value, dict):
            if "m_PathID" in value:
                if value["m_FileID"] != 0: raise ValueError("Resolve original external pipeline texture")
                if value["m_PathID"]: ids.add(value["m_PathID"])
            else:
                for item in value.values(): collect(item)
        elif isinstance(value, list):
            for item in value: collect(item)
    collect(config["22211"]["textures"])
    if len(ids) != 47: raise ValueError("Original renderer texture dependency closure changed")
    inputs, outputs = [], []
    for pid in sorted(ids):
        obj = file.objects[pid]
        if obj.type.name != "Texture2D": raise ValueError("Original pipeline texture identity differs")
        texture = obj.read(); data = texture.get_image_data()
        channels = {1: 1, 3: 3, 4: 4}.get(texture.m_TextureFormat)
        if channels is None or texture.m_MipCount != 1 or len(data) != texture.m_Width * texture.m_Height * channels:
            raise ValueError("Restore original pipeline texture format/mips explicitly: " + texture.m_Name)
        text = native_texture(template, texture, data).decode()
        text, count = re.subn(r"(?m)^  m_TextureFormat: 4$", "  m_TextureFormat: " + str(texture.m_TextureFormat), text)
        if count != 1: raise ValueError("Native texture format field is not unique")
        path = DESTINATION / "Textures" / (str(pid) + "-" + texture.m_Name + ".texture.asset")
        write(path, text.encode())
        guid = hashlib.md5(("zjcs-map13-native:" + source_path(path)).encode()).hexdigest()
        metadata = Path(str(path) + ".meta")
        if metadata.exists() and re.search(r"(?m)^guid: (\w+)$", metadata.read_text(encoding="utf8"))[1] != guid:
            raise ValueError("Refusing unrelated native texture identity: " + str(metadata))
        write(metadata, ("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
        inputs.append(dict(path_id=pid, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), pixel_sha256=sha(data)))
        gpu_data = data
        outputs.append(dict(path_id=pid, path=source_path(path), sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + ".meta").read_bytes()),
            width=texture.m_Width, height=texture.m_Height, format=texture.m_TextureFormat, mip_count=texture.m_MipCount,
            sRGB=texture.m_ColorSpace == 1, readable=texture.m_IsReadable, pixel_sha256=sha(data), byte_count=len(data), gpu_sha256=sha(gpu_data), gpu_byte_count=len(gpu_data),
            filter=texture.m_TextureSettings.m_FilterMode, wrapU=texture.m_TextureSettings.m_WrapU,
            wrapV=texture.m_TextureSettings.m_WrapV, wrapW=texture.m_TextureSettings.m_WrapW,
            aniso=texture.m_TextureSettings.m_Aniso, mipBias=texture.m_TextureSettings.m_MipBias))
    for folder in [DESTINATION, DESTINATION / "Textures"]: import_meta(folder, "DefaultImporter", folder=True)
    receipt = ROOT / "assets/reference/official-map/map13-pipeline-textures-source-manifest.json"
    write(receipt, encode(dict(format=1, mapClassId=13, manifest=apk.version, inputs=inputs, outputs=outputs,
        configuration=dict(path=source_path(config_path), sha256=sha(config_path.read_bytes())),
        serialization_reference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__), ROOT / "scripts/prepare_unity_map13_native_scene.py"]],
        operation="Restore all 47 original renderer/post-process texture dependencies with original Alpha8/RGB24/RGBA32 formats and every raw byte unchanged. Preserve dimensions, mip count, color space, sampler fields and readability. Use independent path-ID-prefixed assets for same-name 16/64 pixel noise textures; no decoding, row flip, mip generation, compression or texture reimport is performed.",
        limitations=["Unity native import, GPU sampling and explicit renderer/post-process bindings require separate validation."])))
    print("Restored original renderer/post-process textures: 47 native assets, unchanged original uncompressed pixels")


if __name__ == "__main__": build()
