"""Restore both complete original global frozen graphs and Windows dependencies."""
from pathlib import Path
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build, SCRIPTS


if __name__ == '__main__':
    mapped = dict(SCRIPTS)
    mapped.update({-822603460: ('StoryPlayables', 'ApplyBySource'),
                   498949265: ('BattleAnimationPlayables', 'MyPlayableAnimationControllerPause'),
                   1574993081: ('BattleMaterialPlayables', 'MyPlayableMaterialProperty'),
                   -425368815: ('BattleMaterialPlayables', 'PlayableMaterialBoundingBoxProvider')})
    build(DEST=ROOT/'unity/Assets/Official/Combat/StatusFrozen/Resources/Combat/StatusFrozen',
          DATA=ROOT/'unity/Assets/Game/Resources/Combat/status-frozen.json',
          RECEIPT=ROOT/'assets/reference/battle-animation/status-frozen-assets-source-manifest.json',
          ROOTS={}, graph_roots={name: 'Prefabs/EcEntity/Fight/FightSkill/GlobalPlayable/'+name+'.prefab'
              for name in ('playable_Frozen_1', 'playable_Frozen_2')}, MAPPED=mapped,
          family='StatusFrozen', shader_prefix='StatusFrozen_',
          runtime_receipts=('status-frozen-runtime-source-manifest.json', 'status-material-runtime-source-manifest.json',
                            'status-pause-runtime-source-manifest.json'), generator=Path(__file__),
          limits=['Both native graphs are complete. Formal battle status producers, actor attachment and timeline installation remain pending.'])
