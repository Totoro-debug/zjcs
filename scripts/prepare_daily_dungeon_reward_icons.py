"""Bind every configured daily dungeon reward to its selected official item sprite."""
from pathlib import Path
import hashlib
import io
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    from official_apk import Apk
    from official_tables import Constructors
    from prepare_character_creation_opening import SpriteCache
    from prepare_dungeon_ui_sprites import asset_meta, ensure_folder_meta
    from PIL import Image

    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review the reward icon source version before reproduction")
    code = ROOT / ".tools/source-packages/current/884_156692/official-code"
    resources = ROOT / "unity/Assets/Game/Resources/Dungeons"
    configs = [resources / name for name in ("dungeons-runtime.json", "daily-dungeon-achievements.json",
                                            "daily-dungeon-top-achievements.json")]
    dungeon, claims, top = [json.loads(path.read_bytes()) for path in configs]
    item_ids = {item for row in dungeon["rewardRules"] for item in row["IdList"]}
    item_ids.update(item for pool in dungeon["randomEquipmentPools"] for item in pool["equipIds"])
    item_ids.update(reward["ItemId"] for config in (claims, top) for row in config["Rows"] for reward in row["Rewards"])
    item_object = apk.asset("config_luban", "/item.g.bytes")
    item_raw = item_object.read().m_Script.encode("utf8", "surrogateescape")
    items = {row["ClassId"]: row for row in Constructors(code).table(item_raw, "TbItemInfo")}
    wanted = {items[item_id]["RawIcon"] for item_id in item_ids}
    if any(not path.startswith("ItemIcon/") for path in wanted):
        raise ValueError("Review an original reward with a non-item sprite path")
    names = {path.rsplit("/", 1)[-1] for path in wanted}
    categories = {path.split("/")[1].lower() for path in wanted}
    source_sprites = {}
    for bundle in apk.index:
        if not any("image_itemicon_" + category + "_" in bundle for category in categories):
            continue
        for obj in apk.objects(bundle):
            if obj.type.name != "Sprite":
                continue
            name = obj.peek_name()
            if name not in names:
                continue
            if name in source_sprites:
                raise ValueError("Ambiguous official reward sprite: " + name)
            source_sprites[name] = obj
    if names - source_sprites.keys():
        raise ValueError("Unresolved original reward sprites: " + str(sorted(names - source_sprites.keys())))

    payload_root = ROOT / "unity/Assets/Official/Dungeons/884_156692/RewardIcons"
    payload_root.mkdir(parents=True, exist_ok=True)
    ensure_folder_meta(payload_root)
    cache, sprites, outputs = SpriteCache(apk), {}, []
    for name, obj in sorted(source_sprites.items()):
        data = obj.read()
        decoded = cache.sprite_image(obj.assets_file, 0, obj.path_id)
        if decoded is None:
            raise ValueError("Cannot decode original reward sprite: " + name)
        # UnityPy applies the original packed rotation and tight mesh mask.
        image = data.image.convert("RGBA")
        rect = data.m_Rect
        rd = data.m_RD
        if data.m_SpriteAtlas and data.m_SpriteAtlas.path_id:
            rd = dict(data.m_SpriteAtlas.read().m_RenderDataMap)[data.m_RenderDataKey]
        logical_size = (round(rect.width), round(rect.height))
        if image.size != logical_size:
            offset = rd.textureRectOffset
            padded = Image.new("RGBA", logical_size)
            padded.paste(image, (round(offset.x), logical_size[1] - round(offset.y) - image.height))
            image = padded
        buffer = io.BytesIO()
        image.save(buffer, format="PNG", optimize=False)
        path = payload_root / (name + ".png")
        path.write_bytes(buffer.getvalue())
        guid = digest(("daily-dungeon-reward-icon-884:" + name).encode())[:32]
        meta = asset_meta(guid).decode("ascii").replace("  spritePixelsToUnits: 100\n",
            "  spritePixelsToUnits: " + str(decoded["ppu"]) + "\n")
        meta += ("  spriteAlignment: 9\n  spritePivot: {x: " + str(decoded["pivot"][0]) + ", y: "
                 + str(decoded["pivot"][1]) + "}\n  spriteBorder: {" + ", ".join(
                     axis + ": " + str(value) for axis, value in zip(("x", "y", "z", "w"), decoded["border"])) + "}\n")
        meta_path = path.with_name(path.name + ".meta")
        meta_path.write_text(meta, encoding="ascii", newline="\n")
        outputs.extend((path, meta_path))
        sprites[name] = dict(guid=guid, source=apk.provenance(obj), object_sha256=digest(obj.get_raw_data()))

    script = ROOT / "unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonRewardAssetCatalog.cs"
    script_guid = next(line.split(": ", 1)[1] for line in script.with_name(script.name + ".meta").read_text().splitlines()
                       if line.startswith("guid: "))
    catalog = resources / "DailyDungeonRewardAssetCatalog.asset"
    text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            "  m_Script: {fileID: 11500000, guid: " + script_guid + ", type: 3}\n"
            "  m_Name: DailyDungeonRewardAssetCatalog\n  m_EditorClassIdentifier:\n  manifest: 884_156692\n  items:\n")
    for item_id in sorted(item_ids):
        row = items[item_id]
        sprite = sprites[row["RawIcon"].rsplit("/", 1)[-1]]
        text += (f"  - itemId: {item_id}\n    quality: {row['Quality']}\n    itemType: {row['ItemType']}\n"
                 f"    showPieceIcon: {int(row['ShowPieceIcon'])}\n"
                 "    sprite: {fileID: 21300000, guid: " + sprite["guid"] + ", type: 3}\n")
    catalog.write_text(text, encoding="utf8", newline="\n")
    meta_path = catalog.with_name(catalog.name + ".meta")
    meta_path.write_text("fileFormatVersion: 2\nguid: " + digest(b"daily-dungeon-reward-catalog-884")[:32]
                        + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n", encoding="ascii")
    outputs.extend((catalog, meta_path))
    receipt = dict(manifest=apk.version, item_config=dict(source=apk.provenance(item_object), sha256=digest(item_raw)),
        configs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in configs],
        operation="Resolve original ItemInfo.RawIcon; decode original texture, atlas rotation and tight mesh; restore sprite rect padding, pivot, border and pixels per unit; bind shared sprites explicitly by item id.",
        sprites=sprites, image_sources=list(cache.sources.values()), item_count=len(item_ids),
        code_sources=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in
                      (code / "common-source/Common/ItemInfo.cs", code / "common-source/Common/TbItemInfo.cs")],
        tools=[dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=digest(Path(__file__).read_bytes()))],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in outputs])
    receipt_path = ROOT / "assets/reference/dungeon-ui/daily-dungeon-reward-icons-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print(json.dumps(dict(items=len(item_ids), sprites=len(sprites))))


if __name__ == "__main__":
    build()
