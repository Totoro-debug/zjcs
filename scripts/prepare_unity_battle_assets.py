"""Restore verified battle HUD and Spine data into Unity's local asset payload."""
import argparse
import hashlib
import json
import os
import re
import tempfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
SPINE_MANIFEST = "assets/reference/battle-spine/source-manifest.json"
HUD_MANIFEST = "assets/reference/battle-hud/source-manifest.json"
MODULE_MANIFEST = "Assets/Official/Combat/battle-presentation-source-manifest.json"
HUD_RUNTIME = "Assets/Official/Combat/HUD/panels-runtime.json"
HUD_RUNTIME_MANIFEST = "Assets/Official/Combat/HUD/panels-runtime-source-manifest.json"
CUSTOM_ACTION_RUNTIME = "Assets/Game/Resources/Combat/battle-custom-action-world-runtime.json"
CUSTOM_ACTION_RUNTIME_MANIFEST = "Assets/Game/Resources/Combat/battle-custom-action-world-runtime-source-manifest.json"
CUSTOM_ACTION_FONT_BINDING = {
    "official_name": "FZY4K SDF - dynamic",
    "source_bundle": "defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle",
    "source_bundle_hash": "bf62732f37a5a423ddbac02681e054a4",
    "source_bundle_sha256": "ebee921d1efee60b02445c5d09faebdef159fdd6c7284658d2c05cf9e3826434",
    "serialized_file": "CAB-43221b4d126447a2b398f3477e814a2c",
    "path_id": "-1054575886599457342",
    "runtime_asset_path": "Assets/Game/Resources/Combat/FZY4K-Dynamic.asset",
    "runtime_asset_guid": "e9b69727f25b00e4c82f8bc68bd859c5",
    "source_ttf_guid": "6318d84275f8ea84bb687a21c99efcf7",
    "source_ttf_sha256": "90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386",
}
UNIT_BARS_RUNTIME = "Assets/Game/Resources/Combat/unit-bars-runtime.json"
UNIT_BARS_RUNTIME_MANIFEST = "Assets/Game/Resources/Combat/unit-bars-runtime-source-manifest.json"
UNIT_BARS_SOURCE = "Assets/Official/Combat/HUD/unit-bars.json"
MODEL_CATALOG_SOURCE = "assets/official/battle-presentation/models.json"
MODEL_CATALOG_TARGET = "Assets/Official/Combat/Presentation/models.json"
MODEL_CATALOG_MANIFEST = "Assets/Official/Combat/Presentation/models-source-manifest.json"
MODEL_IDENTITIES_TARGET = "Assets/Official/Combat/Presentation/model-identities.json"
MODEL_IDENTITIES_MANIFEST = "Assets/Official/Combat/Presentation/model-identities-source-manifest.json"


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def target_path(unity_root, relative):
    root = unity_root.resolve()
    target = root.joinpath(*PurePosixPath(relative).parts)
    if target.is_symlink() or not target.resolve().is_relative_to(root):
        raise ValueError("Battle asset target escapes Unity project: " + relative)
    return target


def copy_verified(source, target, expected_hash):
    if target.exists():
        if sha256(target) != expected_hash:
            raise ValueError("Refusing to overwrite changed Unity battle asset: " + str(target))
        return False

    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-asset-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with source.open("rb") as input_stream, os.fdopen(handle, "wb") as output_stream:
            while block := input_stream.read(1024 * 1024):
                output_stream.write(block)
        if sha256(temporary) != expected_hash:
            raise ValueError("Copied battle asset hash differs: " + str(source))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def copy_bytes(content, target, expected_hash):
    if target.exists():
        if sha256(target) != expected_hash:
            raise ValueError("Refusing to overwrite changed Unity battle manifest: " + str(target))
        return False

    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-manifest-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        if sha256(temporary) != expected_hash:
            raise ValueError("Generated battle manifest hash differs: " + str(target))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def replace_verified_bytes(content, target, previous_hash):
    if sha256(target) != previous_hash:
        raise ValueError("Refusing to replace changed generated battle data: " + str(target))

    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-manifest-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def normalize_hud_node(node):
    node["alpha"] = node.get("alpha", 1.0)
    node["animation_play_automatically"] = bool(node.get("animation_play_automatically", False))
    node["animations"] = [normalize_hud_animation(animation)
                          for animation in node.get("animations", [])]
    node["has_image"] = isinstance(node.get("image"), dict)
    node["has_text"] = isinstance(node.get("text"), dict)
    node["has_scroll"] = isinstance(node.get("scroll"), dict)
    image = node.get("image") if node["has_image"] else {}
    sprite = image.get("sprite") if isinstance(image.get("sprite"), dict) else {}
    node["has_image_sprite"] = bool(sprite)
    for source, target, fallback in (
            ("file", "image_sprite_file", ""), ("width", "image_sprite_width", 0),
            ("height", "image_sprite_height", 0), ("border", "image_sprite_border", None),
            ("pivot", "image_sprite_pivot", None), ("ppu", "image_sprite_ppu", 0)):
        node[target] = sprite.get(source, fallback)
    node["image_color"] = image.get("color")
    node["image_type"] = image.get("type", 0)
    node["image_preserve_aspect"] = image.get("preserve_aspect", False)
    text = node.get("text") if node["has_text"] else {}
    for source, target, fallback in (
            ("value", "text_value", ""), ("color", "text_color", None),
            ("font_size", "text_font_size", 0), ("horizontal", "text_horizontal", 0),
            ("vertical", "text_vertical", 0), ("overflow", "text_overflow", 0),
            ("word_wrap", "text_word_wrap", False), ("margin", "text_margin", None),
            ("auto_size", "text_auto_size", None)):
        node[target] = text.get(source, fallback)
    scroll = node.get("scroll") if node["has_scroll"] else {}
    for source, target, fallback in (
            ("horizontal", "scroll_horizontal", False), ("vertical", "scroll_vertical", False),
            ("inertia", "scroll_inertia", False), ("movement", "scroll_movement", 0),
            ("sensitivity", "scroll_sensitivity", 0), ("elasticity", "scroll_elasticity", 0),
            ("deceleration", "scroll_deceleration", 0)):
        node[target] = scroll.get(source, fallback)
    layout = node.get("layout") if isinstance(node.get("layout"), dict) else {}
    node["has_layout"] = bool(layout)
    for source, target, fallback in (
            ("axis", "layout_axis", 0), ("padding", "layout_padding", None),
            ("alignment", "layout_alignment", 0), ("spacing", "layout_spacing", 0),
            ("spacing_list", "layout_spacing_list", None), ("expand", "layout_expand", None),
            ("control", "layout_control", None), ("child_scale", "layout_child_scale", None),
            ("reverse", "layout_reverse", False)):
        node[target] = layout.get(source, fallback)
    fitter = node.get("size_fitter")
    node["has_size_fitter"] = isinstance(fitter, list) and len(fitter) >= 2
    node["fitter_horizontal"] = fitter[0] if node["has_size_fitter"] else 0
    node["fitter_vertical"] = fitter[1] if node["has_size_fitter"] else 0
    element = node.get("layout_element") if isinstance(node.get("layout_element"), dict) else {}
    node["has_layout_element"] = bool(element)
    node["layout_element_ignore"] = element.get("ignore", False)
    node["layout_element_min"] = element.get("min")
    node["layout_element_preferred"] = element.get("preferred")
    node["layout_element_flexible"] = element.get("flexible")
    for child in node.get("children", []):
        normalize_hud_node(child)


def normalize_hud_animation(animation):
    tracks = []
    for track in animation.get("tracks", []):
        keys = []
        for key in track.get("keys", []):
            if not isinstance(key, list) or len(key) < 2:
                raise ValueError("Official battle HUD animation has an invalid key.")
            keys.append({
                "time": key[0],
                "value": key[1],
                "in_tangent": key[2] if len(key) > 2 and key[2] is not None else 0.0,
                "out_tangent": key[3] if len(key) > 3 and key[3] is not None else 0.0,
            })
        tracks.append({
            "path": track.get("path", ""),
            "property": track.get("property", ""),
            "keys": keys,
        })
    return {
        "name": animation.get("name", ""),
        "owner": animation.get("owner", ""),
        "duration": animation.get("duration", 0.0),
        "loop": animation.get("loop", False),
        "tracks": tracks,
    }


def flatten_hud_node(node, parent_index, rows):
    index = len(rows)
    entry = dict(node)
    children = entry.pop("children", [])
    entry["parentIndex"] = parent_index
    rows.append(entry)
    for child in children:
        flatten_hud_node(child, index, rows)


def normalize_world_float_node(node, parent_index, rows):
    text = node.get("text") if isinstance(node.get("text"), dict) else {}
    sprite_renderer = node.get("sprite") if isinstance(node.get("sprite"), dict) else {}
    sprite = sprite_renderer.get("sprite") if isinstance(sprite_renderer.get("sprite"), dict) else {}
    position = list(node.get("position", [0.0, 0.0]))
    while len(position) < 3:
        position.append(0.0)
    scale = list(node.get("scale", [1.0, 1.0]))
    while len(scale) < 3:
        scale.append(0.0 if text or sprite_renderer else 1.0)
    has_rect_transform = "anchored_position" in node
    entry = {
        "name": node.get("name", ""),
        "path": node.get("path", ""),
        "parentIndex": parent_index,
        "active": bool(node.get("active", True)),
        "uses_rect_transform": has_rect_transform,
        "local_position": position,
        "anchored_position": node.get("anchored_position", [0.0, 0.0]),
        # The pinned Unity RectTransform reports sizeDelta=(10,10); this field was omitted by
        # the HUD JSON exporter, so retain the serialized bundle value for its TMP bounds.
        "size_delta": node.get("size_delta", [10.0, 10.0] if has_rect_transform else [0.0, 0.0]),
        "pivot": node.get("pivot", [0.5, 0.5]),
        "scale": scale,
        "rotation": node.get("rotation", 0.0),
        "has_text": bool(text),
        "text_value": text.get("value", ""),
        "text_color": text.get("color", [1.0, 1.0, 1.0, 1.0]),
        "text_font_size": text.get("font_size", 0.0),
        "text_horizontal": text.get("horizontal", 0),
        "text_vertical": text.get("vertical", 0),
        "text_word_wrap": text.get("word_wrap", False),
        "text_overflow": text.get("overflow", 0),
        "has_sprite_renderer": bool(sprite),
        "image_sprite_file": sprite.get("file", ""),
        "image_sprite_width": sprite.get("width", 0),
        "image_sprite_height": sprite.get("height", 0),
        "image_color": sprite_renderer.get("color", [1.0, 1.0, 1.0, 1.0]),
        "image_order": sprite_renderer.get("order", 0),
        "animation_play_automatically": bool(node.get("animation_play_automatically", False)
                                             or node.get("default_animation")),
        "default_animation": node.get("default_animation", ""),
        "animations": [normalize_hud_animation(animation) for animation in node.get("animations", [])],
    }
    index = len(rows)
    rows.append(entry)
    for child in node.get("children", []):
        normalize_world_float_node(child, index, rows)


def build_custom_action_world_runtime(world_ui, source_sha256, language_values, xxhash):
    prefab = next((value for value in world_ui.get("prefabs", {}).values()
                   if value.get("root", {}).get("name") == "BattleCustomDamageFloatEffect_Scene"), None)
    if prefab is None:
        raise ValueError("Pinned official BattleCustomDamageFloatEffect_Scene prefab is missing.")
    source = prefab.get("source", {})
    if (source.get("bundle") != "defaultpackage_assets_prefabs_ui_fight_damagescene_battlecustomdamagefloateffect_scene.bundle"
            or source.get("bundle_hash") != "14e38fc9585d8e00567ae2f9f4b0e8bb"
            or str(source.get("path_id")) != "-2976502348647371213"):
        raise ValueError("Custom action float prefab does not match the pinned APK object.")
    root = prefab["root"]
    bindings = root.get("original_bindings", {})
    damage_ui = bindings.get("CharacterDamageUI", {})
    action_ui = bindings.get("DamageCustomActionUI", {})
    float_size = bindings.get("DamageFloatSize", {})
    icon_path = action_ui.get("mIcon")
    scene_offset = damage_ui.get("mSceneOffset", {})
    if not icon_path or damage_ui.get("mType") != 11 or float_size.get("mSize") != 5.0:
        raise ValueError("Custom action float component bindings differ from the pinned prefab.")

    nodes = []
    normalize_world_float_node(root, -1, nodes)
    actions = []
    for value, name in enumerate(("None", "Immunity", "Unflinching", "CDModify")):
        key = "EStatusCustomAction." + name
        text = language_values.get(xxhash.xxh64_intdigest(key.encode("utf-8")))
        if text:
            actions.append({"action": value, "key": key, "text": text})
    if not any(action["action"] == 3 and action["text"] == "减少冷却" for action in actions):
        raise ValueError("Pinned localization for EStatusCustomAction.CDModify is missing or changed.")

    return {
        "manifest": VERSION,
        "sourceSha256": source_sha256,
        "sourceBundle": source["bundle"],
        "sourceBundleHash": source["bundle_hash"],
        "sourcePathId": str(source["path_id"]),
        "rootName": root["name"],
        "iconPath": icon_path,
        "textPath": damage_ui["mTipUi"],
        "effectType": damage_ui["mType"],
        "showTime": damage_ui["mShowTime"],
        "height": damage_ui["mHeight"],
        "sceneOffset": [scene_offset.get("x", 0.0), scene_offset.get("y", 0.0), scene_offset.get("z", 0.0)],
        "cameraScale": float_size["mSize"],
        "sortingOrderStart": 1000,
        "nodes": nodes,
        "actions": actions,
    }


def localize_hud_node(node, text_for_key):
    translated = text_for_key(node.get("localization_key", ""))
    if translated is not None:
        node["text_value"] = translated
    for child in node.get("children", []):
        localize_hud_node(child, text_for_key)


def build_hud_damage_float_binding(prefab):
    root = prefab.get("root", {})
    bindings = root.get("original_bindings", {})
    source = bindings.get("CharacterDamageUI")
    if not isinstance(source, dict):
        return None
    scene_offset = source.get("mSceneOffset", {})
    return {
        "type": source.get("mType", -1),
        "showTime": source.get("mShowTime", 0.0),
        "numberPath": source.get("mNumberUi") or "",
        "tipPath": source.get("mTipUi") or "",
        "breakResiliencePath": source.get("mBreakResilienceGo") or "",
        "trueDamagePath": source.get("mTrueDamageText") or "",
        "height": source.get("mHeight", 0.0),
        "sceneOffset": [scene_offset.get("x", 0.0), scene_offset.get("y", 0.0),
                        scene_offset.get("z", 0.0)],
    }


def build_boss_hp_bar_binding(prefab):
    pending = [prefab.get("root", {})]
    source = None
    while pending:
        node = pending.pop()
        bindings = node.get("original_bindings", {})
        if isinstance(bindings, dict) and isinstance(bindings.get("FightBossHpAndShieldBar"), dict):
            source = bindings["FightBossHpAndShieldBar"]
            break
        pending.extend(reversed(node.get("children", [])))
    if not isinstance(source, dict):
        return None

    def sprite_file(value, field):
        if not isinstance(value, dict) or not value.get("file"):
            raise ValueError("Official boss HP bar sprite is missing: " + field)
        return PurePosixPath(value["file"]).name

    hp_sprites = source.get("HpSprites")
    shield_colors = source.get("ShieldColors")
    move_speed = float(source.get("MoveSpeed", 0.0))
    if not isinstance(hp_sprites, list) or not hp_sprites:
        raise ValueError("Official boss HP bar sprite variants are missing.")
    if not isinstance(shield_colors, list) or not shield_colors:
        raise ValueError("Official boss HP bar shield colors are missing.")
    if not 0.0 <= move_speed <= 1.0:
        raise ValueError("Official boss HP bar MoveSpeed is outside its configured range.")

    colors = []
    for color in shield_colors:
        if not isinstance(color, dict) or any(channel not in color for channel in ("r", "g", "b", "a")):
            raise ValueError("Official boss HP bar shield color is malformed.")
        colors.append({"rgba": [float(color[channel]) for channel in ("r", "g", "b", "a")]})

    return {
        "hpSpriteFiles": [sprite_file(sprite, "HpSprites") for sprite in hp_sprites],
        "shieldSpriteFile": sprite_file(source.get("ShieldSprite"), "ShieldSprite"),
        "shieldColors": colors,
        "emptyBarSpriteFile": sprite_file(source.get("EmptyBarSprite"), "EmptyBarSprite"),
        "moveSpeed": move_speed,
    }


def source_manifest(root, relative, expected_version=VERSION):
    path = root / PurePosixPath(relative)
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("manifest") != expected_version:
        raise ValueError("Battle assets are not pinned to " + expected_version + ": " + relative)
    return path, data


def build_unit_bars_runtime(source):
    entries = []
    for entity_id, entity in sorted(source["entities"].items(), key=lambda item: int(item[0])):
        parts = []
        for part in entity["parts"]:
            matrix = part["matrix"]
            if any(abs(matrix[row][column]) > 1e-7
                   for row in range(3) for column in range(3) if row != column):
                raise ValueError("Official unit bar transform contains rotation or shear: " + entity_id)
            sprite = part.get("sprite") or {}
            parts.append({
                "name": part.get("name", ""),
                "role": part.get("role", ""),
                "position": [matrix[0][3], matrix[1][3], matrix[2][3]],
                "scale": [matrix[0][0], matrix[1][1], matrix[2][2]],
                "size": part.get("size", [0.0, 0.0]),
                "color": [part["color"].get(channel, 1.0) for channel in ("r", "g", "b", "a")],
                "spriteFile": PurePosixPath(sprite.get("file", "")).name,
                "fillMode": part.get("fill_mode", -1),
                "fillOrigin": part.get("fill_origin", 0),
                "order": part.get("order", 0),
            })

        status = entity.get("status_layout")
        status_layout = None
        if status is not None:
            matrix = status["matrix"]
            status_layout = {
                "position": [matrix[0][3], matrix[1][3], matrix[2][3]],
                "scale": [matrix[0][0], matrix[1][1], matrix[2][2]],
                "columns": status["columns"],
                "rows": status["rows"],
                "cellSize": status["layout"]["grid"]["cell"],
            }
        entries.append({
            "entityId": int(entity_id),
            "duration": entity.get("duration", 0.5),
            "parts": parts,
            "statusLayout": status_layout,
        })

    factions = []
    for name in ("DefaultEffect", "CharacterMeEffect"):
        source_faction = source["factions"][name]
        factions.append({
            "name": name,
            "hpColor": [source_faction["HpColor"][channel] for channel in ("r", "g", "b", "a")],
            "hpAddColor": [source_faction["HpAddColor"][channel] for channel in ("r", "g", "b", "a")],
            "hpSubColor": [source_faction["HpSubColor"][channel] for channel in ("r", "g", "b", "a")],
        })
    return {"manifest": VERSION, "sourceSha256": "", "entities": entries, "factions": factions}


def restore_hud_runtime(unity_root=ROOT / "unity"):
    unity_root = Path(unity_root).resolve()
    hud_panels_path = target_path(unity_root, "Assets/Official/Combat/HUD/panels.json")
    _, pinned_hud_manifest = source_manifest(ROOT, HUD_MANIFEST)
    pinned_panels_hash = pinned_hud_manifest.get("output_sha256", {}).get("panels.json")
    source_panels_path = ROOT / "assets/official/battle-hud/panels.json"
    if (not pinned_panels_hash or sha256(source_panels_path) != pinned_panels_hash
            or sha256(hud_panels_path) != pinned_panels_hash):
        raise ValueError("Official battle HUD panel input does not match its pinned source manifest.")
    hud_panels = json.loads(hud_panels_path.read_text(encoding="utf-8"))
    import xxhash
    from refresh_current_localization import language
    language_values, language_sources = language(
        ROOT / ".tools/source-packages/history/854_156383/WwiseBnk")

    def localized_text(key):
        if not key:
            return None
        return language_values.get(xxhash.xxh64_intdigest(key.encode("utf-8")))

    runtime_panels = {"manifest": VERSION, "prefabs": []}
    for key, prefab in hud_panels["prefabs"].items():
        root = prefab["root"]
        localize_hud_node(root, localized_text)
        normalize_hud_node(root)
        nodes = []
        flatten_hud_node(root, -1, nodes)
        runtime_prefab = {"key": key, "nodes": nodes}
        damage_float = build_hud_damage_float_binding(prefab)
        if damage_float is not None:
            runtime_prefab["damageUi"] = damage_float
        boss_hp_bar = build_boss_hp_bar_binding(prefab)
        if boss_hp_bar is not None:
            runtime_prefab["bossHpBar"] = boss_hp_bar
        runtime_panels["prefabs"].append(runtime_prefab)
    runtime_panels_bytes = (json.dumps(runtime_panels, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    runtime_panels_hash = hashlib.sha256(runtime_panels_bytes).hexdigest()
    runtime_target = target_path(unity_root, HUD_RUNTIME)
    runtime_manifest_target = target_path(unity_root, HUD_RUNTIME_MANIFEST)
    previous_runtime_manifest = None
    previous_runtime_manifest_hash = None
    if runtime_manifest_target.exists():
        previous_runtime_manifest = json.loads(runtime_manifest_target.read_text(encoding="utf-8"))
        previous_runtime_manifest_hash = sha256(runtime_manifest_target)
        if (previous_runtime_manifest.get("manifest") != VERSION
                or previous_runtime_manifest.get("source_path") != "Assets/Official/Combat/HUD/panels.json"
                or previous_runtime_manifest.get("output_path") != HUD_RUNTIME):
            raise ValueError("Existing battle HUD runtime manifest does not match its pinned source.")
    if runtime_target.exists():
        current_runtime_hash = sha256(runtime_target)
        if current_runtime_hash != runtime_panels_hash:
            if (previous_runtime_manifest is None
                    or previous_runtime_manifest.get("output_sha256") != current_runtime_hash):
                raise ValueError("Refusing to overwrite changed Unity battle HUD runtime payload.")
            replace_verified_bytes(runtime_panels_bytes, runtime_target, current_runtime_hash)
    else:
        copy_bytes(runtime_panels_bytes, runtime_target, runtime_panels_hash)
    runtime_manifest = {
        "manifest": VERSION,
        "source_path": "Assets/Official/Combat/HUD/panels.json",
        "source_sha256": sha256(hud_panels_path),
        "output_path": HUD_RUNTIME,
        "output_sha256": runtime_panels_hash,
        "localization_sources": language_sources,
    }
    runtime_manifest_bytes = (json.dumps(runtime_manifest, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    runtime_manifest_hash = hashlib.sha256(runtime_manifest_bytes).hexdigest()
    if runtime_manifest_target.exists():
        current_manifest_hash = sha256(runtime_manifest_target)
        if current_manifest_hash != runtime_manifest_hash:
            if (previous_runtime_manifest is None
                    or current_manifest_hash != previous_runtime_manifest_hash):
                raise ValueError("Refusing to overwrite changed Unity battle HUD runtime manifest.")
            replace_verified_bytes(runtime_manifest_bytes, runtime_manifest_target, current_manifest_hash)
    else:
        copy_bytes(runtime_manifest_bytes, runtime_manifest_target, runtime_manifest_hash)

    world_ui_path = target_path(unity_root, "Assets/Official/Combat/HUD/world-ui.json")
    source_world_ui_path = ROOT / "assets/official/battle-hud/world-ui.json"
    pinned_world_ui_hash = pinned_hud_manifest.get("output_sha256", {}).get("world-ui.json")
    if (not pinned_world_ui_hash or sha256(source_world_ui_path) != pinned_world_ui_hash
            or sha256(world_ui_path) != pinned_world_ui_hash):
        raise ValueError("Official battle world UI input does not match its pinned source manifest.")
    world_ui = json.loads(world_ui_path.read_text(encoding="utf-8"))
    custom_action_runtime = build_custom_action_world_runtime(
        world_ui, pinned_world_ui_hash, language_values, xxhash)
    custom_action_runtime_bytes = (json.dumps(
        custom_action_runtime, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    custom_action_runtime_hash = hashlib.sha256(custom_action_runtime_bytes).hexdigest()
    custom_action_runtime_target = target_path(unity_root, CUSTOM_ACTION_RUNTIME)
    custom_action_manifest_target = target_path(unity_root, CUSTOM_ACTION_RUNTIME_MANIFEST)
    previous_custom_action_manifest = None
    previous_custom_action_manifest_hash = None
    if custom_action_manifest_target.exists():
        previous_custom_action_manifest = json.loads(
            custom_action_manifest_target.read_text(encoding="utf-8"))
        previous_custom_action_manifest_hash = sha256(custom_action_manifest_target)
        if (previous_custom_action_manifest.get("manifest") != VERSION
                or previous_custom_action_manifest.get("source_path") != "Assets/Official/Combat/HUD/world-ui.json"
                or previous_custom_action_manifest.get("output_path") != CUSTOM_ACTION_RUNTIME):
            raise ValueError("Existing custom action runtime manifest does not match its pinned source.")
    if custom_action_runtime_target.exists():
        current_runtime_hash = sha256(custom_action_runtime_target)
        if current_runtime_hash != custom_action_runtime_hash:
            if (previous_custom_action_manifest is None
                    or previous_custom_action_manifest.get("output_sha256") != current_runtime_hash):
                raise ValueError("Refusing to overwrite changed custom action runtime data.")
            replace_verified_bytes(custom_action_runtime_bytes, custom_action_runtime_target, current_runtime_hash)
    else:
        copy_bytes(custom_action_runtime_bytes, custom_action_runtime_target, custom_action_runtime_hash)
    custom_action_manifest = {
        "manifest": VERSION,
        "source_path": "Assets/Official/Combat/HUD/world-ui.json",
        "source_sha256": pinned_world_ui_hash,
        "output_path": CUSTOM_ACTION_RUNTIME,
        "output_sha256": custom_action_runtime_hash,
        "font_binding": CUSTOM_ACTION_FONT_BINDING,
        "localization_sources": language_sources,
    }
    custom_action_manifest_bytes = (json.dumps(
        custom_action_manifest, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    custom_action_manifest_hash = hashlib.sha256(custom_action_manifest_bytes).hexdigest()
    if custom_action_manifest_target.exists():
        current_manifest_hash = sha256(custom_action_manifest_target)
        if current_manifest_hash != custom_action_manifest_hash:
            if (previous_custom_action_manifest is None
                    or current_manifest_hash != previous_custom_action_manifest_hash):
                raise ValueError("Refusing to overwrite changed custom action runtime manifest.")
            replace_verified_bytes(
                custom_action_manifest_bytes, custom_action_manifest_target, current_manifest_hash)
    else:
        copy_bytes(custom_action_manifest_bytes, custom_action_manifest_target, custom_action_manifest_hash)

    _, pinned_hud_manifest = source_manifest(ROOT, HUD_MANIFEST)
    pinned_unit_bars_hash = pinned_hud_manifest.get("output_sha256", {}).get("unit-bars.json")
    source_unit_bars_path = ROOT / "assets/official/battle-hud/unit-bars.json"
    unit_bars_path = target_path(unity_root, UNIT_BARS_SOURCE)
    if (not pinned_unit_bars_hash or sha256(source_unit_bars_path) != pinned_unit_bars_hash
            or sha256(unit_bars_path) != pinned_unit_bars_hash):
        raise ValueError("Official unit bar input does not match its pinned source manifest.")
    unit_bars = json.loads(unit_bars_path.read_text(encoding="utf-8"))
    runtime_unit_bars = build_unit_bars_runtime(unit_bars)
    runtime_unit_bars["sourceSha256"] = pinned_unit_bars_hash
    runtime_unit_bars_bytes = (json.dumps(runtime_unit_bars, ensure_ascii=False,
        separators=(",", ":")) + "\n").encode("utf-8")
    runtime_unit_bars_hash = hashlib.sha256(runtime_unit_bars_bytes).hexdigest()
    unit_bars_target = target_path(unity_root, UNIT_BARS_RUNTIME)
    unit_bars_manifest_target = target_path(unity_root, UNIT_BARS_RUNTIME_MANIFEST)
    previous_unit_bars_manifest = None
    previous_unit_bars_manifest_hash = None
    if unit_bars_manifest_target.exists():
        previous_unit_bars_manifest = json.loads(unit_bars_manifest_target.read_text(encoding="utf-8"))
        previous_unit_bars_manifest_hash = sha256(unit_bars_manifest_target)
        if (previous_unit_bars_manifest.get("manifest") != VERSION
                or previous_unit_bars_manifest.get("source_path") != UNIT_BARS_SOURCE
                or previous_unit_bars_manifest.get("output_path") != UNIT_BARS_RUNTIME):
            raise ValueError("Existing unit bar runtime manifest does not match its pinned source.")
    if unit_bars_target.exists():
        current_unit_bars_hash = sha256(unit_bars_target)
        if current_unit_bars_hash != runtime_unit_bars_hash:
            if (previous_unit_bars_manifest is None
                    or previous_unit_bars_manifest.get("output_sha256") != current_unit_bars_hash):
                raise ValueError("Refusing to overwrite changed Unity unit bar runtime payload.")
            replace_verified_bytes(runtime_unit_bars_bytes, unit_bars_target, current_unit_bars_hash)
    else:
        copy_bytes(runtime_unit_bars_bytes, unit_bars_target, runtime_unit_bars_hash)

    unit_bars_runtime_manifest = {
        "manifest": VERSION,
        "source_path": UNIT_BARS_SOURCE,
        "source_sha256": pinned_unit_bars_hash,
        "output_path": UNIT_BARS_RUNTIME,
        "output_sha256": runtime_unit_bars_hash,
    }
    unit_bars_runtime_manifest_bytes = (json.dumps(unit_bars_runtime_manifest, sort_keys=True,
        separators=(",", ":")) + "\n").encode("utf-8")
    unit_bars_runtime_manifest_hash = hashlib.sha256(unit_bars_runtime_manifest_bytes).hexdigest()
    if unit_bars_manifest_target.exists():
        current_unit_bars_manifest_hash = sha256(unit_bars_manifest_target)
        if current_unit_bars_manifest_hash != unit_bars_runtime_manifest_hash:
            if (previous_unit_bars_manifest is None
                    or current_unit_bars_manifest_hash != previous_unit_bars_manifest_hash):
                raise ValueError("Refusing to overwrite changed unit bar runtime manifest.")
            replace_verified_bytes(unit_bars_runtime_manifest_bytes, unit_bars_manifest_target,
                                   current_unit_bars_manifest_hash)
    else:
        copy_bytes(unit_bars_runtime_manifest_bytes, unit_bars_manifest_target,
                   unit_bars_runtime_manifest_hash)

    return {"output_path": HUD_RUNTIME, "output_sha256": runtime_panels_hash,
            "prefabs": len(runtime_panels["prefabs"]), "unit_bars_path": UNIT_BARS_RUNTIME,
            "unit_bars_sha256": runtime_unit_bars_hash, "unit_entities": len(runtime_unit_bars["entities"])}


def restore(source_root=ROOT, unity_root=ROOT / "unity"):
    source_root = Path(source_root).resolve()
    unity_root = Path(unity_root).resolve()
    spine_manifest_path, spine_manifest = source_manifest(source_root, SPINE_MANIFEST)
    hud_manifest_path, hud_manifest = source_manifest(source_root, HUD_MANIFEST)
    entries = []

    for source_name, expected_hash in spine_manifest["outputs"].items():
        source_relative = PurePosixPath(source_name)
        if source_relative.suffix == ".geom":
            target_relative = "Assets/Official/Combat/Spine/" + source_relative.stem + ".skel.bytes"
        elif source_relative.name.endswith(".atlas.txt"):
            target_relative = "Assets/Official/Combat/Spine/" + source_relative.name
        elif source_relative.suffix == ".png" and "textures" in source_relative.parts:
            target_relative = "Assets/Official/Combat/Spine/textures/" + source_relative.name
        elif source_relative.name == "catalog.json":
            target_relative = "Assets/Official/Combat/Spine/catalog.json"
        else:
            raise ValueError("Unsupported verified Spine output: " + source_name)

        source = source_root.joinpath(*source_relative.parts)
        target = target_path(unity_root, target_relative)
        if sha256(source) != expected_hash:
            raise ValueError("Spine source output hash differs: " + source_name)
        copy_verified(source, target, expected_hash)
        entries.append({"path": target_relative, "source_path": source_name,
                        "output_sha256": expected_hash})

    for source_name, expected_hash in hud_manifest["output_sha256"].items():
        source_relative = "assets/official/battle-hud/" + source_name
        target_relative = "Assets/Official/Combat/HUD/" + source_name
        source = source_root / PurePosixPath(source_relative)
        if sha256(source) != expected_hash:
            raise ValueError("HUD JSON output hash differs: " + source_relative)
        copy_verified(source, target_path(unity_root, target_relative), expected_hash)
        entries.append({"path": target_relative, "source_path": source_relative,
                        "output_sha256": expected_hash})

    for resource_path, info in hud_manifest["sprites"].items():
        source_relative = PurePosixPath(resource_path.removeprefix("res://"))
        target_relative = "Assets/Official/Combat/HUD/Sprites/" + source_relative.name
        source = source_root.joinpath(*source_relative.parts)
        expected_hash = info["sha256"]
        if sha256(source) != expected_hash:
            raise ValueError("HUD sprite output hash differs: " + str(source_relative))
        copy_verified(source, target_path(unity_root, target_relative), expected_hash)
        entries.append({"path": target_relative, "source_path": source_relative.as_posix(),
                        "output_sha256": expected_hash})

    model_source = source_root / PurePosixPath(MODEL_CATALOG_SOURCE)
    model_hash = sha256(model_source)
    copy_verified(model_source, target_path(unity_root, MODEL_CATALOG_TARGET), model_hash)
    model_source_manifest = {
        "manifest": VERSION,
        "source_path": MODEL_CATALOG_SOURCE,
        "source_sha256": model_hash,
        "output_path": MODEL_CATALOG_TARGET,
        "output_sha256": model_hash,
    }
    model_manifest_bytes = (json.dumps(model_source_manifest, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    copy_bytes(model_manifest_bytes, target_path(unity_root, MODEL_CATALOG_MANIFEST),
               hashlib.sha256(model_manifest_bytes).hexdigest())

    monster_models = []
    for key in json.loads(model_source.read_text(encoding="utf-8")):
        skeleton = spine_manifest["skeletons"].get(key)
        if skeleton is None:
            continue
        bundle = skeleton.get("source", {}).get("bundle", "")
        match = re.search(r"_monster_(\d+)\.bundle$", bundle)
        if match:
            monster_models.append({"monster_id": int(match.group(1)), "key": key})
    monster_models.sort(key=lambda item: item["monster_id"])
    if len(monster_models) != 343 or len({item["monster_id"] for item in monster_models}) != len(monster_models):
        raise ValueError("Pinned battle Spine source does not yield the expected unique monster bindings.")
    identities = {
        "manifest": VERSION,
        "model_source_sha256": model_hash,
        "spine_source_manifest_sha256": sha256(spine_manifest_path),
        "monster_models": monster_models,
    }
    identity_bytes = (json.dumps(identities, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    identity_hash = hashlib.sha256(identity_bytes).hexdigest()
    copy_bytes(identity_bytes, target_path(unity_root, MODEL_IDENTITIES_TARGET), identity_hash)
    identity_manifest = {
        "manifest": VERSION,
        "source_path": "assets/reference/battle-spine/source-manifest.json",
        "source_sha256": identities["spine_source_manifest_sha256"],
        "output_path": MODEL_IDENTITIES_TARGET,
        "output_sha256": identity_hash,
    }
    identity_manifest_bytes = (json.dumps(identity_manifest, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    copy_bytes(identity_manifest_bytes, target_path(unity_root, MODEL_IDENTITIES_MANIFEST),
               hashlib.sha256(identity_manifest_bytes).hexdigest())

    module_record = {
        "manifest": VERSION,
        "sources": {
            SPINE_MANIFEST: sha256(spine_manifest_path),
            HUD_MANIFEST: sha256(hud_manifest_path),
        },
        "assets": sorted(entries, key=lambda item: item["path"]),
    }
    restore_hud_runtime(unity_root)
    content = (json.dumps(module_record, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    manifest_target = target_path(unity_root, MODULE_MANIFEST)
    manifest_hash = hashlib.sha256(content).hexdigest()
    copy_bytes(content, manifest_target, manifest_hash)
    return {"assets": len(entries), "manifest": MODULE_MANIFEST,
            "manifest_sha256": manifest_hash}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=ROOT)
    parser.add_argument("--unity-root", type=Path, default=ROOT / "unity")
    parser.add_argument("--runtime-hud-only", action="store_true",
                        help="Rebuild the verified HUD runtime payload without restoring unrelated battle assets.")
    args = parser.parse_args()
    result = restore_hud_runtime(args.unity_root) if args.runtime_hud_only else restore(args.source_root, args.unity_root)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
