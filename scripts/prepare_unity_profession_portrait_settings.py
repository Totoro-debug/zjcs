"""Restore the APK portrait settings, source implementation and native fallback art."""
import hashlib
import json
from pathlib import Path
import re
import shutil

from prepare_unity_profession_native_inputs import ROOT, CACHE
from official_apk import Apk
from map13_native_schemas import NativeSchemas
from prepare_unity_profession_reward_sprites import build as restore_sprite, Dumper, fields, source_header
from UnityPy.classes import PPtr
import yaml

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
DEST = ROOT / 'unity/Assets/Official/ProfessionUI/Portrait'
BUNDLE = 'defaultpackage_assets_scriptableconfig_modules_playerportrait.bundle'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def guid(path):
    return re.search(r'(?m)^guid: ([0-9a-f]{32})$', path.with_name(path.name + '.meta').read_text(encoding='utf-8'))[1]


def build(unity_project=None):
    project = (unity_project or ROOT / 'unity').resolve()
    if project != (ROOT / 'unity').resolve():
        from validation_workspace import require_temporary_unity_project
        require_temporary_unity_project(project)
    apk = Apk.current()
    if apk.version != '871_156598' or apk.index[BUNDLE] != 'b2bbdefda767f3b9f00117861247e457':
        raise ValueError('Unexpected original portrait settings source')
    code_manifest = CODE.parent / 'code-manifest.json'
    code_inputs = json.loads(code_manifest.read_bytes())
    if code_inputs['manifest'] != apk.version:
        raise ValueError('Portrait asset and code versions differ')
    for name, entry in code_inputs['assemblies'].items():
        if record(CODE / 'managed/decoded' / name)['sha256'] != entry['decoded_sha256']:
            raise ValueError('Original portrait schema assembly changed: ' + name)
    source = apk.asset(BUNDLE, '/PlayerPortraitSettings.asset')
    schemas = NativeSchemas(CODE, unity_project=project)
    original, tree = schemas.read(source)
    ports = []
    for name, relative in [('PlayerPortraitSettings', 'client-source/PlayerPortraitSettings.cs'),
                           ('MonoDictionary', 'framework-firstpass-source/MonoDictionary.cs'),
                           ('MonoKeyValuePair', 'framework-firstpass-source/MonoKeyValuePair.cs'),
                           ('SkinType', 'common-source/Common/SkinType.cs')]:
        path = CODE / relative
        text = path.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Common|Framework[^;]*);\n', '', text, flags=re.M)
        text = text.replace('namespace Common;\n', '')
        text = text.replace('[ScriptableObjectConfig]\n', '')
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        if name == 'MonoDictionary':
            # ILSpy emits ref for the .NET Remove(TKey, out TValue) overload.
            text = text.replace('mSerialized.Remove(key, ref ', 'mSerialized.Remove(key, out ')
        if name == 'PlayerPortraitSettings':
            text, count = re.subn(r'\tpublic enum EPortraitType\n\t\{.*?\n\t\}\n', '', text, flags=re.S)
            assert count == 1
            text = re.sub(r'\bEPortraitType\b', 'ProfessionPortraitKind', text)
            text, count = re.subn(r'\t\tGLoggerErrorInterpolatedStringHandler message = .*?\n\t\tLog.Error\([^\n]+;',
                '\t\tDebug.LogError("请求错误照片配置，不存在的类型：" + type);', text, flags=re.S)
            assert count == 1
        if any(value in text for value in ('GLogger', 'Log.Error', 'ScriptableObjectConfig', 'namespace Common')):
            raise ValueError('Unresolved original portrait settings dependency: ' + name)
        text = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)
        target = RUNTIME / (name + '.cs')
        target.write_text('namespace Zjcs.Official.ProfessionUI\n{\n' + text + '\n}\n', encoding='utf-8', newline='\n')
        script_guid = hashlib.md5(('zjcs-profession-ui-' + name).encode()).hexdigest()
        if name == 'PlayerPortraitSettings':
            exported = CACHE / 'typed/export/ExportedProject/Assets/Scripts/Assembly-CSharp/PlayerPortraitSettings.cs'
            script_guid = guid(exported)
        meta = target.with_name(target.name + '.meta')
        meta.write_text('fileFormatVersion: 2\nguid: ' + script_guid + '\n', encoding='utf-8', newline='\n')
        ports.append(dict(source=record(path), runtime=record(target), meta=record(meta)))
    DEST.mkdir(exist_ok=True)
    folder_meta = DEST.with_name(DEST.name + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:profession:portrait-assets').hexdigest() + '\nfolderAsset: yes\n', encoding='utf-8', newline='\n')
    native = source_header()
    original_fields = json.loads(json.dumps(original))
    bindings, sprites, outputs = {}, [], []
    for name in ('mDefaultPortrait', 'mLoadingPortrait', 'mDisablePortrait'):
        obj = PPtr(**original[name], assetsfile=source.assets_file).deref()
        restored = restore_sprite(apk, obj)
        for kind in ('sprite', 'texture'):
            entry = restored[kind]
            target = DEST / Path(entry['path']).name
            shutil.copyfile(ROOT / entry['path'], target)
            shutil.copyfile(ROOT / (entry['path'] + '.meta'), target.with_name(target.name + '.meta'))
            entry['installed'] = record(target)
            entry['installed_meta'] = record(target.with_name(target.name + '.meta'))
            outputs.extend([record(target), record(target.with_name(target.name + '.meta'))])
        sprites.append(dict(field=name, source=apk.provenance(obj),
            payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), **restored))
        bindings[name] = dict(fileID=21300000, guid=restored['sprite']['guid'], type=2)
        original_fields[name] = dict(m_FileID=0, m_PathID=0)
    original_fields['m_Script'] = dict(m_FileID=0, m_PathID=0)
    native.update(fields(original_fields))
    native.update(bindings)
    native['m_Script'] = dict(fileID=11500000, guid=guid(RUNTIME / 'PlayerPortraitSettings.cs'), type=3)
    native['m_EditorClassIdentifier'] = ''
    body = yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    target = DEST / 'PlayerPortraitSettings.asset'
    target.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' + body, encoding='utf-8', newline='\n')
    identity = hashlib.md5(b'zjcs:profession:871_156598:ScriptableConfig/Modules/PlayerPortrait/PlayerPortraitSettings.asset').hexdigest()
    meta = target.with_name(target.name + '.meta')
    meta.write_text('fileFormatVersion: 2\nguid: ' + identity + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n', encoding='utf-8', newline='\n')
    outputs.extend([record(target), record(meta), record(folder_meta)])
    proof = dict(format=1, manifest=apk.version, source=apk.provenance(source),
        payload_sha256=hashlib.sha256(source.get_raw_data()).hexdigest(), original_fields=original,
        ports=ports, sprites=sprites, outputs=outputs, settings=record(target),
        generator=record(Path(__file__)), sprite_converter=record(ROOT / 'scripts/prepare_unity_profession_reward_sprites.py'),
        source_code_manifest=record(code_manifest),
        schema_fields=schemas.default.records,
        conversion_tools=[record(ROOT / 'scripts' / name) for name in (
            'map13_native_schemas.py', 'official_unity_schema.py', 'prepare_unity_map13_native_scene.py')],
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitSettingsValidation.cs'),
        validation_meta=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitSettingsValidation.cs.meta'),
        operation='Copy original portrait settings, complete serializable dictionary and skin enum. Reuse original portrait-kind ordinals. Recover all four photography parameter groups and all three exact original fallback Sprites/meshes/textures directly from APK before inspecting cloud. Restore native YAML with explicit original Sprite and migrated-script bindings; decode all original ASTC mip blocks into RGBA32 without PNG cropping or recompression.',
        validation_scope="Actual isolated Unity Play Mode verifies all four original camera/texture/particle/depth groups, every skin hash weight and exclusion, native dictionary callbacks and source Remove behavior, offset adjustment and lazy don't-destroy fallback ownership. Verifies three original Sprite rects, pivots, PPU, borders, vertices, triangles, UVs and all decoded ASTC mip bytes via real GPU readback; the three captured original fallback images have been viewed. This does not validate a live character or the profession page.",
        limitations=['Native character photographer/appearance backend, manager, production save binding and full profession UI remain unbound. Fallback portraits are the original loading/disabled/default art, not replacements for live characters.'])
    (ROOT / 'assets/reference/profession-ui/portrait-settings-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    fixture = CACHE / 'typed/portrait-settings-validation.json'
    fixture.write_text(json.dumps(dict(settings='Assets/Official/ProfessionUI/Portrait/PlayerPortraitSettings.asset',
        parameters=original['mPhotographyParameters'], sprites=[dict(field=row['field'], expected=row['expected'],
            path=row['sprite']['installed']['path'].removeprefix('unity/'), texture_path=row['texture']['installed']['path'].removeprefix('unity/'),
            mips=row['original_texture']['mips']) for row in sprites]), ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recovered four original photography groups and three native fallback portraits from APK.')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-project', type=Path)
    build(parser.parse_args().unity_project)
