"""Flatten hash-verified Map13 prop skeletons for independent Unity planar geometry.

The existing project's free summon data converter supplies atlas/weighted attachment
conversion. Extra world bone channels are retained here; no Spine runtime is used.
"""
from __future__ import annotations
import copy
import hashlib
import json
from pathlib import Path
import uuid

from map13_native_skeleton_geometry import flatten

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source/native-entity-skeletons.json'
SOURCE_RECEIPT = ROOT / 'assets/reference/official-map/map13-entity-skeletons-source-manifest.json'
MATERIAL_RECEIPT = ROOT / 'assets/reference/official-map/map13-native-entity-materials-native-source-manifest.json'
DEST = ROOT / 'unity/Assets/Official/World/Map/13/NativeSkeletonModels'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-skeleton-models-source-manifest.json'


def sha(raw): return hashlib.sha256(raw).hexdigest()


def verified(record):
    path = ROOT / record['path']
    raw = path.read_bytes()
    if sha(raw) != record['sha256']:
        raise ValueError('Source hash changed: ' + record['path'])
    return raw


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        temp = ROOT / '.tools/map13-native-skeleton-write.tmp'
        temp.write_bytes(raw)
        temp.replace(path)


def output(path, data):
    raw = (json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode()
    write(path, raw)
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-skeleton:' + path.relative_to(ROOT).as_posix()).hex
        write(meta, ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw), meta_sha256=sha(meta.read_bytes()))


def convert(skeleton, atlas):
    """Retain setup shear, inheritance and single-axis channels absent in summon data."""
    clean = copy.deepcopy(skeleton)
    if any(skeleton.get(k) for k in ('ik', 'transform', 'path', 'physics')):
        raise ValueError('This prop module must not silently omit constraints.')
    for bone in clean['bones']:
        if bone.get('inherit', 'normal') not in ('normal', 'onlyTranslation', 'noScale'):
            raise ValueError('Unimplemented world inheritance: ' + str(bone))
        bone['inherit'] = 'normal'
        bone.pop('shearX', None)
        bone.pop('shearY', None)
    extras = {}
    for name, anim in clean['animations'].items():
        extras[name] = []
        for bone, channels in anim.get('bones', {}).items():
            for channel in list(channels):
                if channel in ('shear', 'translatex', 'translatey', 'scalex', 'scaley'):
                    extras[name].append((bone, channel, channels.pop(channel)))
    result = flatten(clean, atlas)
    for target, original in zip(result['bones'], skeleton['bones']):
        target.update(inherit=original.get('inherit', 'normal'),
                      shearX=original.get('shearX', 0), shearY=original.get('shearY', 0))
    bones = {b['name']: i for i, b in enumerate(result['bones'])}
    for anim in result['animations']:
        for bone, channel, keys in extras[anim['name']]:
            channels = [(4, 0, 'x', 0, 0), (4, 1, 'y', 0, 1)] if channel == 'shear' else [
                (0 if channel.startswith('translate') else 1, 0 if channel.endswith('x') else 1,
                 'value', 0 if channel.startswith('translate') else 1, 0)]
            for kind, axis, field, default, component in channels:
                frames = []
                for key in keys:
                    curve = key.get('curve', [])
                    if curve != 'stepped' and not isinstance(curve, list):
                        raise ValueError('Unknown world interpolation')
                    frames.append(dict(time=key.get('time', 0), value=key.get(field, default),
                        stepped=curve == 'stepped', curve=curve[component*4:component*4+4] if isinstance(curve, list) else []))
                anim['tracks'].append(dict(kind=kind, index=bones[bone], axis=axis, keys=frames))
        anim['duration'] = max([anim['duration']] + [k['time'] for t in anim['tracks'] for k in t['keys']])
    return result


def build():
    source_receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    # The catalog itself, all decoded skeletons and atlases must match their provenance.
    record = next(o for o in source_receipt['outputs'] if o['path'] == SOURCE.relative_to(ROOT).as_posix())
    source = json.loads(verified(record))
    materials = json.loads(MATERIAL_RECEIPT.read_bytes())
    bindings = json.loads(verified(materials['bindings']))
    material_lookup = {(m['cab'].lower(), str(m['path_id'])): m for m in bindings['entries']}
    outputs = []; models = []; profiles = []
    for item in source['skeletons']:
        if not item['name'].startswith('map_'): continue
        if item['split_animations'] or item['lazy_loader'] or len(item['atlases']) != 1:
            raise ValueError('Additional prop source dependency must be restored')
        if any(item['fields'][k] for k in ('skeletonDataModifiers', 'fromAnimation', 'toAnimation', 'duration', 'zSlots')):
            raise ValueError('Prop modifiers or special mixes must be migrated')
        atlas = item['atlases'][0]
        material = next(r for r in atlas['references'] if r['field'] == '.materials[0]')
        bound = material_lookup[material['cab'].lower(), str(material['path_id'])]
        verified(bound['output'])
        skeleton = json.loads(verified(item['decoded']))
        definition = convert(skeleton, verified(atlas['output']).decode('utf-8-sig'))
        key = sha((item['cab'].lower() + ':' + item['path_id']).encode())[:24]
        definition.update(name=item['name'], cab=item['cab'], path_id=item['path_id'],
            scale=item['fields']['scale'], defaultMix=item['fields']['defaultMix'],
            materialPath=bound['output']['path'].removeprefix('unity/'))
        record = output(DEST / (key + '.json'), definition)
        outputs.append(record)
        models.append(dict(cab=item['cab'], path_id=item['path_id'], name=item['name'], definition=record,
            bones=len(definition['bones']), slots=len(definition['slots']), animations=len(definition['animations'])))
        for renderer in source['renderers']:
            if renderer['skeleton'] != dict(cab=item['cab'], path_id=item['path_id']): continue
            f = renderer['fields']
            # Geometry stays in individual slot draws. The original separator names are
            # retained for the later prefab separator/part binding, never discarded.
            if f['tintBlack'] or f['maskInteraction'] or f['addNormals'] or f['calculateTangents']:
                raise ValueError('Additional prop renderer feature must be migrated')
            profiles.append(dict(cab=renderer['cab'], path_id=renderer['path_id'], model=key,
                managed_type=renderer['managed_type'], fields=f))
    if len(models) != 27: raise ValueError('Expected all 27 prop skeleton sources')
    catalog = output(DEST / 'catalog.json', dict(manifest=source['manifest'], mapClassId=13, models=models, renderers=profiles))
    outputs.append(catalog)
    folder_meta = Path(str(DEST) + '.meta')
    if not folder_meta.exists():
        write(folder_meta, ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-skeleton-folder').hex + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
    dependencies = [SOURCE, SOURCE_RECEIPT, MATERIAL_RECEIPT, Path(__file__), ROOT / 'scripts/map13_native_skeleton_geometry.py']
    receipt = dict(manifest=source['manifest'], mapClassId=13, dependencies=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in dependencies],
        outputs=outputs, models=len(models), renderer_profiles=len(profiles),
        public_format_reference=dict(url='https://us.esotericsoftware.com/spine-bones', use='Public inheritance/shear concepts only; independent planar affine mathematics, no runtime source copied.'),
        converter_origin=dict(commit='da348660971cbc087ce6a8c1f92d5ffa2cefc7c6', path='scripts/prepare_battle_summon_models.py', symbols=['atlas_regions', 'flatten']),
        operation='Hash-verified original prop bones, weights, atlas UVs, setup pose, shear/translation/scale/color/attachment/draw-order timelines. Independent project converter and Unity native geometry; no Spine runtime.',
        limitations=['Original Animator controllers/transitions, entity prefab components and formal map-entry binding remain separate migration work. Monster and NPC constrained skeletons are not part of this prop module.'])
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    print(json.dumps(dict(models=len(models), renderer_profiles=len(profiles), bones=sum(m['bones'] for m in models), animations=sum(m['animations'] for m in models))))


if __name__ == '__main__': build()
