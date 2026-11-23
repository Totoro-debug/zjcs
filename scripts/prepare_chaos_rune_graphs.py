"""Convert the current original Chaos Rune and shared confusion graphs."""
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import build


def main():
    manifest = OfficialSource().version
    cache_name = 'sage-chaos-rune-current'
    cache = ROOT / '.tools/source-packages/current' / manifest / cache_name
    shaders = verified_noise_mask_binding(cache)
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/ChaosRuneSkillGraphs/Resources/Combat/ChaosRuneSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/chaos-rune-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/chaos-rune-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11380]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11381]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11382]),
        },
        family='ChaosRuneSkillGraphs', cache_name=cache_name, generator=Path(__file__),
        external_assets=shaders,
    )

    bindings = shared_bindings()
    mapped = {key: bindings['MAPPED'][key] for key in (-253978515, -1107144664, -1247969075, 768753278)}
    mapped[-1481699058] = ('BattleStatusPlayables', 'MyFightStatus')
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat/ConfusionGraphs/Resources/Combat/ConfusionGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/confusion-current-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/confusion-current-graphs-source-manifest.json',
        ROOTS={}, graph_roots={24217: 'Prefabs/EcEntity/Fight/FightSkill/24217/24217.prefab'}, MAPPED=mapped,
        source_scripts={key: bindings['source_scripts'][key] for key in (1200213464, -109006141)},
        family='ConfusionGraphs', generator=Path(__file__), native_manifest=manifest, native_cache=cache,
        native_export_name='native-export', external_assets=shaders,
    )


if __name__ == '__main__':
    main()
