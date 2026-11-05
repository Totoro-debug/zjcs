"""Extract official Type 22 daily-dungeon achievement item sprites."""
from __future__ import annotations

import hashlib
import json
from io import BytesIO
from pathlib import Path
import uuid

from official_apk import Apk

ROOT = Path(__file__).resolve().parents[1]
VERSION = "884_156692"
HOME_RULES = ROOT / "unity/Assets/StreamingAssets/Home/rules.json"
ACHIEVEMENTS = ROOT / "unity/Assets/Game/Resources/Dungeons/daily-dungeon-achievements.json"
OUTPUT_DIR = ROOT / "unity/Assets/StreamingAssets/Home/sprites"
RECEIPT = ROOT / "assets/reference/daily-dungeon-achievement-sprites-source-manifest.json"

ITEMS = {
    102: ("item_102-cae011d41c.png", "defaultpackage_assets_image_itemicon_other_common_noatlas.bundle",
          "Assets/Image/ItemIcon/Other/Common_NoAtlas/item_102.png"),
    103: ("item_103-dd5ad0232c.png", "defaultpackage_assets_image_itemicon_other_common_noatlas.bundle",
          "Assets/Image/ItemIcon/Other/Common_NoAtlas/item_103.png"),
    1105: ("item_1105-eb4028384a.png", "defaultpackage_assets_image_itemicon_gift_gift_atlas.bundle",
           "Assets/Image/ItemIcon/Gift/Gift_Atlas/item_1105.png"),
    1115: ("item_1115-73a94e6ef4.png", "defaultpackage_assets_image_itemicon_gift_gift_atlas.bundle",
           "Assets/Image/ItemIcon/Gift/Gift_Atlas/item_1115.png"),
    1125: ("item_1125-077865581e.png", "defaultpackage_assets_image_itemicon_gift_gift_atlas.bundle",
           "Assets/Image/ItemIcon/Gift/Gift_Atlas/item_1125.png"),
    1134: ("item_1134-406c75a66c.png", "defaultpackage_assets_image_itemicon_gift_gift_atlas.bundle",
           "Assets/Image/ItemIcon/Gift/Gift_Atlas/item_1134.png"),
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_meta(guid: str) -> bytes:
    return (f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def write_verified(path: Path, data: bytes) -> None:
    if path.exists():
        if path.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed official dungeon reward asset: " + str(path))
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def build() -> dict:
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Daily-dungeon reward sprites require the selected manifest " + VERSION)

    home_rules_raw = HOME_RULES.read_bytes()
    home_rules = json.loads(home_rules_raw)
    if home_rules.get("version") != "854_156383":
        raise ValueError("Home item icon mapping is not from the expected rules manifest")
    item_rows = {int(row["id"]): row for row in home_rules["items"]}

    achievements_raw = ACHIEVEMENTS.read_bytes()
    achievements = json.loads(achievements_raw)
    if (achievements.get("Manifest") != "854_156383" or achievements.get("Group") != 39
            or achievements.get("AchievementType") != 22):
        raise ValueError("Daily-dungeon Type 22 reward catalog differs from its pinned source")
    referenced_items = {int(reward["ItemId"]) for row in achievements["Rows"]
                        for reward in row["Rewards"]}
    if not set(ITEMS).issubset(referenced_items):
        raise ValueError("A source sprite is not referenced by the official Type 22 reward catalog")

    outputs = []
    for item_id, (icon_file, bundle, source_path) in sorted(ITEMS.items()):
        rule = item_rows.get(item_id)
        if rule is None or rule.get("icon") != icon_file:
            raise ValueError("Home item rule does not bind the expected icon for item " + str(item_id))

        matches = [(path, pointer) for path, pointer in apk.container(bundle)
                   if path.lower() == source_path.lower() and pointer.type.name == "Sprite"]
        if len(matches) != 1:
            raise ValueError("Official Sprite is not uniquely resolved: " + source_path)
        path, pointer = matches[0]
        sprite = pointer.deref()
        if sprite.read().m_Name != Path(source_path).stem:
            raise ValueError("Official Sprite object name differs: " + source_path)
        image = sprite.read().image
        if image is None or image.width <= 0 or image.height <= 0:
            raise ValueError("Official Sprite has no decoded image: " + source_path)

        stream = BytesIO()
        image.save(stream, format="PNG", optimize=False)
        image_bytes = stream.getvalue()
        target = OUTPUT_DIR / icon_file
        write_verified(target, image_bytes)

        source = apk.provenance(sprite)
        if source.get("bundle") != bundle:
            raise ValueError("Resolved Sprite bundle differs from the pinned bundle: " + source_path)
        guid = uuid.uuid5(uuid.NAMESPACE_URL,
            "zjcs:daily-dungeon-achievement-sprite:" + str(item_id) + ":" + bundle + ":" + str(sprite.path_id)).hex
        metadata = target.with_name(target.name + ".meta")
        metadata_bytes = asset_meta(guid)
        write_verified(metadata, metadata_bytes)

        outputs.append({
            "itemId": item_id,
            "homeIconFile": icon_file,
            "sourcePath": path,
            "sourceObject": sprite.read().m_Name,
            "sourceObjectPathId": str(sprite.path_id),
            "sourceObjectSha256": sha256(sprite.get_raw_data()),
            "source": source,
            "conversion": "Decode the original Unity Sprite image to PNG without drawing or resizing.",
            "width": image.width,
            "height": image.height,
            "output": target.relative_to(ROOT).as_posix(),
            "outputBytes": len(image_bytes),
            "outputSha256": sha256(image_bytes),
            "meta": metadata.relative_to(ROOT).as_posix(),
            "metaGuid": guid,
            "metaSha256": sha256(metadata_bytes),
        })

    selector = ROOT / "official-source.json"
    receipt = {
        "manifest": apk.version,
        "selector": {"path": selector.relative_to(ROOT).as_posix(), "sha256": sha256(selector.read_bytes())},
        "generator": {
            "path": Path(__file__).resolve().relative_to(ROOT).as_posix(),
            "sha256": sha256(Path(__file__).read_bytes()),
        },
        "rulesBinding": {
            "path": HOME_RULES.relative_to(ROOT).as_posix(),
            "version": home_rules["version"],
            "sha256": sha256(home_rules_raw),
            "itemIcons": [{"itemId": item_id, "file": ITEMS[item_id][0]}
                          for item_id in sorted(ITEMS)],
        },
        "achievementCatalog": {
            "path": ACHIEVEMENTS.relative_to(ROOT).as_posix(),
            "sha256": sha256(achievements_raw),
            "group": achievements["Group"],
            "type": achievements["AchievementType"],
        },
        "operation": "Resolve each exact original Sprite through the selected APK first and verified same-version CDN cache second; export UnityPy's decoded Sprite image as PNG.",
        "outputs": outputs,
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
