"""Recover the selected original award queue, both paths and configured icon prefabs."""
import hashlib
import json
import argparse
import re
from pathlib import Path

from official_manifest import read_asset_manifest
from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT, guid, script_id, sha, PPtr
from prepare_current_partner_native import identity
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


class HomeAwardQueueBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        names = {name: name for name in ('AwardListPanel', 'AwardListPanelItem',
            'AwardListPanelNormalPath', 'AwardListPanelSinglePath', 'ItemAwardIconManagerConfig',
            'AnimationAutoNext', 'ItemAwardIconGem', 'ItemAwardIconGodGacha')}
        names['ItemAwardIcon'] = 'HomeAwardIcon'
        for original, name in names.items():
            path = ROOT / 'unity/Assets/Game/Runtime/Official/HomeAwardUI' / (name + '.cs')
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[original] = pointer
            self.class_ids[script_id('', original)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[original] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/VerticalLayoutGroupJSZX.cs'
        meta = Path(str(path) + '.meta')
        pointer = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['UGUIJszx.VerticalLayoutGroupJSZX'] = pointer
        self.class_ids[script_id('UGUIJszx', 'VerticalLayoutGroupJSZX')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['UGUIJszx.VerticalLayoutGroupJSZX'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldItemProgress.cs'
        meta = Path(str(path) + '.meta')
        pointer = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['ItemProgress'] = pointer
        self.class_ids[script_id('', 'ItemProgress')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['ItemProgress'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
        folder = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
        for path in folder.glob('*.cs'):
            if path.stem in self.scripts: continue
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[path.stem] = pointer
            self.class_ids[script_id('', path.stem)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[path.stem] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def original_asset(self, obj):
        if obj.type.name == 'Sprite':
            key, _ = self.recovery.fields(obj)['m_RenderDataKey']
            asset_guid = ''.join(format(key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
            self.bind_dynamic_sprite(asset_guid)
            pointer = self.dynamic_sprites_by_guid[asset_guid]['pointer']
            self.script_guids.add(pointer['guid'])
            return pointer
        return super().original_asset(obj)

    def friendship_templates(self):
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        attachment = self.recovery.apk.asset('defaultpackage_assets_scriptableconfig_modules_itemicon.bundle', '/ItemIconAttachmentConfig.asset')
        self.attachment_config = dict(fields=self.recovery.fields(attachment),
            original=identity(attachment), source=self.recovery.apk.provenance(attachment),
            original_sha256=sha(attachment.get_raw_data()))
        attachment_guids = {row['GuidProxy']['mAssetGuid'] for row in self.attachment_config['fields']['Templates']
            if row['Type'] in (1, 13)}
        manager = self.recovery.apk.asset('defaultpackage_assets_scriptableconfig_modules_itemicon.bundle', '/ItemIconManagerConfig.asset')
        manager_fields = self.recovery.fields(manager)
        # Follow the original GetIconGameObject for the current XP probe.
        pointer = manager_fields['DefPrefab']
        if 50 in manager_fields['IdList']:
            pointer = manager_fields['IdPrefabList'][manager_fields['IdList'].index(50)]
        elif 50 in manager_fields['TypeList']:
            pointer = manager_fields['PrefabList'][manager_fields['TypeList'].index(50)]
        selected_icon = PPtr(**pointer, assetsfile=manager.assets_file).deref()
        selected_icon_name = selected_icon.read().m_Name
        self.item_manager = dict(fields=manager_fields, original=identity(manager),
            original_sha256=sha(manager.get_raw_data()), source=self.recovery.apk.provenance(manager),
            selected_item=50, selected_type=50, selected_original=identity(selected_icon),
            selected_template=selected_icon_name)
        selected = [row for row in records if '/AwardListPanel' in row['path'] or '/ItemAwardIcon' in row['path']
            or row['path'] == 'Assets/Prefabs/UI/Item/' + selected_icon_name + '.prefab'
            or row['guid'] in attachment_guids]
        for row in selected:
            if not row['path'].endswith('.prefab'): continue
            name = Path(row['path']).stem
            self.recovery.recover(name, self.work / 'award-queue-native', bundle=row['bundle'],
                native_relative=row['path'].removeprefix('Assets/'))
        config = self.recovery.apk.asset('defaultpackage_assets_scriptableconfig_modules_itemicon.bundle', '/ItemAwardIconManagerConfig.asset')
        fields = self.recovery.fields(config)
        self.icon_config = dict(original=identity(config), original_sha256=sha(config.get_raw_data()),
            fields=fields, source=self.recovery.apk.provenance(config))
        self.recovery.finalize()
        templates = [(path.stem, panel) for path, _, panel, _ in self.recovery.outputs]
        item_panel, = [panel for name, panel in templates if name == selected_icon_name]
        if item_panel['objectMap'].get(identity(selected_icon)) != item_panel['root']:
            raise ValueError('Original item manager prefab identity differs')
        default = PPtr(**fields['DefAwardPrefab'], assetsfile=config.assets_file).deref()
        name, = [name for name, panel in templates if panel['objectMap'].get(identity(default)) == panel['root']]
        self.icon_config['default_template'] = name
        for _, panel in templates:
            for script in panel['scripts']:
                if script['type'] != 'AwardListPanel': continue
                owner = self.original(script['original'])
                config = self.recovery.fields(owner)['mAwardListPanelItemConfigDict']['mSerializeBuffer']
                for row in config:
                    self.sound(row['Value']['AwardSound'], owner)
        missing = {'.'.join(filter(None, (s['namespace'], s['type'])))
            for _, panel in templates for s in panel['scripts']} - self.scripts.keys()
        if missing: raise ValueError('Port original award dependencies: ' + repr(sorted(missing)))
        for path, _, _, _ in self.recovery.outputs:
            for item in (path, Path(str(path) + '.meta')):
                self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        quality = self.recovery.apk.asset('defaultpackage_assets_scriptableconfig_modules_itemicon.bundle', '/ItemIconConfig.asset')
        self.quality_config = self.original_asset(quality)
        self.quality_config_source = dict(original=identity(quality), original_sha256=sha(quality.get_raw_data()),
            source=self.recovery.apk.provenance(quality))
        colors = self.recovery.apk.asset('scriptableconfig_modules_systemsetting', '/GlobalColors.asset')
        self.colors = dict(fields=self.recovery.fields(colors), source=self.recovery.apk.provenance(colors),
            original_sha256=sha(colors.get_raw_data()))
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(manifest_assets=selected, icon_manager=self.icon_config,
            scope='Complete selected award queue and item prefabs; original geometry/components/animations retained.')
        return templates

    def build(self):
        PartnerGiftBinding.build(self)
        source = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(source.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['icon_manager'] = self.icon_config
        receipt['award_sounds'] = self.audio_proof
        receipt['quality_config'] = dict(pointer=self.quality_config, **self.quality_config_source)
        receipt['global_colors'] = self.colors
        receipt['attachment_config'] = self.attachment_config
        receipt['item_icon_manager'] = self.item_manager
        # Reuse only previously proved byte-identical, non-readable textures.
        # Keep the original CAB inputs intact and record this local conversion.
        shared = ROOT / 'assets/reference/shared-textures-source-manifest.json'
        aliases, reused_textures = {}, []
        staged = {guid(self.work / 'gift-assets' / name): name[:-5]
            for name in receipt['outputs'] if name.endswith('.meta')}
        for row in json.loads(shared.read_bytes())['textures']:
            source, canonical = row['source'], row['canonical']
            relative = staged.get(source['guid'])
            if relative is None: continue
            path = self.work / 'gift-assets' / relative
            raw = path.read_bytes()
            if b'\nTexture2D:' not in raw[:4096] or not re.search(rb'm_IsReadable: 0\s', raw[:4096]):
                raise ValueError('Preserve independently mutable native texture: ' + relative)
            target = ROOT / canonical['path']
            if sha(raw) != source['sha256'] or sha(target.read_bytes()) != canonical['sha256'] or source['sha256'] != canonical['sha256']:
                raise ValueError('Shared award texture payload differs: ' + relative)
            local_meta, target_meta = Path(str(path) + '.meta'), Path(str(target) + '.meta')
            def importer(meta):
                return re.sub(rb'(?m)^guid: [0-9a-f]{32}', b'guid: GUID', meta.read_bytes()).replace(b'\r\n', b'\n')
            if sha(target_meta.read_bytes()) != canonical['meta_sha256'] or importer(local_meta) != importer(target_meta):
                raise ValueError('Shared award texture importer differs: ' + relative)
            aliases[source['guid']] = canonical['guid']
            reused_textures.append(dict(staged_path=relative, staged_sha256=sha(raw), canonical=canonical))
            receipt['outputs'].pop(relative)
            receipt['outputs'].pop(relative + '.meta')
            receipt['inputs'][canonical['path']] = canonical['sha256']
            receipt['inputs'][canonical['path'] + '.meta'] = canonical['meta_sha256']
        receipt['inputs'][shared.relative_to(ROOT).as_posix()] = sha(shared.read_bytes())
        receipt['shared_textures'] = reused_textures
        # This module owns its rebinding of ItemAwardIcon; preserve other owners'
        # prefab identities and script bindings even when their source is shared.
        redirects = {row['guid']: hashlib.md5(('zjcs:home-award-queue:' + name).encode()).hexdigest()
            for name, row in receipt['roots'].items()}
        for name, expected in list(receipt['outputs'].items()):
            path = self.work / 'gift-assets' / name
            raw = path.read_bytes()
            if name.endswith('.meta') or raw.startswith(b'%YAML'):
                for old, new in {**aliases, **redirects}.items():
                    raw = raw.replace(('guid: ' + old).encode(), ('guid: ' + new).encode())
                path.write_bytes(raw)
                receipt['outputs'][name] = sha(raw)
        for row in receipt['roots'].values(): row['guid'] = redirects[row['guid']]
        used = set()
        for relative in receipt['outputs']:
            path = self.work / 'gift-assets' / relative
            if path.suffix != '.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', path.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
            if row['pointer']['guid'] in used}
        receipt['scope'] = 'Staged original award queue dependency closure. Explicit installation, local services, graphical runtime and formal bed binding remain required.'
        (self.work / 'home-award-queue-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original award queue:', len(receipt['outputs']), 'outputs', flush=True)


def install(work):
    receipt_path = ROOT / 'assets/reference/home-award-queue-native-source-manifest.json'
    receipt = json.loads((work / receipt_path.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed original award generator')
    config = receipt['icon_manager']['fields']
    if config['TypeAwardList'] or config['PrefabAwardList']:
        raise ValueError('Bind the selected original icon-manager overrides explicitly.')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Preserve changed original award input: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Rebuild changed award script: ' + row['path'])
    destination = ROOT / 'unity/Assets/Official/Home/UI/AwardQueue'
    previous = json.loads(receipt_path.read_bytes()).get('installed_output_sha256', {}) if receipt_path.exists() else {}
    existing = {}
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Existing Unity GUID collision: ' + key)
            existing[key] = path

    def equivalent(left, right):
        if left.replace(b'\r\n', b'\n') == right.replace(b'\r\n', b'\n'): return True
        if not left.startswith(b'%YAML') or not right.startswith(b'%YAML'): return False
        from prepare_current_partner_native import documents
        def normalize(value):
            if isinstance(value, dict):
                return {key: '' if key == 'm_Name' and child is None else normalize(child) for key, child in value.items()}
            if isinstance(value, list): return [normalize(child) for child in value]
            return value
        return normalize(documents(left.decode('utf8'))) == normalize(documents(right.decode('utf8')))

    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / relative
        if sha(source.read_bytes()) != expected: raise ValueError('Staged original award changed: ' + relative)
        if relative.endswith('.meta'): continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, destination / relative)
        same = target.exists() and equivalent(target.read_bytes(), source.read_bytes())
        if target.exists() and not same:
            if not target.is_relative_to(destination) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different existing original award dependency: ' + str(target))
        if same:
            reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=key,
                sha256=sha(target.read_bytes()), staged_sha256=expected,
                comparison='Exact bytes or parsed YAML equality normalizing only null/empty object names.'))
        else: plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        if target.is_relative_to(destination):
            installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes()) if same else expected
            installed[Path(str(target) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(source) + '.meta').read_bytes())
        existing[key] = target
    # The selected original manager has no type overrides. Do not infer a new
    # icon selection table from other similarly named prefabs in the bundle.
    script = ROOT / 'unity/Assets/Game/Runtime/Official/HomeAwardUI/OfficialHomeAwardQueueAssetCatalog.cs'
    roots = receipt['roots']
    def pointer(row): return '{fileID: ' + str(row['fileID']) + ', guid: ' + row['guid'] + ', type: ' + str(row['type']) + '}'
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    text += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    text += '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(script) + '.meta')) + ', type: 3}\n'
    text += "  m_Name: OfficialHomeAwardQueueAssetCatalog\n  m_EditorClassIdentifier: ''\n  manifest: '884_156692'\n"
    text += '  queuePrefab: ' + pointer(roots['AwardListPanel']) + '\n'
    text += '  defaultIconPrefab: ' + pointer(roots[receipt['icon_manager']['default_template']]) + '\n  itemPrefabs:\n'
    for name in ('AwardListPanelItem', 'AwardListPanelSpringItem'):
        row, = [row for row in receipt['template_selection']['manifest_assets'] if row['path'].endswith('/' + name + '.prefab')]
        text += '  - originalGuid: ' + row['guid'] + '\n    prefab: ' + pointer(roots[name]) + '\n'
    text += '  itemIconPrefab: ' + pointer(roots[receipt['item_icon_manager']['selected_template']]) + '\n'
    text += '  itemIconConfig: ' + pointer(receipt['quality_config']['pointer']) + '\n'
    hint, = [row for row in receipt['template_selection']['manifest_assets']
        if row['guid'] == 'd019ea2d9f143514ebc8c30f545dca16']
    text += '  hintPrefab: ' + pointer(roots[Path(hint['path']).stem]) + '\n'
    extra_config, = [row for row in receipt['attachment_config']['fields']['Templates'] if row['Type'] == 1]
    extra, = [row for row in receipt['template_selection']['manifest_assets']
        if row['guid'] == extra_config['GuidProxy']['mAssetGuid']]
    text += '  extraGetPrefab: ' + pointer(roots[Path(extra['path']).stem]) + '\n'
    text += '  sounds:\n'
    for row in receipt['award_sounds'].values():
        text += '  - eventId: ' + str(row['event']['id']) + '\n    clip: ' + pointer(row['pointer']) + '\n'
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeAwardQueueAssetCatalog.asset'
    if catalog.exists() and catalog.read_text('utf8') != text and sha(catalog.read_bytes()) != previous.get(catalog.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve changed original award catalog.')
    # Only mutate after all source, catalog and shared-asset comparisons pass.
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    catalog.write_text(text, 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter')):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for folder in (destination, *[p for p in destination.rglob('*') if p.is_dir()]):
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused)
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original award closure:', len(installed), 'files;', len(reused), 'reused assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    if args.install: install(validation_directory())
    else: HomeAwardQueueBinding(validation_directory()).build()
