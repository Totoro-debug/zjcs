"""Record the source identities for official equipment-term selection and its host binding."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    data = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(data),
            "sha256": hashlib.sha256(data).hexdigest()}


def main():
    source_root = ROOT / ".tools/source-packages/current/884_156692/official-code/common-source/Common"
    files = [source_root / name for name in (
        "BattleFormulaHandler.cs", "EquipAttributeInfoParser.cs",
        "EquipAttributesPropsInfo.cs", "EquipAttributesPassiveSkillInfo.cs",
        "EquipAttributeConfig.cs", "SkillRankInfoParser.cs", "ValueType.cs")]
    output = {
        "code_manifest": "884_156692",
        "configuration_manifest": "854_156383",
        "original": [identity(path) for path in files],
        "inputs": [identity(ROOT / path) for path in (
            "unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json",
            "unity/Assets/Official/Equipment/Runtime/source-manifest.json",
            "unity/Assets/Official/Combat/rules.json",
            "unity/Assets/Official/Skills/skill-runtime-rules.json")],
        "adapters": [identity(ROOT / path) for path in (
            "unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs",
            "unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.BattleTerms.cs",
            "unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.BattleTerms.cs",
            "unity/Assets/Game/Runtime/Presentation/Combat/OfficialEquipmentBattlePassives.cs",
            "unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs")],
        "hosts": [identity(ROOT / path) for path in (
            "unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs",
            "unity/Assets/Game/Runtime/Presentation/Home/ArenaBattleRunner.cs",
            "unity/Assets/Game/Runtime/Presentation/Home/FantasyTowerBattleRunner.cs",
            "unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestBattleRunner.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Battle.cs",
            "unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.EquipmentTerms.cs")],
        "validation": [identity(ROOT / path) for path in (
            "scripts/prepare_battle_equipment_selection_reference.py",
            "scripts/fixtures/battle_equipment_selection.cs",
            "scripts/test_battle_equipment_selection.ps1",
            "scripts/test_battle_equipment_replay.ps1")],
        "operation": "Port the official CheckEquipsAllEffectTerm order: filter IsApplyInBattle before exclusivity, compare incoming LimitRank/BlessLevel contexts with ties retaining the first term, use first skill rank by quality, and bind equipped and active suit passive skills. Confirmed offline rule uses effective equipment level and integer mean for growing suit terms, while fixed replacement parameters remain unchanged; owner SubRank propagates to status and child skills."
    }
    target = ROOT / "assets/reference/battle-rules/equipment-selection-source-manifest.json"
    target.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded official equipment selection and battle-host identities.")


if __name__ == "__main__":
    main()
