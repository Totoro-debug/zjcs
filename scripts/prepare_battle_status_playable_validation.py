"""Prepare the native-particle integration scene for status playable components."""
import os
from pathlib import Path
import shutil

from prepare_battle_movement_effect_validation import prepare, ROOT


def build(work):
    project = prepare(work)
    # The shared combat assembly also contains native shader implementations;
    # keep their assets alongside the metadata copied by the base helper.
    for path in (ROOT / 'unity/Assets/Game/Runtime/Official/Combat').rglob('*'):
        if path.is_file() and path.suffix in ('.shader', '.hlsl', '.cginc'):
            for source in (path, Path(str(path) + '.meta')):
                if not source.is_file(): continue
                target = project / source.relative_to(ROOT / 'unity')
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
    for folder in ('Runtime/Official/StoryPlayableManager', 'Runtime/Official/BattleStatusPlayables',
                   'Editor/BattleStatusPlayables'):
        source = ROOT / 'unity/Assets/Game' / folder
        for path in [Path(str(source) + '.meta'), *source.rglob('*')]:
            if not path.is_file():
                continue
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    source = ROOT / 'unity/Assets/Official/Combat/StatusOrbits'
    if source.is_dir():
        paths = [Path(str(source) + '.meta'), *source.rglob('*')]
        data = ROOT / 'unity/Assets/Game/Resources/Combat/status-orbits.json'
        paths += [data, Path(str(data) + '.meta')]
        for shader in (ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders').glob('StatusOrbit_*.shader'):
            paths += [shader, Path(str(shader) + '.meta')]
        for path in paths:
            if not path.is_file(): continue
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    return project


if __name__ == '__main__':
    print(build(Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()))
