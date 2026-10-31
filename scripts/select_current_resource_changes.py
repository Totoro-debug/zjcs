"""Select changed bundles consumed by recorded conversions plus CAB closure."""
import argparse
import gc
import json
import os
from pathlib import Path

from official_apk import ROOT, UnityPy
from prepare_unity_bundle_inputs import serialized_files
from prepare_current_resource_inputs import sha


def files(env):
    for loaded in env.files.values():
        yield from serialized_files(loaded)


def select(cache):
    cache = cache.resolve()
    receipt = json.loads((cache/'source-manifest.json').read_bytes())
    rows = {(r['package'], r['name']): r for r in receipt['inputs'] if 'package' in r}
    by_name = {r['name']: key for key, r in rows.items()}
    if len(by_name) != len(rows):
        raise ValueError('Logical names occur in multiple packages; require explicit package matching')
    same, changed, unmatched, unverified = set(), set(), set(), set()
    proofs = []
    for path in (ROOT/'assets/reference').rglob('*source-manifest.json'):
        raw = path.read_bytes()
        document = json.loads(raw)
        used = False
        def visit(value):
            nonlocal used
            if isinstance(value, dict):
                name = value.get('bundle') or value.get('name')
                digest = value.get('bundle_hash') or value.get('hash') or value.get('raw_md5')
                if isinstance(name, str) and name.endswith('.bundle'):
                    used = True
                    if name not in by_name:
                        unmatched.add(name)
                    else:
                        key = by_name[name]
                        row = rows[key]
                        comparisons = []
                        if isinstance(digest, str) and len(digest) == 32:
                            comparisons.append(row['hash'] == digest)
                        for field in ('raw_sha256', 'decoded_sha256', 'bundle_sha256'):
                            digest = value.get(field)
                            if isinstance(digest, str) and len(digest) == 64:
                                comparisons.append(row['decoded_sha256' if field == 'decoded_sha256' else 'raw_sha256'] == digest)
                        if not comparisons:
                            unverified.add(key)
                        elif all(comparisons):
                            same.add(key)
                        else:
                            changed.add(key)
                for child in value.values():
                    if isinstance(child, (dict, list)):
                        visit(child)
            elif isinstance(value, list):
                for child in value:
                    visit(child)
        visit(document)
        if used:
            proofs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
    owners = {}
    raw_names = {r['package']+'/'+r['file']: key for key,r in rows.items()}
    from official_source import OfficialSource
    cab_index = json.loads((OfficialSource().cloud_path/'verified-cab-files.json').read_bytes())
    for cab, paths in cab_index.items():
        owners[cab.casefold()] = raw_names[paths[0]]
    builtin_rows = [r for r in receipt['inputs'] if r['format'] == 'builtin']
    builtin_cabs = set()
    for row in builtin_rows:
        env = UnityPy.load(str(cache/row['output']))
        for file in files(env):
            builtin_cabs.add(file.name.casefold())
    builtin_cabs.update(('unity default resources', 'unity_builtin_extra', 'globalgamemanagers', 'globalgamemanagers.assets', 'resources.assets'))
    # References without byte identity also require conversion; an address-only
    # record cannot prove that an existing native asset matches current bytes.
    unverified -= same | changed
    selected, pending, missing = set(), list(changed | unverified), []
    while pending:
        key = pending.pop()
        if key in selected:
            continue
        selected.add(key)
        row = rows[key]
        if row['format'] != 'UnityFS':
            continue
        env = UnityPy.load(str(cache/row['output']))
        for file in files(env):
            for external in file.externals:
                cab = external.path.rsplit('/', 1)[-1].casefold()
                if cab in builtin_cabs:
                    continue
                owner = owners.get(cab)
                if owner is None:
                    missing.append(dict(owner=row['name'], external=external.path))
                elif owner not in selected:
                    pending.append(owner)
        if len(selected) % 100 == 0:
            print('CAB closure bundles:', len(selected), 'pending:', len(pending), flush=True)
            gc.collect()
    if missing:
        raise ValueError('Missing CAB dependencies: ' + repr(missing))
    target = cache/'changed-inputs'
    target.mkdir(exist_ok=True)
    staged = [rows[key] for key in sorted(selected)] + builtin_rows
    for row in staged:
        relative = Path(row['output']).relative_to('inputs')
        destination = target/relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            os.link(cache/row['output'], destination)
        elif sha(destination.read_bytes()) != row['decoded_sha256']:
            raise ValueError('Retained selected input has different bytes: ' + str(destination))
    for path in (cache/'inputs/Managed').glob('*.dll'):
        destination = target/'Managed'/path.name
        destination.parent.mkdir(exist_ok=True)
        if not destination.exists():
            os.link(path, destination)
    result = dict(manifest=receipt['manifest'], input_manifest_sha256=sha((cache/'source-manifest.json').read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), source_proofs=proofs,
                  unchanged_bundles=[list(key) for key in sorted(same)], changed_bundles=[list(key) for key in sorted(changed)],
                  unverified_referenced_bundles=[list(key) for key in sorted(unverified)],
                  unmatched_retained=sorted(unmatched), inputs=staged, missing_dependencies=missing)
    (cache/'changed-inputs-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Unchanged:', len(same), 'changed:', len(changed), 'without byte proof:', len(unverified), 'with dependencies:', len(selected), 'unmatched retained:', len(unmatched), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    select(parser.parse_args().cache)
