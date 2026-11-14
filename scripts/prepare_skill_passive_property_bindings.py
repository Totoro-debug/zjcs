"""Record/recheck existing official inputs explicitly bound to character skill properties."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / "assets/reference/battle-rules/skill-passive-property-bindings.json"
CHARACTER = "unity/Assets/Game/Resources/Character/OfficialCharacterAssetCatalog.asset"
COMBAT = "unity/Assets/Game/Resources/Combat/OfficialCombatAssetCatalog.asset"
BINDINGS = (
    ("skillPassiveRules", "skillRules", "skill-rules.json"),
    ("skillPassiveSourceManifest", "skillSourceManifest", "skill-rules-source-manifest.json"),
    ("battlePassiveRules", "battleRules", "battle-rules.json"),
    ("battlePassiveSourceManifest", "battleSourceManifest", "battle-rules-source-manifest.json"),
    ("passiveSkillItems", "rules", "rules.json"),
    ("passiveSkillItemsSourceManifest", "rulesSourceManifest", "rules-source-manifest.json"),
)


def identity(name: str, *, normalize_text: bool = False) -> dict:
    data = (ROOT / name).read_bytes()
    if normalize_text:
        data = data.decode("utf-8").replace("\r\n", "\n").encode("utf-8")
    result = {"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    if normalize_text:
        result["normalization"] = "UTF-8, LF line endings"
    return result


def bindings(name: str) -> dict[str, str]:
    return dict(re.findall(r"^  (\w+): \{fileID: 4900000, guid: ([0-9a-f]{32}), type: 3\}$",
                           (ROOT / name).read_text(encoding="utf-8"), re.M))


def prepare() -> dict:
    character, combat = bindings(CHARACTER), bindings(COMBAT)
    inputs = []
    for target_field, source_field, filename in BINDINGS:
        name = "unity/Assets/Official/Combat/" + filename
        meta = (ROOT / (name + ".meta")).read_text(encoding="utf-8")
        guid = re.search(r"^guid: ([0-9a-f]{32})$", meta, re.M).group(1)
        if character.get(target_field) != guid or combat.get(source_field) != guid:
            raise ValueError("Character and combat must bind the identical existing TextAsset: " + filename)
        row = identity(name, normalize_text=filename.endswith("-source-manifest.json"))
        row.update(character_field=target_field, combat_field=source_field, guid=guid)
        if filename in {"skill-rules.json", "battle-rules.json", "rules.json"}:
            source = name.removesuffix(".json") + "-source-manifest.json"
            manifest = json.loads((ROOT / source).read_text(encoding="utf-8"))
            if manifest["output_sha256"] != row["sha256"]:
                raise ValueError("Existing official payload differs from its source manifest: " + name)
            row.update(manifest=manifest["manifest"], source_manifest=source)
        inputs.append(row)
    original = ".tools/source-packages/official-code/common-source/Common/"
    return {
        "operation": "Bind already verified local combat TextAssets to the character property catalog; "
                     "reuse CalcSkillPassiveProps, with original rank, effective slot level and SubRank inputs. "
                     "No source payload or expected payload hash is changed.",
        "inputs": inputs,
        "original_code": [identity(original + name + ".cs")
                          for name in ("SkillPropChunck", "BattleFormulaHandler", "BattleFormula")],
        "selected_scope": identity("assets/reference/battle-rules/first-promotion-growth-source-manifest.json", normalize_text=True),
        "output_binding": identity(CHARACTER, normalize_text=True),
        "binding_preparation": "Zjcs.Editor.Character.CharacterProgressionValidation.BindOfficialCatalogs",
        "runtime_entry": "OfficialCharacterAssetCatalog.Load → OfficialCharacterCatalog.WithSkillPropertyRules "
                         "→ OfflineSkillPropSource.CalculateProps → FightOfficialCombatCatalog.CalculatePassiveSkillProps",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true")
    args = parser.parse_args()
    receipt = prepare()
    if args.record:
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    elif json.loads(RECEIPT.read_text(encoding="utf-8")) != receipt:
        raise ValueError("Character skill property binding receipt differs from the current inputs.")
    print("Character skill passive property bindings verified: six identical TextAssets, three pinned payload hashes.")


if __name__ == "__main__":
    main()
