"""Convert verified Fantasy Tower panel trees to the shared Unity UGUI payload."""
from __future__ import annotations

import hashlib
import io
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/official/tower-push/panels.json"
OUTPUT = ROOT / "unity/Assets/StreamingAssets/TowerPush/ui.json"
SPRITES = ROOT / "unity/Assets/StreamingAssets/TowerPush/sprites"
SOURCE_SPRITES = ROOT / "assets/official/tower-push/sprites"
SOURCE_MANIFEST = ROOT / "assets/reference/tower-push/source-manifest.json"
PANELS = ("main", "end", "result", "notice")
OFFICIAL_PREFABS = {
    "entry": "StageDemonBattlefieldEntryPanel",
    "entry_item_1": "StageDemonBattlefieldEntryPanelItem",
    "stage_list": "StageDemonBattlefieldPanel",
    "stage_item_1": "StageMoonBattlefieldPanelItem",
    "stage_item_2": "StagePrisonBattelfieldPanelItem",
    "stage_item_3": "StageSeaBattlefieldPanelItem",
    "stage_item_4": "StageLoongBattlefieldPanelItem",
}
FIELDS = ("name", "path", "active", "anchor_min", "anchor_max", "anchored_position",
          "size_delta", "pivot", "scale", "rotation", "scripts", "image", "text",
          "localization_key", "mask", "mask_component_flags", "mask_enabled", "mask_show_graphic",
          "has_tab_group", "tab_group_allow_switch_off", "no_drawing_raycast", "raycast_target",
          "raycast_padding", "rectmask2d", "layout", "grid", "size_fitter", "layout_element",
          "aspect_fitter", "alpha", "button", "scroll", "animations")


def flat(node: dict, parent: str = "") -> tuple[dict, list[dict]]:
    record = {key: node[key] for key in FIELDS if key in node}
    record["parent_path"] = parent
    record["path"] = node["path"]
    descendants = []
    for child in node.get("children", []):
        child_record, child_descendants = flat(child, node["path"])
        descendants.append(child_record)
        descendants.extend(child_descendants)
    return record, descendants


def asset_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode()).hexdigest()
    if path.suffix == ".png":
        body = (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
                "  externalObjects: {}\n  spriteMode: 1\n  textureType: 8\n"
                "  mipmapEnabled: 0\n  sRGBTexture: 1\n  alphaIsTransparency: 1\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        body = f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta = Path(str(path) + ".meta")
    if meta.exists() and meta.read_text(encoding="utf-8") != body:
        raise ValueError("Refusing to replace changed Unity metadata: " + str(meta))
    if not meta.exists():
        meta.write_text(body, encoding="utf-8", newline="\n")


def extract_official_prefabs() -> tuple[list[dict], dict[str, bytes], dict]:
    """Convert the entry and four official layer-row prefabs from the pinned bundle."""
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    sys.path.insert(0, str(ROOT / "scripts"))
    from prepare_cooking_module import CookingConverter

    converter = CookingConverter()
    container = converter.apk.container
    bundle_name = "defaultpackage_assets_prefabs_ui_demonbattlefield.bundle"
    converter.apk.container = lambda _name: container(bundle_name)
    panels = []
    sprite_bytes = {}
    input_records = {}
    dynamic_sprites = []
    for key, prefab in OFFICIAL_PREFABS.items():
        data, _ = converter.object_tree(prefab)
        by_index = data["nodes"]
        if not by_index or by_index[0]["parentIndex"] != -1:
            raise ValueError("Official tower prefab has no root: " + prefab)
        root = {"name": by_index[0]["name"], "path": by_index[0]["path"],
                "parent_path": "", "active": by_index[0]["active"],
                "anchor_min": by_index[0].get("anchor_min", [0, 0]),
                "anchor_max": by_index[0].get("anchor_max", [0, 0]),
                "anchored_position": by_index[0].get("anchored_position", [0, 0]),
                "size_delta": by_index[0].get("size_delta", [0, 0]),
                "pivot": by_index[0].get("pivot", [0.5, 0.5]),
                "scale": by_index[0].get("scale", [1, 1, 1]),
                "rotation": by_index[0].get("rotation", 0),
                "scripts": by_index[0].get("scripts", [])}
        for field in FIELDS:
            if field != "parent_path" and field in by_index[0]:
                root[field] = by_index[0][field]
        nodes = []
        for row in by_index:
            path = row["path"]
            if row["parentIndex"] < 0:
                continue
            record = {"parent_path": by_index[row["parentIndex"]]["path"],
                      "name": row["name"], "path": path, "active": row["active"],
                      "scripts": row.get("scripts", [])}
            for field in ("anchor_min", "anchor_max", "anchored_position", "size_delta",
                          "pivot", "scale", "rotation", "layout", "grid", "layout_element",
                          "size_fitter", "scroll", "alpha"):
                if field in row:
                    record[field] = row[field]
            if row.get("button"):
                record["button"] = True
            if row.get("mask"):
                record["mask"] = True
            if row.get("rectmask2d"):
                record["rectmask2d"] = True
            if row.get("image"):
                image = row["image"]
                filename = image["sprite"]
                output = next((value for value in converter.sprite_outputs.values()
                               if value["file"] == filename), None)
                if output is None:
                    raise ValueError("Official tower sprite export is missing: " + filename)
                sprite_bytes[filename] = output["bytes"]
                record["image"] = {"sprite": {
                    "file": "res://assets/official/tower-push/sprites/" + filename,
                    "name": next(key for key, value in converter.sprite_outputs.items()
                                 if value["file"] == filename),
                    "width": output["width"], "height": output["height"],
                    "border": output["border"], "ppu": output["pixels_per_unit"],
                    "pivot": output["pivot"], "rect_size": [output["width"], output["height"]],
                    "trim_offset": [0, 0], "trim_size": [output["width"], output["height"]]},
                    "color": image["color"], "type": image["type"],
                    "preserve_aspect": image["preserve_aspect"],
                    "raycast_target": image["raycast_target"]}
            if row.get("text"):
                text = row["text"]
                record["text"] = {"value": text["value"], "font_size": text["font_size"],
                                  "color": text["color"], "horizontal": text["horizontal"],
                                  "vertical": text["vertical"]}
            nodes.append(record)
        input_records[key] = {"source": data["source"],
                              "prefab": data["source_path"],
                              "node_count": len(by_index)}
        panels.append({"key": "fantasy_tower_" + key, "root": root, "nodes": nodes})
    notes_bundle = "defaultpackage_assets_image_ui_notes_stage.bundle"
    notes_sprites = {}
    for path, pointer in container(notes_bundle):
        if pointer.type.name != "Sprite" or "/Stage/ImgInletBg" not in path:
            continue
        sprite = pointer.deref()
        filename = converter.add_sprite(sprite)
        sprite_bytes[filename] = converter.sprite_outputs[sprite.read(check_read=False).m_Name]["bytes"]
        notes_sprites[sprite.read(check_read=False).m_Name] = filename
        input_records.setdefault("notes_stage", {"source": converter.apk.provenance(sprite),
            "prefab": path, "node_count": 0})
        input_records["sprite:" + sprite.read(check_read=False).m_Name] = {
            "source": converter.apk.provenance(sprite),
            "raw_sha256": hashlib.sha256(sprite.get_raw_data()).hexdigest(),
            "output": filename,
            "output_sha256": hashlib.sha256(converter.sprite_outputs[
                sprite.read(check_read=False).m_Name]["bytes"]).hexdigest()}
    for entry in range(1, 5):
        for suffix, kind in (("_1", "background"), ("_2", "icon")):
            key = "ImgInletBg" + str(entry) + suffix
            if key not in notes_sprites:
                raise ValueError("Official DemonBattleViewInfo sprite is missing: " + key)
            dynamic_sprites.append({"key": "fantasy_tower_entry_" + str(entry) + "_" + kind,
                                    "file": notes_sprites[key]})
    for name, value in converter.sprite_outputs.items():
        sprite_bytes[value["file"]] = value["bytes"]
    sprite_sources = {value["file"]: {"source": value["source"],
        "path_id": value["path_id"], "output_sha256": hashlib.sha256(value["bytes"]).hexdigest()}
        for value in converter.sprite_outputs.values()}
    return panels, sprite_bytes, input_records, dynamic_sprites, sprite_sources


def build() -> None:
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if manifest.get("manifest") != "854_156383" or manifest.get("outputs", {}).get(
            "res://assets/official/tower-push/panels.json") != hashlib.sha256(SOURCE.read_bytes()).hexdigest():
        raise ValueError("Fantasy Tower prefab source manifest does not match panels.json")
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    panel_data = []
    sprite_names = set()
    texts = source.get("texts", {})
    def collect(node: dict) -> None:
        image = node.get("image") or {}
        sprite = image.get("sprite") or {}
        file = sprite.get("file", "")
        if file.startswith("res://assets/official/tower-push/sprites/"):
            sprite_names.add(Path(file).name)
        for child in node.get("children", []):
            collect(child)

    for key in PANELS:
        tree = source[key]["root"]
        root, nodes = flat(tree)
        panel_data.append({"key": "fantasy_tower_" + key, "root": root, "nodes": nodes})
        collect(tree)
    extracted, extracted_sprites, prefab_inputs, dynamic_sprites, sprite_sources = extract_official_prefabs()
    panel_data.extend(extracted)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps({"version": "854_156383", "panels": panel_data,
        "dynamic_sprites": dynamic_sprites,
        "texts": [{"key": key, "value": value} for key, value in texts.items()]},
        ensure_ascii=False, separators=(",", ":")), encoding="utf-8", newline="\n")
    asset_meta(OUTPUT)
    SPRITES.mkdir(parents=True, exist_ok=True)
    asset_meta(SPRITES.parent)
    asset_meta(SPRITES)
    for name in sorted(sprite_names):
        target = SPRITES / name
        content = (SOURCE_SPRITES / name).read_bytes()
        if target.exists() and target.read_bytes() != content:
            raise ValueError("Refusing to replace changed official Sprite: " + str(target))
        if not target.exists():
            shutil.copyfile(SOURCE_SPRITES / name, target)
        asset_meta(target)
    for name, content in sorted(extracted_sprites.items()):
        target = SPRITES / name
        if target.exists() and target.read_bytes() != content:
            raise ValueError("Refusing to replace changed official Sprite: " + str(target))
        if not target.exists():
            target.write_bytes(content)
        asset_meta(target)
    all_sprite_names = sorted(set(sprite_names).union(extracted_sprites))
    for name in all_sprite_names:
        sprite_sources.setdefault(name, {"source": {"manifest": "assets/reference/tower-push/source-manifest.json",
            "file": "res://assets/official/tower-push/sprites/" + name},
            "output_sha256": hashlib.sha256((SPRITES / name).read_bytes()).hexdigest()})
    ui_manifest = {"manifest": "854_156383", "bundle":
        "defaultpackage_assets_prefabs_ui_demonbattlefield.bundle",
        "bundle_hash": "34ed49f088e56437eaffc20e8430494e",
        "bundle_sha256": "50d3609ada6ee44cad3b8cb5787fac483282b9b3b14d44214419bf2093fd4866",
        "prefabs": prefab_inputs,
        "sprites": sprite_sources,
        "outputs": {"unity/Assets/StreamingAssets/TowerPush/ui.json":
            hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
            **{"unity/Assets/StreamingAssets/TowerPush/sprites/" + name:
               hashlib.sha256((SPRITES / name).read_bytes()).hexdigest()
               for name in all_sprite_names}}}
    (ROOT / "assets/reference/tower-push/ui-source-manifest.json").parent.mkdir(parents=True, exist_ok=True)
    (ROOT / "assets/reference/tower-push/ui-source-manifest.json").write_text(
        json.dumps(ui_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    asset_meta(OUTPUT)
    print("Fantasy Tower UI panels:", len(panel_data), "sprites:",
          len(all_sprite_names))


if __name__ == "__main__":
    build()
