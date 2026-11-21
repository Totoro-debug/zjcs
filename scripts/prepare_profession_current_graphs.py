"""Convert selected profession graphs from the current, verified native export."""
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from battle_skill_entity_prefabs import skill_entity_scripts
from prepare_battle_status_orbit_assets import MAPPED, build
from prepare_battle_scene_appearance_runtime import identity, write_changed


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'guardian-passives'
    mapped = dict(MAPPED)
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   1598787400: ('BattleGridItemEntities', 'MyFightStatusShieldPersistent'),
                   -2102852653: ('BattleGridItemEntities', 'MyFightStatusRoundStart'),
                   921933769: ('BattleGridItemEntities', 'MyFightStatusDamageProcess'),
                   -1931582627: ('BattleGridItemEntities', 'MyFightStatusHitSkill'),
                   -822603460: ('StoryPlayables', 'ApplyBySource'),
                   -94696243: ('StoryAnimation', 'MyPlayableOverrideAnimator'),
                   1640720687: ('StorySounds', 'MyPlayableSound')})
    for file_id in (-1946306183, 1450751099, 1725120999):
        binding = skill_entity_scripts()[file_id]
        mapped[file_id] = (binding['target_namespace'].removeprefix('Zjcs.Official.'), binding['name'])
    data = ROOT / 'unity/Assets/Game/Resources/Combat/profession-current-graphs.json'
    receipt = ROOT / 'assets/reference/battle-animation/profession-current-graphs-source-manifest.json'
    build(DEST=ROOT / 'unity/Assets/Official/Combat/ProfessionGraphs/Resources/Combat/ProfessionGraphs',
          DATA=data, RECEIPT=receipt,
          ROOTS={}, graph_roots={key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                                 for key in (11570, 24239, 24489, 25042, 25108, 25115)},
          MAPPED=mapped, family='ProfessionGraphs', shader_prefix='ProfessionGraph_',
          reference_scripts={-210918009: dict(name='WwiseEventReference',
              native_asset='Wwise/ScriptableObjects/Event/0FF7086B-C85E-4769-8F7B-DED235C89952.asset',
              runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
              receipt='assets/reference/profession-ui/audio-source-manifest.json')},
          generator=Path(__file__), native_manifest=source.version,
          native_cache=cache, native_export_name='native-export',
          limits=['Owning skills verify combat rules, acquisition, cultivation, persistence and rendered Windows flows separately.'])
    # The skill root shares this native export but is not a status entity.
    catalog = json.loads(data.read_bytes())
    catalog['graphs'] = [row for row in catalog['graphs'] if row['id'] != 11570]
    write_changed(data, json.dumps(catalog, indent=2) + '\n')
    proof = json.loads(receipt.read_bytes())
    proof['outputs'] = [identity(data) if row['path'] == data.relative_to(ROOT).as_posix() else row
                        for row in proof['outputs']]
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
