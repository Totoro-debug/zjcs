"""Generate the formal home player flow for the editor or an owned Unity copy."""
import json
import hashlib
import argparse
import shutil
from pathlib import Path
from validation_workspace import validation_directory, require_temporary_unity_project

ROOT = Path(__file__).resolve().parents[1]


def write_receipt():
    sources = ['home-player-panel-native', 'home-player-info-native', 'home-player-health-profile',
        'home-player-status', 'home-avatar-portraits', 'home-avatar-native', 'home-experience-profile',
        'home-experience-growth', 'home-experience-environment', 'home-experience-rewards',
        'home-level-up-windows', 'home-level-up-properties', 'home-no-bless-rules', 'home-resonance-profile',
        'home-hud-native']
    inputs = ['assets/reference/' + name + '-source-manifest.json' for name in sources]
    bindings = ['scripts/prepare_home_native_player_validation.py', 'scripts/fixtures/home_native_player_validation.cs',
        'scripts/fixtures/home_furniture_host_validation.cs', 'scripts/fixtures/home_income_formula_validation.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionSoundHost.cs']
    bindings += ['unity/Assets/Game/Runtime/Presentation/Home/' + name for name in [
        'HomeHudView.NativePlayer.cs', 'HomeHudView.NativePlayer.cs.meta', 'HomeHudView.cs',
        'HomeFeatureRuntime.cs', 'HomeAcceptanceDriver.cs', 'HomeHudView.Tasks.cs', 'Zjcs.Presentation.Home.asmdef']]
    editor = ROOT / 'unity/Assets/Game/Validation/HomeNativePlayer'
    if editor.is_dir():
        bindings += [path.relative_to(ROOT).as_posix() for path in sorted(editor.iterdir()) if path.is_file()]
        bindings += [editor.with_name(editor.name + '.meta').relative_to(ROOT).as_posix()]
        bindings += [editor.parent.with_name(editor.parent.name + '.meta').relative_to(ROOT).as_posix()]
    digest = lambda name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
    result = dict(manifest='884_156692', inputs={name: digest(name) for name in inputs},
        runtime_bindings={name: digest(name) for name in bindings},
        loading='Formal HomeHudView.Build -> original MainCanvas Public/Anim -> complete PublicPlayerInfo prefab; local profile, portrait studio, original XP reward handler and level-up windows.')
    (ROOT / 'assets/reference/home-native-player-host-source-manifest.json').write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def prepare(root_project=False):
    if validation_directory() == ROOT / 'output/validation':
        raise ValueError('Set ZJCS_VALIDATION_DIR to an owned validation session first.')
    project = ROOT / 'unity' if root_project else require_temporary_unity_project(validation_directory() / 'unity')
    folder = project / ('Assets/Game/Validation/HomeNativePlayer' if root_project else 'Assets/HomeNativePlayerValidation')
    folder.mkdir(parents=True, exist_ok=True)
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf-8-sig')
    source = source.replace('HomeFurnitureHost', 'HomeNativePlayer')
    source = source.replace('if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))',
        'if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar)\n'
        '            && !string.Equals(Path.GetFullPath(Application.dataPath), Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets")), StringComparison.OrdinalIgnoreCase))')
    source = source.replace('public static class HomeNativePlayerValidation', 'public static partial class HomeNativePlayerValidation')
    source = source.replace('public sealed class HomeNativePlayerRunner', 'public sealed partial class HomeNativePlayerRunner')
    source = source.replace('EditorApplication.EnterPlaymode();', 'ConfigureNativeFrame();\n        EditorApplication.EnterPlaymode();')
    source = source.replace('+ 240);', '+ 900);')
    source = source.replace('AudioListener.volume = 0;', 'Application.runInBackground = true;\n        AudioListener.volume = 0;')
    source = source.replace('var checks = Check();', 'var steps = new Stack<IEnumerator>(); steps.Push(Check());')
    source = source.replace('try { if (!checks.MoveNext()) break; current = checks.Current; }',
        'try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }')
    source = source.replace('            yield return current;', '            if (current is IEnumerator nested) { steps.Push(nested); continue; }\n            yield return current;')
    a = source.index('        Require(home.Rules.FurnitureVersion')
    b = source.index('    private void Open()', a)
    source = source[:a] + '        yield return CheckFormalPlayer();\n    }\n}\n'
    source = source.replace('    private RenderTexture target;\n', '').replace('        CloseCapture();\n', '')
    source = source.replace('HOME_FURNITURE_HOST_PASSED', 'HOME_NATIVE_PLAYER_PASSED')
    source = source.replace('actual home entry, simulated scene and native repair input, four-state binding and saved restoration.',
        'formal home native player panel, portrait pointer, XP animation, notification and saved restoration.')
    (folder / 'HomeNativePlayerRunner.cs').write_text('#if UNITY_EDITOR\n' + source + '\n#endif\n', 'utf8', newline='\n')
    config = (ROOT / 'scripts/fixtures/home_income_formula_validation.cs').read_text('utf8').split('public sealed partial class HomeIncomeFormulaHostRunner')[0]
    config = config[config.index('public static partial class HomeIncomeFormulaValidation'):]
    config = 'using System;\nusing System.Reflection;\nusing UnityEngine;\n' + config.replace('HomeIncomeFormulaValidation', 'HomeNativePlayerValidation')
    (folder / 'NativeFrame.cs').write_text('#if UNITY_EDITOR\n' + config + '\n#endif\n', 'utf8', newline='\n')
    shutil.copyfile(ROOT / 'scripts/fixtures/home_native_player_validation.cs', folder / 'PlayerChecks.cs')
    refs = json.loads((ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef').read_text('utf8'))['references']
    refs += ['Zjcs.Presentation.Home']
    (folder / 'Zjcs.HomeNativePlayerValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeNativePlayerValidation', references=sorted(set(refs))), indent=2) + '\n', 'utf8')
    for path in ([folder.parent] if root_project else []) + [folder, *folder.iterdir()]:
        if path.suffix == '.meta':
            continue
        meta = path.with_name(path.name + '.meta')
        if meta.exists():
            continue
        guid = hashlib.md5(('zjcs:home-native-player-validation:' + path.name).encode()).hexdigest()
        body = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
        if path.is_dir():
            body += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        elif path.suffix == '.cs':
            body += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        else:
            body += 'AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        meta.write_text(body, 'utf8', newline='\n')
    # Overlay the exact current integration after the asset copy has completed.
    if not root_project:
        for relative in ['Game/Runtime/Presentation/Home', 'Game/Runtime/Offline/ProfessionUI']:
            shutil.copytree(ROOT / 'unity/Assets' / relative, project / 'Assets' / relative, dirs_exist_ok=True)
    write_receipt()
    print('Staged formal home native player validation: ' + str(project))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root-project', action='store_true', help='Generate the permanent editor validation entry in the main project.')
    prepare(parser.parse_args().root_project)
