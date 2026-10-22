#!/usr/bin/env python3
"""Package pinned official mesh frames for native Unity creation previews."""
import gzip
import json
from pathlib import Path
import zipfile
from prepare_character_model_runtime import ROOT, SOURCE_MANIFEST, RELATED_MANIFESTS, source_bytes, digest, meta


def build():
    source = json.loads(SOURCE_MANIFEST.read_text('utf-8'))
    if source['manifest'] != '854_156383':
        raise ValueError('Unpinned mesh source')
    outputs = dict(source['outputs'])
    receipts = []
    for path in (SOURCE_MANIFEST, *RELATED_MANIFESTS):
        data = json.loads(path.read_text('utf-8'))
        for key in ('outputs', 'resources'):
            for uri, value in data.get(key, {}).items():
                outputs.setdefault(uri, value)
        receipts.append({'path': path.relative_to(ROOT).as_posix(), 'sha256': digest(path.read_bytes())})
    inputs, aliases, entries = {}, {}, {}
    def read(uri):
        return source_bytes(uri, outputs, inputs, aliases)
    catalog = json.loads(read('res://assets/characters/wardrobe/catalog.json'))
    folder = ROOT / 'unity/Assets/Official/CharacterCreation'
    options = json.loads((folder/'appearance-options.json').read_text('utf-8'))
    rules = json.loads((folder/'wardrobe-rules.json').read_text('utf-8'))
    textures = set()
    for sex_index, sex in enumerate(('male', 'female')):
        ids = set(rules['defaults'][f'{sex_index}:0'].values())
        ids.update(item['skin_id'] for group in options['bodies'][sex].values() for item in group)
        for pose in ('home/idle_01', 'map/run_01'):
            pose_data = json.loads(read(catalog['poses'][sex][pose]))
            parts = []
            for skin_id in sorted(ids):
                skin = rules['skins'][str(skin_id)]
                key = catalog['parts'][sex].get(str(skin_id), f'skin_{skin_id}')
                if skin['SkinType'] in (7, 8):
                    key = f'profession_weapon_{skin_id}'
                uri = pose_data['parts'].get(key, pose_data['parts'].get(f'profession_weapon_{skin_id}'))
                if uri is None:
                    raise ValueError(f'Missing creation skin {sex}/{pose}/{skin_id}')
                for part in json.loads(gzip.decompress(read(uri))):
                    textures.add(part['texture'])
                    parts.append(dict(part, skin=skin['SkinName'], skin_type=skin['SkinType'],
                                      frames=part.get('idle', [{'vertices': part['vertices']}])) )
                    parts[-1].pop('idle', None)
            entries[f'{sex}-{pose.replace("/", "-")}.json'] = json.dumps(
                {'fps': pose_data['fps'], 'duration': pose_data['duration'], 'parts': parts},
                separators=(',', ':')).encode()
    texture_paths = []
    for name in sorted(textures):
        filename = 'textures/' + name + '.png'
        entries[filename] = read(catalog['textures'][name])
        mask = catalog.get('masks', {}).get(name)
        if mask:
            entries['masks/' + name + '.png'] = read(mask)
        texture_paths.append({'name': name, 'texture': filename, 'mask': 'masks/'+name+'.png' if mask else ''})
    entries['textures.json'] = json.dumps({'textures': texture_paths}, separators=(',', ':')).encode()
    manifest = {'manifest': '854_156383', 'operation': 'Reuse pinned official 24 fps mesh frames with native Unity Mesh playback; no Spine runtime dependency.',
                'source_manifests': receipts, 'source_files': inputs, 'source_uri_aliases': aliases,
                'files': {name: digest(data) for name,data in entries.items()}}
    manifest['entries'] = [{'name': name, 'sha256': digest(data)} for name,data in sorted(entries.items())]
    entries['manifest.json'] = json.dumps(manifest, ensure_ascii=False, separators=(',', ':')).encode()
    target = ROOT / 'unity/Assets/StreamingAssets/CharacterCreation/creation-meshes.zip'
    target.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name,data in sorted(entries.items()):
            archive.writestr(name, data)
    meta(target.parent, True)
    meta(target, False)
    manifest['package_sha256'] = digest(target.read_bytes())
    manifest['package_bytes'] = target.stat().st_size
    (ROOT/'assets/reference/character-creation/native-mesh-source-manifest.json').write_text(
        json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Packaged {len(textures)} atlas textures, {target.stat().st_size} bytes')


if __name__ == '__main__':
    build()
