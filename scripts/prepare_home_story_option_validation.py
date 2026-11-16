"""Stage the recovered original option subtree in an owned Unity project."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_card_native import guid, sha
from validation_workspace import require_temporary_unity_project, validation_directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    work = validation_directory()
    receipt = json.loads((work / 'home-story-option-native-source-manifest.json').read_bytes())
    staged = work / 'home-story-option-assets'
    for path, expected in receipt['outputs'].items():
        if sha((staged / path).read_bytes()) != expected:
            raise ValueError('Staged option asset changed: ' + path)
    code = work / 'home-story-option-code'
    target_code = project / 'Assets/Game/Runtime/Official/HomeStoryOptions'
    shutil.copytree(code, target_code, dirs_exist_ok=True)
    metas = [path for path in receipt['outputs'] if path.endswith('.meta')]
    wanted = work / 'home-story-option-validation-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(staged / path) + '\n' for path in metas), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), str(project / 'Assets'), '-g', '*.meta'],
        capture_output=True, text=True)
    if found.returncode not in (0, 1):
        raise ValueError(found.stderr)
    existing = {}
    for name in found.stdout.splitlines():
        metadata = Path(name)
        key = guid(metadata)
        if key in existing:
            raise ValueError('Ambiguous existing Unity asset GUID: ' + key)
        existing[key] = Path(str(metadata)[:-5])
    target_root = project / 'Assets/Official/Home/StoryOptionValidated'
    for relative in receipt['outputs']:
        if relative.endswith('.meta'):
            continue
        source = staged / relative
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, target_root / relative)
        if target.exists():
            if target.read_bytes() != source.read_bytes():
                raise ValueError('Different existing Unity payload: ' + str(target))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
    story = json.loads((ROOT / 'unity/Assets/Official/Home/Source/furniture-unlock-stories.json').read_bytes())
    rows = []
    for row in story['storyOptions']:
        key = f"story_{row['StoryName']}_{row['Id']}_option"
        rows.append(dict(storyName=row['StoryName'], id=row['Id'], icon=row['Icon'], blackIcon=row['BlackIcon'],
            conditionType=row['ConditionType'], conditionParams=row['ConditionParams'], text=story['localization'][key]))
    (work / 'option-test-data.json').write_text(json.dumps(dict(options=rows), ensure_ascii=False), 'utf8', newline='\n')
    source = (ROOT / 'scripts/fixtures/home_story_options_validation.cs').read_text('utf8')
    source = source.replace('__PREFAB_GUID__', receipt['root']['guid']).replace(
        '__ICON_GUID__', receipt['dynamic_icons']['UI/Dialogue/Iconarrow_1']['guid'])
    editor = project / 'Assets/Editor/HomeStoryOptionsValidation'
    editor.mkdir(parents=True, exist_ok=True)
    (editor / 'HomeStoryOptionsValidation.cs').write_text(source, 'utf8', newline='\n')
    frame = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split(
        'public sealed partial class HomeIncomeFormulaHostRunner')[0]
    frame = frame[frame.index('public static partial class HomeIncomeFormulaValidation'):]
    frame = 'using System;\nusing System.Reflection;\nusing UnityEngine;\n' + frame.replace(
        'HomeIncomeFormulaValidation', 'HomeStoryOptionsValidation')
    (editor / 'NativeFrame.cs').write_text(frame, 'utf8', newline='\n')
    (editor / 'HomeStoryOptionsValidation.asmdef').write_text(json.dumps(dict(
        name='HomeStoryOptionsValidation', includePlatforms=['Editor'], references=[
            'Zjcs.Official.HomeStoryOptions', 'Zjcs.Official.WorldUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.HomeHudUI', 'Zjcs.Offline.HomeUI',
            'Unity.ugui', 'Unity.TextMeshPro']), indent=2) + '\n', 'utf8', newline='\n')
    print('Staged native option subtree and simulated pointer validation:', project, flush=True)


if __name__ == '__main__':
    main()
