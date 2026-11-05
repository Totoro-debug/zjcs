"""Record the original application inputs and native battle-player service binding."""
from hashlib import sha256
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = 'unity/Assets/Game/Runtime/Offline/ProfessionUI/'
EDITOR = 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/'
PROOFS = 'assets/reference/battle-animation/'


def identity(name):
    return dict(path=name, sha256=sha256((ROOT / name).read_bytes()).hexdigest())


def main():
    source = '.tools/source-packages/current/884_156692/'
    manifest = json.loads((ROOT / source / 'code-manifest.json').read_bytes())
    assembly = source + 'official-code/managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies']['Assembly-CSharp.dll']['decoded_sha256']
    runtime = [RUNTIME + name + '.cs' for name in (
        'OfflineProfessionBattlePlayerResources', 'ProfessionNativeBattlePlayerActor',
        'OfflineProfessionScenePlayerBindings', 'OfflineProfessionAppearanceBindings')]
    validation = [EDITOR + 'OfficialBattlePlayerModelClipValidation' + suffix + '.cs'
                  for suffix in ('', '.PlayerActor', '.ResourceService')]
    proof = dict(manifest=manifest['manifest'], assembly=identity(assembly),
        originals=[identity(source + 'official-code/client-source/' + name) for name in (
            'AppearanceAssetManager.cs', 'AppearanceSystem.Runtime/CharacterPartEntryProvider.cs')],
        inputs=[identity(PROOFS + name + '-source-manifest.json') for name in (
            'player-resource-ownership', 'player-scene-bindings', 'player-animation-binding',
            'renderer-parts', 'player-effects')],
        runtime=[identity(p) for p in runtime], validation=[identity(p) for p in validation],
        metadata=[identity(p + '.meta') for p in runtime + validation],
        generator=identity(Path(__file__).relative_to(ROOT).as_posix()),
        binding='OfflineProfessionBattlePlayerResources.Create',
        operation='Bind verified original Game actors to read-only local player projections. Own the original part pool and appearance asset manager, real low-memory subscription, explicit Low body/High weapon routes and complete original effect catalog. Update shared cache timers once per Unity frame without advancing actors. Dispose actors, pool, manager, routes and texture owner in that order.',
        limits=['Formal battle view/controller and host entry, UI input, disk-save flow and Windows Player remain separate.'])
    target = ROOT / PROOFS / 'player-resource-service-source-manifest.json'
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()
