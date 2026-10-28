"""Record actual native appearance objects behind migrated application contracts."""
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record


def build():
    folder = ROOT / 'assets/reference/profession-ui'
    parents = [folder / (name + '-source-manifest.json') for name in
               ('skeleton-data', 'runtime-timelines', 'character-textures', 'idle-transform')]
    data, timelines, textures, transform = [json.loads(p.read_bytes()) for p in parents]
    if any(p['manifest'] != '871_156598' for p in (data, timelines, textures, transform)):
        raise ValueError('Verified original native appearance versions differ')
    fixture = CACHE / 'typed/idle-transform-validation.json'
    if transform['fixture'] != record(fixture): raise ValueError('Verified pre-physics world references changed')
    for proof, key in ((data, 'runtime'), (timelines, 'runtime'), (textures, 'runtime')):
        row = proof[key]
        if record(ROOT / row['path']) != row: raise ValueError('Verified actual native resource binding changed')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAppearanceSkeleton.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeAppearanceGraphValidation.cs'
    native_folder = runtime.parent
    paths = [runtime, native_folder / 'ProfessionNativeSkin.cs', native_folder / 'ProfessionNativeSkinSlots.cs',
             native_folder / 'ProfessionNativeMeshBatches.cs', validation]
    app_folder = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
    result = dict(format=1, manifest='871_156598', prerequisites=[record(p) for p in parents], fixture=record(fixture), resolved_preview=record(CACHE / 'typed/native-preview-appearance.json'),
                  runtime=record(runtime), runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
                  outputs=[record(p) for p in paths for p in (p, p.with_name(p.name + '.meta'))],
                  application=[record(app_folder / n) for n in ('AppearanceSpine.cs', 'ProfessionAppearanceSpineBindings.cs',
                               'ProfessionAppearancePartBindings.cs', 'ProfessionAppearanceSkinClipBindings.cs', 'ProfessionAppearancePreview.cs', 'ProfessionAppearanceResolver.cs', 'AppearanceCharacter.cs', 'AppearanceMaterialUtils.cs')],
                  generator=record(Path(__file__)),
                  operation='Bind migrated original application skin composition/setup/cache methods to actual independent native bone, slot, skin and attachment objects. Keep source attachment identities/default fallback and shared draw-order slots. Actual packaged scalar/discrete/IK/transform stages feed the explicitly named pre-physics sampler. Original default builder, local snapshot, profession preview and AppearanceCharacter produce complete base/facial/hair skins and dye offsets for four native GPU diagnostics; original AppearanceSpine.SetDye applies their actual channel values. Batch geometry uses real selected-skin attachments and source textures.',
                  limitations=['This is the pre-physics native object graph. It does not freeze, bake or substitute unimplemented physical motion.',
                               'The validation asset identity marker and derived appearance owner exercise application methods; production skeleton asset/resource loading and SkeletonGraphic remain unbound.',
                               'Physics, mixed animation, live followers/parts, full original native profession page/input/save and Windows Player remain required. Diagnostic renders are not full-page or original SDK pose references.'],
                  validated_scope='Pending actual Play Mode application/native object graph, combined world references and GPU diagnostic inspection.')
    (folder / 'native-appearance-graph-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recorded actual native appearance graph and original application contracts')
    return result


if __name__ == '__main__':
    build()
