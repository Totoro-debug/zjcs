"""Check activated payloads through recorded, path-specific conversion chains."""
from __future__ import annotations

from collections import defaultdict
import hashlib
import json

from official_apk import ROOT
from official_source import OfficialSource


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def records(document):
    """Read output records only; historical source hashes are never edges."""
    for key in ('outputs', 'prior_outputs', 'updated_source_inventories'):
        rows = document.get(key, [])
        if isinstance(rows, list):
            for row in rows:
                if isinstance(row, dict) and 'path' in row and 'sha256' in row:
                    yield row
    if isinstance(document.get('conversions'), dict):
        yield from records(document['conversions'])


def edges(version):
    result = defaultdict(lambda: defaultdict(set))
    for path in (ROOT / 'assets/reference').glob('**/*source-manifest.json'):
        if not path.name.startswith('current-'):
            continue
        document = json.loads(path.read_bytes())
        if document.get('manifest') != version:
            continue
        for row in records(document):
            old = row.get('previous_sha256') or row.get('previous', {}).get('sha256')
            if old:
                result[row['path']][old].add(row['sha256'])
            for intermediate in row.get('intermediate_outputs', []):
                result[row['path']][intermediate['sha256']].add(row['sha256'])
    return result


def verify_outputs(rows, version=None):
    version = version or OfficialSource().version
    links = None
    for row in rows:
        actual = sha((ROOT / row['path']).read_bytes())
        expected = row['sha256']
        if expected == actual:
            continue
        if links is None:
            links = edges(version)
        pending = [expected]
        visited = set()
        while pending:
            value = pending.pop()
            if value in visited:
                continue
            visited.add(value)
            pending.extend(links[row['path']][value] - visited)
        if actual not in visited:
            raise ValueError('Activated payload has no recorded conversion to its current bytes: '
                             + row['path'] + ' expected=' + expected + ' actual=' + actual)


def activated_receipt(path, version=None):
    version = version or OfficialSource().version
    if not path.exists():
        return None
    result = json.loads(path.read_bytes())
    if result.get('manifest') != version:
        return None
    payloads = result.get('outputs', result.get('conversions', {}).get('outputs', []))
    verify_outputs(payloads, version)
    return result


def bind_official_table_extensions():
    """Record independently added official tables without rewriting shared payloads."""
    version = OfficialSource().version
    table_path = ROOT / '.tools/source-packages/current' / version / 'official-data/tables.json'
    tables = json.loads(table_path.read_bytes())
    proofs = ROOT / '.tools/source-packages/current' / version / 'table-proof-source-manifest.json'
    proof = json.loads(proofs.read_bytes())
    if proof['table_sha256'] != sha(table_path.read_bytes()) or proof['manifest'] != version:
        raise ValueError('Official table cache differs from its fully decoded source proof')
    receipt_path = ROOT / 'assets/reference/official-map/current-combat-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    converted = []
    for row in receipt['outputs']:
        raw = (ROOT / row['path']).read_bytes()
        if sha(raw) == row['sha256']:
            continue
        staged = (ROOT / row['staged']).read_bytes()
        if sha(staged) != row['sha256']:
            raise ValueError('Original current conversion stage changed: ' + row['path'])
        old, current = json.loads(staged), json.loads(raw)
        extensions = {}
        for name in current.get('tables', {}).keys() - old.get('tables', {}).keys():
            value = current['tables'][name]
            if name not in tables or value != tables[name]:
                raise ValueError('Added table differs from official current rows: ' + name)
            extensions[name] = current['tables'].pop(name)
        if current != old or not extensions:
            raise ValueError('Shared payload has changes beyond verified additional tables: ' + row['path'])
        from refresh_current_character_meshes import preserve
        converted.append({'path': row['path'], 'previous_sha256': row['sha256'],
                          'sha256': sha(raw), 'official_tables': sorted(extensions),
                          'preserved': preserve(ROOT / row['path'])})
    result = {'manifest': version,
              'operation': 'Verify retained current combat conversions plus independently integrated official tables; payloads are not rewritten.',
              'inputs': [{'path': table_path.relative_to(ROOT).as_posix(), 'sha256': sha(table_path.read_bytes())},
                         {'path': proofs.relative_to(ROOT).as_posix(), 'sha256': sha(proofs.read_bytes())},
                         {'path': receipt_path.relative_to(ROOT).as_posix(), 'sha256': sha(receipt_path.read_bytes())}],
              'outputs': converted, 'generator_sha256': sha(__import__('pathlib').Path(__file__).read_bytes())}
    destination = ROOT / 'assets/reference/official-map/current-shared-table-bindings-source-manifest.json'
    if destination.exists():
        previous = json.loads(destination.read_bytes())
        if previous['outputs'] == converted:
            return previous
        from refresh_current_character_meshes import preserve
        result['previous_receipt'] = preserve(destination)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bind-official-table-extensions', action='store_true')
    options = parser.parse_args()
    if options.bind_official_table_extensions:
        print('Verified shared official table extensions:', len(bind_official_table_extensions()['outputs']))
    else:
        parser.error('Choose --bind-official-table-extensions')
