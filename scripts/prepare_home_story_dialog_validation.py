"""Stage the original complete story dialogue in an owned Unity project."""
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
    receipt = json.loads((work / 'home-story-dialog-native-source-manifest.json').read_bytes())
    staged = work / 'home-story-dialog-assets'
    mask_receipt = json.loads((work / 'home-story-mask-native-source-manifest.json').read_bytes())
    staged_mask = work / 'home-story-mask-assets'
    groups = ((staged, receipt['outputs']), (staged_mask, mask_receipt['outputs']))
    for folder, outputs in groups:
        for path, expected in outputs.items():
            if sha((folder / path).read_bytes()) != expected: raise ValueError('Staged dialogue asset changed: ' + path)
    shutil.copytree(work / 'home-story-dialog-code',
        project / 'Assets/Game/Runtime/Official/HomeStoryDialog', dirs_exist_ok=True)
    wanted = work / 'home-story-dialog-validation-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(folder / path) + '\n'
        for folder, outputs in groups for path in outputs if path.endswith('.meta')), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), str(project / 'Assets'), '-g', '*.meta'],
        capture_output=True, text=True)
    if found.returncode not in (0, 1): raise ValueError(found.stderr)
    existing = {}
    for name in found.stdout.splitlines():
        metadata = Path(name)
        key = guid(metadata)
        if key in existing: raise ValueError('Ambiguous existing Unity asset GUID: ' + key)
        existing[key] = Path(str(metadata)[:-5])
    target_root = project / 'Assets/Official/Home/StoryDialogValidated'
    for folder, outputs in groups:
        for relative in outputs:
            if relative.endswith('.meta'): continue
            source = folder / relative
            key = guid(Path(str(source) + '.meta'))
            target = existing.get(key, target_root / relative)
            if target.exists():
                if target.read_bytes() != source.read_bytes(): raise ValueError('Different existing Unity payload: ' + str(target))
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
                shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
    audio = Path('Assets/Official/Home/FurnitureStoryAudio')
    shutil.copytree(ROOT / 'unity' / audio, project / audio, dirs_exist_ok=True)
    shutil.copy2(ROOT / 'unity' / (str(audio) + '.meta'), project / (str(audio) + '.meta'))
    service_folder = Path('Assets/Game/Runtime/Offline/HomeStoryDialog')
    shutil.copytree(ROOT / 'unity' / service_folder, project / service_folder, dirs_exist_ok=True)
    shutil.copy2(ROOT / 'unity' / (str(service_folder) + '.meta'), project / (str(service_folder) + '.meta'))
    for relative in ('Assets/Game/Runtime/Offline/HomeStory/HomeStoryDialogAssets.cs',
                     'Assets/Game/Runtime/Offline/HomeStory/HomeStoryDialogAssets.cs.meta',
                     'Assets/Game/Resources/Home/HomeStoryDialogAssets.asset',
                     'Assets/Game/Resources/Home/HomeStoryDialogAssets.asset.meta',
                     'Assets/Official/Home/Source.meta',
                     'Assets/Official/Home/Source/furniture-unlock-stories.json',
                     'Assets/Official/Home/Source/furniture-unlock-stories.json.meta'):
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / 'unity' / relative, target)
    stories = json.loads((ROOT / 'unity/Assets/Official/Home/Source/furniture-unlock-stories.json').read_bytes())
    sounds = json.loads((ROOT / 'assets/reference/home-furniture-unlock-story-audio-source-manifest.json').read_bytes())
    game = json.loads((ROOT / 'unity/Assets/Official/Home/UI/CurrencyWindows/home-currency-windows-rules.json').read_bytes())
    payload = dict(dialogs=stories['dialogues']['SenSenHomeStore']['0'], roles=stories['dialogRoles'],
        options=stories['storyOptions'], texts=[dict(key=key, value=value) for key, value in stories['localization'].items()],
        voices=[dict(key=row['eventKey'], path=row['clip']) for row in sounds['entries']],
        playerRoleId=game['tables']['game_settings']['DialogSetting']['PlayerRoleId'])
    (work / 'dialog-test-data.json').write_text(json.dumps(payload, ensure_ascii=False), 'utf8', newline='\n')
    source = (ROOT / 'scripts/fixtures/home_story_dialog_validation.cs').read_text('utf8').replace(
        '__PREFAB_GUID__', receipt['root']['guid'])
    editor = project / 'Assets/Editor/HomeStoryDialogValidation'
    editor.mkdir(parents=True, exist_ok=True)
    (editor / 'HomeStoryDialogValidation.cs').write_text(source, 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/home_story_graph_validation.cs', editor / 'HomeStoryGraphValidation.cs')
    frame = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split(
        'public sealed partial class HomeIncomeFormulaHostRunner')[0]
    frame = frame[frame.index('public static partial class HomeIncomeFormulaValidation'):]
    frame = 'using System;\nusing System.Reflection;\nusing UnityEngine;\n' + frame.replace(
        'HomeIncomeFormulaValidation', 'HomeStoryDialogValidation')
    (editor / 'NativeFrame.cs').write_text(frame, 'utf8', newline='\n')
    (editor / 'HomeStoryDialogValidation.asmdef').write_text(json.dumps(dict(
        name='HomeStoryDialogValidation', includePlatforms=['Editor'], references=[
            'Launch', 'UniTask', 'Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.HomeStoryDialog',
            'Zjcs.Official.HomeStoryOptions', 'Zjcs.Official.HomeStoryCharacter', 'Zjcs.Official.HomeStoryPortrait',
            'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryBubbles', 'Zjcs.Official.HomeHudUI',
            'Zjcs.Official.WorldUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.HomeUI', 'Zjcs.Offline.HomeHudUI',
            'Zjcs.Offline.HomeStory', 'Zjcs.Offline.Story', 'Unity.Newtonsoft.Json',
            'Zjcs.Offline.HomeStoryDialog']), indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original complete dialogue validation:', project, flush=True)


if __name__ == '__main__':
    main()
