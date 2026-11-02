"""Record local profile, original self-icon and live avatar widget binding."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(relative):
    return dict(path=relative, sha256=hashlib.sha256((ROOT / relative).read_bytes()).hexdigest())


def main():
    dependencies = ['assets/reference/partner-main-player-source-manifest.json',
        'assets/reference/partner-player-info-source-manifest.json',
        'assets/reference/partner-player-health-source-manifest.json',
        'assets/reference/partner-ui-player-bindings-source-manifest.json']
    dependencies.append('assets/reference/partner-panel-native-source-manifest.json')
    if any(json.loads((ROOT / path).read_bytes())['manifest'] != '884_156692' for path in dependencies):
        raise ValueError('Use the selected current player data and avatar bindings')
    legacy = json.loads((ROOT / 'assets/reference/profession-ui/player-info-source-manifest.json').read_bytes())
    retained = []
    for name in ('PlayerIconMe', 'PlayerInfoData'):
        port, = [row for row in legacy['ports'] if row['runtime']['path'].endswith('/' + name + '.cs')]
        selected = identity('.tools/source-packages/current/884_156692/official-code/client-source/' + name + '.cs')
        if selected['sha256'] != port['source']['sha256']:
            raise ValueError('Review changed selected original self-icon source: ' + name)
        if identity(port['runtime']['path']) != port['runtime']:
            raise ValueError('Review changed retained original widget port: ' + name)
        retained.append(dict(selected_source=selected, historical_source=port['source'],
            runtime=identity(port['runtime']['path'])))
    runtime = 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerPlayerInfoBindings.cs'
    audio_path = 'unity/Assets/Official/Partners/UI/Card/Audio/Main_Cursor_Normal.wav'
    panel = json.loads((ROOT / dependencies[-1]).read_bytes())
    if identity(audio_path)['sha256'] != panel['installed_output_sha256'][audio_path]:
        raise ValueError('Original partner common click PCM differs from the selected source receipt')
    result = dict(manifest='884_156692', dependencies=[identity(path) for path in dependencies],
        retained_original_widgets=retained,
        reused_audio=[identity(audio_path), identity(audio_path + '.meta'),
            identity('unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionSoundHost.cs')],
        outputs=[identity(runtime), identity(runtime + '.meta')],
        helpers=[identity('scripts/prepare_partner_local_player_widget_validation.py'),
            identity('scripts/fixtures/partner_local_player_widget_validation.cs')],
        generator=identity('scripts/record_partner_local_player_widgets.py'),
        operation=[
            'Use the committed local source for MainPlayer, OnPlayerUpdate and PlayerInfoData. Retain original CultureTextArg translation flags and CultureNumberArg.Compact.',
            'Delegate existing image/configuration, NPC, team and popup services without assigning replacement rules or rendering new UI.',
            'Only the two retained source files listed here are byte-identical to the selected current originals; other historical receipts retain their separate version identities.',
            'The actual original widget is bound before activation to the current native avatar and the live local player health service.',
            'Simulated original pointer input plays event 3772241779 through the existing native sound host using the selected partner PCM, not the historical profession audio catalog.',
            'No serialized prefab layout, mask, slider, text, state table or artwork is changed. No full asset snapshot is produced.',
            'Popup validation covers the original pointer and callback boundary; the full profile popup remains a separate host integration requirement.'])
    target = ROOT / 'assets/reference/partner-local-player-widgets-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded actual local player widget binding and retained original source identities.')


if __name__ == '__main__':
    main()
