"""Record and stage the original MainCanvas in the actual offline home entry."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_home_auto_increase_runtime import metadata
from validation_workspace import validation_directory


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    inputs = {}
    source = ROOT/'assets/reference/home-hud-frame-native-source-manifest.json'
    proof = json.loads(source.read_bytes())
    assert proof['manifest'] == '884_156692'
    inputs[source.relative_to(ROOT).as_posix()] = sha(source)
    for group in ('installed_output_sha256', 'reused_output_sha256'):
        for relative, digest in proof[group].items():
            assert sha(ROOT/relative) == digest, relative
    source = ROOT/'assets/reference/home-hud-native-source-manifest.json'
    native = json.loads(source.read_bytes())
    assert native['manifest'] == '884_156692'
    inputs[source.relative_to(ROOT).as_posix()] = sha(source)
    for group in ('installed_output_sha256', 'reused_output_sha256'):
        for relative, digest in native[group].items():
            assert sha(ROOT/relative) == digest, relative
    for name in ('ScreenAdaptService', 'ScreenMetrics', 'CanvasWidthAdjuster', 'SafeArea'):
        path = ROOT/'unity/Assets/Game/Runtime/Official/HomeScreenUI'/f'{name}.cs'
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for relative in ('scripts/fixtures/home_furniture_host_validation.cs',
                     'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeHudAssets.cs',
                     'unity/Assets/Game/Resources/Home/OfflineHomeHudAssets.asset',
                     'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs'):
        inputs[relative] = sha(ROOT/relative)
    outputs = {}
    for relative in ('unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.NativeFrame.cs',
                     'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
                     'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef'):
        path = ROOT/relative
        for item in (path, metadata(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    fixture = ROOT/'scripts/fixtures/home_native_frame_validation.cs'
    inputs[fixture.relative_to(ROOT).as_posix()] = sha(fixture)
    result = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='The formal HomeFeatureRuntime uses the packaged original MainCanvas and screen/safe-area controllers. Existing home content and bottom bar mount under original Main/Public slots. Other HUD controllers, avatar, currencies and furniture presentation are still being migrated; no complete HomeUI or Player claim.')
    target = ROOT/'assets/reference/home-native-frame-host-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    return result


def stage(project, proof):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    for relative, digest in proof['outputs'].items():
        source = ROOT/relative
        assert sha(source) == digest
        destination = project/source.relative_to(ROOT/'unity')
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
    folder = project/'Assets/HomeNativeFrameHostValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT/'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf8')
    source = source.replace('HomeFurnitureHostValidation', 'HomeNativeFrameHostValidation').replace('HomeFurnitureHostRunner', 'HomeNativeFrameHostRunner')
    source = source.replace('using Object = UnityEngine.Object;', 'using Object = UnityEngine.Object;\nusing Zjcs.Official.HomeScreenUI;')
    source = source.replace('    private IEnumerator Check()\n    {', '    private IEnumerator Check()\n    {\n        using var metrics = ScreenMetrics.Bind(new PortraitMetrics());')
    source = source.replace('        Capture("home-current-before-repair.png");', '        Capture("home-current-before-repair.png");\n        HomeNativeFrameChecks.Check(home.Hud.CanvasTransform.GetComponent<Canvas>());')
    source = source.replace('home-current-', 'native-frame-host-')
    source = source.replace('"home-host-saves-"', '"native-frame-host-saves-"')
    source = source.replace('    private static void Require(bool result, string message)', '''    private sealed class PortraitMetrics : IScreenMetrics
    {
        public int Width => 608;
        public int Height => 1080;
        public ScreenOrientation Orientation => ScreenOrientation.Portrait;
        public Rect SafeArea => new Rect(0, 0, Width, Height);
    }
    private static void Require(bool result, string message)''')
    assert 'using var metrics' in source and 'HomeNativeFrameChecks.Check' in source
    (folder/'HomeNativeFrameHostValidation.cs').write_text(source, 'utf8', newline='\n')
    shutil.copy2(ROOT/'scripts/fixtures/home_native_frame_validation.cs', folder/'HomeNativeFrameChecks.cs')
    definition = dict(name='Zjcs.HomeNativeFrameHostValidation', includePlatforms=['Editor'], references=[
        'Unity.ugui', 'Zjcs.Presentation.Home', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Progression',
        'Zjcs.Offline.State', 'Zjcs.Offline.Home', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeScreenUI',
        'Zjcs.Offline.ProfessionUI', 'Zjcs.Official.HomeConditions', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI'])
    (folder/'HomeNativeFrameHostValidation.asmdef').write_text(json.dumps(definition, indent=2)+'\n', 'utf8', newline='\n')
    print('Staged native MainCanvas in actual home-entry/repair/reopen fixture.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    proof = build()
    if args.project:
        stage(args.project, proof)
