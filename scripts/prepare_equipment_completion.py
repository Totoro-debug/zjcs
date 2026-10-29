"""Migrate pinned equipment detail, suit/blessing tips and fragment UI references."""
import hashlib
import json
import re
import struct
from pathlib import Path
import prepare_cooking_effects as common
from prepare_cooking_effects import CookingExporter
from prepare_official_panels import unique_sibling_paths
from official_ui_layout_fields import enrich_layout_node
from official_tables import Constructors
from official_resource_manifest import write_resources
from npc_travel_language import language
from official_apk import ROOT

ART = ROOT / 'assets/official'
CLASSES = {'EquipPropPanel', 'EquipMutillPropItem', 'EquipSpecialPropItem',
           'PlayerPropItem', 'PlayerGemPropItem', 'PlayerGemBlessPropItem',
           'EquipSuitPropsGroupUI', 'EquipSuitPropItem', 'EquipSuitTipUI',
           'EquipSuitGridItem', 'BlessPropBtn', 'PopTipBlessProp', 'PopTipNormal',
           'PopTipEquipSuit', 'RankIsMatchDesc', 'EquipChooseDialog',
           'EquipChooseDialogItem', 'CommonEquipPieceUseDetailDialog',
           'ItemUseDetailDialog', 'ItemDetailDialog', 'ItemDetailDialogUseContent', 'PopupWindow', 'ConfirmDialog',
           'EquipDetailDialog', 'EquipLockUI', 'EquipSelecDetailDialog', 'CharacterPanelEquipComparePanel',
           'SkillDetailDialog', 'SkillPanelPropPanel', 'SkillStarInfo',
           'SkillDetailDialogSkillTagGridLayout', 'SkillTagGo', 'SkillTagItem',
           'SkillDetailDialogRangeUI', 'SkillDetailDialogRangeUIItem',
           'BlessLevelTextViewChanger', 'TMPEffect', 'IntroductionPopBtn', 'ItemIcon',
           'ItemIconAttachment', 'ItemIconAttachmentHint', 'ItemIconAttachmentCell',
           'PopTipMutilDesc', 'TipInfo', 'AutoAlignForSingleMultiLineText'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class EquipmentExporter(CookingExporter):
    def __init__(self):
        common.CLASSES.update(CLASSES - {'AutoAlignForSingleMultiLineText'})
        super().__init__()
        self.layout_inputs = []

    def object_path(self,ptr):
        go = ptr.deref().read(check_read=False)
        transform = next(p.component.deref().read(check_read=False) for p in go.m_Component if p.component.type.name=='RectTransform')
        names = []
        while transform:
            name = transform.m_GameObject.deref().read(check_read=False).m_Name
            parent = transform.m_Father.deref().read(check_read=False) if transform.m_Father.path_id else None
            if parent:
                index = next(i for i,p in enumerate(parent.m_Children) if p.path_id==transform.object_reader.path_id)
                ordinal = sum(p.deref().read(check_read=False).m_GameObject.deref().read(check_read=False).m_Name==name for p in parent.m_Children[:index])
                if ordinal:name+=' ('+str(ordinal)+')'
            names.insert(0,name);transform=parent
        return '/'+ '/'.join(names)

    def ui_node(self, obj, path=''):
        node = super().ui_node(obj, path)
        if node is not None:
            for part in obj.read().m_Component:
                comp = part.component.deref()
                if comp.type.name == 'MonoBehaviour':
                    cls, raw, _ = self.mono(comp)
                    enrich_layout_node(self, node, comp, cls, raw)
                    if cls == 'TextMeshProUGUI':
                        length = struct.unpack_from('<i',raw,88)[0]
                        aligned = (92+length+3)&~3
                        shared = struct.unpack_from('<i',raw,aligned+28)[0]
                        materials = struct.unpack_from('<i',raw,aligned+44+12*shared)[0]
                        style = aligned+12*(shared+materials)
                        enabled,mode = struct.unpack_from('<2i',raw,style+68)
                        colors = list(struct.unpack_from('<16f',raw,style+76))
                        if enabled not in (0,1) or mode not in range(4) or any(not 0<=v<=1 for v in colors):
                            raise ValueError('Unexpected original TMP vertex gradient: '+node['path'])
                        node['text']['vertex_gradient'] = dict(enabled=bool(enabled),mode=mode,
                            top_left=colors[:4],top_right=colors[4:8],bottom_left=colors[8:12],bottom_right=colors[12:])
                    if cls == 'AutoAlignForSingleMultiLineText':
                        # SafeAction is not Unity-serializable. The generator
                        # follows it incorrectly; use the four pinned fields.
                        if len(raw) != 64:raise ValueError('Original AutoAlign field extent changed: '+str(len(raw)))
                        def ptr(at):
                            file_id,path_id = struct.unpack_from('<iq',raw,at)
                            return dict(m_FileID=file_id,m_PathID=path_id)
                        fields = dict(mTxt=ptr(32),mTmp=ptr(44),mSingleLineAlignment=struct.unpack_from('<i',raw,56)[0],mMultiLineAlignment=struct.unpack_from('<i',raw,60)[0])
                        node.setdefault('original_bindings',{})[cls] = self.convert(fields,comp)
                        self.receipts.append(dict(self.apk.provenance(comp),script=cls,sha256=hashlib.sha256(raw).hexdigest(),operation='Four original serialized fields; exact 64-byte extent; nonserializable SafeAction excluded'))
        return node


def build():
    e = EquipmentExporter()
    specs = {
        'equip_detail': ('character', 'CharacterPanelEquipDetailPanel'),
        'equip_compare': ('character', 'CharacterPanelEquipComparePanel'),
        'equip_select_detail': ('promotepanel', 'EquipSelecDetailDialog'),
        'equip_term': ('itemdetail', 'EquipMutillPropItem'),
        'equip_piece_detail': ('itemdetail', 'CommonEquipPieceUseDetailDialog'),
        'equip_choose': ('equip', 'EquipChooseDialog'),
        'equip_choose_row': ('equip', 'EquipChooseDialogItem'),
        'equip_bless_tip': ('tips', 'PopTipBlessProp'),
        'equip_suit_tip': ('tips', 'PopTipEquipSuit'),
        'equip_piece_confirm': ('confirmdialog', 'ConfirmDialog'),
        'equip_term_skill_detail': ('itemdetail', 'SkillDetailDialog'),
        'equip_choose_icon': ('item', 'ItemIconDefault'),
        'equip_level_tip': ('tips', 'PopTipMutilDesc'),
    }
    panels = {}
    for key, (bundle, name) in specs.items():
        print('Converting original equipment prefab:', name, flush=True)
        panels[key] = e.ui('prefabs_ui_' + bundle, '/' + name + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    config_object = e.apk.asset('scriptableconfig_modules_itemicon', '/ItemIconAttachmentConfig.asset')
    config = e.schemas.read(config_object)
    attachment = next(entry for entry in config['Templates'] if entry['Type'] == 13)
    guid = attachment['GuidProxy']['mAssetGuid']
    raw_manifest = e.apk.archive.read('assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes')
    paths = {m[2].decode():m[1].decode() for m in re.finditer(rb'(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})',raw_manifest)}
    asset_path = paths[guid]
    asset_key = 'defaultpackage_' + asset_path.replace('/','_').lower()
    bundle = max((b for b in e.apk.index if asset_key.startswith(b.removesuffix('.bundle'))),key=len)
    suffix = '/' + asset_path.split('Assets/Prefabs/UI/Item/',1)[1]
    panels['equip_piece_owned_hint'] = e.ui(bundle,suffix)
    panels['equip_piece_owned_hint']['source'].update(attachment_type=13,guid=guid,asset_path=asset_path,
        config=dict(source=e.apk.provenance(config_object),sha256=hashlib.sha256(config_object.get_raw_data()).hexdigest(),template=attachment))
    for obj in e.apk.objects('prefabs_ui_tips'):
        if obj.type.name == 'MonoBehaviour' and e.mono(obj)[0] == 'PopTipMutilDesc':
            fields = e.schemas.read(obj)
            target = e.pointer(fields['mTipInfoPrefab'],obj)
            if target.type.name=='GameObject':
                target = next(part.component.deref() for part in target.read().m_Component
                              if part.component.type.name=='MonoBehaviour' and e.mono(part.component.deref())[0]=='TipInfo')
            panels['equip_level_tip_row'] = e.template(target)
            break
    for obj in e.apk.objects('prefabs_ui_character'):
        if obj.type.name != 'MonoBehaviour' or e.mono(obj)[0] != 'EquipPropPanel':
            continue
        fields = e.schemas.read(obj)
        for field, key in [('GemPropItemPrefab', 'equip_detail_gem')]:
            if key not in panels and fields.get(field, {}).get('m_PathID'):
                panels[key] = e.template(e.pointer(fields[field], obj))
    def walk(node):
        yield node
        for child in node.get('children',[]):yield from walk(child)
    for panel in panels.values():
        nodes = {n['path']:n for n in walk(panel['root'])}
        for node in nodes.values():
            detail = node.get('original_bindings',{}).get('EquipDetailDialog',{})
            for path in detail.get('EquipBlessViewStateSwitcher',{}).get('mBlessTxt',[]):
                gradient = nodes[path]['text']['vertex_gradient']
                if any(gradient[key]!=[1,1,1,1] for key in ['top_left','top_right','bottom_left','bottom_right']):
                    raise ValueError('Blessing text requires a nonidentity vertex-color adapter: '+path)
    # The original gap table has no DevelopType.Equip rows; retain the complete
    # table and parser behavior rather than invent an equipment gap constraint.
    raw_path = ROOT / '.tools/source-packages/official-data/system_level_gap_limit.g.bytes'
    table_obj = e.apk.asset('config_luban', '/system_level_gap_limit.g.bytes')
    table_raw = table_obj.read().m_Script
    if isinstance(table_raw,str):table_raw = table_raw.encode('utf8','surrogateescape')
    if raw_path.exists() and raw_path.read_bytes()!=table_raw:
        raise ValueError('Cached gap table differs from the pinned original asset')
    rows = Constructors().table(table_raw, 'TbSystemLevelGapLimitInfo')
    fidelity = json.loads((ART / 'fidelity-rules.json').read_text('utf8'))
    fidelity['system_level_gap_limit'] = rows
    enum_path = ROOT / '.tools/source-packages/official-code/common-source/Common/PropType.cs'
    prop_names = {int(value):name for name,value in re.findall(r'(\w+)\s*=\s*(\d+)',enum_path.read_text('utf8'))}
    fidelity['equipment_prop_names'] = [dict(PropType=value,Name=name) for value,name in prop_names.items()]
    gradient_obj = e.apk.asset('textmesh_pro_assets_color_gradient_presets','/GreenRainbow.asset')
    gradient_raw = gradient_obj.get_raw_data()
    length = struct.unpack_from('<i',gradient_raw,28)[0]
    offset = (32+length+3)&~3
    if gradient_raw[32:32+length]!=b'GreenRainbow' or len(gradient_raw)!=offset+68:
        raise ValueError('Unexpected original GreenRainbow gradient')
    mode = struct.unpack_from('<i',gradient_raw,offset)[0]
    colors = list(struct.unpack_from('<16f',gradient_raw,offset+4))
    fidelity['equipment_green_rainbow'] = dict(mode=mode,top_left=colors[:4],top_right=colors[4:8],bottom_left=colors[8:12],bottom_right=colors[12:])
    rules = json.loads((ART / 'rules.json').read_text('utf8'))
    keys = {'ui_merge_text', 'GemItem.Rank', 'ui_equip_piece_merge_unselected_tip',
            'ui_equip_piece_merge_count_not_enough_tip', 'ui_equip_piece_merge_owned_tip',
            'ui_equip_piece_merge_already_owned_item_tip', 'EquipPromotePanel.LimitGapLevel',
            'function_limit_bless_level', 'function_limit_season', 'ui_prop_mutex_desc',
            'ui_bless_prop_not_effect', 'ui_daily_equip_suit_extra_rank_desc',
            'ui_equip_attribuate_kNormalWithBlessKey', 'ui_equip_attribuate_kUnEffectKey',
            'ui_equip_attribuate_kUnEffectAllKey', 'ui_equip_attribuate_prop_ PropArg',
            'confirmation', 'confirm', 'cancel', 'common_delimiter', 'ui_space_before_value',
            'ui_daily_dungeon_equip_suit_not_bless_red_desc', 'ui_equip_attribuate_suit',
            'ui_equip_special_prop_skill_type', 'ui_equip_attribuate_prop_NormalWithBlessKey',
            'ui_equip_attribuate_prop_ UnEffectKey', 'ui_equip_attribuate_prop_ MutexKey',
            'ui_equip_attribuate_prop_ OneNormalWithBlessKey',
            'ui_equip_attribuate_prop_ OneUnEffectKey', 'ui_equip_attribuate_prop_ OneRawPropKey'}
    keys.update(['ui_daily_dungeon_equip_level_with_bless_desc','ui_daily_dungeon_equip_level_not_bless_desc',
                 'ui_daily_dungeon_equip_level_not_bless_red_desc','ui_daily_dungeon_equip_level_extra_prop_desc',
                 'ui_daily_dungeon_dialog_equip_bless_title','ui_blessing_level','ui_blessing_equip_level_tip_0',
                 'ui_blessing_equip_level_tip_1','ui_blessing_equip_level_tip_3','ui_blessing_player_level_tip_2',
                 'ui_bless_raw_power_desc','ui_bless_bless_power_desc','ui_bless_bless_power_un_effect_desc'])
    keys.add('ui_daily_dungeon_equip_level_extra_rank_desc')
    for enum in ['Sword','Wand','Shield','Glove','Book','Ball','Helmet','Clothes','Boot']:keys.add('ItemType.'+enum)
    for row in fidelity.get('astrological_season_config',[]):keys.add('astrological_season_%d_name' % row['Season'])
    equipment = json.loads((ART / 'equipment-rules.json').read_text('utf8'))['tables']
    for row in equipment['equip_attributes_passive_skill']:
        keys.add('equip_attributes_%d_desc' % row['Id'])
        for prop in row['Props']:keys.add('%d.%s' % (row['Id'],prop_names[prop['Key']]))
    for row in rules['equip_piece_merge']:
        keys.update(['item_' + str(row['Id']) + '_func_desc', 'item_' + str(row['Id']) + '_desc'])
    for row in json.loads((ART / 'equipment-rules.json').read_text('utf8'))['tables']['equip_suit']:
        keys.add('equip_suit_' + str(row['SuitId']))
        for count in row['SuitAttributes']:keys.add('equip_suit_des_' + str(count))
    keys.add('ItemType.CommonEquipPiece')
    texts, language_sources = language(keys | e.keys)
    def localize(node):
        key = node.get('localization_key')
        if key in texts and 'text' in node:node['text']['value'] = texts[key]
        for child in node['children']:localize(child)
    for panel in panels.values():localize(panel['root'])
    for name, updates in [('ui.json', panels), ('fidelity-rules.json', fidelity),
                          ('fidelity-texts.json', texts)]:
        value = json.loads((ART / name).read_text('utf8'));value.update(updates);e.save(name, value)
    code = ROOT / '.tools/source-packages/official-code'
    sources = {}
    for folder, names in [('client-source', CLASSES | {'EquipChooseDialog', 'EquipPropPanel',
                          'EquipSpecialPropItem', 'EquipMutillPropItem'}),
                          ('common-source/Common', {'PlayerEquipDataExt', 'BattleFormulaHandler',
                          'EquipTypeInfoParser', 'SystemLevelLimitInfoParser',
                          'SystemLevelGapLimitInfoParser', 'LevelBlessInfoParser',
                          'PlayerBlessLevelUtils', 'LanguageUtils', 'Formula', 'FightStatusBaseComponent',
                          'FightStatusComponentImplement', 'PassiveSkillData', 'EquipPropChunck'})]:
        for name in names:
            path = code / folder / (name + '.cs')
            if path.exists():sources[path.relative_to(ROOT).as_posix()] = sha(path)
    for name in ['NumberCultureFormatter_CN','CultureNumberFormatterBase','NumberFormatHelper','CultureNumberArg']:
        path = code/'framework-firstpass-source/Framework'/(name+'.cs')
        sources[path.relative_to(ROOT).as_posix()] = sha(path)
    path = code/'common-source/EquipItemComparer.cs'
    sources[path.relative_to(ROOT).as_posix()] = sha(path)
    reference = ROOT / 'assets/reference/equipment-completion';reference.mkdir(exist_ok=True)
    record = dict(manifest='854_156383', templates={k:v['source'] for k,v in panels.items()},
                  serialized_components=e.receipts, layout_fields=e.layout_inputs, resources=e.records,
                  tmp_schema_sha256=sha(ROOT/'scripts/unity_schema_types.cs'),
                  tmp_metadata_sha256=sha(ROOT/'.tools/source-packages/official-code/global-metadata.dat'),
                  green_rainbow=dict(source=e.apk.provenance(gradient_obj),sha256=hashlib.sha256(gradient_raw).hexdigest(),consumed_bytes=len(gradient_raw)),
                  original_code_sha256={**sources,enum_path.relative_to(ROOT).as_posix():sha(enum_path)},
                  language_sha256=language_sources, table=dict(source=e.apk.provenance(table_obj),
                  sha256=hashlib.sha256(table_raw).hexdigest(), constructor='TbSystemLevelGapLimitInfo', consumed_bytes=len(table_raw)),
                  operation='Exact original prefab trees, referenced rows, serialized fields and original Chinese text; no replacement art',
                  output_sha256={name:sha(ART/name) for name in ['ui.json','fidelity-rules.json','fidelity-texts.json']})
    (reference/'source-manifest.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n','utf8')
    path = ROOT/'assets/reference/official-city/source-manifest.json'
    manifest = json.loads(path.read_text('utf8'));manifest['resources'].update(e.records)
    manifest['data_sha256'].update(record['output_sha256'])
    manifest['panel_prefabs'].update(record['templates'])
    path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),'utf8');write_resources(manifest)
    print('Original equipment prefabs:', len(panels), 'gap rows:', len(rows), flush=True)


if __name__ == '__main__':
    build()
