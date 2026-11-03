"""Restore complete original player status graphs using native attachment followers."""
from pathlib import Path
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build, SCRIPTS


if __name__ == '__main__':
    mapped = dict(SCRIPTS)
    mapped.update({666328883: ('World/Map13TreePlayables', 'ApplyByBone'),
                   -1421533442: ('BattleSpinePlayables', 'AttachmentFollower'),
                   1391534298: ('BattleSpinePlayables', 'MyPlayableFollowSpinePart')})
    build(DEST=ROOT/'unity/Assets/Official/Combat/StatusSpineFollowers/Resources/Combat/StatusSpineFollowers',
          DATA=ROOT/'unity/Assets/Game/Resources/Combat/status-spine-followers.json',
          RECEIPT=ROOT/'assets/reference/battle-animation/spine-follower-assets-source-manifest.json',
          ROOTS={24207: 'playable_24207', 24227: 'playable24227'}, MAPPED=mapped,
          family='StatusSpineFollowers', shader_prefix='StatusSpineFollower_',
          runtime_receipts=('spine-follower-runtime-source-manifest.json',), generator=Path(__file__),
          limits=['Two complete native player status graphs. Other monster/skill graphs and formal battle actor/timeline installation remain pending.'])
