"""Prepare provenance-checked Map11 terrain geometry and referenced textures for Unity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
import sys

from map10_geometry import decode


ROOT = Path(__file__).resolve().parents[1]
SOURCE_GEOMETRY = ROOT / "assets/official/regions/11.geom"
SOURCE_REGIONS = ROOT / "assets/official/regions/11.json"
SOURCE_MANIFEST = ROOT / "assets/reference/official-map/materials-11-source-manifest.json"
SPRITE_ROOT = ROOT / "assets/official/sprites"
UNITY_ROOT = ROOT / "unity/Assets/Official/World/Map/11"
RESOURCE_ROOT = UNITY_ROOT / "Resources/Map11/Textures"
EXPECTED_BATCHES = 14427


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_guid(relative_path: str) -> str:
    return hashlib.md5(("zjcs-official:" + relative_path).encode("utf-8")).hexdigest()


def text_meta(relative_path: str) -> bytes:
    guid = asset_guid("Assets/Official/World/Map/11/" + relative_path)
    return (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")


def texture_meta(relative_path: str) -> bytes:
    guid = asset_guid("Assets/Official/World/Map/11/" + relative_path)
    return (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
            "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 13\n"
            "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n  sRGBTexture: 1\n"
            "  isReadable: 0\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n"
            "    aniso: 1\n    mipBias: 0\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n"
            "  spriteMode: 0\n  alphaIsTransparency: 1\n  textureType: 0\n  textureShape: 1\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")


def folder_meta(relative_path: str) -> bytes:
    guid = asset_guid("Assets/Official/World/Map/11/" + relative_path)
    return (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")


def write_identical(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() != payload:
        raise ValueError(f"Refusing to overwrite a different Map11 Unity asset: {path}")
    if not path.exists():
        path.write_bytes(payload)


def build() -> dict:
    source = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if source.get("manifest") != "854_156383" or source.get("map") != 11:
        raise ValueError("Map11 material receipt does not match the pinned original package")

    geometry = SOURCE_GEOMETRY.read_bytes()
    region_bytes = SOURCE_REGIONS.read_bytes()
    if source.get("outputs", {}).get("assets/official/regions/11.geom") != sha256(geometry):
        raise ValueError("Map11 geometry differs from its pinned material receipt")
    if source.get("outputs", {}).get("assets/official/regions/11.json") != sha256(region_bytes):
        raise ValueError("Map11 region data differs from its pinned material receipt")

    batches = decode(geometry, EXPECTED_BATCHES)
    textures = sorted({batch["metadata"]["texture"] for batch in batches})
    texture_records = []
    for uri in textures:
        if not uri.startswith("res://assets/official/sprites/"):
            raise ValueError(f"Map11 geometry references a nonofficial texture URI: {uri}")
        name = Path(uri).name
        source_path = SPRITE_ROOT / name
        if not source_path.is_file():
            raise FileNotFoundError(f"Map11 terrain texture is missing: {source_path}")
        resource_record = source.get("resources", {}).get(uri)
        expected = resource_record.get("sha256") if resource_record else None
        payload = source_path.read_bytes()
        if expected is None or expected != sha256(payload):
            raise ValueError(f"Map11 terrain texture differs from its pinned material receipt: {name}")
        relative_path = "Resources/Map11/Textures/" + name
        target = RESOURCE_ROOT / name
        write_identical(target, payload)
        write_identical(Path(str(target) + ".meta"), texture_meta(relative_path))
        texture_records.append({"source": "assets/official/sprites/" + name,
                                "path": relative_path, "sha256": sha256(payload)})

    geometry_relative = "map11.geom.bytes"
    geometry_target = UNITY_ROOT / geometry_relative
    write_identical(geometry_target, geometry)
    write_identical(Path(str(geometry_target) + ".meta"), text_meta(geometry_relative))

    write_identical(Path(str(UNITY_ROOT) + ".meta"), folder_meta(""))
    for directory in (UNITY_ROOT / "Resources", UNITY_ROOT / "Resources/Map11",
                      UNITY_ROOT / "Resources/Map11/Textures"):
        relative = directory.relative_to(UNITY_ROOT).as_posix()
        write_identical(Path(str(directory) + ".meta"), folder_meta(relative))

    record = {
        "manifest": "854_156383",
        "mapClassId": 11,
        "geometry": {"source": "assets/official/regions/11.geom", "path": geometry_relative,
                     "sha256": sha256(geometry), "batchCount": len(batches)},
        "textures": texture_records,
    }
    record_path = UNITY_ROOT / "rendering-source-manifest.json"
    record_bytes = (json.dumps(record, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    write_identical(record_path, record_bytes)
    write_identical(Path(str(record_path) + ".meta"), text_meta("rendering-source-manifest.json"))
    print(f"Map11 terrain payload: {len(batches)} batches, {len(texture_records)} official textures")
    return record


if __name__ == "__main__":
    build()
