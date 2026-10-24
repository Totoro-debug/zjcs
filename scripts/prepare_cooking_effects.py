"""Convert the pinned cooking effect templates, properties and original text."""
import hashlib
import json
import struct
from pathlib import Path
from official_apk import ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_character_props import PropertiesExporter
from prepare_official_panels import unique_sibling_paths
from prepare_map_props import decode
from npc_travel_language import language
from official_resource_manifest import write_resources
from UnityPy.classes.PPtr import PPtr

ART = ROOT / 'assets/official'
REF = ROOT / 'assets/reference/cooking-effects'
CLASSES = {'CookingEffectInfoContentUI', 'CookingEffectInfoUIRecovery',
           'CookingEffectInfoUIBuff', 'CookingEffectInfoUIProp', 'PlayerPropItem',
           'CookingBuffActivateForStageItem', 'CookingBuffReplaceConfirmDialog',
           'CookingEffectBuffItem', 'ConfirmDialog', 'CookingPropEffectFullDialog',
           'CookingPropEffectFullItem', 'RuleDialog', 'TipInfo', 'RuleTipNumer',
           'EntryDetailDialog', 'EntryDetailDialogItem'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class CookingExporter(PropertiesExporter):
    def __init__(self):
        super().__init__()
        self.schemas = ScriptSchemas()
        self.bindings = {}
        self.receipts = []
        self.keys = set()

    def pointer(self, fields, owner):
        return PPtr(**fields, assetsfile=owner.assets_file).deref()

    def convert(self, value, owner):
        if isinstance(value, dict):
            if 'm_PathID' in value:
                if not value['m_PathID']:
                    return None
                obj = self.pointer(value, owner)
                if obj.type.name == 'Sprite':
                    return self.sprite(PPtr(**value, assetsfile=owner.assets_file))
                if obj.type.name == 'GameObject':
                    return self.object_path(PPtr(**value, assetsfile=owner.assets_file))
                data = obj.read(check_read=False)
                if hasattr(data, 'm_GameObject') and data.m_GameObject.path_id:
                    return self.object_path(data.m_GameObject)
                return self.apk.provenance(obj)
            return {key: str(entry) if key == 'rid' else self.convert(entry, owner)
                    for key, entry in value.items()}
        if isinstance(value, list):
            return [self.convert(entry, owner) for entry in value]
        return value

    def ui_node(self, obj, path=''):
        node = super().ui_node(obj, path)
        if node is None:
            return None
        for entry in obj.read().m_Component:
            component = entry.component.deref()
            if component.type.name != 'MonoBehaviour':
                continue
            cls, raw, _ = self.mono(component)
            if cls in CLASSES:
                if cls == 'ConfirmDialog':
                    # The original declaration is ten PPtrs, float, PPtr,
                    # float, PPtr. Check the entire serialized byte extent.
                    if len(raw) != 184:
                        raise ValueError('Unexpected original ConfirmDialog field extent: '+str(len(raw)))
                    def ptr(at):
                        file_id, path_id = struct.unpack_from('<iq', raw, at)
                        return dict(m_FileID=file_id, m_PathID=path_id)
                    names = ['TitleText', 'ContentText', 'HintText', 'HintGo',
                             'ConfirmBtn', 'CancelBtn', 'ConfirmText', 'CancelText',
                             'PopupWindow', 'LockMaskWithTime']
                    fields = {name: ptr(32+12*i) for i,name in enumerate(names)}
                    fields.update(LockTime=struct.unpack_from('<f',raw,152)[0],
                        HyperLinkClickListener=ptr(156),
                        CancelBtnCustomWidth=struct.unpack_from('<f',raw,168)[0],
                        CancelBtnLayoutElement=ptr(172))
                else:
                    fields = self.schemas.read(component)
                node.setdefault('original_bindings', {})[cls] = self.convert(
                    {k: v for k, v in fields.items() if k not in
                     ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}, component)
                self.receipts.append(dict(self.apk.provenance(component), script=cls,
                    sha256=hashlib.sha256(raw).hexdigest()))
            if cls == 'TextMeshProUGUI':
                length = struct.unpack_from('<i', raw, 88)[0]
                aligned = (92 + length + 3) & ~3
                shared = struct.unpack_from('<i', raw, aligned + 28)[0]
                materials = struct.unpack_from('<i', raw, aligned + 44 + 12 * shared)[0]
                style = aligned + 12 * (shared + materials)
                node['text']['word_wrap'] = bool(struct.unpack_from('<i', raw, style + 256)[0])
                node['text']['overflow'] = struct.unpack_from('<i', raw, style + 264)[0]
            if cls == 'LoopVerticalScrollWithMaxHeight':
                # The acquired subclass has one serialized field, a float,
                # appended after its base's fields; the Action is not saved.
                height = struct.unpack_from('<f', raw, len(raw)-4)[0]
                if not 0 < height <= 1920:
                    raise ValueError('Unexpected original loop maximum: '+str(height))
                node['loop_max_height'] = height
                self.receipts.append(dict(self.apk.provenance(component),script=cls,
                    sha256=hashlib.sha256(raw).hexdigest(), max_height_offset=len(raw)-4,
                    max_height=height))
        if node.get('localization_key'):
            self.keys.add(node['localization_key'])
        return node

    def template(self, component):
        fields = self.schemas.read(component)
        go = self.pointer(fields['m_GameObject'], component)
        root = self.ui_node(go)
        original_path = self.object_path(PPtr(**fields['m_GameObject'], assetsfile=component.assets_file))
        def local(value):
            if isinstance(value, dict):return {key:local(entry) for key,entry in value.items()}
            if isinstance(value, list):return [local(entry) for entry in value]
            if isinstance(value, str) and (value==original_path or value.startswith(original_path+'/')):
                return root['path']+value[len(original_path):]
            return value
        root = local(root)
        unique_sibling_paths(root)
        root['active'] = True
        return dict(root=root, source=dict(self.apk.provenance(go),
                    script=self.mono(component)[0], raw_sha256=hashlib.sha256(go.get_raw_data()).hexdigest()))


def build():
    e = CookingExporter()
    detail = e.apk.asset('prefabs_ui_itemdetail', '/CookingUseDetailDialog.prefab')
    owner = next(o for o in e.apk.objects('prefabs_ui_itemdetail')
                 if o.type.name == 'MonoBehaviour' and o.assets_file == detail.assets_file
                 and e.mono(o)[0] == 'CookingEffectInfoContentUI')
    content = e.schemas.read(owner)
    templates = {}
    for entry in content['mEffectItemConfig']['mSerializeBuffer']:
        comp = e.pointer(entry['Value'], owner)
        kind = ['recovery', 'buff', 'prop'][entry['Key']]
        templates[kind] = e.template(comp)
        fields = e.schemas.read(comp)
        templates[kind + '_row'] = e.template(e.pointer(fields['mEffectItemTemp'], comp))
        if kind == 'buff':
            templates['stage'] = e.template(e.pointer(fields['mStageItemTemp'], comp))
    templates['confirm'] = e.ui('prefabs_ui_confirmdialog', '/CookingBuffReplaceConfirmDialog.prefab')
    unique_sibling_paths(templates['confirm']['root'])
    confirm_effect = next(o for o in e.apk.objects('prefabs_ui_confirmdialog')
                          if o.type.name == 'MonoBehaviour' and e.mono(o)[0] == 'CookingEffectInfoUIBuff'
                          and e.object_path(o.read(check_read=False).m_GameObject).startswith('/CookingBuffReplaceConfirmDialog/'))
    confirm_fields = e.schemas.read(confirm_effect)
    templates['buff_confirm_row'] = e.template(e.pointer(confirm_fields['mEffectItemTemp'], confirm_effect))
    templates['full'] = e.ui('prefabs_ui_cooking', '/CookingPropEffectFullDialog.prefab')
    full_owner = next(o for o in e.apk.objects('prefabs_ui_cooking')
                      if o.type.name == 'MonoBehaviour' and e.mono(o)[0] == 'CookingPropEffectFullDialog')
    full = e.schemas.read(full_owner)
    templates['full_row'] = e.template(e.pointer(full['mFullInfoItem'], full_owner))
    rule_owner = e.pointer(full['mRuleDialog'], full_owner)
    rule = e.schemas.read(rule_owner)
    tip_go = e.pointer(rule['mTipInfoPrefab'], rule_owner)
    tip = next(part.component.deref() for part in tip_go.read().m_Component
               if part.component.type.name == 'MonoBehaviour' and e.mono(part.component.deref())[0] == 'TipInfo')
    templates['tip'] = e.template(tip)
    templates['entry'] = e.ui('prefabs_ui_tips', '/PopTip/PopTipEntry.prefab')
    templates['entry_row'] = e.ui('prefabs_ui_item', '/EntryDetailItem.prefab')
    for key in ['entry', 'entry_row']:
        unique_sibling_paths(templates[key]['root'])
    raw_path = ROOT / '.tools/source-packages/official-data/cooking_prop_effect_full.g.bytes'
    raw = raw_path.read_bytes()
    table_obj = next(p.deref() for path, p in e.apk.container('config_luban')
                     if path.endswith('/cooking_prop_effect_full.g.bytes'))
    if table_obj.read().m_Script.encode('utf8', 'surrogateescape') != raw:
        raise ValueError('Cooking effect table cache differs from pinned APK')
    rows = Constructors().table(raw, 'TbCookingPropEffectFullInfo')
    tip_obj = next(p.deref() for path,p in e.apk.container('config_luban') if path.endswith('/rule_tip.g.bytes'))
    tip_raw = tip_obj.read().m_Script.encode('utf8','surrogateescape')
    tip_rows = [row for row in Constructors().table(tip_raw,'TbRuleTipInfo') if row['RuleKey']=='CookingPropEffectFullRule']
    tip_rows.sort(key=lambda row:row['Order'])
    e.keys.update('rule_tip_CookingPropEffectFullRule_'+str(row['Order']) for row in tip_rows)
    e.keys.update(['ui_cooking_prop_effect_range1','ui_cooking_prop_effect_range2'])
    rules = json.loads((ART / 'rules.json').read_text('utf8'))
    wanted = {row['EffectInfos']['2']['PropId'] for row in rules['cooking'] if '2' in row['EffectInfos']}
    prop_path = ROOT / '.tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    props = decode(prop_path.read_bytes(), wanted)
    e.keys.update(['ui_cooking_effect_title_' + name for name in ['Recovery', 'Buff', 'Prop']])
    e.keys.update(['ui_cooking_buff_total_times', 'ui_cooking_buff_total_times_playmode',
                   'ui_cooking_buff_remain_times1', 'ui_cooking_buff_remain_times1_playmode',
                   'ui_cooking_prop_effect_permanent_time', 'ui_common_prop_and_percent1',
                   'ui_common_comma', 'ui_cooking_buff_replace_confirm', 'ui_cooking_use',
                   'ui_status_effect_scope_all', 'confirmation', 'confirm', 'cancel'])
    scopes = json.loads((ART / 'map-status.json').read_text('utf8'))['scopes']
    e.keys.update('status_effect_scope_name_' + key for key in scopes)
    entry_obj = next(p.deref() for path,p in e.apk.container('config_luban') if path.endswith('/entry.g.bytes'))
    entry_raw = entry_obj.read().m_Script.encode('utf8','surrogateescape')
    wanted_entries = {row['EntryId'] for row in scopes.values()} | {rules['game_settings']['CookingBuffCommonEntryId']}
    entries = {str(row['Id']):row for row in Constructors().table(entry_raw, 'TbEntryInfo') if row['Id'] in wanted_entries}
    # All original scope entries are text-only. Reject an unexpected icon
    # rather than silently omit a newly configured asset.
    assert all(not row['Icon'] and not row['Bg'] for row in entries.values())
    for ident in entries:
        e.keys.update(['entry_'+ident+'_name', 'entry_'+ident+'_desc'])
    texts, language_sources = language(e.keys)
    def localize(node):
        if node.get('localization_key') in texts and 'text' in node:
            node['text']['value'] = texts[node['localization_key']]
        for child in node['children']:
            localize(child)
    for template in templates.values():
        localize(template['root'])
    colors_obj = e.apk.asset('scriptableconfig_modules_systemsetting','/GlobalColors.asset')
    colors = e.schemas.read(colors_obj)
    value = dict(templates=templates, permanent=rows, props=props, texts=texts,
                 colors={key:colors[key] for key in ['Red','Orange']}, rule_orders=[row['Order'] for row in tip_rows], entries=entries)
    e.save('cooking-effects.json', value)
    code = ROOT / '.tools/source-packages/official-code'
    sources = {}
    for folder, names in {'common-source/Common': ['CookingInfo', 'CookingPropEffectFullInfo',
        'CookingPropEffectFullInfoParser', 'BattleFormulaHandler', 'PlayerCookingData',
        'PlayerStatusEffectManager'], 'client-source': ['PlayerOperUtils', 'ItemUseEffectCooking',
        'CookingEffectInfoUIRecovery', 'CookingEffectInfoUIBuff', 'CookingEffectInfoUIProp',
        'CookingEffectInfoContentUI', 'CookingBuffReplaceConfirmDialog', 'BuffItemUIUtils',
        'UIUtils', 'LoopVerticalScrollWithMaxHeight']}.items():
        for name in names:
            path = code / folder / (name + '.cs')
            sources[path.relative_to(ROOT).as_posix()] = sha(path)
    for name in CLASSES:
        path=code/'client-source'/(name+'.cs')
        if path.exists():sources[path.relative_to(ROOT).as_posix()]=sha(path)
    for name in ['NumberCultureFormatter_CN','NumberFormatHelper','CultureNumberFormatterBase']:
        path=code/'framework-firstpass-source/Framework'/(name+'.cs')
        sources[path.relative_to(ROOT).as_posix()]=sha(path)
    REF.mkdir(parents=True, exist_ok=True)
    record = dict(manifest='854_156383', table=dict(e.apk.provenance(table_obj),
        sha256=sha(raw_path)), level_props=dict(path=prop_path.relative_to(ROOT).as_posix(),
        sha256=sha(prop_path), source=json.loads((prop_path.parent/'bundle-sources.json').read_text('utf8'))[prop_path.name]),
        original_code_sha256=sources, language_sha256=language_sources,
        rule_table=dict(e.apk.provenance(tip_obj),sha256=hashlib.sha256(tip_raw).hexdigest()),
        entry_table=dict(e.apk.provenance(entry_obj),sha256=hashlib.sha256(entry_raw).hexdigest()),
        global_colors=dict(e.apk.provenance(colors_obj),sha256=hashlib.sha256(colors_obj.get_raw_data()).hexdigest()),
        serialized_components=e.receipts, templates={key: t['source'] for key,t in templates.items()},
        output_sha256=sha(ART/'cooking-effects.json'),
        operation='Original referenced prefabs and bindings, exact configuration constructors and level property rows; no replacement art')
    (REF/'source-manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n', 'utf8')
    manifest_path = ROOT/'assets/reference/official-city/source-manifest.json'
    manifest = json.loads(manifest_path.read_text('utf8'))
    manifest['resources'].update(e.records)
    manifest['data_sha256']['cooking-effects.json'] = sha(ART/'cooking-effects.json')
    explicit = manifest.setdefault('explicit_data_resources', [])
    if 'cooking-effects.json' not in explicit:
        explicit.append('cooking-effects.json')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), 'utf8')
    write_resources(manifest)
    print('Original cooking templates:', len(templates), 'permanent thresholds:', len(rows), 'property classes:', len(props))


if __name__ == '__main__':
    build()
