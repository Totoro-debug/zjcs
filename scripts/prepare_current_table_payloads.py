"""Stage current official tables in existing payloads, preserving local projections.

Only complete tables and exact official rows are replaced. Unknown projections
are reported rather than guessed. This command does not activate staged files.
"""
from __future__ import annotations

import hashlib
import itertools
import json
import re
import copy
from collections import defaultdict
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource


def encode(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf8')


def fingerprint(value):
    return hashlib.sha256(encode(value)).hexdigest()


def primary_key(old, new):
    if not old or not new or not all(isinstance(row, dict) for row in old + new):
        return None
    columns = [key for key, value in old[0].items()
               if re.search(r'(Id|Key\d*|Type|Level|Rank|Index|Order)$', key)
               and isinstance(value, (int, str)) and not isinstance(value, bool)]
    for size in range(1, min(4, len(columns)) + 1):
        for keys in itertools.combinations(columns, size):
            try:
                if all(len({tuple(row[key] for key in keys) for row in rows}) == len(rows)
                       for rows in (old, new)):
                    return keys
            except (KeyError, TypeError):
                continue
    return None


def stage():
    profile = OfficialSource()
    from current_conversion_state import activated_receipt
    prior = activated_receipt(ROOT / 'assets/reference/official-map/current-table-source-manifest.json', profile.version)
    if prior is not None:
        return prior['conversions']
    source = ROOT / '.tools/source-packages'
    old_path = source / 'official-data/tables.json'
    new_path = source / 'current' / profile.version / 'official-data/tables.json'
    old = json.loads(old_path.read_text('utf-8-sig'))
    new = json.loads(new_path.read_text('utf-8-sig'))
    changed = {name for name in old.keys() & new.keys() if old[name] != new[name]}
    row_maps = {}
    for name in changed:
        if not isinstance(old[name], list):
            continue
        keys = primary_key(old[name], new[name])
        if keys:
            latest = {tuple(row[key] for key in keys): row for row in new[name]}
            row_maps[name] = {fingerprint(row): latest.get(tuple(row[key] for key in keys))
                              for row in old[name]}
    exact_rows = defaultdict(list)
    for name, mapping in row_maps.items():
        for key, value in mapping.items():
            if value is not None:
                exact_rows[key].append((name, value))
    output = source / 'current' / profile.version / 'table-payloads'
    output.mkdir(parents=True, exist_ok=True)
    records = []

    def add_record(path, converted, changes):
        data = path.read_bytes()
        result = json.dumps(converted, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        digest = hashlib.sha256(result).hexdigest()
        staged = output / (digest + '.json')
        staged.write_bytes(result)
        record = {'path': path.relative_to(ROOT).as_posix(),
                  'previous_sha256': hashlib.sha256(data).hexdigest(),
                  'sha256': digest, 'bytes': len(result),
                  'staged': staged.relative_to(ROOT).as_posix(), 'changes': changes}
        records.append(record)
        print(record['path'], len(changes), flush=True)
    candidates = set()
    for base in ('unity/Assets/Official', 'unity/Assets/StreamingAssets'):
        candidates.update((ROOT / base).rglob('*.json'))
    candidates.update((ROOT / 'assets/official').glob('*.json'))
    for path in sorted(candidates):
        if 'manifest' in path.name or path.stat().st_size > 100_000_000:
            continue
        data = path.read_bytes()
        try:
            document = json.loads(data)
        except (ValueError, UnicodeDecodeError):
            continue
        changes = []

        def visit(value, location='', field=None):
            if field in changed:
                if value == old[field]:
                    changes.append({'pointer': location, 'table': field, 'operation': 'complete_table'})
                    return new[field]
                mapping = row_maps.get(field)
                if mapping and isinstance(value, list) and value:
                    hashes = [fingerprint(row) for row in value]
                    # A filtered table keeps its existing membership. A missing
                    # current row needs an explicit filter decision.
                    if all(key in mapping and mapping[key] is not None for key in hashes):
                        result = [mapping[key] for key in hashes]
                        if result != value:
                            changes.append({'pointer': location, 'table': field, 'operation': 'filtered_rows'})
                        return result
            if isinstance(value, dict):
                if len(value) >= 5 and any(key in value for key in ('Id', 'ClassId', 'GroupId')):
                    matches = exact_rows.get(fingerprint(value), [])
                    if matches and all(row == matches[0][1] for _, row in matches):
                        result = matches[0][1]
                        if result != value:
                            changes.append({'pointer': location, 'table': matches[0][0], 'operation': 'exact_row'})
                        return result
                return {key: visit(child, location + '/' + key.replace('~', '~0').replace('/', '~1'), key)
                        for key, child in value.items()}
            if isinstance(value, list):
                return [visit(child, location + '/' + str(i)) for i, child in enumerate(value)]
            return value

        converted = visit(document)
        if not changes:
            continue
        add_record(path, converted, changes)

    # Reuse each module's projection logic. Verify the old projection against
    # the installed field before replacing it, retaining unrelated local work.
    import prepare_unity_home_runtime as home
    original_read = home.read
    staged_by_path = {row['path']: row for row in records}

    def current_read(path):
        row = staged_by_path.get(Path(path).relative_to(ROOT).as_posix())
        return json.loads((ROOT / row['staged']).read_bytes()) if row else original_read(path)

    icon_receipt_path = ROOT / 'assets/reference/commerce-components/current-gift-icons-source-manifest.json'
    icon_receipt = json.loads(icon_receipt_path.read_bytes()) if icon_receipt_path.exists() else None
    if icon_receipt:
        prior_icons = json.loads((ROOT / icon_receipt['previous']['preserved']).read_bytes())
        home.read = lambda path: prior_icons if Path(path) == ROOT / 'assets/official/icons.json' else original_read(path)
    try:
        before = home.load_rules()
    finally:
        home.read = original_read
    home.read = current_read
    try:
        after = home.load_rules()
    finally:
        home.read = original_read

    def project_fields(path, before, after, generator):
        installed = json.loads(path.read_bytes())
        changes = []
        def merge(target, old, new, location=''):
            for field in old.keys() | new.keys():
                if old.get(field) == new.get(field):
                    continue
                pointer = location + '/' + field
                if all(isinstance(v, dict) for v in (target.get(field), old.get(field), new.get(field))):
                    merge(target[field], old[field], new[field], pointer)
                    continue
                values = (target.get(field), old.get(field), new.get(field))
                if all(isinstance(v, list) and v and all(isinstance(row, dict) for row in v) for v in values):
                    key = next((key for key in ('id', 'classId', 'ClassId', 'Id')
                                if all(all(key in row for row in rows)
                                       and len({row[key] for row in rows}) == len(rows) for rows in values)), None)
                    if key:
                        indexed = [{row[key]: row for row in rows} for rows in values]
                        if indexed[1].keys() == indexed[2].keys() and indexed[1].keys() <= indexed[0].keys():
                            for identity in indexed[1]:
                                merge(indexed[0][identity], indexed[1][identity], indexed[2][identity],
                                      pointer + '/id=' + str(identity))
                            continue
                if target.get(field) not in (old.get(field), new.get(field)):
                    raise ValueError('Installed projection differs from its verified baseline: ' + str(path) + ':' + pointer)
                target[field] = new.get(field)
                changes.append({'pointer': pointer, 'operation': 'module_projection',
                                'generator': generator,
                                'generator_sha256': hashlib.sha256((ROOT / generator).read_bytes()).hexdigest()})
        merge(installed, before, after)
        if changes:
            add_record(path, installed, changes)

    project_fields(ROOT / 'unity/Assets/StreamingAssets/Home/rules.json', before, after,
                   'scripts/prepare_unity_home_runtime.py')

    import prepare_unity_commerce_catalog as commerce
    before = commerce.build()
    original_payload, original_manifest = commerce.PAYLOAD, commerce.MANIFEST
    commerce_stage = output / 'commerce-inputs'
    commerce_stage.mkdir(exist_ok=True)
    receipt = json.loads(original_manifest.read_bytes())
    for entry in receipt['files']:
        name = Path(entry['path']).name
        original = original_payload / name
        if not original.exists():
            continue
        row = staged_by_path.get(original.relative_to(ROOT).as_posix())
        content = (ROOT / row['staged']).read_bytes() if row else original.read_bytes()
        (commerce_stage / name).write_bytes(content)
        entry['sha256'] = hashlib.sha256(content).hexdigest()
    (commerce_stage / 'source-manifest.json').write_bytes(encode(receipt))
    commerce.PAYLOAD, commerce.MANIFEST = commerce_stage, commerce_stage / 'source-manifest.json'
    try:
        after = commerce.build()
    finally:
        commerce.PAYLOAD, commerce.MANIFEST = original_payload, original_manifest
    project_fields(ROOT / 'unity/Assets/Game/Resources/Commerce/commerce-runtime.json', before, after,
                   'scripts/prepare_unity_commerce_catalog.py')
    receipt = {'manifest': profile.version, 'base_manifest': profile.apk_version,
               'operation': __doc__, 'inputs': [
                   {'path': path.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                   for path in (old_path, new_path)],
               'generator_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'outputs': records}
    (output / 'source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


if __name__ == '__main__':
    print('Staged payloads:', len(stage()['outputs']))
