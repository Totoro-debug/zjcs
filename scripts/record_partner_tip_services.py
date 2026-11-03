"""Record original parser/condition/node boundaries for the saved tip services."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review selected tip configuration version')
    original = '.tools/source-packages/current/' + version + '/official-code/'
    inputs = ['official-source.json', 'unity/Assets/Official/Partners/partner-rules.json',
        'assets/reference/partner-condition-handler-source-manifest.json', 'assets/reference/partner-tip-player-source-manifest.json',
        'assets/reference/partner-talent-source-manifest.json', 'assets/reference/partner-profile-source-manifest.json',
        'assets/reference/partner-gift-configuration-source-manifest.json',
        'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots/PartnerTipDotServices.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTalentPlayer.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.Partners.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterCatalog.Partners.cs']
    inputs += [original + path for path in ('common-source/Common/NpcInfoParser.cs',
        'common-source/NpcHelper.cs', 'common-source/Common/NpcProfileInfoParser.cs',
        'common-source/Common/NpcTalentItemInfoParser.cs', 'common-source/Common/NpcTalentTreeNpcMapInfoParser.cs',
        'client-source/TipDotNpcProfileCanUnlockConditionNode.cs', 'client-source/TipDotNpcTalentTreeGiftCanActivePoint.cs',
        'client-source/TipDotNpcTalentTreeGiftCanTransToTalentPoint.cs', 'client-source/NpcTalentTreeUIUtils.cs')]
    inputs += ['unity/Assets/Official/' + path for path in ('Character/character-progression.json', 'Character/item-time-limits.json',
        'CharacterCreation/wardrobe.json', 'CharacterCreation/wardrobe-rules.json', 'Character/character-overview.json')]
    target = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotServices.cs'
    meta = metadata(target)
    meta.write_text('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    helpers = ['scripts/prepare_partner_tip_services_validation.py', 'scripts/fixtures/partner_tipdot_services_validation.cs']
    result = dict(manifest=version, inputs={path: sha(ROOT / path) for path in inputs},
        outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (target, meta)}, recorder_sha256=sha(Path(__file__)),
        validation_helpers={path: sha(ROOT / path) for path in helpers},
        operation=['Bind the original tip-node parser and condition-handler slots to existing selected profile/talent/gift rules and the current saved player.',
            'Keep stable profile/node/graph identities, original chapter/node ordering, exact node condition context and original missing parser defaults.',
            'NpcInfoParser.GetNpcs(ignoreType) excludes bosses and includes star gods; omit only the user-confirmed closed collaboration IDs from visible enumeration/profile entries.',
            'Meet status reuses the current-rule progression service; condition subscriptions reuse the original dispatcher and owning lifetime.',
            'Achievement-key lookup remains a required explicit owner provider, preserving its original NPC ID and normal/star-god arguments; achievement progress/claims and formal full-window ownership are not supplied by this adapter.',
            'Existing historical character inputs retain their recorded 854 identity; selected partner predicates/configuration remain 884. No new assets, save fields, dialogue triggers or reward rules.'])
    path = ROOT / 'assets/reference/partner-tip-services-source-manifest.json'
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded saved partner tip configuration services and original rule dependencies')


if __name__ == '__main__': main()
