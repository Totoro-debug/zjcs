"""Restore matching official Windows Wwise bindings, settings and battle packages."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import uuid

from convert_wwise_android_package import convert_android_package_to_windows
from map14_audio_source import AudioSource

ROOT = Path(__file__).resolve().parents[1]
SDK_ROOT = ROOT / '.tools/wwise/UnityIntegration-2022.1.18.8567.3199'
SDK_PROOF = ROOT / 'assets/reference/battle-animation/wwise-windows-integration-source-manifest.json'
INPUT = ROOT / 'assets/reference/battle-animation/native-sound-dependencies-source-manifest.json'
EVENTS = ROOT / 'assets/reference/battle-animation/native-sound-sources-source-manifest.json'
CODE = ROOT / 'unity/Assets/Game/Runtime/Official/Wwise/API/Runtime'
ART = ROOT / 'unity/Assets/Official/Combat/NativeSound'
EXPORT = ROOT / '.tools/source-packages/current/884_156692/native-conversion/native-export-all'
OUTPUT = ROOT / 'assets/reference/battle-animation/windows-sound-runtime-source-manifest.json'
FORMAT_SOURCE = ROOT / '.tools/wwise/SDK-2022.1.18.8567/format-source.json'


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=digest(raw))


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:windows-wwise:' + path.relative_to(ROOT / 'unity/Assets').as_posix()).hex


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace')
    proof = json.loads(SDK_PROOF.read_bytes())
    if proof['sdk_version'] != '2022.1.18.8567' or proof['integration_build'] != 3199:
        raise ValueError('Inspect changed original SDK version')
    sdk_files = {record['path']: record for record in proof['files']}
    outputs, inputs = {}, {}
    format_source = json.loads(FORMAT_SOURCE.read_bytes())
    if format_source['sdk_version'] != proof['sdk_version']:
        raise ValueError('Inspect changed package format source version')
    for record in format_source['files']:
        path = ROOT / record['path']
        if digest(path.read_bytes()) != record['sha256']:
            raise ValueError('Changed official package format source: ' + record['path'])

    def write(path, raw):
        if isinstance(raw, str):
            raw = raw.encode('utf-8')
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.read_bytes() != raw:
            previous = json.loads(OUTPUT.read_bytes()) if OUTPUT.exists() else {}
            previous_hashes = {item['path']: item['sha256'] for item in previous.get('outputs', [])}
            key = path.relative_to(ROOT).as_posix()
            if digest(path.read_bytes()) != previous_hashes.get(key):
                raise ValueError('Preserve unrecognized existing output: ' + key)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[path.relative_to(ROOT).as_posix()] = identity(path)

    def meta(path, importer=None):
        text = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n'
        if importer:
            text += importer
        write(Path(str(path) + '.meta'), text)

    def sdk(path):
        record = sdk_files[path.relative_to(ROOT).as_posix()]
        raw = path.read_bytes()
        if len(raw) != record['size'] or digest(raw) != record['sha256']:
            raise ValueError('Changed installed official SDK input: ' + str(path))
        inputs[record['path']] = identity(path)
        return raw

    api = SDK_ROOT / 'Wwise/API/Runtime'
    for path in sorted(api.rglob('*')):
        relative = path.relative_to(api)
        # Existing game ports own Wwise event/reference serialization. Native
        # calls use the official core API; WAAPI authoring tools are unnecessary.
        if not path.is_file() or path.suffix not in ('.cs', '.asmdef') or any(part in ('WAAPI', 'WwiseTypes') for part in relative.parts):
            continue
        raw = sdk(path)
        if relative.as_posix() == 'Handwritten/Common/AkCommonPlatformSettings.cs':
            old = b'AkUtilities.GetPathInPackage(@"Runtime\\Plugins\\Windows\\x86_64\\DSP")'
            new = b'"Assets/Official/Combat/NativeSound/Plugins/Windows/x86_64/DSP"'
            if raw.count(old) != 1:
                raise ValueError('Inspect changed official Editor DSP lookup')
            raw = raw.replace(old, new)
        # These SDK getters return borrowed pointers to AkVector64 fields, not
        # newly allocated return objects. Mono's class-return marshaller frees
        # those interior addresses. Copy their values without taking ownership.
        borrowed = {
            'Generated/Common/AkWorldTransform.cs': 'CSharp_AkWorldTransform_Position',
            'Generated/Common/AkImageSourceParams.cs': 'CSharp_AkImageSourceParams_sourcePosition_get',
            'Generated/Common/AkReflectionPathInfo.cs': 'CSharp_AkReflectionPathInfo_imageSource_get',
            'Generated/Common/AkDiffractionPathInfo.cs': 'CSharp_AkDiffractionPathInfo_emitterPos_get',
        }
        name = relative.as_posix()
        if name in borrowed:
            call = b'AkSoundEnginePINVOKE.' + borrowed[name].encode() + b'(swigCPtr)'
            old = b'return ' + call + b';'
            new = b'return global::System.Runtime.InteropServices.Marshal.PtrToStructure<AkVector64>(' + call + b');'
            if raw.count(old) != 1:
                raise ValueError('Inspect changed original borrowed-vector getter: ' + name)
            raw = raw.replace(old, new)
        if name == 'Generated/Windows/AkSoundEnginePINVOKE_Windows.cs':
            for function in borrowed.values():
                old = b'public static extern AkVector64 ' + function.encode() + b'('
                new = b'public static extern global::System.IntPtr ' + function.encode() + b'('
                if raw.count(old) != 1:
                    raise ValueError('Inspect changed original borrowed-vector import: ' + function)
                raw = raw.replace(old, new)
            old = b'public static extern AkVector64 CSharp_ConvertAkVectorToAkVector64('
            new = b'public static extern AkVector64.NativeValue CSharp_ConvertAkVectorToAkVector64('
            if raw.count(old) != 1:
                raise ValueError('Inspect changed original vector value import')
            raw = raw.replace(old, new)
        if name == 'Handwritten/Common/AkVector64.cs':
            old = b'public class AkVector64\r\n{\r\n'
            new = old + b'''\t// Windows x64 returns the conversion's three doubles by value.\r
\t// Keep the public reference type used by the original pointer parameters.\r
\t[StructLayout(LayoutKind.Sequential)]\r
\tpublic struct NativeValue\r
\t{\r
\t\tpublic double X, Y, Z;\r
\t\tpublic static implicit operator AkVector64(NativeValue value)\r
\t\t\t=> new AkVector64 { X = value.X, Y = value.Y, Z = value.Z };\r
\t}\r
\r
'''
            if raw.count(old) != 1:
                raise ValueError('Inspect changed original vector reference layout')
            raw = raw.replace(old, new)
        target = CODE / relative
        write(target, raw)
        meta(target)

    def plugin(editor, player):
        return '''PluginImporter:
  serializedVersion: 2
  externalObjects: {}
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: EDITOR
      settings:
        CPU: x86_64
        OS: Windows
        DefaultValueInitialized: true
  - first:
      Standalone: Win64
    second:
      enabled: PLAYER
      settings:
        CPU: x86_64
  userData:
  assetBundleName:
  assetBundleVariant:
'''.replace('EDITOR', str(int(editor))).replace('PLAYER', str(int(player)))

    native = api / 'Plugins/Windows/x86_64'
    native_files = [(native / 'Profile/AkSoundEngine.dll', True, False),
                    (native / 'Release/AkSoundEngine.dll', False, True)]
    native_files += [(path, True, True) for path in sorted((native / 'DSP').glob('*.dll'))]
    for path, editor, player in native_files:
        target = ART / 'Plugins/Windows/x86_64' / path.relative_to(native)
        write(target, sdk(path))
        meta(target, plugin(editor, player))

    export_proof = EXPORT.with_name(EXPORT.name + '-source-manifest.json')
    export = json.loads(export_proof.read_bytes())
    expected_exports = {record['path']: record for record in export['outputs']}
    if export['manifest'] != '884_156692':
        raise ValueError('Inspect changed original settings export')

    def original(name):
        path = EXPORT / 'ExportedProject/Assets/MonoBehaviour' / name
        record = expected_exports[path.relative_to(EXPORT).as_posix()]
        raw = path.read_bytes()
        if digest(raw) != record['sha256']:
            raise ValueError('Changed original settings conversion: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = identity(path)
        return raw.decode('utf-8-sig')

    settings = ART / 'Resources/Combat/Wwise'
    script = CODE / 'Handwritten/Common/AkWwiseInitializationSettings.cs'
    windows_script = CODE / 'Handwritten/Windows/AkWindowsSettings.cs'
    global_text = original('AkWwiseInitializationSettings.asset')
    global_text = re.sub(r'  m_Script: .*', '  m_Script: {fileID: 11500000, guid: ' + guid(script) + ', type: 3}', global_text)
    global_text, count = re.subn(r'  PlatformSettingsNameList:.*?  InvalidReferencePlatforms:',
        '  PlatformSettingsNameList:\n  - Windows\n  PlatformSettingsList:\n  - {fileID: 11400000, guid: ' + guid(settings / 'Windows.asset') + ', type: 2}\n  InvalidReferencePlatforms:', global_text, flags=re.S)
    if count != 1:
        raise ValueError('Inspect changed original platform list')
    write(settings / 'AkWwiseInitializationSettings.asset', global_text)
    meta(settings / 'AkWwiseInitializationSettings.asset')
    windows = original('Windows.asset')
    windows = re.sub(r'  m_Script: .*', '  m_Script: {fileID: 11500000, guid: ' + guid(windows_script) + ', type: 3}', windows)
    write(settings / 'Windows.asset', windows)
    meta(settings / 'Windows.asset')

    dependency = json.loads(INPUT.read_bytes())
    source = json.loads(EVENTS.read_bytes())
    bank_names = {bank.lower(): bank for event in source['events'] for bank in event['banks']}
    bank_names['minimum'] = 'Minimum'
    package_records, catalog = [], []
    with AudioSource(expected_version='884_156692', work_directory=work) as audio:
        for name, receipt in sorted(dependency['packages'].items()):
            if not name.endswith('_pck.rawfile'):
                continue
            raw = audio.package(name)
            if digest(raw) != receipt['sha256']:
                raise ValueError('Changed original sound package: ' + name)
            stem = name.removeprefix('wwisebnk_assets_wwisedata_android_').removesuffix('_pck.rawfile')
            language = 'zh_CN' if stem.startswith('zh_cn_') else ''
            bank = bank_names[stem.removeprefix('zh_cn_')]
            converted = convert_android_package_to_windows(raw)
            filename = bank + '.pck'
            target = ART / 'Packages' / filename
            write(target, converted)
            meta(target)
            package_records.append(dict(original=name, source=audio.receipts()[name],
                windows=identity(target), header_growth=len(converted) - len(raw)))
            catalog.append(dict(file=filename, bank=bank, language=language))
    runtime_catalog = dict(manifest='884_156692', sdk_version='2022.1.18.8567',
        packages=catalog, events=[dict(id=e['id'], name=e['name']) for e in source['events']])
    target = settings / 'catalog.json'
    write(target, json.dumps(runtime_catalog, ensure_ascii=False, indent=2) + '\n')
    meta(target)

    # Explicitly own every new folder's Unity identity as well as leaf assets.
    for leaf in list(outputs):
        path = ROOT / leaf
        if path.suffix == '.meta':
            continue
        for directory in path.parents:
            if directory == ROOT / 'unity/Assets':
                break
            if directory in (CODE.parents[2], ART.parent):
                break
            folder_meta = Path(str(directory) + '.meta')
            if not folder_meta.exists():
                meta(directory, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
            elif 'guid: ' + guid(directory) in folder_meta.read_text(encoding='utf-8'):
                outputs[folder_meta.relative_to(ROOT).as_posix()] = identity(folder_meta)
    receipt = dict(manifest='884_156692', sdk_source=identity(SDK_PROOF), package_format_source=format_source,
        event_source=identity(EVENTS), dependency_source=identity(INPUT), settings_export=identity(export_proof),
        inputs=list(inputs.values()), packages=package_records,
        tools=[identity(Path(__file__)), identity(ROOT / 'scripts/convert_wwise_android_package.py')],
        outputs=list(outputs.values()), adaptations=[
            'SDK Editor DSP lookup points at the explicitly bound official native payload directory.',
            'Four original AkVector64 getters copy borrowed native fields without freeing their addresses; the vector conversion uses a three-double value return to match the Windows x64 ABI. Public sound values and behavior are unchanged.',
            'Original global and Windows settings retain all serialized values; only script references and the Windows-only platform list are rebound.',
            'AKPK language strings use Windows UTF-16LE; package IDs, file IDs, language IDs, alignment and every bank/media payload byte are preserved.'])
    OUTPUT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(packages=len(catalog), outputs=len(outputs), sdk_inputs=len(inputs))))


if __name__ == '__main__':
    main()
