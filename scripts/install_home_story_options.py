"""Install the verified original story option subtree and its explicit bindings."""
import hashlib
import json
from pathlib import Path

import yaml

from home_native_assets import install_staged_assets
from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid, sha
from validation_workspace import validation_directory


def metadata(path, importer, folder=False):
    path = Path(str(path) + '.meta')
    key = hashlib.md5(('zjcs:home-story-options:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    raw = ('fileFormatVersion: 2\nguid: ' + key + '\n'
        + ('folderAsset: yes\n' if folder else '') + importer + ':\n  externalObjects: {}\n').encode()
    if path.exists() and path.read_bytes() != raw:
        raise ValueError('Preserve existing metadata: ' + str(path))
    path.write_bytes(raw)
    return path


def main():
    work = validation_directory()
    code = json.loads((work / 'home-story-option-code-source-manifest.json').read_bytes())
    native = json.loads((work / 'home-story-option-native-source-manifest.json').read_bytes())
    if code['manifest'] != native['manifest'] or code['manifest'] != '884_156692':
        raise ValueError('The story option sources must use the selected official version.')
    for receipt in (code, native):
        for path, expected in receipt['inputs'].items():
            if sha((ROOT / path).read_bytes()) != expected:
                raise ValueError('Changed story option input: ' + path)
    if sha((ROOT / 'scripts/prepare_home_story_option_code.py').read_bytes()) != code['generator_sha256']:
        raise ValueError('The option code generator changed after staging.')
    staged = work / 'home-story-option-code'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/HomeStoryOptions'
    for name, expected in code['outputs'].items():
        source = staged / name
        output = target / name
        if sha(source.read_bytes()) != expected:
            raise ValueError('Changed staged option controller: ' + name)
        if output.exists() and output.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing option controller: ' + str(output))
    target.mkdir(parents=True, exist_ok=True)
    installed_code = {}
    folder_meta = metadata(target, 'DefaultImporter', folder=True)
    installed_code[folder_meta.relative_to(ROOT).as_posix()] = sha(folder_meta.read_bytes())
    for name in code['outputs']:
        output = target / name
        output.write_bytes((staged / name).read_bytes())
        installed_code[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    code['installed_output_sha256'] = installed_code
    code['scope'] = 'Original option component methods with explicit offline service ports; full dialogue remains separate.'
    (ROOT / 'assets/reference/home-story-option-code-source-manifest.json').write_text(
        json.dumps(code, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')

    for binding in native['script_bindings'].values():
        original = ROOT / binding['path']
        if original.parent == staged:
            original = target / original.name
            binding['path'] = original.relative_to(ROOT).as_posix()
        if sha(original.read_bytes()) != binding['sha256'] or sha(Path(str(original) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Changed original option script binding: ' + str(original))
    installed = install_staged_assets(work, work / 'home-story-option-assets', native['outputs'],
        ROOT / 'unity/Assets/Official/Home/StoryOptionUI', 'home-story-option')
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeUI/HomeStoryOptionAssets.cs'
    script_meta = metadata(script, 'MonoImporter')
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/HomeStoryOptionAssets.asset'
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3),
        m_Name='HomeStoryOptionAssets', m_EditorClassIdentifier='', OptionSlots=native['root'],
        IconArrow1=native['dynamic_icons']['UI/Dialogue/Iconarrow_1']))
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.dump(data, Dumper=Dumper, sort_keys=False)).encode('utf8')
    if catalog.exists() and catalog.read_bytes() != raw:
        raise ValueError('Preserve existing story option catalog: ' + str(catalog))
    catalog.write_bytes(raw)
    catalog_meta = metadata(catalog, 'NativeFormatImporter')
    for path in (script, script_meta, catalog, catalog_meta):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    native['installed_output_sha256'] = installed
    native['installation_script_sha256'] = sha(Path(__file__).read_bytes())
    (ROOT / 'assets/reference/home-story-option-native-source-manifest.json').write_text(
        json.dumps(native, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original story option subtree and explicit catalog.', flush=True)


if __name__ == '__main__':
    main()
