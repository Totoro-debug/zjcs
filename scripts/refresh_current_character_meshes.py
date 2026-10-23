"""Convert current official triangle order into the existing native mesh packages.

Retain sampled vertices only after proving the skeleton, influencing animation
channels and attachment geometry identical. Original archives stay in history.
"""
from __future__ import annotations

import collections
import gzip
import hashlib
import json
from pathlib import Path
import zipfile

from official_apk import ROOT
from official_source import OfficialSource


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8')


def preserve(path):
    raw = path.read_bytes()
    backup = ROOT / '.tools/source-packages/history/converted' / (sha(raw) + '.bin')
    backup.parent.mkdir(parents=True, exist_ok=True)
    if backup.exists() and backup.read_bytes() != raw:
        raise ValueError('Historical conversion hash collision: ' + str(path))
    if not backup.exists():
        backup.write_bytes(raw)
    return {'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(raw),
            'preserved': backup.relative_to(ROOT).as_posix()}


def load_skeleton(version):
    root = ROOT / '.tools/source-packages/current' / version / 'skeletons/male'
    raw = (root / 'skeleton.json').read_bytes()
    receipt = json.loads((root / 'source-manifest.json').read_text('utf8'))
    if receipt['manifest'] != version or receipt['output_sha256'] != sha(raw):
        raise ValueError('Decoded skeleton source hash differs: ' + version)
    for record in receipt['inputs']:
        if sha((root / record['file']).read_bytes()) != record['sha256']:
            raise ValueError('Preserved raw skeleton input differs')
    return json.loads(raw), {'path': (root / 'source-manifest.json').relative_to(ROOT).as_posix(),
                             'sha256': sha((root / 'source-manifest.json').read_bytes())}


def build():
    current = OfficialSource().version
    reference = ROOT / 'assets/reference/character-creation/current-mesh-source-manifest.json'
    if reference.exists():
        previous = json.loads(reference.read_text('utf8'))
        if previous['manifest'] == current:
            for record in previous['packages']:
                if sha((ROOT / record['path']).read_bytes()) != record['sha256']:
                    raise ValueError('Current native mesh archive changed: ' + record['path'])
            return previous
    old, old_receipt = load_skeleton('854_156383')
    new, new_receipt = load_skeleton(current)
    for key in ('bones', 'slots', 'ik', 'transform', 'path', 'physics'):
        if old.get(key) != new.get(key):
            raise ValueError('Changed pose evaluation requires full resampling: ' + key)
    old_skins = {s['name']: s for s in old['skins']}
    new_skins = {s['name']: s for s in new['skins']}
    slot_defaults = {s['name']: s.get('attachment') for s in old['slots']}
    job = json.loads((ROOT / '.tools/source-packages/character-appearance/male-job.json').read_text('utf8'))
    counts = collections.Counter()
    proved = set()

    def active_attachment(slot, animation):
        name = slot_defaults.get(slot)
        keys = old['animations'][animation].get('slots', {}).get(slot, {}).get('attachment', [])
        for key in keys:
            if key.get('time', 0) <= 0:
                name = key.get('name')
        return name

    def verify_pose(skin, animation):
        if (skin, animation) in proved:
            return
        def geometry(value):
            if isinstance(value, dict):
                return {key: geometry(child) for key, child in value.items() if key != 'triangles'}
            if isinstance(value, list):
                return [geometry(child) for child in value]
            return value
        if geometry(old_skins[skin]) != geometry(new_skins[skin]):
            raise ValueError('Selected skin geometry or attachments changed: ' + skin)
        a = old['animations'][animation]
        b = new['animations'][animation]
        changed_bones = {name for name in a.get('bones', {}).keys() | b.get('bones', {}).keys()
                         if a.get('bones', {}).get(name) != b.get('bones', {}).get(name)}
        for field in a.keys() | b.keys():
            if field != 'bones' and a.get(field) != b.get(field):
                raise ValueError('Changed non-bone animation requires resampling: ' + animation)
        if changed_bones:
            affected = set(changed_bones)
            for bone in old['bones']:
                if bone.get('parent') in affected:
                    affected.add(bone['name'])
            attachment_set = old_skins[skin].get('attachments', {})
            for slot, attachments in attachment_set.items():
                slot_bone = next(s['bone'] for s in old['slots'] if s['name'] == slot)
                if slot_bone in affected:
                    raise ValueError('Changed animation influences selected skin: ' + skin)
                for att in attachments.values():
                    vertices = att.get('vertices', [])
                    uvs = att.get('uvs', [])
                    if not uvs or len(vertices) == len(uvs):
                        continue
                    cursor = 0
                    while cursor < len(vertices):
                        number = int(vertices[cursor]); cursor += 1
                        for _ in range(number):
                            bone = old['bones'][int(vertices[cursor])]['name']
                            if bone in affected:
                                raise ValueError('Changed weighted bone requires resampling: ' + skin)
                            cursor += 4
        proved.add((skin, animation))

    def convert_part(part, skin, animation):
        if skin is None or skin.startswith('profession_weapon_'):
            counts['retained_external_weapon'] += 1
            return
        if skin not in old_skins or skin not in new_skins:
            raise ValueError('Unknown official skin: ' + str(skin))
        verify_pose(skin, animation)
        slot = part.get('slot', '')
        if slot.startswith('0:'):
            counts['retained_external_follower'] += 1
            return
        attachments = old_skins[skin].get('attachments', {}).get(slot, {})
        candidates = [(name, value) for name, value in attachments.items()
                      if value.get('triangles') == part.get('triangles')]
        if not candidates:
            if len(part.get('triangles', [])) == 6:
                counts['retained_region'] += 1
                return
            raise ValueError('Native mesh has no matching official attachment: ' + skin + '/' + slot)
        if len(candidates) > 1:
            active = active_attachment(slot, animation)
            candidates = [(name, value) for name, value in candidates if name == active]
        if len(candidates) != 1:
            raise ValueError('Ambiguous native attachment: ' + skin + '/' + slot)
        name, before = candidates[0]
        after = new_skins[skin].get('attachments', {}).get(slot, {}).get(name)
        if after is None:
            raise ValueError('Removed attachment requires a fresh pose conversion: ' + skin + '/' + name)
        if {k: v for k, v in before.items() if k != 'triangles'} != {
                k: v for k, v in after.items() if k != 'triangles'}:
            raise ValueError('Attachment geometry changed: ' + skin + '/' + name)

        def coverage(values):
            # Cyclic rotations retain winding; reversed triangles differ.
            return collections.Counter(min(tuple(values[i+j:i+3] + values[i:i+j]) for j in range(3))
                                       for i in range(0, len(values), 3))

        if coverage(before['triangles']) != coverage(after['triangles']):
            raise ValueError('Changed triangle coverage requires fresh rendering: ' + skin + '/' + name)
        counts['updated_mesh' if before['triangles'] != after['triangles'] else 'identical_mesh'] += 1
        part['triangles'] = after['triangles']

    paths = [ROOT / 'unity/Assets/StreamingAssets/CharacterCreation/creation-meshes.zip',
             ROOT / 'unity/Assets/StreamingAssets/Character/character-models.zip']
    receipts = []
    staged = []
    for path in paths:
        creation = path.parent.name == 'CharacterCreation'
        receipt_path = (ROOT / 'assets/reference/character-creation/native-mesh-source-manifest.json'
                        if creation else ROOT / 'assets/reference/character-appearance/source-manifest.json')
        receipt = json.loads(receipt_path.read_text('utf8'))
        expected = receipt['package_sha256'] if creation else receipt['runtime_pack']['sha256']
        if sha(path.read_bytes()) != expected:
            raise ValueError('Native package differs from its recorded source: ' + str(path))
        original = preserve(path)
        temporary = path.with_suffix('.zip.current.tmp')
        with zipfile.ZipFile(path) as archive, zipfile.ZipFile(temporary, 'w') as output:
            manifest_name = 'manifest.json' if creation else 'character-model-manifest.json'
            manifest = json.loads(archive.read(manifest_name))
            files = {}
            for entry in archive.infolist():
                if entry.filename == manifest_name:
                    continue
                raw = archive.read(entry)
                if creation and entry.filename.startswith('male-') and entry.filename.endswith('.json'):
                    animation = entry.filename.removeprefix('male-').removesuffix('.json').replace('-', '/', 1)
                    value = json.loads(raw)
                    for part in value['parts']:
                        convert_part(part, part['skin'], animation)
                    raw = encode(value)
                elif not creation and '/male/' in entry.filename and entry.filename.endswith('.mesh.gz'):
                    chunks = Path(entry.filename).parts
                    animation = chunks[-2].replace('_', '/', 1)
                    key = Path(entry.filename).name.removesuffix('.mesh.gz')
                    value = json.loads(gzip.decompress(raw))
                    for part in value:
                        convert_part(part, job['parts'].get(key), animation)
                    raw = gzip.compress(encode(value), mtime=0)
                files[entry.filename] = sha(raw)
                output.writestr(entry, raw)
            manifest['base_manifest'] = manifest['manifest']
            manifest['manifest'] = current
            manifest['current_conversion'] = {'source': new_receipt, 'base_source': old_receipt,
                'previous_package': original,
                'operation': 'Use current official triangle order; retain verified identical pose channels, vertices, UVs, weights and textures.'}
            manifest['files'] = files
            if creation:
                manifest['entries'] = [{'name': name, 'sha256': value} for name, value in sorted(files.items())]
            output.writestr(manifest_name, encode(manifest), compress_type=zipfile.ZIP_DEFLATED)
        record = {'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(temporary.read_bytes()),
                  'bytes': temporary.stat().st_size, 'previous': original}
        staged.append((path, temporary, receipt_path, receipt, creation, record))
    # All selected skin/animation combinations and output packages passed before activation.
    for path, temporary, receipt_path, receipt, creation, record in staged:
        preserve(receipt_path)
        temporary.replace(path)
        if creation:
            with zipfile.ZipFile(path) as archive:
                receipt = json.loads(archive.read('manifest.json'))
            receipt.update(package_sha256=record['sha256'], package_bytes=record['bytes'])
        else:
            receipt['runtime_pack'].update(sha256=record['sha256'], bytes=record['bytes'], manifest=current,
                current_conversion_source=reference.relative_to(ROOT).as_posix())
        receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
        receipts.append(record)
    result = {'manifest': current, 'base_manifest': '854_156383', 'skeleton_sources': [old_receipt, new_receipt],
              'operation': 'Convert native cached mesh topology after verifying unchanged pose evaluation inputs.',
              'counts': dict(counts), 'verified_skin_animations': sorted([list(pair) for pair in proved]),
              'packages': receipts, 'generator_sha256': sha(Path(__file__).read_bytes())}
    reference.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return result


if __name__ == '__main__':
    print(json.dumps(build()['counts']))
