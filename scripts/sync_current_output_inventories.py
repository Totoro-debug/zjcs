"""Bind active output inventories to verified current conversions, retaining inputs."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_character_meshes import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(activate=False):
    version = OfficialSource().version
    source_paths = [ROOT / 'assets/reference/official-map' / name for name in (
        'current-table-source-manifest.json', 'current-combat-source-manifest.json',
        'current-material-source-manifest.json', 'current-home-layers-source-manifest.json',
        'current-language-source-manifest.json', 'current-sprites-source-manifest.json',
        'current-animators-source-manifest.json', 'current-shared-table-bindings-source-manifest.json')]
    source_paths += [ROOT / 'assets/reference/character-creation' / name for name in (
        'current-mesh-source-manifest.json', 'current-native-prefabs-source-manifest.json')]
    edges = {}
    sizes = {}
    evidence = []
    for path in source_paths:
        if not path.exists():
            continue
        raw = path.read_bytes()
        source = json.loads(raw)
        if source.get('manifest') != version:
            raise ValueError('Unexpected conversion profile: ' + str(path))
        evidence.append({'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(raw)})
        records = source.get('outputs', source.get('packages', source.get('conversions', {}).get('outputs', [])))
        for record in records:
            before = record.get('previous_sha256', record.get('previous', {}).get('sha256'))
            after = record.get('sha256')
            if not before or not after or before == after:
                continue
            if before in edges and edges[before] != after:
                raise ValueError('Ambiguous conversion of shared bytes: ' + before)
            edges[before] = after
            payload = ROOT / record['path']
            if 'bytes' in record:
                sizes[after] = record['bytes']
            elif payload.exists() and sha(payload.read_bytes()) == after:
                sizes[after] = payload.stat().st_size
    # Follow the actual conversion chain (table -> combat, for example).
    def final(digest):
        seen = set()
        while digest in edges:
            if digest in seen:
                raise ValueError('Cyclic conversion evidence')
            seen.add(digest)
            digest = edges[digest]
        return digest

    def replace(value, location=''):
        active = location.startswith(('/outputs/', '/output/', '/resources/', '/assets/', '/files/',
                                      '/data_sha256/', '/source_outputs/'))
        active |= location in ('/output_sha256', '/outputSha256', '/unity_catalog_sha256', '/runtime_rules/sha256')
        if isinstance(value, str):
            return final(value) if active else value
        if isinstance(value, list):
            return [replace(child, location + '/' + str(i)) for i, child in enumerate(value)]
        if isinstance(value, dict):
            result = {key: replace(child, location + '/' + key) for key, child in value.items()}
            if active and result.get('sha256') != value.get('sha256') and result.get('sha256') in sizes:
                for key in ('bytes', 'size'):
                    if key in result:
                        result[key] = sizes[result['sha256']]
            return result
        return value

    paths = set((ROOT / 'assets/reference').glob('**/*manifest*.json'))
    paths.update((ROOT / 'unity/Assets/Official').glob('**/*manifest*.json'))
    paths.update((ROOT / 'unity/Assets/StreamingAssets').glob('**/*manifest*.json'))
    updates = []
    staged = []
    for path in sorted(paths):
        if path.name.startswith('current-') or 'history' in path.parts:
            continue
        raw = path.read_bytes()
        try:
            before = json.loads(raw)
        except (ValueError, UnicodeDecodeError):
            continue
        after = replace(before)
        if before == after:
            continue
        if not isinstance(after, dict):
            raise ValueError('Output inventory root must be an object: ' + str(path))
        after['current_output_conversion'] = {'manifest': version, 'sources': evidence}
        payload = (json.dumps(after, ensure_ascii=False, indent=2) + '\n').encode('utf8')
        staged.append((path, raw, payload))
        updates.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                        'sha256': sha(payload)})
        print(updates[-1]['path'], flush=True)
    if activate and staged:
        for path, raw, _ in staged:
            if path.read_bytes() != raw:
                raise ValueError('Output inventory changed during staging: ' + str(path))
        for (path, _, payload), record in zip(staged, updates):
            record['previous'] = preserve(path)
            temporary = path.with_name(path.name + '.current.tmp')
            temporary.write_bytes(payload)
            temporary.replace(path)
        result = {'manifest': version, 'operation': __doc__, 'sources': evidence,
                  'outputs': updates, 'generator_sha256': sha(Path(__file__).read_bytes())}
        destination = ROOT / 'assets/reference/official-map/current-output-inventories-source-manifest.json'
        if destination.exists():
            previous = json.loads(destination.read_bytes())
            result['previous_receipt'] = preserve(destination)
            result['prior_outputs'] = previous.get('prior_outputs', []) + previous.get('outputs', [])
        destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return updates


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    print('Updated output inventories:', len(build(parser.parse_args().activate)))
