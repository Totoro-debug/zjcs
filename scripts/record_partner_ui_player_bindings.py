"""Record the local binding of selected original partner player components."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(relative):
    return dict(path=relative, sha256=hashlib.sha256((ROOT / relative).read_bytes()).hexdigest())


def main():
    dependencies = [
        'assets/reference/partner-ui-appearance-source-manifest.json',
        'assets/reference/partner-ui-appearance-assets-source-manifest.json',
        'assets/reference/partner-ui-high-textures-source-manifest.json',
        'assets/reference/partner-player-pose-source-manifest.json']
    if any(json.loads((ROOT / path).read_bytes())['manifest'] != '884_156692' for path in dependencies):
        raise ValueError('Use the selected current UI application and native assets')
    runtime = 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionUiPlayerBindings.cs'
    result = dict(manifest='884_156692', dependencies=[identity(path) for path in dependencies],
        outputs=[identity(runtime), identity(runtime + '.meta')],
        helpers=[identity('scripts/prepare_partner_main_avatar_validation.py'),
                 identity('scripts/fixtures/partner_main_avatar_validation.cs')],
        generator=identity('scripts/record_partner_ui_player_bindings.py'),
        operation=[
            'Read the original serialized SkinTextureAssetGuid and TextureLoaderGuid from each current AppearanceUI. Require exact identities from the selected native catalog; retain the original proxy objects.',
            'Bind the original AppearanceUI, AppearanceCharacter, AppearanceUIPartHandler and PlayerAnimationAppearance before activation and skeleton initialization.',
            'Use the profile-owned player appearance, part loader and material manager. The original component lifecycle owns renderer and texture requests.',
            'A repeated visit leaves an already bound original animation track intact. Destroyed Unity owners are pruned before binding rented or reopened trees.',
            'The default producer uses the existing independent native player pose. Explicit specialized pose producers remain injectable without changing original materials or layout.',
            'No original payload, prefab layout, serialized exclusions, initial animation or freeze setting is changed. No new art or full asset snapshot is produced.',
            'Earlier receipts retain their historical source identities. This receipt records the current window binding and validation helper.'])
    target = ROOT / 'assets/reference/partner-ui-player-bindings-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original partner UI player binding identities.')


if __name__ == '__main__':
    main()
