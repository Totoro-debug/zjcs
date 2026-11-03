"""Record original NPC tip event payloads and their saved local projection."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review selected tip player version')
    original = '.tools/source-packages/current/' + version + '/official-code/'
    inputs = ['official-source.json', 'assets/reference/partner-tipdot-source-manifest.json',
        'assets/reference/partner-profile-source-manifest.json', 'assets/reference/partner-talent-player-source-manifest.json',
        'unity/Assets/Official/Partners/partner-rules.json',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftPlayer.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs',
        'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots/PartnerTipDotServices.cs']
    inputs += [original + path for path in ('common-source/Common/Player.cs', 'common-source/Common/PlayerNpcDataWrap.cs',
        'common-source/Common/PlayerNpcReceivedProfileRewardsWrap.cs', 'common-source/Common/NpcMeetType.cs',
        'common-source/MsgPlayerNpc.cs', 'common-source/MsgPlayerNpcRemove.cs',
        'client-source/TipDotNpcCanSendGiftConditionNode.cs', 'client-source/TipDotNpcProfileCanUnlockConditionNode.cs',
        'client-source/TipDotNpcMeetStatusConditionNode.cs', 'client-source/TipDotNpcTalentTreeGiftCanActivePoint.cs')]
    inputs += ['unity/Assets/Official/' + path for path in ('Character/character-progression.json',
        'Character/item-time-limits.json', 'CharacterCreation/wardrobe.json',
        'CharacterCreation/wardrobe-rules.json', 'Character/character-overview.json')]
    source = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotPlayer.cs'
    meta = metadata(source)
    meta.write_text('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    helpers = ['scripts/prepare_partner_tip_player_validation.py', 'scripts/fixtures/partner_tipdot_player_validation.cs']
    proof = dict(manifest=version, inputs={name: sha(ROOT / name) for name in inputs},
        outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (source, meta,
            ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef',
            ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftPlayer.cs')},
        recorder_sha256=sha(Path(__file__)), validation_helpers={name: sha(ROOT / name) for name in helpers},
        operation=['Read the current saved rank, inventory, NPC friendship/received chapters and existing talent node collection.',
            'Keep the original item enum and previous count, NPC class and Auto/Manual type, old/new friendship levels and profile chapter event payloads.',
            'Synchronize only the current complete local state after successful persistence or explicit reload; unchanged state and failed writes emit no success event.',
            'Use the established gift adapter legacy effective friendship XP; detached NPC/receipt/node projections cannot mutate saved state.',
            'Reference the original helper assembly explicitly for ItemType and qualify the existing gift item Quality alias without changing its behavior.',
            'Use the owning item metadata resolver for all item events, including existing generated skill-piece definitions outside the selected static item rows.',
            'This is the saved player boundary only; production graph services, condition subscriptions and formal UI ownership remain pending.'])
    target = ROOT / 'assets/reference/partner-tip-player-source-manifest.json'
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original NPC tip payloads and saved local player')


if __name__ == '__main__': main()
