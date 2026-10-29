"""Package the pinned official Map12 Water2 meshes and texture closure for Unity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/official/map12-rendering.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/materials-12-source-manifest.json"
WATER_RECEIPT = ROOT / "assets/reference/official-map/water-source-manifest.json"
TARGET = ROOT / "unity/Assets/Game/Resources/Map12/Water"
TEXTURES = TARGET / "Textures"
RECEIPT = ROOT / "assets/reference/official-map/map12-water-runtime-source-manifest.json"
MASK_SOURCE = ROOT / "assets/official/map-water-masks/12.png"
SHADER_SOURCE = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders/WaterSurface.shader"
SHADER_TARGET = ROOT / "unity/Assets/Game/Resources/World/Map12Water.shader"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def guid(path: str) -> str:
    return hashlib.md5(("zjcs-map12-water:" + path).encode("utf-8")).hexdigest()


def meta(path: str, texture: bool = False, shader: bool = False, normal: bool = False) -> bytes:
    key = guid(path)
    if texture:
        body = f"""fileFormatVersion: 2
guid: {key}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
  sRGBTexture: 0
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  spriteMode: 0
  alphaIsTransparency: 0
  textureType: {1 if normal else 0}
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    elif shader:
        body = f"""fileFormatVersion: 2
guid: {key}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  preprocessorOverride: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    else:
        body = f"""fileFormatVersion: 2
guid: {key}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    return body.encode("utf-8")


def folder_meta(path: str) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n").encode("utf-8")


def write(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() != content:
        if path.suffix == ".meta":
            old = path.read_text(encoding="utf-8")
            new = content.decode("utf-8")
            old_guid = next((line.split(":", 1)[1].strip() for line in old.splitlines()
                             if line.startswith("guid:")), "")
            new_guid = next((line.split(":", 1)[1].strip() for line in new.splitlines()
                             if line.startswith("guid:")), "")
            old_importer = next((line for line in old.splitlines() if line.endswith(":")), "")
            new_importer = next((line for line in new.splitlines() if line.endswith(":")), "")
            if old_guid == new_guid and old_importer == new_importer:
                path.write_bytes(content)
                return
        if path.name == "map12-water-runtime.json":
            old = json.loads(path.read_text(encoding="utf-8"))
            if old.get("mapClassId") == 12 and len(old.get("renderers", [])) == 57:
                path.write_bytes(content)
                return
        raise ValueError("Refusing to overwrite a different Map12 water asset: " + str(path))
    if not path.exists():
        path.write_bytes(content)


def build() -> dict:
    for directory in (TARGET, TEXTURES):
        directory_meta_path = Path(str(directory) + ".meta")
        if not directory_meta_path.exists():
            write(directory_meta_path, folder_meta(directory.relative_to(ROOT / "unity").as_posix()))
    source_bytes = SOURCE.read_bytes()
    receipt = json.loads(SOURCE_RECEIPT.read_text(encoding="utf-8"))
    if receipt.get("manifest") != "854_156383" or receipt.get("outputs", {}).get("assets/official/map12-rendering.json") != sha(source_bytes):
        raise ValueError("Pinned Map12 water source differs from its material receipt")
    if receipt["outputs"].get("assets/official/map-water-masks/12.png") != sha(MASK_SOURCE.read_bytes()):
        raise ValueError("Pinned Map12 water mask differs from its material receipt")
    source = json.loads(source_bytes)
    water = source.get("water", {})
    renderers = water.get("renderers", [])
    if source.get("map") != 12 or len(renderers) != 57:
        raise ValueError("Original Map12 water mesh set is incomplete")
    mask = water.get("native_materials", {}).get("mask", {})
    if mask.get("texture") != "res://assets/official/map-water-masks/12.png" or len(mask.get("rect", [])) != 4:
        raise ValueError("Original Map12 water mask binding is incomplete")

    water_receipt_bytes = WATER_RECEIPT.read_bytes()
    water_receipt = json.loads(water_receipt_bytes)
    resource_records = water_receipt.get("resources", {})
    texture_bindings: dict[str, dict] = {}
    for renderer in renderers:
        for prop, uri in renderer.get("textures", {}).items():
            record = resource_records.get(uri)
            if not uri.startswith("res://assets/official/sprites/") or not record:
                raise ValueError("Official Map12 water texture closure is incomplete: " + uri)
            texture_bindings[uri] = {"property": prop, "hash": record["sha256"]}

    resources: dict[str, str] = {}
    outputs: dict[str, str] = {}
    for uri, binding in sorted(texture_bindings.items()):
        source_path = ROOT / uri.removeprefix("res://")
        content = source_path.read_bytes()
        if sha(content) != binding["hash"]:
            raise ValueError("Official Map12 water texture hash mismatch: " + uri)
        destination = TEXTURES / source_path.name
        relative = destination.relative_to(ROOT / "unity/Assets/Game/Resources").with_suffix("").as_posix()
        write(destination, content)
        write(Path(str(destination) + ".meta"), meta(destination.relative_to(ROOT / "unity").as_posix(), True,
            normal=binding["property"] == "_BumpMap"))
        resources[uri] = "Map12/Water/Textures/" + source_path.stem
        outputs[destination.relative_to(ROOT).as_posix()] = sha(content)

    mask_path = TARGET / "map12-water-mask.png"
    mask_content = MASK_SOURCE.read_bytes()
    write(mask_path, mask_content)
    write(Path(str(mask_path) + ".meta"), meta(mask_path.relative_to(ROOT / "unity").as_posix(), True))
    outputs[mask_path.relative_to(ROOT).as_posix()] = sha(mask_content)

    runtime_renderers = []
    for renderer in renderers:
        texture_values = []
        for prop, uri in sorted(renderer.get("textures", {}).items()):
            scale = [0.2, 1.0] if prop == "_NoiseTex" else [1.0, 1.0]
            texture_values.append({"property": prop, "resource": resources[uri], "scale": scale, "offset": [0.0, 0.0]})
        runtime_renderers.append({
            "name": renderer["name"],
            "material": renderer["material"],
            "vertices": [coordinate for vertex in renderer["vertices"] for coordinate in vertex],
            "uv": [coordinate for vertex in renderer["uv"] for coordinate in vertex],
            "indices": renderer["indices"],
            "textures": texture_values,
            "colors": [{"property": key, "value": value} for key, value in sorted(renderer["colors"].items())],
            "parameters": [{"property": key, "value": value} for key, value in sorted(renderer["parameters"].items())],
        })
    runtime = {"format": 1, "mapClassId": 12, "sortingOrder": 18001,
        "maskResource": "Map12/Water/map12-water-mask", "maskRect": mask["rect"], "renderers": runtime_renderers}
    payload = (json.dumps(runtime, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    payload_path = TARGET / "map12-water-runtime.json"
    write(payload_path, payload)
    write(Path(str(payload_path) + ".meta"), meta(payload_path.relative_to(ROOT / "unity").as_posix()))
    outputs[payload_path.relative_to(ROOT).as_posix()] = sha(payload)

    shader_content = SHADER_SOURCE.read_bytes()
    write(SHADER_TARGET, shader_content)
    write(Path(str(SHADER_TARGET) + ".meta"), meta(SHADER_TARGET.relative_to(ROOT / "unity").as_posix(), shader=True))
    outputs[SHADER_TARGET.relative_to(ROOT).as_posix()] = sha(shader_content)

    result = {"manifest": "854_156383", "mapClassId": 12, "input": {
        SOURCE.relative_to(ROOT).as_posix(): sha(source_bytes),
        SOURCE_RECEIPT.relative_to(ROOT).as_posix(): sha(SOURCE_RECEIPT.read_bytes()),
        WATER_RECEIPT.relative_to(ROOT).as_posix(): sha(water_receipt_bytes),
        SHADER_SOURCE.relative_to(ROOT).as_posix(): sha(shader_content)},
        "outputs": outputs, "texture_bindings": resources, "renderer_count": len(runtime_renderers)}
    encoded = (json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    if RECEIPT.exists() and RECEIPT.read_bytes() != encoded:
        old = json.loads(RECEIPT.read_text(encoding="utf-8"))
        if old.get("input") != result["input"] or set(old.get("outputs", {})) != set(outputs):
            raise ValueError("Refusing to replace a different Map12 water runtime receipt")
    if not RECEIPT.exists() or RECEIPT.read_bytes() != encoded: RECEIPT.write_bytes(encoded)
    print("Prepared official Map12 Water2 payload:", len(runtime_renderers), "meshes,", len(resources), "textures")
    return result


if __name__ == "__main__":
    build()
