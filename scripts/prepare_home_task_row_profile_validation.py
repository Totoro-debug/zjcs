"""Exercise the original task rows with actual local task/reward transactions."""
import argparse
import json
from pathlib import Path
import shutil

from prepare_home_task_player import ROOT, sha, build as build_player, stage as stage_player
from validation_workspace import validation_directory


def build():
    proof = json.loads((ROOT / 'assets/reference/home-task-player-source-manifest.json').read_bytes())
    native = json.loads((ROOT / 'assets/reference/home-task-ui-native-source-manifest.json').read_bytes())
    assert native['manifest'] == proof['manifest'] == '884_156692'
    inputs = {relative: sha(ROOT / relative) for relative in (
        'assets/reference/home-task-player-source-manifest.json',
        'assets/reference/home-task-ui-native-source-manifest.json',
        'scripts/fixtures/home_task_row_profile_validation.cs')}
    for group in ('installed_output_sha256', 'reused_output_sha256'):
        for relative, expected in native[group].items():
            assert sha(ROOT / relative) == expected, relative
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs,
        scope='Original native task rows with an actual character profile, condition/event projection and reward transactions. '
            'Explicit fixture boundaries: map limits, combat/rank, calendar, navigation/trace and tip visibility. '
            'Production HUD composition and full task route remain separate.')
    (ROOT / 'assets/reference/home-task-row-profile-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return native


def stage(project, player, native):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    stage_player(project, *player)
    for group in ('installed_output_sha256', 'reused_output_sha256'):
        for relative, expected in native[group].items():
            assert sha(ROOT / relative) == expected
            target = project / (ROOT / relative).relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / relative, target)
    folder = project / 'Assets/HomeCurrencyWindowsNativeValidation'
    path = folder / 'HomeCurrencyWindowsNativeValidation.cs'
    text = path.read_text('utf8')
    marker = '        yield return ExerciseTaskPlayer();'
    assert text.count(marker) == 1
    path.write_text(text.replace(marker, marker + '\n        yield return ExerciseTaskRowsWithProfile();', 1), 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/home_task_row_profile_validation.cs', folder / 'HomeTaskRowProfileValidation.cs')
    path = folder / 'HomeCurrencyWindowsNativeValidation.asmdef'
    definition = json.loads(path.read_bytes())
    definition['references'] = list(dict.fromkeys(definition['references'] + ['Zjcs.Official.HomeMainUiControls']))
    path.write_text(json.dumps(definition, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original task rows with actual profile transactions', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    player = build_player()
    native = build()
    if args.project: stage(args.project, player, native)
