"""Bind the verified native airship background sprites into a local Resources catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from validation_workspace import require_temporary_unity_project


def sha(data):
    return hashlib.sha256(data).hexdigest()


def guid(path):
    return hashlib.md5(('zjcs:home-airship-assets:' + path).encode()).hexdigest()


def prepare(project):
    receipt_path = ROOT / 'assets/reference/home-airship-native-source-manifest.json'
    receipt = json.loads(receipt_path.read_text(encoding='utf8'))
    if receipt['manifest'] != Apk.current().version:
        raise ValueError('Airship native assets do not match the current official input')
    sources, sprite_guids = {}, []
    for name in ('SmallPoster_1', 'SmallPoster_2'):
        relative = 'unity/Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/' + name + '.asset'
        for source in (relative, relative + '.meta'):
            raw = (ROOT / source).read_bytes()
            if sha(raw) != receipt['outputs'][source]:
                raise ValueError('Native sprite differs from its source receipt: ' + source)
            sources[source] = sha(raw)
        content = (ROOT / relative).read_text(encoding='utf8')
        match = re.search(r'^--- !u!213 &(\d+)\s*$', content, re.M)
        metadata = (ROOT / (relative + '.meta')).read_text(encoding='utf8')
        identity = re.search(r'^guid: ([a-f0-9]{32})\s*$', metadata, re.M)
        if not match or not identity:
            raise ValueError('Native sprite identity is unavailable: ' + relative)
        sprite_guids.append((int(match.group(1)), identity.group(1)))

    code_path = 'Assets/Game/Runtime/Offline/HomeAirship/OfflineHomeAirshipAssets.cs'
    folder_path = 'Assets/Game/Resources/Home/Airship'
    asset_path = folder_path + '/airship-assets.asset'
    code = (ROOT / 'scripts/templates/home_airship/OfflineHomeAirshipAssets.cs').read_bytes()
    code_guid = guid(code_path)
    asset = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
             '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
             '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
             f'  m_Script: {{fileID: 11500000, guid: {code_guid}, type: 3}}\n'
             '  m_Name: airship-assets\n  m_EditorClassIdentifier:\n'
             f'  mUnrepairedBackground: {{fileID: {sprite_guids[0][0]}, guid: {sprite_guids[0][1]}, type: 2}}\n'
             f'  mRepairedBackground: {{fileID: {sprite_guids[1][0]}, guid: {sprite_guids[1][1]}, type: 2}}\n').encode()

    def meta(path, importer, folder=False):
        return (f'fileFormatVersion: 2\nguid: {guid(path)}\n' +
                ('folderAsset: yes\n' if folder else '') + importer + ':\n  externalObjects: {}\n').encode()
    outputs = {code_path: code, code_path + '.meta': meta(code_path, 'MonoImporter'),
               folder_path + '.meta': meta(folder_path, 'DefaultImporter', True),
               asset_path: asset, asset_path + '.meta': meta(asset_path, 'NativeFormatImporter')}
    for relative, raw in outputs.items():
        path = project / relative
        if path.exists() and path.read_bytes() != raw:
            raise ValueError('Preserve existing catalog file: ' + str(path))
    for relative, raw in outputs.items():
        path = project / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists():
            path.write_bytes(raw)
    for source in ('scripts/templates/home_airship/OfflineHomeAirshipAssets.cs',
                   'scripts/prepare_home_airship_assets.py'):
        sources[source] = sha((ROOT / source).read_bytes())
    return {'native_manifest': receipt['manifest'], 'native_receipt_sha256': sha(receipt_path.read_bytes()),
            'inputs': sources, 'outputs': {name: sha(raw) for name, raw in outputs.items()}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    destination = parser.add_mutually_exclusive_group(required=True)
    destination.add_argument('--project', help='Owned independent Unity validation project')
    destination.add_argument('--install', action='store_true', help='Install previously validated source assets')
    args = parser.parse_args()
    project = ROOT / 'unity' if args.install else require_temporary_unity_project(Path(args.project))
    if args.install:
        output = ROOT / 'assets/reference/home-airship-asset-catalog-source-manifest.json'
        if output.exists():
            previous = json.loads(output.read_text(encoding='utf8'))
            for name, expected in {**previous['inputs'], **previous['outputs']}.items():
                path = ROOT / name
                if not path.is_file() or sha(path.read_bytes()) != expected:
                    raise ValueError('Preserve changed catalog input or output: ' + name)
            if sha((ROOT / 'assets/reference/home-airship-native-source-manifest.json').read_bytes()) != previous['native_receipt_sha256']:
                raise ValueError('Native airship source receipt has changed')
            print('Verified existing airship sprite catalog')
            return
    result = prepare(project)
    if args.install:
        result['outputs'] = {'unity/' + key: value for key, value in result['outputs'].items()}
    else:
        output = project.parent / 'airship-assets-validation-source.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Prepared airship sprite catalog in', project)


if __name__ == '__main__':
    main()
