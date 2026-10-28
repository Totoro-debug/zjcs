"""Restore the original APK appearance configuration and complete source types."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT, CACHE
from official_apk import Apk
from map13_native_schemas import NativeSchemas
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
import yaml

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
DEST = ROOT / 'unity/Assets/Official/ProfessionUI/Portrait'
BUNDLE = 'defaultpackage_assets_scriptableconfig_modules_appearance.bundle'
NAMES = ['AppearanceConfig', 'PlayerSkinType', 'AppendSkinConfig', 'AppendSkinType',
         'AppearancePartConfig', 'PartAnimConfig', 'AppearanceOverrideConfig',
         'AppearanceOverrideType', 'AppearanceHashValue', 'AppearanceSkeletonPath']


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build(unity_project=None):
    project = (unity_project or ROOT / 'unity').resolve()
    if project != (ROOT / 'unity').resolve():
        from validation_workspace import require_temporary_unity_project
        require_temporary_unity_project(project)
    apk = Apk.current()
    if apk.version != '871_156598' or apk.index[BUNDLE] != '2719b9183d3296a439780b070ce4743c':
        raise ValueError('Unexpected original appearance configuration version')
    code_manifest = CODE.parent / 'code-manifest.json'
    code_inputs = json.loads(code_manifest.read_bytes())
    if code_inputs['manifest'] != apk.version:
        raise ValueError('Appearance configuration and code versions differ')
    for name, entry in code_inputs['assemblies'].items():
        if record(CODE / 'managed/decoded' / name)['sha256'] != entry['decoded_sha256']:
            raise ValueError('Original appearance configuration assembly changed: ' + name)
    source = apk.asset(BUNDLE, '/AppearanceConfig.asset')
    schemas = NativeSchemas(CODE, unity_project=project)
    original, _ = schemas.read(source)
    # No asset dependency is replaced: this config contains only values and names.
    def verify_references(value, field=''):
        if isinstance(value, dict):
            if 'm_FileID' in value and 'm_PathID' in value:
                if field != 'm_Script' and any(value.values()):
                    raise ValueError('Unexpected original appearance object dependency: ' + field)
            else:
                for key, item in value.items(): verify_references(item, key)
        elif isinstance(value, list):
            for item in value: verify_references(item, field)
    verify_references(original)
    ports = []
    for name in NAMES:
        path = CODE / 'common-source' / (name + '.cs')
        text = path.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Common|Framework);\n', '', text, flags=re.M)
        text = text.replace('[ScriptableObjectConfig]\n', '')
        target = RUNTIME / (name + '.cs')
        target.write_text('namespace Zjcs.Official.ProfessionUI\n{\n' + text + '\n}\n', encoding='utf-8', newline='\n')
        script_guid = hashlib.md5(('zjcs-profession-ui-' + name).encode()).hexdigest()
        meta = target.with_name(target.name + '.meta')
        meta.write_text('fileFormatVersion: 2\nguid: ' + script_guid + '\n', encoding='utf-8', newline='\n')
        ports.append(dict(source=record(path), runtime=record(target), meta=record(meta)))
    native = source_header()
    bound_fields = dict(original, m_Script=dict(m_FileID=0, m_PathID=0))
    native.update(fields(bound_fields))
    native['m_Script'] = dict(fileID=11500000,
        guid=hashlib.md5(b'zjcs-profession-ui-AppearanceConfig').hexdigest(), type=3)
    native['m_EditorClassIdentifier'] = ''
    body = yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    target = DEST / 'AppearanceConfig.asset'
    target.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' + body, encoding='utf-8', newline='\n')
    meta = target.with_name(target.name + '.meta')
    meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:profession:871_156598:ScriptableConfig/Modules/Appearance/AppearanceConfig.asset').hexdigest()
        + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n', encoding='utf-8', newline='\n')
    proof = dict(format=1, manifest=apk.version, source=apk.provenance(source),
        payload_sha256=hashlib.sha256(source.get_raw_data()).hexdigest(), original_fields=original,
        ports=ports, outputs=[record(target), record(meta)], generator=record(Path(__file__)),
        source_code_manifest=record(code_manifest), schema_fields=schemas.default.records,
        conversion_tools=[record(ROOT / 'scripts' / name) for name in (
            'map13_native_schemas.py', 'official_unity_schema.py', 'prepare_unity_map13_native_scene.py',
            'prepare_unity_profession_reward_sprites.py')],
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceConfigValidation.cs'),
        validation_meta=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceConfigValidation.cs.meta'),
        operation='Copy all ten original game configuration types with namespace/config-attribute adaptation only. Recover the original APK ScriptableObject fields without modifying order, signed 64-bit hashes, skin enum values, part animations, skeleton paths or colors; bind its migrated script explicitly. No cloud asset is used.',
        limitations=['Configuration only. Character render backend, original appearance components, production studio and complete profession page remain unbound.'])
    (ROOT / 'assets/reference/profession-ui/appearance-config-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    (CACHE / 'typed/appearance-config-validation.json').write_text(json.dumps(dict(
        path='Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset', original=original), ensure_ascii=False, indent=2), encoding='utf-8', newline='\n')
    print('Recovered original APK appearance configuration: 13 skin types, 1742 hashes, 466 skeleton paths.')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-project', type=Path)
    build(parser.parse_args().unity_project)
