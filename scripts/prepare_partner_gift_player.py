"""Record the current-rule player/inventory bridge and its original client evidence."""
import hashlib
import json
from pathlib import Path

from prepare_partner_gift_configuration import method, sha
from prepare_partner_ui_helpers import meta

ROOT = Path(__file__).resolve().parents[1]


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review partner player rules when changing the selected version')
    inputs, methods, outputs = {}, {}, {}
    declarations = {
        'common-source/Common/NpcMeetType.cs': [],
        'common-source/NpcHelper.cs': ['public static bool CanSendGiftToNpc(Player player, int giftId, int npcClassId)',
            'public static IEnumerable<PlayerItemDataWrap> GetAllCanSendToNpcGift(this Player player, int npcClassId)'],
        'common-source/Common/NpcInfoParser.cs': ['private void ProcessNpcExclusiveGift(NpcInfo info)',
            'public bool TryGetNpcByExclusiveGift(int giftId, out int npcId)'],
        'common-source/Common/Player.cs': ['public void FireItemCountUpdate(PlayerItemDataWrap itemData, long preCount, AwardSourceData sourceData)',
            'public void AddNpcFriendship(int npcClassId, long addFriendship)', 'public void SetNpcFriendshipLevel(int npcClassId, int level)'],
        'common-source/PlayerServiceProxyImpl.cs': ['protected override void OperPlayerGiveNpcGiftCallback(int npcClassId, Dictionary<int, long> giftDict, PlayerRemoveItemsResponse rep, int ec)'],
        'common-source/Common/PlayerItemDataWrap.cs': ['public bool CanExpire()', 'public Quality GetQuality()'],
    }
    for name, signatures in declarations.items():
        source = ROOT / '.tools/source-packages/official-code' / name
        canonical = ROOT / '.tools/source-packages/current' / version / 'official-code' / name
        raw = source.read_bytes()
        if raw != canonical.read_bytes():
            raise ValueError('Selected player source differs: ' + name)
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        methods[name] = {signature: sha(method(text, signature).encode()) for signature in signatures}
    for name in ('unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryOperations.cs',
            'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
            'unity/Assets/Game/Runtime/Official/Partners/NpcGiftRules.cs',
            'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftConfiguration.cs',
            'assets/reference/partner-gift-configuration-source-manifest.json', 'unity/Assets/Official/Partners/partner-rules.json'):
        inputs[name] = sha((ROOT / name).read_bytes())
    for name in ('OfflinePartnerGiftInventoryRules', 'OfflinePartnerGiftPlayer'):
        path = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI' / (name + '.cs')
        meta(path)
        metadata = Path(str(path) + '.meta')
        metadata.write_bytes(b'\n'.join(line.rstrip() for line in metadata.read_bytes().splitlines()) + b'\n')
        for item in (path, metadata):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()), source_sha256=inputs,
        methods=methods, outputs=outputs,
        validation_source_sha256={name: sha((ROOT / name).read_bytes()) for name in (
            'scripts/prepare_partner_gift_player_validation.py', 'scripts/fixtures/partner_gift_player_validation.cs')},
        adaptations=['Current selected tables supply item types, exclusive gifts, preferences, rank caps and cumulative friendship.',
            'NPC/item views follow Save.Player replacements and retain stable local class-ID identities.',
            'Atomic local gift command reuses ApplyCharacterMutation and OfflineInventoryOperations.Consume; no server implementation is claimed.',
            'Notifications run after saving, carry previous item count and previous/new friendship levels; external reload publishes actual differences.',
            'Existing local overflow-confirmation and arithmetic range checks are retained.',
            'Custom-title reading/writing and function access remain explicit host ports; no moderation policy is inferred.'])
    (ROOT / 'assets/reference/partner-gift-player-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded current partner inventory/player bridge and original client evidence.')


if __name__ == '__main__':
    main()
