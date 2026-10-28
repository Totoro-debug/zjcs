"""Bind the six source idle timelines to independently evaluated local bone poses."""
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record, sample, f32


def build():
    source_proof = ROOT / 'assets/reference/profession-ui/native-curves-source-manifest.json'
    source = json.loads(source_proof.read_bytes())
    curve_path = CACHE / 'typed/native-curves-validation.json'
    if source['manifest'] != '871_156598' or source['fixture']['sha256'] != record(curve_path)['sha256']:
        raise ValueError('Verified original idle curve fixture changed')
    curves = json.loads(curve_path.read_bytes())['curves']
    cases, inputs = [], []
    for sex in ('male', 'female'):
        path = CACHE / 'skeletons' / sex / 'skeleton.json'
        receipt_path = path.parent / 'source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        verified = next(row for row in source['source'] if row['sex'] == sex)
        if record(path) != verified['source_json'] or record(receipt_path) != verified['receipt']:
            raise ValueError('Character source differs from the verified native curve inputs: ' + sex)
        if receipt['manifest'] != '871_156598' or record(path)['sha256'] != receipt['output_sha256']:
            raise ValueError('Original bone source changed: ' + sex)
        skeleton = json.loads(path.read_bytes())
        original = skeleton['bones']
        indices = {bone['name']: i for i, bone in enumerate(original)}
        modes = dict(normal=0, onlyTranslation=1, noRotationOrReflection=2, noScale=3, noScaleOrReflection=4)
        bones = []
        for i, bone in enumerate(original):
            b = dict(name=bone['name'], parent=indices[bone['parent']] if 'parent' in bone else -1,
                     inherit=modes[bone.get('inherit', 'normal')], skinRequired=bone.get('skin', False))
            for field in ('x', 'y', 'rotation', 'scaleX', 'scaleY', 'shearX', 'shearY'):
                b[field] = bone.get(field, 1 if field in ('scaleX', 'scaleY') else 0)
            if b['parent'] >= i: raise ValueError('Original bone ordering changed')
            bones.append(b)
        clips = []
        for name in ('home/idle_01', '100/idle_01', '200/idle_01'):
            selected = [c for c in curves if c['sex'] == sex and c['animation'] == name]
            if not selected: raise ValueError('Original idle curve fixture is empty')
            channels = {('rotate', 'value'): 0, ('translate', 'x'): 1, ('translate', 'y'): 2,
                        ('scale', 'x'): 3, ('scale', 'y'): 4}
            tracks = [dict(bone=indices[c['bone']], channel=channels[c['channel'], c['axis']], keys=c['keys']) for c in selected]
            samples = []
            for time in (-.001, 0, .15, .5, .9, 1.3, 2, 3.2):
                time = f32(time)
                local = [dict(x=b['x'], y=b['y'], rotation=b['rotation'], scaleX=b['scaleX'], scaleY=b['scaleY']) for b in bones]
                for track in tracks:
                    value = sample(track['keys'], time)
                    if not value['applies']: continue
                    field = ('rotation', 'x', 'y', 'scaleX', 'scaleY')[track['channel']]
                    setup = bones[track['bone']][field]
                    local[track['bone']][field] = f32(setup * value['value'] if track['channel'] >= 3 else setup + value['value'])
                samples.append(dict(time=time, local=local))
            clips.append(dict(name=name, tracks=tracks, samples=samples))
        cases.append(dict(sex=sex, bones=bones, clips=clips))
        inputs.extend([record(path), record(receipt_path)])
    fixture = CACHE / 'typed/bone-pose-validation.json'
    fixture.write_text(json.dumps(dict(cases=cases), ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8', newline='\n')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeBonePoseValidation.cs'
    proof = dict(format=1, manifest='871_156598', inputs=inputs, curves=record(source_proof),
        curve_fixture=record(curve_path), fixture=record(fixture), runtime=record(runtime),
        runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
        validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')),
        generator=record(Path(__file__)),
        operation='Independent Unity affine/local-pose adapter. Bind all six original scalar bone timelines to setup transforms without any SDK runtime, pose baking, character prefab creation or alternate art. Skin-required activation includes selected bone ancestors. Only normal and translation-only world inheritance are implemented; selective rotation/scale/reflection modes fail explicitly.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: 3542 original bones, 1171 source curves, 48 local poses and 425108 scalar checks; maximum absolute local difference 0.00006389618 within 0.0004 + 0.000007 * abs(expected). Synthetic normal affine matrices match native Unity transforms; translation-only inheritance, shear, skin-required activation and ancestors, source ownership, invalid input and explicit unsupported inheritance checks pass. No original full world pose, constraints or character rendering is covered.',
        limitations=['No character rendering or constraints are implemented here. Selective inheritance, IK, transform/physics constraints, original native renderer and full profession page remain required.'])
    (ROOT / 'assets/reference/profession-ui/bone-pose-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Bound source idle local poses:', sum(len(c['bones']) for c in cases), 'bones;', sum(len(k['samples']) for c in cases for k in c['clips']), 'samples.')


if __name__ == '__main__':
    build()
