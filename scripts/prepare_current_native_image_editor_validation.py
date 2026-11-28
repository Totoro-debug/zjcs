"""Run production native-image Editor validators over isolated installed assets."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_current_hud_images_validation import prepare as prepare_hud


def prepare(work):
    project = prepare_hud(work)
    for name in ('assets/reference/npc-icons-source-manifest.json',
                 'assets/reference/battle-hud/current-images-source-manifest.json'):
        source = ROOT/name
        proof = json.loads(source.read_bytes())
        target = work/name
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,target)
        for row in proof['outputs']:
            source = ROOT/row['path']
            target = project/source.relative_to(ROOT/'unity')
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(source,target)
            meta = Path(str(source)+'.meta')
            if not source.name.endswith('.meta') and meta.is_file():
                shutil.copyfile(meta,Path(str(target)+'.meta'))
    editor = project/'Assets/Editor'
    for name in ('OfficialCurrentNativeImageValidation','OfficialBattleHudSpriteTools','OfficialBattleNpcPortraitTools'):
        source = ROOT/'unity/Assets/Game/Editor/Combat'/(name+'.cs')
        shutil.copyfile(source,editor/source.name)
        shutil.copyfile(Path(str(source)+'.meta'),editor/(source.name+'.meta'))
    shutil.copyfile(ROOT/'scripts/fixtures/current_native_image_editor_validation.cs',editor/'CurrentNativeImageEditorValidation.cs')
    print('Prepared production native-image Editor validators:',project,flush=True)
    return project


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    prepare(parser.parse_args().work.resolve())
