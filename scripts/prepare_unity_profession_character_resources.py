"""Bind original player asset paths to native metadata/data and split animation SOs."""
import copy
import hashlib
import json
from pathlib import Path
import re
import uuid

from official_apk import Apk
from convert_official_skeletons import reader_type
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
import yaml


def guid(path):
    return re.search(r'(?m)^guid: ([0-9a-f]{32})$', path.with_name(path.name + '.meta').read_text(encoding='utf-8'))[1]


def meta(path, folder=False, native=False):
    identity = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/profession/character-resource/' + path.relative_to(ROOT).as_posix()).hex
    text = 'fileFormatVersion: 2\nguid: ' + identity + '\n'
    if folder: text += 'folderAsset: yes\n'
    if native: text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
    path.with_name(path.name + '.meta').write_text(text, encoding='utf-8', newline='\n')
    return identity


def asset(path, name, script, values):
    native = source_header()
    native.update(m_Enabled=1, m_EditorHideFlags=0, m_Name=name, m_EditorClassIdentifier='',
                  m_Script=dict(fileID=11500000, guid=guid(script), type=3))
    native.update(values)
    path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
                    yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000), encoding='utf-8', newline='\n')
    return dict(fileID=11400000, guid=meta(path, native=True), type=2)


def build():
    apk = Apk.current()
    if apk.version != '871_156598': raise ValueError('Original character resource version changed')
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    code_manifest = code.parent / 'code-manifest.json'
    if json.loads(code_manifest.read_bytes())['manifest'] != apk.version: raise ValueError('Original application code version differs')
    schemas = NativeSchemas(code, unity_project=ROOT / 'unity'); schema = install(schemas, code)
    proof_path = ROOT / 'assets/reference/profession-ui/skeleton-data-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    if proof['manifest'] != apk.version: raise ValueError('Verified character data version differs')
    manifest_bytes = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    table = {r['path']: r for r in read_asset_manifest(manifest_bytes)}
    dest = ROOT / 'unity/Assets/Official/ProfessionUI/CharacterResources'
    dest.mkdir(parents=True, exist_ok=True); meta(dest, folder=True)
    outputs = [dest.with_name(dest.name + '.meta')]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    application = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
    sources, entries, fixtures = [], [], []
    for sex in ('male', 'female'):
        title = sex.title()
        path = 'Assets/AppearanceAssets/Character/Model/' + title + '_1/character_' + title + '_1_SkeletonData.asset'
        row = table[path]; obj = apk.asset(row['bundle'], row['path'])
        model = next(r['model'] for r in proof['inputs'] if r['model']['source']['path_id'] == str(obj.path_id))
        if hashlib.sha256(obj.get_raw_data()).hexdigest() != model['payload_sha256']:
            raise ValueError('Verified original model metadata changed')
        original = model['fields']
        metadata = dest / (sex + '-original-metadata.json')
        metadata.write_text(json.dumps(original, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8', newline='\n')
        metadata_guid = meta(metadata)
        data_row = next(r for r in proof['outputs'] if r['sex'] == sex)
        payload = ROOT / data_row['file']['path']
        if record(payload) != data_row['file'] or record(payload.with_name(payload.name + '.meta')) != data_row['meta']:
            raise ValueError('Verified native payload changed')
        carrier = dest / (sex + '-skeleton.asset')
        binding = asset(carrier, original['m_Name'], runtime / 'ProfessionNativeSkeletonAsset.cs',
                        dict(sourcePath=path, sourceGuid=row['guid'], sex=sex,
                             payload=dict(fileID=4900000, guid=guid(payload), type=3),
                             originalMetadata=dict(fileID=4900000, guid=metadata_guid, type=3)))
        entries.append(dict(Path=path, Guid=row['guid'], Asset=binding))
        split_path = path.removesuffix('.asset') + '_SplitBinarySO.asset'
        split_row = table[split_path]; split_obj = apk.asset(split_row['bundle'], split_row['path'])
        split, _ = schemas.read(split_obj)
        bound = copy.deepcopy(split); bound.pop('m_Script'); bound.pop('m_Name')
        split_asset = dest / (sex + '-split-animations.asset')
        split_binding = asset(split_asset, split['m_Name'], application / 'SpineAnimationSplitBinarySO.cs', fields(bound))
        entries.append(dict(Path=split_path, Guid=split_row['guid'], Asset=split_binding))
        sources.append(dict(sex=sex, source=apk.provenance(obj), payload_sha256=model['payload_sha256'], fields=original,
                            split_source=apk.provenance(split_obj), split_payload_sha256=hashlib.sha256(split_obj.get_raw_data()).hexdigest(), split_fields=split))
        # Check the source SO's unsplit declarations against the actual main
        # binary, rather than treating a stale declaration as decoded animation.
        receipt_path = CACHE / 'skeletons' / sex / 'source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        raw_row = next(r for r in receipt['inputs'] if r['name'].endswith('.skel'))
        raw_path = receipt_path.parent / raw_row['file']
        if record(raw_path)['sha256'] != raw_row['sha256']: raise ValueError('Verified main binary changed')
        reader_class, _ = reader_type(); reader = reader_class(); raw = raw_path.read_bytes()
        main = reader.convert(raw)
        if reader.inp.index != len(raw): raise ValueError('Main binary was not completely consumed')
        actual_main = list(main['animations'])
        absent_unsplit = [name for name in split['ListNoSplitAnimationNames'] if name not in main['animations']]
        sources[-1]['actual_main_binary'] = record(raw_path)
        sources[-1]['actual_main_animations'] = actual_main
        sources[-1]['declared_unsplit_absent_from_main'] = absent_unsplit
        fixtures.append(dict(sex=sex, path=path, guid=row['guid'], split_path=split_path, split_guid=split_row['guid'],
                             metadata=metadata.read_text(encoding='utf-8'), scale=original['scale'], defaultMix=original['defaultMix'],
                             zSlots=original['zSlots'], fromAnimation=original['fromAnimation'], toAnimation=original['toAnimation'], duration=original['duration'],
                             strings=split['StringArray'], unsplit=split['ListNoSplitAnimationNames'], actualMain=actual_main, missingUnsplit=absent_unsplit,
                             contents=[dict(name=r['ContentName'], guid=r['TextAsset']['mAssetGuid']) for r in split['ListAnimationContent']]))
        outputs.extend([metadata, metadata.with_name(metadata.name + '.meta'), carrier, carrier.with_name(carrier.name + '.meta'), split_asset, split_asset.with_name(split_asset.name + '.meta')])
    catalog = ROOT / 'unity/Assets/Game/Resources/ProfessionUI/character-resources.asset'
    asset(catalog, 'Original player appearance resource bindings', runtime / 'ProfessionNativeCharacterCatalog.cs', dict(entries=entries))
    outputs.extend([catalog, catalog.with_name(catalog.name + '.meta')])
    fixture = CACHE / 'typed/native-character-resources-validation.json'
    fixture.write_text(json.dumps(dict(cases=fixtures), ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    runtime_paths = [runtime / (n + '.cs') for n in ('ProfessionNativeSkeletonAsset', 'ProfessionNativeCharacterCatalog', 'OfflineProfessionCharacterResources')]
    runtime_paths += [application / (n + '.cs') for n in ('SpineAnimationSplitBinarySO', 'SplitSpineBinaryAssetContent')]
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeCharacterResourceValidation.cs'
    closure_path = CACHE / 'typed/appearance-character-binding-closure.json'
    closure = json.loads(closure_path.read_bytes())
    prefab_row = closure['assets']['Prefabs/UI/Profession/CharacterPanelProfessionPanel.prefab']
    prefab = ROOT / prefab_row['path']
    if record(prefab)['sha256'] != prefab_row['sha256']: raise ValueError('Verified original page prefab metadata changed')
    script_meta = CACHE / 'typed/export/ExportedProject/Assets/Scripts/spine-unity/Spine/Unity/SkeletonGraphic.cs.meta'
    script_identity = re.search(r'(?m)^guid: ([0-9a-f]{32})$', script_meta.read_text(encoding='utf-8'))[1]
    starts = []
    for chunk in re.split(r'(?m)^--- ', prefab.read_text(encoding='utf-8')):
        if 'guid: ' + script_identity not in chunk: continue
        match = re.search(r'(?m)^  startingAnimation: (.*)$', chunk)
        starts.append(dict(component=chunk.splitlines()[0], animation=match[1] if match else None))
    result = dict(format=1, manifest=apk.version, code_manifest=record(code_manifest), prerequisite=record(proof_path), asset_table_sha256=hashlib.sha256(manifest_bytes).hexdigest(), sources=sources, original_startup_reference=dict(closure=record(closure_path), prefab=record(prefab), script_identity_meta=record(script_meta), starting_animations=starts),
                  application_sources=[record(code / 'client-source' / n) for n in ('SpineAnimationSplitBinarySO.cs', 'SplitSpineBinaryAssetContent.cs', 'AppearanceSystem.SpineExt/LoadAllAnimationsSkeletonDataModifier.cs')],
                  outputs=[record(p) for p in outputs], runtimes=[record(p) for p in runtime_paths for p in (p, p.with_name(p.name + '.meta'))],
                  validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')), fixture=record(fixture), schema=schema,
                  generator=record(Path(__file__)),
                  operation='Resolve four original player skeleton/split-animation resource paths and AssetTable GUIDs APK-first. Preserve complete original model metadata, bind the verified lossless native payload, and directly migrate the original split-animation SO/value class including string pools, original content GUID lookup and unsplit order. The original LoadAllAnimationsSkeletonDataModifier.Apply is empty. Native request handles refer to catalog-owned packaged assets and release independently; required part callbacks are explicit.',
                  limitations=['The metadata retains original controller, atlas and modifier pointer identities; this carrier does not convert or execute an AnimatorController or animation SDK.',
                               'The original unsplit lists declare home/idle_M_01 for both sexes, but the fully consumed original main binaries do not contain it. The original profession prefab also serializes that startingAnimation; initial graphic handling must be verified before the native page is complete. Preserve the declaration and record this source gap; do not fabricate an animation.',
                               'Split-animation bytes are already decoded without loss in the referenced native payload; this module preserves the original split lookup SO, not a raw binary SDK reader.',
                               'SkeletonGraphic, physics/mixing, actual parts/followers and full profession page/input/save/Windows Player remain required.'],
                  validated_scope='Pending actual Unity import/compile and Play Mode resource/appearance loading validation.')
    (ROOT / 'assets/reference/profession-ui/character-resources-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Bound four original player resource paths, native metadata/payloads and source split-animation SOs')
    return result


if __name__ == '__main__':
    build()
