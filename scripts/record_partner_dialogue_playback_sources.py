"""Record original playback consumers, native bindings and local transaction bridge."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review playback binding for a new source selection')
    outputs = ['unity/Assets/Game/Runtime/Offline/PartnersUI/' + name + suffix
        for name in ('OfflinePartnerDialoguePlaybackServices.cs', 'PartnerDialoguePlaybackView.cs')
        for suffix in ('', '.meta')]
    outputs += ['unity/Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef',
        'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/PartnerDialogueUiServices.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerDialogueUiServices.cs']
    dependencies = ['assets/reference/partner-dialogue-playback-ui-source-manifest.json',
        'assets/reference/partner-dialogue-playback-native-source-manifest.json',
        'assets/reference/partner-dialogue-local-ui-source-manifest.json',
        'assets/reference/partner-conversation-transactions-source-manifest.json',
        'unity/Assets/Game/Runtime/Official/StoryBubbles/TMP_TypeWriterEffect.cs']
    validations = ['scripts/prepare_partner_dialogue_playback_validation.py',
        'scripts/fixtures/partner_dialogue_playback_validation.cs']
    receipt = dict(manifest=version, installed_output_sha256={p: sha(ROOT / p) for p in outputs},
        dependency_sha256={p: sha(ROOT / p) for p in dependencies},
        validation_source_sha256={p: sha(ROOT / p) for p in validations}, recorder_sha256=sha(Path(__file__)),
        scope=['Explicit ports bind original playback controllers to committed stable dialogue views and local acknowledgment transactions.',
            'Copy original player identity and node history into the stable view before publishing each update; retain the history list identity.',
            'Handle disk IO/access failure as a completed error acknowledgment without publishing uncommitted state.',
            'The native window owner binds controllers, original row pool, localized text, images and original sound scope before initialization/activation.'],
        validation_scope=['Exact original main prefab and external row import; actual 2021 option order/text keys and native simulated pointer input.',
            'Actual saved legal branch, failed-write rollback/error completion, stale option rejection, stable history references and disk reload.',
            'Actual original option hiding, award-handler subscription lifetime and selected official PCM.',
            'Full transcript automatic playback, avatar presentation, award popup, hyper-link navigation, Home entry and Windows Player are not covered by this bounded option check.'])
    (ROOT / 'assets/reference/partner-dialogue-playback-runtime-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded selected original playback bindings and local acknowledgment bridge', flush=True)


if __name__ == '__main__': main()
