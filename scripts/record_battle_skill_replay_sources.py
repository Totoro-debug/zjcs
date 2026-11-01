"""Record original replay configuration contracts and the confirmed offline adapter."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CURRENT = ROOT / ".tools/source-packages/current/884_156692"


def identity(path):
    raw = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "size": len(raw),
        "sha256": hashlib.sha256(raw).hexdigest()}


def main():
    receipt = CURRENT / "code-manifest.json"
    code = json.loads(receipt.read_bytes())
    assembly = CURRENT / "official-code/managed/decoded/Console.dll"
    assert code["manifest"] == "884_156692"
    assert identity(assembly)["sha256"] == code["assemblies"]["Console.dll"]["decoded_sha256"]
    source = CURRENT / "official-code/common-source/Common"
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/Combat"
    proof = dict(manifest=code["manifest"], code_receipt=identity(receipt), assembly=identity(assembly),
        original_contracts=[identity(source / (name + ".cs")) for name in (
            "FightSkillReplayCfg", "FightSkillReplayComponentInfo", "FightSkillReplayComponentImplement",
            "FightSkillComponentInfo", "ReplaySkillSlotByCondition", "RateFilter", "LogicFilter",
            "LogicFilterType", "FightSkillAgentComponentImplement", "FightSkillReplayModification")],
        configuration_inputs=[identity(ROOT / ("unity/Assets/Official/Combat/" + name))
            for name in ("skill-rules.json", "battle-rules.json")],
        configuration_manifest="854_156383",
        adapter=[identity(runtime / (name + ".cs")) for name in (
            "FightBattleExecutor", "FightBattleState", "FightSkillState", "FightOfficialCombatCatalog")],
        validation=[identity(ROOT / name) for name in (
            "scripts/test_battle_skill_replay.ps1",
            "unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.cs",
            "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneTimelineView.cs",
            "unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.Replay.cs")],
        recorder=identity(Path(__file__).resolve()),
        operation="Retain historical configuration identity; inspect current original replay contracts and client-consumed modification. Server replay execution is absent. Implement the user-confirmed offline continuation and ordered replacements across original filters, per-round limits, actual skill identities and persistent counters.")
    target = ROOT / "assets/reference/battle-rules/skill-replay-source-manifest.json"
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded original replay contracts, versioned configuration and offline adapter identities.")


if __name__ == "__main__":
    main()
