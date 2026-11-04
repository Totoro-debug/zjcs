"""Record original client notification branches and the shared HUD adaptation."""
import hashlib
import json
import re
from inspect_battle_skill_graph_bindings import EXPORT, EXPORT_RECEIPT, FIELDS, script_file_id
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    if json.loads((ROOT / "official-source.json").read_bytes())["active"] != "884_156692":
        raise ValueError("Review the selected skill lifecycle application sources")
    code = ROOT / ".tools/source-packages/current/884_156692/official-code/client-source"
    originals = [code / (name + ".cs") for name in ("MyFightSkill", "MyFightSkillAgent",
        "FightSkillListDataEntityProvider", "FightSkillListDataProvider", "FightMainPanelSkillUI", "GameSkillIcon")]
    # Verify the source chain, rather than inferring the HUD state from a step's
    # actor or from animation duration. These original callbacks remain in use.
    required = {
        "MyFightSkill": ["SendSkillAppliedEvent(this)", "SendSkillEndedEvent(this)"],
        "FightSkillListDataEntityProvider": ["mSkillAgent.OnSkillApplied += MSkillAgent_OnSkillApply;",
            "mSkillAgent.OnSkillEnded -= MSkillAgent_OnSkillEnd;", "SendSkillStartedEvent(skillSlot);", "SendSkillEndedEvent(skillSlot);"],
        "FightMainPanelSkillUI": ["SetUIState(GameSkillIcon.State.Applied);", "SetUIState(GameSkillIcon.State.Ended);"]}
    for name, fragments in required.items():
        source = (code / (name + ".cs")).read_text("utf8")
        if not all(fragment in source for fragment in fragments):
            raise ValueError("Selected client notification branch differs: " + name)
    graph_data = ROOT / "unity/Assets/Game/Resources/Combat/skill-native-graphs.json"
    entity_data = ROOT / "unity/Assets/Game/Resources/Combat/skill-entity-views.json"
    graphs = json.loads(graph_data.read_bytes())["skills"]
    entities = json.loads(entity_data.read_bytes())["skills"]
    if {row["skill_entity_id"] for row in graphs} != {row["skillEntityId"] for row in entities}:
        raise ValueError("Native graph/entity catalogs differ")
    if next(row for row in graphs if row["skill_entity_id"] == 10100)["playables"]:
        raise ValueError("Recheck the known empty graph fixture")
    original_empty = next(EXPORT.glob("*/10100.prefab"))
    receipt = json.loads(EXPORT_RECEIPT.read_bytes())
    digest = identity(original_empty)
    key = original_empty.relative_to(EXPORT.parents[5]).as_posix()
    if next(row for row in receipt["outputs"] if row["path"] == key)["sha256"] != digest["sha256"]:
        raise ValueError("Original empty skill prefab differs from export receipt")
    components = re.findall(r"(?ms)^--- !u!114 &\d+\nMonoBehaviour:\n(.*?)(?=^--- !u!|\Z)", original_empty.read_text("utf-8-sig"))
    component = next(body for body in components if f"m_Script: {{fileID: {script_file_id()}," in body)
    for field in FIELDS:
        if not re.search(rf"(?m)^  {field}: \{{fileID: 0\}}$", component):
            raise ValueError("Original skill no longer declares an empty playable: " + field)
    runtime = [ROOT / "unity/Assets/Game" / path for path in (
        "Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs",
        "Runtime/Presentation/Combat/OfficialBattleSkillGraphCatalog.cs",
        "Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs",
        "Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs",
        "Runtime/Presentation/Combat/OfficialBattleHudView.cs",
        "Runtime/Presentation/Combat/OfficialBattleHudView.SkillLifecycle.cs",
        "Runtime/Presentation/Combat/OfficialBattleHudView.SkillLifecycle.cs.meta",
        "Runtime/Official/BattleSkillPlayables/MyFightSkill.cs",
        "Runtime/Official/BattleSkillPlayables/MyFightSkillAgent.cs",
        "Runtime/Official/WorldUI/WorldGameSkillIcon.cs")]
    fixtures = [ROOT / "unity/Assets/Game/Editor" / path for path in (
        "Combat/OfficialBattleSkillSlotAcceptance.cs",
        "Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ProductionSkillViews.cs",
        "Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SkillSlotLifecycle.cs",
        "Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SkillSlotLifecycle.cs.meta",
        "Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef")]
    bindings = [ROOT / path for path in (
        "assets/reference/battle-animation/skill-entity-views-source-manifest.json",
        "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json",
        "unity/Assets/Game/Resources/Combat/skill-native-graphs.json",
        "unity/Assets/Game/Resources/Combat/skill-entity-views.json",
        "unity/Assets/Game/Resources/Combat/battle-skill-slots.json",
        "unity/Assets/Game/Resources/Combat/battle-skill-slots-source-manifest.json")]
    fixtures.extend(ROOT / path for path in ("scripts/fixtures/battle_skill_graph_dispatch_harness.cs",
        "scripts/test_battle_skill_graph_dispatch.ps1"))
    proof = dict(known_empty_graph=dict(entity_id=10100, original_prefab=digest,
        native_export_receipt=identity(EXPORT_RECEIPT), original_null_fields=list(FIELDS)),
        matching_catalog_entity_count=len(graphs), manifest="884_156692", generator=identity(Path(__file__).resolve()),
        application_sources=[identity(path) for path in originals],
        source_code_manifest=identity(ROOT / ".tools/source-packages/current/884_156692/code-manifest.json"),
        runtime=[identity(path) for path in runtime], fixtures=[identity(path) for path in fixtures],
        existing_bindings=[identity(path) for path in bindings],
        operation="Forward original MyFightSkillAgent start/end notifications by source unit and slot into the shared HUD. "
            "Keep original Applied/Ended state switches and native entity removal time; detach borrowed-agent subscriptions. "
            "Reuse the existing 854 HUD bindings with their own source identity, without substituting 884 art or altering layout.",
        limits=["No new UI layout, artwork, numerical rule or server skill creation implementation.",
            "Isolated canvas output does not display the complete skill row; state assertions verify original bound objects, not full HUD visual acceptance.",
            "Known original empty playables still instantiate skill entities. Unknown IDs remain excluded; declared missing assets still throw.",
            "The finite view probe does not validate rule-selected durations, interrupts, actual audio, complete menus, saves or Windows Player."])
    target = ROOT / "assets/reference/battle-hud/skill-slot-lifecycle-source-manifest.json"
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + "\n").encode("utf8"))
    print("Original skill slot lifecycle sources recorded:", len(originals), "application files")


if __name__ == "__main__":
    main()
