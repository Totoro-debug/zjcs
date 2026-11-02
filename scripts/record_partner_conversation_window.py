"""Record the production conversation source bound to original window opening."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    return dict(path=path, sha256=hashlib.sha256((ROOT / path).read_bytes()).hexdigest())


def main():
    dependencies = ['partner-conversation-transactions-source-manifest.json',
        'partner-dialogue-local-ui-source-manifest.json', 'partner-dialogue-playback-native-source-manifest.json',
        'partner-panel-runtime-source-manifest.json', 'partner-npc-icons-source-manifest.json']
    for name in dependencies:
        receipt = json.loads((ROOT / 'assets/reference' / name).read_bytes())
        if receipt['manifest'] != '884_156692':
            raise ValueError('Review selected source version: ' + name)
    runtime = 'unity/Assets/Game/Runtime/Offline/PartnersUI/'
    helpers = ['scripts/prepare_partner_conversation_window_validation.py',
        'scripts/prepare_partner_npc_shared_validation.py', 'scripts/prepare_partner_npc_icon_validation.py',
        'scripts/fixtures/partner_npc_icon_validation.cs', 'scripts/fixtures/partner_panel_host_validation.cs']
    result = dict(manifest='884_156692',
        dependencies=[identity('assets/reference/' + name) for name in dependencies],
        outputs=[identity(runtime + name + suffix) for name in
            ['OfflinePartnerConversationUiSession.cs', 'PartnerPanelView.cs'] for suffix in ['', '.meta']],
        reused_runtime=[identity(runtime + name + '.cs') for name in
            ['OfflinePartnerDialogueUiServices', 'OfflinePartnerDialoguePlaybackServices']],
        helpers=[identity(path) for path in helpers], generator=identity('scripts/record_partner_conversation_window.py'),
        operation=[
            'Compose the existing selected conversation player, atomic transactions, stable saved UI views and original playback boundary.',
            'Bind preparation to the original full partner window Open before activating controls. Constructing the UI session does not create records or install clock/state hooks.',
            'Use previously persisted local actor mappings and original NPC order, conditions and day limits. Publish committed source deltas before the original controls subscribe.',
            'Reject rebinding a window to another source or opening transaction. An already open window does not run preparation again.',
            'Validation uses the actual full three-page window, original NPC icon and PCM assets, real saved records and simulated pointer input. No hand-created conversation record is inserted.',
            'No original prefab, artwork, source data or historical receipt is modified. No full asset snapshot is produced.'])
    target = ROOT / 'assets/reference/partner-conversation-window-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded actual original window opening and saved conversation composition')


if __name__ == '__main__':
    main()
