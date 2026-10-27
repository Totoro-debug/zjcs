"""Record official substitute components and the explicitly confirmed offline decisions."""
import json
from pathlib import Path
from prepare_battle_summon_inheritance import ROOT, record


def main():
    parent = json.loads((ROOT / "assets/reference/battle-rules/summon-inheritance-source-manifest.json").read_text("utf-8"))
    names = ["FightHitSummonVoodooDollCfg", "FightHitSummonCfgBase", "FightRoleVoodooDollComponentInfo",
             "FightRoleVoodooDollComponent", "FightRoleVoodooDollSpawnerComponent",
             "FightStatusComponentInfo", "StatusEndComponentInfo", "StatusEndComponent"]
    sources = []
    for name in names:
        relative = "common-source/Common/" + name + ".cs"
        sources.append(record(".tools/source-packages/current/871_156598/official-code/" + relative))
    battle = json.loads((ROOT / "unity/Assets/Official/Combat/battle-rules.json").read_text("utf-8"))
    selected = {key: battle["entities"][key] for key in ["4600", "12200", "12201", "12202",
                                                          "12204", "24000", "24014", "10140", "10141"]}
    output = {"assemblies": parent["assemblies"], "code_manifest": parent["code_manifest"],
              "sources": sources, "runtime_inputs": parent["runtime_inputs"],
              "official_description": record("unity/Assets/Official/Skills/fidelity-texts.json"),
              "description_keys": ["item_10097_name", "item_10097_func_desc"],
              "entities": selected,
              "server_gap": "Spawner and component classes provide serialization/opcodes, not server execution.",
              "confirmed_offline_rules": ["Same logical character type as linked enemy; attackable by caster's side.",
                  "Transmit full suffered damage including lethal overflow, directly to HP, ignoring shield, affinity and modifiers.",
                  "Reject buffs, allow debuffs; transfer remaining rounds/stacks when dead or removed.",
                  "Merge transferred debuffs using official same-status parallel/replace/discard/stack/refresh rules."],
              "scope": "Self sync configuration; official 12200/4600 generation, direct hit transmission, creator-clock lifetime and checkpoint.",
              "not_validated": ["Complete native model/animation/effects", "Other SyncType branches", "All non-hit HP mutation paths", "Windows Player"],
              "validation": "scripts/test_battle_voodoo_doll.ps1"}
    (ROOT / "assets/reference/battle-rules/voodoo-doll-source-manifest.json").write_text(
        json.dumps(output, ensure_ascii=False, indent=2) + "\n", "utf-8")


if __name__ == "__main__":
    main()

