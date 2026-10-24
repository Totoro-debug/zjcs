"""Convert the pinned Cooking recipes and UGUI prefabs for offline runtime use."""
from __future__ import annotations

import hashlib
import io
import json
import math
import re
import struct
import sys
import uuid
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr


MANIFEST_ID = "854_156383"
APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
BUNDLE = "defaultpackage_assets_prefabs_ui_cooking.bundle"
BUNDLE_MD5 = "5b2b455726b8c4f6c699ce328f9bdfc1"
BUNDLE_SHA256 = "3a4b0c6790370ad29f026b954172b32f6cbe3ba894b555a5b434e04c8faf7802"
BUNDLE_SIZE = 158361
RULES_MANIFEST = ROOT / "assets/reference/official-city/rules-manifest.json"
SOURCE_RECEIPTS = ROOT / ".tools/source-packages/extracted/bundle-sources.json"
SOURCE_CLOUD_ROOT = ROOT / ".tools/source-packages/cloud/854_156383"
SOURCE_BUNDLE_RECEIPTS = SOURCE_CLOUD_ROOT / "receipts.jsonl"
BUNDLE_FILE = ROOT / ".tools/source-packages/extracted" / BUNDLE
CLOUD_BUNDLE_FILE = ROOT / ".tools/source-packages/cloud/854_156383/DefaultPackage" / (BUNDLE_MD5 + ".bundle")
PAYLOAD = ROOT / "unity/Assets/StreamingAssets/Cooking"
SPRITES = ROOT / "unity/Assets/Game/Resources/Cooking/Sprites"
REFERENCE = ROOT / "assets/reference/cooking-module"

SCHEMA_CLASSES = {
    "Button", "CanvasGroup", "ContentSizeFitter", "GridLayoutGroup", "HorizontalLayoutGroup",
    "Image", "LayoutElement", "Mask", "RectMask2D", "ScrollRect", "LoopVerticalScrollRect", "TextMeshProUGUI",
    "VerticalLayoutGroup", "CookingPanel", "CookingPanelItem",
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_sha(path: Path) -> str:
    return sha256(path.read_bytes())


def normalized(value):
    if isinstance(value, dict):
        return {str(key): normalized(entry) for key, entry in value.items()}
    if isinstance(value, list):
        return [normalized(entry) for entry in value]
    return value


def vec(value, dimensions: int) -> list[float]:
    names = ("x", "y", "z", "w")
    if value is None:
        return [0.0] * dimensions
    return [float(getattr(value, names[index])) for index in range(dimensions)]


def rotation_z_degrees(value) -> float:
    if value is None:
        return 0.0
    return math.degrees(2.0 * math.atan2(float(value.z), float(value.w)))


def safe_name(value: str) -> str:
    name = re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._")
    return name or "sprite"


def pointer(value: dict, owner):
    return PPtr(**value, assetsfile=owner.assets_file).deref()


class CookingConverter:
    def __init__(self):
        self.apk = Apk()
        self.schemas = ScriptSchemas()
        self.constructors = Constructors()
        self.bundle = self.apk.load(BUNDLE)
        self.sprite_outputs = {}
        self.receipts = json.loads(SOURCE_RECEIPTS.read_text(encoding="utf-8"))
        cloud_receipts = [json.loads(line) for line in SOURCE_BUNDLE_RECEIPTS.read_text(encoding="utf-8").splitlines()
                          if line.strip()]
        self.cloud_receipts_by_bundle = {}
        for value in cloud_receipts:
            self.cloud_receipts_by_bundle.setdefault(value.get("name"), []).append(value)
        matches = [value for value in self.cloud_receipts_by_bundle.get(BUNDLE, [])
                   if value.get("file") == "DefaultPackage/" + BUNDLE_MD5 + ".bundle"]
        if len(matches) != 1:
            raise ValueError("Expected one fixed-version Cooking prefab CDN receipt")
        self.bundle_source_receipt = matches[0]
        self.package_source_cache = {}
        self.records = {}

    def raw_table(self, name: str, table_type: str):
        obj = self.apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        return raw, self.constructors.table(raw, table_type)

    def game_object_components(self, game_object):
        components = []
        transform = None
        for entry in game_object.m_Component:
            component = entry.component.deref()
            if component is None:
                continue
            if component.type.name in ("RectTransform", "Transform"):
                transform = component.read(check_read=False)
                continue
            class_name = component.type.name
            fields = None
            if component.type.name == "MonoBehaviour":
                basic = component.read(check_read=False)
                try:
                    script = basic.m_Script.deref().read(check_read=False)
                    class_name = script.m_ClassName
                    if class_name in SCHEMA_CLASSES:
                        try:
                            fields = self.text_mesh_pro_fields(component) if class_name == "TextMeshProUGUI" \
                                else self.schemas.read(component)
                        except (ValueError, KeyError, TypeError, IndexError):
                            fields = None
                except (AttributeError, TypeError):
                    class_name = "MonoBehaviour"
            components.append((class_name, component, fields))
        return transform, components

    @staticmethod
    def text_mesh_pro_fields(component):
        raw = component.get_raw_data()
        if len(raw) < 92:
            raise ValueError("Original TMP component data is truncated")
        text_length = struct.unpack_from("<i", raw, 88)[0]
        if not 0 <= text_length <= len(raw) - 92:
            raise ValueError("Original TMP string extent is invalid")
        text = raw[92:92 + text_length].decode("utf-8", "replace")
        aligned = (92 + text_length + 3) & ~3
        shared = struct.unpack_from("<i", raw, aligned + 28)[0]
        if not 0 <= shared <= 64:
            raise ValueError("Original TMP shared material count is invalid")
        material_offset = aligned + 44 + 12 * shared
        materials = struct.unpack_from("<i", raw, material_offset)[0]
        if not 0 <= materials <= 64:
            raise ValueError("Original TMP material count is invalid")
        style = aligned + 12 * (shared + materials)
        if style + 368 > len(raw):
            raise ValueError("Original TMP style extent is invalid")
        font_size = struct.unpack_from("<f", raw, style + 192)[0]
        color = struct.unpack_from("<4f", raw, style + 52)
        horizontal = struct.unpack_from("<i", raw, style + 220)[0]
        vertical = struct.unpack_from("<i", raw, style + 224)[0]
        word_wrap = struct.unpack_from("<i", raw, style + 256)[0]
        overflow = struct.unpack_from("<i", raw, style + 264)[0]
        margin = struct.unpack_from("<4f", raw, style + 352)
        if not math.isfinite(font_size) or font_size <= 0 or horizontal not in range(0, 9) \
                or vertical not in (0, 256, 512, 1024, 2048, 4096, 8192) \
                or word_wrap not in (0, 1) or overflow not in range(7) \
                or not all(math.isfinite(value) for value in color + margin):
            raise ValueError("Original TMP style values are invalid")
        return {"m_text": text, "m_fontSize": font_size,
                "m_fontColor": dict(zip(("r", "g", "b", "a"), color)),
                "m_HorizontalAlignment": horizontal, "m_VerticalAlignment": vertical,
                "m_enableWordWrapping": word_wrap, "m_overflowMode": overflow,
                "m_margin": dict(zip(("x", "y", "z", "w"), margin))}

    def object_tree(self, prefab_name: str):
        matches = [(path, ptr) for path, ptr in self.apk.container("prefabs_ui_cooking")
                   if path.endswith("/" + prefab_name + ".prefab") and ptr.type.name == "GameObject"]
        if len(matches) != 1:
            raise ValueError(f"Expected one original {prefab_name} prefab, found {len(matches)}")
        source_path, root_ptr = matches[0]
        root = root_ptr.deref().read(check_read=False)
        rows = []
        paths_by_path_id = {}
        component_sources = []
        sprite_refs = {}
        sibling_counts = {}

        def visit(game_object, parent_index: int, parent_path: str):
            transform, components = self.game_object_components(game_object)
            stem = parent_path + "/" + game_object.m_Name
            duplicate = sibling_counts.get(stem, 0)
            sibling_counts[stem] = duplicate + 1
            path = stem + (f" ({duplicate})" if duplicate else "")
            rect_transform = transform is not None and hasattr(transform, "m_AnchorMin")
            if rect_transform:
                row = {
                    "parentIndex": parent_index,
                    "name": game_object.m_Name,
                    "path": path,
                    "active": bool(game_object.m_IsActive),
                    "is_rect_transform": True,
                    "anchor_min": vec(transform.m_AnchorMin, 2),
                    "anchor_max": vec(transform.m_AnchorMax, 2),
                    "anchored_position": vec(transform.m_AnchoredPosition, 2),
                    "size_delta": vec(transform.m_SizeDelta, 2),
                    "pivot": vec(transform.m_Pivot, 2),
                    "scale": vec(transform.m_LocalScale, 3),
                    "rotation": rotation_z_degrees(transform.m_LocalRotation),
                    "scripts": [],
                }
            else:
                row = {
                    "parentIndex": parent_index,
                    "name": game_object.m_Name,
                    "path": path,
                    "active": bool(game_object.m_IsActive),
                    "is_rect_transform": False,
                    "local_position": vec(transform.m_LocalPosition, 3) if transform else [0, 0, 0],
                    "local_rotation": vec(transform.m_LocalRotation, 4) if transform else [0, 0, 0, 1],
                    "scale": vec(transform.m_LocalScale, 3) if transform else [1, 1, 1],
                    "scripts": [],
                }
            index = len(rows)
            rows.append(row)
            paths_by_path_id[int(game_object.object_reader.path_id)] = path
            row["scripts"] = [component[0] for component in components]
            if "Button" in row["scripts"]:
                row["button"] = True
            if "ItemIcon" in row["scripts"]:
                row["item_icon"] = True
            if "LoopVerticalScrollRect" in row["scripts"]:
                row["has_scroll"] = True
                row["scroll"] = {"horizontal": False, "vertical": True, "inertia": True,
                                 "movement": 1, "sensitivity": 1.0, "elasticity": 0.1,
                                 "deceleration": 0.135}
            for class_name, component, fields in components:
                if class_name in ("CookingPanel", "CookingPanelItem") and fields:
                    component_sources.append((class_name, fields, component))
                if fields is None:
                    continue
                if class_name == "Image":
                    row["has_image"] = True
                    sprite_fields = fields.get("m_Sprite")
                    sprite_object = pointer(sprite_fields, component) if sprite_fields and sprite_fields.get("m_PathID") else None
                    sprite = None
                    if sprite_object is not None:
                        sprite = self.add_sprite(sprite_object)
                        sprite_refs[sprite_object.path_id] = sprite
                    row["image"] = {
                        "sprite": sprite,
                        "color": [fields.get("m_Color", {}).get(channel, 1.0) for channel in ("r", "g", "b", "a")],
                        "type": int(fields.get("m_Type", 0)),
                        "preserve_aspect": bool(fields.get("m_PreserveAspect", 0)),
                        "raycast_target": bool(fields.get("m_RaycastTarget", 1)),
                    }
                elif class_name == "TextMeshProUGUI":
                    row["has_text"] = True
                    font = fields.get("m_fontAsset") or {}
                    font_obj = pointer(font, component) if font.get("m_PathID") else None
                    row["text"] = {
                        "value": fields.get("m_text", ""),
                        "font_asset_name": self.scriptable_name(font_obj),
                        "color": [fields.get("m_fontColor", {}).get(channel, 1.0) for channel in ("r", "g", "b", "a")],
                        "font_size": float(fields.get("m_fontSize", 32.0)),
                        "horizontal": int(fields.get("m_HorizontalAlignment", 2)),
                        "vertical": int(fields.get("m_VerticalAlignment", 512)),
                        "overflow": int(fields.get("m_overflowMode", 0)),
                        "word_wrap": bool(fields.get("m_enableWordWrapping", 1)),
                        "margin": [fields.get("m_margin", {}).get(key, 0.0) for key in ("x", "y", "z", "w")],
                    }
                elif class_name in ("HorizontalLayoutGroup", "VerticalLayoutGroup"):
                    row["has_layout"] = True
                    padding = fields.get("m_Padding", {})
                    row["layout"] = {
                        "axis": 0 if class_name == "HorizontalLayoutGroup" else 1,
                        "padding": [int(padding.get(key, 0)) for key in ("m_Left", "m_Right", "m_Top", "m_Bottom")],
                        "alignment": int(fields.get("m_ChildAlignment", 4)),
                        "spacing": float(fields.get("m_Spacing", 0.0)),
                        "expand": [bool(fields.get("m_ChildForceExpandWidth", 0)), bool(fields.get("m_ChildForceExpandHeight", 0))],
                        "control": [bool(fields.get("m_ChildControlWidth", 1)), bool(fields.get("m_ChildControlHeight", 1))],
                        "child_scale": [bool(fields.get("m_ChildScaleWidth", 0)), bool(fields.get("m_ChildScaleHeight", 0))],
                        "reverse": bool(fields.get("m_ReverseArrangement", 0)),
                    }
                elif class_name == "GridLayoutGroup":
                    row["has_grid"] = True
                    padding = fields.get("m_Padding", {})
                    cell = fields.get("m_CellSize", {})
                    spacing = fields.get("m_Spacing", {})
                    row["grid"] = {
                        "padding": [int(padding.get(key, 0)) for key in ("m_Left", "m_Right", "m_Top", "m_Bottom")],
                        "alignment": int(fields.get("m_ChildAlignment", 4)),
                        "corner": int(fields.get("m_StartCorner", 0)),
                        "axis": int(fields.get("m_StartAxis", 0)),
                        "constraint": int(fields.get("m_Constraint", 0)),
                        "count": int(fields.get("m_ConstraintCount", 2)),
                        "cell": [float(cell.get("x", 0.0)), float(cell.get("y", 0.0))],
                        "spacing": [float(spacing.get("x", 0.0)), float(spacing.get("y", 0.0))],
                    }
                elif class_name == "LayoutElement":
                    row["has_layout_element"] = True
                    row["layout_element"] = {
                        "ignore": bool(fields.get("m_IgnoreLayout", 0)),
                        "min": [float(fields.get("m_MinWidth", -1)), float(fields.get("m_MinHeight", -1))],
                        "preferred": [float(fields.get("m_PreferredWidth", -1)), float(fields.get("m_PreferredHeight", -1))],
                        "flexible": [float(fields.get("m_FlexibleWidth", -1)), float(fields.get("m_FlexibleHeight", -1))],
                    }
                elif class_name == "ContentSizeFitter":
                    row["has_size_fitter"] = True
                    row["size_fitter"] = [int(fields.get("m_HorizontalFit", 0)), int(fields.get("m_VerticalFit", 0))]
                elif class_name == "CanvasGroup":
                    row["alpha"] = float(fields.get("m_Alpha", 1.0))
                elif class_name in ("ScrollRect", "LoopVerticalScrollRect"):
                    row["has_scroll"] = True
                    row["scroll"] = {
                        "horizontal": bool(fields.get("m_Horizontal", 0)),
                        "vertical": bool(fields.get("m_Vertical", 1)),
                        "inertia": bool(fields.get("m_Inertia", 1)),
                        "movement": int(fields.get("m_MovementType", 1)),
                        "sensitivity": float(fields.get("m_ScrollSensitivity", 1.0)),
                        "elasticity": float(fields.get("m_Elasticity", 0.1)),
                        "deceleration": float(fields.get("m_DecelerationRate", 0.135)),
                    }
                if class_name == "Button":
                    row["button"] = True
                if class_name == "Mask":
                    row["mask"] = True
                if class_name == "RectMask2D":
                    row["rectmask2d"] = True

            if transform:
                for child in transform.m_Children:
                    child_object = child.deref().read(check_read=False).m_GameObject.deref().read(check_read=False)
                    visit(child_object, index, path)
            return index

        visit(root, -1, "")
        binding_rows = []
        for class_name, fields, component in component_sources:
            binding_names = ("ItemIcon", "MaterialsList", "StateSwitcher", "SetCountSlider", "Btn",
                             "ScrollRect", "ItemDesc", "BtnRedDot", "mSelectGo", "mItemSlot")
            owner_id = int(component.read(check_read=False).m_GameObject.path_id)
            bindings = {"component": class_name, "owner": paths_by_path_id.get(owner_id)}
            for name in binding_names:
                if name not in fields:
                    continue
                value = fields[name]
                if isinstance(value, dict) and "m_PathID" in value:
                    bindings[name] = paths_by_path_id.get(int(value["m_PathID"]))
                elif isinstance(value, list):
                    bindings[name] = [paths_by_path_id.get(int(entry.get("m_PathID", 0))) if entry else None for entry in value]
            binding_rows.append(bindings)
        prefab = root_ptr.deref()
        source = self.apk.provenance(prefab)
        return {"source_path": source_path, "source": source, "nodes": rows,
                "component_bindings": binding_rows}, sprite_refs

    def scriptable_name(self, obj):
        if obj is None:
            return ""
        try:
            data = obj.read(check_read=False)
            return getattr(data, "m_Name", "") or ""
        except (AttributeError, TypeError, ValueError):
            return ""

    def add_sprite(self, obj):
        name = obj.read(check_read=False).m_Name or "sprite"
        if name in self.sprite_outputs:
            if self.sprite_outputs[name]["path_id"] != str(obj.path_id):
                raise ValueError("Cooking prefab has distinct sprites with the same name: " + name)
            return self.sprite_outputs[name]["file"]
        sprite = obj.read()
        image = sprite.image
        output = io.BytesIO()
        image.save(output, format="PNG")
        data = output.getvalue()
        filename = f"{safe_name(name)}-{sha256(data)[:10]}.png"
        source = self.sprite_source(obj)
        self.sprite_outputs[name] = {"file": filename, "bytes": data, "source": source,
                                     "path_id": str(obj.path_id), "width": image.width, "height": image.height,
                                     "border": vec(getattr(sprite, "m_Border", None), 4),
                                     "pivot": vec(getattr(sprite, "m_Pivot", None), 2),
                                     "pixels_per_unit": float(getattr(sprite, "m_PixelsToUnits", 100.0))}
        return filename

    def sprite_source(self, obj):
        source = self.apk.provenance(obj)
        bundle_name = source["bundle"]
        receipt = self.receipts.get(bundle_name)
        if receipt and receipt.get("bundle_hash") != source["bundle_hash"]:
            raise ValueError("Sprite bundle receipt hash differs from resolved source: " + bundle_name)
        if (receipt and receipt.get("origin") == "apk_zip") or source.get("origin") == "apk_zip":
            apk_sha256 = receipt.get("apk_sha256") if receipt else source.get("apk_sha256")
            apk_entry = receipt.get("path") if receipt else source.get("apk_entry")
            if apk_sha256 != APK_SHA256 or not apk_entry:
                raise ValueError("Sprite APK receipt differs from the pinned APK: " + bundle_name)
            source["origin"] = "apk_zip"
            source["manifest"] = MANIFEST_ID
            source["apk_sha256"] = apk_sha256
            source["apk_entry"] = apk_entry
            if bundle_name not in self.package_source_cache:
                raw = self.apk.archive.read(apk_entry)
                if hashlib.md5(raw).hexdigest() != source["bundle_hash"]:
                    raise ValueError("Packaged sprite bundle MD5 differs from its receipt: " + bundle_name)
                self.package_source_cache[bundle_name] = {"bundle_size": len(raw), "bundle_sha256": sha256(raw)}
            source.update(self.package_source_cache[bundle_name])
        else:
            matches = [value for value in self.cloud_receipts_by_bundle.get(bundle_name, [])
                       if value.get("status") == "downloaded"
                       and value.get("md5") == source["bundle_hash"]]
            if len(matches) != 1:
                raise ValueError("Sprite source has no unique fixed-version CDN receipt: " + bundle_name)
            cloud_receipt = matches[0]
            expected_file = "DefaultPackage/" + source["bundle_hash"] + ".bundle"
            if cloud_receipt.get("file") != expected_file:
                raise ValueError("Sprite CDN receipt path differs from its bundle hash: " + bundle_name)
            cloud_path = SOURCE_CLOUD_ROOT / expected_file
            if not cloud_path.is_file() or cloud_path.stat().st_size != cloud_receipt.get("size") \
                    or file_sha(cloud_path) != cloud_receipt.get("sha256"):
                raise ValueError("Pinned sprite CDN file differs from its receipt: " + bundle_name)
            source.update(origin="official_cdn", manifest=MANIFEST_ID, url=cloud_receipt["url"],
                          bundle_size=cloud_receipt["size"], bundle_sha256=cloud_receipt["sha256"],
                          cdn_path=str(cloud_path.relative_to(ROOT)).replace("\\", "/"))
        return source

    def cooking_data(self):
        rules_manifest = json.loads(RULES_MANIFEST.read_text(encoding="utf-8"))
        if rules_manifest.get("apk_sha256") != APK_SHA256:
            raise ValueError("The decoded cooking rules manifest does not match its pinned APK receipt")
        table_receipts = rules_manifest.get("tables", {})
        cooking_raw, cooking_source = self.raw_table("cooking", "TbCookingInfo")
        item_raw, item_source = self.raw_table("item", "TbItemInfo")
        settings_raw, settings_source = self.raw_table("game_settings", "TbGameSettings")
        for name, raw, expected in (
            ("cooking", cooking_raw, "99cfe4b96d2b46897f0ceb3125029732e14b16a5d97b0c164540616f8e64275f"),
            ("item", item_raw, "a317a104405d25b63d1ec8c6f63397001f8cc6f2329a04034c2a49be4a18934d"),
            ("game_settings", settings_raw, "8832aa02061ada1c2dad9cc190fbe6fe54a25be1e30596513b9f8c1a1ef22b27"),
        ):
            if sha256(raw) != expected or sha256(raw) != table_receipts[name]["sha256"]:
                raise ValueError("Pinned official config bytes differ from the rules manifest: " + name)
        settings = normalized(settings_source)
        if settings.get("MaxCookingCountAtATime") != 9999 or settings.get("CookingRecoveryCountThreshold") != 10:
            raise ValueError("Pinned official cooking limits differ from the verified source table")

        item_ids = {row["Id"] for row in cooking_source}
        item_ids.update(int(item_id) for row in cooking_source for item_id in (row.get("Materials") or {}))
        item_ids.update(row["Condition"]["ClassId"] for row in cooking_source
                        if row["Condition"]["_type"] == "UsedItemCondition")
        source_items = {row["ClassId"]: normalized(row) for row in item_source}
        items = []
        for item_id in sorted(item_ids):
            if item_id not in source_items:
                raise ValueError("A cooking output, material or formula is absent from the pinned item table: " + str(item_id))
            parsed_item = source_items[item_id]
            items.append({"id": item_id, "name": parsed_item["Name"], "icon": parsed_item["RawIcon"],
                          "itemType": parsed_item["ItemType"], "quality": parsed_item["Quality"]})

        recipes = []
        for row in cooking_source:
            condition = row["Condition"]
            effects = []
            for effect_type, details in sorted((row.get("EffectInfos") or {}).items(), key=lambda entry: int(entry[0])):
                effects.append({
                    "type": int(effect_type),
                    "kind": details["_type"],
                    "curePercent": details.get("CurePercent", 0.0),
                    "cureValue": details.get("CureValue", 0.0),
                    "statusEffectId": details.get("StatusEffectId", 0),
                    "propId": details.get("PropId", 0),
                    "propTypes": details.get("PropTypes") or [],
                })
            recipes.append({
                "id": row["Id"], "count": row["Count"], "type": row["Type"],
                "materials": [{"itemId": int(item_id), "count": count}
                              for item_id, count in sorted((row.get("Materials") or {}).items(), key=lambda entry: int(entry[0]))],
                "conditionType": condition["_type"], "mapClassId": condition.get("MapClassId", 0),
                "formulaItemId": condition.get("ClassId", 0), "useSourceOperate": condition.get("UseSourceOperate", 0),
                "effects": effects, "effectIcon": row.get("EffectIcon", ""),
                "effectIconColor": row.get("EffectIconColor", ""),
            })
        return ({"manifest": MANIFEST_ID,
                 "sourceHashes": {"cookingTable": sha256(cooking_raw), "itemTable": sha256(item_raw),
                                  "settingsTable": sha256(settings_raw)},
                 "maxCookingCountAtATime": settings["MaxCookingCountAtATime"],
                 "cookingRecoveryCountThreshold": settings["CookingRecoveryCountThreshold"],
                 "recipes": recipes, "items": items},
                {"cooking": sha256(cooking_raw), "item": sha256(item_raw),
                 "game_settings": sha256(settings_raw), "cooking_count": len(recipes), "item_count": len(items)})

    def write_sprite(self, filename: str, data: bytes, metadata: dict):
        target = SPRITES / filename
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs:cooking:" + filename).hex
        border = metadata["border"]
        pivot = metadata["pivot"]
        meta = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 9
  spritePivot: {{x: {pivot[0]:.9g}, y: {pivot[1]:.9g}}}
  spritePixelsToUnits: {metadata["pixels_per_unit"]:.9g}
  spriteBorder: {{x: {border[0]:.9g}, y: {border[1]:.9g}, z: {border[2]:.9g}, w: {border[3]:.9g}}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {guid}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
        target.with_name(target.name + ".meta").write_text(meta, encoding="utf-8")

    def build(self):
        if not BUNDLE_FILE.is_file() or file_sha(BUNDLE_FILE) != BUNDLE_SHA256:
            raise ValueError("Cached Cooking prefab bundle is missing or differs from the pinned CDN receipt")
        if not CLOUD_BUNDLE_FILE.is_file() or file_sha(CLOUD_BUNDLE_FILE) != BUNDLE_SHA256:
            raise ValueError("The fixed-version Cooking prefab CDN cache differs from its pinned receipt")
        receipt = self.bundle_source_receipt
        if receipt.get("status") != "downloaded" or receipt.get("size") != BUNDLE_SIZE \
                or receipt.get("md5") != BUNDLE_MD5 or receipt.get("sha256") != BUNDLE_SHA256:
            raise ValueError("Cooking prefab CDN receipt does not match the pinned source")
        catalog, config_sources = self.cooking_data()
        panel, _ = self.object_tree("CookingPanel")
        item_row, _ = self.object_tree("CookingPanelItem")
        ui = {"manifest": MANIFEST_ID, "panel": panel, "itemRow": item_row,
              "panelBindings": next((value for value in panel["component_bindings"]
                                      if value["component"] == "CookingPanel"), {}),
              "itemRowBindings": next((value for value in item_row["component_bindings"]
                                       if value["component"] == "CookingPanelItem"), {})}

        PAYLOAD.mkdir(parents=True, exist_ok=True)
        SPRITES.mkdir(parents=True, exist_ok=True)
        REFERENCE.mkdir(parents=True, exist_ok=True)
        self.write_folder_meta(SPRITES.parent)
        self.write_folder_meta(SPRITES)
        self.write_folder_meta(PAYLOAD)
        for metadata in self.sprite_outputs.values():
            self.write_sprite(metadata["file"], metadata["bytes"], metadata)
        (PAYLOAD / "cooking.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (PAYLOAD / "ui.json").write_text(json.dumps(ui, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        self.write_file_meta(PAYLOAD / "cooking.json")
        self.write_file_meta(PAYLOAD / "ui.json")

        code_paths = [
            ".tools/source-packages/official-code/client-source/CookingPanel.cs",
            ".tools/source-packages/official-code/client-source/CookingPanelItem.cs",
            ".tools/source-packages/official-code/client-source/ItemUseEffectCookingFormula.cs",
            ".tools/source-packages/official-code/common-source/Common/CookingInfo.cs",
            ".tools/source-packages/official-code/common-source/Common/CookingInfoParser.cs",
            ".tools/source-packages/official-code/common-source/Common/CookingExtraInfoRecovery.cs",
            ".tools/source-packages/official-code/common-source/CookingMakeItemComparer.cs",
            ".tools/source-packages/official-code/common-source/CookingItemComparer.cs",
            ".tools/source-packages/official-code/common-source/ItemComparer.cs",
            ".tools/source-packages/official-code/common-source/ItemComparerBase.cs",
            ".tools/source-packages/official-code/common-source/Common/UsedItemCondition.cs",
            ".tools/source-packages/official-code/common-source/Common/MapUnlockCondition.cs",
            ".tools/source-packages/official-code/common-source/Common/SourceOperate.cs",
            ".tools/source-packages/official-code/common-source/Common/PlayerCookingRequest.cs",
        ]
        code = {path.removeprefix(".tools/source-packages/official-code/"): file_sha(ROOT / path)
                for path in code_paths if (ROOT / path).is_file()}
        sprite_records = [{"name": name, "file": value["file"], "source": value["source"],
                           "path_id": value["path_id"], "sha256": sha256(value["bytes"])}
                          for name, value in sorted(self.sprite_outputs.items())]
        source_manifest = {
            "manifest": MANIFEST_ID,
            "apk": {"path": ".tools/source-packages/zjcs_M509085.apk", "sha256": APK_SHA256},
            "bundle": {"path": str(CLOUD_BUNDLE_FILE.relative_to(ROOT)).replace("\\", "/"),
                       "name": BUNDLE, "origin": "official_cdn", "url": receipt["url"],
                       "manifest": MANIFEST_ID, "size": BUNDLE_SIZE, "md5": BUNDLE_MD5,
                       "sha256": BUNDLE_SHA256, "panel": panel["source"], "itemRow": item_row["source"]},
            "config": {"tables": config_sources, "source_records": table_receipts_from(RULES_MANIFEST)},
            "official_code": code,
            "sprites": sprite_records,
            "outputs": {
                "unity/Assets/StreamingAssets/Cooking/cooking.json": file_sha(PAYLOAD / "cooking.json"),
                "unity/Assets/StreamingAssets/Cooking/ui.json": file_sha(PAYLOAD / "ui.json"),
                **{f"unity/Assets/Game/Resources/Cooking/Sprites/{value['file']}":
                   file_sha(SPRITES / value["file"])
                   for value in self.sprite_outputs.values()},
            },
            "counts": {"recipes": len(catalog["recipes"]), "related_items": len(catalog["items"]),
                       "panel_nodes": len(panel["nodes"]), "item_row_nodes": len(item_row["nodes"]),
                       "sprites": len(sprite_records)},
            "limitations": [
                "The prefab is rebuilt from original UGUI transforms, images, text, layout and input component fields; original Unity Animation and custom particle/UI-mesh runtime components are recorded but not executed by this offline panel.",
                "The original LoopVerticalScrollRect and LoopScrollGridLayoutGroup runtime sources are unavailable; native ScrollRect and a responsive grid use the official 100x100 item-row size.",
                "The official OperPlayerCooking server settlement is not present in the client sources; offline craft settlement follows the official recipe costs/output and commits through the local inventory transaction.",
                "The original cooking buff, permanent-property application, recovery consumption and combat refresh flow are outside this module's crafting scope.",
            ],
        }
        (REFERENCE / "source-manifest.json").write_text(json.dumps(source_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"recipes": len(catalog["recipes"]), "items": len(catalog["items"]),
                          "panel_nodes": len(panel["nodes"]), "row_nodes": len(item_row["nodes"]),
                          "sprites": len(sprite_records), "outputs": source_manifest["outputs"]}, ensure_ascii=False, indent=2))

    @staticmethod
    def write_folder_meta(directory: Path):
        directory.mkdir(parents=True, exist_ok=True)
        meta = directory.with_name(directory.name + ".meta")
        if not meta.exists():
            relative = str(directory.relative_to(ROOT)).replace("\\", "/")
            guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs:cooking:" + relative).hex
            meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
                            encoding="utf-8")

    @staticmethod
    def write_file_meta(path: Path):
        meta = path.with_name(path.name + ".meta")
        if not meta.exists():
            guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs:cooking:" + str(path.relative_to(ROOT)).replace("\\", "/")).hex
            meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
                            encoding="utf-8")


def table_receipts_from(path: Path):
    manifest = json.loads(path.read_text(encoding="utf-8"))
    return {name: manifest["tables"][name] for name in ("cooking", "item", "game_settings")}


def main():
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    CookingConverter().build()


if __name__ == "__main__":
    main()
