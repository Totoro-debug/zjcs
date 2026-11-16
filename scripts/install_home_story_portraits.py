"""Install original story portrait prefabs and GUID-addressed Sprite bindings."""
import json
from pathlib import Path

import yaml

from home_native_assets import install_staged_assets
from install_home_story_options import metadata
from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid, sha
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    code = json.loads((work / 'home-story-portrait-code-source-manifest.json').read_bytes())
    native = json.loads((work / 'home-story-portrait-native-source-manifest.json').read_bytes())
    if code['manifest'] != native['manifest'] or code['manifest'] != '884_156692':
        raise ValueError('The story portrait sources must use the selected official version.')
    for receipt in (code, native):
        for path, expected in receipt['inputs'].items():
            if sha((ROOT / path).read_bytes()) != expected:
                raise ValueError('Changed story portrait input: ' + path)
    if sha((ROOT / 'scripts/prepare_home_story_portrait_code.py').read_bytes()) != code['generator_sha256']:
        raise ValueError('The portrait code generator changed after staging.')
    staged = work / 'home-story-portrait-code'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/HomeStoryPortrait'
    for name, expected in code['outputs'].items():
        source, output = staged / name, target / name
        if sha(source.read_bytes()) != expected: raise ValueError('Changed staged portrait controller: ' + name)
        if output.exists() and output.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing portrait controller: ' + str(output))
    target.mkdir(parents=True, exist_ok=True)
    folder_meta = metadata(target, 'DefaultImporter', folder=True)
    installed_code = {folder_meta.relative_to(ROOT).as_posix(): sha(folder_meta.read_bytes())}
    for name in code['outputs']:
        output = target / name
        output.write_bytes((staged / name).read_bytes())
        installed_code[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    code['installed_output_sha256'] = installed_code
    code['scope'] = 'Original portrait and face components with explicit local region and Sprite loading.'
    (ROOT / 'assets/reference/home-story-portrait-code-source-manifest.json').write_text(
        json.dumps(code, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    for binding in native['script_bindings'].values():
        original = ROOT / binding['path']
        if original.parent == staged:
            original = target / original.name
            binding['path'] = original.relative_to(ROOT).as_posix()
        if sha(original.read_bytes()) != binding['sha256'] or sha(Path(str(original) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Changed original portrait script binding: ' + str(original))
    installed = install_staged_assets(work, work / 'home-story-portrait-assets', native['outputs'],
        ROOT / 'unity/Assets/Official/Home/StoryPortraitUI', 'home-story-portrait')
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeUI/HomeStoryPortraitAssets.cs'
    script_meta = metadata(script, 'MonoImporter')
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/HomeStoryPortraitAssets.asset'
    rows = [dict(Guid=key, Sprite=value) for key, value in sorted(native['sprite_proxy_bindings'].items())]
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3),
        m_Name='HomeStoryPortraitAssets', m_EditorClassIdentifier='',
        Portrait8402=native['roots']['Npc_Portrait_8402'], Portrait8411=native['roots']['Npc_Portrait_8411'], Sprites=rows))
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.dump(data, Dumper=Dumper, sort_keys=False)).encode('utf8')
    if catalog.exists() and catalog.read_bytes() != raw:
        raise ValueError('Preserve existing story portrait catalog: ' + str(catalog))
    catalog.write_bytes(raw)
    catalog_meta = metadata(catalog, 'NativeFormatImporter')
    for path in (script, script_meta, catalog, catalog_meta):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    native['installed_output_sha256'] = installed
    native['installation_script_sha256'] = sha(Path(__file__).read_bytes())
    (ROOT / 'assets/reference/home-story-portrait-native-source-manifest.json').write_text(
        json.dumps(native, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original story portrait prefabs and Sprite catalog.', flush=True)


if __name__ == '__main__':
    main()
