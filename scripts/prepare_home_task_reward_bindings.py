"""Install the selected mainline reward sprites and explicit Resources catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_card_native import guid, sha
from prepare_home_scene_ui_validation import native_equivalent
from validation_workspace import validation_directory
import yaml


def install(work, project):
    work = work.resolve()
    assert work.parent == (ROOT / 'output/validation').resolve() and (work / '.zjcs-validation.json').is_file()
    if project.resolve() != (ROOT / 'unity').resolve():
        assert project.resolve().is_relative_to(validation_directory().resolve())
    receipt = json.loads((work / 'home-task-reward-images-source-manifest.json').read_bytes())
    assert receipt['manifest'] == '884_156692'
    assert receipt['generator_sha256'] == sha((ROOT / 'scripts/prepare_home_task_reward_images.py').read_bytes())
    for relative, expected in receipt['inputs'].items():
        assert sha((ROOT / relative).read_bytes()) == expected, relative
    currency_path = ROOT / 'assets/reference/home-currency-windows-native-source-manifest.json'
    currency = json.loads(currency_path.read_bytes())
    assert currency['manifest'] == receipt['manifest']
    config = currency['icon_manager']['fields']
    assert set(receipt['items']) == {'1', '2', '50', '110', '120', '41300', '42200'}
    assert not config['IdList'] and not config['IdPrefabList']
    for row in receipt['items'].values():
        assert row['item']['ItemType'] not in config['TypeList'], row['item']['ClassId']
    source_root = work / 'task-reward-images'
    keys = {guid(source_root / name) for name in receipt['outputs'] if name.endswith('.meta')}
    result = subprocess.run(['rg', '--files-with-matches', '--null', '--no-ignore', '--glob', '*.meta',
        '-e', '^guid: (' + '|'.join(sorted(keys)) + ')\\r?$', str(project / 'Assets')],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode not in (0, 1):
        raise RuntimeError(result.stderr.decode('utf8', 'replace'))
    existing = {}
    for name in result.stdout.decode('utf8').split('\0'):
        if name:
            meta = Path(name)
            key = guid(meta)
            assert key not in existing, key
            existing[key] = Path(str(meta)[:-5])
    outputs, reused, plan = {}, {}, []
    destination = project / 'Assets/Official/Home/UI/TaskRewards'
    for relative, expected in receipt['outputs'].items():
        source = source_root / relative
        assert sha(source.read_bytes()) == expected, relative
        if source.suffix == '.meta':
            continue
        source_meta = Path(str(source) + '.meta')
        target = existing.get(guid(source_meta), destination / relative)
        if target.exists():
            assert native_equivalent(source.read_bytes(), target.read_bytes()), target
            for path in (target, Path(str(target) + '.meta')):
                recorded = outputs if path.is_relative_to(destination) else reused
                recorded['unity/' + path.relative_to(project).as_posix()] = sha(path.read_bytes())
        else:
            for original, path in ((source, target), (source_meta, Path(str(target) + '.meta'))):
                plan.append((original, path))
                outputs['unity/' + path.relative_to(project).as_posix()] = sha(original.read_bytes())
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    destination.mkdir(parents=True, exist_ok=True)
    for folder in (destination, *sorted(path for path in destination.rglob('*') if path.is_dir())):
        meta = Path(str(folder) + '.meta')
        if not meta.exists():
            key = hashlib.md5(('zjcs:home-task-reward-folder:' + folder.relative_to(project).as_posix()).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: ' + key + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        outputs['unity/' + meta.relative_to(project).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
    path = project / 'Assets/Game/Resources/Home/OfflineHomeTaskRewardImages.asset'
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name=path.stem, m_EditorClassIdentifier='', Entries=[dict(
            Path='Assets/Image/' + row['item']['RawIcon'], AssetGuid=row['manifest_asset']['guid'], Asset=row['pointer'])
            for row in receipt['items'].values()])
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.safe_dump(dict(MonoBehaviour=body), sort_keys=False, allow_unicode=True), 'utf8', newline='\n')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-task-reward-images').hexdigest()
            + '\nNativeFormatImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
    for value in (path, meta):
        outputs['unity/' + value.relative_to(project).as_posix()] = sha(value.read_bytes())
    if project.resolve() == (ROOT / 'unity').resolve():
        receipt.update(installed_output_sha256=outputs, reused_output_sha256=reused,
            binding_generator_sha256=sha(Path(__file__).read_bytes()),
            binding_inputs={currency_path.relative_to(ROOT).as_posix(): sha(currency_path.read_bytes())},
            scope='Seven selected native reward sprites and Windows texture dependencies, explicitly bound by Resources catalog. '
                'All seven original item types select the default ItemIcon prefab; runtime pool forwards original icon behavior. '
                'Live task-row transactions and production detail-window integration remain required.')
        module = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTaskUI'
        receipt['runtime_output_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
            for path in (Path(str(module) + '.meta'), *sorted(module.iterdir()))}
        for relative in ('scripts/prepare_home_task_reward_validation.py',
                'scripts/prepare_home_currency_windows_validation.py',
                'scripts/fixtures/home_task_reward_icons_native_validation.cs',
                'scripts/fixtures/home_currency_windows_native_validation.cs',
                '.tools/source-packages/current/884_156692/official-code/client-source/ItemIconManager.cs',
                '.tools/source-packages/current/884_156692/official-code/client-source/UIUtils.cs',
                '.tools/source-packages/current/884_156692/official-code/client-source/ItemIcon.cs'):
            receipt['binding_inputs'][relative] = sha((ROOT / relative).read_bytes())
        (ROOT / 'assets/reference/home-task-reward-images-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Original task reward bindings:', len(outputs), 'installed outputs;', len(reused), 'reused outputs', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--source-work', type=Path, required=True)
    parser.add_argument('--project', type=Path, default=ROOT / 'unity')
    args = parser.parse_args()
    install(args.source_work, args.project)
