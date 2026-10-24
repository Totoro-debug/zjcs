#!/usr/bin/env python3
"""Package the verified official Fantasy Tower transition events."""
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
SOURCE_MANIFEST = ROOT / 'assets/reference/battle-audio/source-manifest.json'
SOURCE_CATALOG = ROOT / 'assets/official/battle-audio/catalog.json'
OUTPUT_DIR = ROOT / 'unity/Assets/StreamingAssets/TowerPush/audio'
OUTPUT_MAP = OUTPUT_DIR / 'audio.json'
OUTPUT_MANIFEST = ROOT / 'assets/reference/tower-push/audio-source-manifest.json'
EVENTS = {
    'Note_FantasyLadder_Flash': 2367806325,
    'Note_FantasyLadder_Elevator': 828673327,
}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / 'unity').as_posix()
    guid = hashlib.md5(('zjcs-official:' + relative).encode()).hexdigest()
    body = (f'fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n'
            '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    meta = Path(str(path) + '.meta')
    if meta.exists() and meta.read_text(encoding='utf-8') != body:
        raise ValueError('Refusing to replace changed Unity metadata: ' + str(meta))
    if not meta.exists():
        meta.write_text(body, encoding='utf-8', newline='\n')


def build() -> None:
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding='utf-8'))
    if manifest.get('manifest') != '854_156383':
        raise ValueError('Fantasy Tower audio source is not pinned to 854_156383')
    expected_catalog = manifest.get('outputs', {}).get(
        'res://assets/official/battle-audio/catalog.json')
    catalog_bytes = SOURCE_CATALOG.read_bytes()
    if not expected_catalog or digest(catalog_bytes) != expected_catalog:
        raise ValueError('Official battle audio catalog hash differs from its source manifest')
    catalog = json.loads(catalog_bytes)
    events = []
    sources = {}
    outputs = {}
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    asset_meta(OUTPUT_DIR)
    for name, event_id in EVENTS.items():
        event = catalog.get('events', {}).get(name)
        if event is None or event.get('id') != event_id or len(event.get('variants', [])) != 1:
            raise ValueError('Official Fantasy Tower audio event is missing or ambiguous: ' + name)
        variant = event['variants'][0]
        uri = variant.get('file', '')
        resource = manifest.get('resources', {}).get(uri)
        if (not uri.startswith('res://assets/official/battle-audio/') or resource is None
                or variant.get('sha256') != resource.get('sha256')):
            raise ValueError('Fantasy Tower audio source is not covered by the pinned manifest: ' + name)
        source = ROOT / uri.removeprefix('res://')
        data = source.read_bytes()
        if digest(data) != resource['sha256']:
            raise ValueError('Official Fantasy Tower audio file hash differs: ' + name)
        target = OUTPUT_DIR / (name + '.ogg')
        target.write_bytes(data)
        asset_meta(target)
        sources[uri] = resource['sha256']
        outputs[target.relative_to(ROOT).as_posix()] = digest(data)
        events.append({'name': name, 'file': target.name, 'duration': variant['duration'],
                       'sha256': resource['sha256']})
    mapping = {'manifest': '854_156383', 'events': events}
    OUTPUT_MAP.write_text(json.dumps(mapping, ensure_ascii=False, separators=(',', ':')) + '\n',
                          encoding='utf-8', newline='\n')
    asset_meta(OUTPUT_MAP)
    outputs[OUTPUT_MAP.relative_to(ROOT).as_posix()] = digest(OUTPUT_MAP.read_bytes())
    OUTPUT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MANIFEST.write_text(json.dumps({
        'manifest': '854_156383',
        'source_manifest': {'path': SOURCE_MANIFEST.relative_to(ROOT).as_posix(),
                            'sha256': digest(SOURCE_MANIFEST.read_bytes())},
        'source_catalog': {'path': SOURCE_CATALOG.relative_to(ROOT).as_posix(),
                           'sha256': expected_catalog},
        'events': {event['name']: {'id': EVENTS[event['name']], 'source': next(
            uri for uri in sources if sources[uri] == event['sha256']),
            'source_sha256': event['sha256'], 'duration': event['duration']}
                   for event in events},
        'outputs': outputs,
    }, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Packaged official Fantasy Tower audio events:', len(events))


if __name__ == '__main__':
    build()
