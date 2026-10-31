"""Stage current official friendship property prefabs with their native dependencies."""
import hashlib
import json
import copy
from pathlib import Path

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, script_id, sha, language
from validation_workspace import validation_directory


class PartnerGiftBinding(PartnerCardBinding):
    def friendship_templates(self):
        # The dialog consumes an embedded RuleFormDataItem, not the standalone
        # similarly named prefab (which has no travel bindings in this version).
        self.recovery.recover('NpcFriendshipDialog', self.work / 'partner-native')
        self.recovery.recover('NpcFriendshipPropItemGroup', self.work / 'partner-native')
        self.recovery.finalize()
        _, _, dialog, _ = self.recovery.outputs[0]
        consumer, = [row for row in dialog['scripts'] if row['type'] == 'NpcFriendshipDialog']
        root = dialog['documents'][consumer['nativeFileID']]['MonoBehaviour']['mPropItemPrefab']['fileID']
        selected = set()

        def visit(go_id):
            selected.add(go_id)
            components = dialog['documents'][go_id]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(dialog['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(dialog['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        preview = copy.deepcopy(dialog)
        preview['root'] = root
        preview['documents'] = {key: value for key, value in preview['documents'].items() if key in selected}
        preview['scripts'] = [row for row in preview['scripts'] if row['nativeFileID'] in selected]
        preview['objectMap'] = {key: value for key, value in preview['objectMap'].items() if value in selected}
        transform_id = preview['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(preview['documents'][transform_id].values()))
        old_parent = transform['m_Father']
        transform['m_Father'] = dict(fileID=0)

        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Embedded friendship template references outside its original subtree: ' + repr(value))
                for item in value.values():
                    check(item)
            elif isinstance(value, list):
                for item in value:
                    check(item)

        check(preview['documents'])
        source_root, = [key for key, value in preview['objectMap'].items() if value == root]
        self.template_selection = dict(consumer=consumer['original'], field='mPropItemPrefab',
            source_root=source_root, source_name=preview['documents'][root]['GameObject']['m_Name'],
            native_root=root, original_parent=old_parent, detached_parent=dict(fileID=0),
            mapped_objects=len(selected), adaptation='Extract the exact referenced subtree; detach root parent only. Internal fields and geometry retained.')
        for record in self.recovery.proof['prefabs']:
            path = ROOT / record['nativeSource']
            self.inputs[record['nativeSource']] = sha(path.read_bytes())
            self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        return [('NpcFriendshipPreviewPropItem', preview),
                ('NpcFriendshipPropItemGroup', self.recovery.outputs[1][2])]

    def gradient_evidence(self):
        folder = ROOT / '.tools/source-packages/official-code/aot-metadata'
        receipt_path = folder / 'source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        if receipt['manifest'] != self.recovery.apk.version:
            raise ValueError('Gradient AOT input version differs')
        checked = {Path(row['path']).name: row['sha256'] for row in receipt['outputs']}
        image_path = folder / 'native-image.so'
        image = image_path.read_bytes()
        if sha(image) != checked['native-image.so']:
            raise ValueError('Selected AOT image bytes differ')
        self.inputs[receipt_path.relative_to(ROOT).as_posix()] = sha(receipt_path.read_bytes())
        self.inputs[image_path.relative_to(ROOT).as_posix()] = sha(image)
        rows = [
            ('get_TextMeshPro', 0x113a7564, 168, 'cba537e8c66d9153058b669eadb4279ceae811a563921a46649d726a608c1dd2'),
            ('OnEnable', 0x113a760c, 232, '37e564603ec78281bb6e8db156aa7d65b1137f93275fbb0826ce31fd4e8e3c0a'),
            ('OnDisable', 0x113a7794, 232, '05c6e238e3d7cec96321c30746b3eb3da7478cd370e66cfd2bab23498ddb0d59'),
            ('SetColors', 0x113a787c, 532, 'b8bb619b5f3af2fb80e39583aebf8ffac7d2e34e1bf75460a9bbca2138607c10'),
            ('SetGradientColor', 0x113a7a90, 104, '74e82566c25b46528c86c9408a8075c7ed6d11a1dfc2ece86b35a87c13e62e4e'),
            ('OnPreRenderText', 0x113a7af8, 472, '3d00e8a188618a8b938599215612566ed5f0ff89ce8bdcd4394b0c0295f16d77'),
            ('UpdateMesh', 0x113a76f4, 160, '056c3371ac02a80b2d2c767e0ca18ec8cc00b67d83dbb2f933f0e461979bd336'),
            ('.ctor', 0x113a7cd0, 116, 'b74c4076e60eb0c57ff520190b6540a7403d88f7e88428ccb9af6cf3ef57b199')]
        for name, address, size, expected in rows:
            if sha(image[address:address + size]) != expected:
                raise ValueError('Reinspect changed official gradient method: ' + name)
        return [dict(method='TMPColorGradient.' + name, address=hex(address), byte_count=size, sha256=expected)
                for name, address, size, expected in rows]

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / 'PartnersUI/Gifts' / (name + '.cs') for name in
                 ('NpcFriendshipPropItem', 'NpcFriendshipPreviewPropItem', 'NpcFriendshipPropItemGroup')}
        paths['PlayerPropItem'] = runtime / 'ProfessionUI/ProfessionPlayerPropItem.cs'
        paths['TMPColorGradient'] = runtime / 'World/NativeLabelUI/TMPColorGradient.cs'
        for name in ('AkGameObj', 'AkEvent', 'WwiseEventReference'):
            paths[name] = runtime / 'ProfessionUI' / (name + '.cs')
        for name, path in paths.items():
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = pointer
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'} and value['class'] == 'StateUIColorConfig' and value['asm'] == 'Framework':
            return dict(value, **{'class': 'WorldStateUIColorConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'})
        return super().replace(value)

    def build(self):
        gradient = self.gradient_evidence()
        text_keys = {'ui_common_level_style_1', 'ui_common_prop_plus', 'ui_space_before_value',
                     'PropType.Attack', 'PropType.Defence', 'PropType.MaxHp', 'PropType.HangUpExpAdd'}
        texts, sources = language(text_keys)
        if set(texts) != text_keys:
            raise ValueError('Selected friendship text entries missing: ' + repr(text_keys - texts.keys()))
        records = {row['name']: row for row in self.recovery.apk.source.manifest('WwiseBnk', self.recovery.apk.archive)['bundles']}
        for path, digest in sources.items():
            raw = (ROOT / path).read_bytes()
            row = records[Path(path).name]
            if len(raw) != row['size'] or hashlib.md5(raw).hexdigest() != row['hash']:
                raise ValueError('Selected language bytes differ: ' + path)
            self.inputs[path] = digest
        helper = ROOT / 'scripts/npc_travel_language.py'
        self.inputs[helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        templates = self.friendship_templates()
        roots, prefabs = {}, []
        for _, prefab in templates:
            for key, pointer in prefab['external'].items():
                self.objects[pointer['guid']] = self.original(key)
            for key, file_id in prefab['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(prefab['documents'][file_id].values())), owner)
        for name, prefab in templates:
            print('Original prefab script types:', name, sorted({row['type'] for row in prefab['scripts']}), flush=True)
            for script in prefab['scripts']:
                fields = prefab['documents'][script['nativeFileID']]['MonoBehaviour']
                full_name = '.'.join(filter(None, [script['namespace'], script['type']]))
                fields['m_Script'] = self.scripts[full_name]
                if script['type'] == 'ButtonSound':
                    owner = self.original(script['original'])
                    fields['ClickEvent'] = self.sound(self.recovery.fields(owner)['ClickEvent'], owner)
                if script['type'] == 'AkEvent':
                    owner = self.original(script['original'])
                    triggers = self.recovery.fields(owner)['triggerList']
                    if not isinstance(triggers, list) or any(not isinstance(value, int) for value in triggers):
                        raise ValueError('Original Wwise trigger list is not decoded')
                    fields['triggerList'] = triggers
            resolved = self.replace(prefab['documents'])
            target_guid = hashlib.md5(('zjcs:partner-current:' + name + '.prefab').encode()).hexdigest()
            prefabs.append((Path(name + '.prefab'), target_guid, resolved))
            roots[name] = dict(fileID=prefab['root'], guid=target_guid, type=3)
        for target, target_guid, data in self.queue:
            if isinstance(data, dict):
                print('Binding friendship dependency:', target.as_posix(), flush=True)
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        self.queue.extend(prefabs)
        text_payload = dict(manifest=self.recovery.apk.version, texts=[dict(key=key, text=texts[key]) for key in sorted(texts)])
        self.queue.append((Path('partner-gift-texts.json'), hashlib.md5(b'zjcs:partner-gift-texts').hexdigest(),
                           (json.dumps(text_payload, ensure_ascii=False, indent=2) + '\n').encode('utf8')))
        folder = self.work / 'gift-assets'
        outputs = {}
        for target, target_guid, data in self.queue:
            path = folder / target
            if isinstance(data, dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in data.items():
                    kind, = content
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = text.encode('utf8')
            else:
                raw = data
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            suffix = path.suffix.lower()
            importer = ('PrefabImporter' if suffix == '.prefab' else 'ShaderImporter' if suffix == '.shader'
                        else 'TextScriptImporter' if suffix == '.json'
                        else 'TrueTypeFontImporter' if suffix in ('.ttf', '.otf') else 'NativeFormatImporter')
            metadata = Path(str(path) + '.meta')
            metadata.write_text('fileFormatVersion: 2\nguid: ' + target_guid + '\n' + importer + ':\n  externalObjects: {}\n', 'utf8', newline='\n')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(metadata.read_bytes())
        if self.audio_outputs:
            raise ValueError('Emit original audio dependencies before validating friendship prefabs')
        inputs = dict(self.inputs)
        for name in ('prepare_partner_card_native.py', 'prepare_current_partner_native.py'):
            path = ROOT / 'scripts' / name
            inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        gradient_evidence = ROOT / 'unity/Assets/Official/World/Map/13/Source/native-text-gradient.json'
        inputs[gradient_evidence.relative_to(ROOT).as_posix()] = sha(gradient_evidence.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=inputs, objects=self.proof, outputs=outputs,
                       native_recovery=self.recovery.proof, template_selection=self.template_selection,
                       roots=roots, script_bindings=self.script_sources, gradient_aot_methods=gradient,
                       gradient_observations='Selected AOT: all eight methods inspected. Preserve inclusive line character bounds, all four vertices, rotated X/unrotated line extents, alpha-one keys, renderMode=255 and ForceMeshUpdate(false,false).',
                       generator_sha256=sha(Path(__file__).read_bytes()))
        (self.work / 'partner-gift-native-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original friendship property prefabs:', len(outputs), flush=True)


def install_staged(work):
    proof_path = work / 'partner-gift-native-source-manifest.json'
    receipt = json.loads(proof_path.read_bytes())
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected:
            raise ValueError('Staged friendship input changed: ' + path)
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild staged friendship assets after generator changes')
    for binding in receipt['script_bindings'].values():
        path = ROOT / binding['path']
        if sha(path.read_bytes()) != binding['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Staged friendship script changed: ' + str(path))
    staged = work / 'gift-assets'
    for path, expected in receipt['outputs'].items():
        if sha((staged / path).read_bytes()) != expected:
            raise ValueError('Staged friendship output changed: ' + path)
    partners = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for metadata in partners.rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            asset_guid = guid(metadata)
            if asset_guid in existing:
                raise ValueError('Existing partner GUID collision: ' + asset_guid)
            existing[asset_guid] = asset
    destination = partners / 'UI/Friendship'
    plan = []
    for path in receipt['outputs']:
        if path.endswith('.meta'):
            continue
        original = staged / path
        asset_guid = guid(Path(str(original) + '.meta'))
        target = existing.get(asset_guid, destination / path)
        if target.is_file() and target.read_bytes() != original.read_bytes():
            raise ValueError('Existing original asset has different bytes: ' + str(target))
        plan.append((original, target))
    installed, reused, folders = {}, [], {}
    for original, target in plan:
        metadata = Path(str(target) + '.meta')
        if target.is_file():
            reused.append(target.relative_to(ROOT).as_posix())
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(original.read_bytes())
            metadata.write_bytes(Path(str(original) + '.meta').read_bytes())
        for path in (target, metadata):
            installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        parent = target.parent
        while parent.is_relative_to(destination):
            entry = Path(str(parent) + '.meta')
            if not entry.is_file():
                entry.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:partner-friendship-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                                 + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
            folders[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
            parent = parent.parent
    receipt['installed_output_sha256'] = installed
    receipt['folder_meta_sha256'] = folders
    receipt['reused_assets'] = reused
    receipt['scope'] = 'Original friendship property prefabs; complete gift/detail window, local property provider and audio host remain pending.'
    (ROOT / 'assets/reference/partner-gift-native-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed friendship assets:', len(plan), 'reused:', len(reused))


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    if parser.parse_args().install:
        install_staged(validation_directory())
    else:
        PartnerGiftBinding(validation_directory()).build()
