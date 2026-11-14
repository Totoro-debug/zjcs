"""Bind the installed original function menu to its reusable local window owner."""
import hashlib
import json
import shutil
from pathlib import Path

import yaml
from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid
from validation_workspace import validation_directory, require_temporary_unity_project


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeFunctionMenuUI'
    paths = [runtime, *sorted(runtime.iterdir())]
    for path in paths:
        if path.suffix == '.meta':
            continue
        meta = Path(str(path) + '.meta')
        key = hashlib.md5(('zjcs:home-function-menu-owner:' + path.name).encode()).hexdigest()
        if not meta.exists():
            importer = ('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if path.is_dir() else
                        'AssemblyDefinitionImporter:\n  externalObjects: {}\n' if path.suffix == '.asmdef' else
                        'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
                        '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
            meta.write_text('fileFormatVersion: 2\nguid: ' + key + '\n' + importer, 'utf8', newline='\n')
    native_manifest = ROOT / 'assets/reference/home-function-menu-native-source-manifest.json'
    receipt = json.loads(native_manifest.read_bytes())
    for group in ('installed_output_sha256', 'reused_output_sha256'):
        for relative, expected in receipt[group].items():
            assert sha(ROOT / relative) == expected, relative
    script = runtime / 'OfflineHomeFunctionMenuAssets.cs'
    # Bind the complete selected SafeArea implementation, preserving every
    # serialized object, field and reference in the recovered menu prefab.
    native = ROOT / 'unity/Assets/Official/Home/UI/FunctionMenu/FunctionEntryDialog.prefab'
    old_script = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldSafeArea.cs.meta'
    safe_script = ROOT / 'unity/Assets/Game/Runtime/Official/HomeScreenUI/SafeArea.cs.meta'
    original = native.read_text('utf8')
    needle = 'guid: ' + guid(old_script)
    assert original.count(needle) == 1, 'Review original menu SafeArea binding'
    bound = native.with_name('FunctionEntryDialogBound.prefab')
    bound_data = original.replace(needle, 'guid: ' + guid(safe_script))
    bound_key = hashlib.md5(b'zjcs:home-function-menu-full-safe-area').hexdigest()
    bound_meta = 'fileFormatVersion: 2\nguid: ' + bound_key + '\nPrefabImporter:\n  externalObjects: {}\n'
    for path, data in ((bound, bound_data), (Path(str(bound) + '.meta'), bound_meta)):
        if path.exists() and path.read_text('utf8') != data:
            raise ValueError('Preserve different existing original menu binding: ' + str(path))
        if not path.exists():
            path.write_text(data, 'utf8', newline='\n')
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name='OfflineHomeFunctionMenuAssets', m_EditorClassIdentifier='',
        prefab=dict(receipt['roots']['FunctionEntryDialog'], guid=bound_key),
        texts=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-function-menu-texts').hexdigest(), type=3),
        tipSourceObjects=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-menu-tip-source-objects').hexdigest(), type=3),
        sounds=[dict(eventId=row['event']['id'], clip=row['pointer']) for row in receipt['audio'].values()])
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/OfflineHomeFunctionMenuAssets.asset'
    catalog_data = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' + yaml.dump(
        dict(MonoBehaviour=fields), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    catalog_meta = ('fileFormatVersion: 2\nguid: ' +
        hashlib.md5(b'zjcs:home-function-menu-owner-catalog').hexdigest() +
        '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n')
    for path, data in ((catalog, catalog_data), (Path(str(catalog) + '.meta'), catalog_meta)):
        if path.exists() and path.read_text('utf8') != data:
            raise ValueError('Preserve different existing function menu catalog: ' + str(path))
        if not path.exists():
            path.write_text(data, 'utf8', newline='\n')
    outputs = [Path(str(runtime) + '.meta'), *sorted(runtime.iterdir()), catalog, Path(str(catalog) + '.meta'),
               bound, Path(str(bound) + '.meta')]
    for source in outputs:
        target = work / source.relative_to(ROOT)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    fixture = ROOT / 'scripts/fixtures/home_function_menu_owner_validation.cs'
    original_fixture = ROOT / 'scripts/fixtures/home_function_menu_native_validation.cs'
    original = original_fixture.read_text('utf8')
    services = original[original.index('    sealed class MenuCondition:'):]
    folder = project / 'Assets/HomeFunctionMenuOwnerValidation'
    folder.mkdir(exist_ok=True)
    (folder / 'HomeFunctionMenuOwnerRunner.cs').write_text(fixture.read_text('utf8'), 'utf8', newline='\n')
    (folder / 'ControlledServices.cs').write_text('#if UNITY_EDITOR\n' + original[:original.index('public sealed partial class')] +
        'public sealed partial class HomeFunctionMenuOwnerRunner\n{\n' + services + '\n#endif\n', 'utf8', newline='\n')
    references = ['Unity.ugui', 'Unity.TextMeshPro', 'UniTask', 'Zjcs.Official.HomeFunctionMenuUI',
        'Zjcs.Official.HomeHudUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI',
        'Zjcs.Official.HomePlayerPanelUI', 'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.HomeScreenUI',
        'Zjcs.Offline.HomeFunctionMenuUI', 'Zjcs.Offline.HomeHudUI']
    (folder / 'Zjcs.HomeFunctionMenuOwnerValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeFunctionMenuOwnerValidation', references=references), indent=2) + '\n', 'utf8', newline='\n')
    inputs = [native_manifest, fixture, original_fixture, Path(__file__), native, old_script, safe_script,
              Path(str(safe_script)[:-5]),
              ROOT / '.tools/source-packages/current/884_156692/official-code/framework-firstpass-source-resolved/SafeArea.cs']
    proof = dict(manifest='884_156692', inputs={path.relative_to(ROOT).as_posix(): sha(path) for path in inputs},
        runtime={path.relative_to(ROOT).as_posix(): sha(path) for path in outputs},
        scope='Original menu prefab, explicit pre-Awake local bindings, native show/hide and window observer '
              'lifetime. Feature endpoints are controlled in this fixture; production HUD attachment and '
              'full tip graph evaluation remain pending.')
    (ROOT / 'assets/reference/home-function-menu-owner-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original function menu owner:', len(outputs), 'runtime assets')


if __name__ == '__main__':
    prepare()
