"""Bind the complete recovered HomeUI to its packaged frame, fonts and sound."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

import yaml

from official_apk import ROOT
from prepare_current_partner_native import documents
from prepare_partner_card_native import Dumper, guid, sha
from validation_workspace import validation_directory


def build():
    inputs = {}

    def read(relative):
        path = ROOT / relative
        inputs[relative] = sha(path.read_bytes())
        return path.read_bytes()

    native = json.loads(read('assets/reference/home-hud-native-source-manifest.json'))
    frame = json.loads(read('assets/reference/home-hud-frame-native-source-manifest.json'))
    info = json.loads(read('assets/reference/home-player-info-native-source-manifest.json'))
    assert {native['manifest'], frame['manifest'], info['manifest']} == {'884_156692'}
    for receipt in (native, frame):
        for name, expected in {**receipt['installed_output_sha256'], **receipt['reused_output_sha256']}.items():
            assert sha(read(name)) == expected, name
    legacy = documents(read('unity/Assets/Game/Resources/Home/OfficialHomePlayerInfoAssetCatalog.asset').decode('utf8'))[11400000]['MonoBehaviour']
    name = documents(read('unity/Assets/Official/Home/UI/PlayerInfo/HomeNameText.prefab').decode('utf8'))
    default_font, = [doc['MonoBehaviour']['m_fontAsset'] for doc in name.values()
                    if 'm_fontAsset' in doc.get('MonoBehaviour', {})]
    fonts = []
    for row in legacy['fontEntries']:
        source = dict(fileID=0)
        if row['FontPath'] == legacy['sourceFontPath']:
            source = legacy['sourceFont']
            assert {k: row[k] for k in ('FontPath', 'RawFontPath', 'Hash', 'Length')} == info['font']['source_font_definition']
        elif row['FontPath'] == native['source_font']['definition']['FontPath']:
            source = native['source_font']['pointer']
            assert {k: row[k] for k in ('FontPath', 'RawFontPath', 'Hash', 'Length')} == native['source_font']['definition']
        fonts.append(dict(row, source=source))
    # Preserve all five original config rows. Only the two source fonts consumed
    # by the retained CN HomeUI/task prefabs are addressed by this catalog.
    assert sum(bool(row['source']['fileID']) for row in fonts) == 2
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeHudAssets.cs'
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(Path(str(script)+'.meta')), type=3),
        m_Name='OfflineHomeHudAssets', m_EditorClassIdentifier='', framePrefab=frame['root'],
        homePrefab=native['roots']['HomeHud'],
        texts=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-hud-texts').hexdigest(), type=3),
        originalFontConfig=legacy['originalFontConfig'], defaultFontAsset=legacy['defaultFontAsset'],
        defaultFont=default_font, fonts=fonts,
        sounds=[dict(eventId=row['event']['id'], clip=row['pointer']) for row in native['audio'].values()])
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/OfflineHomeHudAssets.asset'
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
           + yaml.dump(dict(MonoBehaviour=fields), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf8')
    catalog.write_bytes(raw)
    meta = Path(str(catalog)+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-hud-local-catalog').hexdigest()
            +'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n', 'utf8', newline='\n')
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeHudUI'
    outputs = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
               for path in (Path(str(module)+'.meta'), *sorted(module.iterdir()), catalog, meta) if path.is_file()}
    for name in ('unity/Assets/Game/Runtime/Official/ProfessionUI/FontAssetFontService.cs',
                 'unity/Assets/Game/Runtime/Official/HomePlayerInfoUI/OfficialHomePlayerInfoAssetCatalog.cs'):
        read(name)
    for relative in ('assets/reference/home-condition-notifications-source-manifest.json',
                     'assets/reference/home-condition-source-manifest.json',
                     'assets/reference/home-rules-current-source-manifest.json',
                     '.tools/source-packages/current/884_156692/official-code/common-source/Common/FunctionLimitInfoParser.cs',
                     '.tools/source-packages/current/884_156692/official-code/common-source/Common/StoryStatus.cs',
                     'unity/Assets/Game/Resources/Home/OfflineHomeCurrencyAssets.asset'):
        read(relative)
    notifications = json.loads(read('assets/reference/home-condition-notifications-source-manifest.json'))
    assert notifications['manifest'] == '884_156692'
    for relative, expected in notifications['outputs'].items():
        assert sha(read(relative)) == expected, relative
    pointer = json.loads(read('assets/reference/home-pointer-input-source-manifest.json'))
    assert pointer['manifest'] == '884_156692'
    for relative, expected in pointer['outputs'].items():
        assert sha(read(relative)) == expected, relative
    currency = json.loads(read('assets/reference/home-currency-windows-native-source-manifest.json'))
    settings_path = 'unity/Assets/Official/Home/UI/CurrencyWindows/home-currency-windows-rules.json'
    settings_raw = read(settings_path)
    assert sha(settings_raw) == currency['installed_output_sha256'][settings_path]
    assert json.loads(settings_raw)['manifest'] == '884_156692'
    for fixture in ('home_hud_bindings', 'home_hud_infrastructure', 'home_task_ui', 'home_social_ui', 'home_result_ui'):
        read('scripts/fixtures/'+fixture+'_native_validation.cs')
        if fixture != 'home_hud_bindings':
            read('scripts/prepare_'+fixture+'_validation.py')
            read('assets/reference/'+fixture.replace('_','-')+'-native-source-manifest.json')
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__).read_bytes()),
        inputs=inputs, outputs=outputs, dependency_outputs={**notifications['outputs'], **pointer['outputs']},
        scope='Packaged full HomeUI resource catalog and typed dependency/lifetime binding. Production profile service implementations and full runtime flow validation remain required.')
    (ROOT/'assets/reference/home-hud-binding-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    return receipt


def stage(project, receipt):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    for relative, expected in {**receipt['outputs'], **receipt['dependency_outputs']}.items():
        source = ROOT / relative
        assert sha(source.read_bytes()) == expected, relative
        target = project / source.relative_to(ROOT/'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    from prepare_home_hud_infrastructure_validation import stage_fixture as infrastructure
    from prepare_home_task_ui_validation import stage_fixture as tasks
    from prepare_home_social_ui_validation import stage_fixture as social
    from prepare_home_result_ui_validation import stage_fixture as results
    for render in (infrastructure, tasks, social, results):
        render(ROOT/'assets/reference', project)
    folder = project/'Assets/HomeHudBindingsNativeValidation'
    folder.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_hud_bindings_native_validation.cs', folder/'HomeHudBindingsNativeValidation.cs')
    references = json.loads((ROOT/'unity/Assets/Game/Runtime/Offline/HomeHudUI/Zjcs.Offline.HomeHudUI.asmdef').read_text('utf8'))['references']
    references += ['Zjcs.Offline.HomeHudUI', 'Zjcs.Official.HomeFunctionMenuUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.World.NativeCamera', 'Zjcs.Official.World.NativeLabelUI', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.State', 'Zjcs.Offline.HomeCurrencyUI', 'Unity.Newtonsoft.Json']
    (folder/'HomeHudBindingsNativeValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeHudBindingsNativeValidation', includePlatforms=['Editor'], references=references), indent=2)+'\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    receipt = build()
    if args.project: stage(args.project, receipt)
    print('Bound original HomeUI resources and local dependency owner:', len(receipt['outputs']), 'outputs', flush=True)
