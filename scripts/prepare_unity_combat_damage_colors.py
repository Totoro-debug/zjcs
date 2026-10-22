"""Export the official battle-HUD damage number colours for the combat float labels.

Runtime data only: the numbers themselves come from the battle rules. The official float prefabs
(TextMesh + AnimationClip) are not migrated yet, so the battle scene keeps the official font,
colours and spacing while using a local rise/fade motion.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/official/battle-hud/rules.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Combat/damage-colors.json"


def text_color(entry: dict) -> list[float]:
    value = entry["TextColor"]
    return [round(float(value[channel]), 6) for channel in ("r", "g", "b", "a")]


def build() -> dict:
    data = json.loads(SOURCE.read_text(encoding="utf-8"))["damage_settings"]
    elements = []
    for row in data["ElementDamageColors"]:
        variants = {}
        for name in ("Normal", "Crit", "Block", "Immunity"):
            if name in row:
                variants[name.lower()] = text_color(row[name])
        elements.append({
            "element": int(row["ElementType"]),
            "normal": variants.get("normal", [1, 1, 1, 1]),
            "crit": variants.get("crit", [1, 1, 1, 1]),
            "block": variants.get("block", [1, 1, 1, 1]),
            "immunity": variants.get("immunity", [1, 1, 1, 1]),
        })
    elements.sort(key=lambda item: item["element"])
    return {
        "elements": elements,
        "cure": text_color(data["ElementCureColor"]),
        "skillCost": text_color(data["SkillCostDamageColor"]),
        "statusDamage": text_color(data["StatusDamageColor"]),
        "statusCure": text_color(data["StatusCureColor"]),
        "defaultColor": text_color(data["DefaultColor"]),
        "spacing": [round(float(data["DamageUISpacing"][axis]), 6) for axis in ("x", "y", "z")],
    }


def meta_guid(path: Path) -> str:
    return hashlib.md5(("zjcs-official:" + path.relative_to(ROOT).as_posix()).encode()).hexdigest()


def main() -> None:
    payload = build()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    meta = Path(str(OUTPUT) + ".meta")
    if not meta.exists():
        meta.write_text(
            "fileFormatVersion: 2\nguid: " + meta_guid(OUTPUT) + "\nTextScriptImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print("damage colours:", len(payload["elements"]), "elements")
    print("output:", OUTPUT.relative_to(ROOT).as_posix(), hashlib.sha256(OUTPUT.read_bytes()).hexdigest())


if __name__ == "__main__":
    main()
