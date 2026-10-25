"""Restore serialized Image and TMP data removed by the current AssetRipper export."""
from __future__ import annotations

import hashlib
import io
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
VERSION = "871_156598"
RUNTIME = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI"
BINDINGS = RUNTIME / "official-bindings.json"
RECEIPT = ROOT / "assets/reference/dungeon-ui/prefab-binding-source-manifest.json"
PREFABS = (
    ("TravelNotesPanel", "prefabs_ui_home", "/TravelNotesPanel.prefab"),
    ("DailyDungeonEntryPanel", "prefabs_ui_dailydungeon", "/DailyDungeonEntryPanel.prefab"),
    ("DailyDungeonEntryPanelItem", "prefabs_ui_dailydungeon", "/DailyDungeonEntryPanelItem.prefab"),
    ("DailyDungeonConfirmEnterPanel", "prefabs_ui_dailydungeon", "/DailyDungeonConfirmEnterPanel.prefab"),
    ("DailyDungeonDialog", "prefabs_ui_dailydungeon", "/DailyDungeonDialog.prefab"),
    ("DailyDungeonBigMapDialog", "prefabs_ui_dailydungeon", "/DailyDungeonBigMapDialog.prefab"),
    ("DailyDungeonRoomItem", "prefabs_ui_dailydungeon", "/DailyDungeonRoomItem.prefab"),
    ("ArenaPanel", "prefabs_ui_arena", "/ArenaPanel.prefab"),
)
GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def relative_transform_path(game_object, path=(), names=()):
    transform = next((pair.component.read() for pair in game_object.m_Component
                      if pair.component.type.name in ("RectTransform", "Transform")), None)
    yield game_object, path, names + (game_object.m_Name,), transform
    if transform is None:
        return
    for index, child in enumerate(transform.m_Children):
        yield from relative_transform_path(child.read().m_GameObject.read(), path + (index,),
                                           names + (game_object.m_Name,))


def prefab_bindings(apk, schemas, sprite_cache, key, bundle, asset_path):
    from prepare_character_creation_opening import decode_image, decode_text
    from prepare_dungeon_ui_sprites import asset_meta, ensure_folder_meta

    prefab = apk.asset(bundle, asset_path)
    transforms = []
    images = []
    texts = []
    for game_object, path, names, transform in relative_transform_path(prefab.read()):
        path_key = "/".join(map(str, path))
        object_names = list(names)
        if transform is not None:
            position = transform.m_LocalPosition
            rotation = transform.m_LocalRotation
            scale = transform.m_LocalScale
            record = {
                "path": path_key,
                "objects": object_names,
                "isRectTransform": hasattr(transform, "m_AnchorMin"),
                "localPosition": [float(position.x), float(position.y), float(position.z)],
                "localRotation": [float(rotation.x), float(rotation.y), float(rotation.z), float(rotation.w)],
                "localScale": [float(scale.x), float(scale.y), float(scale.z)],
            }
            if record["isRectTransform"]:
                record.update({
                    "anchorMin": [float(transform.m_AnchorMin.x), float(transform.m_AnchorMin.y)],
                    "anchorMax": [float(transform.m_AnchorMax.x), float(transform.m_AnchorMax.y)],
                    "anchoredPosition": [float(transform.m_AnchoredPosition.x),
                                         float(transform.m_AnchoredPosition.y)],
                    "sizeDelta": [float(transform.m_SizeDelta.x), float(transform.m_SizeDelta.y)],
                    "pivot": [float(transform.m_Pivot.x), float(transform.m_Pivot.y)],
                })
            transforms.append(record)
        for pair in game_object.m_Component:
            reader = pair.component.deref()
            if reader.type.name != "MonoBehaviour":
                continue
            script = reader.read(check_read=False).m_Script.read().m_ClassName
            if script == "Image":
                fields = schemas.read(reader)
                decoded = decode_image(fields)
                image = {
                    "path": path_key,
                    "objects": object_names,
                    "enabled": bool(reader.read(check_read=False).m_Enabled),
                    "color": decoded["color"],
                    "type": decoded["type"],
                    "preserveAspect": decoded["preserve"],
                    "raycastTarget": bool(fields["m_RaycastTarget"]),
                    "maskable": bool(fields["m_Maskable"]),
                    "sprite": None,
                }
                file_id, path_id = decoded["sprite"]
                if path_id:
                    sprite = sprite_cache.sprite_image(reader.assets_file, file_id, path_id)
                    if sprite is None:
                        raise ValueError("Official UI Sprite could not be resolved: "
                                         + key + ":" + path_key)
                    source_object = sprite_cache.lookup(reader.assets_file, file_id, path_id)
                    source_key = source_object.assets_file.name + ":" + str(source_object.path_id)
                    provenance = sprite_cache.sources[source_key]
                    source_bundle = provenance["bundle"]
                    sprite_key = source_bundle + ":" + str(source_object.path_id)
                    filename = re.sub(r"[^A-Za-z0-9_.-]+", "_", sprite["name"]).strip("._")
                    filename = (filename or "sprite") + "-" + sha256(sprite_key.encode())[:12] + ".png"
                    relative = (Path("Sprites") / source_bundle.removesuffix(".bundle") / filename)
                    target = RUNTIME / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    ensure_folder_meta(target.parent)
                    buffer = io.BytesIO()
                    sprite["image"].save(buffer, format="PNG", optimize=False)
                    payload = buffer.getvalue()
                    if target.exists() and target.read_bytes() != payload:
                        raise ValueError("Refusing to overwrite changed dungeon UI sprite: " + str(target))
                    if not target.exists():
                        target.write_bytes(payload)
                    source_guid = hashlib.sha256(("zjcs-dungeon-ui-bound-sprite:" + sprite_key)
                                                 .encode()).hexdigest()[:32]
                    meta = target.with_name(target.name + ".meta")
                    meta_bytes = asset_meta(source_guid)
                    if meta.exists():
                        existing = GUID.search(meta.read_text(encoding="ascii"))
                        if existing is None or existing.group(1) != source_guid:
                            raise ValueError("Dungeon UI sprite GUID changed: " + str(meta))
                    else:
                        meta.write_bytes(meta_bytes)
                    image["sprite"] = {
                        "file": relative.as_posix(),
                        "name": sprite["name"],
                        "pivot": sprite["pivot"],
                        "border": sprite["border"],
                        "pixelsPerUnit": sprite["ppu"],
                        "source": provenance,
                        "sha256": sha256(payload),
                    }
                images.append(image)
            elif script in ("TextMeshProUGUI", "TextMeshPro"):
                decoded = decode_text(reader.get_raw_data(), key + ":" + path_key)
                texts.append({
                    "path": path_key,
                    "objects": object_names,
                    "text": decoded["text"],
                    "fontSize": decoded["font_size"],
                    "fontWeight": decoded["font_weight"],
                    "fontStyle": decoded["font_style"],
                    "autoSizing": decoded["auto_sizing"],
                    "fontSizeRange": decoded["font_size_range"],
                    "alignment": decoded["alignment"],
                    "wordWrap": decoded["word_wrap"],
                    "overflow": decoded["overflow"],
                    "margin": decoded["margin"],
                    "color": decoded["color"],
                })
    return {"name": key, "transforms": transforms, "images": images, "texts": texts}, apk.provenance(prefab)


def build():
    sys.path.insert(0, str(ROOT / "scripts"))
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from prepare_character_creation_opening import SpriteCache
    from prepare_dungeon_ui_sprites import asset_meta, ensure_output_folders

    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Dungeon UI bundle version is not the active official version")
    ensure_output_folders()
    schemas = ScriptSchemas()
    sprites = SpriteCache(apk)
    prefabs = []
    sources = []
    for key, bundle, path in PREFABS:
        prefab, source = prefab_bindings(apk, schemas, sprites, key, bundle, path)
        prefabs.append(prefab)
        sources.append({"name": key, "path": path, "source": source,
                        "image_count": len(prefab["images"]),
                        "text_count": len(prefab["texts"])})
    document = {"manifest": VERSION, "prefabs": prefabs}
    encoded = (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    BINDINGS.parent.mkdir(parents=True, exist_ok=True)
    update = "--update" in sys.argv[1:]
    if BINDINGS.exists() and BINDINGS.read_bytes() != encoded and not update:
        raise ValueError("Refusing to overwrite changed official dungeon UI bindings")
    if not BINDINGS.exists() or update:
        BINDINGS.write_bytes(encoded)
    meta = BINDINGS.with_name(BINDINGS.name + ".meta")
    if not meta.exists():
        guid = hashlib.sha256(b"zjcs-dungeon-ui-official-bindings:" + VERSION.encode()).hexdigest()[:32]
        meta.write_text("fileFormatVersion: 2\n"
                        f"guid: {guid}\nTextScriptImporter:\n"
                        "  externalObjects: {}\n  userData: \n  assetBundleName: \n"
                        "  assetBundleVariant: \n", encoding="ascii")
    sprite_outputs = []
    for relative in sorted({entry["sprite"]["file"] for prefab in prefabs
                            for entry in prefab["images"] if entry["sprite"]}):
        path = RUNTIME / relative
        sprite_outputs.append({"path": path.relative_to(ROOT).as_posix(),
                               "sha256": sha256(path.read_bytes()),
                               "meta_sha256": sha256(path.with_name(path.name + ".meta").read_bytes())})
    receipt = {"manifest": VERSION, "bindings": {
        "path": BINDINGS.relative_to(ROOT).as_posix(), "sha256": sha256(encoded),
        "meta_sha256": sha256(meta.read_bytes())},
        "prefabs": sources, "sprites": sprite_outputs,
        "sprite_sources": sorted({entry["sprite"]["source"]["bundle"]
                                   for prefab in prefabs for entry in prefab["images"]
                                   if entry["sprite"]})}
    receipt_encoded = json.dumps(receipt, ensure_ascii=False, sort_keys=True, indent=2) + "\n"
    if RECEIPT.exists() and RECEIPT.read_text(encoding="utf-8") != receipt_encoded and not update:
        raise ValueError("Refusing to overwrite changed dungeon UI binding receipt")
    if not RECEIPT.exists() or update:
        RECEIPT.parent.mkdir(parents=True, exist_ok=True)
        RECEIPT.write_text(receipt_encoded, encoding="utf-8", newline="\n")
    print(json.dumps({"manifest": VERSION, "prefabs": sources,
                      "sprites": len(sprite_outputs), "receipt": RECEIPT.relative_to(ROOT).as_posix()},
                     ensure_ascii=False))


if __name__ == "__main__":
    build()
