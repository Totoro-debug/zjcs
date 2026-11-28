"""Shared helpers for existing asset tools; no conversion command entry point."""

from pathlib import PurePosixPath

VERSION = "854_156383"

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
