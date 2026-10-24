#!/usr/bin/env python3
"""Package pinned official mesh frames for native Unity creation previews."""
import gzip
import json
from pathlib import Path
import zipfile
from prepare_character_model_runtime import ROOT, SOURCE_MANIFEST, RELATED_MANIFESTS, source_bytes, digest, meta

GROUP_MANIFEST = ROOT / 'assets/reference/character-creation/appearance-group-icons-source-manifest.json'
ACTION_MANIFESTS = {
    'male': ROOT / 'assets/reference/official-map/player-actions-source-manifest.json',
    'female': ROOT / 'assets/reference/character-genders/source-manifest.json',
}
ACTION_FILES = {
    'male': ROOT / 'assets/official/map-player-actions.json',
    'female': ROOT / 'assets/characters/female/map-actions.json',
}


def build():
    source = json.loads(SOURCE_MANIFEST.read_text('utf-8'))
    if source['manifest'] != '854_156383':
        raise ValueError('Unpinned mesh source')
    outputs = dict(source['outputs'])
    receipts = []
    for path in (SOURCE_MANIFEST, *RELATED_MANIFESTS, GROUP_MANIFEST):
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
    groups = json.loads(GROUP_MANIFEST.read_text('utf-8'))
    if groups.get('manifest') != '854_156383' or len(groups.get('groups', [])) != 12:
        raise ValueError('Appearance group source is not the pinned 12-group manifest')
    player_actions = {}
    action_skin_groups = {}
    for sex, manifest_path in ACTION_MANIFESTS.items():
        action_manifest = json.loads(manifest_path.read_text('utf-8'))
        if action_manifest.get('manifest') != '854_156383':
            raise ValueError(f'{sex} player action source is not pinned to 854_156383')
        action_data_path = ACTION_FILES[sex]
        action_bytes = action_data_path.read_bytes()
        action_hashes = action_manifest.get('outputs', {})
        expected = next((value for key, value in action_hashes.items()
                         if Path(key.replace('\\\\', '/')).name == action_data_path.name), None)
        if not expected or digest(action_bytes) != expected:
            raise ValueError(f'{sex} player action output hash differs from its source manifest')
        player_actions[sex] = json.loads(action_bytes)
        action_skin_groups[sex] = {skin_name: group
                                   for group, skin_name in action_manifest['parts'].items()}
        receipts.append({'path': manifest_path.relative_to(ROOT).as_posix(),
                         'sha256': digest(manifest_path.read_bytes())})
        receipts.append({'path': action_data_path.relative_to(ROOT).as_posix(),
                         'sha256': expected})
    textures = set()
    pose_skin_ids = {}
    for sex_index, sex in enumerate(('male', 'female')):
        ids = set(rules['defaults'][f'{sex_index}:0'].values())
        ids.update(item['skin_id'] for group in options['bodies'][sex].values() for item in group)
        ids.update(skin['skin_id'] for group in groups['groups'] if group['sex'] == sex
                   for skin in group['appearance_skins'])
        pose_skin_ids[sex] = {}
        for pose in ('home/idle_01', 'map/run_01', 'map/portal_01'):
            is_action = pose == 'map/portal_01'
            pose_data = (player_actions[sex]['animations'][pose] if is_action
                         else json.loads(read(catalog['poses'][sex][pose])))
            action_groups = action_skin_groups[sex]
            pose_ids = ({skin_id for skin_id in ids
                         if rules['skins'][str(skin_id)]['SkinName'] in action_groups}
                        if is_action else ids | compatible_skin_ids(sex, pose_data, rules, catalog))
            pose_skin_ids[sex][pose] = sorted(pose_ids)
            skin_entries = {}
            for skin_id in sorted(pose_ids):
                skin = rules['skins'][str(skin_id)]
                if is_action:
                    action_group = action_groups[skin['SkinName']]
                    action_parts = pose_data['parts'].get(action_group)
                    if action_parts is None:
                        raise ValueError(f'Missing official action skin {sex}/{skin["SkinName"]}')
                    parts = []
                    for slot, samples in action_parts.items():
                        variants = {}
                        for sample in samples:
                            if not sample.get('vertices') or not sample.get('triangles'):
                                continue
                            topology = json.dumps((sample['texture'], sample['uv'], sample['triangles']),
                                                  separators=(',', ':'))
                            variants.setdefault(topology, sample)
                        for topology, template in variants.items():
                            template_key = json.dumps((template['texture'], template['uv'],
                                                       template['triangles']), separators=(',', ':'))
                            frames = []
                            for sample in samples:
                                sample_key = (json.dumps((sample['texture'], sample['uv'],
                                                          sample['triangles']), separators=(',', ':'))
                                              if sample.get('vertices') and sample.get('triangles') else None)
                                frames.append({'vertices': sample.get('vertices', [])
                                               if sample_key == template_key else [],
                                               'order': sample.get('order', template['order']),
                                               'has_order': True})
                            part = dict(template, skin=skin['SkinName'],
                                        skin_type=skin['SkinType'], frames=frames)
                            parts.append(part)
                            textures.add(part['texture'])
                else:
                    key = catalog['parts'][sex].get(str(skin_id), f'skin_{skin_id}')
                    uri = pose_data['parts'].get(key)
                    if uri is None:
                        uri = pose_data['parts'].get(f'profession_weapon_{skin_id}')
                    if uri is None:
                        raise ValueError(f'Missing creation skin {sex}/{pose}/{skin_id}')
                    parts = []
                    for part in json.loads(gzip.decompress(read(uri))):
                        textures.add(part['texture'])
                        parts.append(dict(part, skin=skin['SkinName'], skin_type=skin['SkinType'],
                                          frames=part.get('idle', [{'vertices': part['vertices']}])) )
                        parts[-1].pop('idle', None)
                entry = f'poses/{sex}/{pose.replace("/", "-")}/skin_{skin_id}.json'
                entries[entry] = json.dumps(
                    {'fps': pose_data['fps'], 'duration': pose_data['duration'], 'parts': parts},
                    separators=(',', ':')).encode()
                skin_entries.setdefault(skin['SkinName'], []).append(entry)
            entries[f'{sex}-{pose.replace("/", "-")}-index.json'] = json.dumps({
                'fps': pose_data['fps'], 'duration': pose_data['duration'],
                'skins': [{'name': name, 'entries': sorted(paths)}
                          for name, paths in sorted(skin_entries.items())],
            }, separators=(',', ':')).encode()
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
                'files': {name: digest(data) for name,data in entries.items()},
                'pose_skin_ids': pose_skin_ids}
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


def compatible_skin_ids(sex, pose_data, rules, catalog):
    """Return official skins whose exact source attachment exists in this pose."""
    result = set()
    for skin_id in rules['skins']:
        key = catalog['parts'][sex].get(str(skin_id), f'skin_{skin_id}')
        if key in pose_data['parts'] or f'profession_weapon_{skin_id}' in pose_data['parts']:
            result.add(int(skin_id))
    return result


if __name__ == '__main__':
    build()
