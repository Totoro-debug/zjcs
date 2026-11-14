"""Bind the verified selected notification to a Resources catalog and local owner."""
import hashlib
import json
import re
from pathlib import Path
from official_apk import ROOT

RUNTIME = ROOT/'unity/Assets/Game/Runtime/Offline/HomeLevelUpUI'
NATIVE = ROOT/'unity/Assets/Official/Home/UI/LevelUp'
OUTPUT = ROOT/'unity/Assets/Game/Resources/Home'
RECEIPT = ROOT/'assets/reference/home-level-up-windows-source-manifest.json'


def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def relative(path): return path.relative_to(ROOT).as_posix()
def guid(path): return re.search(r'(?m)^guid: (\w{32})', Path(str(path)+'.meta').read_text('utf8')).group(1)
def write(path, text): path.write_text(text, 'utf8', newline='\n')


def meta(path, importer):
    target = Path(str(path)+'.meta')
    if not target.exists():
        identity = hashlib.md5(('zjcs:home-level-up-windows:'+relative(path)).encode()).hexdigest()
        write(target, f'fileFormatVersion: 2\nguid: {identity}\n{importer}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    return target


def header(path, script):
    return '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+guid(script)+', type: 3}\n  m_Name: '+path.stem+'\n  m_EditorClassIdentifier:\n'


def pointer(path, identity, kind=2):
    return '{fileID: '+str(identity)+', guid: '+guid(path)+', type: '+str(kind)+'}'


def prepare():
    # The earlier conversion receipt is evidence, never rewrite it to accept a
    # changed payload. Import-time dynamic font atlas changes remain detectable.
    native_receipt = ROOT/'assets/reference/home-level-up-native-source-manifest.json'
    source = json.loads(native_receipt.read_bytes())
    for name, expected in source['installed_output_sha256'].items():
        if digest(ROOT/name) != expected: raise ValueError('Changed selected native payload: '+name)
    for row in source['reused']:
        if digest(ROOT/row['path']) != row['sha256']: raise ValueError('Changed reused native payload: '+row['path'])
    for row in source['script_bindings'].values():
        path = ROOT/row['path']
        if digest(path) != row['sha256'] or digest(Path(str(path)+'.meta')) != row['meta_sha256']:
            raise ValueError('Changed original script binding: '+row['path'])
    inputs = {relative(native_receipt): digest(native_receipt)}
    formula_receipt = ROOT/'assets/reference/home-level-up-properties-source-manifest.json'
    inputs[relative(formula_receipt)] = digest(formula_receipt)
    for path in (NATIVE/'LevelUpNotifyTip.prefab', NATIVE/'home-level-up-texts.json',
        NATIVE/'Audio/GrowUp_LevelUp.wav', ROOT/'unity/Assets/Official/ProfessionUI/Fonts/FontAssetFontConfig.json',
        ROOT/'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf'):
        inputs[relative(path)] = digest(path)
    for name in ('Framework.UI/WindowContainer.cs', 'Framework.UI/PopupWindowsFactory.cs', 'PopupWindowControlStrategyFactory.cs'):
        path = ROOT/'.tools/source-packages/current/884_156692/official-code/framework-firstpass-source'/name
        inputs[relative(path)] = digest(path)
    for path in RUNTIME.glob('*.cs'): meta(path, 'MonoImporter')
    assembly = RUNTIME/'Zjcs.Offline.HomeLevelUpUI.asmdef'
    definition = json.loads(assembly.read_bytes())
    for name in ('Zjcs.Offline.ProfessionUI', 'Zjcs.Offline.PartnersUI', 'Zjcs.Official.HomeLevelUpUI',
                 'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI', 'Unity.TextMeshPro'):
        if name not in definition['references']: definition['references'].append(name)
    write(assembly, json.dumps(definition, indent=2)+'\n')
    image_script = ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
    catalog = OUTPUT/'HomeLevelUpImages.asset'
    entries = [('Assets/Image/ItemIcon/Props/'+name, NATIVE/('Image/ItemIcon/Props/Props_Atlas/'+name+'.asset'), 21300000, 2)
        for name in ('P_Attack','P_Defence','P_MaxHp','P_Speed')]
    font = NATIVE/'TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset'
    font_config = ROOT/'unity/Assets/Official/ProfessionUI/Fonts/FontAssetFontConfig.json'
    config = json.loads(font_config.read_bytes())
    entries += [('Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json', font_config, 4900000, 3),
        (config['DefaultFontAsset'], font, 11400000, 2),
        (config['FontAssetInfoDict'][config['DefaultFontAsset']]['FontPath'], ROOT/'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf', 12800000, 3)]
    text = header(catalog, image_script)+'  Entries:\n'
    for name, path, identity, kind in entries:
        inputs[relative(path)] = digest(path)
        text += '  - Path: '+name+'\n    AssetGuid: '+guid(path)+'\n    Asset: '+pointer(path, identity, kind)+'\n'
    write(catalog, text); meta(catalog, 'NativeFormatImporter')
    prefab = NATIVE/'LevelUpNotifyTip.prefab'
    blocks = re.split(r'(?m)^--- !u!', prefab.read_text('utf8'))
    roots = [block for block in blocks if block.startswith('224 ') and re.search(r'(?m)^  m_Father: \{fileID: 0\}',block)]
    assert len(roots) == 1
    root_id = int(re.search(r'm_GameObject: \{fileID: (\d+)\}',roots[0]).group(1))
    asset = OUTPUT/'HomeLevelUpAssets.asset'
    text = header(asset, RUNTIME/'OfflineHomeLevelUpAssets.cs')
    for name, path, identity, kind in [('prefab',prefab,root_id,3),('texts',NATIVE/'home-level-up-texts.json',4900000,3),
        ('images',catalog,11400000,2),('sound',NATIVE/'Audio/GrowUp_LevelUp.wav',8300000,3)]:
        text += '  '+name+': '+pointer(path,identity,kind)+'\n'
    write(asset, text); meta(asset, 'NativeFormatImporter')
    outputs = [*RUNTIME.glob('*.cs'), *RUNTIME.glob('*.cs.meta'), assembly, catalog, Path(str(catalog)+'.meta'),asset,Path(str(asset)+'.meta')]
    scripts = [Path(__file__),ROOT/'scripts/prepare_home_level_up_windows_validation.py',ROOT/'scripts/fixtures/home_level_up_windows_validation.cs']
    inputs.update({relative(path):digest(path) for path in scripts if path.exists()})
    write(RECEIPT, json.dumps(dict(manifest='884_156692', inputs=inputs,
        output_sha256={relative(path):digest(path) for path in outputs}, bindings=[dict(path=name,asset=relative(path),guid=guid(path)) for name,path,_,_ in entries]),ensure_ascii=False,indent=2)+'\n')
    print('Bound original level-up notification: 7 image/font entries, explicit prefab/text/audio and runtime owner.',flush=True)


if __name__ == '__main__': prepare()
