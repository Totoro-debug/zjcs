"""Consolidate byte-identical, immutable native textures with identical importers.

Resources addresses, readable/runtime-writable textures and dirty assets are
never removed. The source manifest is also the recipe for finalizing regenerated
outputs; original APK/CAB records and their expected hashes are not rewritten.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
from concurrent.futures import ThreadPoolExecutor
from validation_workspace import temporary_validation_directory

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/shared-textures-source-manifest.json'
GUID = re.compile(rb'(?m)^guid: ([0-9a-f]{32})')
PPTR = re.compile(rb'guid: ([0-9a-f]{32})')


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def importer(raw):
    return GUID.sub(b'guid: GUID', raw).replace(b'\r\n', b'\n')


def checked_path(relative):
    path = ROOT / relative
    if not path.resolve().is_relative_to((ROOT / 'unity/Assets/Official').resolve()):
        raise ValueError('Texture must stay inside Assets/Official: ' + relative)
    if path.is_symlink() or path.is_junction():
        raise ValueError('Texture links are not supported: ' + relative)
    return path


def inspect_groups():
    groups = {}
    for base, dirs, files in os.walk(ROOT / 'unity/Assets/Official'):
        for name in files:
            if not name.endswith('.asset'):
                continue
            path = Path(base) / name
            with path.open('rb') as stream:
                head = stream.read(4096)
                if b'\nTexture2D:' not in head:
                    continue
                checksum = hashlib.sha256(head)
                while block := stream.read(4 * 1024 * 1024):
                    checksum.update(block)
            meta_path = Path(str(path) + '.meta')
            meta = meta_path.read_bytes()
            match = GUID.search(meta)
            if not match or b'NativeFormatImporter:' not in meta:
                continue
            key = checksum.hexdigest(), digest(importer(meta))
            groups.setdefault(key, []).append(dict(path=path.relative_to(ROOT).as_posix(), sha256=key[0],
                meta_sha256=digest(meta), guid=match[1].decode(), bytes=path.stat().st_size))
    return [rows for rows in groups.values() if len(rows) > 1]


def plan(groups):
    dirty = subprocess.check_output(['git', '-c', 'core.quotepath=false', 'diff', '--name-only', 'HEAD'], cwd=ROOT, stderr=subprocess.DEVNULL).decode().splitlines()
    tracked = set(subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0'))
    untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '-z'], cwd=ROOT).decode().split('\0')
    protected_guids = set()
    for relative in set(dirty + untracked):
        if not relative.startswith(('unity/Assets/', 'assets/reference/')):
            continue
        path = ROOT / relative
        if not path.is_file() or path.suffix not in ('.asset', '.prefab', '.mat', '.meta', '.json', '.controller', '.anim', '.unity'):
            continue
        with path.open('rb') as stream:
            head = stream.read(4096)
            if b'\nTexture2D:' in head:
                continue
            raw = head + stream.read()
        protected_guids.update(m.decode() for m in re.findall(rb'[0-9a-f]{32}', raw))
    protected = set()
    if RECEIPT.is_file():
        protected.update(row['canonical']['path'] for row in json.loads(RECEIPT.read_bytes())['textures'])
    for folder, suffix in (('scripts', '*.py'), ('unity/Assets/Game', '*.cs')):
        for script in (ROOT / folder).rglob(suffix):
            for path in re.findall(r'(?:unity/)?Assets/Official/[^\'"\r\n]+?\.asset', script.read_text('utf8', errors='replace')):
                protected.add(path if path.startswith('unity/') else 'unity/' + path)
    rows = []
    for group in groups:
        group = [row for row in group if row['path'] in tracked and row['path'] not in dirty and row['path'] + '.meta' not in dirty]
        if len(group) < 2:
            continue
        # Runtime font atlases and readable data textures have independent state.
        with checked_path(group[0]['path']).open('rb') as stream:
            head = stream.read(4096)
        if b'\nTexture2D:' not in head or not re.search(rb'm_IsReadable: 0\s', head):
            continue
        # Appearance loaders use placeholder object identity as a dictionary key.
        # Two original objects can have identical names, pixels and importers.
        if re.search(rb'(?m)^  m_Name: [^\r\n]*_placeholder\s*$', head):
            continue
        resources = [row for row in group if '/Resources/' in row['path']]
        templates = [row for row in group if row['path'] in protected or row['guid'] in protected_guids]
        canonical = sorted(resources or templates or group, key=lambda r: ('/Shared/' not in r['path'], r['path']))[0]
        for row in group:
            if row == canonical or '/Resources/' in row['path'] or row['path'] in protected or row['guid'] in protected_guids:
                continue
            rows.append(dict(source=row, canonical=canonical))
    return dict(format=1, operation='Share exact non-readable native Texture2D payloads and importers; preserve Resources addresses.',
                textures=rows)


def validate_rows(receipt, allow_missing=False):
    seen = set()
    for row in receipt['textures']:
        source, canonical = row['source'], row['canonical']
        if source['path'] in seen or source['guid'] == canonical['guid']:
            raise ValueError('Ambiguous texture alias')
        seen.add(source['path'])
        if '/Resources/' in source['path']:
            raise ValueError('Preserve Resources addresses')
        for item in (source, canonical):
            path = checked_path(item['path'])
            if allow_missing and item is source and not path.exists():
                if Path(str(path) + '.meta').exists():
                    raise ValueError('Orphan alias metadata: ' + item['path'])
                continue
            if sha(path) != item['sha256'] or sha(Path(str(path) + '.meta')) != item['meta_sha256']:
                raise ValueError('Preserve changed texture/importer: ' + item['path'])
        if source['sha256'] != canonical['sha256']:
            raise ValueError('Texture payload differs')
        old = checked_path(source['path'])
        if old.exists() and importer(Path(str(old) + '.meta').read_bytes()) != importer(
                Path(str(checked_path(canonical['path'])) + '.meta').read_bytes()):
            raise ValueError('Texture importer differs')


def references(receipt):
    aliases = {row['source']['guid'].encode(): row['canonical']['guid'].encode() for row in receipt['textures']}
    paths = {row['source']['path']: row['canonical']['path'] for row in receipt['textures']}
    suffixes = {'.asset', '.prefab', '.mat', '.unity', '.controller', '.anim', '.overrideController', '.meta', '.json'}
    def examine(path):
        with path.open('rb') as stream:
            head = stream.read(4096)
            # Texture2D has null/internal PPtrs and large inline pixel arrays.
            if path.suffix == '.asset' and b'\nTexture2D:' in head:
                return None
            raw = head + stream.read()
        def replace(match):
            # Keep the literal prefix searchable by the regex engine; a leading
            # lookbehind makes scanning large Unity byte arrays very expensive.
            previous = raw[match.start() - 1:match.start()] if match.start() else b''
            if previous and previous.isalpha():
                return match[0]
            return b'guid: ' + aliases.get(match[1], match[1])
        new = PPTR.sub(replace, raw)
        return (path, raw, new) if new != raw else None

    candidates = []
    rg = shutil.which('rg')
    if rg:
        with temporary_validation_directory('texture-references-') as temporary:
            patterns = Path(temporary) / 'guids.txt'
            patterns.write_bytes(b'\n'.join(b'guid: ' + g for g in aliases) + b'\n')
            args = [rg, '--hidden', '--no-ignore', '-a', '-l', '-0', '-F', '-f', str(patterns)]
            for suffix in sorted(suffixes):
                args += ['-g', '*' + suffix]
            args.append(str(ROOT / 'unity/Assets'))
            result = subprocess.run(args, capture_output=True,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
            if result.returncode not in (0, 1):
                raise ValueError('Texture reference scan failed: ' + result.stderr.decode(errors='replace'))
            candidates = [Path(os.fsdecode(p)) for p in result.stdout.split(b'\0') if p]
    else:
        for base, dirs, files in os.walk(ROOT / 'unity/Assets'):
            candidates.extend(Path(base) / n for n in files if Path(n).suffix in suffixes)
    candidates = [p for p in candidates if p.relative_to(ROOT).as_posix().removesuffix('.meta') not in paths]
    with ThreadPoolExecutor(max_workers=8) as executor:
        for offset in range(0, len(candidates), 64):
            for result in executor.map(examine, candidates[offset:offset + 64]):
                if result is not None:
                    yield result


def update_output_records(value, path_map, hash_changes, guid_map):
    """Update local outputs only, retaining all original input/provenance hashes."""
    if isinstance(value, list):
        return [update_output_records(v, path_map, hash_changes, guid_map) for v in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, item in value.items():
        if key in path_map:
            change = hash_changes.get(key)
            result[path_map[key]] = change[1] if change and item == change[0] else item
        elif key in ('outputs', 'output_hashes') and isinstance(item, dict):
            output = {}
            for path, expected in item.items():
                target = path_map.get(path, path)
                change = hash_changes.get(path)
                output[target] = change[1] if change and expected == change[0] else expected
                if target in hash_changes:
                    before, after = hash_changes[target]
                    if output[target] == before:
                        output[target] = after
            result[key] = output
        else:
            result[key] = update_output_records(item, path_map, hash_changes, guid_map)
    # Full repository paths identify installed output records, as distinct from
    # relative source bundle paths and historical CAB/PathID identities.
    for field in ('path', 'output'):
        path = value.get(field)
        if isinstance(path, str) and path.startswith('unity/Assets/'):
            target = path_map.get(path, path)
            result[field] = target
            change = hash_changes.get(path)
            for hash_field in ('sha256', 'output_sha256'):
                if change and result.get(hash_field) == change[0]:
                    result[hash_field] = change[1]
            meta_change = hash_changes.get(path + '.meta')
            if meta_change and result.get('meta_sha256') == meta_change[0]:
                result['meta_sha256'] = meta_change[1]
    # Local pointer objects, not original source GUID identities.
    if 'fileID' in result and result.get('guid') in guid_map:
        result['guid'] = guid_map[result['guid']]
    return result


def update_hash_links(value, changes):
    """Advance only hash edges whose old value matches this exact transaction."""
    if isinstance(value, list):
        return [update_hash_links(v, changes) for v in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, item in value.items():
        if key in changes and isinstance(item, str) and item == changes[key][0]:
            result[key] = changes[key][1]
        else:
            result[key] = update_hash_links(item, changes)
    path = result.get('path')
    if isinstance(path, str) and path in changes and result.get('sha256') == changes[path][0]:
        result['sha256'] = changes[path][1]
    return result


def matching_source_manifests(patterns, tracked):
    """Find affected provenance records before parsing large unrelated inputs."""
    rg = shutil.which('rg')
    if not rg:
        return [p for p in (ROOT / 'assets/reference').rglob('*.json')
                if p != RECEIPT and p.relative_to(ROOT).as_posix() in tracked]
    with temporary_validation_directory('texture-source-') as temporary:
        pattern_file = Path(temporary) / 'paths.txt'
        pattern_file.write_text('\n'.join(sorted(patterns)) + '\n', encoding='utf8')
        result = subprocess.run([rg, '--hidden', '--no-ignore', '-a', '-l', '-0', '-F',
            '-f', str(pattern_file), '-g', '*.json', str(ROOT / 'assets/reference')], capture_output=True,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        if result.returncode not in (0, 1):
            raise ValueError('Source manifest scan failed: ' + result.stderr.decode(errors='replace'))
        return [p for raw in result.stdout.split(b'\0') if raw
                if (p := Path(os.fsdecode(raw))) != RECEIPT and p.relative_to(ROOT).as_posix() in tracked]


def reuse_staged_shared_textures(folder, receipt):
    """Apply proved immutable texture aliases to one newly converted closure."""
    if not RECEIPT.is_file():
        return
    staged = {}
    for name, expected in receipt['outputs'].items():
        path = folder / name
        if digest(path.read_bytes()) != expected:
            raise ValueError('Staged asset changed before texture reuse: ' + name)
        if name.endswith('.meta'):
            match = GUID.search(path.read_bytes())
            if match:
                staged[match[1].decode()] = name[:-5]
    aliases, reused = {}, []
    for row in json.loads(RECEIPT.read_bytes())['textures']:
        source, canonical = row['source'], row['canonical']
        relative = staged.get(source['guid'])
        if relative is None:
            continue
        path = folder / relative
        raw = path.read_bytes()
        target = checked_path(canonical['path'])
        local_meta, target_meta = Path(str(path) + '.meta'), Path(str(target) + '.meta')
        if (b'\nTexture2D:' not in raw[:4096] or not re.search(rb'm_IsReadable: 0\s', raw[:4096])
                or digest(raw) != source['sha256'] or sha(target) != canonical['sha256']
                or source['sha256'] != canonical['sha256']
                or sha(target_meta) != canonical['meta_sha256']
                or importer(local_meta.read_bytes()) != importer(target_meta.read_bytes())):
            raise ValueError('Staged texture/importer differs from proved shared source: ' + relative)
        aliases[source['guid'].encode()] = canonical['guid'].encode()
        reused.append(dict(staged_path=relative, staged_sha256=digest(raw), canonical=canonical))
    # All original output and alias checks finish before changing staged bytes.
    for row in reused:
        receipt['outputs'].pop(row['staged_path'])
        receipt['outputs'].pop(row['staged_path'] + '.meta')
        target = row['canonical']
        receipt['inputs'][target['path']] = target['sha256']
        receipt['inputs'][target['path'] + '.meta'] = target['meta_sha256']
    for name in receipt['outputs']:
        path = folder / name
        raw = path.read_bytes()
        if name.endswith('.meta') or raw.startswith(b'%YAML'):
            updated = PPTR.sub(lambda match: b'guid: ' + aliases.get(match[1], match[1]), raw)
            if updated != raw:
                path.write_bytes(updated)
                receipt['outputs'][name] = digest(updated)
    receipt['inputs'][RECEIPT.relative_to(ROOT).as_posix()] = sha(RECEIPT)
    receipt['shared_textures'] = reused


def stage_shared_textures(project):
    """After staging a fixture, include only shared textures it actually references."""
    if not RECEIPT.is_file():
        return
    receipt = json.loads(RECEIPT.read_bytes())
    by_guid = {row['canonical']['guid'].encode(): row['canonical']['path'] for row in receipt['textures']}
    paths = set()
    for base, dirs, files in os.walk(project / 'Assets'):
        for name in files:
            path = Path(base) / name
            if path.suffix not in ('.asset', '.mat', '.prefab', '.unity', '.controller', '.anim', '.meta'):
                continue
            with path.open('rb') as stream:
                head = stream.read(4096)
                if b'\nTexture2D:' in head:
                    continue
                raw = head + stream.read()
            paths.update(by_guid[g] for g in PPTR.findall(raw) if g in by_guid)
    for relative in sorted(paths):
        for suffix in ('', '.meta'):
            source = checked_path(relative + suffix)
            target = project / Path(relative).relative_to('unity')
            if target.exists():
                if sha(target) != sha(source):
                    raise ValueError('Preserve different staged texture: ' + str(target))
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)


def apply(receipt, work):
    previous_receipt = RECEIPT.read_bytes() if RECEIPT.is_file() else None
    if RECEIPT.is_file():
        previous = json.loads(previous_receipt)
        rows = {r['source']['path']: r for r in previous['textures']}
        rows.update({r['source']['path']: r for r in receipt['textures']})
        receipt['textures'] = list(rows.values())
    validate_rows(receipt, allow_missing=True)
    print('Verified source/canonical texture payloads and importers.', flush=True)
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use New-ZjcsValidationWorkspace for mutation backups')
    backup = work / 'texture-originals'
    if backup.exists():
        raise ValueError('Keep the previous transaction until validation completes')
    backup.mkdir()
    if previous_receipt is not None:
        (work / 'texture-previous-receipt.json').write_bytes(previous_receipt)
    patches = list(references(receipt))
    print('Found', len(patches), 'serialized binding files to update.', flush=True)
    changes = {p.relative_to(ROOT).as_posix(): (digest(old), digest(new)) for p, old, new in patches}
    for relative in ('scripts/check_unity_project.py',):
        previous = subprocess.check_output(['git', 'show', 'HEAD:' + relative], cwd=ROOT)
        current = (ROOT / relative).read_bytes()
        if previous != current:
            changes[relative] = digest(previous), digest(current)
    path_map, guid_map = {}, {}
    for row in receipt['textures']:
        src, dst = row['source'], row['canonical']
        path_map[src['path']] = dst['path']
        path_map[src['path'] + '.meta'] = dst['path'] + '.meta'
        guid_map[src['guid']] = dst['guid']
        changes[src['path'] + '.meta'] = (src['meta_sha256'], dst['meta_sha256'])
    # Record the verified transformation separately; never replace original
    # source-input expectations with whatever happens to be on disk.
    manifests = {}
    tracked = set(subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0'))
    pending = set(path_map) | set(changes) | set(guid_map)
    while pending:
        candidates = matching_source_manifests(pending, tracked)
        pending = set()
        for p in candidates:
            if p in manifests:
                continue
            raw = p.read_bytes()
            try:
                value = json.loads(raw)
            except ValueError:
                continue
            base = update_output_records(value, path_map, changes, guid_map)
            updated = update_hash_links(base, changes)
            manifests[p] = (raw, value, base)
            if updated != value:
                relative = p.relative_to(ROOT).as_posix()
                changes[relative] = digest(raw), digest((json.dumps(updated, ensure_ascii=False, indent=2) + '\n').encode())
                pending.add(relative)
    print('Indexed', len(manifests), 'affected source manifests.', flush=True)
    # Source manifests form a dependency graph. Propagate the verified
    # transformation through hash edges, rather than accepting unrelated drift.
    bases = {p: value for p, (raw, original, value) in manifests.items()}
    for iteration in range(len(manifests) + 1):
        moved = False
        for p, (raw, original, value) in list(manifests.items()):
            updated = update_hash_links(bases[p], changes)
            new = (json.dumps(updated, ensure_ascii=False, indent=2) + '\n').encode() if updated != original else raw
            relative = p.relative_to(ROOT).as_posix()
            change = (digest(raw), digest(new))
            if new != raw and changes.get(relative) != change:
                changes[relative] = change
                moved = True
            manifests[p] = raw, original, updated
        if not moved:
            break
    else:
        raise ValueError('Cyclic source manifest hash dependencies')
    for p, (raw, original, value) in manifests.items():
        if value != original:
            patches.append((p, raw, (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()))
    print('Verified source manifest transformation graph;', len(patches), 'total binding/receipt files.', flush=True)
    # Check the whole transaction before changing any shared workspace file.
    for p, old, new in patches:
        if p.read_bytes() != old:
            raise ValueError('Concurrent modification: ' + str(p))
    validate_rows(receipt, allow_missing=True)
    if (RECEIPT.read_bytes() if RECEIPT.is_file() else None) != previous_receipt:
        raise ValueError('Concurrent modification of shared texture recipe')
    journal = dict(changes={p.relative_to(ROOT).as_posix(): dict(before=digest(old), after=digest(new)) for p, old, new in patches},
                   removed=[row['source']['path'] for row in receipt['textures']])
    receipt['bindings'] = journal['changes']
    recipe_bytes = (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode()
    journal['receipt_after'] = digest(recipe_bytes)
    (work / 'texture-transaction.json').write_text(json.dumps(journal, indent=2) + '\n', encoding='utf8')
    for p, old, new in patches:
        target = backup / p.relative_to(ROOT)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(old)
        p.write_bytes(new)
    for row in receipt['textures']:
        for relative in (row['source']['path'], row['source']['path'] + '.meta'):
            p = checked_path(relative)
            if p.exists():
                target = backup / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(p, target)
    RECEIPT.write_bytes(recipe_bytes)
    print('Consolidated', len(receipt['textures']), 'copies,', sum(row['source']['bytes'] for row in receipt['textures']), 'bytes;', len(patches), 'updated bindings/receipts')


def rollback(work):
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use the owned transaction workspace')
    journal = json.loads((work / 'texture-transaction.json').read_bytes())
    if 'receipt_after' in journal and (not RECEIPT.is_file() or sha(RECEIPT) != journal['receipt_after']):
        raise ValueError('Preserve concurrent change to shared texture recipe during rollback')
    backup = work / 'texture-originals'
    restore = []
    for relative, row in journal['changes'].items():
        path = ROOT / relative
        if sha(path) != row['after']:
            raise ValueError('Preserve concurrent change during rollback: ' + relative)
        restore.append((backup / relative, path))
    for relative in journal['removed']:
        for suffix in ('', '.meta'):
            path = ROOT / (relative + suffix)
            source = backup / (relative + suffix)
            if source.exists():
                if path.exists():
                    raise ValueError('Preserve recreated alias: ' + str(path))
                restore.append((source, path))
    for source, path in restore:
        path.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(source, path)
    previous_receipt = work / 'texture-previous-receipt.json'
    if previous_receipt.is_file():
        RECEIPT.write_bytes(previous_receipt.read_bytes())
    elif RECEIPT.exists():
        RECEIPT.unlink()
    print('Restored the texture transaction; concurrent edits were not overwritten.')


def verify(receipt):
    validate_rows(receipt, allow_missing=True)
    for row in receipt['textures']:
        if checked_path(row['source']['path']).exists():
            raise ValueError('Regenerated duplicate; finalize with apply: ' + row['source']['path'])
    for p, old, new in references(receipt):
        raise ValueError('Stale duplicate GUID binding: ' + str(p))
    print('Shared texture payloads, importers, removed paths and serialized GUID bindings verified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('plan', 'apply', 'verify', 'rollback'))
    parser.add_argument('--groups', type=Path, help='Verified Texture2D hash/importer groups from inspection')
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--work', type=Path)
    args = parser.parse_args()
    if args.command == 'plan':
        receipt = plan(json.loads(args.groups.read_bytes()) if args.groups else inspect_groups())
        args.plan.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf8')
        print(len(receipt['textures']), 'copies;', sum(row['source']['bytes'] for row in receipt['textures']), 'bytes')
    elif args.command == 'rollback':
        rollback(args.work)
    else:
        receipt = json.loads((args.plan or RECEIPT).read_bytes())
        if args.command == 'apply':
            apply(receipt, args.work)
        else:
            verify(receipt)
