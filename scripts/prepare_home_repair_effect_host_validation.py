"""Add the real-home pointer and repair-effect check to an owned Unity copy."""
import argparse
import json
from pathlib import Path
import shutil

from prepare_partner_card_native import sha
from validation_workspace import require_temporary_unity_project

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    receipt = json.loads((ROOT / 'assets/reference/home-repair-effect-native-source-manifest.json').read_bytes())
    # Validate the complete runtime/tooling closure before copying any inputs.
    for relative, expected in receipt['runtime_bindings'].items():
        source = ROOT / relative
        if not source.is_file():
            raise FileNotFoundError('Home effect validation input is missing: ' + relative)
        if sha(source.read_bytes()) != expected:
            raise ValueError('Home effect validation input changed: ' + relative)
    paths = dict(receipt['installed_output_sha256'])
    for name in ('HomeRepairEffectAssets.cs', 'HomeRepairEffectAssets.cs.meta', 'HomeRepairEffectPlayer.cs',
                 'HomeRepairEffectPlayer.cs.meta', 'HomeSceneView.cs', 'HomeFeatureRuntime.cs'):
        relative = 'unity/Assets/Game/Runtime/Presentation/Home/' + name
        paths[relative] = receipt['runtime_bindings'][relative]
    for relative, expected in paths.items():
        source = ROOT / relative
        if not source.is_file():
            raise FileNotFoundError('Home effect validation input is missing: ' + relative)
        if sha(source.read_bytes()) != expected: raise ValueError('Home effect validation input changed: ' + relative)
        target = project / Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf8')
    source = source.replace('public static class HomeFurnitureHostValidation', 'public static partial class HomeFurnitureHostValidation')
    source = source.replace('EditorApplication.EnterPlaymode();', 'ConfigureNativeFrame();\n        EditorApplication.EnterPlaymode();')
    source = source.replace('AudioListener.volume = 0;', 'Application.runInBackground = true;\n        AudioListener.volume = 0;')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;',
        '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    before = '''        Click(repair.RepairBtn);
        yield return new WaitForSeconds(0.8f);'''
    after = '''        Click(repair.RepairBtn);
        Require(home.Session.OwnsFurniture(0), "Repair transaction committed before effect");
        var effect = home.Scene.Root.transform.Find("FurnitureRepairEffect_0");
        Require(effect && effect.GetComponentsInChildren<ParticleSystem>(true).Length == 5,
            "Original bed effect bound at actual repair");
        Require(!home.Scene.Root.transform.Find("Furniture_0_Repaired").gameObject.activeSelf &&
            home.Scene.FurnitureClickState(0, HomeFurnitureState.Functional) == HomeFurnitureState.CanRepair,
            "Original previous state remains before effect transition");
        Capture("home-repair-effect-active.png");
        yield return new WaitForSeconds(0.35f);
        Require(home.Scene.Root.transform.Find("Furniture_0_Repaired").gameObject.activeSelf &&
            home.Scene.FurnitureClickState(0, HomeFurnitureState.Functional) == HomeFurnitureState.Functional,
            "Actual repaired state becomes visible after effect delay");
        yield return new WaitForSeconds(0.85f);
        Require(!effect, "Actual bed effect is released");'''
    if source.count(before) != 1:
        raise ValueError('Actual home repair fixture changed')
    source = source.replace(before, after).replace(
        'HOME_FURNITURE_HOST_PASSED: actual home entry, simulated scene and native repair input, four-state binding and saved restoration.',
        'HOME_REPAIR_EFFECT_HOST_PASSED: actual bed/cart drag, native repair input, staged effects and saved restoration.')
    scene_click = '        SceneClick(0);'
    if source.count(scene_click) != 1:
        raise ValueError('Actual scene input fixture changed')
    source = source.replace(scene_click, '        PanToFurniture(0);\n' + scene_click)
    before_cart = '''        Capture("home-current-repair-notify.png");
        home.Close();'''
    after_cart = '''        Capture("home-current-repair-notify.png");
        var notify = Object.FindObjectOfType<FurnitureRepairedNotifyDialog>();
        Click(notify.GetComponentsInChildren<Zjcs.Official.ProfessionUI.CloseWindowBtn>()
            .First(value => value.myButton.isActiveAndEnabled).myButton);
        yield return new WaitForSeconds(.6f);
        Require(!home.Session.OwnsFurniture(10) &&
            home.HomeProgression.FurnitureState(home.Session, 10, home.Rules.CurrentContext) == HomeFurnitureState.CanRepair,
            "Actual cart is eligible for repair");
        PanToFurniture(10);
        SceneClick(10);
        yield return new WaitForSeconds(.8f);
        repair = Object.FindObjectOfType<FurnitureRepairDialog>();
        Require(repair, "Scene pointer opens original cart repair dialog");
        Click(repair.RepairBtn);
        Require(home.Session.OwnsFurniture(10), "Actual cart repair commits");
        var cartEffect = home.Scene.Root.transform.Find("FurnitureRepairEffect_10");
        Require(cartEffect && cartEffect.GetComponentsInChildren<ParticleSystem>(true).Length == 5 &&
            !home.Scene.Root.transform.Find("Furniture_10_Repaired").gameObject.activeSelf,
            "Original cart effect begins before its state changes");
        yield return new WaitForSeconds(.1f);
        Capture("home-cart-repair-effect-active.png");
        yield return new WaitForSeconds(.35f);
        Require(home.Scene.Root.transform.Find("Furniture_10_Repaired").gameObject.activeSelf,
            "Actual cart state changes after the original delay");
        yield return new WaitForSeconds(.85f);
        Require(!cartEffect, "Actual cart effect is released");
        home.Close();'''
    if source.count(before_cart) != 1:
        raise ValueError('Actual home repair notification fixture changed')
    source = source.replace(before_cart, after_cart)
    reopened = '        Capture("home-current-reopened.png");'
    if source.count(reopened) != 1:
        raise ValueError('Actual home reopen fixture changed')
    source = source.replace(reopened, '''        Require(home.Session.OwnsFurniture(10) &&
            home.HomeProgression.FurnitureState(home.Session, 10, home.Rules.CurrentContext) == HomeFurnitureState.Functional,
            "Actual home reopen restores repaired cart");
        PanToFurniture(10);
        Capture("home-current-reopened-cart.png");
        PanToFurniture(0);
''' + reopened)
    helper = '''    private void PanToFurniture(int type)
    {
        Require(home.Scene.TryGetFurnitureClickBounds(type, out Rect bounds), "Original drag target");
        var input = typeof(HomeFeatureRuntime).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var down = input.GetType().GetMethod("PointerDown", flags);
        var move = input.GetType().GetMethod("PointerMove", flags);
        var up = input.GetType().GetMethod("PointerUp", flags);
        for (int i = 0; i < 8 && Mathf.Abs(home.Scene.Camera.transform.position.x - bounds.center.x) > 1f; i++)
        {
            Vector2 start = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var pointer = new PointerEventData(EventSystem.current) { position = start };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count == 0, "Drag starts on uncovered scene");
            float pixels = -(bounds.center.x - home.Scene.Camera.transform.position.x) * Screen.height /
                (2f * home.Scene.OrthographicSize * home.Scene.DragSensitivity);
            Vector2 end = start + new Vector2(Mathf.Clamp(pixels, -Screen.width * .35f, Screen.width * .35f), 0);
            down.Invoke(input, new object[] { start, false });
            move.Invoke(input, new object[] { end });
            up.Invoke(input, new object[] { end, false });
        }
        Vector2 point = home.Scene.Camera.WorldToScreenPoint(bounds.center);
        Require(point.x >= 0 && point.x < Screen.width && point.y >= 0 && point.y < Screen.height,
            "Furniture lies within the captured scene after simulated drag");
    }
'''
    marker = '    private void SceneClick(int type)'
    if source.count(marker) != 1:
        raise ValueError('Actual scene input helper changed')
    source = source.replace(marker, helper + marker)
    source = source.replace('    private RenderTexture target;\n', '').replace('        CloseCapture();\n', '')
    start = source.index('    private void Open()')
    end = source.index('    private void PanToFurniture', start)
    source = source[:start] + '''    private void Open()
    {
        home = HomeFeatureRuntime.Begin(slot, root, null);
        home.TutorialAutoStartEnabled = false;
        if (!Object.FindObjectOfType<AudioListener>()) home.Scene.Camera.gameObject.AddComponent<AudioListener>();
    }
''' + source[end:]
    start = source.index('    private void Capture(string name)')
    end = source.index('    private static void Require(', start)
    source = source[:start] + '''    private IEnumerator CaptureFrame(string name)
    {
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        Require(texture && texture.width == 608 && texture.height == 1080, "Actual portrait Game View size");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
        Object.Destroy(texture);
    }
    private IEnumerator WaitForPopupShow(GameObject window)
    {
        var popup = window.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
        float deadline = Time.time + 5f;
        while (popup && popup.State != Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show && Time.time < deadline)
            yield return null;
        Require(popup && popup.State == Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show,
            "Original popup completes its show animation before pointer input");
    }
''' + source[end:]
    source = source.replace('        Capture("', '        yield return CaptureFrame("')
    source = source.replace('        Click(notify.GetComponentsInChildren',
        '        yield return WaitForPopupShow(notify.gameObject);\n        Click(notify.GetComponentsInChildren')
    source = source.replace('        var rect = (RectTransform)button.transform;\n        Vector2 point = RectTransformUtility.WorldToScreenPoint(home.Scene.Camera, rect.TransformPoint(rect.rect.center));',
        '        Canvas.ForceUpdateCanvases();\n        var rect = (RectTransform)button.transform;\n'
        '        var camera = button.GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : home.Scene.Camera;\n'
        '        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));')
    source = source.replace('"Repair button is top UI hit");',
        '"Repair button is top UI hit; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name)));')
    seed = 'player.Level = 3; player.Experience = character.CumulativeLevelExperience[3]; player.SubRank = 1;'
    if source.count(seed) != 1:
        raise ValueError('Home repair profile seed changed')
    source = source.replace(seed, '''player.Level = 60; player.Experience = character.CumulativeLevelExperience[60];
            player.SubRank = 12; player.Rank = 4;
            if (!player.UnlockedMapClassIds.Contains(11)) player.UnlockedMapClassIds.Add(11);
            if (!player.ProfessionLevels.Any(value => value.Profession == 1))
                player.ProfessionLevels.Add(new OfflineProfessionLevel { Profession = 1, Level = 1 });''')
    source = source.replace('"Seed real level 3 profile"', '"Seed eligible home validation profile"')
    source = source.replace('        Open();\n        yield return new WaitForSeconds(1);',
        '        Require(profile.SetStoryProgress("SenSenThanks", 1, 2).IsApplied, "Seed the official store prerequisite story");\n'
        '        Open();\n        yield return new WaitForSeconds(1);', 1)
    stop = '        Require(!cartEffect, "Actual cart effect is released");\n        home.Close();'
    if source.count(stop) != 1:
        raise ValueError('Home repair cart validation changed')
    source = source.replace(stop, '''        Require(!cartEffect, "Actual cart effect is released");
        foreach (int type in new[] { 1, 2, 3, 5, 7, 8, 9, 12 })
        {
            notify = Object.FindObjectOfType<FurnitureRepairedNotifyDialog>();
            yield return WaitForPopupShow(notify.gameObject);
            Click(notify.GetComponentsInChildren<Zjcs.Official.ProfessionUI.CloseWindowBtn>()
                .First(value => value.myButton.isActiveAndEnabled).myButton);
            yield return new WaitForSeconds(.6f);
            Require(!home.Session.OwnsFurniture(type) &&
                home.HomeProgression.FurnitureState(home.Session, type, home.Rules.CurrentContext) == HomeFurnitureState.CanRepair,
                "Actual furniture is eligible for repair: " + type);
            PanToFurniture(type);
            SceneClick(type);
            yield return new WaitForSeconds(.8f);
            repair = Object.FindObjectOfType<FurnitureRepairDialog>();
            Require(repair, "Scene pointer opens original repair dialog: " + type);
            Click(repair.RepairBtn);
            Require(home.Session.OwnsFurniture(type), "Actual repair commits: " + type);
            var extraEffect = home.Scene.Root.transform.Find("FurnitureRepairEffect_" + type);
            Require(extraEffect && extraEffect.GetComponentsInChildren<ParticleSystem>(true).Length == 5 &&
                !home.Scene.Root.transform.Find("Furniture_" + type + "_Repaired").gameObject.activeSelf,
                "Original effect begins before its state changes: " + type);
            var originalSlot = home.Catalog.Scene.furniture.Single(value => value.type == type).slot;
            var offset = type == 2 ? new Vector3(.018f, -.047f, 0) : Vector3.zero;
            Require(Vector3.Distance(extraEffect.localPosition,
                new Vector3(originalSlot[0], originalSlot[1], originalSlot[2]) + offset) < .0001f,
                "Original effect furniture root placement: " + type);
            yield return CaptureFrame("home-repair-effect-active-" + type + ".png");
            yield return new WaitForSeconds(type == 7 ? .7f : .35f);
            Require(home.Scene.Root.transform.Find("Furniture_" + type + "_Repaired").gameObject.activeSelf,
                "Actual state changes after original delay: " + type);
            yield return new WaitForSeconds(type == 7 ? 1.2f : .85f);
            Require(!extraEffect, "Actual effect is released: " + type);
        }
        home.Close();''')
    source = source.replace('        PanToFurniture(10);\n        yield return CaptureFrame("home-current-reopened-cart.png");',
        '''        foreach (int type in new[] { 1, 2, 3, 5, 7, 8, 9, 12 })
            Require(home.Session.OwnsFurniture(type) &&
                home.HomeProgression.FurnitureState(home.Session, type, home.Rules.CurrentContext) == HomeFurnitureState.Functional,
                "Actual home reopen restores furniture: " + type);
        PanToFurniture(10);
        yield return CaptureFrame("home-current-reopened-cart.png");''')
    source = source.replace('actual bed/cart drag, native repair input, staged effects and saved restoration.',
        'actual ten-furniture drag, native repair input, staged effects and saved restoration.')
    target = project / 'Assets/Editor/HomeRepairEffectHost/HomeRepairEffectHostValidation.cs'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(source, 'utf8', newline='\n')
    frame = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split('public sealed partial class HomeIncomeFormulaHostRunner')[0]
    frame = frame[frame.index('public static partial class HomeIncomeFormulaValidation'):]
    frame = 'using System;\nusing System.Reflection;\nusing UnityEngine;\n' + frame.replace('HomeIncomeFormulaValidation', 'HomeFurnitureHostValidation')
    (target.parent / 'NativeFrame.cs').write_text(frame, 'utf8', newline='\n')
    references = ['Zjcs.Official.HomeConditions', 'Zjcs.Official.Partners', 'Zjcs.Offline.Home',
        'Zjcs.Offline.HomeUI', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Editor',
        'Zjcs.Presentation.Home', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Official.HomeUI',
        'Zjcs.Offline.ProfessionUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeText', 'Zjcs.Offline.PartnersUI',
        'Zjcs.Official.PartnerFriendshipDialogUI', 'Zjcs.Presentation.World', 'Unity.TextMeshPro',
        'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.UI',
        'spine-unity', 'spine-csharp']
    (target.parent / 'HomeRepairEffectHostValidation.asmdef').write_text(json.dumps(dict(
        name='HomeRepairEffectHostValidation', includePlatforms=['Editor'], references=references), indent=2) + '\n',
        'utf8', newline='\n')
    print('Staged actual home repair effect pointer validation:', project, flush=True)


if __name__ == '__main__':
    main()
