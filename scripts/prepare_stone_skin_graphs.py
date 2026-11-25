"""Convert the current original Stone Skin listener and stacked status graphs."""
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'stone-skin-10542'
    mapped = dict(MAPPED)
    shared = shared_bindings()
    mapped[1640720687] = shared['MAPPED'][1640720687]
    event = dict(shared['reference_scripts'][-210918009])
    event['native_asset'] = 'Wwise/ScriptableObjects/Event/6820B439-4E09-4683-9DE2-E9F6F6397FFF.asset'
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   1907728065: ('BattleGridItemEntities', 'MyFightStatusDamageSkill'),
                   -550836577: ('BattleGridItemEntities', 'MyFightStatusProp')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/StoneSkinGraphs/Resources/Combat/StoneSkinGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/stone-skin-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/stone-skin-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={
              25033: 'Prefabs/EcEntity/Fight/FightSkill/25033/25033.prefab',
              24229: 'Prefabs/EcEntity/Fight/FightSkill/24229/24229.prefab'},
          MAPPED=mapped, family='StoneSkinGraphs', shader_prefix='StoneSkinGraph_',
          generator=Path(__file__), native_manifest=source.version, native_cache=cache,
          native_export_name='native-export', reference_scripts={-210918009: event},
          limits=['The owning skill separately verifies stacking, damage reduction, persistence and rendered Windows gameplay.'])


if __name__ == '__main__':
    main()
