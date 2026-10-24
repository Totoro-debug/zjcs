"""Convert extracted official NPC travel prefab trees to HomeUiCatalog payloads."""
import json
import hashlib
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "official" / "npc-travel-ui"
OFFICIAL = ROOT / "assets" / "official"
OUTPUT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Home" / "Travel"
PANEL_KEYS = {
    "travel_dialog", "travel_npc", "travel_condition", "travel_start_status",
    "travel_end_status", "travel_reward", "travel_brief", "travel_refresh_confirm",
    "travel_condition_tip", "travel_friendship", "travel_friendship_preview",
}
OFFICIAL_UI_KEYS = {"npc_adventure", "npc_travel", "npc_travel_select"}


def node_copy(source):
    node = {key: value for key, value in source.items() if key != "children"}
    image = node.get("image")
    if image and image.get("sprite"):
        file = image["sprite"].get("file", "")
        if file.startswith("res://"):
            image["sprite"]["file"] = Path(file).name
    return node


def main():
    source = json.loads((SOURCE / "panels.json").read_text(encoding="utf-8"))
    official_ui_path = ROOT / "assets" / "official" / "ui.json"
    official_ui = json.loads(official_ui_path.read_text(encoding="utf-8"))
    travel_catalog = json.loads((OUTPUT.parent / "travel-catalog.json").read_text(encoding="utf-8"))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sprite_output = OUTPUT / "sprites"
    sprite_output.mkdir(exist_ok=True)
    panels = []
    used_sprites = set()
    dynamic_sprites = []
    missing_npc_avatars = []
    sprite_sources = {}
    entry_icon = "note_NpcAdventureEntry-6322981017.png"
    if (OFFICIAL / "sprites" / entry_icon).is_file():
        used_sprites.add(entry_icon)
    for key, prefab in source["prefabs"].items():
        if key not in PANEL_KEYS:
            continue
        root_source = prefab["root"]
        root = node_copy(root_source)
        nodes = []
        def visit(parent, parent_path):
            for child in parent.get("children", []):
                item = node_copy(child)
                item["parent_path"] = parent_path
                nodes.append(item)
                sprite = (child.get("image") or {}).get("sprite") or {}
                file = sprite.get("file", "")
                if file:
                    used_sprites.add(Path(file).name)
                visit(child, child["path"])
        root_sprite = (root_source.get("image") or {}).get("sprite") or {}
        if root_sprite.get("file"):
            used_sprites.add(Path(root_sprite["file"]).name)
        visit(root_source, root_source["path"])
        panels.append({"key": key, "root": root, "nodes": nodes})

    for npc in travel_catalog["npcs"]:
        if not npc.get("can_develop") or npc.get("travel_profession", 0) <= 0:
            continue
        avatar_found = False
        for field in ("figure_image", "icon"):
            configured_path = npc.get(field, "")
            avatar_key = Path(configured_path).name
            if not avatar_key:
                continue
            candidates = sorted((OFFICIAL / "sprites").glob(avatar_key + "-*.png"))
            if not candidates:
                candidates = sorted(OFFICIAL.glob("*/sprites/" + avatar_key + "-*.png"))
            if not candidates:
                continue
            icon_path = candidates[0]
            filename = icon_path.name
            used_sprites.add(filename)
            sprite_sources[filename] = icon_path
            dynamic_sprites.append({
                "key": avatar_key,
                "file": filename,
                "npc_id": npc["id"],
                "source_field": field,
                "configured_path": configured_path,
                "source": icon_path.relative_to(ROOT).as_posix(),
                "source_sha256": hashlib.sha256(icon_path.read_bytes()).hexdigest(),
            })
            avatar_found = True
            break
        if not avatar_found:
            missing_npc_avatars.append({
                "npc_id": npc["id"],
                "figure_image": npc.get("figure_image", ""),
                "icon": npc.get("icon", ""),
                "figure_model_ui": npc.get("figure_model_ui", ""),
                "figure_model_with_weapon_ui": npc.get("figure_model_with_weapon_ui", ""),
            })

    # The official settings asset orders 10 map groups, 4 professions, then 4 preferences.
    category_specs = (
        ("map_group", "map_group_id", "map_groups", 1, 10),
        ("profession", "travel_profession", "professions", 11, 4),
        ("preference", "preference_type", "preferences", 15, 4),
    )
    for kind, npc_field, travel_field, icon_start, expected_count in category_specs:
        category_ids = {
            npc.get(npc_field, 0) for npc in travel_catalog["npcs"] if npc.get(npc_field, 0) > 0
        }
        category_ids.update(
            requirement["id"]
            for row in travel_catalog["travels"]
            for requirement in row.get(travel_field, [])
            if requirement.get("id", 0) > 0
        )
        category_ids = sorted(category_ids)
        if len(category_ids) != expected_count:
            raise ValueError(f"Official NPC travel {kind} icon map changed: {category_ids}")
        for offset, category_id in enumerate(category_ids):
            icon_name = f"NpcAdventure_{icon_start + offset}"
            candidates = sorted(SOURCE.glob("sprites/" + icon_name + "-*.png"))
            if not candidates:
                candidates = sorted((OFFICIAL / "sprites").glob(icon_name + "-*.png"))
            if not candidates:
                raise FileNotFoundError("Official travel category icon was not extracted: " + icon_name)
            icon_path = candidates[0]
            filename = icon_path.name
            used_sprites.add(filename)
            sprite_sources[filename] = icon_path
            dynamic_sprites.append({
                "key": f"travel-{kind}-{category_id}",
                "file": filename,
                "category_id": category_id,
                "source": icon_path.relative_to(ROOT).as_posix(),
                "source_sha256": hashlib.sha256(icon_path.read_bytes()).hexdigest(),
            })

    official_ui_keys = OFFICIAL_UI_KEYS | {
        key for key in official_ui if key.startswith("npc_map_")
    }
    for key in sorted(official_ui_keys):
        root_source = official_ui[key]["root"]
        root = node_copy(root_source)
        nodes = []
        def visit_official(parent):
            for child in parent.get("children", []):
                item = node_copy(child)
                item["parent_path"] = parent["path"]
                nodes.append(item)
                sprite = (child.get("image") or {}).get("sprite") or {}
                if sprite.get("file"):
                    used_sprites.add(Path(sprite["file"]).name)
                visit_official(child)
        root_sprite = (root_source.get("image") or {}).get("sprite") or {}
        if root_sprite.get("file"):
            used_sprites.add(Path(root_sprite["file"]).name)
        visit_official(root_source)
        panels.append({"key": key, "root": root, "nodes": nodes})

    copied = []
    meta_hashes = {}
    for filename in sorted(used_sprites):
        candidates = list(SOURCE.glob("sprites/" + filename))
        if not candidates and filename in sprite_sources:
            candidates = [sprite_sources[filename]]
        if not candidates and (ROOT / "assets" / "official" / "sprites" / filename).is_file():
            candidates = [ROOT / "assets" / "official" / "sprites" / filename]
        if not candidates:
            raise FileNotFoundError("Official travel sprite was not extracted: " + filename)
        shutil.copy2(candidates[0], sprite_output / filename)
        copied.append(filename)
        sprite_path = sprite_output / filename
        guid = hashlib.sha256(("npc-travel-ui:" + filename).encode("utf-8")).hexdigest()[:32]
        meta_path = Path(str(sprite_path) + ".meta")
        meta_path.write_text(
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8",
        )
        meta_hashes[filename] = hashlib.sha256(meta_path.read_bytes()).hexdigest()
    from npc_travel_language import language
    text_keys = {f"npc_adventure_travel_{suffix}_{row['travel_id']}"
                 for row in travel_catalog["travels"] for suffix in ("name", "desc")}
    text_keys.update(npc["name_key"] for npc in travel_catalog["npcs"] if npc.get("name_key"))
    texts, text_sources = language(text_keys)
    payload = {
        "version": source["manifest"], "panels": panels,
        "dynamic_sprites": [{"key": icon["key"], "file": icon["file"]}
                            for icon in dynamic_sprites],
        "texts": [{"key": key, "value": value} for key, value in sorted(texts.items())],
    }
    output_file = OUTPUT / "ui.json"
    output_file.write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    receipt = {
        "manifest": source["manifest"],
        "input": "assets/official/npc-travel-ui/panels.json",
        "input_sha256": hashlib.sha256((SOURCE / "panels.json").read_bytes()).hexdigest(),
        "official_ui_input": "assets/official/ui.json",
        "official_ui_input_sha256": hashlib.sha256(official_ui_path.read_bytes()).hexdigest(),
        "official_ui_keys": sorted(official_ui_keys),
        "language_sources": text_sources,
        "language_keys": len(text_keys),
        "language_resolved": len(texts),
        "dynamic_sprites": dynamic_sprites,
        "missing_npc_avatars": missing_npc_avatars,
        "outputs": {
            "ui.json": hashlib.sha256(output_file.read_bytes()).hexdigest(),
            "sprites": {name: hashlib.sha256((sprite_output / name).read_bytes()).hexdigest()
                        for name in copied},
            "sprite_meta": meta_hashes,
        },
    }
    receipt_path = OUTPUT / "source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"Wrote {len(panels)} official travel panels and {len(copied)} sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
