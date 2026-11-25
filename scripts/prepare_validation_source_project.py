"""Copy the current Unity source into a new, independent validation project.

This is a working-directory copy, not a committed release snapshot. Stop Unity
and writers before preparing it; a changing input causes preparation to fail.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from validation_workspace import validation_root


REPOSITORY = Path(__file__).resolve().parents[1]
INPUTS = ('Assets', 'Packages', 'ProjectSettings')


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.digest()


def files(root):
    result = []
    for name in INPUTS:
        base = root / name
        if not base.is_dir() or base.is_symlink() or base.is_junction():
            raise ValueError('Missing or linked input: ' + str(base))
        for path in base.rglob('*'):
            if path.is_symlink() or path.is_junction():
                raise ValueError('Linked input: ' + str(path))
            if path.is_file():
                result.append(path.relative_to(root))
    return sorted(result)


def prepare(workspace, source=None, repository=REPOSITORY):
    repository = Path(repository).resolve()
    workspace = Path(workspace).absolute()
    root = validation_root()
    if workspace.parent != root or workspace.is_symlink() or workspace.is_junction():
        raise ValueError('Use a direct, unlinked validation workspace')
    marker = json.loads((workspace / '.zjcs-validation.json').read_text('utf-8-sig'))
    if marker.get('format') != 1 or Path(marker.get('repository', '')).resolve() != repository:
        raise ValueError('Validation workspace belongs to another repository')
    source = Path(source or repository / 'unity').resolve()
    project = workspace / 'source-unity'
    if project.exists() or project.is_symlink() or project.is_junction():
        raise FileExistsError('Refusing to overwrite an existing project: ' + str(project))
    inputs = files(source)
    if Path('ProjectSettings/ProjectVersion.txt') not in inputs:
        raise ValueError('Source is not a Unity project')
    project.mkdir()
    for name in INPUTS:
        shutil.copytree(source / name, project / name)
    if files(source) != inputs or files(project) != inputs:
        raise RuntimeError('Source file list changed during preparation')
    for relative in inputs:
        if digest(source / relative) != digest(project / relative):
            raise RuntimeError('Source changed during preparation: ' + str(relative))
    return project


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', required=True, type=Path)
    args = parser.parse_args()
    print(prepare(args.workspace))


if __name__ == '__main__':
    main()
