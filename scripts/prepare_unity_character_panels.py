"""Package the pinned original character panels for Unity runtime binding."""
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / "assets/official/ui.json"
UI_MANIFEST = ROOT / "assets/reference/official-city/source-manifest.json"
RECEIPT = ROOT / "assets/reference/character-overview-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Official/Character/character-panels.json"


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    manifest = json.loads(UI_MANIFEST.read_text(encoding="utf-8"))
    expected = manifest["data_sha256"]["ui.json"]
    if sha256(UI) != expected or manifest["manifest"] != "854_156383":
        raise ValueError("The official UI catalog does not match its pinned source manifest")

    screens = json.loads(UI.read_text(encoding="utf-8"))
    names = ("character", "character_props", "rank_upgrade", "character_prop_row", "character_prop_group", "item", "equipment", "equip_row", "equip_compare", "equip_detail", "equip_promote", "skills", "skill_list_item", "skill_promote")
    if any(name not in screens for name in names):
        raise ValueError("An original character panel is missing from the official UI catalog")
    from prepare_character_prop_bindings import BindingReader
    bindings = BindingReader().read(
        screens["character_props"]["source"],
        ("CharacterDetailPropsDialogOverallContent", "CharacterDetailPropsDialogDetailedContent"),
        screens["character_prop_group"]["source"],
        ("CharacterDetailPropDialogDetailPropItem",),
        screens["rank_upgrade"]["source"],
        ("CharacterUpgradePanel", "CharacterUpgradePanelStarItem"))
    preview_path = ROOT / "assets/official/appearance-chest-preview.json"
    preview_data = json.loads(preview_path.read_text(encoding="utf-8"))
    preview = preview_data.get("templates", {}).get("appearance_chest_preview")
    if not preview or preview.get("root", {}).get("name") != "PreviewAppearanceDialog":
        raise ValueError("The original PreviewAppearanceDialog template is missing or not pinned.")
    packaged_screens = {name: screens[name] for name in names}
    packaged_screens["appearance_preview"] = {"root": preview["root"]}
    profession_names = (
        "profession", "profession_detail", "profession_promotion", "profession_change",
        "profession_prop", "profession_skill", "profession_convert_prop", "profession_notify",
        "profession_skill_notify", "profession_level_rules", "profession_rule_row",
        "profession_overview_prop", "profession_promotion_prop", "profession_condition",
        "profession_tree_condition", "profession_notify_prop", "profession_player",
    )
    if any(name not in screens for name in profession_names):
        raise ValueError("An original profession panel is missing from the official UI catalog")
    packaged_screens.update({name: screens[name] for name in profession_names})
    profession_data = ROOT / "assets/official/professions.json"
    profession_payload = json.loads(profession_data.read_text(encoding="utf-8"))
    profession_source = json.loads((ROOT / "assets/reference/profession-ui/source-manifest.json").read_text(encoding="utf-8"))
    if profession_source.get("manifest") != "854_156383" or profession_source.get("outputs", {}).get("professions.json") != sha256(profession_data):
        raise ValueError("The original profession settings do not match their pinned source manifest")
    rules_path = ROOT / "assets/official/rules.json"
    rules_source_path = ROOT / "assets/reference/dungeons-runtime-source-manifest.json"
    rules_source = json.loads(rules_source_path.read_text(encoding="utf-8"))
    if rules_source.get("manifest") != "854_156383" or not any(row.get("path") == "assets/official/rules.json"
               and row.get("sha256") == sha256(rules_path) for row in rules_source.get("inputs", [])):
        raise ValueError("The profession rules do not match their pinned official data source")
    official_rules = json.loads(rules_path.read_text(encoding="utf-8"))
    achievements = [row for row in official_rules["achievement"]
                    if row.get("Group") == 10 and row.get("AchievementType") == 56]
    profession_rules = {
        "profession_base": official_rules["profession_base"],
        "profession_level_cost": official_rules["profession_level_cost"],
        "profession_achievement": achievements,
        "source_sha256": sha256(rules_path),
    }
    portrait_manifest_path = ROOT / "assets/reference/profession-ui/portraits-source-manifest.json"
    portrait_manifest = json.loads(portrait_manifest_path.read_text(encoding="utf-8"))
    if portrait_manifest.get("manifest") != "854_156383" or not portrait_manifest.get("profession_portraits"):
        raise ValueError("The pinned profession portrait extraction receipt is missing or invalid")
    for rows in portrait_manifest["profession_portraits"].values():
        for source_path in rows.values():
            filename = source_path.rsplit("/", 1)[-1]
            sprite_path = ROOT / "unity/Assets/Official/Character/Art" / source_path
            meta_path = sprite_path.with_suffix(sprite_path.suffix + ".meta")
            if not sprite_path.is_file() or sha256(sprite_path) != portrait_manifest["outputs"].get(
                    "unity/Assets/Official/Character/Art/" + source_path):
                raise ValueError("An extracted profession portrait differs from its pinned receipt: " + filename)
            if not meta_path.is_file():
                raise ValueError("A profession portrait Unity sprite meta file is missing: " + filename)
    payload = {
        "manifest": "854_156383",
        "ui_sha256": expected,
        "appearance_preview_sha256": sha256(preview_path),
        "profession_ui_sha256": sha256(profession_data),
        "profession": profession_payload,
        "profession_rules": profession_rules,
        "profession_portraits": portrait_manifest["profession_portraits"],
        "screens": packaged_screens,
        "bindings": bindings,
    }
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(encoded, encoding="utf-8", newline="")

    unity_source_manifest = OUTPUT.parent / "source-manifest.json"
    unity_receipt = json.loads(unity_source_manifest.read_text(encoding="utf-8"))
    unity_receipt["outputs"] = [
        row for row in unity_receipt["outputs"] if row["name"] != OUTPUT.name
    ]
    unity_receipt["outputs"].append({
        "name": OUTPUT.name,
        "bytes": OUTPUT.stat().st_size,
        "sha256": sha256(OUTPUT),
    })
    unity_receipt["outputs"].sort(key=lambda row: row["name"])
    unity_source_manifest.write_text(
        json.dumps(unity_receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )

    receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
    receipt["unity_character_panels"] = {
        "manifest": "854_156383",
        "input": "assets/official/ui.json",
        "input_sha256": expected,
        "screens": list(names) + ["appearance_preview"] + list(profession_names),
        "profession_settings_input": "assets/official/professions.json",
        "profession_settings_sha256": sha256(profession_data),
        "profession_rules_input": "assets/official/rules.json",
        "profession_rules_sha256": sha256(rules_path),
        "profession_portraits_input": "assets/reference/profession-ui/portraits-source-manifest.json",
        "profession_portraits_sha256": sha256(portrait_manifest_path),
        "profession_portrait_count": len(portrait_manifest["asset_guids"]),
        "profession_achievement_rows": len(achievements),
        "appearance_preview_input": "assets/official/appearance-chest-preview.json",
        "appearance_preview_input_sha256": sha256(preview_path),
        "output": "unity/Assets/Official/Character/character-panels.json",
        "output_sha256": sha256(OUTPUT),
    }
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Packaged original character panel layouts:", ", ".join(list(names) + ["appearance_preview"] + list(profession_names)))


if __name__ == "__main__":
    build()
