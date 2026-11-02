"""Prepare the native-particle integration scene for status playable components."""
import os
from pathlib import Path
import shutil

from prepare_battle_movement_effect_validation import prepare, ROOT


def build(work):
    project = prepare(work)
    for folder in ('Runtime/Official/StoryPlayableManager', 'Runtime/Official/BattleStatusPlayables',
                   'Editor/BattleStatusPlayables'):
        source = ROOT / 'unity/Assets/Game' / folder
        for path in [Path(str(source) + '.meta'), *source.rglob('*')]:
            if not path.is_file():
                continue
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    return project


if __name__ == '__main__':
    print(build(Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()))
