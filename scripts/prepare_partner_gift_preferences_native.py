"""Restore selected official partner gift icons and their native dependencies."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import documents, identity, pointer
from prepare_partner_card_native import guid, script_id, sha, language
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory


class PartnerGiftPreferencesBinding(PartnerGiftBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / 'ProfessionUI' / (name + '.cs') for name in
                 ('ItemIcon', 'ItemIconAttachment', 'UIClickSound',
                  'ItemIconAttachmentCell', 'ItemIconAttachmentHint', 'ItemIconAttachmentConfig', 'ItemIconConfig')}
        paths['ItemIconNpcGift'] = runtime / 'PartnersUI/Gifts/ItemIconNpcGift.cs'
        paths['LayoutElementClamper'] = runtime / 'WorldUI/WorldLayoutElementClamper.cs'
        paths['ItemProgress'] = runtime / 'WorldUI/WorldItemProgress.cs'
        for name, path in paths.items():
            metadata = Path(str(path) + '.meta')
            reference = dict(fileID=11500000, guid=guid(metadata), type=3)
            self.scripts[name] = reference
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=reference,
                sha256=sha(path.read_bytes()), meta_sha256=sha(metadata.read_bytes()))
            self.class_ids[script_id('', name)] = reference
            self.script_guids.add(reference['guid'])

    def gradient_evidence(self):
        return []  # These prefabs do not contain TMPColorGradient.

    def friendship_templates(self):
        manifest_bytes = self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive)
        records = read_asset_manifest(manifest_bytes)
        configs = {}
        for obj in self.recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_itemicon.bundle'):
            if obj.type.name == 'MonoBehaviour':
                name = obj.read(check_read=False).m_Name
                if name in ('ItemIconManagerConfig', 'ItemIconAttachmentConfig', 'ItemIconConfig'):
                    configs[name] = obj
        manager = self.recovery.fields(configs['ItemIconManagerConfig'])
        default = pointer(manager['DefPrefab'], configs['ItemIconManagerConfig'])
        if manager['IdList'] or manager['IdPrefabList'] or default.read().m_Name != 'ItemIconDefault':
            raise ValueError('Reinspect original item-ID prefab selection')
        attachment = self.recovery.fields(configs['ItemIconAttachmentConfig'])
        specifications = [('ItemIconDefault', 'defaultpackage_assets_prefabs_ui_item.bundle', 'Prefabs/UI/Item/ItemIconDefault.prefab'),
                          ('ItemIconNpcGift', 'defaultpackage_assets_prefabs_ui_item.bundle', 'Prefabs/UI/Item/ItemIconNpcGift.prefab')]
        attachment_records = []
        for attachment_type in (13, 16):
            definition, = [row for row in attachment['Templates'] if row['Type'] == attachment_type]
            record, = [row for row in records if row['guid'] == definition['GuidProxy']['mAssetGuid']]
            obj, = [ref.deref() for name, ref in self.recovery.apk.container(record['bundle'])
                    if name.casefold() == record['path'].casefold() and ref.deref().type.name == 'GameObject']
            specifications.append((obj.read().m_Name, record['bundle'], record['path'].removeprefix('Assets/')))
            attachment_records.append(dict(type=attachment_type, name=obj.read().m_Name, asset=record))
        for name, bundle, native in specifications:
            self.recovery.recover(name, self.work / 'partner-native', bundle=bundle, native_relative=native)
        self.recovery.finalize()
        templates = [(spec[0], output[2]) for spec, output in zip(specifications, self.recovery.outputs)]
        for _, data in templates:
            for key, reference in data['external'].items():
                self.objects[reference['guid']] = self.original(key)
            for key, file_id in data['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(data['documents'][file_id].values())), owner)
            self.replace(data['documents'])
        for record in self.recovery.proof['prefabs']:
            path = ROOT / record['nativeSource']
            self.inputs[record['nativeSource']] = sha(path.read_bytes())
            self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(default=identity(default), attachments=attachment_records,
            manager_source=self.recovery.apk.provenance(configs['ItemIconManagerConfig']),
            manager_sha256=sha(configs['ItemIconManagerConfig'].get_raw_data()),
            id_overrides=manager['IdList'], type_overrides=manager['TypeList'])
        self.attachment_config = self.original_asset(configs['ItemIconAttachmentConfig'])
        quality_path = self.native / 'ScriptableConfig/Modules/ItemIcon/ItemIconConfig.asset'
        quality_guid = guid(Path(str(quality_path) + '.meta'))
        self.objects[quality_guid] = configs['ItemIconConfig']
        self.icon_config = self.load(quality_guid)
        self.images = self.gift_sprites(records, manager)
        self.images['asset_manifest_sha256'] = sha(manifest_bytes)
        self.images['font'] = self.chinese_font(records)
        texts, sources = language({'ui_ItemIcon_count_key'})
        if set(texts) != {'ui_ItemIcon_count_key'}:
            raise ValueError('Selected original count format is unavailable')
        language_records = {row['name']: row for row in self.recovery.apk.source.manifest('WwiseBnk', self.recovery.apk.archive)['bundles']}
        for relative, expected in sources.items():
            raw = (ROOT / relative).read_bytes()
            record = language_records[Path(relative).name]
            if sha(raw) != expected or hashlib.md5(raw).hexdigest() != record['hash'] or len(raw) != record['size']:
                raise ValueError('Selected count language bytes differ: ' + relative)
            self.inputs[relative] = expected
        self.images['texts'] = [dict(key=key, text=value) for key, value in texts.items()]
        payload = dict(self.images, icon_config=self.icon_config, attachment_config=self.attachment_config,
            roots={name: dict(fileID=data['root'], guid=hashlib.md5(('zjcs:partner-current:' + name + '.prefab').encode()).hexdigest(), type=3)
                   for name, data in templates})
        self.queue.append((Path('partner-gift-images.json'), hashlib.md5(b'zjcs:partner-gift-images').hexdigest(),
                           (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf8')))
        return templates

    def gift_sprites(self, records, manager):
        rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
        data = json.loads(rules.read_bytes())
        if data['manifest'] != self.recovery.apk.version:
            raise ValueError('Gift configuration version differs')
        self.inputs[rules.relative_to(ROOT).as_posix()] = sha(rules.read_bytes())
        source = ROOT / '.tools/source-packages/official-code/framework-firstpass-source/ResourceLoaderExtensions.cs'
        selected = ROOT / '.tools/source-packages/current' / data['manifest'] / 'official-code/framework-firstpass-source/ResourceLoaderExtensions.cs'
        if source.read_bytes() != selected.read_bytes() or '"Assets/Image/" + spriteName' not in source.read_text('utf-8-sig'):
            raise ValueError('Original sprite address prefix changed')
        self.inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        preferences, deferred = {}, {}
        for npc in data['tables']['npc']:
            for item in npc.get('GiftPreferenceDict') or {}:
                # User-confirmed paused collaborations, documented in
                # docs/features/offline-access.md. Keep their table rows intact.
                target = deferred if 11015 <= npc['Id'] <= 11022 else preferences
                target.setdefault(int(item), []).append(npc['Id'])
        items = {row['ClassId']: row for row in data['tables']['item']}
        bindings = []
        for item_id, npc_ids in sorted(preferences.items()):
            item = items[item_id]
            if item['ItemType'] not in (328, 433) or item['ItemType'] in manager['TypeList']:
                raise ValueError('Reinspect gift icon selection and ItemInfo.GetIconPath: ' + str(item_id))
            address = item['RawIcon']
            record, = [row for row in records if row['address'] == 'Assets/Image/' + address
                       or row['path'] == 'Assets/Image/' + address + '.png']
            obj, = [ref.deref() for path, ref in self.recovery.apk.container(record['bundle'])
                    if path.casefold() == record['path'].casefold() and ref.deref().type.name == 'Sprite']
            source_key, source_id = self.recovery.fields(obj)['m_RenderDataKey']
            render_guid = ''.join(format(source_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
            if render_guid != record['guid']:
                raise ValueError('Original gift sprite key differs: ' + address)
            name = self.name_key(obj.read(check_read=False).m_Name)
            matches = []
            for key in [name] + [name + str(i) for i in range(100)]:
                for candidate in self.by_name.get(key, []):
                    if candidate.suffix == '.asset':
                        docs = documents(candidate.read_text('utf8'))
                        if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                            matches.append(candidate)
            candidate, = matches
            source_guid = guid(Path(str(candidate) + '.meta'))
            if source_guid in self.objects and identity(self.objects[source_guid]) != identity(obj):
                raise ValueError('Gift sprite identity collides with serialized dependency')
            self.objects[source_guid] = obj
            bindings.append(dict(item_id=item_id, npc_ids=npc_ids, address=address, pointer=self.load(source_guid),
                manifest_asset=record, original=identity(obj), original_sha256=sha(obj.get_raw_data()),
                source=self.recovery.apk.provenance(obj)))
            print('Bound preference sprite:', item_id, address, flush=True)
        return dict(manifest=data['manifest'], sprites=bindings,
            deferred=[dict(item_id=item, npc_ids=ids) for item, ids in sorted(deferred.items()) if item not in preferences],
            deferred_scope='User-confirmed paused collaborations 11015–11022; full rules retain their original rows.')

    def build(self):
        super().build()
        path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(path.read_bytes())
        receipt.pop('gradient_aot_methods')
        receipt.pop('gradient_observations')
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['icon_config'] = self.icon_config
        receipt['attachment_config'] = self.attachment_config
        receipt['dynamic_images'] = self.images
        used_scripts = set()
        for relative in receipt['outputs']:
            if Path(relative).suffix in ('.prefab', '.asset', '.mat'):
                raw = (self.work / 'gift-assets' / relative).read_bytes()
                used_scripts.update(match.decode() for match in re.findall(rb'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})', raw))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used_scripts}
        for filename in ('ItemIconManager.cs', 'IconConfigPool.cs', 'ItemIconManagerConfig.cs', 'ItemIconAttachmentManager.cs'):
            relative = Path('client-source') / filename
            source = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if source.read_bytes() != selected.read_bytes():
                raise ValueError('Selected manager implementation differs: ' + filename)
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        for relative in (Path('common-source/Common/ItemInfo.cs'), Path('common-source/ItemComparerBase.cs')):
            source = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if source.read_bytes() != selected.read_bytes():
                raise ValueError('Selected item semantics differ: ' + str(relative))
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        base = ROOT / 'scripts/prepare_partner_gift_native.py'
        receipt['inputs'][base.relative_to(ROOT).as_posix()] = sha(base.read_bytes())
        receipt['scope'] = 'Original default/gift icon prefabs, ReduceTip attachment and all preference sprites; host binding and Play Mode validation pending.'
        (self.work / 'partner-gift-preferences-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        path.unlink()


def install_staged(work):
    receipt = json.loads((work / 'partner-gift-preferences-native-source-manifest.json').read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild preferences after generator changes')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Preference input changed: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Preference script changed: ' + row['path'])
    partners = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for metadata in partners.rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            identity = guid(metadata)
            if identity in existing:
                raise ValueError('Existing partner GUID collision: ' + identity)
            existing[identity] = asset
    destination = partners / 'UI/Gifts'
    plan, reused = [], []
    for relative, expected in receipt['outputs'].items():
        path = work / 'gift-assets' / relative
        if sha(path.read_bytes()) != expected:
            raise ValueError('Staged preference bytes differ: ' + relative)
        if relative.endswith('.meta'):
            continue
        target = existing.get(guid(Path(str(path) + '.meta')), destination / relative)
        if target.is_file():
            if target.read_bytes() != path.read_bytes():
                raise ValueError('Existing preference asset changed: ' + str(target))
            reused.append(target.relative_to(ROOT).as_posix())
        plan.extend(((path, target), (Path(str(path) + '.meta'), Path(str(target) + '.meta'))))
    installed, folders = {}, {}
    for source, target in plan:
        if not target.is_file():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
        parent = target.parent
        while parent == destination or parent.is_relative_to(destination):
            metadata = Path(str(parent) + '.meta')
            if not metadata.is_file():
                metadata.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:partner-gift-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                    + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
            folders[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
            parent = parent.parent
    receipt.update(installed_output_sha256=installed, installed_folder_sha256=folders, reused=reused,
        installed_root=destination.relative_to(ROOT).as_posix())
    proof = ROOT / 'assets/reference/partner-gift-preferences-source-manifest.json'
    proof.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed native gift assets:', len(installed), 'reused:', len(reused))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--install-staged', action='store_true')
    args = parser.parse_args()
    work = (args.output or validation_directory()).resolve()
    if args.install_staged:
        install_staged(work)
    else:
        PartnerGiftPreferencesBinding(work).build()
