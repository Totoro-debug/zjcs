"""Copy only changed build files; remove only obsolete files owned by our receipt."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def owned(name):
    parts = Path(name).parts
    return bool(parts) and (parts[0] in {
        'Zjcs_Data', 'MonoBleedingEdge', 'Zjcs.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe'
    }) and not Path(name).is_absolute() and '..' not in parts


def publish(source, destination):
    source, destination = source.resolve(), destination.resolve()
    if source == destination or source in destination.parents or destination in source.parents:
        raise ValueError('Build and delivery directories must be separate')
    if not (source / 'Zjcs.exe').is_file() or not (source / 'Zjcs_Data').is_dir():
        raise ValueError('A complete Windows Player is required')
    # Refuse links before examining or replacing any delivery files.
    for root in (source, destination):
        if root.exists():
            for folder, dirs, files in os.walk(root, followlinks=False):
                for path in [Path(folder), *(Path(folder)/n for n in dirs+files)]:
                    if path.is_symlink() or (hasattr(path, 'is_junction') and path.is_junction()):
                        raise ValueError(f'Linked delivery path: {path}')
    destination.mkdir(parents=True, exist_ok=True)
    receipt = destination / 'release-files.json'
    previous = json.loads(receipt.read_text(encoding='utf-8')) if receipt.exists() else {}
    if any(not owned(name) for name in previous):
        raise ValueError('Invalid release ownership receipt')
    current = {}
    copied = 0
    for file in source.rglob('*'):
        if not file.is_file():
            continue
        name = file.relative_to(source).as_posix()
        if not owned(name):
            continue
        if not name.isascii():
            raise ValueError(f'Non-ASCII build path: {name}')
        current[name] = digest(file)
        target = destination / name
        if target.is_file() and target.stat().st_size == file.stat().st_size and digest(target) == current[name]:
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + '.release-tmp')
        try:
            shutil.copyfile(file, temporary)
            if digest(temporary) != current[name]:
                raise ValueError(f'Copy changed bytes: {name}')
            os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)
        copied += 1
    for name in previous.keys() - current.keys():
        (destination / name).unlink(missing_ok=True)
    temporary_receipt = receipt.with_suffix('.json.tmp')
    temporary_receipt.write_text(json.dumps(current, indent=2) + '\n', encoding='utf-8')
    os.replace(temporary_receipt, receipt)
    print(f'Published: {copied} changed, {len(current)-copied} unchanged, {len(previous.keys()-current.keys())} obsolete files')
    return copied


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    arguments = parser.parse_args()
    publish(arguments.source, arguments.destination)
