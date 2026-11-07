"""Validate native grid entity registries with committed runtime dependencies."""
import io
import json
import os
import shutil
import subprocess
import tarfile
from pathlib import Path

from prepare_battle_status_playable_validation import build, ROOT


def prepare(work):
    project = build(work, include_grid_entities=True)
    runtime = project / 'Assets/Game/Runtime'
    assert runtime.resolve().is_relative_to(work.resolve()) and not runtime.is_symlink()
    shutil.rmtree(runtime)
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip()
    archived = subprocess.check_output(['git', 'archive', '--format=tar', head,
        'unity/Assets/Game/Runtime', 'unity/Assets/Spine',
        'unity/Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll',
        'unity/Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll.meta'], cwd=ROOT)
    with tarfile.open(fileobj=io.BytesIO(archived)) as archive:
        for member in archive.getmembers():
            if not member.isfile():
                continue
            target = project / Path(member.name).relative_to('unity')
            assert target.resolve().is_relative_to(project.resolve())
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.extractfile(member).read())
    paths = []
    for folder in ('Assets/Game/Runtime/Official/BattleGridItemEntities',
                   'Assets/Official/Combat/GridEntities',
                   'Assets/Game/Editor/BattleStatusPlayables'):
        source = ROOT / 'unity' / folder
        paths += [Path(str(source) + '.meta'), *source.rglob('*')]
    data = ROOT / 'unity/Assets/Game/Resources/Combat/grid-entities.json'
    paths += [data, Path(str(data) + '.meta')]
    for path in paths:
        if not path.is_file():
            continue
        target = project / path.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    for shader in (ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders').glob('GridEntity_*.shader'):
        for source in (shader, Path(str(shader) + '.meta')):
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    (work / 'grid-entity-validation-input.json').write_text(json.dumps(dict(
        committed_runtime=head, current_module='BattleGridItemEntities',
        resource_receipt='assets/reference/battle-animation/grid-entity-assets-source-manifest.json'),
        indent=2), encoding='utf-8')
    print(project)
    return project


if __name__ == '__main__':
    prepare(Path(os.environ['ZJCS_VALIDATION_DIR']).resolve())
