"""Bind the selected original UI material configuration to verified native assets."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas, fields_tree
from UnityPy.classes import PPtr
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def main():
    apk = Apk.current()
    assert apk.version == '884_156692'
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    schemas = NativeSchemas(code, unity_project=ROOT / 'unity')
    obj = apk.asset('scriptableconfig_modules_appearance', 'AppearanceAssetManagerConfig.asset')
    fields = obj.read_typetree(nodes=fields_tree(schemas.default,
        [('UIDyeShader', 'PPtr<Shader>'), ('DefaultSkeletonMaterial', 'PPtr<Material>')]))
    panel_path = ROOT / 'assets/reference/partner-panel-native-source-manifest.json'
    panel = json.loads(panel_path.read_bytes())
    old_path = ROOT / 'assets/reference/profession-ui/weapon-texture-loader-source-manifest.json'
    old = json.loads(old_path.read_bytes())
    dependencies, bindings = [], {}
    for field in ('UIDyeShader', 'DefaultSkeletonMaterial'):
        original = PPtr(**fields[field], assetsfile=obj.assets_file).deref()
        digest = hashlib.sha256(original.get_raw_data()).hexdigest()
        if field == 'UIDyeShader':
            matches = [r for r in panel['objects'].values() if r.get('original_sha256') == digest]
            row, = matches
            path = ROOT / 'unity/Assets/Official/Partners/UI/Panel' / row['output']
            assert identity(path)['sha256'] == panel['installed_output_sha256'][path.relative_to(ROOT).as_posix()]
            file_id = 4800000
        else:
            # Reuse the actual material only after checking its complete raw
            # source payload; the UI shader above is a different current object.
            row, = [r for r in old['materials'] if r['source']['path_id'] == str(original.path_id)]
            assert any(r.get('payload_sha256') == digest and r['source'] == row['source'] for r in old['sources'])
            path = ROOT / row['output']['path']
            assert identity(path) == row['output']
            file_id = 2100000
        guid = re.search(r'(?m)^guid: ([0-9a-f]{32})', Path(str(path) + '.meta').read_text('utf8'))[1]
        bindings[field] = '{fileID: ' + str(file_id) + ', guid: ' + guid + ', type: 3}'
        dependencies.append(dict(field=field, source=apk.provenance(original), original_sha256=digest,
            native=identity(path), meta=identity(Path(str(path) + '.meta'))))
    target = ROOT / 'unity/Assets/Official/Partners/Appearance/Resources/Partners/appearance-asset-manager-config.asset'
    for folder in reversed([target.parent, *target.parent.parents]):
        if folder == ROOT / 'unity/Assets/Official/Partners' or ROOT / 'unity/Assets/Official/Partners' in folder.parents:
            folder.mkdir(exist_ok=True)
            if not Path(str(folder) + '.meta').exists():
                guid = hashlib.md5(('zjcs:partner-ui-appearance:' + folder.relative_to(ROOT).as_posix()).encode()).hexdigest()
                write_changed(Path(str(folder) + '.meta'), 'fileFormatVersion: 2\nguid: ' + guid + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n')
    source = ROOT / 'unity/Assets/Official/ProfessionUI/WeaponTextureLoader/appearance-asset-manager-config.asset'
    text = source.read_text('utf8')
    for field, binding in bindings.items():
        text, count = re.subn(r'(?m)^  ' + field + r': .*$', '  ' + field + ': ' + binding, text)
        assert count == 1
    write_changed(target, text)
    meta(target)
    target_meta = Path(str(target) + '.meta')
    target_guid = re.search(r'(?m)^guid: ([0-9a-f]{32})', target_meta.read_text('utf8'))[1]
    write_changed(target_meta, 'fileFormatVersion: 2\nguid: ' + target_guid
        + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    receipt = dict(manifest=apk.version, source=apk.provenance(obj),
        original_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), original_fields=fields, dependencies=dependencies,
        input_receipts=[identity(panel_path), identity(old_path)],
        outputs=[identity(target), identity(target_meta)] + [identity(ROOT / path) for path in (
            'unity/Assets/Official/Partners/Appearance/Resources/Partners.meta',
            'unity/Assets/Official/Partners/Appearance/Resources.meta',
            'unity/Assets/Official/Partners/Appearance.meta')], generator=identity(Path(__file__)),
        operation='Preserve both original configuration fields. Bind the selected UI dye shader recovered from the APK, and reuse the complete raw-byte-identical default skeleton material. Historical UI dye shader is not substituted.')
    write_changed(ROOT / 'assets/reference/partner-ui-appearance-assets-source-manifest.json',
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
    print('Bound current original UI appearance material configuration.', flush=True)


if __name__ == '__main__':
    main()
