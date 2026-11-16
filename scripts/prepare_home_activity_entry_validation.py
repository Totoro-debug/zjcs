"""Stage an original home right-entry prefab in an owned graphical Unity test project."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_home_activity_entry_native import validate_dynamic_atlas
from prepare_partner_card_native import guid, sha
from validation_workspace import validation_directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--first-pay', action='store_true')
    first_pay = parser.parse_args().first_pay
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    source = ROOT / 'unity'
    project = work / 'native-unity'
    if project.exists():
        raise ValueError('Use a fresh validation project: ' + str(project))
    receipt_name = 'home-first-pay-entry-native-source-manifest.json' if first_pay else 'home-activity-entry-native-source-manifest.json'
    receipt = json.loads((ROOT / 'assets/reference' / receipt_name).read_bytes())
    if receipt['manifest'] != '884_156692':
        raise ValueError('Unexpected activity entry source version')
    for group in ('installed_output_sha256', 'folder_meta_sha256', 'runtime_bindings'):
        for relative, expected in receipt[group].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Activity entry validation input changed: ' + relative)
    for relative, row in receipt['reused_assets'].items():
        current = (ROOT / relative).read_bytes()
        if sha(current) == row['staged_sha256']:
            continue
        if relative != 'unity/Assets/Official/Partners/UI/Card/Converted/ac573a40fda4c096.asset':
            raise ValueError('Reused activity entry input changed: ' + relative)
        baseline = subprocess.check_output(['git', 'show', 'HEAD:' + relative], cwd=ROOT)
        if sha(baseline) != row['staged_sha256']:
            raise ValueError('Dynamic font atlas baseline differs: ' + relative)
        validate_dynamic_atlas(baseline, current)

    names = subprocess.check_output(['rg', '--files', str(source / 'Assets'), str(source / 'Packages'),
        '-g', '*.asmdef'], text=True).splitlines()
    by_name, by_guid, by_dir = {}, {}, {}
    for name in names:
        path = Path(name)
        definition = json.loads(path.read_text(encoding='utf-8-sig'))
        by_name[definition['name']] = (path, definition)
        by_dir[path.parent] = definition['name']
        metadata = Path(str(path) + '.meta')
        if metadata.is_file():
            by_guid[guid(metadata)] = definition['name']
    selected = {'Zjcs.Offline.HomeHudUI'}
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if not path.is_relative_to(source / 'Assets'):
            continue
        for parent in path.parents:
            if parent in by_dir:
                selected.add(by_dir[parent])
                break
    queue = list(selected)
    while queue:
        name = queue.pop()
        if name not in by_name:
            continue  # Unity packages are installed through the project manifest.
        for reference in by_name[name][1].get('references', []):
            dependency = by_guid.get(reference[5:], reference) if reference.startswith('GUID:') else reference
            if dependency not in selected:
                selected.add(dependency)
                queue.append(dependency)

    def copy(path):
        target = project / path.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)

    copied = set()
    for name in selected:
        if name not in by_name:
            continue
        base = by_name[name][0].parent
        if not base.is_relative_to(source / 'Assets'):
            continue
        for path in base.rglob('*'):
            if not path.is_file() or path.suffix not in ('.cs', '.asmdef', '.asmref', '.rsp'):
                continue
            nearest = next((by_dir[parent] for parent in path.parents if parent in by_dir), None)
            if nearest not in selected or path in copied:
                continue
            copy(path)
            metadata = Path(str(path) + '.meta')
            if metadata.is_file():
                copy(metadata)
            copied.add(path)
    refs = subprocess.check_output(['rg', '--files', str(source / 'Assets'), '-g', '*.asmref'], text=True).splitlines()
    for name in refs:
        path = Path(name)
        reference = json.loads(path.read_text(encoding='utf-8-sig'))['reference']
        assembly = by_guid.get(reference[5:], reference) if reference.startswith('GUID:') else reference
        if assembly not in selected:
            continue
        for item in path.parent.rglob('*'):
            if item.is_file() and (item.suffix in ('.cs', '.asmref') or item.name.endswith(('.cs.meta', '.asmref.meta'))):
                copy(item)
    for group in ('installed_output_sha256', 'folder_meta_sha256'):
        for relative in receipt[group]:
            copy(ROOT / relative)
    for relative in receipt['reused_assets']:
        path = ROOT / relative
        copy(path)
        copy(Path(str(path) + '.meta'))
    catalog = 'OfflineHomeFirstPayEntryAssets' if first_pay else 'OfflineHomeActivityEntryAssets'
    for relative in ('Assets/Game/Resources/Home/' + catalog + '.asset',
                     'Assets/Game/Resources/Home/' + catalog + '.asset.meta',
                     'Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll',
                     'Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll.meta'):
        copy(source / relative)
    shutil.copytree(source / 'Packages', project / 'Packages',
        ignore=shutil.ignore_patterns('Tests', 'Tests.meta', 'Documentation~', 'Samples~'))
    shutil.copytree(source / 'ProjectSettings', project / 'ProjectSettings')
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True)
    fixture = 'home_first_pay_prefab_validation.cs' if first_pay else 'home_activity_entry_prefab_validation.cs'
    target = 'HomeFirstPayPrefabValidation.cs' if first_pay else 'HomeActivityEntryPrefabValidation.cs'
    shutil.copy2(ROOT / 'scripts/fixtures' / fixture, editor / target)
    print('Staged original home entry prefab validation:', len(selected), 'assembly names,', len(copied), 'source files', flush=True)


if __name__ == '__main__':
    main()
