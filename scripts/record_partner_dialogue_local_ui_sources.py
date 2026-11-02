"""Record the original address-book consumption and saved-state adapter."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review dialogue UI binding for a new source version')
    sources = {}
    for relative in ('client-source/NpcPanelDialogueList.cs', 'client-source/NpcPanelDialogueListNpcItem.cs',
                     'client-source/NpcPanelDialogueListDialogueItem.cs', 'common-source/Common/PlayerNpcDialogueManager.cs'):
        path = ROOT / '.tools/source-packages/official-code' / relative
        if path.read_bytes() != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected dialogue UI consumer differs: ' + relative)
        sources[path.relative_to(ROOT).as_posix()] = sha(path)
    outputs = ['unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerDialogueUiServices.cs' + suffix
        for suffix in ('', '.meta')]
    dependencies = ['assets/reference/partner-conversation-local-source-manifest.json',
        'assets/reference/partner-dialogue-list-ui-source-manifest.json',
        'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/PartnerDialogueUiServices.cs',
        'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/NpcPanelDialogueList.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/MovementType.cs']
    validations = ['scripts/prepare_partner_dialogue_local_ui_validation.py',
        'scripts/fixtures/partner_dialogue_local_ui_validation.cs']
    receipt = dict(manifest=version, source_sha256=sources,
        dependency_sha256={p: sha(ROOT / p) for p in dependencies},
        installed_output_sha256={p: sha(ROOT / p) for p in outputs},
        validation_source_sha256={p: sha(ROOT / p) for p in validations},
        recorder_sha256=sha(Path(__file__)),
        operation=['Read actual committed profile conversations and their original NPC/player/dialogue identities.',
            'Publish each added dialogue separately so the original first-NPC full scan and subsequent append cannot duplicate rows.',
            'Keep mutable original UI view references across saved updates and unchanged reloads.',
            'Unpublish removed rows before notification, retaining their NPC view through the original refresh callback.',
            'Validate the complete original-node and NPC identity relation before publishing any delta.',
            'Resolve node conditions using the actual original conversation configuration and saved player adapter.',
            'Require explicit native presentation services; this adapter supplies no substitute UI or avatars.'],
        limitations=['The Play Mode fixture validates state/notifications and real disk rollback only.',
            'The production native avatar, nonempty panel interaction, playback window, trigger and reward transaction remain pending.'])
    destination = ROOT / 'assets/reference/partner-dialogue-local-ui-source-manifest.json'
    destination.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original address-book consumers and committed-profile adapter', flush=True)


if __name__ == '__main__': main()
