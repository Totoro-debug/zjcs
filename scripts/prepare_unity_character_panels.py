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
    names = ("character", "character_props", "rank_upgrade", "character_prop_row", "character_prop_group", "item", "equipment", "equip_row")
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
    payload = {
        "manifest": "854_156383",
        "ui_sha256": expected,
        "screens": {name: screens[name] for name in names},
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
        "screens": list(names),
        "output": "unity/Assets/Official/Character/character-panels.json",
        "output_sha256": sha256(OUTPUT),
    }
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Packaged original character panel layouts:", ", ".join(names))


if __name__ == "__main__":
    build()
