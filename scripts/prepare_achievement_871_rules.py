"""Replace achievement, grouping, and score reward tables from pinned 871 resources."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors


RULES = ROOT / "assets/official/rules.json"
RECEIPT = ROOT / "assets/reference/achievements/achievement-data-source-manifest.json"
BUNDLE = "defaultpackage_assets_config_luban.bundle"
TABLES = ("achievement", "achievement_group", "gacha", "gacha_additional_awards")
UI_PREFABS = (
    ("defaultpackage_assets_prefabs_ui_achievement.bundle", "/AchievementPanel.prefab"),
    ("defaultpackage_assets_prefabs_ui_achievement.bundle", "/AchievementGroupPanel.prefab"),
    ("defaultpackage_assets_prefabs_ui_achievement.bundle", "/AchievementPanelItem.prefab"),
    ("defaultpackage_assets_prefabs_ui_achievement.bundle", "/AchievementGroupContent.prefab"),
    ("defaultpackage_assets_prefabs_ui_gachapanel.bundle", "/ScoreRewardUI.prefab"),
    ("defaultpackage_assets_prefabs_ui_gachapanel.bundle", "/ScoreRewardDialog.prefab"),
    ("defaultpackage_assets_prefabs_ui_gachapanel.bundle", "/ScoreRewardDialogItem.prefab"),
)


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def table_type(constructors: Constructors, name: str) -> str:
    pattern = re.compile(r'new (Tb\w+)\(loader\("([^"]+)"\)\)')
    matches = [match[1] for line in constructors.source("Tables").splitlines()
               if (match := pattern.search(line)) and match[2] == name]
    if len(matches) != 1:
        raise ValueError(f"Expected one generated constructor for {name}, got {len(matches)}")
    return matches[0]


def build() -> dict:
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError(f"Achievement migration requires selected 871_156598 resources, got {apk.version}")

    constructors = Constructors()
    objects = {obj.read().m_Name: obj for obj in apk.objects(BUNDLE) if obj.type.name == "TextAsset"}
    table_inputs = {}
    tables = {}
    for name in TABLES:
        source = objects.get(name + ".g")
        if source is None:
            raise FileNotFoundError(f"{name}.g is absent from {apk.version}/{BUNDLE}")
        raw = source.read().m_Script.encode("utf-8", "surrogateescape")
        kind = table_type(constructors, name)
        rows = constructors.table(raw, kind)
        if not isinstance(rows, list) or (name != "gacha_additional_awards" and not rows):
            raise ValueError(f"Official {name} table decoded to no rows")
        table_inputs[name] = {"bytes": len(raw), "sha256": sha(raw), "class": kind,
                              "bundle": apk.provenance(source)}
        tables[name] = rows
    ui_prefabs = [{"path": path, **apk.provenance(apk.asset(bundle, path))}
                  for bundle, path in UI_PREFABS]

    catalog = json.loads(RULES.read_text(encoding="utf-8"))
    prior = {name: catalog.get(name) for name in TABLES}
    if any(value is not None and not isinstance(value, list) for value in prior.values()):
        raise ValueError("Existing local rules catalog has an invalid achievement/reward table")
    if any(name != "achievement_group" and value is None for name, value in prior.items()):
        raise ValueError("Existing local rules catalog is missing an achievement/reward table")
    catalog.update(tables)
    output = (json.dumps(catalog, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    temporary = RULES.with_suffix(".json.part")
    temporary.write_bytes(output)
    temporary.replace(RULES)

    receipt = {
        "format": 1,
        "manifest": apk.version,
        "tables": {name: dict(record, textAsset=name + ".g",
                              decoderSource="official code extracted from APK 854_156383",
                              decoderSha256=sha(constructors.source(record["class"]).encode("utf-8")),
                              priorTableSha256=(sha(json.dumps(prior[name], ensure_ascii=False,
                                                              separators=(",", ":")).encode("utf-8"))
                                                if prior[name] is not None else None),
                              rowCount=len(tables[name])) for name, record in table_inputs.items()},
        "verifiedUiPrefabs": ui_prefabs,
        "output": {"path": RULES.relative_to(ROOT).as_posix(), "bytes": len(output), "sha256": sha(output)},
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(json.dumps({"manifest": apk.version, "tables": {name: len(rows) for name, rows in tables.items()},
                      "outputSha256": receipt["output"]["sha256"]},
                     ensure_ascii=False))
    return receipt


if __name__ == "__main__":
    build()
