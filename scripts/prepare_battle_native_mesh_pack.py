"""Package verified official battle mesh-frame animations for Unity's native renderer."""
import argparse
import hashlib
import json
import os
import tempfile
import zipfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
MODELS = ROOT / "assets/official/battle-presentation/models.json"
PRESENTATION_MANIFEST = ROOT / "assets/reference/official-map/battle-presentation-source-manifest.json"
SPINE_CATALOG = ROOT / "unity/Assets/Official/Combat/Spine/catalog.json"
PAGES_MANIFEST = ROOT / "assets/reference/battle-spine/pages-source-manifest.json"
SPRITE_ROOT = ROOT / "unity/Assets/Official/Combat/sprites"
DEFAULT_OUTPUT = ROOT / "unity/Assets/StreamingAssets/Combat/native-battle-meshes.zip"
OUTPUT_MANIFEST = ROOT / "unity/Assets/Official/Combat/native-battle-meshes-source-manifest.json"


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def normalize_animation(source):
    if not isinstance(source, dict):
        raise ValueError("Native battle animation root must be an object")
    duration = float(source.get("duration", -1))
    fps = float(source.get("fps", 0))
    scale = float(source.get("scale", 0))
    frames = source.get("frames")
    if duration < 0 or fps <= 0 or scale <= 0 or not isinstance(frames, list) or not frames:
        raise ValueError("Native battle animation timing or frame data is invalid")

    normalized_frames = []
    pages = set()
    for frame in frames:
        if not isinstance(frame, list):
            raise ValueError("Native battle animation frame must be a draw list")
        batches = []
        for draw in frame:
            if not isinstance(draw, dict):
                raise ValueError("Native battle animation draw must be an object")
            page = draw.get("page")
            vertices = draw.get("vertices")
            uvs = draw.get("uv")
            triangles = draw.get("triangles")
            color = draw.get("color")
            if (not isinstance(page, str) or not page or PurePosixPath(page).name != page
                    or not page.lower().endswith(".png")
                    or not isinstance(vertices, list) or not vertices or len(vertices) % 2
                    or not isinstance(uvs, list) or len(uvs) != len(vertices)
                    or not isinstance(triangles, list) or not triangles or len(triangles) % 3
                    or not isinstance(color, list) or len(color) != 4):
                raise ValueError("Native battle animation draw geometry is malformed")
            vertex_count = len(vertices) // 2
            if any(not isinstance(index, int) or index < 0 or index >= vertex_count for index in triangles):
                raise ValueError("Native battle animation triangle index is outside its vertex range")
            if int(draw.get("blend", 0)) not in (0, 1, 2, 3):
                raise ValueError("Native battle animation uses an unknown Spine blend mode")
            pages.add(page)
            batches.append({
                "order": int(draw.get("order", 0)),
                "slot": str(draw.get("slot", "")),
                "page": page,
                "vertices": vertices,
                "uv": uvs,
                "triangles": triangles,
                "color": color,
                "blend": int(draw.get("blend", 0)),
            })
        normalized_frames.append({"batches": batches})

    payload = {
        "animation": str(source.get("animation", "")),
        "duration": duration,
        "fps": fps,
        "scale": scale,
        "frames": normalized_frames,
    }
    encoded = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    return encoded, sorted(pages)


def bind_animation_pages(encoded, source_pages, model_textures, verified_pages):
    bindings = {}
    missing = []
    for page in source_pages:
        uri = model_textures.get(page)
        record = verified_pages.get(uri) if isinstance(uri, str) else None
        if record is None:
            missing.append(page)
        else:
            bindings[page] = record["archive_name"]
    if missing:
        return None, sorted(missing)

    payload = json.loads(encoded.decode("utf-8"))
    for frame in payload["frames"]:
        for draw in frame["batches"]:
            draw["page"] = bindings[draw["page"]]
    remapped = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    return remapped, []


def _entry(name, content):
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o100644 << 16
    return info, content


def _stable_meta(path):
    unity_root = ROOT / "unity"
    relative = path.relative_to(unity_root).as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    return ("fileFormatVersion: 2\n"
            "guid: " + guid + "\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("utf-8")


def _write_new_or_identical(path, content):
    path = Path(path)
    if path.exists():
        if path.read_bytes() != content:
            raise ValueError("Refusing to overwrite changed generated asset: " + str(path))
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".native-battle-output-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def _read_pinned(root):
    models_data = json.loads(MODELS.read_text(encoding="utf-8"))
    presentation_manifest = json.loads(PRESENTATION_MANIFEST.read_text(encoding="utf-8"))
    spine_data = json.loads(SPINE_CATALOG.read_text(encoding="utf-8"))
    pages_manifest = json.loads(PAGES_MANIFEST.read_text(encoding="utf-8"))
    if presentation_manifest.get("manifest") != VERSION or pages_manifest.get("manifest") != VERSION:
        raise ValueError("Battle mesh inputs must use the pinned manifest " + VERSION)
    model_hash = presentation_manifest.get("outputs", {}).get("battle-presentation/models.json")
    if model_hash != sha256_file(MODELS):
        raise ValueError("Official battle model animation index hash does not match its source manifest")
    page_inputs = pages_manifest.get("inputs", {})
    for label, path in (("catalog", SPINE_CATALOG),):
        record = page_inputs.get(label, {})
        if record.get("sha256") != sha256_file(path):
            raise ValueError("Battle Spine page input hash changed: " + label)
    if not isinstance(spine_data.get("skeletons"), dict):
        raise ValueError("Official battle Spine model catalog is malformed")
    return root, models_data, presentation_manifest, spine_data["skeletons"], pages_manifest


def build(output=DEFAULT_OUTPUT):
    root, models_data, presentation_manifest, skeletons, pages_manifest = _read_pinned(ROOT)
    identities_path = ROOT / "unity/Assets/Official/Combat/Presentation/model-identities.json"
    identities = json.loads(identities_path.read_text(encoding="utf-8"))
    if identities.get("manifest") != VERSION:
        raise ValueError("Official battle model identities are not pinned to " + VERSION)
    monster_ids = {row["key"]: int(row["monster_id"]) for row in identities.get("monster_models", [])}

    page_records = pages_manifest.get("textures", {})
    valid_pages = {}
    source_failures = pages_manifest.get("failures", {})
    page_failures = {key: value for key, value in source_failures.items()
                     if key.startswith("page:")}
    model_binding_failures = {key: value for key, value in source_failures.items()
                              if not key.startswith("page:")}
    for base, record in page_records.items():
        relative = record.get("file", "")
        source = (ROOT / PurePosixPath(relative)).resolve()
        if not source.is_relative_to(ROOT) or not source.is_file():
            page_failures[base + ".png"] = "restored page file is missing: " + relative
            continue
        digest = sha256_file(source)
        if digest != record.get("sha256") or pages_manifest.get("outputs", {}).get(base) != digest:
            page_failures[base + ".png"] = "restored page hash does not match its manifest"
            continue
        sprite_source = record.get("sprite_source")
        archive_name = PurePosixPath(relative).name
        if not isinstance(sprite_source, str) or not sprite_source.startswith("res://assets/official/sprites/"):
            page_failures[base + ".png"] = "restored page has no official sprite binding"
            continue
        existing = valid_pages.get(sprite_source)
        resolved = {"source": source, "sha256": digest, "archive_name": archive_name}
        if existing is not None and (existing["sha256"] != digest
                                     or existing["archive_name"] != archive_name):
            valid_pages.pop(sprite_source)
            page_failures[sprite_source] = "official sprite binding resolves to multiple page files"
            continue
        if sprite_source not in page_failures:
            valid_pages[sprite_source] = resolved

    output = Path(output).resolve()
    streaming_root = (ROOT / "unity/Assets/StreamingAssets").resolve()
    if not output.is_relative_to(streaming_root) or output.suffix.lower() != ".zip":
        raise ValueError("Native battle pack must be a .zip under unity/Assets/StreamingAssets")
    output.parent.mkdir(parents=True, exist_ok=True)
    model_records = []
    skipped = []
    needed_pages = {}
    written_entries = set()
    animation_count = 0
    source_hashes = {
        "models": sha256_file(MODELS),
        "presentation_manifest": sha256_file(PRESENTATION_MANIFEST),
        "spine_catalog": sha256_file(SPINE_CATALOG),
        "page_manifest": sha256_file(PAGES_MANIFEST),
        "identities": sha256_file(identities_path),
    }
    handle, temporary_name = tempfile.mkstemp(prefix=".native-battle-pack-", suffix=".zip",
                                               dir=output.parent)
    os.close(handle)
    temporary = Path(temporary_name)
    try:
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED,
                             compresslevel=6, strict_timestamps=True) as archive:
            for model_key in sorted(models_data):
                spine = skeletons.get(model_key)
                if spine is None:
                    skipped.append({"model": model_key, "reason": "no_battle_skeleton_binding"})
                    continue
                animations = []
                for alias, uri in sorted(models_data[model_key].items()):
                    prefix = "res://assets/official/"
                    if not isinstance(uri, str) or not uri.startswith(prefix):
                        skipped.append({"model": model_key, "animation": alias,
                                        "reason": "animation_path_is_not_an_official_resource"})
                        continue
                    relative = PurePosixPath(uri[len("res://"):])
                    if relative.parts[:3] != ("assets", "official", "battle-presentation"):
                        skipped.append({"model": model_key, "animation": alias,
                                        "reason": "animation_path_is_outside_battle_presentation"})
                        continue
                    source = root.joinpath(*relative.parts)
                    manifest_key = PurePosixPath(*relative.parts[2:]).as_posix()
                    expected = presentation_manifest.get("outputs", {}).get(manifest_key)
                    if not source.is_file() or not expected or sha256_file(source) != expected:
                        skipped.append({"model": model_key, "animation": alias,
                                        "reason": "animation_source_missing_or_hash_mismatch",
                                        "path": relative.as_posix()})
                        continue
                    try:
                        encoded, pages = normalize_animation(json.loads(source.read_text(encoding="utf-8")))
                    except (UnicodeError, json.JSONDecodeError, TypeError, ValueError) as error:
                        skipped.append({"model": model_key, "animation": alias,
                                        "reason": "animation_payload_invalid", "detail": str(error)})
                        continue
                    encoded, missing = bind_animation_pages(
                        encoded, pages, spine.get("textures", {}), valid_pages)
                    if missing:
                        skipped.append({"model": model_key, "animation": alias,
                                        "reason": "official_texture_page_unavailable", "pages": missing})
                        continue
                    entry_name = "animations/" + sha256_bytes(
                        (model_key + "\n" + relative.as_posix()).encode("utf-8")) + ".json"
                    if entry_name not in written_entries:
                        archive.writestr(*_entry(entry_name, encoded), compress_type=zipfile.ZIP_DEFLATED,
                                         compresslevel=6)
                        written_entries.add(entry_name)
                    animations.append({"name": alias, "entry": entry_name})
                    for page in pages:
                        uri = spine.get("textures", {}).get(page)
                        record = valid_pages[uri]
                        needed_pages[record["archive_name"]] = record
                    animation_count += 1
                if animations:
                    model_records.append({
                        "key": model_key,
                        "monsterId": monster_ids.get(model_key, 0),
                        "scale": float(spine.get("scale", 1.0)),
                        "animations": animations,
                    })

            for page in sorted(needed_pages):
                source = needed_pages[page]["source"]
                with source.open("rb") as stream:
                    archive.writestr(*_entry("textures/" + page, stream.read()),
                                     compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)
            catalog = {"manifest": VERSION, "models": model_records}
            catalog_bytes = (json.dumps(catalog, ensure_ascii=False, sort_keys=True,
                                        separators=(",", ":")) + "\n").encode("utf-8")
            archive.writestr(*_entry("catalog.json", catalog_bytes),
                             compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)

        digest = sha256_file(temporary)
        previous_manifest = OUTPUT_MANIFEST
        if output.exists():
            if not previous_manifest.is_file():
                raise ValueError("Refusing to replace an untracked native battle pack: " + str(output))
            previous = json.loads(previous_manifest.read_text(encoding="utf-8"))
            if (previous.get("generated_by") != "prepare_battle_native_mesh_pack.py"
                    or previous.get("output_sha256") != sha256_file(output)):
                raise ValueError("Refusing to replace a changed native battle pack: " + str(output))
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)

    manifest = {
        "manifest": VERSION,
        "generated_by": "prepare_battle_native_mesh_pack.py",
        "operation": "Official sampled battle meshes normalized to Unity JsonUtility frame objects and packed with verified atlas pages",
        "inputs": source_hashes,
        "output": output.relative_to(ROOT).as_posix(),
        "output_sha256": digest,
        "model_count": len(model_records),
        "animation_count": animation_count,
        "texture_page_count": len(needed_pages),
        "skipped": skipped,
        "page_failures": page_failures,
        "model_binding_failures": model_binding_failures,
        "models_without_idle": [model["key"] for model in model_records
                                if not any(animation["name"] == "idle_01" for animation in model["animations"])],
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    if OUTPUT_MANIFEST.exists():
        previous = json.loads(OUTPUT_MANIFEST.read_text(encoding="utf-8"))
        if previous.get("generated_by") != "prepare_battle_native_mesh_pack.py":
            raise ValueError("Refusing to overwrite an untracked battle mesh source manifest")
    OUTPUT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    temporary_manifest = OUTPUT_MANIFEST.with_name("." + OUTPUT_MANIFEST.name + ".tmp")
    temporary_manifest.write_bytes(manifest_bytes)
    temporary_manifest.replace(OUTPUT_MANIFEST)
    _write_new_or_identical(OUTPUT_MANIFEST.with_suffix(OUTPUT_MANIFEST.suffix + ".meta"),
                            _stable_meta(OUTPUT_MANIFEST))
    _write_new_or_identical(output.with_suffix(output.suffix + ".meta"), _stable_meta(output))
    print(json.dumps({"output": manifest["output"], "sha256": digest,
                      "models": len(model_records), "animations": animation_count,
                      "textures": len(needed_pages), "skipped": len(skipped),
                      "page_failures": len(page_failures),
                      "model_binding_failures": len(model_binding_failures),
                      "bytes": output.stat().st_size},
                     ensure_ascii=False))
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    build(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
