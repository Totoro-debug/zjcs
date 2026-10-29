"""Decode original invitation NPC skeletons and every applicable lazy animation."""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from convert_official_skeletons import reader_type, WHEEL_SHA256
from map13_native_schemas import NativeSchemas
from map12_team_native_schemas import install
from prepare_unity_bundle_inputs import collect, serialized_files, read_source
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_npc_sources import OUTPUT as MODELS, RECEIPT as MODEL_RECEIPT
from prepare_unity_map13_entity_skeletons import validate_references
from UnityPy.classes.PPtr import PPtr

DEST = ROOT / 'unity/Assets/Official/World/Map/12/Source/TeamNpcSkeletons'
CATALOG = DEST.parent / 'team-npc-skeletons.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-skeletons-source-manifest.json'


def build(write=False):
    apk = Apk.current()
    source = json.loads(MODELS.read_bytes())
    proof = json.loads(MODEL_RECEIPT.read_bytes())
    model_record, = [row for row in proof['outputs'] if row['path'] == source_path(MODELS)]
    if source['manifest'] != apk.version or sha(MODELS.read_bytes()) != model_record['sha256']:
        raise ValueError('Original NPC model source selection differs')
    roots = sorted({'defaultpackage_assets_prefabs_' + row['figureModelUI'].rsplit('/', 1)[0].lower().replace('/', '_') + '.bundle'
                    for row in source['models']})
    _, missing = collect(apk, roots)
    if missing:
        raise ValueError('Resolve original NPC skeleton CAB inputs: ' + str(missing))
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    schemas = NativeSchemas(code)
    schema_proof = install(schemas, code)
    objects = {file.name + ':' + str(obj.path_id): obj for bundle in apk.loaded.values()
               for file in serialized_files(bundle) for obj in file.objects.values()}
    decoded_fields, records = {}, {}

    def key(obj):
        return obj.assets_file.name + ':' + str(obj.path_id)

    def record(obj):
        return dict(identity=key(obj), type=obj.type.name, name=obj.peek_name(),
                    source=apk.provenance(obj), payloadSha256=sha(obj.get_raw_data()))

    def fields(obj):
        identity = key(obj)
        if identity not in decoded_fields:
            value, _ = schemas.read(obj)
            decoded_fields[identity] = value
            records[identity] = dict(**record(obj), fields=value)
        return decoded_fields[identity]

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref()

    def text(obj):
        records.setdefault(key(obj), record(obj))
        return obj.read().m_Script.encode('utf8', 'surrogateescape')

    lazy_by_go = {}
    for row in source['objects']:
        if row.get('scriptType') == 'SpineLazyAnimLoad':
            obj = objects[row['identity']]
            lazy_by_go[key(pointer(obj, row['fields']['m_GameObject']))] = pointer(obj, row['fields']['mSplitBinarySo'])
    skeletons, loaders, renderers = {}, {}, []
    for row in source['objects']:
        if row.get('scriptType') != 'Spine.Unity.SkeletonGraphic':
            continue
        obj = objects[row['identity']]
        if not row['fields']['skeletonDataAsset']['m_PathID']:
            raise ValueError('Original NPC graphic has no skeleton data')
        skeleton = pointer(obj, row['fields']['skeletonDataAsset'])
        skeletons[key(skeleton)] = skeleton
        go = pointer(obj, row['fields']['m_GameObject'])
        loader = lazy_by_go.get(key(go))
        if loader is not None:
            previous = loaders.setdefault(key(skeleton), loader)
            if key(previous) != key(loader):
                raise ValueError('One original NPC skeleton uses conflicting lazy animation tables')
        renderers.append(dict(identity=row['identity'], gameObject=key(go), skeleton=key(skeleton), fields=row['fields']))
    assets_by_guid = {}
    for row in read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive)):
        if row['guid']:
            if row['guid'] in assets_by_guid:
                raise ValueError('Ambiguous original animation GUID')
            assets_by_guid[row['guid']] = row
    reader_class, binary_input = reader_type()
    outputs, models = {}, []

    def save(path, raw):
        outputs[path] = raw
        identity = source_path(path)
        outputs[Path(str(path) + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
            hashlib.md5(('zjcs:map12:team-npc-skeleton:' + identity).encode()).hexdigest() +
            '\nTextScriptImporter:\n  externalObjects: {}\n').encode()
        return dict(path=identity, sha256=sha(raw))

    for identity, obj in sorted(skeletons.items()):
        original = fields(obj)
        raw = text(pointer(obj, original['skeletonJSON']))
        reader = reader_class()
        is_json = raw.lstrip().startswith(b'{')
        if is_json:
            decoded, strings = json.loads(raw), []
        else:
            decoded = reader.convert(raw)
            if reader.inp.index != len(raw):
                raise ValueError('Original NPC skeleton binary was not fully consumed')
            strings = list(reader.inp.strings)
        embedded = list(decoded.get('animations', {}))
        split_records = []
        loader = loaders.get(identity)
        declared_loader = loader
        if loader is not None:
            # Original GetLazyLoadSpineArgs rejects a component's stale table
            # when its object name does not contain the SkeletonDataAsset name.
            fields(loader)
            if obj.peek_name() and obj.peek_name() not in loader.peek_name():
                loader = None
        if loader is not None:
            table = fields(loader)
            if strings != table['StringArray']:
                raise ValueError('Original NPC lazy animation string table differs: ' + obj.peek_name() +
                                 ' source=' + str(len(strings)) + ' loader=' + str(len(table['StringArray'])) +
                                 ' JSON=' + str(is_json) + ' split=' + str(len(table['ListAnimationContent'])))
            seen = set()
            for row in table['ListAnimationContent']:
                name, guid = row['ContentName'], row['TextAsset']['mAssetGuid']
                if name in seen or not guid or guid not in assets_by_guid:
                    raise ValueError('Original NPC lazy animation is duplicated or unresolved')
                seen.add(name)
                asset = assets_by_guid[guid]
                candidates = {key(ptr.deref()): ptr.deref() for path, ptr in apk.container(asset['bundle'])
                              if path.casefold() == asset['path'].casefold()}
                if len(candidates) != 1:
                    raise ValueError('Original NPC animation GUID does not resolve exactly once')
                animation_obj = next(iter(candidates.values()))
                if animation_obj.type.name != 'TextAsset':
                    raise ValueError('Original NPC animation is not a TextAsset')
                animation_raw = text(animation_obj)
                reader.inp = binary_input(animation_raw)
                reader.inp.strings = strings
                if reader.inp.read_string() != name:
                    raise ValueError('Original NPC animation GUID/header/name differs')
                animation = reader._read_animation()
                if reader.inp.index != len(animation_raw):
                    raise ValueError('Original NPC animation binary was not fully consumed')
                if name in decoded.setdefault('animations', {}) and decoded['animations'][name] != animation:
                    raise ValueError('Original embedded and lazy NPC animation conflict')
                decoded['animations'][name] = animation
                split_records.append(dict(name=name, asset=asset, original=record(animation_obj), bytesSha256=sha(animation_raw)))
        validate_references(decoded)
        folder = DEST / sha(identity.encode())[:24]
        atlas_records = []
        for index, value in enumerate(original['atlasAssets']):
            atlas = pointer(obj, value)
            atlas_fields = fields(atlas)
            atlas_raw = text(pointer(atlas, atlas_fields['atlasFile']))
            atlas_records.append(dict(original=record(atlas), fields=atlas_fields,
                output=save(folder / ('atlas-' + str(index) + '.txt'), atlas_raw)))
        payload = json.dumps(decoded, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode()
        compressed = gzip.compress(payload, compresslevel=9, mtime=0)
        if gzip.decompress(compressed) != payload:
            raise ValueError('Original decoded skeleton compression changed bytes')
        models.append(dict(identity=identity, original=record(obj), fields=original,
            originalSkeletonBytesSha256=sha(raw), format='json' if is_json else 'binary',
            decoded=save(folder / 'skeleton.json.gz.bytes', compressed), decodedSha256=sha(payload),
            atlases=atlas_records, embeddedAnimationNames=embedded, splitAnimations=split_records,
            lazyLoader=record(declared_loader) if declared_loader is not None else None,
            lazyLoaderAccepted=loader is not None,
            rejectedLoaderReason='Original AppearanceAnimUtils.GetLazyLoadSpineArgs SoNoMatch name guard'
                if declared_loader is not None and loader is None else None,
            declaredNonSplitNamesAbsentFromBinary=[name for name in fields(loader)['ListNoSplitAnimationNames'] if name not in embedded]
                if loader is not None else [],
            counts={name: len(decoded.get(name, [])) for name in ('bones', 'slots', 'skins', 'ik', 'transform', 'path', 'physics', 'animations')}))
        print('Decoded original invitation NPC skeleton', obj.peek_name(), models[-1]['counts'], flush=True)
    save(CATALOG, encode(dict(manifest=apk.version, mapClassId=12, skeletons=models, renderers=renderers,
                             sourceObjects=[records[k] for k in sorted(records)])))
    folders = {DEST, *(path.parent for path in outputs if path.is_relative_to(DEST))}
    for folder in folders:
        outputs[Path(str(folder) + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
            hashlib.md5(('zjcs:map12:team-npc-skeleton:' + source_path(folder)).encode()).hexdigest() +
            '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    input_records = []
    for name in sorted(apk.loaded):
        raw, provenance = read_source(apk, name)
        input_records.append(dict(provenance, decodedSha256=sha(raw)))
    def file_record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))
    receipt = encode(dict(format=1, manifest=apk.version, mapClassId=12, inputs=input_records,
        sourceModels=file_record(MODELS), sourceReceipt=file_record(MODEL_RECEIPT), schemaProof=schema_proof,
        tools=[file_record(ROOT / name) for name in ('scripts/prepare_unity_map12_team_npc_skeletons.py',
            'scripts/convert_official_skeletons.py', 'scripts/map12_team_native_schemas.py',
            'scripts/prepare_unity_map13_entity_skeletons.py')],
        originalCode=[file_record(code / 'client-source' / name) for name in
            ('AppearanceSystem.Runtime/AppearanceAnimUtils.cs', 'SpineLazyAnimLoad.cs', 'SpineAnimationSplitBinarySO.cs')],
        decoder=dict(name='SkelToJson', version='1.0.1', wheelSha256=WHEEL_SHA256, license='MIT'),
        counts=dict(skeletons=len(models), graphicBindings=len(renderers),
                    splitAnimations=sum(len(row['splitAnimations']) for row in models),
                    acceptedLazyTables=sum(row['lazyLoaderAccepted'] for row in models),
                    rejectedLazyTables=sum(bool(row['rejectedLoaderReason']) for row in models)),
        outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in sorted(outputs.items())],
        operation='Decode all original model skeletons and exact GUID-declared lazy animations using the pinned '
                  'independent MIT format reader. Verify complete binary consumption, names, string tables, '
                  'bone/slot/skin/constraint references and embedded/split conflicts. Retain mismatched '
                  'declared tables as source metadata and apply the original SoNoMatch name guard. Preserve all float32 '
                  'fields, weighted attachments, skin memberships, deform, curves, physics, events and '
                  'original atlas data in deterministic lossless compressed JSON; no runtime or pose baking.',
        limitations=['This is lossless source data, not a complete graphic/constraint/animation evaluator. '
                     'Native resource binding, original component lifecycle and actual NPC UI/save/battle '
                     'and Windows Player verification remain required.']))
    outputs[RECEIPT] = receipt
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original NPC skeleton source drift: ' + source_path(path))
    print('Original invitation NPC skeleton closure verified:', len(models), 'skeletons', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
