"""Convert the current original Chaos Rune and shared confusion graphs."""
import json
import re
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from current_battle_native_skill_graphs import build_current_skill_graphs
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_scene_appearance_runtime import identity
from prepare_battle_status_orbit_assets import build


def shared_shader(cache):
    previous_path = ROOT / 'assets/reference/battle-animation/fire-core-current-skill-graphs-source-manifest.json'
    previous = json.loads(previous_path.read_bytes())
    inventory = json.loads((cache / 'all-assets/inventory.json').read_bytes())
    shader_name = 'Effect/Noise_Mask_Pro'
    current, = (value for value in inventory['shaders'] if value['name'] == shader_name)
    converted, = previous['shaders']
    assert current['object_sha256'] == converted['object_sha256']
    target = 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders/FireCoreSkillGraph_Noise_Mask_Pro.shader'
    assert identity(ROOT / target) in previous['outputs']
    assert identity(ROOT / (target + '.meta')) in previous['outputs']
    source = 'Shaders/Effect/Noise_Mask_Pro.asset'
    source_meta = cache / 'native-export/ExportedProject/Assets' / (source + '.meta')
    guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', source_meta.read_text('utf-8-sig'))[1]
    return {guid: dict(source=source, target=target)}


def main():
    manifest = OfficialSource().version
    cache_name = 'sage-chaos-rune-current'
    cache = ROOT / '.tools/source-packages/current' / manifest / cache_name
    shaders = shared_shader(cache)
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
