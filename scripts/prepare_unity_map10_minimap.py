"""Prepare the pinned Map10 minimap geometry and official textures for Unity."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from map10_geometry import decode, version as geometry_version


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
UNITY_OUTPUT = ROOT / "unity/Assets/Official/World/Map/10"
MINIMAP_ROOT = ROOT / "assets/official/map-minimaps"
MINIMAP_CATALOG = MINIMAP_ROOT / "catalog.json"
SOURCE_MANIFEST = ROOT / "assets/reference/official-map/minimaps-source-manifest.json"
GEOMETRY_NAME = "7e3d086220a8c392.geom"
GEOMETRY_RESOURCE = "Resources/Map10/MiniMap/geometry.geom.bytes"
CATALOG_RESOURCE = "Resources/Map10/MiniMap/catalog.json"
TEXTURE_RESOURCE_DIR = "Resources/Map10/MiniMapTextures"
OUTPUT_MANIFEST = "minimap-source-manifest.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def write_generated(path: Path, content: bytes, previous_hashes: dict[Path, str]) -> None:
    if path.exists():
        current = path.read_bytes()
        if current == content:
            return
        expected = previous_hashes.get(path.resolve())
        if expected is None or sha256(current) != expected:
            raise ValueError("Refusing to overwrite changed Map10 minimap asset: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def write_meta(path: Path, importer: str) -> None:
    try:
        relative = path.resolve().relative_to(ROOT / "unity").as_posix()
    except ValueError:
        relative = "Assets/Official/World/Map/10/" + path.name
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    if importer == "DefaultImporter":
        content = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif importer == "TextureImporter":
        content = (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
                   "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 13\n"
                   "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n"
                   "  isReadable: 0\n  textureSettings:\n    serializedVersion: 2\n"
                   "    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 0\n"
                   "    wrapV: 0\n    wrapW: 0\n  spriteMode: 0\n"
                   "  alphaIsTransparency: 1\n  textureType: 0\n  textureShape: 1\n"
                   "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        content = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
                   "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    target = Path(str(path) + ".meta")
    if target.exists():
        existing = target.read_text(encoding="utf-8")
        if f"guid: {guid}" not in existing or importer not in existing:
            if importer == "DefaultImporter" and "folderAsset: yes" in existing and "DefaultImporter:" in existing:
                return
            if importer == "DefaultImporter" and existing.splitlines()[:1] == ["fileFormatVersion: 2"]:
                lines = existing.splitlines()
                if len(lines) == 2 and lines[1].startswith("guid: ") and len(lines[1][6:]) == 32:
                    content = content.replace("guid: " + guid, lines[1])
                    target.write_text(content, encoding="utf-8", newline="\n")
                    return
            raise ValueError("Refusing to replace an existing Map10 minimap meta file: " + str(target))
    else:
        target.write_text(content, encoding="utf-8", newline="\n")


def verified_previous_outputs(output: Path, inputs: dict) -> dict[Path, str]:
    manifest_path = output / OUTPUT_MANIFEST
    if not manifest_path.is_file():
        return {}
    previous = json.loads(manifest_path.read_text(encoding="utf-8"))
    if previous.get("format") != 1 or previous.get("manifest") != VERSION or previous.get("mapClassId") != 10:
        raise ValueError("Existing Map10 minimap manifest has an unexpected identity")
    if previous.get("inputs") != inputs:
        raise ValueError("Existing Map10 minimap source inputs differ from their manifest")
    verified = {}
    for item in previous.get("outputs", []):
        path = output / item["path"]
        if not path.is_file() or sha256(path.read_bytes()) != item["sha256"]:
            raise ValueError("Existing Map10 minimap output differs from its manifest: " + str(path))
        verified[path.resolve()] = item["sha256"]
    verified[manifest_path.resolve()] = sha256(manifest_path.read_bytes())
    return verified


def build(output: Path = UNITY_OUTPUT) -> dict:
    output = Path(output).resolve()
    source_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    minimap_catalog = json.loads(MINIMAP_CATALOG.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != VERSION:
        raise ValueError("Map10 minimap source manifest is not pinned to " + VERSION)
    if sha256(MINIMAP_CATALOG.read_bytes()) != source_manifest.get("outputs", {}).get("catalog.json"):
        raise ValueError("Official minimap catalog differs from its source manifest")
    source_entry = minimap_catalog.get("10")
    if not isinstance(source_entry, dict) or source_entry.get("file") != "res://assets/official/map-minimaps/" + GEOMETRY_NAME:
        raise ValueError("Official Map10 minimap geometry binding is missing")

    geometry_path = MINIMAP_ROOT / GEOMETRY_NAME
    geometry_bytes = geometry_path.read_bytes()
    if sha256(geometry_bytes) != source_manifest.get("outputs", {}).get(GEOMETRY_NAME):
        raise ValueError("Official Map10 minimap geometry differs from its source manifest")
    batches = decode(geometry_bytes, geometry_version(geometry_bytes))
    if len(batches) != source_entry.get("geometry", {}).get("batches"):
        raise ValueError("Official Map10 minimap batch count differs from its catalog")

    minimap_textures = {}
    input_records = {
        "minimapCatalog": {"path": MINIMAP_CATALOG.relative_to(ROOT).as_posix(),
                           "sha256": sha256(MINIMAP_CATALOG.read_bytes())},
        "geometry": {"path": geometry_path.relative_to(ROOT).as_posix(), "sha256": sha256(geometry_bytes)},
        "sourceManifest": {"path": SOURCE_MANIFEST.relative_to(ROOT).as_posix(),
                           "sha256": sha256(SOURCE_MANIFEST.read_bytes())},
    }
    for uri, record in source_manifest.get("resources", {}).items():
        if record.get("manifest") != VERSION:
            raise ValueError("Map10 minimap resource is not pinned to the official APK: " + uri)
        source_path = ROOT / uri.removeprefix("res://")
        texture_bytes = source_path.read_bytes()
        if sha256(texture_bytes) != record.get("sha256"):
            raise ValueError("Official Map10 minimap texture hash differs: " + str(source_path))
        input_records["texture:" + uri] = {
            "path": source_path.relative_to(ROOT).as_posix(), "sha256": sha256(texture_bytes)
        }
        minimap_textures[uri] = {
            "resource": "Map10/MiniMapTextures/" + source_path.stem,
            "sha256": sha256(texture_bytes),
        }
    for batch in batches:
        uri = batch["metadata"]["texture"]
        if uri not in minimap_textures:
            raise ValueError("Map10 minimap geometry texture is missing from the source manifest: " + uri)

    previous = verified_previous_outputs(output, input_records)
    for directory in (output / "Resources/Map10/MiniMap", output / "Resources/Map10/MiniMapTextures"):
        directory.mkdir(parents=True, exist_ok=True)
        write_meta(directory, "DefaultImporter")
    texture_outputs = []
    for uri, record in sorted(minimap_textures.items()):
        source_path = ROOT / uri.removeprefix("res://")
        target = output / TEXTURE_RESOURCE_DIR / source_path.name
        payload = source_path.read_bytes()
        write_generated(target, payload, previous)
        write_meta(target, "TextureImporter")
        texture_outputs.append({"path": target.relative_to(output).as_posix(), "sha256": sha256(payload)})

    geometry_target = output / GEOMETRY_RESOURCE
    write_generated(geometry_target, geometry_bytes, previous)
    write_meta(geometry_target, "TextScriptImporter")
    catalog_payload = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": 10,
        "geometryResource": "Map10/MiniMap/geometry.geom",
        "geometryVersion": geometry_version(geometry_bytes),
        "geometryBatchCount": len(batches),
        "textures": [
            {"uri": uri, **record} for uri, record in sorted(minimap_textures.items())
        ],
    }
    catalog_bytes = json_bytes(catalog_payload)
    catalog_target = output / CATALOG_RESOURCE
    write_generated(catalog_target, catalog_bytes, previous)
    write_meta(catalog_target, "TextScriptImporter")

    outputs = [
        {"path": GEOMETRY_RESOURCE, "sha256": sha256(geometry_bytes)},
        {"path": CATALOG_RESOURCE, "sha256": sha256(catalog_bytes)},
        *texture_outputs,
    ]
    manifest = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": 10,
        "sourceGeometry": source_entry["file"],
        "geometryVersion": geometry_version(geometry_bytes),
        "geometryBatchCount": len(batches),
        "inputs": input_records,
        "outputs": outputs,
    }
    manifest_bytes = json_bytes(manifest)
    manifest_path = output / OUTPUT_MANIFEST
    write_generated(manifest_path, manifest_bytes, previous)
    write_meta(manifest_path, "TextScriptImporter")
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=UNITY_OUTPUT)
    args = parser.parse_args()
    manifest = build(args.output)
    print("Map10 official minimap prepared:", manifest["geometryBatchCount"], "geometry batches,",
          len(manifest["outputs"]) - 2, "official textures")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
