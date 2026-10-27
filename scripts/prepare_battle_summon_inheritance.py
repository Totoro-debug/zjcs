"""Record verified official formula inputs for the summon inheritance port."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def record(path):
    return {"path": path, "sha256": hashlib.sha256((ROOT / path).read_bytes()).hexdigest()}


def main():
    current = ".tools/source-packages/current/871_156598/official-code/"
    historical = ".tools/source-packages/official-code/"
    names = ["common-source/Common/BattleFormulaHandler.cs",
             "common-source/Common/SummonRankAdditiveFactorParser.cs",
             "client-source/MonsterInfoPanelNavigateHandler.cs"]
    code = []
    for name in names:
        latest, previous = record(current + name), record(historical + name)
        if latest["sha256"] != previous["sha256"]:
            raise RuntimeError("Source versions differ; review before recording: " + name)
        code.append({"current": latest, "historical": previous, "byte_identical": True})
    preparation = json.loads((ROOT / "assets/reference/battle-rules/preparation-source-manifest.json").read_text("utf-8"))
    for assembly in preparation["assemblies"]:
        if record(assembly["path"])["sha256"] != assembly["sha256"]:
            raise RuntimeError("Decoded assembly changed: " + assembly["path"])
    inputs = []
    for name in ["skill-rules", "battle-rules", "rules"]:
        path = "unity/Assets/Official/Combat/" + name
        receipt = json.loads((ROOT / (path + "-source-manifest.json")).read_text("utf-8"))
        payload = record(path + ".json")
        if payload["sha256"] != receipt["output_sha256"]:
            raise RuntimeError("Runtime payload hash mismatch: " + path)
        inputs.append({**payload, "manifest": receipt["manifest"],
                       "source_manifest": record(path + "-source-manifest.json")})
    output = {"code_manifest": record(".tools/source-packages/current/871_156598/code-manifest.json"),
              "assemblies": preparation["assemblies"], "sources": code, "runtime_inputs": inputs,
              "formula": "CalcSummonMonsterInheritProp: fixed level/rank props + (long)(addition/10000.0 * source raw value * exact rank factor/10000.0)",
              "scope": "Ordinary monster summon execution; pet and substitute generation remain separate consumers.",
              "critical": "58,75,79 inherit together according to per-summon tables; no universal critical chance bonus is present.",
              "ports": ["unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.SummonProperties.cs",
                        "unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs"],
              "validation": "scripts/test_battle_summon_inheritance.ps1"}
    destination = ROOT / "assets/reference/battle-rules/summon-inheritance-source-manifest.json"
    destination.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", "utf-8")


if __name__ == "__main__":
    main()
