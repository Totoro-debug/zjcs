#!/usr/bin/env python3
"""Convert the pinned official skill appearance prefabs to the local UGUI node schema."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
BUNDLE = "prefabs_ui_appendappearance"
ICON_BUNDLE = "prefabs_ui_item"
SKILL_BUNDLE = "prefabs_ui_skill"
OUTPUT = ROOT / "unity" / "Assets" / "Game" / "Resources" / "Character"
SPRITES = OUTPUT / "SkillAppearanceSprites"
RUNTIME_JSON = OUTPUT / "skill-appearance-ui.json"
SOURCE_MANIFEST = ROOT / "assets" / "reference" / "character-appearance" / "skill-appearance-ui.json"
WARDROBE_TEXTS = ROOT / "unity" / "Assets" / "Official" / "CharacterCreation" / "wardrobe-ui.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def guid(path: str) -> str:
    return sha256(("zjcs-unity:" + path).encode("utf-8"))[:32]


def png_meta(path: str, pixels_per_unit: float) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {guid(path)}\nTextureImporter:\n"
            "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n"
            "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n  sRGBTexture: 1\n"
            "  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n"
            "  alphaIsTransparency: 1\n  spriteMode: 1\n"
            f"  spritePixelsToUnits: {pixels_per_unit:g}\n"
            "  spriteMeshType: 1\n  textureCompression: 1\n  platformSettings: []\n"
            "  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n"
            "    physicsShape: []\n    bones: []\n    spriteID: \n    internalID: 0\n"
            "    vertices: []\n    indices: \n    edges: []\n    weights: []\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def folder_meta(path: str) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def text_meta(path: str) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {guid(path)}\nTextScriptImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("ascii")


def safe(value):
    if isinstance(value, dict):
        return {str(key): safe(item) for key, item in value.items()
                if not (key == "m_FileID" and "m_PathID" in value)}
    if isinstance(value, (list, tuple)):
        return [safe(item) for item in value]
    if isinstance(value, (bool, int, float, str)) or value is None:
        return value
    return None


def convert() -> tuple[dict, dict[str, bytes], dict]:
    sys.path.insert(0, str(ROOT / "scripts"))
    sys.path.insert(0, str(ROOT / ".tools" / "character-art-python"))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from prepare_character_creation_opening import SpriteCache, decode_image, decode_text, prefab_walk
    from prepare_character_skill_skin_videos import load_official_rows

    apk = Apk(VERSION)
    schemas = ScriptSchemas()
    sprite_cache = SpriteCache(apk)
    sprite_payloads: dict[str, bytes] = {}
    sprite_records: dict[str, dict] = {}
    layout_records: list[dict] = []

    try:
        texts_document = json.loads(WARDROBE_TEXTS.read_text(encoding="utf-8"))
        texts = texts_document["texts"]
    except (OSError, KeyError, json.JSONDecodeError) as error:
        raise ValueError("The existing official appearance localization table is unavailable.") from error

    def image_data(reader, node_path):
        fields = schemas.read(reader)
        decoded = decode_image(fields)
        file_id, path_id = decoded["sprite"]
        sprite_data = None
        if path_id:
            sprite_object = sprite_cache.lookup(reader.assets_file, file_id, path_id)
            if sprite_object is None or sprite_object.type.name != "Sprite":
                raise ValueError("The official skill appearance UI references a missing Sprite at " + node_path)
            sprite_data = sprite_cache.sprite_image(reader.assets_file, file_id, path_id)
            if sprite_data is None:
                raise ValueError("The official skill appearance Sprite could not be decoded at " + node_path)
            encoded = io.BytesIO()
            sprite_data["image"].save(encoded, format="PNG")
            payload = encoded.getvalue()
            filename = re.sub(r"[^A-Za-z0-9_.-]+", "_", sprite_data["name"]).strip("._")
            filename = (filename or "sprite") + "-" + sha256(payload)[:10] + ".png"
            sprite_payloads[filename] = payload
            sprite_records[filename] = {
                "name": sprite_data["name"],
                "path": "Character/SkillAppearanceSprites/" + filename,
                "sha256": sha256(payload),
                "source": apk.provenance(sprite_object),
                "object_path": node_path,
                "size": sprite_data["rect"],
                "pivot": sprite_data["pivot"],
                "border": sprite_data["border"],
                "pixels_per_unit": sprite_data["ppu"],
            }
        return {
            "color": decoded["color"],
            "type": decoded["type"],
            "preserve_aspect": decoded["preserve"],
            "sprite": None if sprite_data is None else {
                "file": "res://character-skill-appearance/sprites/" + filename,
                "border": sprite_data["border"],
            },
        }

    def convert_prefab(prefab_path: str, subtree_name: str | None = None,
                       bundle: str = BUNDLE) -> tuple[dict, dict[int, str], object]:
        pointer = apk.asset(bundle, prefab_path)
        prefab = pointer.read()
        root_path_id = pointer.path_id
        if subtree_name is not None:
            def find_subtree(game_object, game_object_path_id):
                if game_object.m_Name == subtree_name:
                    return game_object, game_object_path_id
                transform = next((pair.component.read() for pair in game_object.m_Component
                                  if pair.component.type.name in ("RectTransform", "Transform")), None)
                if transform is not None:
                    for child in transform.m_Children:
                        child_game_object = child.read().m_GameObject
                        found = find_subtree(child_game_object.read(), child_game_object.path_id)
                        if found is not None:
                            return found
                return None
            subtree = find_subtree(prefab, root_path_id)
            if subtree is None:
                raise ValueError("The official appearance panel has no " + subtree_name + " node.")
            prefab, root_path_id = subtree
        component_paths: dict[int, str] = {}

        def build(game_object, parent_path: str, game_object_path_id: int) -> dict:
            path = parent_path + "/" + game_object.m_Name
            component_paths[game_object_path_id] = path
            node = {
                "name": game_object.m_Name,
                "path": path,
                "active": bool(game_object.m_IsActive),
                "anchor_min": [0.0, 0.0],
                "anchor_max": [0.0, 0.0],
                "anchored_position": [0.0, 0.0],
                "size_delta": [0.0, 0.0],
                "pivot": [0.5, 0.5],
                "scale": [1.0, 1.0, 1.0],
                "rotation": 0.0,
                "scripts": [],
                "children": [],
            }
            transform = None
            for pair in game_object.m_Component:
                reader = pair.component.deref()
                kind = reader.type.name
                component_paths[reader.path_id] = path
                if kind in ("Transform", "RectTransform"):
                    transform = reader.read()
                    if kind == "RectTransform":
                        rect = transform
                        node["anchor_min"] = [float(rect.m_AnchorMin.x), float(rect.m_AnchorMin.y)]
                        node["anchor_max"] = [float(rect.m_AnchorMax.x), float(rect.m_AnchorMax.y)]
                        node["anchored_position"] = [float(rect.m_AnchoredPosition.x), float(rect.m_AnchoredPosition.y)]
                        node["size_delta"] = [float(rect.m_SizeDelta.x), float(rect.m_SizeDelta.y)]
                        node["pivot"] = [float(rect.m_Pivot.x), float(rect.m_Pivot.y)]
                        node["scale"] = [float(rect.m_LocalScale.x), float(rect.m_LocalScale.y), float(rect.m_LocalScale.z)]
                        node["rotation"] = 0.0
                    continue
                if kind != "MonoBehaviour":
                    continue
                try:
                    basic = reader.read(check_read=False)
                    script = basic.m_Script.read()
                    class_name = script.m_ClassName
                except Exception as error:
                    raise ValueError("Could not identify official component at " + path) from error
                if class_name not in node["scripts"]:
                    node["scripts"].append(class_name)
                try:
                    if class_name == "Image":
                        node["image"] = image_data(reader, path)
                    elif class_name == "RawImage":
                        pass
                    elif class_name == "TextMeshProUGUI":
                        text = decode_text(reader.get_raw_data(), path)
                        alignment = int(text["alignment"])
                        horizontal = alignment & 0x3F
                        vertical = alignment & ~0x3F
                        node["text"] = {
                            "value": text["text"],
                            "color": text["color"],
                            "font_size": text["font_size"],
                            "horizontal": horizontal or 2,
                            "vertical": vertical or 512,
                            "word_wrap": text["word_wrap"],
                            "overflow": text["overflow"],
                            "margin": text["margin"],
                        }
                    elif class_name == "MultiLanguageText":
                        key = schemas.read(reader).get("myKey", "")
                        if isinstance(key, str) and key:
                            node["localization_key"] = key
                    elif class_name == "AppendAppearanceSkillContent":
                        node.setdefault("original_bindings", {})[class_name] = safe(schemas.read(reader))
                    elif class_name == "ItemIconSkill":
                        node.setdefault("original_bindings", {})[class_name] = safe(schemas.read(reader))
                    elif class_name == "SkillSkinGridItem":
                        node.setdefault("original_bindings", {})[class_name] = safe(schemas.read(reader))
                    elif class_name == "GridLayoutGroup":
                        node.setdefault("original_bindings", {})[class_name] = safe(schemas.read(reader))
                    elif class_name == "LoopScrollGridLayoutGroup":
                        raw = reader.get_raw_data()
                        if len(raw) != 104:
                            raise ValueError("Unexpected original loop grid data extent at " + path)
                        flexible, min_x, min_y, minimum, maximum = struct.unpack_from("<i2f2i", raw, 84)
                        if flexible not in (0, 1) or not all(map(math.isfinite, (min_x, min_y))):
                            raise ValueError("Invalid original loop grid settings at " + path)
                        node["grid_layout"] = {
                            "flexible": bool(flexible),
                            "min_spacing": [min_x, min_y],
                            "minimum": minimum,
                            "maximum": maximum,
                        }
                        layout_records.append({
                            **apk.provenance(reader),
                            "object_path": path,
                            "raw_size": len(raw),
                            "raw_sha256": sha256(raw),
                        })
                    elif class_name in ("Button", "UIButton", "TabItem"):
                        node["button"] = True
                    elif class_name in ("Mask",):
                        node["mask"] = True
                    elif class_name == "HorizontalLayoutGroup":
                        node["layout"] = {"axis": 0, "padding": [0, 0, 0, 0], "alignment": 4,
                                           "spacing": 0, "expand": [False, False], "control": [False, False],
                                           "child_scale": [False, False], "reverse": False}
                    elif class_name == "VerticalLayoutGroup":
                        node["layout"] = {"axis": 1, "padding": [0, 0, 0, 0], "alignment": 4,
                                           "spacing": 0, "expand": [False, False], "control": [False, False],
                                           "child_scale": [False, False], "reverse": False}
                    elif class_name == "AppendAppearanceFirstTabItem":
                        binding = schemas.read(reader)
                        node.setdefault("original_bindings", {})[class_name] = safe(binding)
                except Exception as error:
                    if class_name in ("Image", "TextMeshProUGUI", "MultiLanguageText",
                                      "AppendAppearanceFirstTabItem", "ItemIconSkill", "SkillSkinGridItem",
                                      "GridLayoutGroup", "LoopScrollGridLayoutGroup"):
                        raise ValueError("Could not decode official " + class_name + " at " + path) from error
                if class_name in ("ScrollRect", "LoopVerticalScrollRect"):
                    node.setdefault("scripts", []).append("ScrollRect")
                if class_name == "RectMask2D" and "RectMask2D" not in node["scripts"]:
                    node["scripts"].append("RectMask2D")
            if node.get("text") is not None:
                key = node.get("localization_key", "")
                if key in texts:
                    node["text"]["value"] = texts[key]
            if transform is not None:
                children = []
                for child in transform.m_Children:
                    child_game_object = child.read().m_GameObject
                    children.append(build(child_game_object.read(), path, child_game_object.path_id))
                node["children"] = children
            return node

        return build(prefab, "", root_path_id), component_paths, pointer

    skill_root, skill_component_paths, skill_pointer = convert_prefab("/AppearanceSkillContent.prefab")
    cell_root, cell_paths, cell_pointer = convert_prefab("/SkillSkinGridItem.prefab", bundle=SKILL_BUNDLE)
    icon_root, icon_paths, icon_pointer = convert_prefab("/ItemIconSkill.prefab", bundle=ICON_BUNDLE)
    attachment_config_pointer = apk.asset("scriptableconfig_modules_itemicon", "/ItemIconAttachmentConfig.asset")
    attachment_config = schemas.read(attachment_config_pointer)
    raw_manifest = apk.archive.read(
        "assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes")
    guid_paths = {match[2].decode(): match[1].decode()
                  for match in re.finditer(rb"(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})", raw_manifest)}

    def convert_attachment(attachment_type: int) -> tuple[dict, dict, object]:
        row = next(entry for entry in attachment_config["Templates"]
                   if int(entry["Type"]) == attachment_type)
        asset_guid = row["GuidProxy"]["mAssetGuid"]
        asset_path = guid_paths.get(asset_guid)
        if not asset_path or "Assets/Prefabs/UI/Item/" not in asset_path:
            raise ValueError("The pinned item attachment asset path is missing for type "
                             + str(attachment_type))
        asset_key = "defaultpackage_" + asset_path.replace("/", "_").lower()
        bundle = max((name for name in apk.index if asset_key.startswith(name.removesuffix(".bundle"))),
                     key=len)
        prefab_path = "/" + asset_path.split("Assets/Prefabs/UI/Item/", 1)[1]
        root, _, pointer = convert_prefab(prefab_path, bundle=bundle.removesuffix(".bundle").removeprefix("defaultpackage_assets_"))
        return root, {
            "type": attachment_type,
            "guid": asset_guid,
            "asset_path": asset_path,
            "config": safe(row),
            "provenance": apk.provenance(pointer),
        }, pointer

    wearing_root, wearing_source, wearing_pointer = convert_attachment(12)
    new_root, new_source, new_pointer = convert_attachment(4)
    video_root, _, appearance_pointer = convert_prefab("/AppearancePanel.prefab", "SkillColorfulBg")
    content_bindings = skill_root.get("original_bindings", {}).get("AppendAppearanceSkillContent", {})
    tab_rows = content_bindings.get("mTabDatas", [])
    tabs = []
    for row in tab_rows:
        pointer = row.get("TabItem", {})
        path = skill_component_paths.get(int(pointer.get("m_PathID", 0)))
        if not path:
            raise ValueError("The official skill appearance tab reference is not present in its prefab tree.")
        tabs.append({"path": path, "item_type": int(row["TabType"])})
    if len(tabs) != 2 or {entry["item_type"] for entry in tabs} != {100, 101}:
        raise ValueError("The official skill appearance tabs differ from active/passive ItemType values.")
    cell_component = cell_root.get("original_bindings", {}).get("SkillSkinGridItem", {})
    slot_object = cell_component.get("mSlot", {})
    icon_slot_path = cell_paths.get(int(slot_object.get("m_PathID", 0))) \
        if isinstance(slot_object, dict) else None
    if not icon_slot_path:
        raise ValueError("The original SkillSkinGridItem slot binding is missing.")
    icon_component = icon_root.get("original_bindings", {}).get("ItemIconSkill", {})
    selected_object = icon_component.get("SelectedObj", {})
    selected_path = icon_paths.get(int(selected_object.get("m_PathID", 0))) \
        if isinstance(selected_object, dict) else None
    if not selected_path:
        raise ValueError("The original ItemIconSkill selected-object binding is missing.")

    skill_rows = load_official_rows()
    payload = {
        "manifest": VERSION,
        "source": {
            "bundle": apk.provenance(skill_pointer),
            "skill_icon_bundle": apk.provenance(icon_pointer),
            "skill_cell_bundle": apk.provenance(cell_pointer),
            "skill_attachment_config": apk.provenance(attachment_config_pointer),
            "skill_attachments": [wearing_source, new_source],
            "skill_content_prefab": "/AppearanceSkillContent.prefab",
            "grid_item_prefab": "/SkillSkinGridItem.prefab",
            "skill_icon_prefab": "/ItemIconSkill.prefab",
        },
        "texts": {key: texts[key] for key in sorted(texts)
                  if key in ("ui_skil_skin_btn", "ui_skill_skin_take_off_btn", "ui_skill_skin_unselect",
                             "ItemType.ActiveSkill", "ItemType.PassiveSkill", "ui_append_appearance_null_text")},
        "tabs": tabs,
        "layout_components": layout_records,
        "skill_skins": [
            {"item_id": int(row["ClassId"]), "skill_id": int(row["SkillId"]),
             "effect_skill_id": int(row["EffectSkillId"]), "skin_type": int(row["SkinType"])}
            for row in skill_rows
        ],
        "root": skill_root,
        "grid_item": {"root": cell_root},
        "skill_icon_slot_path": icon_slot_path,
        "skill_icon": icon_root,
        "skill_icon_selected_path": selected_path,
        "skill_wearing_attachment": wearing_root,
        "skill_new_attachment": new_root,
        "video_root": video_root,
    }

    sprite_outputs = {}
    for filename, data in sprite_payloads.items():
        relative = "Assets/Game/Resources/Character/SkillAppearanceSprites/" + filename
        record = sprite_records[filename]
        meta = png_meta(relative, float(record["pixels_per_unit"]))
        sprite_outputs["Character/SkillAppearanceSprites/" + filename] = data
        sprite_outputs["Character/SkillAppearanceSprites/" + filename + ".meta"] = meta
    runtime_bytes = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    sprite_outputs["Character/skill-appearance-ui.json"] = runtime_bytes
    sprite_outputs["Character/skill-appearance-ui.json.meta"] = text_meta(
        "Assets/Game/Resources/Character/skill-appearance-ui.json")
    source = {
        "manifest": VERSION,
        "bundle": payload["source"]["bundle"],
        "prefabs": [
            {"path": "/AppearanceSkillContent.prefab", "provenance": apk.provenance(skill_pointer)},
            {"path": "/SkillSkinGridItem.prefab", "provenance": apk.provenance(cell_pointer)},
            {"path": "/ItemIconSkill.prefab", "provenance": apk.provenance(icon_pointer)},
            {"path": "/Attachments/WearingAppendAppearanceGo.prefab", "provenance": apk.provenance(wearing_pointer),
             "attachment": wearing_source},
            {"path": "/Attachments/NewMark.prefab", "provenance": apk.provenance(new_pointer),
             "attachment": new_source},
            {"path": "/AppearancePanel.prefab#SkillColorfulBg", "provenance": apk.provenance(appearance_pointer)},
        ],
        "attachment_config": {
            "provenance": apk.provenance(attachment_config_pointer),
            "sha256": sha256(attachment_config_pointer.get_raw_data()),
        },
        "localization": {
            "path": WARDROBE_TEXTS.relative_to(ROOT).as_posix(),
            "sha256": sha256(WARDROBE_TEXTS.read_bytes()),
        },
        "skill_skin_rows": len(skill_rows),
        "tabs": tabs,
        "layout_components": layout_records,
        "sprites": [sprite_records[name] for name in sorted(sprite_records)],
        "outputs": {name: sha256(data) for name, data in sorted(sprite_outputs.items())},
        "runtime_sha256": sha256(runtime_bytes),
    }
    return payload, sprite_outputs, source


def write_or_check(outputs: dict[str, bytes], source: dict, check: bool) -> None:
    expected_source = (json.dumps(source, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    all_outputs = dict(outputs)
    all_outputs["source-manifest"] = expected_source
    previous = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8")) if SOURCE_MANIFEST.is_file() else None
    if previous is not None:
        same_inputs = (previous.get("manifest") == source["manifest"]
                       and previous.get("bundle") == source["bundle"]
                       and previous.get("localization") == source["localization"])
        if not same_inputs:
            raise ValueError("Existing skill appearance outputs use different pinned inputs.")
        for relative, old_hash in previous.get("outputs", {}).items():
            if relative in all_outputs:
                continue
            target = OUTPUT / relative[len("Character/"):] if relative.startswith("Character/") else None
            if target is None or not target.is_file():
                continue
            if sha256(target.read_bytes()) != old_hash:
                raise ValueError("Refusing to remove changed obsolete skill appearance output: " + str(target))
            if check:
                raise ValueError("Obsolete skill appearance output is still present: " + str(target))
            target.unlink()
    for relative, data in all_outputs.items():
        target = (OUTPUT / relative[len("Character/"):]) if relative.startswith("Character/") else SOURCE_MANIFEST
        if check:
            if not target.is_file() or target.read_bytes() != data:
                raise ValueError("Official skill appearance UI output is missing or changed: " + str(target))
            continue
        if target.is_file() and target.read_bytes() != data:
            prior_hash = previous.get("outputs", {}).get(relative) if previous is not None else None
            if relative == "source-manifest":
                safe_to_update = previous is not None and same_inputs
            else:
                safe_to_update = prior_hash is not None and sha256(target.read_bytes()) == prior_hash
            if not safe_to_update:
                raise ValueError("Refusing to overwrite different skill appearance UI content: " + str(target))
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_bytes() != data:
            target.write_bytes(data)
    if check:
        expected_folder = folder_meta("Assets/Game/Resources/Character/SkillAppearanceSprites")
        folder = SPRITES.with_suffix(".meta")
        if not folder.is_file() or folder.read_bytes() != expected_folder:
            raise ValueError("Official skill appearance sprite folder metadata is missing or changed.")
    else:
        folder = SPRITES.with_suffix(".meta")
        expected_folder = folder_meta("Assets/Game/Resources/Character/SkillAppearanceSprites")
        if folder.is_file() and folder.read_bytes() != expected_folder:
            raise ValueError("Refusing to overwrite different official skill appearance folder metadata.")
        if not folder.exists():
            folder.write_bytes(expected_folder)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify converted prefabs and outputs without writing.")
    args = parser.parse_args()
    _, outputs, source = convert()
    write_or_check(outputs, source, args.check)
    print(f"Verified {VERSION}: {source['skill_skin_rows']} skill skin rows, {len(source['sprites'])} prefab sprites.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
