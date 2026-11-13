"""Bind the original bed/cart prompts to the actual offline home entry."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_home_auto_increase_runtime import metadata, sha
from validation_workspace import validation_directory


def build():
    inputs = {}
    for name in ('home-hud-native', 'home-hangup-tips-runtime'):
        path = ROOT/'assets/reference'/f'{name}-source-manifest.json'
        if not path.exists() and name == 'home-hangup-tips-runtime':
            path = ROOT/'assets/reference/home-hangup-tips-source-manifest.json'
        proof = json.loads(path.read_bytes())
        assert proof['manifest'] == '884_156692'
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        groups = ('installed_output_sha256', 'reused_output_sha256') if name == 'home-hud-native' else ('outputs',)
        for group in groups:
            for relative, digest in proof.get(group, {}).items():
                assert sha(ROOT/relative) == digest, relative
    code = ROOT/'.tools/source-packages/current/884_156692/official-code'
    for relative in ('common-source/Common/PlayerExpUtils.cs', 'common-source/Common/PlayerHangUpDataExt.cs',
                     'common-source/Common/FunctionType.cs', 'common-source/Common/Player.cs',
                     'client-source/HomeCameraController.cs'):
        path = code/relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for relative in ('scripts/fixtures/home_furniture_host_validation.cs',
                     'scripts/fixtures/home_hangup_host_validation.cs',
                     'unity/Assets/Game/Runtime/Offline/State/UserDataStorage.cs',
                     'unity/Assets/Game/Runtime/Offline/State/UserDataStorage.cs.meta',
                     'unity/Assets/Game/Runtime/Offline/State/Zjcs.Offline.State.asmdef',
                     'unity/Assets/Game/Runtime/Offline/State/Zjcs.Offline.State.asmdef.meta',
                     'unity/Assets/StreamingAssets/Home/scene.json',
                     'unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs'):
        inputs[relative] = sha(ROOT/relative)
    outputs = {}
    for relative in ('unity/Assets/Game/Runtime/Presentation/Home/HomeHangUpHudRuntime.cs',
                     'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
                     'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
                     'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
                     'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs'):
        path = ROOT/relative
        for item in (path, metadata(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='Actual home income drives the original bed/cart tip controllers and native direction guides under the original safe area. Anchors and camera clamp use the current packaged 871 scene conversion. The retained reward-retrieval NPC subtree stays inactive until its owner is migrated; complete HomeUI, original home scene and Player publication remain pending.')
    (ROOT/'assets/reference/home-hangup-host-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    return proof


def stage(project, proof):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    for relative, digest in proof['outputs'].items():
        source = ROOT/relative
        assert sha(source) == digest
        destination = project/source.relative_to(ROOT/'unity')
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
    for name in ('UserDataStorage.cs', 'Zjcs.Offline.State.asmdef'):
        for suffix in ('', '.meta'):
            relative = 'Assets/Game/Runtime/Offline/State/'+name+suffix
            shutil.copy2(ROOT/'unity'/relative, project/relative)
    folder = project/'Assets/HomeHangUpHostValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT/'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf8')
    source = source.replace('HomeFurnitureHostValidation', 'HomeHangUpHostValidation').replace('HomeFurnitureHostRunner', 'HomeHangUpHostRunner')
    source = source.replace('Zjcs.HomeFurnitureHost', 'Zjcs.HomeHangUpHost')
    source = source.replace('"Repair button is top UI hit"', '"Top UI hit for " + button.name + " at " + point + ": " + string.Join(",", hits.Select(hit => hit.gameObject.name))')
    source = source.replace('        var checks = Check();', '        var checks = Flatten(Check());')
    source = source.replace('    private IEnumerator Check()', '''    private static IEnumerator Flatten(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if (current.Current is IEnumerator child) stack.Push(child);
                else yield return current.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
    private IEnumerator Check()''')
    source = source.replace('sealed class HomeHangUpHostRunner', 'sealed partial class HomeHangUpHostRunner')
    source = source.replace('using Object = UnityEngine.Object;', 'using Object = UnityEngine.Object;\nusing Zjcs.Official.HomeScreenUI;')
    source = source.replace('    private IEnumerator Check()\n    {', '    private IEnumerator Check()\n    {\n        using var metrics = ScreenMetrics.Bind(new PortraitMetrics());')
    source = source.replace('        Capture("home-current-reopened.png");', '        Capture("home-current-reopened.png");\n        yield return CheckHangUp();')
    source = source.replace('home-current-', 'hangup-host-').replace('"home-host-saves-"', '"hangup-host-saves-"')
    source = source.replace('    private static void Require(bool result, string message)', '''    private sealed class PortraitMetrics : IScreenMetrics
    {
        public int Width => 608;
        public int Height => 1080;
        public ScreenOrientation Orientation => ScreenOrientation.Portrait;
        public Rect SafeArea => new Rect(0, 0, Width, Height);
    }
    private static void Require(bool result, string message)''')
    assert 'using var metrics' in source and 'yield return CheckHangUp();' in source
    (folder/'HomeHangUpHostValidation.cs').write_text(source, 'utf8', newline='\n')
    shutil.copy2(ROOT/'scripts/fixtures/home_hangup_host_validation.cs', folder/'HomeHangUpHostChecks.cs')
    definition = dict(name='Zjcs.HomeHangUpHostValidation', includePlatforms=['Editor'], references=[
        'Unity.ugui', 'Zjcs.Presentation.Home', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Progression',
        'Zjcs.Offline.State', 'Zjcs.Offline.Home', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeScreenUI',
        'Zjcs.Official.HomeSceneUiControls', 'Zjcs.Offline.HomeHangUpUI', 'Zjcs.Official.HomeHangUpTips',
        'Zjcs.Offline.ProfessionUI', 'Zjcs.Official.HomeConditions', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.HomePlayerPanelUI', 'Zjcs.Official.HomeFunctionMenuUI',
        'Zjcs.Official.World.NativeCamera'])
    (folder/'HomeHangUpHostValidation.asmdef').write_text(json.dumps(definition, indent=2)+'\n', 'utf8', newline='\n')
    print('Staged actual home entry, original bed/cart tips and native direction guide checks.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    proof = build()
    if args.project:
        stage(args.project, proof)
