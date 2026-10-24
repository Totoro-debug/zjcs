"""Build the runtime commerce catalog from the pinned official payloads."""

import hashlib
import io
import json
import re
import shutil
import struct
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
import UnityPy
import xxhash

PAYLOAD = ROOT / "unity/Assets/Official/Commerce"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Commerce/commerce-runtime.json"
MANIFEST = PAYLOAD / "source-manifest.json"
CITY_MANIFEST = ROOT / "assets/reference/official-city/source-manifest.json"
AUDIO_MANIFEST = ROOT / "assets/reference/commerce-components/audio-source-manifest.json"
LEGACY_LANGUAGE = ROOT / ".tools/source-packages/history/854_156383/WwiseBnk/wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"
LEGACY_LANGUAGE_SHA256 = "738069eafacd7fe33aae970aa5edacd76f48ed02acdd07ededad1c4a499b93b8"
OFFICIAL_UI = ROOT / "assets/official/ui.json"
DIRECT_PAY_BUNDLE = ROOT / ".tools/source-packages/extracted/defaultpackage_assets_image_ui_pay_directpay.bundle"
DIRECT_PAY_BUNDLE_MD5 = "cc6a0b656b8c49c5204f21d4adfae36f"
VOUCHER_ICON_BUNDLE = ROOT / ".tools/source-packages/extracted/defaultpackage_assets_image_ui_pay_vouchers.bundle"
ACCUMULATED_IMAGE_BUNDLE = ROOT / ".tools/source-packages/extracted/defaultpackage_assets_image_ui_pay_accumulated.bundle"
ACCUMULATED_IMAGE_BUNDLE_MD5 = "e1ed8b9eb1cda15317920de572ee33fb"
ITEM_QUALITY_IMAGE_BUNDLE = ROOT / ".tools/source-packages/extracted/defaultpackage_assets_image_ui_itemquality.bundle"
ITEM_QUALITY_IMAGE_BUNDLE_MD5 = "74e6afc6b39b88d0e8977d8f12267b52"
ITEM_QUALITY_SPRITES = {
    "ImgQuality_White", "ImgQuality_Purple", "ImgQuality_Orange", "ImgQuality_Gold",
    "ImgQuality_Red", "ImgQuality_Multicolour",
}
ACCUMULATED_PANEL_BUNDLE = ROOT / ".tools/source-packages/cloud/854_156383/DefaultPackage/0b11bb5781ce56f124f55e08687be9d7.bundle"
ACCUMULATED_PANEL_BUNDLE_MD5 = "0b11bb5781ce56f124f55e08687be9d7"
ACCUMULATED_PANEL_CLIP_PATH_ID = -4611456846825482089
ACCUMULATED_QUALITY_CONFIG_BUNDLE = ROOT / ".tools/source-packages/cloud/854_156383/DefaultPackage/cdc659a0bec6070792727ca540729321.bundle"
ACCUMULATED_QUALITY_CONFIG_BUNDLE_MD5 = "cdc659a0bec6070792727ca540729321"
ACCUMULATED_QUALITY_CONFIG_PATH_ID = -6274676659179333590
ACCUMULATED_QUALITY_SPRITES = {
    3: ("ImgPurple_Big", "ImgPurple_Small"),
    4: ("ImgOrange_Big", "ImgOrange_Small"),
    5: ("ImgGold_Big", "ImgGold_Small"),
    6: ("ImgRed_Big", "ImgRed_Small"),
    7: ("ImgRainbow_Big", "ImgRainbow_Small"),
}
ACCUMULATED_QUALITY_CONFIG_GUIDS = {
    3: ("3a28354fac99b1a4da254979c67f28cf", "4680ee44d655ccf42be8d7db080c1f55"),
    4: ("fca414240b4aae348af2383c6cb53289", "4ae5e7903f0c5c24e959fb8e0da4a8a6"),
    5: ("077de3a22d861ff43a8a6539e01d3048", "ba3488d68a61336439beaea3300f84c0"),
    6: ("48733c1039c41234c9c3b34a466587eb", "6e39e837db27a614998bbe7b2e533133"),
    7: ("0826954923923f0498d03726c1b849bd", "cb3ac61203216f746b2627c84d111660"),
}
ACCUMULATED_LOCKED_GUID = "5e1fab7f6be569f40a278f93193048ef"
ACCUMULATED_LOCKED_SPRITE = "ImgStayTuned"
ACCUMULATED_EXISTING_SPRITES = {
    "ImgPurple_Small": "ImgPurple_Small-8d7445071c.png",
    "ImgRed_Big": "ImgRed_Big-c44276f3f3.png",
}
VOUCHER_ICON_BUNDLE_MD5 = "18bdd7ad9506362e87e6b33dc0823d44"
DIRECT_ICON_SHA256 = {
    "ImgIcon_DirectPayIcon_1": "5ac198062f640992397fd4fbbd1560ad8ca7ef0592b41d6ae335a919eb8459c3",
    "ImgIcon_DirectPayIcon_2": "50e59d13c05f293dc5c3908ad01031ddf938ea5069beb3ef6c777900c24bdc2c",
    "ImgIcon_DirectPayIcon_3": "8d580aaa6c11da277e736d06d4c23b3864085b456761ee93138569333ba35bd2",
    "ImgIcon_DirectPayIcon_4": "c4e2d2bb82ae9368497d37770839464f3f6a31d2a983823f3aa603c01996efa3",
    "ImgIcon_DirectPayIcon_5": "b06e77938cec3f0383528123e4f7c0506d5d44a45b12781095c842f8d7b41d8a",
    "ImgIcon_DirectPayIcon_6": "e059d09b94d9ef4ad444f3b498b2715275f08b7426e511624d07a81a7e1f4e6a",
}
VOUCHER_ICON_SHA256 = {
    "ImgIcon_Vouchers_1": "dabcc4315e4b2294d0df7430727e8f4356c6d78142927f6b942fca2cb324045b",
    "ImgIcon_Vouchers_2": "589dc3de62eda4dab318ca7365f1902c948e96df7ee82bf6e8c7a9d3c33a759f",
    "ImgIcon_Vouchers_3": "51be9bc489a686467911fd77d99f3c370d7b944a171e96a6cb0eb0142a80c138",
    "ImgIcon_Vouchers_4": "fb7f9716a1b16198c10d5e9b461749be37ef7329092693cbb125b6e96cfc6875",
    "ImgIcon_Vouchers_5": "ea2659ced92de3f4197658b3b8b267aa5def7add960fe6dd3e298d1a5281997a",
    "ImgIcon_Vouchers_6": "c70cede21dde8f548c2c8c9c6605ce1e96fb7bf563bdad35fd3904d6be054c8b",
}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rewards(values):
    return [
        {"itemId": int(item_id), "count": int(count)}
        for item_id, count in sorted(values.items(), key=lambda entry: int(entry[0]))
    ]


def flatten_tree(root):
    """Flatten a prefab tree for JsonUtility, which cannot parse deep UI hierarchies."""
    rows = []

    def visit(node, parent_index):
        children = node.get("children") or []
        row = {key: value for key, value in node.items() if key != "children"}
        row["parentIndex"] = parent_index
        # JsonUtility instantiates missing [Serializable] members instead of leaving them
        # null, so record optional-component presence explicitly.
        row["has_image"] = node.get("image") is not None
        row["has_text"] = node.get("text") is not None
        row["has_scroll"] = node.get("scroll") is not None
        row["has_layout"] = node.get("layout") is not None
        row["has_grid"] = node.get("grid") is not None
        row["has_size_fitter"] = node.get("size_fitter") is not None
        row["has_layout_element"] = node.get("layout_element") is not None
        row["has_aspect_fitter"] = node.get("aspect_fitter") is not None
        restore_animation_events(row)
        if row.get("animations"):
            row["animations"] = [normalize_animation(animation)
                                  if animation.get("name") == "AccumulatedPayCoreMulltiAwardGo_Loop"
                                  else animation for animation in row["animations"]]
        index = len(rows)
        rows.append(row)
        for child in children:
            visit(child, index)

    visit(root, -1)
    return rows


def normalize_animation(animation):
    result = dict(animation)
    result["tracks"] = []
    for track in animation.get("tracks") or []:
        normalized = dict(track)
        normalized["keys"] = [
            {"time": float(key[0]), "value": float(key[1]),
             "inTangent": float(key[2] or 0), "outTangent": float(key[3] or 0)}
            for key in track.get("keys") or []
        ]
        result["tracks"].append(normalized)
    return result


def restore_animation_events(node):
    for animation in node.get("animations") or []:
        if animation.get("name") != "AccumulatedPayCoreMulltiAwardGo_Loop":
            continue
        source = animation.get("source") or {}
        manifest = source.get("manifest")
        bundle_hash = source.get("bundle_hash")
        path_id = int(source.get("path_id", 0))
        if not manifest or not bundle_hash or not path_id:
            continue
        bundle = ROOT / ".tools/source-packages/cloud" / manifest / "DefaultPackage" / (bundle_hash + ".bundle")
        if not bundle.is_file():
            raise FileNotFoundError(f"Official commerce animation source is missing: {bundle}")
        raw_sha256 = sha256(bundle)
        expected_sha256 = source.get("bundle_sha256")
        if expected_sha256 and raw_sha256 != expected_sha256:
            raise ValueError(f"Official commerce animation bundle hash mismatch: {bundle}")
        environment = UnityPy.load(str(bundle))
        clip_object = next((obj for obj in environment.objects
                            if obj.type.name == "AnimationClip" and obj.path_id == path_id), None)
        if clip_object is None:
            raise ValueError(f"Official commerce AnimationClip is missing: {bundle_hash}/{path_id}")
        clip = clip_object.read()
        events = [{"time": float(event.time), "functionName": event.functionName}
                  for event in clip.m_Events or []]
        animation["events"] = events
        animation["duration"] = max(float(animation.get("duration", 0)),
                                    max((event["time"] for event in events), default=0))


def combined_rewards(*sources):
    result = {}
    for source in sources:
        for item_id, count in source.items():
            key = str(item_id)
            result[key] = result.get(key, 0) + count
    return result


def checked_payload(path, expected):
    actual = sha256(path)
    if actual != expected:
        raise ValueError(f"Official commerce source hash mismatch: {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def official_localized_texts(keys):
    payload = LEGACY_LANGUAGE.read_bytes()
    if sha256(LEGACY_LANGUAGE) != LEGACY_LANGUAGE_SHA256:
        raise ValueError("The pinned 854 Chinese localization payload changed")
    count = struct.unpack_from("<I", payload)[0]
    offsets = {struct.unpack_from("<Q", payload, 4 + index * 12)[0]:
               struct.unpack_from("<i", payload, 12 + index * 12)[0] for index in range(count)}
    start = 4 + count * 12
    result = {}
    for key in keys:
        offset = offsets.get(xxhash.xxh64(key.encode("utf-8")).intdigest())
        if offset is None:
            raise ValueError(f"Missing official Chinese localization text: {key}")
        text_offset = start + offset
        text_length = struct.unpack_from("<H", payload, text_offset)[0]
        result[key] = payload[text_offset + 2:text_offset + 2 + text_length].decode("utf-8")
    return result


def extract_accumulated_quality_sprites():
    if not ACCUMULATED_IMAGE_BUNDLE.is_file():
        raise FileNotFoundError(f"Official accumulated-pay image bundle is missing: {ACCUMULATED_IMAGE_BUNDLE}")
    bundle_bytes = ACCUMULATED_IMAGE_BUNDLE.read_bytes()
    if hashlib.md5(bundle_bytes).hexdigest() != ACCUMULATED_IMAGE_BUNDLE_MD5:
        raise ValueError("The pinned APK accumulated-pay image bundle is missing or has changed")
    environment = UnityPy.load(str(ACCUMULATED_IMAGE_BUNDLE))
    wanted = {name for pair in ACCUMULATED_QUALITY_SPRITES.values() for name in pair}
    wanted.add(ACCUMULATED_LOCKED_SPRITE)
    sprites = {}
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        if sprite.m_Name in wanted:
            buffer = io.BytesIO()
            sprite.image.save(buffer, format="PNG")
            payload = buffer.getvalue()
            digest = hashlib.sha256(payload).hexdigest()
            sprite_root = PAYLOAD / "Art/sprites"
            existing_name = ACCUMULATED_EXISTING_SPRITES.get(sprite.m_Name)
            existing = sprite_root / existing_name if existing_name else None
            target = existing if existing is not None and existing.is_file() \
                and existing.read_bytes() == payload else sprite_root / f"{sprite.m_Name}-{digest[:10]}.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != payload:
                raise ValueError(f"Refusing to replace a changed official commerce Sprite: {target}")
            target.write_bytes(payload)
            write_sprite_meta(target)
            sprites[sprite.m_Name] = {
                "path": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
                "sha256": digest,
                "bytes": len(payload),
                "pathId": str(obj.path_id),
            }
    if set(sprites) != wanted:
        raise ValueError("The pinned accumulated-pay atlas is missing sprites: "
                         + ", ".join(sorted(wanted.difference(sprites))))
    return sprites


def extract_item_quality_sprites():
    if not ITEM_QUALITY_IMAGE_BUNDLE.is_file():
        raise FileNotFoundError(f"Official item quality image bundle is missing: {ITEM_QUALITY_IMAGE_BUNDLE}")
    if hashlib.md5(ITEM_QUALITY_IMAGE_BUNDLE.read_bytes()).hexdigest() != ITEM_QUALITY_IMAGE_BUNDLE_MD5:
        raise ValueError("The pinned item-quality image bundle is missing or has changed")
    environment = UnityPy.load(str(ITEM_QUALITY_IMAGE_BUNDLE))
    sprites = {}
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        if sprite.m_Name not in ITEM_QUALITY_SPRITES:
            continue
        buffer = io.BytesIO()
        sprite.image.save(buffer, format="PNG")
        payload = buffer.getvalue()
        digest = hashlib.sha256(payload).hexdigest()
        target = PAYLOAD / "Art/sprites" / f"{sprite.m_Name}-{digest[:10]}.png"
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != payload:
            raise ValueError(f"Refusing to replace a changed official item-quality Sprite: {target}")
        target.write_bytes(payload)
        write_sprite_meta(target)
        sprites[sprite.m_Name] = {
            "path": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
            "sha256": digest,
            "bytes": len(payload),
            "pathId": str(obj.path_id),
        }
    if set(sprites) != ITEM_QUALITY_SPRITES:
        raise ValueError("The pinned item-quality atlas is missing sprites: "
                         + ", ".join(sorted(ITEM_QUALITY_SPRITES.difference(sprites))))
    return sprites


def accumulated_panel_animation_source():
    if not ACCUMULATED_PANEL_BUNDLE.is_file():
        raise FileNotFoundError(f"Official accumulated-pay prefab bundle is missing: {ACCUMULATED_PANEL_BUNDLE}")
    bundle_bytes = ACCUMULATED_PANEL_BUNDLE.read_bytes()
    if hashlib.md5(bundle_bytes).hexdigest() != ACCUMULATED_PANEL_BUNDLE_MD5:
        raise ValueError("The pinned accumulated-pay prefab bundle is missing or has changed")
    environment = UnityPy.load(str(ACCUMULATED_PANEL_BUNDLE))
    clip = next((obj for obj in environment.objects
                 if obj.type.name == "AnimationClip" and obj.path_id == ACCUMULATED_PANEL_CLIP_PATH_ID), None)
    if clip is None or clip.read().m_Name != "AccumulatedPayPanel_Show":
        raise ValueError("The official accumulated-pay panel show AnimationClip is missing")
    return {"name": "AccumulatedPayPanel_Show", "pathId": str(clip.path_id),
            "bundle": ACCUMULATED_PANEL_BUNDLE.name,
            "bundleSha256": sha256(ACCUMULATED_PANEL_BUNDLE)}


def accumulated_quality_config_source():
    if not ACCUMULATED_QUALITY_CONFIG_BUNDLE.is_file():
        raise FileNotFoundError(f"Official accumulated-pay quality config bundle is missing: {ACCUMULATED_QUALITY_CONFIG_BUNDLE}")
    if hashlib.md5(ACCUMULATED_QUALITY_CONFIG_BUNDLE.read_bytes()).hexdigest() != ACCUMULATED_QUALITY_CONFIG_BUNDLE_MD5:
        raise ValueError("The pinned accumulated-pay quality config bundle is missing or has changed")
    environment = UnityPy.load(str(ACCUMULATED_QUALITY_CONFIG_BUNDLE))
    obj = next((obj for obj in environment.objects
                if obj.type.name == "MonoBehaviour" and obj.path_id == ACCUMULATED_QUALITY_CONFIG_PATH_ID), None)
    if obj is None:
        raise ValueError("The official AccumulatedPayAwardShowConfig object is missing")
    raw = obj.get_raw_data()
    if b"AccumulatedPayAwardShowConfig" not in raw:
        raise ValueError("The official accumulated quality config object name differs")
    qualities = sorted(ACCUMULATED_QUALITY_CONFIG_GUIDS)
    ordered_guids = ([ACCUMULATED_QUALITY_CONFIG_GUIDS[quality][0] for quality in qualities]
                     + [ACCUMULATED_QUALITY_CONFIG_GUIDS[quality][1] for quality in qualities]
                     + [ACCUMULATED_LOCKED_GUID])
    positions = [raw.find(guid.encode("ascii")) for guid in ordered_guids]
    if any(position < 0 for position in positions) or positions != sorted(positions):
        raise ValueError("The official accumulated quality GUID order differs from the pinned mapping")
    return {
        "bundle": ACCUMULATED_QUALITY_CONFIG_BUNDLE.name,
        "bundleSha256": sha256(ACCUMULATED_QUALITY_CONFIG_BUNDLE),
        "pathId": str(obj.path_id),
        "guidMapping": {str(quality): {"big": guids[0], "small": guids[1]}
                        for quality, guids in ACCUMULATED_QUALITY_CONFIG_GUIDS.items()},
        "lockedGuid": ACCUMULATED_LOCKED_GUID,
    }


def build():
    accumulated_sprites = extract_accumulated_quality_sprites()
    item_quality_sprites = extract_item_quality_sprites()
    accumulated_panel_animation = accumulated_panel_animation_source()
    accumulated_quality_config = accumulated_quality_config_source()
    source_manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    files = {Path(entry["path"]).name: entry["sha256"] for entry in source_manifest["files"]}
    rules_path = PAYLOAD / "rules.json"
    ui_path = PAYLOAD / "commerce-ui.json"
    rules = checked_payload(rules_path, files["rules.json"])
    ui = checked_payload(ui_path, files["commerce-ui.json"])
    official_ui = json.loads(OFFICIAL_UI.read_text(encoding="utf-8"))
    direct_item = official_ui.get("pay_item")
    if not isinstance(direct_item, dict) or direct_item.get("root", {}).get("name") != "PayDirectPayItem":
        raise ValueError("The pinned official PayDirectPayItem prefab is unavailable")
    direct_source = direct_item.get("source", {})
    if (direct_source.get("bundle") != "defaultpackage_assets_prefabs_ui_pay.bundle"
            or direct_source.get("bundle_hash") != "eb7ae25f9853eef383bea22906bd9cea"
            or direct_source.get("prefab") != "/PayDirectPayItem.prefab"):
        raise ValueError("The PayDirectPayItem source differs from the pinned APK prefab")
    shop = ui["prefabs"].get("commerce_main", {}).get("root")
    if shop is None:
        raise ValueError("The official shop activity panel is unavailable")
    direct_scroll = []
    pending = [shop]
    while pending:
        node = pending.pop()
        if "LoopVerticalScrollRectMulti" in node.get("scripts", []):
            direct_scroll.append(node)
        pending.extend(node.get("children", []))
    if len(direct_scroll) != 1:
        raise ValueError("The official direct-pay scroll binding is missing or ambiguous")
    direct_scroll[0]["scroll"] = {
        "horizontal": False, "vertical": True, "inertia": True,
        "movement": 2, "sensitivity": 1.0, "elasticity": 0.1, "deceleration": 0.135,
    }
    settings = json.loads((ROOT / ".tools/source-packages/official-data/settings.json").read_text(encoding="utf-8"))
    item_rows = rules["item"]
    names = {int(item["ClassId"]): item["Name"] for item in item_rows}
    qualities = {int(item["ClassId"]): int(item.get("Quality", 1)) for item in item_rows}
    item_icons_path = ROOT / "assets/official/icons.json"
    item_icons = json.loads(item_icons_path.read_text(encoding="utf-8"))
    localized = official_localized_texts(
        [f"accumulated_pay_desc_{level}" for level in range(1, 76)] + [
            "ui_accumulated_pay_active_progress", "ui_accumulated_pay_progress",
            "ui_accumulated_pay_locked_progress", "ui_accumulated_pay_active_require",
            "ui_accumulated_pay_lock_require",
            "DayCountdown", "DayCountdown2", "time_hour", "time_minute", "time_second", "time_end",
            "PropType.HangUpCartAllPercent", "PropType.AddFinallyAwardExpPercent",
        ])
    prop_names = {
        1034: localized["PropType.HangUpCartAllPercent"],
        6004: localized["PropType.AddFinallyAwardExpPercent"],
    }
    ui_texts = dict(ui["texts"])
    ui_texts.update({key: localized[key] for key in
                     ("DayCountdown", "DayCountdown2", "time_hour", "time_minute", "time_second", "time_end")})

    money_costs = []
    for row in rules["money_cost"]:
        money_costs.append({
            "id": row["Id"],
            "voucherCost": int(row["VoucherValue"].get("0", 0)),
            "currencyCost": float(row["CurrencyCost"].get("0", 0)),
            "startValue": float(row["StartValueGet"].get("0", 0)),
            "isShow": bool(row["IsShow"]),
            "productId": row["DefaultProductId"],
        })

    direct_pay = []
    for row in rules["direct_pay"]:
        direct_pay.append({
            "id": row["Id"], "moneyCostId": row["MoneyCostId"],
            "icon": row["Icon"], "titleKey": row["TitleKey"],
            "fixedAward": rewards(row["FixAward"]),
            "firstExtraAward": rewards(row["FirstExtraAward"]),
            "repeatExtraAward": rewards(row["NotFirstExtraAward"]),
            "repeatExtraPercent": row["NotFirstExtraAwardPercent"],
        })

    periodic_cards = []
    for row in rules["periodic_card_pay"]:
        periodic_cards.append({
            "id": row["Id"], "moneyCostId": row["MoneyCostId"],
            "durationDays": row["PeriodicTime"], "awardMultiple": row["AwardMultiple"],
            "fixedAward": rewards(row["FixAward"]), "periodicDuration": row["PeriodicDuration"],
            "periodicAward": rewards(row["PeriodicAward"]), "titleKey": row["TitleKey"],
            "discount": row["Discount"],
            "extraProps": [{"propId": int(key), "name": prop_names[int(key)], "value": int(value)}
                           for key, value in sorted((row.get("ExtraProps") or {}).items(), key=lambda entry: int(entry[0]))],
        })

    daily_groups = [
        {"groupId": row["GroupId"], "bundleMoneyCostId": row["GroupBuyMoneyCostId"],
         "greatValue": row["GreatValue"]}
        for row in ui["tables"]["daily_pay_gift_group"]
    ]
    daily_gifts = [
        {"groupId": row["GroupId"], "id": row["GiftId"], "moneyCostId": row["MoneyCostId"],
         "titleKey": f"daily_pay_gift_{row['GroupId']}_{row['GiftId']}",
         "limit": 1, "greatValue": row["GreatValue"], "rewards": rewards(row["Award"])}
        for row in rules["daily_pay_gift"]
    ]
    weekly_groups = [
        {"groupId": row["GroupId"], "seasonId": row["SeasonId"], "seasonStartDay": row["SeasonStartDay"]}
        for row in ui["tables"]["weekly_pay_gift_group"]
    ]
    weekly_gifts = []
    for row in rules["weekly_pay_gift"]:
        condition = row.get("Condition", {}).get("_type")
        if condition != "NoneCondition":
            raise ValueError(f"Unresolved official weekly-gift condition: {condition}")
        weekly_gifts.append({
            "groupId": row["GroupId"], "id": row["GiftId"], "moneyCostId": row["MoneyCostId"],
            "titleKey": f"weekly_pay_gift_{row['GroupId']}_{row['GiftId']}",
            "limit": row["BuyCountLimit"], "greatValue": row["GreatValue"],
            "rewards": rewards(row["Award"]),
        })

    vouchers = [
        {"id": row["Id"], "moneyCostId": row["MoneyCostId"], "icon": row["IconPath"],
         "titleKey": f"pay_voucher_{row['Id']}_title"}
        for row in ui["tables"]["pay_voucher"]
    ]
    first_awards = []
    for row in ui["tables"]["first_pay_award"]:
        item_rewards = []
        for item_id, count in sorted(row["FirstAwardItems"].items(), key=lambda entry: int(entry[0])):
            item = next(item for item in item_rows if item["ClassId"] == int(item_id))
            equip = next((entry for entry in rules["equip_type"] if entry["ItemType"] == item["ItemType"]), None)
            item_rewards.append({"itemId": int(item_id), "count": int(count),
                                 "professions": [] if equip is None else equip["Professions"]})
        additional = [{"itemId": item_id, "count": count}
                      for item_id, count in zip(row["OtherAwardItemId"], row["OtherAwardItemCount"])]
        first_awards.append({"day": row["Index"], "rewards": additional, "professionItems": item_rewards})
    accumulated = [
        {"level": row["Level"], "threshold": float(row["StartValueRequire"].get("0", 0)),
         "description": localized[f"accumulated_pay_desc_{row['Level']}"],
         "progressActiveFormat": localized["ui_accumulated_pay_active_progress"],
         "progressFormat": localized["ui_accumulated_pay_progress"],
         "progressLockedFormat": localized["ui_accumulated_pay_locked_progress"],
         "requireFormat": localized["ui_accumulated_pay_active_require"],
         "requireLocked": localized["ui_accumulated_pay_lock_require"],
         "isBig": row["IsBigLevel"], "group": row["Group"],
         "coreRewards": rewards(row["CoreAward"]), "subRewards": rewards(row["SubAwards"]),
         "rewards": rewards(combined_rewards(row["CoreAward"], row["SubAwards"]))}
        for row in ui["tables"]["accumulated_pay_award"]
    ]
    all_item_ids = {reward["itemId"] for rows in [daily_gifts, weekly_gifts, first_awards, accumulated]
                    for row in rows for reward in row["rewards"]}
    all_item_ids.update(reward["itemId"] for row in periodic_cards
                        for reward in row["fixedAward"] + row["periodicAward"])
    all_item_ids.update(item["itemId"] for row in first_awards for item in row["professionItems"])
    all_item_ids.update(item for row in direct_pay for item in
                        [award["itemId"] for award in row["fixedAward"] + row["firstExtraAward"] + row["repeatExtraAward"]])
    for item_id in all_item_ids:
        if str(item_id) not in item_icons:
            raise ValueError(f"Missing official item icon binding: {item_id}")
    for row in direct_pay:
        icon = row["icon"]
        matches = [path for path in (ROOT / "assets/official/sprites").glob(Path(icon).name + "-*.png")]
        if len(matches) != 1:
            raise ValueError(f"Official direct-pay icon is missing or ambiguous: {icon}")
        expected = DIRECT_ICON_SHA256.get(Path(icon).name)
        if expected is None or sha256(matches[0]) != expected:
            raise ValueError(f"Official direct-pay icon hash differs from its APK decode: {icon}")
    for row in vouchers:
        icon = Path(row["icon"]).name
        matches = [path for path in (ROOT / "assets/official/sprites").glob(icon + "-*.png")]
        expected = VOUCHER_ICON_SHA256.get(icon)
        if len(matches) != 1 or expected is None or sha256(matches[0]) != expected:
            raise ValueError(f"Official voucher icon is missing or differs from its APK decode: {icon}")
    actual_bundle_md5 = hashlib.md5(DIRECT_PAY_BUNDLE.read_bytes()).hexdigest()
    if actual_bundle_md5 != DIRECT_PAY_BUNDLE_MD5:
        raise ValueError("The pinned APK direct-pay image bundle is missing or has changed")
    actual_voucher_md5 = hashlib.md5(VOUCHER_ICON_BUNDLE.read_bytes()).hexdigest()
    if actual_voucher_md5 != VOUCHER_ICON_BUNDLE_MD5:
        raise ValueError("The pinned APK voucher image bundle is missing or has changed")
    audio_manifest = json.loads(AUDIO_MANIFEST.read_text(encoding="utf-8"))
    audio = [{"eventName": entry["event"], "file": Path(uri).name, "sha256": entry["sha256"]}
             for uri, entry in sorted(audio_manifest["resources"].items()) if uri.endswith(".ogg")]
    if len(audio) != 10:
        raise ValueError(f"Expected 10 original commerce audio events, found {len(audio)}")
    return {
        "manifest": source_manifest["manifest"],
        "sourceHashes": {"rulesJson": files["rules.json"], "commerceUiJson": files["commerce-ui.json"],
                         "officialUiJson": sha256(OFFICIAL_UI),
                         "settingsJson": sha256(ROOT / ".tools/source-packages/official-data/settings.json"),
                         "itemIconsJson": sha256(item_icons_path),
                         "accumulatedDescriptionsRawfile": LEGACY_LANGUAGE_SHA256,
                         "accumulatedQualityBundle": sha256(ACCUMULATED_IMAGE_BUNDLE),
                         "itemQualityBundle": sha256(ITEM_QUALITY_IMAGE_BUNDLE),
                         "accumulatedQualityConfigBundle": accumulated_quality_config["bundleSha256"],
                         "accumulatedPanelAnimationBundle": accumulated_panel_animation["bundleSha256"]},
        "accumulatedQualityFrames": [
            {"quality": quality,
             "big": Path(accumulated_sprites[big]["path"]).name,
             "small": Path(accumulated_sprites[small]["path"]).name}
            for quality, (big, small) in sorted(ACCUMULATED_QUALITY_SPRITES.items())],
        "accumulatedLockedSprite": Path(
            accumulated_sprites[ACCUMULATED_LOCKED_SPRITE]["path"]).name,
        "accumulatedQualitySource": accumulated_sprites,
        "itemQualitySpriteSource": item_quality_sprites,
        "accumulatedQualityConfig": accumulated_quality_config,
        "accumulatedPanelAnimation": accumulated_panel_animation,
        "settings": {"voucherItemId": settings["Pay"]["VoucherId"],
                     "flareStarItemId": settings.get("FlareStarId", 0),
                     "diamondItemId": settings.get("DiamonId", 0),
                     "exchangeMin": settings["Pay"]["DiamonChangeMin"],
                     "exchangeMax": settings["Pay"]["DiamonChangeMax"],
                     "exchangeScale": settings["Pay"]["DiamonChangeScale"],
                     "firstPurchaseMoneyCostId": settings["FirstPaySetting"]["MoneyCostId"],
                     "periodicCardRemindRemainDay": settings["Pay"]["PeriodicCardRemindRemainDay"],
                     "dailyFreeRewards": rewards(settings["PeriodicPayGiftSetting"]["DailyFreeAward"])},
        "items": [{"id": item_id, "name": names.get(item_id, f"道具{item_id}"),
                   "quality": qualities.get(item_id, 1),
                   "icon": item_icons[str(item_id)]["file"]} for item_id in sorted(all_item_ids)],
        "texts": [{"key": key, "value": value} for key, value in sorted(ui_texts.items())
                   if isinstance(value, str)],
        "prefabs": ([{"key": key, "nodes": flatten_tree(value["root"])} for key, value in ui["prefabs"].items()]
                    + [{"key": "commerce_direct_item", "nodes": flatten_tree(direct_item["root"])}]),
        "moneyCosts": money_costs, "directPay": direct_pay, "periodicCards": periodic_cards,
        "dailyGroups": daily_groups, "dailyGifts": daily_gifts,
        "weeklyGroups": weekly_groups, "weeklyGifts": weekly_gifts,
        "vouchers": vouchers, "firstAwards": first_awards, "accumulatedAwards": accumulated,
        "rules": [{"key": key, "title": value.get("title", ""), "rows": value.get("rows", [])}
                  for key, value in sorted(ui["rules"].items())],
        "audio": audio,
    }


def restore_runtime_assets(data):
    city = json.loads(CITY_MANIFEST.read_text(encoding="utf-8"))
    audio_manifest = json.loads(AUDIO_MANIFEST.read_text(encoding="utf-8"))
    source_icons = json.loads((ROOT / "assets/official/icons.json").read_text(encoding="utf-8"))
    apk_sha256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
    direct_bundle = {
        "bundle": "defaultpackage_assets_image_ui_pay_directpay.bundle",
        "bundle_hash": DIRECT_PAY_BUNDLE_MD5,
        "origin": "apk_zip",
        "apk_sha256": apk_sha256,
    }
    voucher_bundle = {
        "bundle": "defaultpackage_assets_image_ui_pay_vouchers.bundle",
        "bundle_hash": VOUCHER_ICON_BUNDLE_MD5,
        "origin": "apk_zip",
        "apk_sha256": apk_sha256,
    }
    records = {}

    for item in data["items"]:
        uri = source_icons[str(item["id"])]["file"]
        source_record = city["resources"].get(uri)
        if source_record is None:
            raise ValueError(f"Official item icon source is not registered: {uri}")
        source = ROOT / uri.removeprefix("res://")
        if sha256(source) != source_record["sha256"]:
            raise ValueError(f"Official item icon hash mismatch: {source}")
        target = PAYLOAD / "Art/items" / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and sha256(target) != source_record["sha256"]:
            raise ValueError(f"Conflicting commerce icon output: {target}")
        if not target.exists():
            shutil.copyfile(source, target)
        records[uri] = {"output": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
                        "sha256": source_record["sha256"], "source": source_record}

    for row in data["directPay"]:
        source = next((ROOT / "assets/official/sprites").glob(Path(row["icon"]).name + "-*.png"))
        target = PAYLOAD / "Art/sprites" / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        expected = sha256(source)
        if target.exists() and sha256(target) != expected:
            raise ValueError(f"Conflicting direct-pay icon output: {target}")
        if not target.exists():
            shutil.copyfile(source, target)
        write_sprite_meta(target)
        if sha256(target) != expected:
            raise ValueError(f"Restored direct-pay icon hash mismatch: {target}")
        records["apk://" + row["icon"]] = {
            "output": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
            "sha256": expected,
            "source": {**direct_bundle, "sprite": row["icon"], "source_path": str(source.relative_to(ROOT)).replace("\\", "/"),
                       "decoded_png_sha256": expected},
        }

    for row in data["vouchers"]:
        icon = Path(row["icon"]).name
        source = next((ROOT / "assets/official/sprites").glob(icon + "-*.png"))
        target = PAYLOAD / "Art/sprites" / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        expected = sha256(source)
        if target.exists() and sha256(target) != expected:
            raise ValueError(f"Conflicting voucher icon output: {target}")
        if not target.exists():
            shutil.copyfile(source, target)
        write_sprite_meta(target)
        if sha256(target) != expected:
            raise ValueError(f"Restored voucher icon hash mismatch: {target}")
        records["apk://" + row["icon"]] = {
            "output": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
            "sha256": expected,
            "source": {**voucher_bundle, "sprite": icon, "source_path": str(source.relative_to(ROOT)).replace("\\", "/"),
                       "decoded_png_sha256": expected},
        }

    # Include every sprite the official prefab trees reference, including the
    # standalone PayDirectPayItem prefab recovered from the pinned APK.
    prefab_sprites = set()
    for prefab in data["prefabs"]:
        for node in prefab["nodes"]:
            sprite = ((node.get("image") or {}).get("sprite") or {}).get("file")
            if sprite:
                prefab_sprites.add(Path(sprite).name)
    for name in sorted(prefab_sprites):
        uri = "res://assets/official/sprites/" + name
        source = ROOT / "assets/official/sprites" / name
        source_record = city["resources"].get(uri)
        if source_record is None or not source.is_file():
            raise FileNotFoundError(f"Official commerce prefab sprite is not registered: {uri}")
        if sha256(source) != source_record["sha256"]:
            raise ValueError(f"Official commerce prefab sprite hash mismatch: {uri}")
        target = PAYLOAD / "Art/sprites" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and sha256(target) != source_record["sha256"]:
            raise ValueError(f"Conflicting commerce prefab sprite output: {target}")
        if not target.exists():
            shutil.copyfile(source, target)
        write_sprite_meta(target)
        if sha256(target) != source_record["sha256"]:
            raise ValueError(f"Restored commerce prefab sprite hash mismatch: {target}")
        records[uri] = {"output": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
                        "sha256": source_record["sha256"], "source": source_record}

    font_uri = "res://assets/official/FZY4K.ttf"
    font_record = city["resources"].get(font_uri)
    if font_record is None:
        raise ValueError("The original FZY4K font source is not registered")
    font_source = ROOT / font_uri.removeprefix("res://")
    if sha256(font_source) != font_record["sha256"] or font_source.read_bytes()[:4] != b"\x00\x01\x00\x00":
        raise ValueError("The official FZY4K TTF failed its source or signature check")
    font_target = PAYLOAD / "Art/fonts/FZY4K.ttf"
    font_target.parent.mkdir(parents=True, exist_ok=True)
    if not font_target.exists():
        shutil.copyfile(font_source, font_target)
    if sha256(font_target) != font_record["sha256"]:
        raise ValueError("The restored FZY4K font hash is incorrect")
    records[font_uri] = {"output": str(font_target.relative_to(ROOT / "unity")).replace("\\", "/"),
                         "sha256": font_record["sha256"], "source": font_record}

    for uri, source_record in sorted(audio_manifest["resources"].items()):
        if not uri.endswith(".ogg"):
            continue
        source = ROOT / uri.removeprefix("res://")
        if sha256(source) != source_record["sha256"]:
            raise ValueError(f"Official commerce audio hash mismatch: {source}")
        target = PAYLOAD / "Art/audio" / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            shutil.copyfile(source, target)
        if sha256(target) != source_record["sha256"]:
            raise ValueError(f"Restored commerce audio hash mismatch: {target}")
        records[uri] = {"output": str(target.relative_to(ROOT / "unity")).replace("\\", "/"),
                        "sha256": source_record["sha256"], "source": source_record}

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    sprite_files = {entry["path"]: entry for entry in manifest.get("files", [])}
    for sprite_name, existing_name in ACCUMULATED_EXISTING_SPRITES.items():
        for path in list(sprite_files):
            filename = Path(path).name
            if filename.startswith(sprite_name + "-") and filename.endswith(".png") and filename != existing_name:
                del sprite_files[path]
    for sprite in data["accumulatedQualitySource"].values():
        sprite_files[sprite["path"]] = {"path": sprite["path"], "sha256": sprite["sha256"],
                                         "bytes": sprite["bytes"]}
    for sprite in data["itemQualitySpriteSource"].values():
        sprite_files[sprite["path"]] = {"path": sprite["path"], "sha256": sprite["sha256"],
                                         "bytes": sprite["bytes"]}
    manifest["files"] = sorted(sprite_files.values(), key=lambda entry: entry["path"])
    for entry in manifest["files"]:
        payload = ROOT / "unity" / entry["path"]
        if payload.is_file() and sha256(payload) == entry["sha256"]:
            entry["bytes"] = payload.stat().st_size
    manifest.setdefault("runtime_resources", {}).update(records)
    manifest["first_preview_source_manifest"] = {
        "path": "assets/reference/commerce-components/first-preview-source-manifest.json",
        "sha256": sha256(ROOT / "assets/reference/commerce-components/first-preview-source-manifest.json"),
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    root_path = ROOT / "assets/reference/commerce-ui/source-manifest.json"
    root_manifest = json.loads(root_path.read_text(encoding="utf-8"))
    root_manifest.setdefault("unity_runtime_resources", {}).update({
        uri: {"path": row["output"], "sha256": row["sha256"]} for uri, row in records.items()
    })
    root_manifest.setdefault("runtime_direct_pay_icons", {}).update({
        row["icon"]: {
            "path": records["apk://" + row["icon"]]["output"],
            "sha256": records["apk://" + row["icon"]]["sha256"],
            "source": records["apk://" + row["icon"]]["source"],
        } for row in data["directPay"]
    })
    root_manifest.setdefault("runtime_voucher_icons", {}).update({
        Path(row["icon"]).name: {
            "path": records["apk://" + row["icon"]]["output"],
            "sha256": records["apk://" + row["icon"]]["sha256"],
            "source": records["apk://" + row["icon"]]["source"],
        } for row in data["vouchers"]
    })
    root_manifest.setdefault("runtime_panel_sources", {})["commerce_direct_item"] = {
        "bundle": "defaultpackage_assets_prefabs_ui_pay.bundle",
        "bundle_hash": "eb7ae25f9853eef383bea22906bd9cea",
        "prefab": "/PayDirectPayItem.prefab",
        "path_id": "-5281991176679043744",
        "origin": "apk_zip",
        "apk_sha256": "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9",
        "ui_json_sha256": sha256(OFFICIAL_UI),
    }
    root_manifest["accumulated_quality_frames"] = {
        "bundle": "defaultpackage_assets_image_ui_pay_accumulated.bundle",
        "bundle_md5": ACCUMULATED_IMAGE_BUNDLE_MD5,
        "bundle_sha256": sha256(ACCUMULATED_IMAGE_BUNDLE),
        "config_bundle": data["accumulatedQualityConfig"]["bundle"],
        "config_bundle_md5": ACCUMULATED_QUALITY_CONFIG_BUNDLE_MD5,
        "config_bundle_sha256": data["accumulatedQualityConfig"]["bundleSha256"],
        "config_path_id": data["accumulatedQualityConfig"]["pathId"],
        "operation": "Official Unity Sprite decode from the pinned accumulated-pay atlas",
        "quality_mapping": {str(quality): {"big": big, "small": small}
                             for quality, (big, small) in ACCUMULATED_QUALITY_SPRITES.items()},
        "quality_guid_mapping": data["accumulatedQualityConfig"]["guidMapping"],
        "locked": ACCUMULATED_LOCKED_SPRITE,
        "locked_guid": data["accumulatedQualityConfig"]["lockedGuid"],
        "sprites": data["accumulatedQualitySource"],
    }
    root_manifest["accumulated_panel_animation"] = {
        **data["accumulatedPanelAnimation"],
        "output": "unity/Assets/Official/Commerce/Art/animations/AccumulatedPayPanel_Show.anim",
        "operation": "Unity Editor native bundle deserialization; Legacy clip for Animation playback",
    }
    root_manifest["commerce_item_quality_sprites"] = {
        "bundle": "defaultpackage_assets_image_ui_itemquality.bundle",
        "bundle_md5": ITEM_QUALITY_IMAGE_BUNDLE_MD5,
        "bundle_sha256": sha256(ITEM_QUALITY_IMAGE_BUNDLE),
        "operation": "Official Unity Sprite decode from the pinned ItemQuality atlas",
        "quality_mapping": {"1": "ImgQuality_White", "2": "ImgQuality_Blue",
                             "3": "ImgQuality_Purple", "4": "ImgQuality_Orange",
                             "5": "ImgQuality_Gold", "6": "ImgQuality_Red",
                             "7": "ImgQuality_Multicolour"},
        "sprites": data["itemQualitySpriteSource"],
    }
    root_manifest["unity_catalog_sha256"] = sha256(OUTPUT)
    root_path.write_text(json.dumps(root_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_meta(path):
    meta = path.with_suffix(path.suffix + ".meta")
    if not meta.exists():
        guid = hashlib.md5(str(path.relative_to(ROOT / "unity")).replace("\\", "/").encode()).hexdigest()
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")


def write_sprite_meta(path):
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    template = PAYLOAD / "Art/sprites/AccumulatedPay-cd53633163.png.meta"
    if not template.exists():
        raise FileNotFoundError(f"Unity sprite importer metadata template is missing: {template}")
    guid = hashlib.md5(str(path.relative_to(ROOT / "unity")).replace("\\", "/").encode()).hexdigest()
    source = template.read_text(encoding="utf-8")
    meta.write_text(re.sub(r"(?m)^guid: [0-9a-f]{32}$", "guid: " + guid, source, count=1), encoding="utf-8")


if __name__ == "__main__":
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    data = build()
    OUTPUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    write_meta(OUTPUT)
    restore_runtime_assets(data)
    print(f"Wrote {OUTPUT.relative_to(ROOT)} ({OUTPUT.stat().st_size} bytes)")
