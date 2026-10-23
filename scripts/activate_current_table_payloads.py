"""Activate staged table conversions after checking official bytes and constructors."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from refresh_current_character_meshes import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def activate():
    apk = Apk.current()
    current = ROOT / '.tools/source-packages/current' / apk.version
    staged_path = current / 'table-payloads/source-manifest.json'
    staged = json.loads(staged_path.read_bytes())
    destination = ROOT / 'assets/reference/official-map/current-table-source-manifest.json'
    if destination.exists():
        previous = json.loads(destination.read_bytes())
        if previous.get('staged_sha256') == sha(staged_path.read_bytes()):
            from current_conversion_state import verify_outputs
            verify_outputs(staged['outputs'], apk.version)
            return previous
    for record in staged['inputs']:
        if sha((ROOT / record['path']).read_bytes()) != record['sha256']:
            raise ValueError('Staged conversion input changed')
    if sha((ROOT / 'scripts/prepare_current_table_payloads.py').read_bytes()) != staged['generator_sha256']:
        raise ValueError('Table projection generator changed since staging')
    tables = json.loads((current / 'official-data/tables.json').read_bytes())
    constructors = Constructors(current / 'official-code')
    mapping = {match[2]: match[1] for match in re.finditer(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    proof_path = current / 'table-proof-source-manifest.json'
    proof_cache = json.loads(proof_path.read_bytes()) if proof_path.exists() else None
    valid_cache = (proof_cache is not None
                   and proof_cache['table_sha256'] == sha((current / 'official-data/tables.json').read_bytes())
                   and proof_cache['bundle_md5'] == apk.index['defaultpackage_assets_config_luban.bundle']
                   and all(sha(constructors.source(name).encode('utf8')) == digest
                           for name, digest in proof_cache['constructors'].items()))
    proofs = proof_cache['tables'] if valid_cache else []
    for obj in ([] if valid_cache else apk.objects('config_luban')):
        if obj.type.name != 'TextAsset':
            continue
        text = obj.read()
        name = text.m_Name.removesuffix('.g')
        if name not in tables:
            continue
        raw = text.m_Script.encode('utf8', 'surrogateescape')
        if raw != (current / 'official-data' / (text.m_Name + '.bytes')).read_bytes():
            raise ValueError('Decoded current table differs from its official TextAsset: ' + name)
        decoded = json.loads(json.dumps(constructors.table(raw, mapping[name]), ensure_ascii=False))
        if decoded != tables[name]:
            raise ValueError('Current table cache differs from fully consumed constructor output: ' + name)
        proofs.append({'table': name, 'sha256': sha(raw), 'bytes': len(raw),
                       'source': apk.provenance(obj), 'constructor': mapping[name],
                       'constructor_sha256': sha(constructors.source(mapping[name]).encode('utf8'))})
    if len(proofs) != len(tables):
        raise ValueError('Current table proof coverage differs: ' + str((len(proofs), len(tables))))
    if not valid_cache:
        proof_cache = {'manifest': apk.version,
                       'table_sha256': sha((current / 'official-data/tables.json').read_bytes()),
                       'bundle_md5': apk.index['defaultpackage_assets_config_luban.bundle'],
                       'constructors': {name: sha(text.encode('utf8')) for name, text in constructors.sources.items()},
                       'tables': proofs}
        proof_path.write_text(json.dumps(proof_cache, ensure_ascii=False, indent=2) + '\n', 'utf8')
    # Check every conversion and every current target before any activation.
    replacements = {}
    for record in staged['outputs']:
        target = ROOT / record['path']
        if sha(target.read_bytes()) != record['previous_sha256']:
            raise ValueError('Table payload changed since staging: ' + record['path'])
        content = (ROOT / record['staged']).read_bytes()
        if len(content) != record['bytes'] or sha(content) != record['sha256']:
            raise ValueError('Staged table payload hash differs: ' + record['path'])
        replacements[record['previous_sha256']] = record['sha256']
    # Dependency source inventories explicitly retain their base profile. Their
    # new output hashes are tied to this conversion receipt and official proofs.
    manifests = set((ROOT / 'assets/reference').glob('**/*manifest*.json'))
    manifests.update((ROOT / 'unity/Assets/Official').glob('**/*manifest*.json'))
    manifests.update((ROOT / 'unity/Assets/StreamingAssets').glob('**/*manifest*.json'))
    updates = []

    def replace_hashes(value, location=''):
        if isinstance(value, str):
            # Update conversion outputs only. Historical input receipts still
            # describe the bytes actually used by their original generators.
            active_output = location.startswith(('/outputs/', '/output/', '/assets/', '/files/',
                                                  '/data_sha256/', '/source_outputs/'))
            active_output |= location in ('/output_sha256', '/unity_catalog_sha256', '/runtime_rules/sha256')
            return replacements.get(value, value) if active_output else value
        if isinstance(value, list):
            return [replace_hashes(child, location + '/' + str(i)) for i, child in enumerate(value)]
        if isinstance(value, dict):
            return {key: replace_hashes(child, location + '/' + key) for key, child in value.items()}
        return value

    for path in sorted(manifests):
        # Dedicated current/historical evidence must remain immutable.
        if path == destination or path.name.startswith('current-') or 'history' in path.parts:
            continue
        original = path.read_bytes()
        try:
            before = json.loads(original)
        except (ValueError, UnicodeDecodeError):
            continue
        after = replace_hashes(before)
        if before == after:
            continue
        if not isinstance(after, dict):
            raise ValueError('Dependent source inventory has no object root: ' + str(path))
        after['current_table_conversion'] = {
            'manifest': apk.version, 'source': destination.relative_to(ROOT).as_posix(),
            'staged_sha256': sha(staged_path.read_bytes())}
        updates.append((path, original, json.dumps(after, ensure_ascii=False, indent=2).encode('utf8')))
    for path, previous, _ in updates:
        if path.read_bytes() != previous:
            raise ValueError('Dependent source inventory changed during conversion')
    history = []
    for record in staged['outputs']:
        path = ROOT / record['path']
        history.append(preserve(path))
        temporary = path.with_name(path.name + '.current.tmp')
        temporary.write_bytes((ROOT / record['staged']).read_bytes())
        temporary.replace(path)
    source_updates = []
    for path, _, result in updates:
        prior = preserve(path)
        path.write_bytes(result)
        source_updates.append({'path': path.relative_to(ROOT).as_posix(), 'previous': prior,
                               'sha256': sha(result)})
    receipt = {'manifest': apk.version, 'base_manifest': staged['base_manifest'],
               'operation': __doc__, 'staged_sha256': sha(staged_path.read_bytes()),
               'tables': proofs, 'conversions': staged, 'preserved': history,
               'updated_source_inventories': source_updates,
               'generator_sha256': sha(Path(__file__).read_bytes())}
    destination.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


if __name__ == '__main__':
    result = activate()
    print('Verified current tables:', len(result['tables']),
          'converted payloads:', len(result['conversions']['outputs']))
