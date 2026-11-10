"""Recover the selected home top-bar subtree, pool prefabs and currency icons."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT, guid, script_id, sha
from prepare_current_partner_native import identity, pointer
from official_manifest import read_asset_manifest
from official_tables import Constructors
from validation_workspace import validation_directory


class HomeTopBarBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('TopBarHandler', 'TopBarItem', 'MePlayerItemUI'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / (name + '.cs')
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/HorizontalLayoutGroupJSZX.cs'
        meta = Path(str(path)+'.meta')
        ref = dict(fileID=11500000, guid=guid(meta), type=3)
        name = 'UGUIJszx.HorizontalLayoutGroupJSZX'
        self.scripts[name] = ref
        self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
        self.class_ids[script_id('UGUIJszx', 'HorizontalLayoutGroupJSZX')] = ref
        self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        recovery = self.recovery
        bundle = 'defaultpackage_assets_prefabs_ui_home.bundle'
        home, = [obj for obj in recovery.apk.objects(bundle) if obj.type.name == 'GameObject'
            and obj.read().m_Name == 'HomeUI'
            and obj.read().m_Component[0].component.read().m_Father.path_id == 0]
        parent, = [child.read().m_GameObject.deref()
            for child in home.read().m_Component[0].component.read().m_Children
            if child.read().m_GameObject.read().m_Name == 'NewPublicTopBarParent']
        from prepare_current_partner_native import documents
        native = recovery.native / 'Prefabs/UI/Home/HomeUI.prefab'
        docs = documents(native.read_text('utf8'))
        root, = [key for key, doc in docs.items() if doc.get('GameObject', {}).get('m_Name') == 'NewPublicTopBarParent']
        recovery.recover('HomeTopBarParent', self.work / 'top-bar-native', bundle=bundle,
            native_relative='Prefabs/UI/Home/HomeUI.prefab', source_root=parent, native_root_id=root)
        config = recovery.apk.asset('scriptableconfig_modules_topbar', '/TopBarManagerConfig.asset')
        fields = recovery.fields(config)
        self.manager_config = dict(source=recovery.apk.provenance(config), identity=identity(config),
            sha256=sha(config.get_raw_data()), fields=fields)
        for key in ('ManagerPrefab', 'TopBarPrefab', 'CurrencyPrefab'):
            obj = pointer(fields[key], config)
            recovery.recover(obj.read().m_Name, self.work / 'top-bar-native',
                bundle=recovery.apk.provenance(obj)['bundle'],
                native_relative='Prefabs/UI/Public/' + obj.read().m_Name + '.prefab')
        recovery.finalize()
        self.empty_strings = []
        self.home_template_pointers = {}

        def restore(source, converted, owner, address=''):
            if isinstance(source, dict) and isinstance(converted, dict):
                for key in source.keys() & converted.keys():
                    if converted[key] is None and (source[key] == '' or source[key] == [] or source[key] == {}):
                        converted[key] = source[key]
                        self.empty_strings.append(dict(original=owner, field=address+'/'+key, value=source[key]))
                    else:
                        restore(source[key], converted[key], owner, address+'/'+key)
            elif isinstance(source, list) and isinstance(converted, list) and len(source) == len(converted):
                for i, (left, right) in enumerate(zip(source, converted)):
                    restore(left, right, owner, address+'/'+str(i))

        result = []
        for native, output, panel, record in recovery.outputs:
            name = output.stem
            target_guid = hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({key: dict(fileID=file_id, guid=target_guid, type=3)
                for key, file_id in panel['objectMap'].items()})
            self.script_guids.add(target_guid)
            for item in (native, Path(str(native)+'.meta')):
                self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
            for key, ref in panel['external'].items():
                self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            for script in panel['scripts']:
                owner = self.original(script['original'])
                restore(recovery.fields(owner), panel['documents'][script['nativeFileID']]['MonoBehaviour'], script['original'])
                if script['type'] == 'UIClickSound':
                    self.sound(recovery.fields(owner)['AkEvent'], owner)
            missing = {'.'.join(filter(None, (s['namespace'], s['type']))) for s in panel['scripts']} - self.scripts.keys()
            if missing:
                raise ValueError('Migrate selected original top-bar scripts: '+repr(sorted(missing)))
            result.append((name, panel))
        cache = recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(scope='Original HomeUI top-bar subtree, original pooled manager/bar/currency prefabs.',
            source=identity(parent), original_parent_detached=True)
        self.currencies = self.currency_sources()
        return result

    def currency_sources(self):
        apk = self.recovery.apk
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
            re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        values, evidence = {}, {}
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset':
                continue
            name = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if name not in ('game_settings', 'item', 'quick_buy'):
                continue
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            values[name] = constructors.table(raw, mapping[name])
            evidence[name] = dict(source=apk.provenance(obj), sha256=sha(raw), constructor=mapping[name])
        manifest = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        currencies = []
        for item_type, key in ((0, 'MoneyId'), (5, 'DiamonId'), (2, 'TiliId')):
            item_id = values['game_settings'][key]
            row, = [row for row in values['item'] if row['ClassId'] == item_id]
            asset, = [asset for asset in manifest if asset['path'].endswith('/'+row['RawIcon']+'.png')]
            self.bind_dynamic_sprite(asset['guid'])
            currencies.append(dict(type=item_type, itemId=item_id, setting=key, item=row,
                icon=self.dynamic_sprites_by_guid[asset['guid']]['pointer'],
                quickBuy=[q for q in values.get('quick_buy', []) if q['ItemId'] == item_id]))
        self.currency_evidence = evidence
        for name in constructors.sources:
            path = code / 'common-source/Common' / (name+'.cs')
            if not path.is_file():
                path = code / 'common-source/Common.LubanGen' / (name+'.cs')
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return currencies

    def build(self):
        PartnerGiftBinding.build(self)
        path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(path.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            item = self.work / 'gift-assets' / relative
            if item.suffix != '.meta' and item.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', item.read_text('utf8')))
        receipt['script_bindings'] = {key: row for key, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()), manager_config=self.manager_config,
            currencies=self.currencies, currency_sources=self.currency_evidence,
            original_empty_value_restorations=self.empty_strings,
            scope='Selected original top-bar native resources. Production home binding and Play Mode validation remain required.')
        for name in ('TopBarHandler', 'TopBarItem', 'MePlayerItemUI', 'CurrencyItemData', 'PlayerItemUIItemIdProvider'):
            current = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source' / (name+'.cs')
            previous = ROOT / '.tools/source-packages/official-code/client-source' / (name+'.cs')
            if current.read_bytes() != previous.read_bytes():
                raise ValueError('Recheck shared original top-bar implementation: '+name)
            receipt['inputs'][current.relative_to(ROOT).as_posix()] = sha(current.read_bytes())
            receipt['inputs'][previous.relative_to(ROOT).as_posix()] = sha(previous.read_bytes())
        for name in ('TopBarManager', 'TopBarManagerConfig', 'IPlayerItemUIHandler', 'PlayerItemUIGetItemIdFactory',
                     'PlayerItemUIHandlerFactory', 'PlayerDefaultItemUIHandler', 'PlayerDiamondItemUIHandler',
                     'PlayerItemQuickyShopHandler', 'PlayerItemUICountProviderFactory',
                     'PlayerItemUIPlayerItemCountProvider', 'PlayerItemUIMainPlayerCountProviderBase'):
            source = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source' / (name+'.cs')
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        for name in ('prepare_home_repair_native.py', 'prepare_home_bottom_bar_native.py', 'prepare_partner_gift_native.py',
                     'prepare_home_top_bar_validation.py', 'fixtures/home_top_bar_native_validation.cs'):
            source = ROOT / 'scripts' / name
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        receipt['runtime_code'] = {}
        for name in ('Official/HomeHudUI/HomeTopBarManager', 'Official/HomeHudUI/OfficialHomeTopBarAssetCatalog',
                     'Offline/HomeUI/OfflineHomeTopBar'):
            for suffix in ('.cs', '.cs.meta'):
                source = ROOT / 'unity/Assets/Game/Runtime' / (name+suffix)
                receipt['runtime_code'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        (self.work / 'home-top-bar-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')


if __name__ == '__main__':
    argparse.ArgumentParser(__doc__).parse_args()
    HomeTopBarBinding(validation_directory()).build()
