"""Verify the original active/passive fragment set and the UI code that consumes it."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / "assets/reference/skill-piece-types-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterCatalog.SkillPieceTypes.cs"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true")
    args = parser.parse_args()
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review the fragment source version before conversion.")
    code = ROOT / ".tools/source-packages/current/884_156692/official-code"
    obj = apk.asset("config_luban", "/game_settings.g.bytes")
    raw = obj.read().m_Script.encode("utf8", "surrogateescape")
    constructors = Constructors(code)
    rows = constructors.table(raw, "TbGameSettings")
    settings = rows[0] if isinstance(rows, list) else rows
    types = settings["SkillPieceTypes"]
    if types != [111, 112]:
        raise ValueError("Original SkillPieceTypes changed: " + repr(types))
    item_object = apk.asset("config_luban", "/item.g.bytes")
    item_raw = item_object.read().m_Script.encode("utf8", "surrogateescape")
    items = constructors.table(item_raw, "TbItemInfo")
    pieces = sorted((row["ClassId"], row["ItemType"]) for row in items if row["ItemType"] in types)
    if len(pieces) != 376 or dict(pieces).get(12588) != 112 or dict(pieces).get(12061) != 111:
        raise ValueError("Review the selected original fragment item mapping before conversion.")
    lines = ["// Generated from the selected official APK item table; see skill-piece-types-source-manifest.json.",
             "using System.Collections.Generic;", "using System.Linq;", "", "namespace Zjcs.Offline.Progression", "{",
             "    public sealed partial class OfficialCharacterCatalog", "    {",
             "        private static void NormalizeOfficialSkillPieceTypes(Dictionary<int, Dictionary<string, object>> items)",
             "        {", "            foreach (int id in items.Keys.ToArray())", "            {",
             "                int type = SelectedOfficialSkillPieceType(id);",
             "                if (type == 0 || Integer(items[id], \"ItemType\") == type) continue;",
             "                items[id] = new Dictionary<string, object>(items[id], System.StringComparer.Ordinal) { [\"ItemType\"] = type };",
             "            }", "        }", "", "        private static int SelectedOfficialSkillPieceType(int id)",
             "        {", "            switch (id)", "            {"]
    for item_type in types:
        lines.extend("                case " + str(item_id) + ":" for item_id, kind in pieces if kind == item_type)
        lines.append("                    return " + str(item_type) + ";")
    lines.extend(["                default: return 0;", "            }", "        }", "    }", "}", ""])
    generated = "\n".join(lines).encode("utf-8")
    if args.record:
        OUTPUT.write_bytes(generated)
    elif not OUTPUT.exists() or OUTPUT.read_bytes() != generated:
        raise ValueError("Generated original fragment item types differ; inspect before recording.")
    files = [
        "common-source/Common/GameSettings.cs", "common-source/Common/ItemType.cs",
        "common-source/Common/ItemInfo.cs", "common-source/Common/TbItemInfo.cs",
        "common-source/Common/SkillRankInfoParser.cs",
        "client-source/CharacterPanelSkillListContent.cs",
        "client-source/ItemDetailDialogFactory.cs", "client-source/ItemIconSkillPiece.cs",
        "client-source/ItemIcon.cs", "client-source/ItemProgress.cs",
    ]
    inputs = []
    for name in files:
        path = code / name
        inputs.append({"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())})
    receipt = {
        "schema": 1, "version": apk.version,
        "settings": {"source": apk.provenance(obj), "raw_sha256": digest(raw), "SkillPieceTypes": types},
        "fragment_items": {"source": apk.provenance(item_object), "raw_sha256": digest(item_raw),
                           "types": [{"item_id": item_id, "item_type": kind} for item_id, kind in pieces],
                           "active_count": sum(kind == 111 for _, kind in pieces),
                           "passive_count": sum(kind == 112 for _, kind in pieces)},
        "output": {"path": OUTPUT.relative_to(ROOT).as_posix(), "sha256": digest(generated)},
        "official_code": inputs,
        "original_entries": ["CharacterPanelSkillListContent.SelectSkillPieceItem",
                             "ItemInfo.IsSkillPiece", "ItemDetailDialogFactory.GetItemDetailDialogType",
                             "ItemIconSkillPiece.ShowProgress", "ItemIcon.SetRequireCount"],
        "runtime_entries": ["CharacterSkillPanel.RefreshPieceList", "CharacterSkillPanel.BuildSkillIcon",
                            "CharacterSkillPanel.BuildOriginalCultivationMaterial"],
        "conversion": "Preserve the shared fragment tab; classify both original active and passive fragments for its list, icon template and cultivation material popup. Apply only selected 884 fragment ItemType values to cloned historical runtime item records; preserve all other fields and original payload hashes.",
    }
    if args.record:
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    elif not RECEIPT.exists() or json.loads(RECEIPT.read_text(encoding="utf-8")) != receipt:
        raise ValueError("Fragment type source receipt differs; inspect before recording.")
    print("Original active/passive fragment types verified:", types)


if __name__ == "__main__":
    main()
