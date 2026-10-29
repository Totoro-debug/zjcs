"""Create, verify and restore local asset packs without adding payloads to Git."""
import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
INVENTORY = '_asset-pack-manifest.json'
CHUNK = 1024 * 1024
WINDOWS_RESERVED = {'CON', 'PRN', 'AUX', 'NUL',
                    *(prefix + str(n) for prefix in ['COM', 'LPT'] for n in range(1, 10))}


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


@contextmanager
def binding_snapshot(root):
    handles = []
    kernel = None
    try:
        if os.name == 'nt':
            import ctypes
            from ctypes import wintypes
            kernel = ctypes.WinDLL('kernel32', use_last_error=True)
            kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                          wintypes.LPVOID, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
            kernel.CreateFileW.restype = wintypes.HANDLE
            kernel.CloseHandle.argtypes = [wintypes.HANDLE]
            kernel.CloseHandle.restype = wintypes.BOOL
            for name in layout_at(root)['code_files']:
                # Permit concurrent readers, reject writes and atomic replacement.
                handle = kernel.CreateFileW(str((root / name).resolve()), 0x80000000,
                                            1, None, 3, 0x80, None)
                if handle == ctypes.c_void_p(-1).value:
                    raise ctypes.WinError(ctypes.get_last_error())
                handles.append(handle)
        yield
    finally:
        for handle in reversed(handles):
            kernel.CloseHandle(handle)


@contextmanager
def workspace_mutex(root):
    git_dir = Path(subprocess.check_output(
        ['git', 'rev-parse', '--absolute-git-dir'], cwd=root, text=True).strip())
    lock = git_dir / 'zjcs-restoration-workspace.lock'
    try:
        with lock.open('x', encoding='utf8') as stream:
            stream.write('asset-pack')
    except FileExistsError:
        raise ValueError('Workspace is locked; retry after the current owner finishes') from None
    try:
        with binding_snapshot(root):
            yield
    finally:
        if lock.exists() and lock.read_text(encoding='utf8').strip() == 'asset-pack':
            lock.unlink()


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf8')


def atomic_write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, name = tempfile.mkstemp(dir=path.parent, prefix=path.name + '.', suffix='.tmp')
    temporary = Path(name)
    try:
        with os.fdopen(handle, 'wb') as stream:
            stream.write(content)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def layout_at(root):
    return json.loads((root / 'asset-pack/layout.json').read_text(encoding='utf8'))


def is_payload(name, layout):
    return (any(name.startswith(prefix + '/') for prefix in layout['payload_roots'])
            and name not in layout['excluded_paths']
            and PurePosixPath(name).suffix not in layout['excluded_suffixes'])


def is_acceptance_artifact(name):
    if not name.startswith('assets/reference/'):
        return False
    path = PurePosixPath(name)
    filename = path.name.lower()
    return (any(word in filename for word in ['validation', 'verification', 'acceptance', 'handoff'])
            or filename in {'ui-audit.json', 'ui-source-audit.json', 'layout-cleanup.json',
                            'screenshot-index.json'}
            or path.suffix.lower() == '.log'
            or (path.suffix.lower() == '.md'
                and name not in {'assets/reference/SOURCES.md', 'assets/reference/LEGACY_SOURCES.md'}))


def target_at(root, name, layout, checked_parents=None):
    """Reject Windows paths, traversal, symlinks and entries outside the payload."""
    parts = PurePosixPath(name).parts
    if is_acceptance_artifact(name):
        raise ValueError('Standalone acceptance reports are forbidden in asset packs: ' + name)
    if (not parts or '\\' in name or ':' in name or name.startswith('/')
            or any(part in {'.', '..'} or part.endswith(('.', ' ')) for part in name.split('/'))
            or any(part.split('.')[0].upper() in WINDOWS_RESERVED for part in parts)
            or any(ord(char) < 32 or char in '<>"|?*' for char in name)
            or PurePosixPath(name).as_posix() != name or not is_payload(name, layout)
            or PurePosixPath(name).suffix in {'.py', '.ps1'}):
        raise ValueError('Invalid asset pack path: ' + name)
    target = root / name
    if target.is_symlink() or (hasattr(target, 'is_junction') and target.is_junction()):
        raise ValueError('Asset path uses a link or junction: ' + name)
    # Cache directory checks within a read-only pass, rather than resolving the
    # same parents tens of thousands of times. Installation rechecks each path.
    if checked_parents is not None and target.parent in checked_parents:
        return target
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Asset path escapes project: ' + name)
    for parent in target.parents:
        if parent == root:
            break
        if parent.is_symlink() or (hasattr(parent, 'is_junction') and parent.is_junction()):
            raise ValueError('Asset path uses a link or junction: ' + name)
        if parent.exists() and not parent.is_dir():
            raise ValueError('Asset parent is not a directory: ' + name)
    if checked_parents is not None:
        checked_parents.add(target.parent)
    return target


def payload_files(root, layout):
    result = {}
    checked_parents = set()
    for folder in layout['payload_roots']:
        directory = root / folder
        if not directory.is_dir():
            raise ValueError('Missing asset directory: ' + folder)
        for path in sorted(directory.rglob('*')):
            if not path.is_file():
                continue
            name = path.relative_to(root).as_posix()
            if not is_payload(name, layout):
                continue
            target_at(root, name, layout, checked_parents)
            result[name] = path
    return result


def code_hashes(root, layout):
    return {name: digest(root / name) for name in layout['code_files']}


def create(root, output_dir):
    with workspace_mutex(root):
        return _create(root, output_dir)


def _create(root, output_dir):
    layout = layout_at(root)
    paths = payload_files(root, layout)
    if not paths:
        raise ValueError('No asset payload found')
    output_dir = output_dir.resolve()
    for folder in layout['payload_roots']:
        if output_dir.is_relative_to((root / folder).resolve()):
            raise ValueError('Pack output must be outside the payload directories')
    output_dir.mkdir(parents=True, exist_ok=True)
    code = code_hashes(root, layout)
    snapshot = {name: (p.stat().st_size, p.stat().st_mtime_ns) for name, p in paths.items()}
    records = {}
    handle, name = tempfile.mkstemp(dir=output_dir, suffix='.zip.tmp')
    os.close(handle)
    temporary = Path(name)
    try:
        with temporary.open('w+b', buffering=8 * CHUNK) as output, zipfile.ZipFile(
                output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
            for index, (name, path) in enumerate(paths.items(), 1):
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = (zipfile.ZIP_STORED if path.suffix in {'.png', '.ogg', '.wav', '.ttf'}
                                      else zipfile.ZIP_DEFLATED)
                info._compresslevel = 1
                hashed = hashlib.sha256()
                with path.open('rb') as source, archive.open(info, 'w', force_zip64=True) as target:
                    while block := source.read(CHUNK):
                        hashed.update(block)
                        target.write(block)
                records[name] = {'bytes': snapshot[name][0], 'sha256': hashed.hexdigest()}
                if index % 10000 == 0:
                    print(f'Packed {index}/{len(paths)} files', flush=True)
            current = payload_files(root, layout)
            if (set(current) != set(paths) or code_hashes(root, layout) != code
                    or any((p.stat().st_size, p.stat().st_mtime_ns) != snapshot[n]
                           for n, p in current.items())):
                raise ValueError('Assets or generated code changed while packing; retry after conversion stops')
            manifest = encode({'format': 1, 'files': records})
            info = zipfile.ZipInfo(INVENTORY, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info._compresslevel = 1
            archive.writestr(info, manifest)
        inventory_hash = hashlib.sha256(manifest).hexdigest()
        archive_hash = digest(temporary)
        filename = 'zjcs-assets-' + layout['source_manifests'][0] + '-' + archive_hash[:16] + '.zip'
        lock = {
            'format': 1,
            'archive_file': filename,
            'archive_sha256': archive_hash,
            'archive_bytes': temporary.stat().st_size,
            'inventory_sha256': inventory_hash,
            'files': len(records),
            'uncompressed_bytes': sum(record['bytes'] for record in records.values()),
            'source_manifests': layout['source_manifests'],
            'layout_sha256': digest(root / 'asset-pack/layout.json'),
            'code_sha256': code,
        }
        destination = output_dir / filename
        if destination.exists() and digest(destination) != lock['archive_sha256']:
            raise ValueError('A different archive already uses this filename: ' + str(destination))
        temporary.replace(destination)
        atomic_write(output_dir / (filename + '.manifest.json'), manifest)
        atomic_write(root / 'asset-pack/lock.json', encode(lock))
        return {'archive': str(destination), **lock}
    finally:
        temporary.unlink(missing_ok=True)


def locked_manifest(root, archive_path=None):
    layout = layout_at(root)
    lock = json.loads((root / 'asset-pack/lock.json').read_text(encoding='utf8'))
    if (lock['format'] != 1 or lock['layout_sha256'] != digest(root / 'asset-pack/layout.json')
            or lock['code_sha256'] != code_hashes(root, layout)):
        raise ValueError('Asset pack layout or generated code differs from lock.json')
    if archive_path:
        if archive_path.stat().st_size != lock['archive_bytes'] or digest(archive_path) != lock['archive_sha256']:
            raise ValueError('Archive size/SHA256 differs from lock.json')
        with zipfile.ZipFile(archive_path) as archive:
            manifest = archive.read(INVENTORY)
            names = archive.namelist()
    else:
        manifest = (root / '.tools/asset-packs' / (lock['archive_file'] + '.manifest.json')).read_bytes()
        names = None
    if hashlib.sha256(manifest).hexdigest() != lock['inventory_sha256']:
        raise ValueError('Inventory SHA256 differs from lock.json')
    inventory = json.loads(manifest)
    files = inventory['files']
    if (inventory['format'] != 1 or len(files) != lock['files']
            or sum(r['bytes'] for r in files.values()) != lock['uncompressed_bytes']):
        raise ValueError('Inventory format/count/size differs from lock.json')
    folded = set()
    checked_parents = set()
    for name in files:
        target_at(root, name, layout, checked_parents)
        if name.casefold() in folded:
            raise ValueError('Duplicate Windows asset path: ' + name)
        folded.add(name.casefold())
    if names is not None and (len(names) != len(files) + 1 or set(names) != set(files) | {INVENTORY}):
        raise ValueError('Archive entries differ from inventory')
    return layout, lock, files, manifest


def verify(root, archive_path=None):
    layout, lock, files, _ = locked_manifest(root, archive_path)
    errors = []
    checked_parents = set()
    for index, (name, record) in enumerate(files.items(), 1):
        path = target_at(root, name, layout, checked_parents)
        if not path.is_file():
            errors.append('Missing: ' + name)
        elif path.stat().st_size != record['bytes'] or digest(path) != record['sha256']:
            errors.append('Changed: ' + name)
        if index % 10000 == 0:
            print(f'Checked {index}/{len(files)} local files', file=sys.stderr, flush=True)
    extra = set(payload_files(root, layout)) - set(files)
    errors.extend('Extra: ' + name for name in sorted(extra))
    return {'files': lock['files'], 'uncompressed_bytes': lock['uncompressed_bytes'], 'errors': errors}


def restore(root, archive_path, replace=False):
    with workspace_mutex(root):
        return _restore(root, archive_path, replace)


def _restore(root, archive_path, replace=False):
    layout, lock, files, manifest = locked_manifest(root, archive_path)
    conflicts = []
    pending = []
    for name, record in files.items():
        path = target_at(root, name, layout)
        if path.exists() and not path.is_file():
            raise ValueError('Asset destination is not a file: ' + name)
        if path.is_file():
            if path.stat().st_size == record['bytes'] and digest(path) == record['sha256']:
                continue
            if not replace:
                conflicts.append(name)
        pending.append(name)
    if conflicts:
        raise ValueError('Existing assets differ; review before using --replace: ' + ', '.join(conflicts[:10]))
    # A no-op restore already checked the archive hash and every installed file.
    if not pending:
        atomic_write(root / '.tools/asset-packs' / (lock['archive_file'] + '.manifest.json'), manifest)
        return {'restored': 0, 'unchanged': len(files),
                'extra_files': sorted(set(payload_files(root, layout)) - set(files))}
    staging_parent = root / '.tools/asset-packs'
    staging_parent.mkdir(parents=True, exist_ok=True)
    # The archive is fully checked in staging before installing any payload.
    with tempfile.TemporaryDirectory(prefix='restore-', dir=staging_parent) as stage:
        staging = Path(stage)
        with zipfile.ZipFile(archive_path) as archive:
            for name, record in files.items():
                info = archive.getinfo(name)
                if info.file_size != record['bytes']:
                    raise ValueError('Archive entry size differs: ' + name)
                output = staging / name
                output.parent.mkdir(parents=True, exist_ok=True)
                hashed = hashlib.sha256()
                with archive.open(name) as source, output.open('wb') as target:
                    while block := source.read(CHUNK):
                        hashed.update(block)
                        target.write(block)
                if hashed.hexdigest() != record['sha256']:
                    raise ValueError('Archive entry SHA256 differs: ' + name)
        for name in pending:
            path = target_at(root, name, layout)
            path.parent.mkdir(parents=True, exist_ok=True)
            (staging / name).replace(path)
    atomic_write(staging_parent / (lock['archive_file'] + '.manifest.json'), manifest)
    return {'restored': len(pending), 'unchanged': len(files) - len(pending),
            'extra_files': sorted(set(payload_files(root, layout)) - set(files))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    build = commands.add_parser('create', help='Build a pack and update asset-pack/lock.json')
    build.add_argument('--output-dir', type=Path, default=ROOT / '.tools/asset-packs')
    check = commands.add_parser('verify', help='Check local assets against the locked inventory')
    check.add_argument('--archive', type=Path, help='Read inventory from this verified ZIP instead of the local cache')
    install = commands.add_parser('restore', help='Verify and install a pack into its original local paths')
    install.add_argument('archive', type=Path)
    install.add_argument('--replace', action='store_true', help='Replace existing files whose contents differ')
    args = parser.parse_args()
    try:
        if args.command == 'create':
            result = create(ROOT, args.output_dir)
        elif args.command == 'verify':
            result = verify(ROOT, args.archive)
        else:
            result = restore(ROOT, args.archive, args.replace)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1 if result.get('errors') else 0
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        print('Asset pack error:', error)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
