"""Convert extracted official UI trees into the Unity commerce transport model."""
from __future__ import annotations

import json
import math
from pathlib import Path


def commerce_node(node, sprite_bindings):
    rotation = node.get("local_rotation") or [0, 0, 0, 1]
    value = {
        "name": node["name"], "path": node["path"], "active": bool(node.get("active", True)),
        "anchor_min": node.get("anchor_min") or [0.5, 0.5],
        "anchor_max": node.get("anchor_max") or [0.5, 0.5],
        "anchored_position": node.get("anchored_position") or [0, 0],
        "size_delta": node.get("size_delta") or [0, 0],
        "pivot": node.get("pivot") or [0.5, 0.5],
        "scale": node.get("scale") or [1, 1],
        "rotation": 2 * math.degrees(math.atan2(float(rotation[2]), float(rotation[3]))),
        "alpha": float(node.get("alpha", 1)), "scripts": node.get("scripts") or [],
        "children": [commerce_node(child, sprite_bindings) for child in node.get("children") or []],
        "button": bool(node.get("button")), "mask": bool(node.get("mask")),
        "rectmask2d": bool(node.get("rectmask2d")),
        "has_image": bool(node.get("has_image")), "has_text": bool(node.get("has_text")),
        "has_mesh": bool(node.get("has_mesh")), "ui_particle": bool(node.get("ui_particle")),
        "is_rect_transform": bool(node.get("is_rect_transform", True)),
    }
    if value["has_image"]:
        sprite = sprite_bindings.get(node.get("sprite"))
        if sprite is None:
            value["has_image"] = False
        else:
            value["image"] = {
                "sprite": sprite, "color": node.get("image_color") or [1, 1, 1, 1],
                "type": int(node.get("image_type", 0)),
                "preserve_aspect": bool(node.get("image_preserve_aspect")),
            }
    if value["has_text"]:
        alignment = int(node.get("text_alignment", 514))
        horizontal = alignment & 0xFF
        value["text"] = {
            "value": node.get("text_value", ""),
            "color": node.get("text_color") or [1, 1, 1, 1],
            "margin": node.get("text_margin") or [0, 0, 0, 0],
            "auto_size": node.get("text_font_size_range") or [0, 0],
            "font_size": float(node.get("text_font_size", 32)),
            "horizontal": {1: 0, 2: 1, 4: 2}.get(horizontal, 1),
            "vertical": alignment & 0xFF00,
            "overflow": int(node.get("text_overflow", 0)),
            "word_wrap": bool(node.get("text_word_wrap", True)),
        }
    for key in (
        "has_layout", "layout", "has_grid", "grid", "has_scroll", "scroll",
        "has_size_fitter", "size_fitter", "has_layout_element", "layout_element",
        "has_aspect_fitter", "aspect_fitter", "mesh_color", "mesh_raycast_target",
        "mesh_maskable",
    ):
        if key in node:
            value[key] = node[key]
    for source, target in (
        ("mesh", "mesh_json"), ("mesh_material", "mesh_material_json"),
        ("particle_system", "particle_system_json"),
        ("particle_renderer", "particle_renderer_json"),
        ("particle_material", "particle_material_json"),
    ):
        if source in node:
            value[target] = json.dumps(node[source], ensure_ascii=False, separators=(",", ":"))
            value[source] = node[source]
    if "animations" in node:
        value["animations_json"] = json.dumps(node["animations"], ensure_ascii=False, separators=(",", ":"))
    if "local_position" in node:
        value["local_position"] = node["local_position"]
    if "local_rotation" in node:
        value["local_rotation"] = node["local_rotation"]
    return value


def flatten_ui_node(root):
    nodes = []

    def visit(node, parent_index):
        value = {key: child for key, child in node.items() if key != "children"}
        for flag, component in (
            ("has_image", "image"), ("has_text", "text"), ("has_scroll", "scroll"),
            ("has_layout", "layout"), ("has_grid", "grid"),
            ("has_size_fitter", "size_fitter"), ("has_layout_element", "layout_element"),
            ("has_aspect_fitter", "aspect_fitter"), ("has_mesh", "mesh"),
        ):
            value[flag] = bool(node.get(flag, node.get(component) is not None))
        value["is_rect_transform"] = bool(node.get("is_rect_transform", True))
        for source, target in (
            ("mesh", "mesh_json"), ("mesh_material", "mesh_material_json"),
            ("particle_system", "particle_system_json"),
            ("particle_renderer", "particle_renderer_json"),
            ("particle_material", "particle_material_json"),
            ("animations", "animations_json"),
        ):
            if source in node:
                value[target] = json.dumps(node[source], ensure_ascii=False, separators=(",", ":"))
                value.pop(source, None)
        value["parentIndex"] = parent_index
        index = len(nodes)
        nodes.append(value)
        for child in node.get("children") or []:
            visit(child, index)

    visit(root, -1)
    return nodes
