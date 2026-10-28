"""Record actual native bone/slot views used by original application followers."""
import json
from pathlib import Path
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record


def build():
    folder = ROOT / 'assets/reference/profession-ui'
    parents = [folder / (name + '-source-manifest.json') for name in
               ('native-appearance-graph', 'idle-transform', 'followers', 'character-resources')]
    proofs = [json.loads(p.read_bytes()) for p in parents]
    if any(p['manifest'] != '871_156598' for p in proofs): raise ValueError('Verified native follower versions differ')
    fixture = CACHE / 'typed/idle-transform-validation.json'
    if proofs[1]['fixture'] != record(fixture): raise ValueError('Independent source world references changed')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAppearanceSkeleton.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeFollowerValidation.cs'
    app = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
    result = dict(format=1, manifest='871_156598', prerequisites=[record(p) for p in parents], fixture=record(fixture),
                  runtime=record(runtime), runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
                  validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')),
                  application=[record(app / (name + '.cs')) for name in ('ProfessionFollowerBindings', 'BaseSkeletonFollower', 'SkeletonGraphicFollower', 'SkeletonRendererFollower')],
                  generator=record(Path(__file__)),
                  operation='Expose stable live native bone and slot objects through the migrated original application follower interfaces. Translate source world positions by the original data scale; world axes stay unscaled, world rotation is the actual X-axis angle and scales are actual local values. Skin-clip/part/follower slot views share identity and current attachment/order. Original UI/world followers execute their own parent/external transforms, visibility and lifecycle against these views. Custom nonzero depth requires an explicit validated provider and is not invented; the original zero-depth UI branch needs none.',
                  limitations=['This binds actual native pose objects, not a new physical solver or full SkeletonGraphic.',
                               'Unbound custom slot depth throws; production nonzero custom-depth derivation and actual part/weapon assets remain required.',
                               'Physics/mixing, complete original profession page/input/save and Windows Player remain required.'],
                  validated_scope='Pending actual Direct3D Play Mode native bone/slot views and original follower checks, application graph regression and global/static checks.')
    (folder / 'native-followers-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recorded actual native follower views and original application contracts')
    return result


if __name__ == '__main__':
    build()
