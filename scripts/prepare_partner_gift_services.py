"""Record the current local implementation of the original gift UI service boundary."""
import json
from pathlib import Path

from prepare_partner_gift_configuration import method, sha

ROOT = Path(__file__).resolve().parents[1]


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review partner gift services for the newly selected version')
    inputs, methods, outputs = {}, {}, {}
    for relative, signatures in {
        'client-source/NpcDetailPanelGiftContent.cs': ['private void DoGiveGift()', 'private void OnCustomFriendShipTag()'],
        'common-source/Common/LanguageUtils.cs': ['public static string GetNpcFriendshipTagNameKey(int tagId)'],
        'common-source/Common/ItemInfo.cs': ['public string GetIconPath(Sex sex = Sex.None)'],
        'common-source/Common/NpcAdventureSettings.cs': ['public bool IsTraveNpcUnlock(Player player)', 'public bool IsUnlockTraveNpcProp(Player player, PropType propType)'],
        'framework-firstpass-source/ResourceLoaderExtensions.cs': [],
    }.items():
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Current gift service source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        source = raw.decode('utf-8-sig').replace('\r\n', '\n')
        methods[relative] = {signature: sha(method(source, signature).encode()) for signature in signatures}
    for relative in ('assets/reference/partner-gift-configuration-source-manifest.json',
        'assets/reference/partner-gift-player-source-manifest.json',
        'assets/reference/partner-gift-presentation-assets-source-manifest.json',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftPlayer.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerFriendshipTags.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerFriendshipProperties.cs'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    for name in ('OfflinePartnerGiftUiServices', 'OfficialPartnerGiftPageAssetCatalog',
                 'OfflinePartnerGiftRuntimeBindings', 'OfflinePartnerItemInfo'):
        for suffix in ('.cs', '.cs.meta'):
            relative = 'unity/Assets/Game/Runtime/Offline/PartnersUI/' + name + suffix
            outputs[relative] = sha((ROOT / relative).read_bytes())
    relative = 'unity/Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef'
    outputs[relative] = sha((ROOT / relative).read_bytes())
    validations = ('scripts/prepare_partner_gift_current_validation.py', 'scripts/fixtures/partner_gift_current_steps.cs',
        'scripts/fixtures/partner_gift_controller_validation.cs', 'scripts/fixtures/partner_gift_preferences_validation.cs',
        'unity/Assets/Game/Editor/Home/HomeAcceptanceLauncher.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs')
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=inputs, methods=methods, outputs=outputs,
        validation_source_sha256={relative: sha((ROOT / relative).read_bytes()) for relative in validations},
        adaptations=['Current configuration/player/property ports supply the original full gift controller.',
            'Copy selected quantities before the deferred local response; original DoGiveGift has already performed overflow confirmation.',
            'Atomic local command publishes player events before the original success callback; file write failures report failure without success.',
            'Original sprite prefix, relationship language key and travel-property gate are retained.',
            'Gift ItemInfo.Icon follows the original no-PartInfo branch and reads RawIcon; original culture number formatting is bound before ItemIcon initialization.',
            'Explicit Resources references package the native gift subtree, all available preference images, relationship backgrounds, attachment roots and original fonts.'],
        scope='Native gift subtree and local transactions; formal complete partner window routing, production popup contents and Windows Player remain pending.')
    (ROOT/'assets/reference/partner-gift-services-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Recorded current native gift UI/local transaction service boundary.')


if __name__ == '__main__':
    main()
