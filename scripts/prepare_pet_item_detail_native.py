"""Restore original item-use, ordinary detail, choice and referenced cell prefabs."""
import argparse
import hashlib
import json
from pathlib import Path

from home_native_assets import install_staged_assets, write_staged_assets
from home_native_cache_verification import verify_native_cache
from official_source import OfficialSource
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_current_partner_native import PartnerNativeRecovery, identity, documents, pointer as source_pointer
from prepare_map16_story_source import decode_localization
from prepare_markable_confirm_native import OfficialPopupBinding
from prepare_partner_card_native import ROOT, guid, sha
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory
from home_native_furniture_builder import prepare_native_cache
from official_apk import Apk
from current_component_schemas import CurrentSchemas
from prepare_current_resource_inputs import prepare as prepare_inputs
from convert_current_resource_project import prepare_managed_inputs

ATTACHMENT_TYPES = (10, 13, 27, 29)

BUNDLE = 'defaultpackage_assets_prefabs_ui_itemdetail.bundle'
DEST = ROOT / 'unity/Assets/Official/Home/PetGacha/ItemDetails'
CATALOG = ROOT / 'unity/Assets/Game/Resources/PetGacha/OfficialPetItemDetailAssetCatalog.asset'
RECEIPT = ROOT / 'assets/reference/pet-item-detail-native-source-manifest.json'
SPECS = [(name, BUNDLE, 'Prefabs/UI/ItemDetail/' + name + '.prefab')
    for name in ('ItemUseDetailDialog', 'ItemDetailDialog', 'ItemChooseDialog')]
SPECS += [(name, 'defaultpackage_assets_prefabs_ui_item.bundle', 'Prefabs/UI/Item/' + name + '.prefab')
    for name in ('ItemChooseDialogItem', 'UseResultIcon', 'ItemIconDefault')]
SPECS += [('ItemSourceItem', BUNDLE, 'Prefabs/UI/ItemDetail/Common/ItemSourceItem.prefab')]


def prepare_cache():
    apk = Apk.current()
    try:
        records = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        rules = json.loads((ROOT / 'unity/Assets/Game/Resources/PetGacha/chest-use-preview.json').read_bytes())
        paths = {'Assets/Image/' + row['RawIcon'] + '.png' for row in rules['tables']['item'] if row['RawIcon']}
        bundles = {bundle for _, bundle, _ in SPECS}
        for name in ('ItemIconConfig', 'ItemIconAttachmentConfig', 'GlobalUIConfig', 'GlobalColors', 'HomeSettings'):
            row, = [row for row in records if row['path'].endswith('/' + name + '.asset')]
            bundles.add(row['bundle'])
        attachment, = [row for row in records if row['path'].endswith('/ItemIconAttachmentConfig.asset')]
        bootstrap = ROOT / '.tools/source-packages/current' / apk.version / 'home-item-detail-type-inputs'
        prepare_inputs(bootstrap, [attachment['bundle']])
        prepare_managed_inputs(bootstrap)
        schemas = CurrentSchemas(managed_directory=bootstrap / 'inputs/Managed')
        obj = apk.asset(attachment['bundle'], attachment['path'])
        attachment_fields = schemas.read(obj)
        for kind in ATTACHMENT_TYPES:
            template, = [row for row in attachment_fields['Templates'] if row['Type'] == kind]
            asset, = [row for row in records if row['guid'] == template['GuidProxy']['mAssetGuid']]
            bundles.add(asset['bundle'])
        addresses = {path.removesuffix('.png').casefold() for path in paths}
        bundles.update(row['bundle'] for row in records if row['path'] in paths or row.get('address', '').casefold() in addresses
            or row['path'].startswith('Assets/Image/UI/ItemDetailDialog/Img'))
        selection = hashlib.sha256('\n'.join(sorted(bundles)).encode('utf8')).hexdigest()[:12]
        cache = ROOT / '.tools/source-packages/current' / apk.version / ('home-pet-item-detail-native-' + selection)
        prior = json.loads((cache / 'source-manifest.json').read_bytes()) if (cache / 'source-manifest.json').is_file() else {}
        available = {row['name'] for row in prior.get('inputs', []) if 'name' in row}
        if not cache.exists():
            prepare_native_cache(cache, sorted(bundles))
        elif not bundles.issubset(available):
            raise ValueError('Retain and inspect incomplete item-detail conversion inputs: ' + str(cache))
        verify_native_cache(cache, [path for _, _, path in SPECS], apk.version)
        return cache
    finally:
        apk.reader.close()


def enum_names(path):
    import re
    body = path.read_text('utf8').split('{', 1)[1].rsplit('}', 1)[0]
    result, value = {}, 0
    for entry in body.split(','):
        if not entry.strip(): continue
        match = re.fullmatch(r'\s*(\w+)(?:\s*=\s*(\d+))?\s*', entry)
        if not match: raise ValueError('Decode the original enum expression: ' + str(path) + ': ' + entry)
        if match[2] is not None: value = int(match[2])
        result[value] = match[1]
        value += 1
    return result


class PetItemDetailBinding(OfficialPopupBinding):
    panel_name, bundle, prefab, identity = 'ItemUseDetailDialog', BUNDLE, SPECS[0][2], 'pet-item-detail'
    literal_keys = ('{0}', '{0}/{1}', '+1', '+10', '+100', '+{0}', '-1', '-10', '-100')
    required_keys = ('ui_ItemDetailDialogUseResult_confirmed_tip', 'ui_ItemDetailDialogUseResult_optional_tip',
        'ui_ItemDetailDialogUseResult_random_tip', 'ui_choos_dialog_title', 'ui_choos_dialog_tip',
        'ui_itemChoose_randomTitle', 'ui_itemChoose_randomTip', 'ui_fast_hang_up_max_count_tip',
        'ui_common_contrast_1', 'ui_common_item_not_enough', 'ui_ItemIcon_count_key')
    supplementary_scripts = {name: 'ItemDetailUI/' + name + '.cs' for name in (
        'ItemDetailDialog', 'ItemDetailDialogUseResult', 'ItemDetailDialogAddition',
        'ItemSourceGo', 'ItemSourceItem', 'ItemDecomposContent', 'OutputAdditionDetailButton', 'UseResultIcon',
        'ItemUseDetailDialog', 'ItemDetailDialogUseContent', 'ItemChooseDialog', 'ItemChooseDialogItem')}
    supplementary_scripts.update({'GMFunction.ItemDetailGmTools': 'ItemDetailUI/ItemDetailGmTools.cs',
        'LoopVerticalScrollWithMaxHeight': 'SkillsUI/LoopVerticalScrollWithMaxHeight.cs',
        'UIToggleObject': 'SkillsUI/UIToggleObject.cs', 'ShowRemainTimeInfo': 'ProfessionUI/ShowRemainTimeInfo.cs',
        'SetUseCountSlider': 'HomeCountControlsUI/SetUseCountSlider.cs',
        'AwardPanelLayoutElement': 'WorldUI/AwardPanelLayoutElement.cs',
        'UGUIJszx.VerticalLayoutGroupJSZX': 'WorldUI/VerticalLayoutGroupJSZX.cs'})
    source_paths = tuple('client-source/' + name + '.cs' for name in (
        'ItemDetailDialog', 'ItemDetailDialogUseResult', 'ItemDetailDialogUseContent', 'ItemUseDetailDialog',
        'ItemDetailDialogFactory', 'ItemChooseDialog', 'ItemChooseDialogItem', 'UseResultIcon', 'UIUtils',
        'ItemUseEffectOpenChest', 'SkillOptionSortUtils', 'AwardPanelLayoutElement', 'SetUseCountSlider'))
    runtime_paths = ('Official/ItemDetailUI/ItemUseBindings.cs', 'Official/ItemDetailUI/ItemDetailUiGray.cs',
        'Official/ItemDetailUI/Zjcs.Official.ItemDetailUI.asmdef', 'Official/WorldUI/AwardPanelLayoutElement.cs',
        'Official/ProfessionUI/ItemIconAttachmentType.cs',
        'Offline/GachaUI/OfficialPetItemDetailAssetCatalog.cs')

    def friendship_templates(self):
        self.records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.resources, self.dynamic_sources = [], {}
        self.icon_config_source = self.config('ItemIconConfig')
        self.attachment_source = self.config('ItemIconAttachmentConfig')
        self.attachment_assets = []
        specs = list(SPECS)
        for kind in ATTACHMENT_TYPES:
            template, = [row for row in self.attachment_source['fields']['Templates'] if row['Type'] == kind]
            asset, = [row for row in self.records if row['guid'] == template['GuidProxy']['mAssetGuid']]
            obj, = [pointer.deref() for path, pointer in self.recovery.apk.container(asset['bundle'])
                if path.casefold() == asset['path'].casefold() and pointer.deref().type.name == 'GameObject']
            name = obj.read().m_Name
            self.attachment_assets.append(dict(asset=asset, name=name, type=kind))
            specs.append((name, asset['bundle'], asset['path'].removeprefix('Assets/')))
        for name, bundle, path in specs:
            self.recovery.recover(name, self.work / 'original', bundle=bundle, native_relative=path)
        self.recovery.finalize()
        self.empty_restorations = []
        result, selections = [], {}
        # Register each original root before walking any cross-prefab pointer.
        for (name, _, _), (native, _, panel, record) in zip(specs, self.recovery.outputs):
            key, = [key for key, file_id in panel['objectMap'].items() if file_id == panel['root']]
            token = hashlib.md5(('zjcs:' + self.identity + ':' + name).encode()).hexdigest()
            pointer = dict(fileID=panel['root'], guid=token, type=3)
            self.prefab_object_pointers[key] = pointer
            source_guid = guid(Path(str(native) + '.meta'))
            self.prefab_references[source_guid] = token
            self.redirects[source_guid] = pointer
            self.script_guids.add(token)
        for (name, _, _), (native, _, panel, record) in zip(specs, self.recovery.outputs):
            self.prepare_popup_template(native, panel, record)
            if panel['unresolved']:
                raise ValueError('Resolve all original item-detail prefab pointers: ' + repr(panel['unresolved']))
            selections[name] = dict(source=record['originalRoot'], mapped_objects=len(panel['objectMap']))
            result.append((name, panel))
        self.template_selection = selections
        self.dynamic()
        return result

    def config(self, name):
        row, = [row for row in self.records if row['path'].endswith('/' + name + '.asset')]
        obj = self.recovery.apk.asset(row['bundle'], row['path'])
        native = self.native / row['path'].removeprefix('Assets/')
        if not native.is_file(): raise ValueError('Recover the original dynamic configuration: ' + row['path'])
        for path in (native, Path(str(native) + '.meta')):
            relative = path.relative_to(self.cache / self.recovery.export_name).as_posix()
            if sha(path.read_bytes()) != self.checked[relative]: raise ValueError('Original config differs: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[relative]
        token = guid(Path(str(native) + '.meta'))
        self.objects[token] = obj
        fields = self.recovery.fields(obj)
        self.dynamic_sources[name] = dict(source=self.recovery.apk.provenance(obj), raw_sha256=sha(obj.get_raw_data()), fields=fields)
        return dict(obj=obj, fields=fields, native=native, guid=token)

    def sprite(self, path):
        address = 'Assets/Image/' + path
        row, = [row for row in self.records if row['path'].casefold() == (address + '.png').casefold()
            or row.get('address', '').casefold() == address.casefold()]
        obj, = [pointer.deref() for name, pointer in self.recovery.apk.container(row['bundle'])
            if name.casefold() == row['path'].casefold() and pointer.deref().type.name == 'Sprite']
        key, sub_id = self.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(key['data[' + str(index) + ']'], '08x')[::-1] for index in range(4))
        if render_guid != row['guid']: raise ValueError('Original sprite GUID differs: ' + path)
        name = self.name_key(obj.read().m_Name)
        candidates = []
        for stem in [name] + [name + str(index) for index in range(100)]:
            for native in self.by_name.get(stem, []):
                if native.suffix != '.asset': continue
                data = documents(native.read_text('utf8'))
                if len(data) == 1 and next(iter(data.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: sub_id}:
                    candidates.append(native)
        native, = candidates
        token = guid(Path(str(native) + '.meta'))
        self.objects[token] = obj
        pointer = self.load(token)
        self.resources.append(dict(path='Assets/Image/' + path, assetGuid=row['guid'], asset=pointer))
        self.dynamic_sources[path] = dict(source=self.recovery.apk.provenance(obj), raw_sha256=sha(obj.get_raw_data()), asset=row)
        return pointer

    def dynamic(self):
        self.icon_config = self.load(self.icon_config_source['guid'])
        self.attachment_config = self.load(self.attachment_source['guid'])
        ui = self.config('GlobalUIConfig')
        native_fields, = [doc['MonoBehaviour'] for doc in documents(ui['native'].read_text('utf8')).values() if 'MonoBehaviour' in doc]
        for field in ('GrayMat', 'GrayFontMat'):
            self.learn({field: ui['fields'][field]}, {field: native_fields[field]}, ui['obj'])
        self.gray = {field: self.load(native_fields[field]['guid']) for field in ('GrayMat', 'GrayFontMat')}
        colors = self.config('GlobalColors')['fields']
        home = self.config('HomeSettings')['fields']
        preview_path = ROOT / 'unity/Assets/Game/Resources/PetGacha/chest-use-preview.json'
        preview_receipt = ROOT / 'assets/reference/pet-score-chest-preview-source-manifest.json'
        prior = json.loads(preview_receipt.read_bytes())
        if sha(preview_path.read_bytes()) != prior['outputs'][preview_path.relative_to(ROOT).as_posix()]:
            raise ValueError('Existing original chest preview payload differs')
        for relative, expected in prior['inputs'].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Existing original chest rule source differs: ' + relative)
            self.inputs[relative] = expected
        preview = json.loads(preview_path.read_bytes())
        if preview['manifest'] != self.recovery.apk.version: raise ValueError('New item detail inputs must share their original selection')
        self.inputs.update({path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (preview_path, preview_receipt)})
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        constructors = Constructors(code)
        tables, proofs = {}, []
        for name, kind in (('game_settings', 'TbGameSettings'), ('item_source', 'TbItemSourceInfo'),
                           ('item_merge', 'TbItemMergeInfo'), ('item_count_limit', 'TbItemCountLimitInfo')):
            obj = self.recovery.apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = constructors.table(raw, kind)
            proofs.append(dict(name=name, source=self.recovery.apk.provenance(obj), payload_sha256=sha(raw), raw_sha256=sha(obj.get_raw_data())))
        ids = {row['ClassId'] for row in preview['tables']['item']}
        manager = self.config('ItemIconManagerConfig')
        manager_fields = manager['fields']
        if manager_fields['IdList'] or manager_fields['IdPrefabList']:
            raise ValueError('Inspect original item-icon ID factory overrides')
        if {row['ItemType'] for row in preview['tables']['item']} & set(manager_fields['TypeList']):
            raise ValueError('Recover the original specialized item-icon templates for these candidates')
        default = source_pointer(manager_fields['DefPrefab'], manager['obj'])
        expected = self.template_selection['ItemIconDefault']['source']
        if identity(default) != expected:
            raise ValueError('Original item-icon factory points to a different default prefab')
        settings = tables['game_settings']
        selected_tables = dict(preview['tables'])
        selected_tables['game_settings'] = {key: settings[key] for key in (
            'ItemBatchUseMinCount', 'ItemBatchUseMaxCount', 'GameOpenChestOnceMaxCount',
            'SkillTypes', 'SkillPieceTypes', 'IgnoreSourceItemIdList', 'OutputAdditionSetting')}
        selected_tables['item_source'] = [row for row in tables['item_source'] if row['Id'] in ids]
        selected_tables['item_merge'] = [row for row in tables['item_merge'] if row['ProductId'] in ids]
        selected_tables['item_count_limit'] = [row for row in tables['item_count_limit'] if row['ClassId'] in ids]
        self.rule_data = dict(manifest=self.recovery.apk.version, tables=selected_tables,
            global_colors={key: value for key, value in colors.items() if not key.startswith('m_')},
            home_settings={key: home[key] for key in ('mFastItemMaxCountMessageColdDown',)})
        self.dynamic_sources['tables'] = proofs
        for row in selected_tables['item']:
            if row['RawIcon']: self.sprite(row['RawIcon'])
        quality_path = code / 'common-source/Common/Quality.cs'
        qualities = enum_names(quality_path)
        self.quality_names = {row['Quality']: qualities[row['Quality']] for row in selected_tables['item']}
        for quality in self.quality_names.values(): self.sprite('UI/ItemDetailDialog/Img' + quality + '_PopUps')
        self.inputs[quality_path.relative_to(ROOT).as_posix()] = sha(quality_path.read_bytes())
        for name in constructors.sources:
            path = code / 'common-source/Common' / (name + '.cs')
            if path.is_file(): self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())

    def build(self):
        folder, receipt = super().build()
        rows = self.rule_data['tables']['item']
        keys = {'item_' + str(row['ClassId']) + suffix for row in rows for suffix in ('_name', '_desc', '_func_desc')}
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        enum_path = code / 'common-source/Common/ItemType.cs'
        types = enum_names(enum_path)
        mandatory = {'item_' + str(row['ClassId']) + '_name' for row in rows}
        mandatory.update('ItemType.' + types[row['ItemType']] for row in rows)
        mandatory.update('Quality.' + name for name in self.quality_names.values())
        self.rule_data['item_type_names'] = {str(row['ItemType']): types[row['ItemType']] for row in rows}
        texts, proof = decode_localization(self.recovery.apk, keys | mandatory, mandatory)
        text_path = folder / (self.identity + '-texts.json')
        ui_texts = json.loads(text_path.read_bytes())['texts']
        combined = {row['key']: row['text'] for row in ui_texts}; combined.update(texts)
        text_path.write_text(json.dumps(dict(texts=[dict(key=key, text=value) for key, value in sorted(combined.items())]),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        receipt['outputs'][text_path.name] = sha(text_path.read_bytes())
        rules_token = hashlib.md5(b'zjcs:pet-item-detail:rules').hexdigest()
        receipt['outputs'].update(write_staged_assets([(Path('pet-item-detail-rules.json'), rules_token,
            (json.dumps(self.rule_data, ensure_ascii=False, indent=2) + '\n').encode('utf8'))], folder))
        receipt['rules'] = dict(fileID=4900000, guid=rules_token, type=3)
        receipt['icon_config'] = self.remapped_pointer(receipt, self.icon_config)
        receipt['attachment_config'] = self.remapped_pointer(receipt, self.attachment_config)
        receipt['gray'] = {key: self.remapped_pointer(receipt, value) for key, value in self.gray.items()}
        receipt['images'] = [dict(row, asset=self.remapped_pointer(receipt, row['asset'])) for row in self.resources]
        receipt['images'].extend(dict(path=row['asset']['path'], assetGuid=row['asset']['guid'],
            asset=receipt['roots'][row['name']]) for row in self.attachment_assets)
        receipt['dynamic_sources'] = self.dynamic_sources
        receipt['item_localization'] = proof
        receipt['inputs'][enum_path.relative_to(ROOT).as_posix()] = sha(enum_path.read_bytes())
        receipt['inputs'][Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = list((runtime / 'Official/ItemDetailUI').glob('*.cs*'))
        paths += [runtime / path for path in ('Official/HomeCountControlsUI/SetUseCountSlider.cs',
            'Official/HomeCountControlsUI/SetCountSlider.cs', 'Offline/GachaUI/OfficialPetItemDetailAssetCatalog.cs.meta')]
        receipt['runtime_bindings'].update({path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in paths})
        return folder, receipt

    def remapped_pointer(self, receipt, pointer):
        tokens = {guid(self.work / 'gift-assets' / relative) for relative in receipt['outputs'] if relative.endswith('.meta')}
        # OfficialPopupBinding namespaces every emitted native asset GUID.
        token = hashlib.md5(('zjcs:' + self.identity + ':asset:' + pointer['guid']).encode()).hexdigest()
        if token not in tokens: raise ValueError('Dynamic resource is outside the original native closure: ' + str(pointer))
        return dict(pointer, guid=token)


def prepare_metadata():
    root = ROOT / 'unity/Assets/Game/Runtime/Official/ItemDetailUI'
    for name in ('ItemUseBindings', 'ItemUseDetailDialog', 'ItemDetailDialogUseContent',
                 'ItemChooseDialog', 'ItemChooseDialogItem', 'ItemDetailUiGray'):
        metadata(root / (name + '.cs'))
    metadata(ROOT / 'unity/Assets/Game/Runtime/Offline/GachaUI/OfficialPetItemDetailAssetCatalog.cs')


def install(work, folder, receipt):
    previous = json.loads(RECEIPT.read_bytes()).get('outputs', {}) if RECEIPT.is_file() else {}
    receipt['outputs'] = install_staged_assets(work, folder, receipt['outputs'], DEST,
        'pet-item-detail', previous_outputs=previous)
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/GachaUI/OfficialPetItemDetailAssetCatalog.cs'
    names = dict(UseDetail='ItemUseDetailDialog', Detail='ItemDetailDialog', Choose='ItemChooseDialog',
        ChooseItem='ItemChooseDialogItem', ResultIcon='UseResultIcon', ItemSourceItem='ItemSourceItem', ItemIcon='ItemIconDefault')
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name=CATALOG.stem, m_EditorClassIdentifier='',
        Texts=receipt['texts'], Rules=receipt['rules'], IconConfig=receipt['icon_config'],
        AttachmentConfig=receipt['attachment_config'], GrayMaterial=receipt['gray']['GrayMat'],
        GrayFontMaterial=receipt['gray']['GrayFontMat'], Images=receipt['images'],
        **{key: receipt['roots'][name] for key, name in names.items()})
    staged = work / 'catalog-assets'
    outputs = write_staged_assets([(Path(CATALOG.name), hashlib.md5(b'zjcs:pet-item-detail:catalog').hexdigest(),
        {11400000: {'MonoBehaviour': fields}})], staged)
    receipt['outputs'].update(install_staged_assets(work, staged, outputs, CATALOG.parent,
        'pet-item-detail-catalog', previous_outputs=previous))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def check():
    receipt = json.loads(RECEIPT.read_bytes())
    for section in ('inputs', 'outputs', 'runtime_bindings'):
        for relative, expected in receipt[section].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Original item-detail input or output differs: ' + relative)
    for name, row in receipt['script_bindings'].items():
        path = ROOT / row['path']
        meta = Path(str(path) + '.meta')
        if guid(meta) != row['pointer']['guid'] or sha(path.read_bytes()) != row['sha256'] or sha(meta.read_bytes()) != row['meta_sha256']:
            raise ValueError('Original item-detail controller differs: ' + name)
    print('Original item-detail native closure and source verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.check:
        check()
        raise SystemExit(0)
    prepare_metadata()
    cache = prepare_cache()
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
    try:
        work = validation_directory()
        folder, receipt = PetItemDetailBinding(work, recovery=recovery).build()
        if args.install:
            install(work, folder, receipt)
        (work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    finally:
        recovery.apk.reader.close()
