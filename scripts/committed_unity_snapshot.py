"""Export committed Unity source without a checkout, worktree or shared Library."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import tarfile


INPUTS = ('unity/Assets', 'unity/Packages', 'unity/ProjectSettings', 'assets/reference')


def git(repository, *arguments):
    return subprocess.check_output(['git', '-C', str(repository), *arguments])


def checked_path(root, name, verified=None):
    path = PurePosixPath(name)
    if path.is_absolute() or '..' in path.parts or '\\' in name or ':' in name:
        raise ValueError('Invalid snapshot path: ' + name)
    target = root / name
    # A verified parent already covers its ancestors. Most inputs share a
    # parent, so avoid constructing the entire chain for every file twice.
    pending = []
    candidate = target.parent if target != root else root
    while candidate != root:
        if verified is not None and candidate in verified:
            break
        pending.append(candidate)
        candidate = candidate.parent
    if verified is None or root not in verified:
        pending.append(root)
    for candidate in reversed(pending):
        if candidate.is_symlink() or candidate.is_junction():
            raise ValueError('Linked snapshot path: ' + str(candidate))
        if verified is not None:
            verified.add(candidate)
    # The target itself is rechecked on every operation, including when its
    # parent was verified during the inventory pass.
    if target.is_symlink() or target.is_junction():
        raise ValueError('Linked snapshot path: ' + str(target))
    return target


def blob_hash(path):
    digest = hashlib.sha1(b'blob ' + str(path.stat().st_size).encode() + b'\0')
    with path.open('rb') as stream:
        while data := stream.read(1024 * 1024):
            digest.update(data)
    return digest.hexdigest()


def restore_lfs(repository, path):
    if path.stat().st_size > 1024:
        return
    pointer = path.read_bytes()
    if not pointer.startswith(b'version https://git-lfs.github.com/spec/v1\n'):
        return
    fields = dict(line.split(b' ', 1) for line in pointer.splitlines())
    temporary = path.with_name(path.name + '.snapshot-tmp')
    try:
        with temporary.open('wb') as destination:
            subprocess.run(['git', '-C', str(repository), 'lfs', 'smudge'], input=pointer,
                           stdout=destination, check=True)
        with temporary.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if temporary.stat().st_size != int(fields[b'size']) or 'sha256:' + digest != fields[b'oid'].decode():
            raise ValueError('LFS payload differs from committed pointer: ' + str(path))
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def snapshot(repository, workspace, commit='HEAD'):
    repository, workspace = Path(repository).resolve(), Path(workspace).absolute()
    allowed = repository / 'output/validation'
    if (workspace.parent != allowed or workspace.is_symlink() or workspace.is_junction()
            or allowed.is_symlink() or allowed.is_junction()
            or allowed.parent.is_symlink() or allowed.parent.is_junction()):
        raise ValueError('An independent validation session is required')
    marker = json.loads((workspace / '.zjcs-validation.json').read_text('utf-8-sig'))
    if marker['format'] != 1 or Path(marker['repository']).resolve() != repository:
        raise ValueError('Snapshot workspace belongs to another repository')
    target_commit = git(repository, 'rev-parse', '--verify', commit + '^{commit}').decode().strip()
    files = {}
    for record in git(repository, 'ls-tree', '-rz', target_commit, '--', *INPUTS).split(b'\0'):
        if not record:
            continue
        header, name = record.split(b'\t', 1)
        mode, kind, oid = header.decode().split()
        if mode not in ('100644', '100755') or kind != 'blob':
            raise ValueError('Snapshot requires regular source files: ' + name.decode())
        files[name.decode('utf8')] = oid
    if not any(name.startswith('unity/Assets/') for name in files):
        raise ValueError('Commit has no Unity source')
    root = checked_path(workspace, 'source')
    receipt = workspace / 'committed-source-snapshot.json'
    previous = json.loads(receipt.read_text('utf8')) if receipt.exists() else None
    if previous and (previous['format'] != 1 or previous['repository'] != str(repository)):
        raise ValueError('Snapshot identity changed')
    if (root / '.git').exists():
        raise ValueError('A source snapshot cannot contain a checkout or worktree')
    if not previous and root.exists() and any(root.iterdir()):
        raise ValueError('Refusing to replace an unowned source directory')
    old_files = previous['files'] if previous else {}
    verified = set()
    # Inspect only build inputs; Library, Temp and isolated saves stay independent.
    for relative in INPUTS:
        directory = checked_path(root, relative, verified)
        for folder, directories, names in os.walk(directory, followlinks=False):
            relative_folder = Path(folder).relative_to(root).as_posix()
            for name in directories:
                checked_path(root, relative_folder + '/' + name, verified)
            for name in names:
                relative_name = relative_folder + '/' + name
                path = checked_path(root, relative_name, verified)
                if relative_name not in old_files and relative_name not in files:
                    raise ValueError('Unexpected snapshot input: ' + str(path))
    print('Committed source inventory verified; comparing cached file bytes.', flush=True)
    changed = [] if previous else list(files)
    for name, oid in (files.items() if previous else ()):
        path = checked_path(root, name, verified)
        old = old_files.get(name)
        stat = path.stat() if path.is_file() else None
        if old and old['oid'] == oid and stat and stat.st_size == old['size']:
            if stat.st_mtime_ns == old['mtime_ns'] or blob_hash(path) == oid:
                continue
        changed.append(name)
    root.mkdir(exist_ok=True)
    if not previous:
        # Own partial exports too, so an interrupted first export can be resumed.
        receipt.write_text(json.dumps(dict(format=1, repository=str(repository), commit=target_commit,
            files={name: dict(oid=oid, size=-1, mtime_ns=-1) for name, oid in files.items()})), 'utf8')
        # git archive emits exact repository bytes and never consults dirty files.
        roots = [prefix for prefix in INPUTS if any(name.startswith(prefix + '/') for name in files)]
        process = subprocess.Popen(['git', '-c', 'core.autocrlf=false', '-c', 'core.eol=lf',
                                    '-C', str(repository), 'archive', '--format=tar', target_commit,
                                    '--', *roots], stdout=subprocess.PIPE)
        try:
            with tarfile.open(fileobj=process.stdout, mode='r|') as archive:
                for member in archive:
                    path = checked_path(root, member.name.rstrip('/'), verified)
                    if member.isdir():
                        path.mkdir(parents=True, exist_ok=True)
                    elif member.isfile() and member.name in files:
                        path.parent.mkdir(parents=True, exist_ok=True)
                        with archive.extractfile(member) as source, path.open('wb') as destination:
                            digest = hashlib.sha1(b'blob ' + str(member.size).encode() + b'\0')
                            while data := source.read(1024 * 1024):
                                destination.write(data)
                                digest.update(data)
                        # Explicit eol/export-subst attributes can affect archives.
                        # Recover the blob itself if an archive filter changed it.
                        if digest.hexdigest() != files[member.name]:
                            path.write_bytes(git(repository, 'cat-file', 'blob', files[member.name]))
                        restore_lfs(repository, path)
                    else:
                        raise ValueError('Unexpected archived input: ' + member.name)
            if process.wait() != 0:
                raise RuntimeError('Committed source export failed')
        finally:
            process.stdout.close()
            if process.poll() is None:
                process.terminate()
                process.wait()
    elif changed:
        process = subprocess.Popen(['git', '-C', str(repository), 'cat-file', '--batch'],
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        try:
            for name in changed:
                oid = files[name]
                process.stdin.write((oid + '\n').encode())
                process.stdin.flush()
                actual_oid, kind, size = process.stdout.readline().split()
                if actual_oid.decode() != oid or kind != b'blob':
                    raise ValueError('Invalid committed blob: ' + name)
                path = checked_path(root, name, verified)
                path.parent.mkdir(parents=True, exist_ok=True)
                remaining = int(size)
                with path.open('wb') as destination:
                    while remaining:
                        data = process.stdout.read(min(remaining, 1024 * 1024))
                        if not data:
                            raise EOFError('Incomplete committed source: ' + name)
                        destination.write(data)
                        remaining -= len(data)
                if process.stdout.read(1) != b'\n':
                    raise ValueError('Invalid committed blob separator')
                restore_lfs(repository, path)
            process.stdin.close()
            if process.wait() != 0:
                raise RuntimeError('Committed source update failed')
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
            process.stdin.close()
            process.stdout.close()
    for name in old_files.keys() - files.keys():
        checked_path(root, name, verified).unlink(missing_ok=True)
    records = {}
    for name, oid in files.items():
        stat = (root / name).stat()
        records[name] = dict(oid=oid, size=stat.st_size, mtime_ns=stat.st_mtime_ns)
    temporary = receipt.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(dict(format=1, repository=str(repository), commit=target_commit,
                                        files=records), separators=(',', ':')) + '\n', 'utf8')
    os.replace(temporary, receipt)
    print(f'Committed Unity source: {target_commit}; {len(changed)} changed, {len(files)} files; no worktree', flush=True)
    return root


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository', type=Path, required=True)
    parser.add_argument('--workspace', type=Path, required=True)
    parser.add_argument('--commit', default='HEAD')
    arguments = parser.parse_args()
    snapshot(arguments.repository, arguments.workspace, arguments.commit)
