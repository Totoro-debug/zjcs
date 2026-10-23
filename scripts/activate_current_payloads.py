"""Activate a staged official conversion after checking every input and target."""
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


def activate(staged_path, generator, destination):
    raw = staged_path.read_bytes()
    result = json.loads(raw)
    if result['manifest'] != OfficialSource().version:
        raise ValueError('Staged conversion has a different official profile')
    if destination.exists():
        prior = json.loads(destination.read_bytes())
        if prior.get('staged_sha256') == sha(raw):
            from current_conversion_state import verify_outputs
            verify_outputs(prior['outputs'], prior['manifest'])
            return prior
        if not result['outputs']:
            return prior
    if sha(generator.read_bytes()) != result['generator_sha256']:
        raise ValueError('Conversion generator changed after staging')
    for record in result['outputs']:
        path = ROOT / record['path']
        if sha(path.read_bytes()) != record['previous_sha256']:
            raise ValueError('Target changed after staging: ' + record['path'])
        payload = (ROOT / record['staged']).read_bytes()
        if sha(payload) != record['sha256'] or ('bytes' in record and len(payload) != record['bytes']):
            raise ValueError('Staged payload changed: ' + record['path'])
    for record in result['outputs']:
        path = ROOT / record['path']
        record['previous'] = preserve(path)
        temporary = path.with_name(path.name + '.current.tmp')
        temporary.write_bytes((ROOT / record['staged']).read_bytes())
        temporary.replace(path)
    result['staged_sha256'] = sha(raw)
    result['activation_generator_sha256'] = sha(Path(__file__).read_bytes())
    if destination.exists():
        result['previous_receipt'] = preserve(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staged', type=Path, required=True)
    parser.add_argument('--generator', type=Path, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    options = parser.parse_args()
    print('Activated converted payloads:', len(activate(options.staged.resolve(), options.generator.resolve(),
                                                       options.destination.resolve())['outputs']))
