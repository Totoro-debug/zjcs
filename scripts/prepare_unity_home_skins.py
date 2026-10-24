"""Stage the extracted HomeSkin entity models and their official textures."""
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "unity" / "Assets"
OFFICIAL = ROOT / "assets" / "official"
DECOR = OFFICIAL / "home-decor"
OUTPUT = ASSETS / "StreamingAssets" / "Home"
SPRITES = OUTPUT / "sprites"
RUNTIME_CATALOG = OUTPUT / "home-skins.json"
RUNTIME_MANIFEST = OUTPUT / "source-manifest.json"
SOURCE_MANIFEST = ROOT / "assets" / "reference" / "home-decor" / "source-manifest.json"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_guid(relative_path: str) -> str:
    return hashlib.md5(("zjcs:" + relative_path.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def write_meta(path: Path) -> None:
    meta_path = Path(str(path) + ".meta")
    if meta_path.is_file():
        return
    relative = path.relative_to(ASSETS).as_posix()
    body = (
        "fileFormatVersion: 2\n"
        "guid: " + stable_guid(relative) + "\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )
    meta_path.write_text(body, encoding="utf-8", newline="\n")


def flatten_vertices(vertices) -> list[float]:
    if not isinstance(vertices, list) or not vertices:
        return []
    if isinstance(vertices[0], (int, float)):
        return [float(value) for value in vertices]
    return [float(value) for point in vertices for value in point]


def trim_sprite(sprite: dict) -> dict:
    file = sprite.get("file", "")
    if file.startswith("res://"):
        file = file[len("res://"):]
    return {
        "file": Path(file).name,
        "name": sprite.get("name", ""),
        "width": sprite.get("width", 0),
        "height": sprite.get("height", 0),
        "pivot": sprite.get("pivot", []),
        "ppu": sprite.get("ppu", 100.0),
        "border": sprite.get("border", []),
        "mesh_type": sprite.get("mesh_type", 0),
        "rect_size": sprite.get("rect_size", []),
        "trim_offset": sprite.get("trim_offset", []),
        "trim_size": sprite.get("trim_size", []),
    }


def load_home_skin_entities() -> dict:
    catalog_path = DECOR / "catalog.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    entities = []
    for key, uri in sorted(catalog.get("entities", {}).items()):
        source_path = ROOT / uri.removeprefix("res://")
        if not source_path.is_file():
            raise FileNotFoundError(f"Official HomeSkin entity is missing: {source_path}")
        source = json.loads(source_path.read_text(encoding="utf-8"))
        skin_item_id, environment_skin_id = (int(value) for value in key.split(":"))
        layers = []
        for layer in source.get("layers") or []:
            sprite = layer.get("sprite")
            vertices = flatten_vertices(layer.get("vertices"))
            if not sprite or not sprite.get("file") or len(vertices) < 8:
                continue
            layers.append({
                "path": layer.get("path", ""),
                "sprite": trim_sprite(sprite),
                "vertices": vertices,
                "color": layer.get("color", []),
                "sorting_layer": layer.get("sorting_layer", 0),
                "sorting_order": layer.get("sorting_order", 0),
                "depth": layer.get("depth", 0.0),
                "materials": [
                    {
                        "name": material.get("name", ""),
                        "shader": material.get("shader", ""),
                        "colors": material.get("colors", {}),
                    }
                    for material in layer.get("materials") or []
                ],
            })
        entities.append({
            "skin_item_id": skin_item_id,
            "environment_skin_id": environment_skin_id,
            "name": source.get("name", ""),
            "type": source.get("type", 0),
            "slot": source.get("slot") or [],
            "layers": layers,
        })
    if len(entities) != len(catalog.get("entities", {})):
        raise ValueError("Official HomeSkin entity catalog did not load completely.")
    return {
        "version": "854_156383",
        "entities": entities,
    }


def stage_sprites(payload: dict) -> dict[str, str]:
    SPRITES.mkdir(parents=True, exist_ok=True)
    hashes = {}
    names = {
        layer["sprite"]["file"]
        for entity in payload["entities"]
        for layer in entity["layers"]
    }
    for name in sorted(names):
        source = OFFICIAL / "sprites" / name
        if not source.is_file():
            source = DECOR / "sprites" / name
        if not source.is_file():
            raise FileNotFoundError(f"Official HomeSkin texture is missing: {name}")
        data = source.read_bytes()
        target = SPRITES / name
        if target.is_file() and target.read_bytes() != data:
            raise ValueError(f"Home sprite path already contains different official content: {name}")
        if not target.is_file():
            target.write_bytes(data)
        write_meta(target)
        hashes[name] = digest(data)
    return hashes


def write_runtime_catalog(payload: dict) -> dict:
    RUNTIME_CATALOG.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    RUNTIME_CATALOG.write_bytes(data)
    write_meta(RUNTIME_CATALOG)
    return {"bytes": len(data), "sha256": digest(data)}


def update_runtime_manifest(catalog_info: dict, sprite_hashes: dict[str, str]) -> None:
    if not RUNTIME_MANIFEST.is_file():
        raise FileNotFoundError("Home runtime source manifest is missing.")
    manifest = json.loads(RUNTIME_MANIFEST.read_text(encoding="utf-8"))
    manifest.setdefault("outputs", {})["home-skins.json"] = catalog_info
    manifest.setdefault("counts", {})["home_skin_entities"] = len(load_home_skin_entities()["entities"])
    manifest["counts"]["home_skin_entity_sprites"] = len(sprite_hashes)
    manifest.setdefault("sprites", {}).update(sprite_hashes)
    manifest["home_skin_assets"] = {
        "manifest": "854_156383",
        "source": "assets/official/home-decor/catalog.json",
        "source_sha256": digest((DECOR / "catalog.json").read_bytes()),
        "source_manifest": "assets/reference/home-decor/source-manifest.json",
        "source_manifest_sha256": digest(SOURCE_MANIFEST.read_bytes()),
        "entity_count": manifest["counts"]["home_skin_entities"],
        "operation": "Stage trimmed original furniture/theme/weather entities and every referenced official sprite.",
    }
    RUNTIME_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, separators=(",", ":")),
                                encoding="utf-8", newline="\n")


def build() -> None:
    payload = load_home_skin_entities()
    sprite_hashes = stage_sprites(payload)
    catalog_info = write_runtime_catalog(payload)
    update_runtime_manifest(catalog_info, sprite_hashes)
    print("HomeSkin runtime payload:", {
        "entities": len(payload["entities"]),
        "entity_sprites": len(sprite_hashes),
        "catalog_bytes": catalog_info["bytes"],
    })


if __name__ == "__main__":
    build()
