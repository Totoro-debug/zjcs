"""Convert the pinned Friendship Wish prefab and its serialized UI components."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_character_creation_opening import SpriteCache, convert_prefab, prefab_walk
from commerce_ui_conversion import commerce_node
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

VERSION = "854_156383"
BUNDLE = "defaultpackage_assets_prefabs_ui_gachapanel.bundle"
PREFAB_PATH = "Assets/Prefabs/UI/GachaPanel/FriendGachaPanel.prefab"
SOURCE_ROOT = ROOT / "assets/official/wish-friend-panel"
OUTPUT = SOURCE_ROOT / "friend-panel.json"
REFERENCE = ROOT / "assets/reference/wishes/friend-panel-source-manifest.json"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def make_schemas() -> ScriptSchemas:
    editor = Path(os.environ.get("ZJCS_UNITY_EDITOR", "D:/Tool/Unity/Editor/Unity.exe"))
    if editor.suffix.lower() == ".exe":
        editor = editor.parent
    data = editor / "Data"
    managed = data / "Managed/UnityEngine"
    ui_package = ROOT / "unity/Library/ScriptAssemblies/UnityEngine.UI.dll"
    required = [
        managed / "UnityEngine.CoreModule.dll",
        managed / "UnityEngine.TextRenderingModule.dll",
        managed / "UnityEngine.UIModule.dll",
        data / "il2cpp/build/deploy/netstandard.dll",
        ui_package,
    ]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError("Unity 2022.3.57 UI schema assemblies are missing: " + ", ".join(missing))

    folder = ROOT / ".tools/unity-schema"
    schema = ScriptSchemas.__new__(ScriptSchemas)
    generator = TypeTreeGenerator("2022.3.60f1", generator="AssetsTools")
    generator.load_local_dll_folder(str(ROOT / ".tools/source-packages/official-code/managed/decoded"))
    for path in (
        data / "il2cpp/build/deploy/netstandard.dll",
        managed / "UnityEngine.CoreModule.dll",
        managed / "UnityEngine.TextRenderingModule.dll",
        managed / "UnityEngine.UIModule.dll",
        ui_package,
    ):
        generator.load_dll(path.read_bytes())
    for module in (
        "UnityEngine.TilemapModule", "UnityEngine", "UnityEngine.DirectorModule",
        "UnityEngine.GridModule", "SimpleAnimationComponent", "UnityEngine.AnimationModule",
        "UnityEngine.ParticleSystemModule", "spine-unity", "Unity.RenderPipelines.Universal.Runtime",
        "Unity.TextMeshPro", "AK.Wwise.Unity.API.WwiseTypes", "Unity.Timeline", "Cinemachine",
        "spine-timeline", "AK.Wwise.Unity.Timeline",
    ):
        generator.load_dll((folder / (module + ".dll")).read_bytes())

    windir = Path(os.environ.get("WINDIR", "C:/Windows"))
    generator.load_dll((windir / "Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll").read_bytes())
    generator.load_dll((windir / "Microsoft.NET/Framework64/v4.0.30319/System.Core.dll").read_bytes())
    dotnet = Path(subprocess.check_output(["where.exe", "dotnet"], text=True).splitlines()[0]).parent
    runtime = sorted((dotnet / "shared/Microsoft.NETCore.App").iterdir(),
                     key=lambda path: tuple(map(int, path.name.split("."))))[-1]
    for name in ("System.Runtime", "System.Private.CoreLib", "System.Collections"):
        generator.load_dll((runtime / (name + ".dll")).read_bytes())
    schema.generator = generator
    schema.cache = {}
    schema.records = {}
    return schema


def walk_nodes(root):
    yield root
    for child in root.get("children") or []:
        yield from walk_nodes(child)


def rect_offset(value):
    return [int(value.get(key, 0)) for key in ("m_Left", "m_Right", "m_Top", "m_Bottom")]


def component_script(component):
    try:
        return component.read(check_read=False).m_Script.read().m_ClassName
    except Exception:
        return ""


def enrich_component_data(apk, schemas, game_objects, nodes):
    if len(game_objects) != len(nodes):
        raise ValueError("Friendship prefab transform order differs from the converted hierarchy.")
    component_counts = {}
    for game_object, node in zip(game_objects, nodes):
        scripts = []
        for component in game_object.m_Component:
            if component.component.type.name != "MonoBehaviour":
                continue
            reader = component.component.deref()
            class_name = component_script(reader)
            if not class_name:
                continue
            scripts.append(class_name)
            if class_name in {
                "Image", "TextMeshProUGUI", "Button", "HorizontalLayoutGroup", "VerticalLayoutGroup",
                "GridLayoutGroup", "ScrollRect", "ContentSizeFitter", "LayoutElement",
                "AspectRatioFitter", "RectMask2D", "Mask", "CanvasGroup", "UIMesh", "UIParticle",
            }:
                component_counts[class_name] = component_counts.get(class_name, 0) + 1
            if class_name in ("HorizontalLayoutGroup", "VerticalLayoutGroup"):
                value = schemas.read(reader)
                node["has_layout"] = True
                node["layout"] = {
                    "axis": 0 if class_name == "HorizontalLayoutGroup" else 1,
                    "padding": rect_offset(value.get("m_Padding") or {}),
                    "alignment": int(value.get("m_ChildAlignment", 0)),
                    "spacing": float(value.get("m_Spacing", 0)),
                    "expand": [bool(value.get("m_ChildForceExpandWidth")),
                               bool(value.get("m_ChildForceExpandHeight"))],
                    "control": [bool(value.get("m_ChildControlWidth")),
                                bool(value.get("m_ChildControlHeight"))],
                    "child_scale": [bool(value.get("m_ChildScaleWidth")),
                                    bool(value.get("m_ChildScaleHeight"))],
                    "reverse": bool(value.get("m_ReverseArrangement")),
                }
            elif class_name == "GridLayoutGroup":
                value = schemas.read(reader)
                cell, spacing = value.get("m_CellSize") or {}, value.get("m_Spacing") or {}
                node["has_grid"] = True
                node["grid"] = {
                    "padding": rect_offset(value.get("m_Padding") or {}),
                    "alignment": int(value.get("m_ChildAlignment", 0)),
                    "corner": int(value.get("m_StartCorner", 0)),
                    "axis": int(value.get("m_StartAxis", 0)),
                    "constraint": int(value.get("m_Constraint", 0)),
                    "count": int(value.get("m_ConstraintCount", 1)),
                    "cell": [float(cell.get("x", 0)), float(cell.get("y", 0))],
                    "spacing": [float(spacing.get("x", 0)), float(spacing.get("y", 0))],
                }
            elif class_name == "ScrollRect":
                value = schemas.read(reader)
                node["has_scroll"] = True
                node["scroll"] = {
                    "horizontal": bool(value.get("m_Horizontal")),
                    "vertical": bool(value.get("m_Vertical")),
                    "inertia": bool(value.get("m_Inertia")),
                    "movement": int(value.get("m_MovementType", 0)),
                    "sensitivity": float(value.get("m_ScrollSensitivity", 1)),
                    "elasticity": float(value.get("m_Elasticity", 0.1)),
                    "deceleration": float(value.get("m_DecelerationRate", 0.135)),
                }
            elif class_name == "ContentSizeFitter":
                value = schemas.read(reader)
                node["has_size_fitter"] = True
                node["size_fitter"] = [int(value.get("m_HorizontalFit", 0)), int(value.get("m_VerticalFit", 0))]
            elif class_name == "LayoutElement":
                value = schemas.read(reader)
                node["has_layout_element"] = True
                node["layout_element"] = {
                    "ignore": bool(value.get("m_IgnoreLayout")),
                    "min": [float(value.get("m_MinWidth", -1)), float(value.get("m_MinHeight", -1))],
                    "preferred": [float(value.get("m_PreferredWidth", -1)),
                                  float(value.get("m_PreferredHeight", -1))],
                    "flexible": [float(value.get("m_FlexibleWidth", -1)),
                                 float(value.get("m_FlexibleHeight", -1))],
                }
            elif class_name == "AspectRatioFitter":
                value = schemas.read(reader)
                node["has_aspect_fitter"] = True
                node["aspect_fitter"] = {
                    "mode": int(value.get("m_AspectMode", 0)),
                    "ratio": float(value.get("m_AspectRatio", 1)),
                }
            elif class_name == "RectMask2D":
                node["rectmask2d"] = True
                node["mask"] = False
            elif class_name == "Mask":
                node["mask"] = True
            elif class_name == "CanvasGroup":
                node["alpha"] = float(schemas.read(reader).get("m_Alpha", 1))
        node["scripts"] = scripts
    return component_counts


def write_sprite_files(sprite_cache):
    output_dir = SOURCE_ROOT / "sprites"
    output_dir.mkdir(parents=True, exist_ok=True)
    bindings = {}
    outputs = {}
    for image in sprite_cache.images.values():
        if image is None:
            continue
        key = image["key"]
        safe_name = re.sub(r"[^A-Za-z0-9._-]+", "_", image["name"]).strip("._") or "sprite"
        filename = safe_name + "-" + hashlib.sha256(key.encode("utf-8")).hexdigest()[:12] + ".png"
        path = output_dir / filename
        image["image"].save(path, format="PNG", optimize=False)
        relative = "res://assets/official/wish-friend-panel/sprites/" + filename
        bindings[key] = {
            "file": relative,
            "name": image["name"],
            "width": image["rect"][0],
            "height": image["rect"][1],
            "border": image["border"],
            "ppu": image["ppu"],
            "pivot": [image["pivot"][0] * image["rect"][0], image["pivot"][1] * image["rect"][1]],
        }
        outputs[str(path.relative_to(ROOT)).replace("\\", "/")] = digest(path)
    return bindings, outputs


def main():
    apk = Apk()
    schemas = make_schemas()
    sprite_cache = SpriteCache(apk)
    pointer = next((value for path, value in apk.container(BUNDLE) if path == PREFAB_PATH), None)
    if pointer is None:
        raise FileNotFoundError("Pinned Friendship Wish prefab is missing: " + PREFAB_PATH)
    prefab_object = pointer.deref()
    root_game_object = pointer.read()
    converted = convert_prefab(apk, sprite_cache, schemas, PREFAB_PATH, BUNDLE, SOURCE_ROOT)
    game_objects = [game_object for game_object, _ in prefab_walk(root_game_object)]
    source_nodes = list(walk_nodes(converted))
    counts = enrich_component_data(apk, schemas, game_objects, source_nodes)
    sprite_bindings, outputs = write_sprite_files(sprite_cache)
    root = commerce_node(converted, sprite_bindings)
    raw = {
        "manifest": VERSION,
        "source": {
            **apk.provenance(prefab_object),
            "prefab": PREFAB_PATH,
            "raw_sha256": hashlib.sha256(prefab_object.get_raw_data()).hexdigest(),
        },
        "componentCounts": counts,
        "root": root,
    }
    SOURCE_ROOT.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(raw, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    outputs[str(OUTPUT.relative_to(ROOT)).replace("\\", "/")] = digest(OUTPUT)
    reference = {
        "manifest": VERSION,
        "source": raw["source"],
        "sourceObjects": sorted(sprite_cache.sources.values(),
                                 key=lambda value: (value["bundle"], value["path_id"])),
        "componentCounts": counts,
        "outputs": outputs,
        "spriteCount": len(sprite_bindings),
    }
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_text(json.dumps(reference, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Pinned Friendship Wish prefab converted:", len(game_objects), "nodes,",
          len(sprite_bindings), "sprites,", len(counts), "component kinds.")


if __name__ == "__main__":
    main()
