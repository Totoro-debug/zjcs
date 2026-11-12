"""Bind original task condition notifications to the local task UI scope."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from prepare_home_task_condition_reader import ROOT, build as build_reader, stage as stage_reader, sha


def build():
    reader = build_reader()
    proof_path = ROOT / 'assets/reference/home-condition-notifications-source-manifest.json'
    original = json.loads(proof_path.read_bytes())
    assert original['manifest'] == '884_156692'
    assert original['generator_sha256'] == sha(ROOT / 'scripts/prepare_home_condition_notifications.py')
    for group in ('inputs', 'outputs'):
        for relative, expected in original[group].items():
            assert sha(ROOT / relative) == expected, relative
    adapter = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskConditions.cs'
    meta = Path(str(adapter) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-task-condition-notifications').hexdigest()
            + '\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n', 'utf8', newline='\n')
    inputs = {relative: sha(ROOT / relative) for relative in (
        'assets/reference/home-condition-notifications-source-manifest.json',
        'assets/reference/home-task-condition-reader-source-manifest.json',
        'scripts/fixtures/home_task_condition_notifications_validation.cs',
        'scripts/prepare_home_task_condition_reader.py')}
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs,
        outputs={p.relative_to(ROOT).as_posix(): sha(p) for p in (adapter, meta)},
        scope='Original task condition-type identities and selected ConditionHandler event ordering. '
            'Local task UI subscriptions read the live profile through their owning reader. '
            'The player owner must forward matching committed events; automatic forwarding and native task-row composition remain separate.')
    (ROOT / 'assets/reference/home-task-condition-notifications-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return reader, proof, original


def stage(project, reader, proof, original):
    stage_reader(project, reader)
    for relative, expected in dict(original['outputs'], **proof['outputs']).items():
        assert sha(ROOT / relative) == expected, relative
        target = project / (ROOT / relative).relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / relative, target)
    folder = project / 'Assets/HomeCurrencyWindowsNativeValidation'
    path = folder / 'HomeTaskConditionReaderValidation.cs'
    text = path.read_text('utf8')
    marker = '            UnityEngine.Debug.Log("HOME_TASK_CONDITION_READER_PASSED:'
    assert text.count(marker) == 1
    text = text.replace(marker, '            CheckTaskConditionNotifications(profile,reader,data,hub);\n' + marker, 1)
    path.write_text(text, 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/home_task_condition_notifications_validation.cs', folder / 'HomeTaskConditionNotificationsValidation.cs')
    assembly = next(folder.glob('*.asmdef'))
    value = json.loads(assembly.read_bytes())
    for name in ('Zjcs.Official.HomeConditions', 'Zjcs.Offline.HomeUI'):
        if name not in value['references']:
            value['references'].append(name)
    assembly.write_text(json.dumps(value, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original task condition notifications and live-profile subscription validation', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    reader, proof, original = build()
    if args.project:
        stage(args.project, reader, proof, original)
