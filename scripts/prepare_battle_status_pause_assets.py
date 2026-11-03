"""Restore the complete current original 24390 animation pause graph."""
from pathlib import Path
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build, SCRIPTS


if __name__ == '__main__':
    mapped = dict(SCRIPTS)
    mapped.update({-822603460: ('StoryPlayables', 'ApplyBySource'),
                   498949265: ('BattleAnimationPlayables', 'MyPlayableAnimationControllerPause')})
    build(DEST=ROOT/'unity/Assets/Official/Combat/StatusAnimations/Resources/Combat/StatusAnimations',
          DATA=ROOT/'unity/Assets/Game/Resources/Combat/status-animations.json',
          RECEIPT=ROOT/'assets/reference/battle-animation/status-pause-assets-source-manifest.json',
          ROOTS={24390: 'playable24390'}, MAPPED=mapped,
          family='StatusAnimations', shader_prefix='StatusAnimation_',
          runtime_receipts=('status-pause-runtime-source-manifest.json',), generator=Path(__file__),
          limits=['One complete native status graph. Other freeze graphs and formal status actor/timeline installation remain pending.'])
