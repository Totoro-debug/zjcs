"""Stage original home collider and live home story-mask interaction checks."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_card_native import sha
from validation_workspace import require_temporary_unity_project, validation_directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    work = validation_directory()
    receipt = json.loads((work / 'home-story-collider-source-manifest.json').read_bytes())
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Collider source changed: ' + path)
    target = project / 'Assets/Official/Home/StoryColliderValidation'
    target.mkdir(parents=True, exist_ok=True)
    for path, expected in receipt['outputs'].items():
        source = work / 'home-story-collider-assets' / path
        if sha(source.read_bytes()) != expected: raise ValueError('Collider staging changed: ' + path)
        destination = target / path
        if destination.exists() and destination.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing validation asset: ' + str(destination))
        shutil.copy2(source, destination)
    for relative in ('Assets/Game/Runtime/Offline/HomeStoryDialog/HomeStoryFurnitureColliderAssets.cs',
                     'Assets/Game/Runtime/Offline/HomeStoryDialog/HomeStoryFurnitureColliderAssets.cs.meta',
                     'Assets/Game/Resources/Home/HomeStoryFurnitureColliderAssets.asset',
                     'Assets/Game/Resources/Home/HomeStoryFurnitureColliderAssets.asset.meta'):
        destination = project / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / 'unity' / relative, destination)
    entries = [row for rows in receipt['chain'].values() for row in rows]
    position = [sum(row['position'][index] for row in entries) for index in range(3)]
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf8')
    source = source.replace('HomeFurnitureHostValidation', 'HomeStoryMaskValidation').replace('HomeFurnitureHostRunner', 'HomeStoryMaskRunner')
    source = source.replace('Zjcs.HomeFurnitureHost.Active', 'Zjcs.HomeStoryMask.Active')
    source = source.replace('public static class HomeStoryMaskValidation', 'public static partial class HomeStoryMaskValidation')
    source = source.replace('public sealed class HomeStoryMaskRunner', 'public sealed partial class HomeStoryMaskRunner')
    source = source.replace('EditorApplication.EnterPlaymode();', 'ConfigureNativeFrame();\n        EditorApplication.EnterPlaymode();')
    source = source.replace('AudioListener.volume = 0;', 'Application.runInBackground = true;\n        AudioListener.volume = 0;')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    source = source.replace('        SceneClick(0);', '        yield return CheckFurnitureMask();\n        SceneClick(0);')
    source = source.replace('        Click(repair.RepairBtn);', '        yield return CheckRepairButtonMask(repair);')
    source = source.replace('        var rect = (RectTransform)button.transform;\n        Vector2 point = RectTransformUtility.WorldToScreenPoint(home.Scene.Camera, rect.TransformPoint(rect.rect.center));',
        '        Canvas.ForceUpdateCanvases();\n        var rect = (RectTransform)button.transform;\n'
        '        var camera = button.GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : home.Scene.Camera;\n'
        '        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));')
    capture_start = source.index('    private void Capture(string name)')
    capture_end = source.index('    private Color SkyPixel()', capture_start)
    source = source[:capture_start] + '''    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        home.Scene.Camera.Render();
        Save(name);
    }
''' + source[capture_end:]
    source = source.replace('HOME_FURNITURE_HOST_PASSED: actual home entry, simulated scene and native repair input, four-state binding and saved restoration.',
                            'HOME_STORY_MASK_PASSED: original collider and masks with actual repair button pointer and saved restoration.')
    editor = project / 'Assets/Editor/HomeStoryMaskValidation'
    editor.mkdir(parents=True, exist_ok=True)
    (editor / 'HomeStoryMaskValidation.cs').write_text(source, 'utf8', newline='\n')
    fixture = (ROOT / 'scripts/fixtures/home_story_mask_validation.cs').read_text('utf8')
    fixture = fixture.replace('__COLLIDER_GUID__', receipt['root']['guid']).replace('__COLLIDER_X__', repr(position[0])).replace('__COLLIDER_Y__', repr(position[1]))
    size = receipt['collider']['fields']['m_Size']
    for axis in ('x', 'y', 'z'): fixture = fixture.replace('__COLLIDER_S' + axis.upper() + '__', repr(size[axis]))
    (editor / 'MaskChecks.cs').write_text(fixture, 'utf8', newline='\n')
    frame = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split('public sealed partial class HomeIncomeFormulaHostRunner')[0]
    frame = frame[frame.index('public static partial class HomeIncomeFormulaValidation'):]
    frame = 'using System;\nusing System.Reflection;\nusing UnityEngine;\n' + frame.replace('HomeIncomeFormulaValidation', 'HomeStoryMaskValidation')
    (editor / 'NativeFrame.cs').write_text(frame, 'utf8', newline='\n')
    references = ['Zjcs.Official.HomeConditions', 'Zjcs.Official.Partners', 'Zjcs.Offline.Home',
        'Zjcs.Offline.HomeUI', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Editor',
        'Zjcs.Presentation.Home', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Official.HomeUI',
        'Zjcs.Offline.ProfessionUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeText', 'Zjcs.Offline.PartnersUI',
        'Zjcs.Official.PartnerFriendshipDialogUI', 'Zjcs.Presentation.World', 'Zjcs.Official.StoryMasks',
        'Zjcs.Offline.HomeStoryDialog',
        'Unity.TextMeshPro', 'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Universal.Runtime',
        'UnityEngine.UI', 'spine-unity', 'spine-csharp']
    (editor / 'HomeStoryMaskValidation.asmdef').write_text(json.dumps(dict(name='HomeStoryMaskValidation',
        includePlatforms=['Editor'], references=references), indent=2) + '\n', 'utf8', newline='\n')
    print('Staged actual home original story-mask interaction validation.', flush=True)


if __name__ == '__main__':
    main()
