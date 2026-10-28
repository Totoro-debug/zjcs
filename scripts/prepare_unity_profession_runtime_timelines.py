"""Record actual packaged slot/draw-order/idle-IK bindings and independent references."""
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record


def build():
    folder = ROOT / 'assets/reference/profession-ui'
    data_path = folder / 'skeleton-data-source-manifest.json'
    data = json.loads(data_path.read_bytes())
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs'
    if data['manifest'] != '871_156598' or data['runtime'] != record(runtime):
        raise ValueError('Verified packaged skeleton data/runtime binding changed')
    for row in data['inputs']:
        for key in ('receipt', 'skeleton', 'atlas'):
            if record(ROOT / row[key]['path']) != row[key]:
                raise ValueError('Original source data changed: ' + key)
    for row in data['outputs']:
        if record(ROOT / row['file']['path']) != row['file']:
            raise ValueError('Original packaged skeleton payload changed')
    references = []
    for stem, fixture in (('slot-pose', 'slot-pose-validation.json'), ('idle-ik', 'idle-ik-validation.json')):
        proof_path = folder / (stem + '-source-manifest.json'); proof = json.loads(proof_path.read_bytes()); path = CACHE / 'typed' / fixture
        if proof['manifest'] != '871_156598' or proof['fixture'] != record(path):
            raise ValueError('Verified independent source reference changed: ' + stem)
        references.append(dict(source=record(proof_path), fixture=record(path)))
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionRuntimeTimelineValidation.cs'
    result = dict(format=1, manifest='871_156598', packaged_data=record(data_path), independent_references=references,
                  runtime=record(runtime), runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
                  validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')),
                  ik_solver=record(runtime.with_name('ProfessionNativeIdleIk.cs')), slot_state=record(runtime.with_name('ProfessionNativeSlotPose.cs')),
                  generator=record(Path(__file__)),
                  operation='Construct discrete slot attachment/draw-order clips and constant idle IK states directly from the actual lossless packaged source. Preserve source key/slot order, null clears, signed 32-bit offsets and stable untouched slots. Bind IK setup and animation defaults separately, preserve original order, child length and bend/mix. Independent source-data/circle-intersection fixtures supply expected values only.',
                  limitations=['Only unmixed attachment/draw-order timelines and the six verified constant idle IK branches are bound here. Other slot channels, skin-required IK, nonconstant IK and active softness/stretch/compress explicitly reject.',
                               'No animation mixing, physical constraints, actor/page input/rendering or Windows Player validation.'],
                  validated_scope='Pending Unity compilation and actual packaged runtime timeline Play Mode validation.')
    (folder / 'runtime-timelines-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recorded actual packaged runtime slot/draw-order/idle-IK source bindings')
    return result


if __name__ == '__main__':
    build()
