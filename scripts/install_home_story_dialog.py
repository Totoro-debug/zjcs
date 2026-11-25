"""Install the complete original story dialogue and its asset catalog."""
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
    code = json.loads((work / 'home-story-dialog-code-source-manifest.json').read_bytes())
    native = json.loads((work / 'home-story-dialog-native-source-manifest.json').read_bytes())
    mask = json.loads((work / 'home-story-mask-native-source-manifest.json').read_bytes())
    if code['manifest'] != native['manifest'] or mask['manifest'] != native['manifest'] or code['manifest'] != '884_156692':
        raise ValueError('The dialogue sources must use the selected official version.')
    for receipt in (code, native, mask):
        for path, expected in receipt['inputs'].items():
            if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Changed dialogue input: ' + path)
    if sha((ROOT / 'scripts/prepare_home_story_dialog_code.py').read_bytes()) != code['generator_sha256']:
        raise ValueError('The dialogue code generator changed after staging.')
    staged = work / 'home-story-dialog-code'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/HomeStoryDialog'
    for name, expected in code['outputs'].items():
        source, output = staged / name, target / name
        if sha(source.read_bytes()) != expected: raise ValueError('Changed staged dialogue controller: ' + name)
        if output.exists() and output.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing dialogue controller: ' + str(output))
    target.mkdir(parents=True, exist_ok=True)
    folder_meta = metadata(target, 'DefaultImporter', folder=True)
    installed_code = {folder_meta.relative_to(ROOT).as_posix(): sha(folder_meta.read_bytes())}
    for name in code['outputs']:
        output = target / name
        output.write_bytes((staged / name).read_bytes())
        installed_code[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    code['installed_output_sha256'] = installed_code
    code['scope'] = 'Original dialogue panel and execution order with explicit local service ports.'
    (ROOT / 'assets/reference/home-story-dialog-code-source-manifest.json').write_text(
        json.dumps(code, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    option_code = work / 'home-story-option-code'
    for binding in native['script_bindings'].values():
        original = ROOT / binding['path']
        if original.parent == staged: original = target / original.name
        elif original.parent == option_code: original = ROOT / 'unity/Assets/Game/Runtime/Official/HomeStoryOptions' / original.name
        binding['path'] = original.relative_to(ROOT).as_posix()
        if sha(original.read_bytes()) != binding['sha256'] or sha(Path(str(original) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Changed original dialogue script binding: ' + str(original))
    installed = install_staged_assets(work, work / 'home-story-dialog-assets', native['outputs'],
        ROOT / 'unity/Assets/Official/Home/StoryDialogUI', 'home-story-dialog')
    for binding in mask['script_bindings'].values():
        original = ROOT / binding['path']
        if sha(original.read_bytes()) != binding['sha256'] or sha(Path(str(original) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Changed original invisible mask script binding: ' + str(original))
    installed_mask = install_staged_assets(work, work / 'home-story-mask-assets', mask['outputs'],
        ROOT / 'unity/Assets/Official/Home/StoryDialogUI', 'home-story-mask')
    installed.update(installed_mask)
    mask['installed_output_sha256'] = installed_mask
    (ROOT / 'assets/reference/home-story-mask-native-source-manifest.json').write_text(
        json.dumps(mask, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeStory/HomeStoryDialogAssets.cs'
    script_meta = metadata(script, 'MonoImporter')
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/HomeStoryDialogAssets.asset'
    story = ROOT / 'unity/Assets/Official/Home/Source/furniture-unlock-stories.json'
    story_meta = Path(str(story) + '.meta')
    story_receipt = ROOT / 'assets/reference/home-furniture-unlock-story-source-manifest.json'
    audio_receipt = ROOT / 'assets/reference/home-furniture-unlock-story-audio-source-manifest.json'
    story_proof = json.loads(story_receipt.read_bytes())
    audio_proof = json.loads(audio_receipt.read_bytes())
    if story_proof['manifest'] != code['manifest'] or audio_proof['manifest'] != code['manifest'] \
            or sha(story.read_bytes()) != story_proof['output']['sha256']:
        raise ValueError('Original furniture story data differs from its receipt.')
    if audio_proof['inputs'][story.relative_to(ROOT).as_posix()] != sha(story.read_bytes()):
        raise ValueError('Original furniture story audio refers to another story source.')
    outputs_by_name = {Path(row['path']).name: row for row in audio_proof['outputs']}
    voices = []
    for row in audio_proof['entries']:
        record = outputs_by_name[str(row['media_id']) + '.wav']
        asset = ROOT / record['path']
        asset_meta = Path(str(asset) + '.meta')
        if sha(asset.read_bytes()) != row['wav_sha256'] or sha(asset_meta.read_bytes()) != record['meta_sha256']:
            raise ValueError('Original furniture story voice changed: ' + row['eventKey'])
        voices.append(dict(EventKey=row['eventKey'], Clip=dict(fileID=8300000, guid=guid(asset_meta), type=3)))
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3),
        m_Name='HomeStoryDialogAssets', m_EditorClassIdentifier='', OptionTalkDialog=native['root'],
        InvisibleMask=mask['root'], StorySource=dict(fileID=4900000, guid=guid(story_meta), type=3), Voices=voices))
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.dump(data, Dumper=Dumper, sort_keys=False)).encode('utf8')
    if catalog.exists() and catalog.read_bytes() != raw:
        previous_path = ROOT / 'assets/reference/home-story-dialog-native-source-manifest.json'
        if not previous_path.is_file(): raise ValueError('Preserve existing dialogue catalog: ' + str(catalog))
        previous = json.loads(previous_path.read_bytes())
        if previous.get('installed_output_sha256', {}).get(catalog.relative_to(ROOT).as_posix()) != sha(catalog.read_bytes()):
            raise ValueError('The existing dialogue catalog differs from its receipt.')
    catalog.write_bytes(raw)
    catalog_meta = metadata(catalog, 'NativeFormatImporter')
    for path in (script, script_meta, catalog, catalog_meta): installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    offline = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeStoryDialog'
    for path, importer, folder in ((offline, 'DefaultImporter', True),
            (offline / 'HomeStoryDialogServices.cs', 'MonoImporter', False),
            (offline / 'HomeStoryDialogWindow.cs', 'MonoImporter', False),
            (offline / 'HomeFurnitureStoryGraph.cs', 'MonoImporter', False),
            (offline / 'HomeFurnitureStoryDialogNode.cs', 'MonoImporter', False),
            (offline / 'Zjcs.Offline.HomeStoryDialog.asmdef', 'AssemblyDefinitionImporter', False)):
        output_meta = metadata(path, importer, folder)
        if not folder: installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        installed[output_meta.relative_to(ROOT).as_posix()] = sha(output_meta.read_bytes())
    native['installed_output_sha256'] = installed
    native['story_data_inputs'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
        for path in (story, story_meta, story_receipt, audio_receipt)}
    timing_sources = [ROOT / '.tools/source-packages/current/884_156692/official-code/framework-firstpass-source/PopupWindow.cs',
        ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs',
        ROOT / 'scripts/fixtures/home_story_dialog_validation.cs', ROOT / 'scripts/fixtures/home_furniture_host_validation.cs']
    native['popup_timing'] = dict(
        behavior='Original PlayPlayable yields once after the show or hide duration before replacing the clip.',
        sha256={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in timing_sources})
    graph_sources = [ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/RunNode.cs',
        ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/OptionDialogNode.cs',
        ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/DialogNode.cs',
        ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/OptionTalkDialogProxy.cs',
        ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/StoryGraph.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Story/StoryModule.cs',
        ROOT / 'scripts/fixtures/home_story_graph_validation.cs']
    native['graph_connections'] = dict(
        behavior='Original Exit and indexed Options connections; node actions remain the host responsibility.',
        sha256={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in graph_sources})
    native['installation_script_sha256'] = sha(Path(__file__).read_bytes())
    (ROOT / 'assets/reference/home-story-dialog-native-source-manifest.json').write_text(
        json.dumps(native, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed complete original story dialogue and explicit catalog.', flush=True)


if __name__ == '__main__':
    main()
