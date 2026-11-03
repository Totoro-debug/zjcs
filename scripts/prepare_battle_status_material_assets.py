"""Restore the full original 24059 material/particle graph, including all leaves."""
from pathlib import Path
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build, SCRIPTS


if __name__ == '__main__':
    mapped = dict(SCRIPTS)
    mapped.update({-822603460: ('StoryPlayables', 'ApplyBySource'),
                   1574993081: ('BattleMaterialPlayables', 'MyPlayableMaterialProperty')})
    build(DEST=ROOT/'unity/Assets/Official/Combat/StatusMaterials/Resources/Combat/StatusMaterials',
          DATA=ROOT/'unity/Assets/Game/Resources/Combat/status-materials.json',
          RECEIPT=ROOT/'assets/reference/battle-animation/status-material-assets-source-manifest.json',
          ROOTS={24059: 'playable24059'}, MAPPED=mapped,
          family='StatusMaterials', shader_prefix='StatusMaterial_',
          runtime_receipts=('status-material-runtime-source-manifest.json',),
          generator=Path(__file__),
          limits=['One complete native graph is restored. Formal battle actors, all status roles and other material graphs are still pending.'])
