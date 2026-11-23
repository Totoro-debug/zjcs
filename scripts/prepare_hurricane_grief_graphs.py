"""Convert the current original 24488 status and 12660 skill graphs."""
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from current_battle_native_skill_graphs import build_current_skill_graphs
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main() -> None:
    source = OfficialSource()
    cache = ROOT / ".tools/source-packages/current" / source.version / "destroyer-hurricane-grief"
    mapped = dict(MAPPED)
    shared = shared_bindings()
    mapped.update(shared["MAPPED"])
    mapped.update({
        1200213464: ("EntityLifecycle", "MyEntity"),
        -109006141: ("EntityActions", "MyAction"),
        -1481699058: ("BattleStatusPlayables", "MyFightStatus"),
        812704826: ("BattleGridItemEntities", "MyFightStatusActionStart"),
    })
    event_binding = dict(shared["reference_scripts"][-210918009])
    event_binding["native_asset"] = (
        "Wwise/ScriptableObjects/Event/B37110EF-FF0B-47AA-8C61-632CD4905E32.asset"
    )
    build(
        DEST=ROOT / "unity/Assets/Official/Combat/HurricaneGriefGraphs/Resources/Combat/HurricaneGriefGraphs",
        DATA=ROOT / "unity/Assets/Game/Resources/Combat/hurricane-grief-current-graphs.json",
        RECEIPT=ROOT / "assets/reference/battle-animation/hurricane-grief-current-graphs-source-manifest.json",
        ROOTS={}, graph_roots={24488: "Prefabs/EcEntity/Fight/FightSkill/24488/24488.prefab"},
        MAPPED=mapped, reference_scripts={-210918009: event_binding},
        family="HurricaneGriefGraphs", shader_prefix="HurricaneGriefGraph_",
        generator=Path(__file__), native_manifest=source.version,
        native_cache=cache, native_export_name="native-export",
        limits=["The owning skill verifies 35% wind-hit application, movement-triggered damage, cultivation, persistence and rendered Windows gameplay separately."],
    )
    build_current_skill_graphs(
        DEST=ROOT / "unity/Assets/Official/Combat/HurricaneGriefSkillGraphs/Resources/Combat/HurricaneGriefSkillGraphs",
        DATA=ROOT / "unity/Assets/Game/Resources/Combat/hurricane-grief-current-skill-graphs.json",
        RECEIPT=ROOT / "assets/reference/battle-animation/hurricane-grief-current-skill-graphs-source-manifest.json",
        GROUPS={
            "skills": ("skill_entity_id", "MyFightSkill", [12660]),
            "hits": ("hit_entity_id", "MyFightHitFixed", []),
            "moves": ("action_entity_id", "MyFightActionSkillMove", []),
            "damages": ("damage_entity_id", "MyFightDamage", []),
        },
        family="HurricaneGriefSkillGraphs", cache_name="destroyer-hurricane-grief",
        generator=Path(__file__),
    )


if __name__ == "__main__":
    main()
