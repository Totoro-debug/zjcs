"""Package the fixed-version official Map10 world and story UI for Unity."""
import hashlib
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = "854_156383"
PANEL_SOURCE = ROOT / "assets/official/map-ui.json"
PANEL_MANIFEST = ROOT / "assets/reference/official-map/ui-source-manifest.json"
SPRITE_SOURCE = ROOT / "assets/official/sprites"
OUTPUT_ROOT = ROOT / "unity/Assets/Official/World/Map/10/UI"
SPRITE_ROOT = OUTPUT_ROOT / "Sprites"
OUTPUT_DATA = OUTPUT_ROOT / "map10-ui.json"
OUTPUT_MANIFEST = OUTPUT_ROOT / "source-manifest.json"
REFERENCE_MANIFEST = ROOT / "assets/reference/official-map/map10-ui-source-manifest.json"

PANEL_KEYS = (
    "WorldCanvas",
    "WorldPanel",
    "MiniMapFullScreenPanel",
    "MapEntityListMenu",
    "MapEntityListMenuItem",
    "MapNotifyUI",
    "MapGalaxyPanel",
    "TalkDialog",
    "OptionTalkDialog",
    "OptionTalkDialogItem",
    "BubbleDialog",
    "WorldStageDialog",
    "WorldCommonDialog",
    "WorldTouchDialog",
)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def as_battle_node(source):
    image = source.get("image") or {}
    sprite = image.get("sprite") or {}
    text = source.get("text") or {}
    scroll = source.get("scroll") or {}
    layout = source.get("layout") or {}
    layout_element = source.get("layout_element") or {}
    fitter = source.get("size_fitter") or [0, 0]
    return {
        "name": source["name"],
        "path": source["path"],
        "scripts": source.get("scripts", []),
        "localization_key": text.get("localization_key", ""),
        "active": source.get("active", True),
        "button": source.get("button", False),
        "mask": source.get("mask", False),
        "rectmask2d": source.get("rectmask2d", False),
        "has_image": bool(image),
        "has_text": bool(text),
        "has_scroll": bool(scroll),
        "text_value": text.get("value", ""),
        "text_color": text.get("color"),
        "text_margin": text.get("margin"),
        "text_auto_size": text.get("auto_size"),
        "text_font_size": text.get("font_size", 0),
        "text_horizontal": text.get("horizontal", 0),
        "text_vertical": text.get("vertical", 0),
        "text_overflow": text.get("overflow", 0),
        "text_word_wrap": text.get("word_wrap", False),
        "image_sprite_file": sprite.get("file", ""),
        "image_sprite_width": sprite.get("width", 0),
        "image_sprite_height": sprite.get("height", 0),
        "image_type": image.get("type", 0),
        "image_sprite_border": sprite.get("border"),
        "image_sprite_pivot": sprite.get("pivot"),
        "image_color": image.get("color"),
        "image_graphic_flags": image.get("graphic_flags", False),
        "image_raycast_target": image.get("raycast_target", False),
        "image_maskable": image.get("maskable", True),
        "image_sprite_ppu": sprite.get("ppu", 100),
        "has_image_sprite": bool(sprite),
        "image_preserve_aspect": image.get("preserve_aspect", False),
        "scroll_horizontal": scroll.get("horizontal", False),
        "scroll_vertical": scroll.get("vertical", False),
        "scroll_inertia": scroll.get("inertia", False),
        "scroll_movement": scroll.get("movement", 0),
        "scroll_sensitivity": scroll.get("sensitivity", 0),
        "scroll_elasticity": scroll.get("elasticity", 0),
        "scroll_deceleration": scroll.get("deceleration", 0),
        "has_layout": bool(layout),
        "has_size_fitter": bool(source.get("size_fitter")),
        "has_layout_element": bool(layout_element),
        "layout_reverse": layout.get("reverse", False),
        "layout_element_ignore": layout_element.get("ignore", False),
        "layout_padding": layout.get("padding"),
        "layout_spacing_list": layout.get("spacing_list"),
        "layout_element_min": layout_element.get("min"),
        "layout_element_preferred": layout_element.get("preferred"),
        "layout_element_flexible": layout_element.get("flexible"),
        "layout_spacing": layout.get("spacing", 0),
        "layout_axis": layout.get("axis", 0),
        "layout_alignment": layout.get("alignment", 0),
        "fitter_horizontal": fitter[0],
        "fitter_vertical": fitter[1],
        "layout_expand": layout.get("expand"),
        "layout_control": layout.get("control"),
        "layout_child_scale": layout.get("child_scale"),
        "anchor_min": source.get("anchor_min"),
        "anchor_max": source.get("anchor_max"),
        "anchored_position": source.get("anchored_position"),
        "size_delta": source.get("size_delta"),
        "pivot": source.get("pivot"),
        "scale": source.get("scale"),
        "rotation": source.get("rotation", 0),
        "alpha": source.get("alpha", 1),
        "children": [as_battle_node(child) for child in source.get("children", [])],
    }


def flatten_nodes(source):
    rows = []

    def visit(node, parent_index):
        children = node.pop("children")
        index = len(rows)
        node["parentIndex"] = parent_index
        rows.append(node)
        for child in children:
            visit(child, index)

    visit(as_battle_node(source), -1)
    return rows


def build():
    if not PANEL_SOURCE.is_file() or not PANEL_MANIFEST.is_file():
        raise FileNotFoundError("The fixed-version official map UI data or source manifest is missing.")
    panels = json.loads(PANEL_SOURCE.read_text(encoding="utf-8"))
    source_manifest = json.loads(PANEL_MANIFEST.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != MANIFEST:
        raise ValueError("Official map UI source manifest is not pinned to " + MANIFEST)
    source_prefabs = source_manifest.get("prefabs", {})
    source_resources = source_manifest.get("resources", {})

    selected = []
    selected_sources = {}
    sprite_uris = set()
    for key in PANEL_KEYS:
        root = panels.get(key)
        provenance = source_prefabs.get(key)
        if root is None or provenance is None:
            raise ValueError("Official Map10 UI panel or source record is missing: " + key)
        selected.append({
            "key": key,
            "nodes": flatten_nodes(root),
            "storyAnimations": [
                {"name": name, "steps": steps}
                for name, steps in sorted(root.get("story_animations", {}).items())
            ],
        })
        selected_sources[key] = provenance
        pending = [root]
        while pending:
            node = pending.pop()
            image = node.get("image") or {}
            sprite = image.get("sprite") or {}
            if sprite.get("file"):
                sprite_uris.add(sprite["file"])
            pending.extend(node.get("children", []))

    sprite_records = []
    SPRITE_ROOT.mkdir(parents=True, exist_ok=True)
    for uri in sorted(sprite_uris):
        prefix = "res://assets/official/sprites/"
        if not uri.startswith(prefix):
            raise ValueError("Map10 UI sprite is outside the official sprite payload: " + uri)
        source = SPRITE_SOURCE / uri[len(prefix):]
        provenance = source_resources.get(uri)
        if provenance is None or not source.is_file():
            raise FileNotFoundError("Official Map10 UI sprite or source record is missing: " + uri)
        actual_hash = sha256(source.read_bytes())
        if actual_hash.lower() != provenance.get("sha256", "").lower():
            raise ValueError("Official Map10 UI sprite SHA-256 differs from its manifest: " + uri)

        target = SPRITE_ROOT / source.name
        if target.is_file():
            if sha256(target.read_bytes()).lower() != actual_hash.lower():
                raise ValueError("Existing Map10 UI sprite differs from pinned source: " + str(target))
        else:
            shutil.copyfile(source, target)

        sprite_metadata = None
        for key in PANEL_KEYS:
            stack = [panels[key]]
            while stack:
                node = stack.pop()
                sprite = ((node.get("image") or {}).get("sprite") or {})
                if sprite.get("file") == uri:
                    sprite_metadata = sprite
                    break
                stack.extend(node.get("children", []))
            if sprite_metadata is not None:
                break
        if sprite_metadata is None:
            raise ValueError("Map10 UI sprite metadata is missing: " + uri)
        sprite_records.append({
            "uri": uri,
            "file": source.name,
            "unityAssetPath": "Assets/Official/World/Map/10/UI/Sprites/" + source.name,
            "sha256": actual_hash,
            "width": sprite_metadata.get("width", 0),
            "height": sprite_metadata.get("height", 0),
            "border": sprite_metadata.get("border", [0, 0, 0, 0]),
            "pivot": sprite_metadata.get("pivot", [0, 0]),
            "pixelsPerUnit": sprite_metadata.get("ppu", 100),
        })

    panel_source_hash = sha256(PANEL_SOURCE.read_bytes())
    panel_manifest_hash = sha256(PANEL_MANIFEST.read_bytes())
    payload = {
        "manifest": MANIFEST,
        "mapClassId": 10,
        "sourceUiSha256": panel_source_hash,
        "sourceManifestSha256": panel_manifest_hash,
        "panels": selected,
        "sprites": sprite_records,
    }
    data_bytes = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    OUTPUT_DATA.write_bytes(data_bytes)
    manifest = {
        "manifest": MANIFEST,
        "mapClassId": 10,
        "source": {
            "uiDataPath": "assets/official/map-ui.json",
            "uiDataSha256": panel_source_hash,
            "uiSourceManifestPath": "assets/reference/official-map/ui-source-manifest.json",
            "uiSourceManifestSha256": panel_manifest_hash,
            "panels": selected_sources,
            "sprites": [
                {"uri": uri, "source": source_resources[uri]}
                for uri in sorted(sprite_uris)
            ],
        },
        "output": {
            "path": "map10-ui.json",
            "sha256": sha256(data_bytes),
            "panelCount": len(selected),
            "spriteCount": len(sprite_records),
        },
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    OUTPUT_MANIFEST.write_bytes(manifest_bytes)
    REFERENCE_MANIFEST.write_bytes(manifest_bytes)
    print("Map10 official UI panels: " + str(len(selected)))
    print("Map10 official UI sprites: " + str(len(sprite_records)))
    print("Map10 UI JSON SHA-256: " + sha256(data_bytes))
    print("Map10 UI source manifest: " + str(REFERENCE_MANIFEST.relative_to(ROOT)))


if __name__ == "__main__":
    build()
