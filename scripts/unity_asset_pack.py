"""Create, verify, and restore snapshots of Unity's local official payload."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import stat
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
UNITY_ROOT = ROOT / 'unity'
PAYLOAD = PurePosixPath('Assets/Official')
MANIFEST = '_unity-official-manifest.json'
CHUNK = 1024 * 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        while block := stream.read(CHUNK):
            digest.update(block)
    return digest.hexdigest()


def safe_name(name: str) -> PurePosixPath:
    path = PurePosixPath(name)
    if (path.as_posix() != name or path.is_absolute() or '\\' in name or ':' in name
            or any(part in {'', '.', '..'} or part.endswith(('.', ' ')) for part in name.split('/'))
            or not path.parts or path.parts[0] != PAYLOAD.parts[0]
            or len(path.parts) < 3 or path.parts[:2] != PAYLOAD.parts
            or path.suffix.lower() in {'.cs', '.asmdef', '.py', '.ps1', '.log'}):
        raise ValueError('Invalid Unity official payload path: ' + name)
    return path


def target_at(unity_root: Path, name: str) -> Path:
    relative = Path(*safe_name(name).parts)
    target = unity_root / relative
    if target.is_symlink() or not target.resolve().is_relative_to(unity_root.resolve()):
        raise ValueError('Unity asset path escapes project: ' + name)
    for parent in target.parents:
        if parent == unity_root:
            break
        if parent.is_symlink() or (hasattr(parent, 'is_junction') and parent.is_junction()):
            raise ValueError('Unity asset path uses a link or junction: ' + name)
    return target


def payload_files(unity_root: Path = UNITY_ROOT) -> dict[str, Path]:
    directory = unity_root / Path(*PAYLOAD.parts)
    if not directory.exists():
        return {}
    if not directory.is_dir():
        raise ValueError('Unity official payload root is not a directory: ' + str(directory))
    files = {}
    for file in sorted(directory.rglob('*')):
        if not file.is_file():
            continue
        relative = file.relative_to(unity_root).as_posix()
        safe_name(relative)
        if file.is_symlink() or not file.resolve().is_relative_to(directory.resolve()):
            raise ValueError('Unity official payload path escapes its root: ' + relative)
        files[relative] = file
    if files and not any(Path(name).name.lower() == 'source-manifest.json' for name in files):
        raise ValueError('Unity official payload must include a source-manifest.json')
    return files


def encode(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unity_version(unity_root: Path) -> str:
    version_file = unity_root / 'ProjectSettings/ProjectVersion.txt'
    try:
        return next(line.strip() for line in version_file.read_text(encoding='utf8').splitlines()
                    if line.startswith('m_EditorVersion:'))
    except (OSError, StopIteration) as error:
        raise ValueError('Unity ProjectVersion.txt is missing a pinned editor version') from error


def snapshot(unity_root: Path, output_dir: Path) -> dict:
    files = payload_files(unity_root)
    if not files:
        raise ValueError('No Unity official payload files found')
    output_dir.mkdir(parents=True, exist_ok=True)
    entries = {name: {'bytes': path.stat().st_size, 'sha256': sha256(path)}
               for name, path in files.items()}
    manifest = encode({'format': 1, 'unity_version': unity_version(unity_root),
                       'files': entries})
    handle, temp_name = tempfile.mkstemp(prefix='.unity-assets-', suffix='.zip.tmp', dir=output_dir)
    os.close(handle)
    temporary = Path(temp_name)
    try:
        with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
            _write(archive, MANIFEST, manifest)
            for name, path in files.items():
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                with path.open('rb') as source, archive.open(info, 'w', force_zip64=True) as target:
                    while block := source.read(CHUNK):
                        target.write(block)
                if sha256(path) != entries[name]['sha256']:
                    raise ValueError('Unity official payload changed during snapshot: ' + name)
        archive_hash = sha256(temporary)
        destination = output_dir / f'unity-official-{archive_hash[:16]}.zip'
        if destination.exists() and sha256(destination) != archive_hash:
            raise ValueError('A different archive already uses this filename')
        temporary.replace(destination)
        return {'archive': str(destination), 'archive_sha256': archive_hash,
                'files': len(entries), 'uncompressed_bytes': sum(x['bytes'] for x in entries.values())}
    finally:
        temporary.unlink(missing_ok=True)


def _write(archive: zipfile.ZipFile, name: str, data: bytes) -> None:
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o100644 << 16
    archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=1)


def read_archive(archive_path: Path) -> tuple[dict, dict[str, zipfile.ZipInfo]]:
    with zipfile.ZipFile(archive_path) as archive:
        infos = archive.infolist()
        names = [info.filename for info in infos]
        if (len(names) != len(set(names)) or len(names) != len({name.casefold() for name in names})
                or names.count(MANIFEST) != 1):
            raise ValueError('Unity asset archive has duplicate entries or no unique manifest')
        try:
            manifest = json.loads(archive.read(MANIFEST))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise ValueError('Unity asset archive manifest is invalid') from error
        if manifest.get('format') != 1 or not isinstance(manifest.get('files'), dict):
            raise ValueError('Unsupported Unity asset archive manifest')
        records = manifest['files']
        expected = set(records) | {MANIFEST}
        if set(names) != expected:
            raise ValueError('Archive entries differ from its manifest')
        indexed = {}
        for info in infos:
            if info.filename == MANIFEST:
                continue
            safe_name(info.filename)
            if info.is_dir() or info.file_size < 0 or stat.S_ISLNK(info.external_attr >> 16):
                raise ValueError('Invalid Unity asset archive entry: ' + info.filename)
            record = records[info.filename]
            if (not isinstance(record, dict) or record.get('bytes') != info.file_size
                    or not isinstance(record.get('sha256'), str) or len(record['sha256']) != 64):
                raise ValueError('Invalid Unity asset archive record: ' + info.filename)
            indexed[info.filename] = info
        for name, info in indexed.items():
            digest = hashlib.sha256()
            with archive.open(info) as stream:
                while block := stream.read(CHUNK):
                    digest.update(block)
            if digest.hexdigest() != records[name]['sha256']:
                raise ValueError('Unity asset archive SHA256 mismatch: ' + name)
        return manifest, indexed


def verify(archive_path: Path, unity_root: Path = UNITY_ROOT) -> dict:
    manifest, entries = read_archive(archive_path)
    if manifest.get('unity_version') != unity_version(unity_root):
        raise ValueError('Unity asset archive editor version differs from ProjectVersion.txt')
    local = payload_files(unity_root)
    errors = []
    for name, record in manifest['files'].items():
        path = local.get(name)
        if path is None:
            errors.append('Missing: ' + name)
        elif path.stat().st_size != record['bytes'] or sha256(path) != record['sha256']:
            errors.append('Changed: ' + name)
    errors.extend('Extra: ' + name for name in sorted(set(local) - set(manifest['files'])))
    return {'files': len(entries), 'errors': errors}


def restore(archive_path: Path, unity_root: Path = UNITY_ROOT, replace: bool = False) -> dict:
    manifest, entries = read_archive(archive_path)
    if manifest.get('unity_version') != unity_version(unity_root):
        raise ValueError('Unity asset archive editor version differs from ProjectVersion.txt')
    local = payload_files(unity_root)
    changed = [name for name in entries if name in local
               and (local[name].stat().st_size != manifest['files'][name]['bytes']
                    or sha256(local[name]) != manifest['files'][name]['sha256'])]
    if changed and not replace:
        raise ValueError('Existing Unity assets differ; review before using --replace: '
                         + ', '.join(changed[:10]))
    pending = [name for name in entries if name not in local or name in changed]
    if not pending:
        return {'restored': 0, 'unchanged': len(entries)}
    (unity_root / Path(*PAYLOAD.parts)).mkdir(parents=True, exist_ok=True)
    staging_root = unity_root / 'Temp' / 'unity-official-restore'
    staging_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='stage-', dir=staging_root) as stage_name:
        stage = Path(stage_name)
        with zipfile.ZipFile(archive_path) as archive:
            for name in pending:
                output = stage / Path(*PurePosixPath(name).parts)
                output.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(entries[name]) as source, output.open('wb') as target:
                    while block := source.read(CHUNK):
                        target.write(block)
        for name in pending:
            target = target_at(unity_root, name)
            target.parent.mkdir(parents=True, exist_ok=True)
            (stage / Path(*PurePosixPath(name).parts)).replace(target)
    return {'restored': len(pending), 'unchanged': len(entries) - len(pending)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    create = commands.add_parser('create')
    create.add_argument('--unity-root', type=Path, default=UNITY_ROOT)
    create.add_argument('--output-dir', type=Path, default=ROOT / '.tools/asset-packs')
    check = commands.add_parser('verify')
    check.add_argument('archive', type=Path)
    check.add_argument('--unity-root', type=Path, default=UNITY_ROOT)
    install = commands.add_parser('restore')
    install.add_argument('archive', type=Path)
    install.add_argument('--unity-root', type=Path, default=UNITY_ROOT)
    install.add_argument('--replace', action='store_true')
    args = parser.parse_args()
    try:
        if args.command == 'create':
            result = snapshot(args.unity_root, args.output_dir)
        elif args.command == 'verify':
            result = verify(args.archive, args.unity_root)
        else:
            result = restore(args.archive, args.unity_root, args.replace)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1 if result.get('errors') else 0
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        parser.error(str(error))
    return 2


if __name__ == '__main__':
    raise SystemExit(main())
