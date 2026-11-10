"""Bind every configured daily dungeon reward to its selected official item sprite."""
from pathlib import Path
import hashlib
import io
import json
import re
import sys
from collections import Counter

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
    constructors = Constructors(code)
    items = {row["ClassId"]: row for row in constructors.table(item_raw, "TbItemInfo")}
    settings_object = apk.asset("config_luban", "/game_settings.g.bytes")
    settings_raw = settings_object.read().m_Script.encode("utf8", "surrogateescape")
    settings = constructors.table(settings_raw, "TbGameSettings")
    settings = settings[0] if isinstance(settings, list) else settings
    random_object = apk.asset("config_luban", "/random_reward.g.bytes")
    random_raw = random_object.read().m_Script.encode("utf8", "surrogateescape")
    random_rewards = {row["Id"]: row for row in constructors.table(random_raw, "TbRandomRewardInfo")}
    equip_piece_object = apk.asset("config_luban", "/equip_piece_merge.g.bytes")
    equip_piece_raw = equip_piece_object.read().m_Script.encode("utf8", "surrogateescape")
    equip_piece_merges = {row["Id"]: row for row in constructors.table(
        equip_piece_raw, "TbEquipPieceMergeInfo")}
    item_piece_object = apk.asset("config_luban", "/item_piece_merge.g.bytes")
    item_piece_raw = item_piece_object.read().m_Script.encode("utf8", "surrogateescape")
    item_piece_merges = {row["Id"]: row for row in constructors.table(
        item_piece_raw, "TbItemPieceMergeInfo")}
    item_type_source = code / "common-source/Common/ItemType.cs"
    item_types = {name: int(value) for name, value in re.findall(
        r"(?m)^\s*(\w+)\s*=\s*(-?\d+)\s*,?\s*$", item_type_source.read_text(encoding="utf-8"))}
    item_type_names = {value: name for name, value in item_types.items()}
    item_use_effect_source = code / "common-source/Common/ItemUseEffectType.cs"
    item_use_effect_types = {name: value for value, name in enumerate(re.findall(
        r"(?m)^\s*(\w+)\s*,?\s*$", item_use_effect_source.read_text(encoding="utf-8").split("{", 1)[1].split("}", 1)[0]))}
    equip_types = set(settings["EquipTypes"])
    skill_types = set(settings["SkillTypes"])
    monster_skill_types = set(settings["MonsterSkillTypes"])
    skill_piece_types = set(settings["SkillPieceTypes"])
    appearance_types = set(settings["AppearanceTypes"])
    home_decoration_types = set(settings["HomeDecorationTypes"])
    appearance_chest_types = set(settings["AppearanceChestTypes"])

    def detail_dialog_type(item):
        item_type = item["ItemType"]
        if item_type in equip_types:
            return "EquipDetailDialog"
        if item_type == item_types["Gem"]:
            return "GemDetailDialog"
        if item_type in skill_types:
            return "SkillDetailDialog"
        if item_type in monster_skill_types:
            return "MonsterSkillDetailDialog"
        if item_type in skill_piece_types:
            return "SkillPieceDetailDialog"
        fixed_dialogs = {
            "CommonItemPiece": "CommonItemPieceUseDetailDialog",
            "CommonEquipPiece": "CommonEquipPieceUseDetailDialog",
            "RandomTreasurePiece": "RandomTreasurePieceUseDetailDialog",
            "Treasure": "TreasureDetailDialog",
            "TreasurePiece": "TreasurePieceDialog",
            "Cooking": "CookingUseDetailDialog",
            "EntitySkill": "PetSkillDetailDialog",
            "Pet": "PetEnvolutionPreviewPanel",
        }
        for type_name, dialog in fixed_dialogs.items():
            if item_type == item_types[type_name]:
                return dialog
        if item_type in appearance_types:
            return "HonorTitleItemDetailDialog" if item_type == item_types["HonorTitle"] else "AppearanceItemDetailDialog"
        if item_type in home_decoration_types:
            return "FurnitureDecorationItemDetailDialog"
        type_dialogs = {
            "LegionHangUpMaterial": "LegionMapHangUpMaterialDetailDialog",
            "SkillSkin": "SkillSkinItemDetailDialog",
            "WrapItem": "WrapItemDetailDialog",
            "EquipAttributeReplaceItem": "ItemEquipAttribuateReplaceDetailDialog",
        }
        for type_name, dialog in type_dialogs.items():
            if item_type == item_types[type_name]:
                return dialog
        if item["CanUse"]:
            if item_type in appearance_chest_types:
                return "AppearanceChestItemUseDetailDialog"
            if item_type == item_types["CookingFormula"]:
                return "CookingFormulaUseDetailDialog"
            effect = item["ItemUseEffectType"]
            if effect == item_use_effect_types["AddDelegateTask"]:
                return "AddDelegateTaskItemUseDetailDialog"
            if item_type == item_types["FastHangUp"]:
                return "FastHangUpItemUseDetailDialog"
            if item_type == item_types["ResetHangUpLevel"]:
                return "ResetHangUpLevelItemUseDetailDialog"
            if effect == item_use_effect_types["AddVoidfissureAwardTimes"]:
                return "AddVoidFissureAwardTimesItemUseDetailDialog"
            if effect == item_use_effect_types["PetPieceMerge"]:
                return "PetMergeItemUseDetailDialog"
            return "ItemUseDetailDialog"
        return "ItemDetailDialog"

    def included_item_types(item_id):
        reward = random_rewards.get(item_id)
        if reward is None:
            return []
        selector = reward.get("RandomItemRangeSelector")
        if selector is not None:
            selector_type = selector.get("_type")
            if selector_type == "RandomItemRangeProfessionSkillSelector":
                return ([item_types["PassiveSkillPiece"], item_types["ActiveSkillPiece"]]
                        if selector["IsPiece"] else [item_types["ActiveSkill"], item_types["PassiveSkill"]])
            if selector_type == "RandomItemRangeTreasureGachaSelector":
                return [item_types["TreasurePiece"] if selector["IsPiece"] else item_types["Treasure"]]
            raise ValueError("Unsupported official random reward selector: " + str(selector_type))
        if reward.get("ItemTypes"):
            return list(reward["ItemTypes"])
        return [items[contained_id]["ItemType"] for contained_id in reward.get("IdList", [])
                if contained_id in items]

    def is_filtration_item(item):
        item_type = item["ItemType"]
        if item_type in (item_types["Money"], item_types["Prop"], item_types["StrengthenMat"]):
            return True
        if item_type == item_types["RandomReward"]:
            included = included_item_types(item["ClassId"])
            return item_types["Money"] in included or item_types["Prop"] in included
        return False

    detail_dialog_counts = Counter(detail_dialog_type(items[item_id]) for item_id in item_ids)
    expected_detail_dialog_counts = Counter({
        "EquipDetailDialog": 861,
        "CommonEquipPieceUseDetailDialog": 71,
        "CommonItemPieceUseDetailDialog": 1,
        "ItemDetailDialog": 231,
    })
    if detail_dialog_counts != expected_detail_dialog_counts:
        raise ValueError("Selected 884 daily reward detail-dialog types changed; inspect the official factory branches.")

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
    script_meta = script.with_name(script.name + ".meta")
    script_guid = next(line.split(": ", 1)[1] for line in script_meta.read_text().splitlines()
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
        item_type_name = item_type_names[row["ItemType"]]
        piece_merge_count = 0
        if row["ItemType"] == item_types["CommonEquipPiece"]:
            piece_merge_count = equip_piece_merges[item_id]["PieceMergeCount"]
        elif row["ItemType"] == item_types["CommonItemPiece"]:
            piece_merge_count = item_piece_merges[item_id]["PieceMergeCount"]
        description_key = "item_{}_desc".format(item_id)
        if row["ItemType"] in skill_piece_types:
            description_key = "item_desc_skillpiece"
        elif row["ItemType"] == item_types["TreasurePiece"]:
            description_key = "treasure_piece_desc"
        text += (f"  - itemId: {item_id}\n    quality: {row['Quality']}\n    itemType: {row['ItemType']}\n"
                 f"    itemTypeName: {item_type_name}\n"
                 f"    nameKey: item_{item_id}_name\n    typeKey: ItemType.{item_type_name}\n"
                 f"    descriptionKey: {description_key}\n"
                 f"    functionDescriptionKey: item_{item_id}_func_desc\n"
                 f"    pieceMergeCount: {piece_merge_count}\n"
                 f"    canOverlap: {int(row['CanOverlap'])}\n"
                 f"    showPlayerItemCount: {int(row['ShowPlayerItemCount'])}\n"
                 f"    detailDialogType: {detail_dialog_type(row)}\n"
                 f"    showPieceIcon: {int(row['ShowPieceIcon'])}\n"
                 f"    isEquipment: {int(row['ItemType'] in equip_types)}\n"
                 f"    isCommonEquipmentPiece: {int(row['ItemType'] == item_types['CommonEquipPiece'])}\n"
                 f"    isRandomReward: {int(row['ItemType'] == item_types['RandomReward'])}\n"
                 f"    isFiltrationItem: {int(is_filtration_item(row))}\n"
                 "    sprite: {fileID: 21300000, guid: " + sprite["guid"] + ", type: 3}\n")
    catalog.write_text(text, encoding="utf8", newline="\n")
    meta_path = catalog.with_name(catalog.name + ".meta")
    meta_path.write_text("fileFormatVersion: 2\nguid: " + digest(b"daily-dungeon-reward-catalog-884")[:32]
                        + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n", encoding="ascii")
    outputs.extend((catalog, meta_path))
    receipt = dict(manifest=apk.version, item_config=dict(source=apk.provenance(item_object), sha256=digest(item_raw)),
        settings_config=dict(source=apk.provenance(settings_object), sha256=digest(settings_raw)),
        random_reward_config=dict(source=apk.provenance(random_object), sha256=digest(random_raw)),
        equip_piece_merge_config=dict(source=apk.provenance(equip_piece_object), sha256=digest(equip_piece_raw)),
        item_piece_merge_config=dict(source=apk.provenance(item_piece_object), sha256=digest(item_piece_raw)),
        configs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in configs],
        operation="Resolve original ItemInfo.RawIcon; decode original texture, atlas rotation and tight mesh; restore sprite rect padding, pivot, border and pixels per unit; apply the selected ItemDetailDialogFactory branch order using ItemInfo and GameSettings; bind each reward and its original detail-dialog type explicitly by item id.",
        sprites=sprites, image_sources=list(cache.sources.values()), item_count=len(item_ids),
        code_sources=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in
                      (code / "common-source/Common/ItemInfo.cs", code / "common-source/Common/TbItemInfo.cs",
                       code / "common-source/Common/ItemType.cs",
                       code / "common-source/Common/ItemUseEffectType.cs",
                       code / "common-source/Common/RandomRewardInfoParser.cs",
                       code / "common-source/Common/RandomItemRangeProfessionSkillSelector.cs",
                       code / "common-source/Common/RandomItemRangeTreasureGachaSelector.cs",
                       code / "common-source/Common/EquipPieceMergeInfo.cs",
                       code / "common-source/Common/ItemPieceMergeInfo.cs",
                       code / "common-source/Common/LanguageUtils.cs",
                       code / "client-source/ItemDetailDialogFactory.cs",
                       code / "client-source/CommonEquipPieceUseDetailDialog.cs",
                       code / "client-source/CommonItemPieceUseDetailDialog.cs",
                       code / "client-source/DailyDungeonDialogAwardItemComparer.cs",
                       code / "client-source/DailyDungeonUtils.cs")],
        detail_dialog_types=dict(sorted(detail_dialog_counts.items())),
        tools=[dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=digest(Path(__file__).read_bytes())),
               dict(path=script.relative_to(ROOT).as_posix(), sha256=digest(script.read_bytes()),
                    meta_sha256=digest(script_meta.read_bytes()))],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in outputs])
    receipt_path = ROOT / "assets/reference/dungeon-ui/daily-dungeon-reward-icons-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print(json.dumps(dict(items=len(item_ids), sprites=len(sprites))))


if __name__ == "__main__":
    build()
