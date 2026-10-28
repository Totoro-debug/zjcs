"""Package verified character data for the independent native appearance backend."""
import gzip
import hashlib
import json
from pathlib import Path
import struct

from official_apk import Apk
from convert_official_skeletons import WHEEL_SHA256
from prepare_native_character_geometry import atlas_regions
from prepare_unity_profession_native_inputs import CACHE, ROOT
from prepare_unity_profession_photographer import record


def string(value):
    data = value.encode('utf-8')
    return struct.pack('<I', len(data)) + data


def block(data):
    compressed = gzip.compress(data, compresslevel=9, mtime=0)
    return struct.pack('<I', len(data)) + hashlib.sha256(data).digest() + struct.pack('<I', len(compressed)) + compressed


def meta(path, folder=False):
    identity = path.relative_to(ROOT).as_posix()
    path.with_name(path.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' +
        hashlib.md5(('zjcs-profession-native-data-' + identity).encode()).hexdigest() +
        ('\nfolderAsset: yes' if folder else '') + '\n', encoding='utf-8', newline='\n')


def build():
    proof_path = ROOT / 'assets/reference/profession-ui/native-attachments-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    if proof['manifest'] != '871_156598':
        raise ValueError('Native attachment data version changed')
    apk = Apk.current()
    if apk.version != proof['manifest']:
        raise ValueError('Selected original source version changed')
    root = ROOT / 'unity/Assets/Official/ProfessionUI/NativeSkeletonData'
    folder = root / 'Resources/ProfessionUI/NativeSkeletons'
    folder.mkdir(parents=True, exist_ok=True)
    parent = folder
    while parent != root.parent:
        meta(parent, True)
        parent = parent.parent
    membership_proof_path = ROOT / 'assets/reference/profession-ui/native-skins-source-manifest.json'
    membership_proof = json.loads(membership_proof_path.read_bytes())
    membership_path = CACHE / 'typed/native-skins-validation.json'
    if membership_proof['fixture'] != record(membership_path):
        raise ValueError('Verified original skin membership fixture changed')
    membership = json.loads(membership_path.read_bytes())
    supporting_fixtures = []
    for name, fixture in (('bone-pose', 'bone-pose-validation.json'), ('slot-pose', 'slot-pose-validation.json')):
        parent = ROOT / 'assets/reference/profession-ui' / (name + '-source-manifest.json')
        data = json.loads(parent.read_bytes())
        fixture_path = CACHE / 'typed' / fixture
        if data['fixture'] != record(fixture_path):
            raise ValueError('Verified original setup fixture changed: ' + name)
        supporting_fixtures.append(dict(source=record(parent), fixture=record(fixture_path)))
    outputs, inputs, fixtures = [], [], []
    for sex in ('male', 'female'):
        source_folder = CACHE / 'skeletons' / sex
        receipt_path = source_folder / 'source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        skeleton_path, atlas_path = source_folder / 'skeleton.json', source_folder / 'atlas.txt'
        if receipt['manifest'] != apk.version or record(skeleton_path)['sha256'] != receipt['output_sha256']:
            raise ValueError('Verified source skeleton changed: ' + sex)
        if record(skeleton_path) not in proof['inputs'] or record(atlas_path) not in proof['inputs']:
            raise ValueError('Verified skeleton/atlas no longer match attachment conversion: ' + sex)
        for original in receipt['inputs']:
            if record(source_folder / original['file'])['sha256'] != original['sha256']:
                raise ValueError('Original binary data changed: ' + original['file'])
        if receipt['tool']['wheel_sha256'] != WHEEL_SHA256 or receipt['converter_sha256'] != record(ROOT / 'scripts/convert_official_skeletons.py')['sha256']:
            raise ValueError('Independent format converter changed')
        model = next(row for row in proof['model_inputs'] if row['fields']['m_Name'] == 'character_' + sex.title() + '_1_SkeletonData')
        original = next(obj for obj in apk.objects('appearanceassets_character_model_' + sex + '_1')
            if str(obj.path_id) == model['source']['path_id'])
        if hashlib.sha256(original.get_raw_data()).hexdigest() != model['payload_sha256']:
            raise ValueError('Original SkeletonDataAsset changed: ' + sex)
        scale = model['fields']['scale']
        source = skeleton_path.read_bytes()
        atlas = json.dumps(atlas_regions(atlas_path.read_text(encoding='utf-8')), ensure_ascii=False, separators=(',', ':')).encode()
        data = b'ZJCSPROF' + struct.pack('<I', 1) + string(apk.version) + string(sex) + struct.pack('<f', scale) + block(source) + block(atlas)
        target = folder / (sex + '.bytes')
        target.write_bytes(data)
        meta(target)
        skeleton = json.loads(source)
        types = {}
        for skin in skeleton['skins']:
            for entries in skin.get('attachments', {}).values():
                for attachment in entries.values():
                    kind = attachment.get('type', 'region')
                    types[kind] = types.get(kind, 0) + 1
        outputs.append(dict(sex=sex, resource='ProfessionUI/NativeSkeletons/' + sex,
            file=record(target), meta=record(target.with_name(target.name + '.meta')),
            skeleton_sha256=hashlib.sha256(source).hexdigest(), skeleton_bytes=len(source),
            atlas_sha256=hashlib.sha256(atlas).hexdigest(), atlas_bytes=len(atlas), scale=scale,
            counts=dict(bones=len(skeleton['bones']), slots=len(skeleton['slots']), skins=len(skeleton['skins']),
                animations=len(skeleton['animations']), ik=len(skeleton.get('ik', [])),
                transform=len(skeleton.get('transform', [])), physics=len(skeleton.get('physics', [])),
                regions=len(json.loads(atlas)), attachments=types)))
        counts = outputs[-1]['counts']
        members = next(row for row in membership['cases'] if row['sex'] == sex)
        fixtures.append(dict(sex=sex, skeletonHash=outputs[-1]['skeleton_sha256'], atlasHash=outputs[-1]['atlas_sha256'],
            bones=counts['bones'], slots=counts['slots'], skins=counts['skins'], animations=counts['animations'],
            regions=counts['regions'], attachments=sum(types.values()), constraints=members['constraintCount'],
            members=[{key: row[key] for key in ('name', 'bones', 'constraints')} for row in members['skins']]))
        inputs.append(dict(receipt=record(receipt_path), skeleton=record(skeleton_path), atlas=record(atlas_path), model=model))
    fixture_path = CACHE / 'typed/skeleton-data-validation.json'
    fixture_path.write_text(json.dumps(dict(bodies=fixtures), ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8', newline='\n')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionSkeletonDataValidation.cs'
    result = dict(format=1, manifest=apk.version, inputs=inputs, prerequisite=record(proof_path), outputs=outputs,
        membership_source=record(membership_proof_path), membership_fixture=record(membership_path),
        supporting_fixtures=supporting_fixtures,
        fixture=record(fixture_path), geometry_fixture=proof['fixture'],
        runtime=record(runtime), runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
        validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')),
        parser=record(ROOT / 'unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentJson.cs'),
        generator=record(Path(__file__)),
        operation='Losslessly gzip the verified independent MIT-format-decoder JSON and original atlas-region projection. '
            'Preserve every animation, event, constraint and attachment field; native runtime verifies decompressed byte hashes '
            'and builds actual bone/slot/skin/attachment objects. No SDK runtime, pose baking or alternate art.',
        limitations=['Data loading is not constraint evaluation, animation mixing or native character rendering.',
            'All source channels and constraints remain in the package; unimplemented evaluators are not replaced by zero values.',
            'Texture/material/graphic/window services and actual profession page/Windows Player validation remain incomplete.'],
        validated_scope='Pending Unity import/compile and actual Play Mode native resource/data validation.')
    dest = ROOT / 'assets/reference/profession-ui/skeleton-data-source-manifest.json'
    dest.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Packaged lossless native skeleton data:', [(row['sex'], (ROOT / row['file']['path']).stat().st_size) for row in outputs])
    return result


if __name__ == '__main__':
    build()
