"""Restore complete original projectile and actor-movement graphs with native curves."""
from pathlib import Path
import json
import re
from hashlib import sha256
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build,SCRIPTS
from prepare_battle_scene_appearance_runtime import write_changed


if __name__=='__main__':
    mapped=dict(SCRIPTS)
    mapped.update({-822603460:('StoryPlayables','ApplyBySource'),
        -741389653:('BattleTrackPlayables','MyPlayableTrack'),
        602813528:('BattleTrackPlayables','MyTrack'),
        1801709843:('BattleTrackPlayables','MyCurveTrack'),
        180732079:('BattleTrackPlayables','AnimationTrack'),
        116049708:('BattleTrackPlayables','ParabolicTrack')})
    build(DEST=ROOT/'unity/Assets/Official/Combat/Tracks/Resources/Combat/Tracks',
        DATA=ROOT/'unity/Assets/Game/Resources/Combat/tracks.json',
        RECEIPT=ROOT/'assets/reference/battle-animation/track-assets-source-manifest.json',
        ROOTS={10950:'playable10951_2',10580:'playable10584_move',11170:'playable11171'},MAPPED=mapped,
        family='Tracks',shader_prefix='Track_',runtime_receipts=('track-runtime-source-manifest.json',),
        generator=Path(__file__),limits=['Three complete original graphs and their native curve dependencies. Full production battle graph selection and timeline integration remain pending.'])
    receipt=ROOT/'assets/reference/battle-animation/track-assets-source-manifest.json'
    proof=json.loads(receipt.read_bytes())
    canonical=[]
    for output in proof['outputs']:
        path=ROOT/output['path']
        if path.suffix!='.meta':continue
        original=path.read_bytes()
        normalized=b'\n'.join(line.rstrip() for line in original.splitlines())+b'\n'
        if original==normalized:continue
        # Empty metadata values retain their exact meaning and stable GUID.
        assert re.search(rb'(?m)^guid: (\w+)$',original)[1]==re.search(rb'(?m)^guid: (\w+)$',normalized)[1]
        write_changed(path,normalized.decode('utf-8'))
        canonical.append(dict(path=output['path'],before_sha256=output['sha256']))
        output['sha256']=sha256(normalized).hexdigest()
    proof['metadata_canonicalization']=dict(operation='Trailing whitespace and LF only; GUIDs and field values preserved',outputs=canonical)
    write_changed(receipt,json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
