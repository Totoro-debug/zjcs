"""Record original node/level methods and the approved local refresh/ack boundary."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review local conversation transactions for selected version')
    sources = {}
    for relative in ('common-source/Common/PlayerNpcDialogueManager.cs', 'common-source/Common/PlayerNpcDialogueDataWrap.cs',
                     'common-source/Common/NpcFriendshipLevelInfoParser.cs', 'client-source/NpcDialoguePanel.cs',
                     'client-source/NpcDialoguePanelOptionItem.cs', 'client-source/HomeNpcChatEntry.cs'):
        path = ROOT / '.tools/source-packages/official-code' / relative
        if path.read_bytes() != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected original transaction source differs: ' + relative)
        sources[path.relative_to(ROOT).as_posix()] = sha(path)
    original = (ROOT / '.tools/source-packages/official-code/common-source/Common/NpcFriendshipLevelInfoParser.cs').read_text('utf-8-sig')
    signature = 'public int GetFriendshipLevelByExp(int id, long friendshipExp, SubRank subRank)'
    body, = re.findall(r'\t' + re.escape(signature) + r'\n\t\{.*?\n\t\}', original, re.S)
    method_sha = hashlib.sha256(body.encode()).hexdigest()
    expected = body.replace(signature, 'private int GetFriendshipLevelByExp(int id, long friendshipExp, int subRank)')
    expected = expected.replace('Dictionary<int, NpcFriendshipLevelInfo> dictionary = Get(id);',
        'friendshipLevels.TryGetValue(id, out var dictionary);')
    expected = expected.replace('NpcFriendshipLevelInfo', 'FriendshipLevelInfo')
    service = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflinePartnerConversationService.cs'
    actual, = re.findall(r'        private int GetFriendshipLevelByExp\([^\n]+\)\n        \{.*?\n        \}',
        service.read_text('utf8'), re.S)
    if expected.split() != actual.split(): raise ValueError('Original friendship method differs beyond the declared type/configuration adaptations')
    outputs = ['unity/Assets/Game/Runtime/Offline/Progression/OfflinePartnerConversationService.cs' + suffix
        for suffix in ('', '.meta')]
    outputs.append('unity/Assets/Game/Runtime/Offline/Progression/OfflinePartnerConversationPlayer.cs')
    outputs.extend('unity/Assets/Game/Runtime/Offline/Progression/' + name for name in (
        'OfflineLocalActorIdentities.cs', 'OfflineLocalActorIdentities.cs.meta',
        'OfflineCharacterPropertyProgress.cs'))
    dependencies = ['assets/reference/partner-conversation-rules-source-manifest.json',
        'assets/reference/partner-conversation-day-limits-source-manifest.json',
        'assets/reference/partner-conversation-world-source-manifest.json',
        'assets/reference/partner-conversation-conditions-source-manifest.json',
        'unity/Assets/Official/Partners/partner-rules.json',
        'unity/Assets/Official/Partners/partner-dialogue-rules.json',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryOperations.cs']
    validations = ['scripts/prepare_partner_conversation_transaction_validation.py',
        'scripts/fixtures/partner_conversation_transaction_validation.cs',
        'scripts/fixtures/partner_local_actor_identity_validation.cs']
    receipt = dict(manifest=version, source_sha256=sources,
        original_friendship_method_sha256=method_sha,
        dependency_sha256={p: sha(ROOT / p) for p in dependencies},
        installed_output_sha256={p: sha(ROOT / p) for p in outputs},
        validation_source_sha256={p: sha(ROOT / p) for p in validations}, recorder_sha256=sha(Path(__file__)),
        original=['Use original NPC Dialogues order, node unlock/chain conditions, Auto type and original daily check/record methods.',
            'Use original Create and SetCurrentNodeAndUpdateStatus; capture their update notification until disk commit.',
            'Verify the friendship level method token-for-token against selected original source after row/rank/lookup adaptations.',
            'Resolve conditions from the cloned transaction player while world/story facts remain live committed session inputs.'],
        confirmed_local_boundary=['Only an explicit partner panel opening requests eligible not-yet-started conversations.',
            'No automatic refresh on creation/load/friendship/condition change or the daily clock boundary.',
            'Acknowledge only an original eligible successor; grant its configured item/friendship reward with history/status in one transaction.',
            'Persist a deterministic per-dialogue/node receipt; review and repeated acknowledgment cannot duplicate rewards.',
            'Persist local player/NPC identity mappings and a checked high-water sequence; allocate new dialogue IDs inside the transaction clone.',
            'Adopt existing conversation identities and retain reward-receipt IDs when migrating a legacy profile.',
            'Original function availability and post-commit presentation remain required host dependencies.'],
        validation_scope=['Actual complete dialogue 2021, configured item 110 x3, same-day chain 2022, per-NPC daily quota and real disk rollback.',
            'All selected official RewardFriendship fields are zero; the nonzero branch uses one clearly marked, restored in-memory synthetic fixture value.',
            'Production identity allocator, legacy adoption, reload, collision/exhaustion rejection and actual creation/migration disk rollback.',
            'Native nonempty address-book input, concrete Home function binding and playback window are not exercised.'])
    path = ROOT / 'assets/reference/partner-conversation-transactions-source-manifest.json'
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original node/level rules and user-confirmed local refresh/reward transactions', flush=True)


if __name__ == '__main__': main()
