"""Stage the fixed-build official SettingsDialog for the Home UI runtime."""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

import prepare_unity_home_runtime as home

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "official" / "settings" / "ui.json"
SOURCE_MANIFEST = ROOT / "assets" / "reference" / "settings" / "source-manifest.json"
OUTPUT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Home" / "Settings"
SPRITES = OUTPUT / "sprites"
CLICK_SOURCE = ROOT / "assets" / "official" / "settings" / "click.wav"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value, compact: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, ensure_ascii=False,
                      separators=(",", ":") if compact else None,
                      indent=None if compact else 2)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(
                mode="w", encoding="utf-8", newline="", dir=path.parent,
                prefix=".settings-", suffix=".tmp", delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(text)
        os.replace(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def ensure_meta(path: Path, is_folder: bool = False) -> None:
    meta = path.with_name(path.name + ".meta") if is_folder else Path(str(path) + ".meta")
    if meta.exists():
        return
    relative = path.relative_to(ROOT).as_posix()
    guid = hashlib.sha256(("zjcs-home-settings:" + relative).encode("utf-8")).hexdigest()[:32]
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
        newline="\n",
    )


def build() -> dict:
    source_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    screen = source.get("screen") or {}
    root = screen.get("root")
    if not isinstance(root, dict) or root.get("name") != "SettingsDialog":
        raise ValueError("The fixed official SettingsDialog prefab tree is missing.")

    home.SPRITE_SOURCES.clear()
    panel = home.flatten_panel({"key": "settings", "root": home.trim_node(root)})
    extension = {
        "version": source_manifest.get("manifest", "854_156383"),
        "panels": [panel],
        "dynamic_sprites": [],
        "texts": home.flatten_texts(source.get("texts") or {}),
    }

    OUTPUT.mkdir(parents=True, exist_ok=True)
    SPRITES.mkdir(parents=True, exist_ok=True)
    ensure_meta(OUTPUT, is_folder=True)
    ensure_meta(SPRITES, is_folder=True)
    staged_sprites = {}
    for uri, record in sorted((source_manifest.get("sprites") or {}).items()):
        source_path = ROOT / uri.removeprefix("res://")
        if not source_path.is_file():
            raise FileNotFoundError(f"Official Settings sprite is missing: {source_path}")
        content = source_path.read_bytes()
        expected = (record or {}).get("sha256")
        if expected and sha256(content) != expected:
            raise ValueError(f"Official Settings sprite hash mismatch: {source_path}")
        destination = SPRITES / source_path.name
        if not destination.is_file() or sha256(destination.read_bytes()) != sha256(content):
            shutil.copyfile(source_path, destination)
        ensure_meta(destination)
        staged_sprites[source_path.name] = sha256(content)

    click_record = (source_manifest.get("outputs") or {}).get(
        "assets/official/settings/click.wav")
    click_content = CLICK_SOURCE.read_bytes()
    if click_record and sha256(click_content) != click_record:
        raise ValueError("Official Settings click audio hash mismatch.")
    click_path = OUTPUT / "click.wav"
    if not click_path.is_file() or sha256(click_path.read_bytes()) != sha256(click_content):
        shutil.copyfile(CLICK_SOURCE, click_path)
    ensure_meta(click_path)

    extension_path = OUTPUT / "settings-ui.json"
    write_json(extension_path, extension, compact=True)
    ensure_meta(extension_path)
    runtime_outputs = {
        extension_path.relative_to(ROOT).as_posix(): sha256(extension_path.read_bytes()),
        click_path.relative_to(ROOT).as_posix(): sha256(click_path.read_bytes()),
    }
    sprite_outputs = {
        (SPRITES / name).relative_to(ROOT).as_posix(): digest
        for name, digest in sorted(staged_sprites.items())
    }
    source_manifest["runtime"] = {
        "generator": "scripts/prepare_unity_home_settings.py",
        "panel": "settings",
        "node_count": len(panel["nodes"]) + 1,
        "outputs": runtime_outputs,
        "sprites": sprite_outputs,
    }
    write_json(SOURCE_MANIFEST, source_manifest)
    return {
        "panel_nodes": len(panel["nodes"]) + 1,
        "sprites": len(staged_sprites),
        "outputs": len(runtime_outputs),
    }


if __name__ == "__main__":
    result = build()
    print("Prepared official Home Settings:", json.dumps(result, sort_keys=True))
