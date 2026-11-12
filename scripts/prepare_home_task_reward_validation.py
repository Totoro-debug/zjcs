"""Stage original reward icons into the existing isolated currency UI host."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_home_currency_windows_validation import stage_fixture as stage_currency_fixture
from validation_workspace import validation_directory


def stage(project):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    from prepare_partner_card_native import sha
    currency_path = ROOT / 'assets/reference/home-currency-windows-native-source-manifest.json'
    currency = json.loads(currency_path.read_bytes())
    assert currency['manifest'] == '884_156692'
    for group in ('installed_output_sha256', 'reused_output_sha256', 'catalog_outputs'):
        for relative, expected in currency[group].items():
            source = ROOT / relative
            assert sha(source.read_bytes()) == expected, relative
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    for name in ('HomeCurrencyUI',):
        module = ROOT / 'unity/Assets/Game/Runtime/Offline' / name
        for source in (Path(str(module) + '.meta'), *sorted(module.iterdir())):
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    stage_currency_fixture(ROOT / 'assets/reference', project)
    proof = json.loads((ROOT / 'assets/reference/home-task-reward-images-source-manifest.json').read_bytes())
    assert proof['manifest'] == '884_156692'
    assert proof['binding_generator_sha256'] == sha((ROOT / 'scripts/prepare_home_task_reward_bindings.py').read_bytes())
    for relative, expected in {**proof['binding_inputs'], **proof['runtime_output_sha256']}.items():
        assert sha((ROOT / relative).read_bytes()) == expected, relative
    for relative, expected in {**proof['installed_output_sha256'], **proof['reused_output_sha256']}.items():
        source = ROOT / relative
        assert sha(source.read_bytes()) == expected, relative
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTaskUI'
    for source in (Path(str(module) + '.meta'), *sorted(module.iterdir())):
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    folder = project / 'Assets/HomeCurrencyWindowsNativeValidation'
    fixture = folder / 'HomeCurrencyWindowsNativeValidation.cs'
    text = fixture.read_text('utf8')
    before = 'public sealed class HomeCurrencyWindowsNativeRunner:'
    assert text.count(before) == 1
    text = text.replace(before, 'public sealed partial class HomeCurrencyWindowsNativeRunner:')
    before = '        var info=assets.QuickBuy(1);'
    assert text.count(before) == 1
    text = text.replace(before, '        yield return ExerciseTaskRewardIcons();\n' + before)
    before = 'new IconInfo(assets.ItemRow(id));public object Compact'
    assert text.count(before) == 1
    text = text.replace(before, 'new IconInfo(taskIconData==null?assets.ItemRow(id):taskIconData.CopyItemRow(id));public object Compact')
    fixture.write_text(text, 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/home_task_reward_icons_native_validation.cs', folder / 'HomeTaskRewardIconsNativeValidation.cs')
    asmdef = folder / 'HomeCurrencyWindowsNativeValidation.asmdef'
    data = json.loads(asmdef.read_bytes())
    data['references'] += ['Zjcs.Offline.HomeTaskUI', 'Zjcs.Official.HomeTaskUI']
    asmdef.write_text(json.dumps(data, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged selected reward icons with original currency validation host', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args()
    stage(args.project)
