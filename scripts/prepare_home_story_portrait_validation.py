"""Stage recovered original portrait prefabs in an owned Unity project."""
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
    receipt = json.loads((work / 'home-story-portrait-native-source-manifest.json').read_bytes())
    staged = work / 'home-story-portrait-assets'
    for path, expected in receipt['outputs'].items():
        if sha((staged / path).read_bytes()) != expected:
            raise ValueError('Staged portrait asset changed: ' + path)
    shutil.copytree(work / 'home-story-portrait-code',
        project / 'Assets/Game/Runtime/Official/HomeStoryPortrait', dirs_exist_ok=True)
    wanted = work / 'home-story-portrait-validation-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(staged / path) + '\n'
        for path in receipt['outputs'] if path.endswith('.meta')), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), str(project / 'Assets'), '-g', '*.meta'],
        capture_output=True, text=True)
    if found.returncode not in (0, 1): raise ValueError(found.stderr)
    existing = {}
    for name in found.stdout.splitlines():
        metadata = Path(name)
        key = guid(metadata)
        if key in existing: raise ValueError('Ambiguous existing Unity asset GUID: ' + key)
        existing[key] = Path(str(metadata)[:-5])
    target_root = project / 'Assets/Official/Home/StoryPortraitValidated'
    for relative in receipt['outputs']:
        if relative.endswith('.meta'): continue
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
    source = (ROOT / 'scripts/fixtures/home_story_portrait_validation.cs').read_text('utf8')
    rows = receipt['sprite_proxy_bindings']
    source = source.replace('__SPRITE_BINDINGS__', '\n'.join(
        '            { "' + key + '", "' + value['guid'] + '" },' for key, value in sorted(rows.items())))
    for number in (8402, 8411):
        source = source.replace('__PREFAB_' + str(number) + '__', receipt['roots']['Npc_Portrait_' + str(number)]['guid'])
    editor = project / 'Assets/Editor/HomeStoryPortraitValidation'
    editor.mkdir(parents=True, exist_ok=True)
    (editor / 'HomeStoryPortraitValidation.cs').write_text(source, 'utf8', newline='\n')
    frame = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split(
        'public sealed partial class HomeIncomeFormulaHostRunner')[0]
    frame = frame[frame.index('public static partial class HomeIncomeFormulaValidation'):]
    frame = 'using System;\nusing System.Reflection;\nusing UnityEngine;\n' + frame.replace(
        'HomeIncomeFormulaValidation', 'HomeStoryPortraitValidation')
    (editor / 'NativeFrame.cs').write_text(frame, 'utf8', newline='\n')
    (editor / 'HomeStoryPortraitValidation.asmdef').write_text(json.dumps(dict(
        name='HomeStoryPortraitValidation', includePlatforms=['Editor'], references=[
            'Launch', 'Zjcs.Official.HomeStoryPortrait', 'Zjcs.Official.HomeHudUI',
            'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.HomeUI', 'Unity.ugui']), indent=2) + '\n', 'utf8', newline='\n')
    print('Staged native story portrait validation:', project, flush=True)


if __name__ == '__main__':
    main()
