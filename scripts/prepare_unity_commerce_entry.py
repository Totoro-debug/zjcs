"""Merge the official function-entry UI into the commerce runtime payload.

The shop window is opened from the HUD function menu (official
`FunctionEntryDialog` + `FunctionEntryShopItem`, driven by `ShopEntryBtn`). Those
prefabs are part of the pinned `prefabs_ui_functionentry` bundle and were already
extracted into `assets/official/ui.json`; this script copies the exact trees and
their sprites into the commerce payload without redrawing any visual.
"""

import hashlib
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAYLOAD = ROOT / "unity/Assets/Official/Commerce"
UI_JSON = ROOT / "assets/official/ui.json"
COMMERCE_UI = PAYLOAD / "commerce-ui.json"
SOURCE_MANIFEST = PAYLOAD / "source-manifest.json"
OFFICIAL_SOURCES = ROOT / "assets/reference/official-city/source-manifest.json"
REFERENCE_MANIFEST = ROOT / "assets/reference/commerce-ui/source-manifest.json"
SPRITE_DIR = PAYLOAD / "Art/sprites"
SPRITE_SOURCE = ROOT / "assets/official/sprites"
MANIFEST = "854_156383"
FUNCTION_BUNDLE = "defaultpackage_assets_prefabs_ui_functionentry.bundle"
FUNCTION_BUNDLE_HASH = "d65cab44dd21c58f4b48325e4b06ac26"


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def find_node(root, name):
    stack = [root]
    while stack:
        node = stack.pop()
        if node.get("name") == name:
            return node
        stack.extend(reversed(node.get("children") or []))
    return None


def collect_sprites(node, result):
    image = node.get("image") or {}
    sprite = image.get("sprite") or {}
    if sprite.get("file"):
        result.add(sprite["file"])
    for child in node.get("children") or []:
        collect_sprites(child, result)


def collect_texts(node, result):
    key = node.get("localization_key")
    text = node.get("text") or {}
    value = text.get("value")
    if key and isinstance(value, str) and key not in result:
        result[key] = value
    for child in node.get("children") or []:
        collect_texts(child, result)


def ensure_sprite(uri, records):
    prefix = "res://assets/official/sprites/"
    if not uri.startswith(prefix):
        raise ValueError("Function entry sprite is outside the official sprite payload: " + uri)
    source = SPRITE_SOURCE / uri[len(prefix):]
    record = records.get(uri)
    if record is None or not source.is_file():
        raise FileNotFoundError("Official function entry sprite or source record is missing: " + uri)
    if sha256(source).lower() != record.get("sha256", "").lower():
        raise ValueError("Official function entry sprite SHA-256 differs from its manifest: " + uri)
    target = SPRITE_DIR / source.name
    SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    if target.exists() and sha256(target).lower() != record["sha256"].lower():
        raise ValueError("Existing commerce sprite differs from the pinned source: " + str(target))
    if not target.exists():
        target.write_bytes(source.read_bytes())
    meta = target.with_suffix(target.suffix + ".meta")
    if not meta.exists():
        template = next(SPRITE_DIR.glob("*.png.meta"))
        guid = hashlib.md5(str(target.relative_to(ROOT / "unity")).replace("\\", "/").encode()).hexdigest()
        meta.write_text(re.sub(r"(?m)^guid: [0-9a-f]{32}$", "guid: " + guid,
                               template.read_text(encoding="utf-8"), count=1), encoding="utf-8")
    if sha256(target).lower() != record["sha256"].lower():
        raise ValueError("Copied function entry sprite differs from its pinned source: " + str(target))
    return target, record


def build():
    ui = json.loads(UI_JSON.read_text(encoding="utf-8"))
    functions = ui.get("functions", {}).get("root")
    if functions is None or functions.get("name") != "FunctionEntryDialog":
        raise ValueError("The pinned official function entry dialog is unavailable.")
    shop_item = find_node(functions, "FunctionEntryShopItem")
    if shop_item is None or "FunctionEntryBigBtnItem" not in (shop_item.get("scripts") or []):
        raise ValueError("The pinned official FunctionEntryShopItem prefab is unavailable.")
    activity_entry = ui.get("activity_entry", {}).get("root")
    if activity_entry is None or activity_entry.get("name") != "ActivityCommonEntry":
        raise ValueError("The pinned official ActivityCommonEntry prefab is unavailable.")
    item_detail = ui.get("item_detail", {}).get("root")
    if item_detail is None or item_detail.get("name") != "ItemDetailDialog":
        raise ValueError("The pinned official ItemDetailDialog prefab is unavailable.")

    commerce = json.loads(COMMERCE_UI.read_text(encoding="utf-8"))
    prefabs = commerce.setdefault("prefabs", {})
    texts = commerce.setdefault("texts", {})
    ui_hash = sha256(UI_JSON)
    source = {
        "bundle": FUNCTION_BUNDLE,
        "bundle_hash": FUNCTION_BUNDLE_HASH,
        "origin": "apk_zip",
        "manifest": MANIFEST,
        "prefab": "/FunctionEntryDialog.prefab",
        "ui_json_sha256": ui_hash,
    }
    prefabs["commerce_functions"] = {"root": functions, "source": dict(source)}
    prefabs["commerce_shop_entry"] = {"root": shop_item, "source": dict(source, prefab="/FunctionEntryDialog.prefab#FunctionEntryShopItem")}
    activity_source = {
        "bundle": "defaultpackage_assets_prefabs_ui_activityentry.bundle",
        "bundle_hash": "21e9e72818419b37b3cb9f9d1b5c0c80",
        "origin": "apk_zip",
        "manifest": MANIFEST,
        "prefab": "/ActivityCommonEntry.prefab",
        "ui_json_sha256": ui_hash,
    }
    prefabs["commerce_activity_entry"] = {"root": activity_entry, "source": activity_source}
    detail_source = dict(ui.get("item_detail", {}).get("source") or {})
    detail_source.update({"origin": "apk_zip", "manifest": MANIFEST, "ui_json_sha256": ui_hash})
    prefabs["commerce_item_detail"] = {"root": item_detail, "source": detail_source}
    main_root = commerce.get("prefabs", {}).get("commerce_main", {}).get("root")
    first_entry = find_node(main_root, "AccumulatedPayEntryGo") if main_root else None
    if first_entry is None:
        raise ValueError("The official direct-pay entry prefab is unavailable.")
    prefabs["commerce_first_entry"] = {"root": first_entry, "source": dict(source, prefab="/ShopActivityEntryPanel.prefab#DirectPayTopGo/AccumulatedPayEntryGo")}

    collected = {}
    collect_texts(functions, collected)
    collect_texts(activity_entry, collected)
    collect_texts(item_detail, collected)
    for key, value in collected.items():
        texts.setdefault(key, value)

    sprite_uris = set()
    collect_sprites(functions, sprite_uris)
    collect_sprites(activity_entry, sprite_uris)
    collect_sprites(item_detail, sprite_uris)
    official = json.loads(OFFICIAL_SOURCES.read_text(encoding="utf-8"))
    records = official["resources"]
    copied = {}
    for uri in sorted(sprite_uris):
        target, record = ensure_sprite(uri, records)
        copied[uri] = {"path": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
                       "sha256": record["sha256"], "bytes": target.stat().st_size, "source": record}

    COMMERCE_UI.write_text(json.dumps(commerce, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")

    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    files = [entry for entry in manifest["files"]
             if not entry["path"].endswith("commerce-ui.json")
             and entry["path"] not in {row["path"] for row in copied.values()}]
    files.append({"bytes": COMMERCE_UI.stat().st_size,
                  "path": "Assets/Official/Commerce/commerce-ui.json",
                  "sha256": sha256(COMMERCE_UI)})
    for row in copied.values():
        files.append({"bytes": row["bytes"], "path": row["path"], "sha256": row["sha256"]})
    manifest["files"] = sorted(files, key=lambda entry: entry["path"])
    manifest["source_outputs"]["commerce-ui.json"] = sha256(COMMERCE_UI)
    manifest.setdefault("runtime_resources", {}).update({
        uri: {"output": row["path"], "sha256": row["sha256"], "source": row["source"]}
        for uri, row in copied.items()
    })
    manifest.setdefault("prefabs", {})["commerce_functions"] = dict(source)
    manifest["prefabs"]["commerce_shop_entry"] = dict(source, prefab="/FunctionEntryDialog.prefab#FunctionEntryShopItem")
    SOURCE_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    reference = json.loads(REFERENCE_MANIFEST.read_text(encoding="utf-8"))
    reference.setdefault("unity_runtime_resources", {}).update({
        uri: {"path": row["path"], "sha256": row["sha256"]} for uri, row in copied.items()
    })
    reference.setdefault("runtime_panel_sources", {})["commerce_functions"] = dict(source)
    reference["runtime_panel_sources"]["commerce_shop_entry"] = dict(
        source, prefab="/FunctionEntryDialog.prefab#FunctionEntryShopItem")
    reference["source_ui_sha256"] = ui_hash
    REFERENCE_MANIFEST.write_text(json.dumps(reference, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Commerce function entry merged: 2 prefabs, " + str(len(collected)) + " texts, "
          + str(len(copied)) + " sprites")


if __name__ == "__main__":
    build()
