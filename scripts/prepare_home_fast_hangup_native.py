"""Recover the complete original item acceleration window and icon dependencies."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from prepare_home_currency_windows_native import HomeCurrencyWindowsBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT, guid, sha, script_id, Dumper
from prepare_current_partner_native import documents
from prepare_partner_input_dialog import metadata
from official_manifest import read_asset_manifest
from official_tables import Constructors
from npc_travel_language import language
from validation_workspace import validation_directory
import yaml

RECEIPT = ROOT/'assets/reference/home-fast-hangup-native-source-manifest.json'
DESTINATION = ROOT/'unity/Assets/Official/Home/UI/FastHangUp'


class HomeFastHangUpBinding(HomeCurrencyWindowsBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('FurnitureFastHangUpDialog', 'FurnitureUseItemSliderGroup', 'SetUseCountSlider'):
            path = ROOT/'unity/Assets/Game/Runtime/Official/HomeFastHangUpUI'/(name+'.cs')
            meta = Path(str(path)+'.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer; self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def friendship_templates(self):
        self.recovery.recover('FurnitureFastHangUpDialog', self.work/'fast-hangup-native',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
            native_relative='Prefabs/UI/Furniture/FurnitureFastHangUpDialog.prefab')
        self.recovery.recover('FurnitureSpeedUsedItem', self.work/'fast-hangup-native',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
            native_relative='Prefabs/UI/Furniture/FurnitureSpeedUsedItem.prefab')
        templates = super().friendship_templates()
        # Existing currency extraction supplies the exact native item manager
        # and its hint attachment; unrelated currency window roots are not built.
        return [(name, panel) for name, panel in templates if name not in ('ShopQuickBuyDialog', 'PayMoneyChangeDialog')]

    def configuration(self):
        super().configuration()
        code = ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code'
        constructors = Constructors(code)
        mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
            re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        wanted = {'fast_hang_up', 'hang_up', 'item'}
        for obj in self.recovery.apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset': continue
            name = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if name not in wanted: continue
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            self.tables[name] = constructors.table(raw, mapping[name])
            self.table_proofs[name] = dict(source=self.recovery.apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw), constructor=mapping[name])
        if wanted-self.tables.keys(): raise ValueError('Selected acceleration configuration is missing')
        settings, = [obj for obj in self.recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_home.bundle')
            if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Name == 'HomeSettings']
        self.home_settings = dict(fields=self.recovery.fields(settings), source=self.recovery.apk.provenance(settings),
            sha256=sha(settings.get_raw_data()))
        item_ids = {int(key) for row in self.tables['fast_hang_up'] for key in row['FastHangUpItemDic']}
        item_ids.update(self.tables['game_settings'][key] for key in ('MoneyId', 'PlayerExpItemId'))
        self.tables['item'] = [row for row in self.tables['item'] if row['ClassId'] in item_ids]
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.item_images = {}
        for item in self.tables['item']:
            path = 'Assets/Image/'+item['RawIcon']
            record, = [row for row in records if row.get('address', '').casefold() == path.casefold()
                or row['path'].casefold() == (path+'.png').casefold()]
            self.bind_dynamic_sprite(record['guid'])
            self.item_images[str(item['ClassId'])] = dict(item=item, **self.dynamic_sprites_by_guid[record['guid']])
        for name in constructors.sources:
            path = code/'common-source/Common'/(name+'.cs')
            if not path.exists(): path = code/'common-source/Common.LubanGen'/(name+'.cs')
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())

    def build(self):
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        folder = self.work/'gift-assets'
        keys = set()
        for relative in receipt['outputs']:
            path = folder/relative
            if path.suffix != '.prefab': continue
            keys.update(doc['MonoBehaviour']['myKey'] for doc in documents(path.read_text('utf8')).values()
                if doc.get('MonoBehaviour', {}).get('myKey'))
        keys.update(('ui_common_contrast_1', 'ui_ItemIcon_count_key', 'ui_fast_hang_up_max_count_tip',
            'fast_hang_up_no_item_tip', 'ui_fast_hang_up_exp_exceed_tip_content', 'ui_fast_hang_up_exp_exceed_tip_title',
            'ui_fast_hang_up_exceed_tip_content', 'ui_fast_hang_up_exceed_tip_title'))
        keys.update('item_'+str(row['ClassId'])+'_name' for row in self.tables['item'])
        texts, sources = language(keys)
        missing = keys-texts.keys()
        if missing-{'{0}'}: raise ValueError('Missing original acceleration text: '+repr(missing))
        receipt['inputs'].update(sources)
        payload = dict(manifest=self.recovery.apk.version, texts=[dict(key=k, text=v) for k, v in sorted(texts.items())])
        path = folder/'home-fast-hangup-texts.json'
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-fast-hangup-texts').hexdigest()
            +'\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (path, Path(str(path)+'.meta')): receipt['outputs'][item.name] = sha(item.read_bytes())
        path = folder/'home-fast-hangup-rules.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version, tables=self.tables,
            homeSettings=self.home_settings['fields'], globalColors=self.global_colors['fields']),
            ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-fast-hangup-rules').hexdigest()
            +'\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (path, Path(str(path)+'.meta')): receipt['outputs'][item.name] = sha(item.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            raw = (folder/relative).read_bytes()
            if raw.startswith(b'%YAML'): used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', raw.decode('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        for name in ('prepare_home_fast_hangup_native.py', 'prepare_home_currency_windows_native.py'):
            path = ROOT/'scripts'/name; receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()), tables=self.tables, table_sources=self.table_proofs,
            home_settings=self.home_settings, global_colors=self.global_colors, item_images=self.item_images,
            icon_config=self.icon_config, attachment_config=self.attachment_config, icon_manager=self.icon_manager,
            hint_attachment=self.hint_attachment,
            scope='Complete original item acceleration selection window and native item dependencies; actual local owner, progress appearance and Player validation remain pending.')
        (self.work/RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        print('Original acceleration window:', len(receipt['outputs']), 'outputs', flush=True)


def equivalent(left, right):
    if left.replace(b'\r\n', b'\n') == right.replace(b'\r\n', b'\n'): return True
    if not left.startswith(b'%YAML') or not right.startswith(b'%YAML'): return False
    def normalize(value):
        if isinstance(value, dict): return {k: '' if k == 'm_Name' and v is None else normalize(v) for k, v in value.items()}
        if isinstance(value, list): return [normalize(v) for v in value]
        return value
    return normalize(documents(left.decode('utf8'))) == normalize(documents(right.decode('utf8')))


def install(work):
    receipt = json.loads((work/RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild changed native generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT/relative).read_bytes()) != expected: raise ValueError('Acceleration input changed: '+relative)
    for row in receipt['script_bindings'].values():
        if sha((ROOT/row['path']).read_bytes()) != row['sha256'] or sha((ROOT/(row['path']+'.meta')).read_bytes()) != row['meta_sha256']:
            raise ValueError('Acceleration script binding changed: '+row['path'])
    existing = {}
    for meta in (ROOT/'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if not path.is_file(): continue
        key = guid(meta)
        if key in existing: raise ValueError('Existing Unity GUID collision: '+key)
        existing[key] = path
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.exists() else {}
    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work/'gift-assets'/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Staged acceleration resource changed: '+relative)
        if relative.endswith('.meta'): continue
        key = guid(Path(str(source)+'.meta')); target = existing.get(key, DESTINATION/relative)
        same = target.exists() and equivalent(target.read_bytes(), source.read_bytes())
        if target.exists() and not same:
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different original asset: '+str(target))
        if same: reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=key, sha256=sha(target.read_bytes())))
        else: plan.extend(((source, target), (Path(str(source)+'.meta'), Path(str(target)+'.meta'))))
        if target.is_relative_to(DESTINATION):
            for item, staged in ((target, source), (Path(str(target)+'.meta'), Path(str(source)+'.meta'))):
                installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes()) if same else sha(staged.read_bytes())
        existing[key] = target
    catalog = ROOT/'unity/Assets/Game/Resources/Home/OfficialHomeFastHangUpAssetCatalog.asset'
    script = ROOT/'unity/Assets/Game/Runtime/Official/HomeFastHangUpUI/OfficialHomeFastHangUpAssetCatalog.cs'
    roots = receipt['roots']
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script)+'.meta')), type=3),
        m_Name='OfficialHomeFastHangUpAssetCatalog', m_EditorClassIdentifier='',
        selectionPrefab=roots['FurnitureFastHangUpDialog'], itemIconPrefab=roots['HomeCurrencyItemIconDefault'],
        hintPrefab=roots['HomeCurrency'+receipt['hint_attachment']['name']],
        iconConfig=receipt['icon_config'], attachmentConfig=receipt['attachment_config'],
        texts=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-fast-hangup-texts').hexdigest(), type=3),
        rules=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-fast-hangup-rules').hexdigest(), type=3),
        images=[dict(path=row['item']['RawIcon'], assetGuid=row['manifest_asset']['guid'], sprite=row['pointer'])
            for row in receipt['item_images'].values()])
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(dict(MonoBehaviour=fields), Dumper=Dumper, allow_unicode=True, sort_keys=False)
    if catalog.exists() and not equivalent(catalog.read_bytes(), raw.encode('utf8')) and sha(catalog.read_bytes()) != previous.get(catalog.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve changed acceleration catalog')
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(source.read_bytes())
    for folder in [DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        path = metadata(folder, 'DefaultImporter', True); installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    catalog.write_text(raw, 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter')): installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Installed original acceleration window:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--install', action='store_true')
    args = parser.parse_args(); work = validation_directory()
    if args.install: install(work)
    else: HomeFastHangUpBinding(work).build()
