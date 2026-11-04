"""Record original application inputs and the explicit native texture ownership port."""
from hashlib import sha256
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
SOURCE = '.tools/source-packages/current/884_156692/official-code/'
RUNTIME = 'unity/Assets/Game/Runtime/'
EDITOR = 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/'


def identity(name):
    path = ROOT / name
    return dict(path=name, sha256=sha256(path.read_bytes()).hexdigest())


def main():
    original = [SOURCE + 'framework-firstpass-source/UnsafeAssetProxy.cs']
    original += [SOURCE + 'client-source/' + name for name in (
        'AppearanceAssetManager.cs', 'AppearanceSystem.Runtime/AppearanceOnDemandTextureLoader.cs',
        'AppearanceSystem.Runtime/CharacterPartEntryProvider.cs')]
    runtime = [RUNTIME + 'Official/ProfessionUI/ProfessionAppearanceTextureBindings.cs',
               RUNTIME + 'Offline/ProfessionUI/OfflineProfessionBattleSceneTextures.cs']
    validation = [EDITOR + 'OfficialBattlePlayerModelClipValidation.cs',
                  EDITOR + 'OfficialBattlePlayerModelClipValidation.ResourceRoutes.cs']
    proof = dict(manifest='884_156692', originals=[identity(p) for p in original],
                 runtime=[identity(p) for p in runtime], validation=[identity(p) for p in validation],
                 metadata=[identity(p + '.meta') for p in runtime + validation],
                 generator=identity(Path(__file__).relative_to(ROOT).as_posix()),
                 operation='Keep original application proxy handles, material references and release callbacks. Register exact packaged texture GUIDs and atlas identities without replacing the host UI endpoint. Registrations may end in any order; handles retain their original resource owner.',
                 binding='OfflineProfessionBattleSceneTextures.Register',
                 limits=['Production player factory, host input, disk-save flow and Windows Player remain separate.'])
    target = ROOT / 'assets/reference/battle-animation/player-resource-ownership-source-manifest.json'
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()
