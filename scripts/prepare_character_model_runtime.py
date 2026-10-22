#!/usr/bin/env python3
"""Package pinned 24 fps character meshes and textures for the offline Unity player."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import zipfile


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "assets/characters/wardrobe"
SOURCE_MANIFEST = ROOT / "assets/reference/character-appearance/source-manifest.json"
OUTPUT = ROOT / "unity/Assets/StreamingAssets/Character/character-models.zip"
POSES = (("male", "100/idle_01"), ("male", "200/idle_01"),
         ("female", "100/idle_01"), ("female", "200/idle_01"))
PREFIX = "res://assets/characters/wardrobe/"
RELATED_MANIFESTS = (
    ROOT / "assets/reference/sign-preview/source-manifest.json",
    ROOT / "assets/reference/character-genders/appearance-source-manifest.json",
    ROOT / "assets/reference/character-genders/free-parts-source-manifest.json",
    ROOT / "assets/reference/appearance-chest-preview/source-manifest.json",
    ROOT / "assets/reference/profession-appearance/source-manifest.json",
)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def rel_uri(uri: str) -> str:
    if not uri.startswith("res://assets/characters/"):
        raise ValueError("Character model URI escaped the pinned wardrobe output: " + uri)
    return uri.removeprefix("res://")


def source_bytes(uri: str, outputs: dict[str, str], inputs: dict[str, str],
                 aliases: dict[str, str]) -> bytes:
    path = ROOT / uri.removeprefix("res://")
    data = path.read_bytes()
    expected = outputs.get(uri)
    source_uri = uri
    if isinstance(expected, dict):
        expected = expected.get("sha256")
    if not expected and uri.startswith("res://assets/characters/original/"):
        actual = digest(data)
        filename = Path(uri).name
        for folder in ("female", "male", "sign-preview"):
            candidate = "res://assets/characters/" + folder + "/" + filename
            candidate_hash = outputs.get(candidate)
            if isinstance(candidate_hash, dict):
                candidate_hash = candidate_hash.get("sha256")
            if candidate_hash == actual:
                source_uri = candidate
                expected = candidate_hash
                aliases[uri] = candidate
                break
    if not expected or len(expected) != 64:
        raise ValueError("Pinned source manifest has no output hash for " + uri)
    if digest(data) != expected:
        raise ValueError("Character source output hash mismatch: " + uri)
    inputs[source_uri] = expected
    return data


def meta(path: Path, folder: bool) -> None:
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    guid = hashlib.md5(("zjcs:" + relative).encode("utf-8")).hexdigest()
    if folder:
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        body = "DefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target.write_text("fileFormatVersion: 2\nguid: " + guid + "\n" + body, encoding="utf-8")


def build() -> None:
    source = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if source.get("manifest") != "854_156383":
        raise ValueError("Character model source manifest is not pinned to 854_156383")
    outputs = source.get("outputs")
    if not isinstance(outputs, dict):
        raise ValueError("Character source output hashes are unavailable")
    input_manifests = []
    for manifest_path in RELATED_MANIFESTS:
        if not manifest_path.is_file():
            continue
        related = json.loads(manifest_path.read_text(encoding="utf-8"))
        if related.get("manifest", related.get("version")) != "854_156383":
            raise ValueError("Related character texture source is not pinned to 854_156383: " + str(manifest_path))
        related_outputs = related.get("outputs", {})
        if isinstance(related_outputs, dict):
            for uri, value in related_outputs.items():
                if uri not in outputs:
                    outputs[uri] = value
        related_resources = related.get("resources", {})
        if isinstance(related_resources, dict):
            for uri, value in related_resources.items():
                if uri not in outputs and isinstance(value, dict) and value.get("sha256"):
                    outputs[uri] = value["sha256"]
        input_manifests.append({"path": manifest_path.relative_to(ROOT).as_posix(),
                                "sha256": digest(manifest_path.read_bytes())})

    catalog_uri = PREFIX + "catalog.json"
    inputs: dict[str, str] = {}
    aliases: dict[str, str] = {}
    catalog_bytes = source_bytes(catalog_uri, outputs, inputs, aliases)
    catalog = json.loads(catalog_bytes.decode("utf-8"))
    entries: dict[str, bytes] = {rel_uri(catalog_uri): catalog_bytes}
    texture_names: set[str] = set()

    for sex, pose in POSES:
        pose_uri = catalog["poses"][sex][pose]
        pose_bytes = source_bytes(pose_uri, outputs, inputs, aliases)
        pose_data = json.loads(pose_bytes.decode("utf-8"))
        entries[rel_uri(pose_uri)] = pose_bytes
        for mesh_uri in pose_data["parts"].values():
            mesh_bytes = source_bytes(mesh_uri, outputs, inputs, aliases)
            entries[rel_uri(mesh_uri)] = mesh_bytes
            parts = json.loads(gzip.decompress(mesh_bytes).decode("utf-8"))
            texture_names.update(part["texture"] for part in parts if part.get("texture"))

    textures = catalog.get("textures", {})
    masks = catalog.get("masks", {})
    for name in sorted(texture_names):
        texture_uri = textures.get(name)
        if not texture_uri:
            raise ValueError("Character mesh references an unindexed texture: " + name)
        texture_bytes = source_bytes(texture_uri, outputs, inputs, aliases)
        entries[rel_uri(texture_uri)] = texture_bytes
        mask_uri = masks.get(name)
        if mask_uri:
            mask_bytes = source_bytes(mask_uri, outputs, inputs, aliases)
            entries[rel_uri(mask_uri)] = mask_bytes

    file_hashes = {name: digest(data) for name, data in sorted(entries.items())}
    pack_manifest = {
        "manifest": "854_156383",
        "operation": "Package only the male/female 100 and 200 profile idle poses from official 24 fps baked meshes.",
        "poses": [sex + "/" + pose for sex, pose in POSES],
        "files": file_hashes,
        "source_files": inputs,
        "source_uri_aliases": aliases,
    }
    manifest_bytes = (json.dumps(pack_manifest, ensure_ascii=False, sort_keys=True,
                                 separators=(",", ":")) + "\n").encode("utf-8")
    entries["character-model-manifest.json"] = manifest_bytes

    previous = source.get("runtime_pack")
    if OUTPUT.exists():
        existing_hash = digest(OUTPUT.read_bytes())
        if not isinstance(previous, dict) or previous.get("sha256") != existing_hash:
            raise ValueError("Refusing to overwrite a changed character model runtime package")

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    meta(OUTPUT.parent, True)
    meta(OUTPUT, False)
    temp = OUTPUT.with_suffix(".zip.tmp")
    try:
        with zipfile.ZipFile(temp, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for name, data in sorted(entries.items()):
                info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_STORED if name.endswith((".png", ".mesh.gz")) else zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                archive.writestr(info, data, compress_type=info.compress_type,
                                 compresslevel=None if info.compress_type == zipfile.ZIP_STORED else 9)
        os.replace(temp, OUTPUT)
    finally:
        if temp.exists():
            temp.unlink()

    package_bytes = OUTPUT.read_bytes()
    source["runtime_pack"] = {
        "path": OUTPUT.relative_to(ROOT).as_posix(),
        "bytes": len(package_bytes),
        "sha256": digest(package_bytes),
        "generator": "scripts/prepare_character_model_runtime.py",
        "generator_sha256": digest(Path(__file__).read_bytes()),
        "operation": pack_manifest["operation"],
        "source_manifests": input_manifests,
        "source_files": inputs,
        "source_uri_aliases": aliases,
    }
    SOURCE_MANIFEST.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Packaged", len(entries) - 1, "model and texture files:", OUTPUT.relative_to(ROOT),
          len(package_bytes), "bytes")


if __name__ == "__main__":
    build()
