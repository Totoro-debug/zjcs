"""Record the current-save boundary for selected original talent predicates."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    if json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '884_156692':
        raise ValueError('Review selected talent source version')
    source = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTalentPlayer.cs'
    meta = metadata(source)
    meta.write_text('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    inputs = ['official-source.json', 'assets/reference/partner-talent-source-manifest.json',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfficialPartnerAssetCatalog.cs',
        'unity/Assets/Official/Partners/partner-rules.json']
    inputs += ['.tools/source-packages/current/884_156692/official-code/common-source/Common/' + name + '.cs'
        for name in ('Player', 'PlayerItemManagerExt', 'ItemType', 'PlayerSingleNpcTalentTreeData')]
    helpers = ['scripts/fixtures/partner_talent_player_validation.cs', 'scripts/prepare_partner_talent_player_validation.py']
    proof = dict(manifest='884_156692', inputs={relative: sha(ROOT / relative) for relative in inputs},
        outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (source, meta)},
        validation_helpers={relative: sha(ROOT / relative) for relative in helpers}, recorder_sha256=sha(Path(__file__)),
        operation=['Bind original talent predicates to the current Save.Player and its existing NpcTalentTrees, NpcFriendships and inventory fields.',
            'Read the provider on every query, return detached node IDs, and preserve original absent-tree zero experience / empty-node observations without mutating a save.',
            'Project only original ItemType.NpcGift (328) inventory, independently of friendship gift preferences and NPC fragments.',
            'No new save schema, activation or gift-conversion transaction, artwork, prefab, layout or full asset snapshot.'])
    path = ROOT / 'assets/reference/partner-talent-player-source-manifest.json'
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded actual saved partner talent state boundary')


if __name__ == '__main__': main()
