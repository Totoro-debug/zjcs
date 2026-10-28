"""Restore original reward Sprite geometry and lossless Windows Texture2D assets."""
import copy
import hashlib
import json
from pathlib import Path
import yaml
from prepare_unity_profession_native_inputs import ROOT, CACHE
from UnityPy.helpers.MeshHelper import MeshHandler
from prepare_unity_map13_native_scene import decode_mips


class Dumper(yaml.SafeDumper):
    pass


def mapping(dumper, value):
    return dumper.represent_mapping("tag:yaml.org,2002:map", value.items(),
        flow_style=bool(value) and all(not isinstance(v, (dict, list)) for v in value.values()))


Dumper.add_representer(dict, mapping)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def source_header():
    return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject={"fileID": 0},
        m_PrefabInstance={"fileID": 0}, m_PrefabAsset={"fileID": 0})


def fields(value):
    if isinstance(value, dict):
        if set(value) == {"m_FileID", "m_PathID"}:
            if value["m_PathID"]:
                raise ValueError("Original reward pointer needs an explicit binding")
            return {"fileID": 0}
        return {key: fields(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [fields(item) for item in value]
    if isinstance(value, bool):
        return int(value)
    return value


def write(kind, name, source, native):
    relative = "ProfessionDynamic/" + name + "." + kind.lower() + ".asset"
    path = CACHE / "typed/dynamic" / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    main_id = 2800000 if kind == "Texture2D" else 21300000
    class_id = 28 if kind == "Texture2D" else 213
    identity = source.assets_file.name + ":" + str(source.path_id)
    asset_guid = hashlib.md5(("zjcs:profession:original-native:" + identity).encode()).hexdigest()
    body = yaml.dump({kind: native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    raw = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!" + str(class_id) + " &" + str(main_id) + "\n" + body).encode()
    path.write_bytes(raw)
    meta = path.with_name(path.name + ".meta")
    meta.write_text("fileFormatVersion: 2\nguid: " + asset_guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: " + str(main_id) + "\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
    return dict(path=path.relative_to(ROOT).as_posix(), native_relative_path=relative,
        guid=asset_guid, sha256=sha(raw), meta_sha256=sha(meta.read_bytes()), file_id=main_id)


def build(apk, obj):
    sprite = obj.read()
    if sprite.m_SpriteAtlas.path_id or sprite.m_RD.alphaTexture.path_id or sprite.m_RD.secondaryTextures or sprite.m_Bones or sprite.m_PhysicsShape:
        raise ValueError("Restore additional original reward Sprite dependencies")
    texture_obj = sprite.m_RD.texture.deref()
    texture = texture_obj.read()
    if texture.m_MipsStripped or texture.m_ImageCount != 1 or texture.m_TextureDimension != 2:
        raise ValueError("Inspect the original reward texture topology")
    rgba, mips, profile = decode_mips(texture)
    original_texture = texture_obj.read_typetree()
    original_texture.pop("image data")
    native_texture = source_header()
    native_texture.update(fields(copy.deepcopy(original_texture)))
    native_texture.update(serializedVersion=2, m_TextureFormat=4, m_CompleteImageSize=len(rgba),
        m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=""))
    native_texture["m_TextureSettings"]["serializedVersion"] = 2
    native_texture["m_PlatformBlob"] = ""
    native_texture["image data"] = len(rgba)
    native_texture["_typelessdata"] = rgba.hex()
    texture_output = write("Texture2D", sprite.m_Name, texture_obj, native_texture)
    original = copy.deepcopy(obj.read_typetree())
    rd = original["m_RD"]
    rd["texture"] = {"fileID": 0}
    key, local_id = original["m_RenderDataKey"]
    original["m_RenderDataKey"] = {"".join(f"{key['data[' + str(i) + ']']:08x}"[::-1] for i in range(4)): local_id}
    original["m_Rect"]["serializedVersion"] = 2
    rd["serializedVersion"] = 3
    rd["textureRect"]["serializedVersion"] = 2
    for submesh in rd["m_SubMeshes"]:
        submesh["serializedVersion"] = 2
    rd["m_IndexBuffer"] = bytes(rd["m_IndexBuffer"]).hex()
    vertex_data = rd["m_VertexData"]
    vertex_bytes = bytes(vertex_data["m_DataSize"])
    vertex_data.update(serializedVersion=3, m_DataSize=len(vertex_bytes), _typelessdata=vertex_bytes.hex())
    native = source_header()
    native.update(fields(original))
    native["m_RD"]["texture"] = dict(fileID=2800000, guid=texture_output["guid"], type=2)
    sprite_output = write("Sprite", sprite.m_Name, obj, native)
    mesh = MeshHandler(sprite.m_RD, obj.version)
    mesh.process()
    uv = sprite.m_RD.uvTransform
    expected = dict(name=sprite.m_Name, rect={key: getattr(sprite.m_Rect, key) for key in ("x", "y", "width", "height")},
        pivot=dict(x=sprite.m_Pivot.x * sprite.m_Rect.width, y=sprite.m_Pivot.y * sprite.m_Rect.height),
        ppu=sprite.m_PixelsToUnits, border={key: getattr(sprite.m_Border, key) for key in "xyzw"},
        vertices=[dict(x=v[0], y=v[1]) for v in mesh.m_Vertices],
        triangles=list(mesh.m_IndexBuffer),
        uv=[dict(x=(v[0] * uv.x + uv.y) / texture.m_Width, y=(v[1] * uv.z + uv.w) / texture.m_Height) for v in mesh.m_Vertices],
        width=texture.m_Width, height=texture.m_Height, mip_count=texture.m_MipCount,
        filter=texture.m_TextureSettings.m_FilterMode, aniso=texture.m_TextureSettings.m_Aniso,
        wrapU=texture.m_TextureSettings.m_WrapU, wrapV=texture.m_TextureSettings.m_WrapV,
        rgba_sha256=sha(rgba))
    return dict(sprite=sprite_output, texture=texture_output, expected=expected,
        original_texture=dict(source=apk.provenance(texture_obj), payload_sha256=sha(texture_obj.get_raw_data()),
            fields=original_texture, encoded_sha256=sha(texture.get_image_data()), profile=profile, mips=mips),
        operation="Preserve original logical rect, pivot, border, PPU, native mesh/index/vertex bytes, render key, texture-rect offsets and UV transform. Decode original ASTC mip blocks with the existing UNORM8 decoder into native RGBA32 Texture2D; preserve original texture flags and sampler parameters. Do not use the converter's cropped single-Sprite importer pivot or re-compress the Windows texture.")
