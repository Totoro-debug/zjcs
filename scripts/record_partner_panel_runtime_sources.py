"""Record explicit original panel asset bindings and independent Canvas code."""
import hashlib
import json
import subprocess
from pathlib import Path
from official_apk import ROOT


def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT / 'assets/reference/partner-panel-native-source-manifest.json'
    receipt = json.loads(source.read_bytes())
    if receipt['manifest'] != '884_156692': raise ValueError('Selected panel version differs')
    for key in ('inputs', 'installed_output_sha256', 'support_output_sha256'):
        for name, expected in receipt[key].items():
            if digest(ROOT / name) != expected: raise ValueError('Panel input/output changed: ' + name)
    for value in receipt['script_bindings'].values():
        path = ROOT / value['path']
        if digest(path) != value['sha256'] or digest(Path(str(path) + '.meta')) != value['meta_sha256']:
            raise ValueError('Panel component changed: ' + str(path))
    # This shared adapter also belongs to the original friendship window.
    # Retain a checked Git source for its existing default-constructor behavior
    # while recording the explicit panel-event resolver overload.
    shared_receipt = ROOT / 'assets/reference/partner-friendship-window-source-manifest.json'
    shared = json.loads(shared_receipt.read_bytes())
    shared_path = 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerUiSoundScope.cs'
    original_commit = '93840ba46e95b9d38c8b53a645d7e1c690498849'
    current = digest(ROOT / shared_path)
    if shared['runtime_code'][shared_path] != current:
        raw = subprocess.check_output(['git', 'show', original_commit + ':' + shared_path], cwd=ROOT)
        original_hash = hashlib.sha256(raw).hexdigest()
        prior = shared.get('shared_runtime_adaptations', {}).get('sound_scope', {})
        if shared['runtime_code'][shared_path] != original_hash and not (
            prior.get('original_commit') == original_commit and prior.get('original_sha256') == original_hash):
            raise ValueError('Inspect shared friendship sound adapter changes')
        shared['shared_runtime_adaptations'] = dict(sound_scope=dict(original_commit=original_commit,
            original_sha256=original_hash, adaptation='Retain default friendship click resolver; add an explicit panel event-to-PCM resolver overload and forward original sound-object registration and lifetime calls to the underlying host.'))
        shared['runtime_code'][shared_path] = current
        shared_receipt.write_text(json.dumps(shared, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline'
    paths = [runtime / folder / (name + suffix)
        for folder, name in [('ProfessionUI', 'IProfessionNativeUiPoseSource'),
            ('ProfessionUI', 'ProfessionNativeUiBatchGraphic'), ('ProfessionUI', 'ProfessionNativeUiGraphic'),
            ('ProfessionUI', 'OfflineProfessionNativeSkinClipBindings'),
            ('PartnersUI', 'OfficialPartnerPanelAssetCatalog'), ('PartnersUI', 'PartnerPanelView'),
            ('PartnersUI', 'OfflinePartnerUiSoundScope')]
        for suffix in ('.cs', '.cs.meta')]
    paths += [ROOT / 'scripts' / name for name in ('prepare_partner_panel_validation.py',
        'prepare_partner_panel_host_validation.py', 'partner_panel_language.py',
        'fixtures/partner_panel_host_validation.cs', 'fixtures/partner_panel_native_graphic_validation.cs')]
    output = dict(manifest='884_156692', generator_sha256=digest(Path(__file__)),
        source_receipt_sha256={path.relative_to(ROOT).as_posix(): digest(path) for path in (source, shared_receipt)},
        binding_sha256={path.relative_to(ROOT).as_posix(): digest(path) for path in paths},
        adaptations=['Retain six original deferred player-avatar prototypes with null skeleton assets.',
            'Independent Canvas geometry consumes the existing native pose/mesh implementation and exact atlas materials.',
            'The pose source owns animation evaluation; this backend does not invent transition rules.',
            'Require explicit bindings for unsupported original graphic settings.',
            'Host the complete original three-page window and retain its popup and tab animation controllers.',
            'Scope original language, image, pool and native skin bindings to the window lifetime.',
            'Initialize original dialogue/roster/star-god rows through the real local-service boundaries.',
            'Production dialogue persistence/playback, player avatar animation and graph services remain separate work.'])
    target = ROOT / 'assets/reference/partner-panel-runtime-source-manifest.json'
    target.write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original panel and independent Canvas bindings:', len(paths), flush=True)


if __name__ == '__main__': main()
