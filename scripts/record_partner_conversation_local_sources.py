"""Record selected original dialogue fields and the local persistence adaptation."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review conversation save migration for a new source version')
    sources = {}
    for name in ('PlayerNpcDialogueData', 'PlayerNpcDialogueDataWrap'):
        relative = 'common-source/Common/' + name + '.cs'
        path = ROOT / '.tools/source-packages/official-code' / relative
        if path.read_bytes() != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected conversation source differs: ' + name)
        sources[path.relative_to(ROOT).as_posix()] = sha(path)
    outputs = ['unity/Assets/Game/Runtime/Offline/Progression/' + name + suffix
        for name in ('OfflineNpcConversationProgress', 'OfflinePartnerConversationPlayer') for suffix in ('.cs', '.cs.meta')]
    outputs.append('unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs')
    dependencies = ['assets/reference/partner-conversation-rules-source-manifest.json',
        'assets/reference/partner-conversation-conditions-source-manifest.json',
        'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationRules.cs',
        'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationConditionRules.cs']
    validations = ['scripts/prepare_partner_conversation_save_validation.py',
        'scripts/fixtures/partner_conversation_save_validation.cs',
        'scripts/fixtures/partner_gift_transaction_validation.cs',
        'scripts/fixtures/partner_roster_transaction_validation.cs']
    receipt = dict(manifest=version, source_sha256=sources,
        dependency_sha256={p: sha(ROOT / p) for p in dependencies},
        output_sha256={p: sha(ROOT / p) for p in outputs},
        validation_source_sha256={p: sha(ROOT / p) for p in validations},
        generator_sha256=sha(Path(__file__)),
        operation=['Persist the eight original dialogue fields without the MongoDB identity.',
            'Keep the separate original NPC identity and the offline NPC class mapping.',
            'Represent DateTime as ticks plus Kind for JsonUtility, deep-copy node history and preserve repeated nodes.',
            'Normalize absent legacy conversation collections to empty; reject malformed identities, history tails and duplicate dialogue IDs.',
            'Read actual committed friendship, base level, inventory, map accessibility, activated regions, stories and ended dialogue records.',
            'Keep raw map progress and goddess identity/activation as explicit actual-world service dependencies.'],
        limitations=['No creation trigger, daily-limit decision, reward transaction, production dialogue notification or playback UI is installed by this adaptation.'])
    destination = ROOT / 'assets/reference/partner-conversation-local-source-manifest.json'
    destination.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original dialogue field schema and local profile adaptation', flush=True)


if __name__ == '__main__': main()
