"""Convert verified official home moon data into Unity StreamingAssets."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil


ROOT = Path(__file__).resolve().parents[1]
SOURCE_MANIFEST = ROOT / 'assets/reference/home-environment/source-manifest.json'
SOURCE_JSON = ROOT / 'assets/official/home-environment/moon.json'
SOURCE_SPRITES = ROOT / 'assets/official/home-environment/sprites'
SKY_JSON = ROOT / 'assets/official/home-environment/sky-timeline.json'
SKY_SPRITES = ROOT / 'assets/official/home-environment/day-sky-sprites'
SKY_MANIFEST = ROOT / 'assets/reference/home-environment/solar-timeline-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/StreamingAssets/Home/environment-visuals'
PREFIX = 'res://'


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def source_path(uri: str) -> Path:
    if not uri.startswith(PREFIX):
        raise ValueError('Unexpected official asset URI: ' + uri)
    return ROOT / Path(uri[len(PREFIX):])


def require_hash(path: Path, expected: str) -> None:
    if not path.is_file():
        raise FileNotFoundError('Official home moon input is missing: ' + str(path))
    actual = sha256(path)
    if actual != expected:
        raise ValueError(f'Official home moon hash mismatch for {path}: {actual} != {expected}')


def unity_meta(path: Path, relative: str, folder: bool = False) -> str:
    guid = hashlib.sha256(('zjcs-home-environment:' + relative).encode('utf-8')).hexdigest()[:32]
    folder_flag = 'folderAsset: yes\n' if folder else ''
    return (
        'fileFormatVersion: 2\n'
        f'guid: {guid}\n'
        f'{folder_flag}'
        'DefaultImporter:\n'
        '  externalObjects: {}\n'
        '  userData: \n'
        '  assetBundleName: \n'
        '  assetBundleVariant: \n'
    )


def write_generated(path: Path, content: bytes, description: str, replace: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == content:
        return
    if path.exists() and not replace:
        raise FileExistsError('Refusing to overwrite a different existing payload: ' + str(path))
    path.write_bytes(content)
    print(f'Wrote {description}: {path.relative_to(ROOT)}')


def prepare() -> None:
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding='utf-8'))
    moon_uri = 'res://assets/official/home-environment/moon.json'
    require_hash(SOURCE_JSON, manifest['outputs'][moon_uri])
    moon = json.loads(SOURCE_JSON.read_text(encoding='utf-8'))

    layers = []
    sprite_sources: dict[str, Path] = {}
    variants = moon.get('variants') or {}
    for phase in sorted(variants, key=int):
        variant = variants[phase]
        for kind in ('moon', 'outline', 'glow'):
            item = variant.get(kind)
            if not item:
                raise ValueError(f'Official moon phase {phase} has no {kind} layer.')
            sprite = item['sprite']
            uri = sprite['file']
            path = source_path(uri)
            require_hash(path, manifest['outputs'][uri])
            filename = path.name
            previous = sprite_sources.get(filename)
            if previous is not None and previous != path:
                raise ValueError('Official moon sprite filename collision: ' + filename)
            sprite_sources[filename] = path
            local_path = moon['local_trs'][item['path']]['scale']
            layers.append({
                'phase': int(phase),
                'kind': kind,
                'name': sprite['name'],
                'file': filename,
                'vertices': [coordinate for point in item['vertices'] for coordinate in point],
                'color': item['color'],
                'scale': local_path,
                'sortingLayer': item['sorting_layer'],
                'sortingOrder': item['sorting_order'],
            })

    clip = next((entry for entry in moon.get('day_clips', []) if entry.get('name') == 'Night_Common'), None)
    if clip is None:
        raise ValueError('The official Night_Common moon animation clip is missing.')
    tracks = []
    for track in clip.get('tracks', []):
        keys = []
        stepped = track['property'] == 'm_IsActive'
        for raw in track.get('keys', []):
            if len(raw) < 2:
                raise ValueError('Invalid Night_Common animation key in ' + track['property'])
            keys.append({
                'time': raw[0],
                'value': raw[1],
                'inTangent': 0.0 if len(raw) < 4 or raw[3] is None else raw[3],
                'outTangent': 0.0 if len(raw) < 3 or raw[2] is None else raw[2],
            })
        tracks.append({
            'path': track['path'],
            'property': track['property'],
            'stepped': stepped,
            'keys': keys,
        })

    root_path = moon['root_path']
    scale_path = root_path + '/Scale'
    runtime_data = {
        'sourceManifest': '854_156383',
        'sourceMoonJsonSha256': manifest['outputs'][moon_uri],
        'rootPath': root_path,
        'rootPosition': moon['local_trs'][root_path]['position'],
        'scalePosition': moon['local_trs'][scale_path]['position'],
        'scaleBasePosition': moon['local_trs'][scale_path]['position'],
        'duration': clip['duration'],
        'loop': clip['loop'],
        'layers': layers,
        'tracks': tracks,
    }
    json_bytes = (json.dumps(runtime_data, ensure_ascii=False, separators=(',', ':')) + '\n').encode('utf-8')
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for filename, path in sprite_sources.items():
        write_generated(DESTINATION / 'sprites' / filename, path.read_bytes(), 'official moon sprite')
        sprite_dir = DESTINATION / 'sprites'
        sprite_rel = (sprite_dir / filename).relative_to(ROOT).as_posix()
        meta = unity_meta(sprite_dir / (filename + '.meta'), sprite_rel)
        write_generated(sprite_dir / (filename + '.meta'), meta.encode('utf-8'), 'Unity metadata')
    payload_path = DESTINATION / 'moon-visuals.json'
    write_generated(payload_path, json_bytes, 'moon visual data', replace=True)
    payload_rel = payload_path.relative_to(ROOT).as_posix()
    write_generated(payload_path.with_suffix(payload_path.suffix + '.meta'),
                     unity_meta(payload_path, payload_rel).encode('utf-8'), 'Unity metadata')
    sprites_dir = DESTINATION / 'sprites'
    sprites_rel = sprites_dir.relative_to(ROOT).as_posix()
    write_generated(sprites_dir.with_suffix('.meta'),
                     unity_meta(sprites_dir, sprites_rel, folder=True).encode('utf-8'), 'Unity folder metadata')

    if SKY_JSON.exists():
        sky_manifest = json.loads(SKY_MANIFEST.read_text(encoding='utf-8'))
        sky_uri = 'assets/official/home-environment/sky-timeline.json'
        require_hash(SKY_JSON, sky_manifest['outputs'][sky_uri])
        sky_data = json.loads(SKY_JSON.read_text(encoding='utf-8'))
        if sky_data.get('sourceManifest') != '854_156383' or not sky_data.get('tracks'):
            raise ValueError('Official Home sky timeline payload is incomplete or unpinned.')
        for renderer in sky_data.get('renderers', []):
            filename = renderer.get('file', '')
            sprite_path = SKY_SPRITES / filename
            sprite_uri = 'assets/official/home-environment/day-sky-sprites/' + filename
            if not filename or sprite_uri not in sky_manifest['outputs']:
                raise ValueError('Official Home sky sprite is not registered in its source manifest: ' + filename)
            require_hash(sprite_path, sky_manifest['outputs'][sprite_uri])
            target = DESTINATION / 'sprites' / 'day-sky' / filename
            write_generated(target, sprite_path.read_bytes(), 'official daytime sky sprite')
            sprite_rel = target.relative_to(ROOT).as_posix()
            write_generated(target.with_suffix(target.suffix + '.meta'),
                             unity_meta(target, sprite_rel).encode('utf-8'), 'Unity metadata')
        sky_target = DESTINATION / 'day-sky-timeline.json'
        write_generated(sky_target, SKY_JSON.read_bytes(), 'official daytime sky timeline', replace=True)
        sky_rel = sky_target.relative_to(ROOT).as_posix()
        write_generated(sky_target.with_suffix(sky_target.suffix + '.meta'),
                         unity_meta(sky_target, sky_rel).encode('utf-8'), 'Unity metadata')
        day_sky_dir = DESTINATION / 'sprites' / 'day-sky'
        day_sky_rel = day_sky_dir.relative_to(ROOT).as_posix()
        write_generated(day_sky_dir.with_suffix('.meta'),
                         unity_meta(day_sky_dir, day_sky_rel, folder=True).encode('utf-8'), 'Unity folder metadata')
        print(f'Staged official daytime sky: {len(sky_data["tracks"])} tracks, '
              f'{len(sky_data["renderers"])} renderers.')

    dest_rel = DESTINATION.relative_to(ROOT).as_posix()
    write_generated(DESTINATION.with_suffix('.meta'),
                     unity_meta(DESTINATION, dest_rel, folder=True).encode('utf-8'), 'Unity folder metadata')
    print(f'Verified {len(sprite_sources)} official moon sprites and {len(tracks)} animation tracks.')


if __name__ == '__main__':
    prepare()
