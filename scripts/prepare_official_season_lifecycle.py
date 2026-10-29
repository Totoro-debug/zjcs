"""Independent fixed-version season tables and original component bindings.

Outputs only season-lifecycle assets/source receipts; no shared catalogs/lock.
"""
import copy
import hashlib
import json
import re
import struct
import xxhash
from official_apk import ROOT
from official_tables import Constructors, SOURCE
from official_unity_schema import ScriptSchemas
from prepare_official_panels import PanelExporter, unique_sibling_paths
from UnityPy.classes.PPtr import PPtr

CLASSES = {
    'AstrologicalDestinyPanel', 'AstrologicalDestinySubPanel',
    'AstrologicalDestinyRoleContent', 'AstrologicalDestinyRoleScoreItem',
    'AstrologicalDestinyScoreItem', 'AstrologicalDestinyCultivationItem',
    'AstrologicalDestinyAwardItem', 'AstrologicalDestinyGamePlayTag',
    'AstrologicalDestinyEvaluationItem', 'AstrologicalDestinyShopContent',
    'AstrologicalDestinyRuleDialog', 'AstrologicalDestinyRuleGradeContent',
    'AstrologicalDestinyRuleGradeItem', 'AstrologicalDestinyRuleScoreRangeDialogItem',
    'AstrologicalDestinyRuleDialogExtraText',
    'AstrologicalDestinyDetailPropsDialog', 'EquipCabinetPanel',
    'EquipCabinetGridItem', 'EquipCabinetEvaluateContent', 'ScoreRewardUI',
    'ScoreRewardItem', 'RuleGradeScrollRectAwardInfo', 'PopupWindow', 'PanelTitle', 'TabGroup',
    'ItemIconAttachmentCell',
}


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':'),
                               allow_nan=False), encoding='utf8')


def bundle_receipt(e, source):
    result = dict(source)
    if result.get('origin'):
        return result
    if not hasattr(e, 'season_bundle_receipts'):
        e.season_bundle_receipts = {}
    if result['bundle_hash'] in e.season_bundle_receipts:
        result.update(e.season_bundle_receipts[result['bundle_hash']])
        return result
    entry = 'assets/yoo/DefaultPackage/' + result['bundle_hash'] + '.bundle'
    raw = e.apk.archive.read(entry)
    if hashlib.md5(raw).hexdigest() != result['bundle_hash']:
        raise ValueError('Pinned attachment bundle checksum mismatch: ' + entry)
    receipt = dict(origin='apk_zip', manifest='854_156383', apk_entry=entry,
                   raw_size=len(raw), raw_sha256=hashlib.sha256(raw).hexdigest())
    e.season_bundle_receipts[result['bundle_hash']] = receipt
    result.update(receipt)
    return result


def reward_attachment(e, schemas, panels, original, sources):
    obj = e.apk.asset('scriptableconfig_modules_itemicon', '/ItemIconAttachmentConfig.asset')
    config = schemas.read(obj)
    selected = [entry for entry in config['Templates'] if entry['Type'] == 2]
    if len(selected) != 1:
        raise ValueError('Original GotTip Type2 must have exactly one template')
    guid = selected[0]['GuidProxy']['mAssetGuid']
    manifest_entry = 'assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes'
    raw = e.apk.archive.read(manifest_entry)
    paths = {m[2].decode(): m[1].decode() for m in re.finditer(
        rb'(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})', raw)}
    asset_path = paths[guid]
    asset_key = 'defaultpackage_' + asset_path.replace('/', '_').lower()
    candidates = sorted((b for b in e.apk.index if asset_key.startswith(b.removesuffix('.bundle'))),
                        key=len, reverse=True)
    if not candidates:
        raise ValueError('Original GotTip prefab bundle absent from pinned manifest: ' + asset_path)
    bundle = candidates[0]
    suffix = '/' + asset_path.split('Assets/Prefabs/UI/Item/', 1)[1]
    prefab_object = e.apk.asset(bundle, suffix)
    key = 'season_reward_got_tip'
    panels[key] = e.ui(bundle, suffix)
    unique_sibling_paths(panels[key]['root'])
    panels[key]['source'].update(attachment_type=2, guid=guid, asset_path=asset_path)
    sources['reward_attachment'] = dict(type=2, guid=guid, asset_path=asset_path,
        config=dict(bundle_receipt(e, e.apk.provenance(obj)),
                    object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                    template=selected[0]),
        manifest_entry=manifest_entry, manifest_sha256=hashlib.sha256(raw).hexdigest(),
        prefab=dict(bundle_receipt(e, e.apk.provenance(prefab_object)),
                    object_sha256=hashlib.sha256(prefab_object.get_raw_data()).hexdigest()),
        item_host=dict(source=original['item']['source'],
                       geometry_sha256=hashlib.sha256(json.dumps(original['item'], ensure_ascii=False,
                           separators=(',', ':')).encode('utf8')).hexdigest()))
    return dict(prefab=key, guid=guid, type=2)


def extract_bindings(e, schemas, panels, sources):
    selected_paths = set()

    def collect(node):
        selected_paths.add(node['path'])
        for child in node['children']:
            collect(child)
    for panel in panels.values():
        collect(panel['root'])
    def unique_path(pointer):
        if pointer.type.name != 'GameObject':
            pointer = pointer.deref().read(check_read=False).m_GameObject
        obj = pointer.read()
        transform = next(c.component.read() for c in obj.m_Component
                         if c.component.type.name == 'RectTransform')
        names = []
        while True:
            name = obj.m_Name
            if transform.m_Father.path_id:
                parent = transform.m_Father.read()
                occurrence = 0
                for sibling in parent.m_Children:
                    if sibling.path_id == transform.object_reader.path_id:
                        break
                    if sibling.read().m_GameObject.read().m_Name == name:
                        occurrence += 1
                if occurrence:
                    name += f' ({occurrence})'
            names.insert(0, name)
            if not transform.m_Father.path_id:
                break
            transform = transform.m_Father.read()
            obj = transform.m_GameObject.read()
        return '/' + '/'.join(names)

    def convert(value, component):
        if isinstance(value, dict):
            if 'm_PathID' in value:
                if not value['m_PathID']:
                    return None
                pointer = PPtr(m_FileID=value['m_FileID'], m_PathID=value['m_PathID'],
                               assetsfile=component.assets_file)
                if pointer.type.name in ['GameObject', 'MonoBehaviour', 'RectTransform', 'Transform']:
                    return unique_path(pointer)
                asset = pointer.deref()
                return dict(asset_type=pointer.type.name, path_id=str(pointer.path_id),
                            source=e.apk.provenance(asset))
            return {key: str(entry) if key == 'rid' else convert(entry, component)
                    for key, entry in value.items()}
        if isinstance(value, list):
            return [convert(entry, component) for entry in value]
        return value

    bindings, receipts = {}, []
    for bundle in sorted({panel['source']['bundle'] for panel in panels.values()}):
        for component in e.apk.objects(bundle):
            if component.type.name != 'MonoBehaviour':
                continue
            cls, raw, basic = e.mono(component)
            if cls not in CLASSES:
                continue
            path = unique_path(basic.m_GameObject)
            if path not in selected_paths:
                continue
            fields = schemas.read(component)
            bindings.setdefault(path, {})[cls] = {
                key: convert(value, component) for key, value in fields.items()
                if key not in ['m_GameObject', 'm_Script', 'm_Name', 'm_Enabled']}
            receipts.append(dict(e.apk.provenance(component), object_path=path, script=cls,
                sha256=hashlib.sha256(raw).hexdigest(), operation='Complete original schema read; exact pointer paths, typed asset receipts and managed reference IDs'))

    def enrich(node):
        if node['path'] in bindings:
            node['original_bindings'] = bindings[node['path']]
        for child in node['children']:
            enrich(child)
    for panel in panels.values():
        enrich(panel['root'])
    sources['season_bindings'] = receipts


def build():
    e, c = PanelExporter(), Constructors()
    schemas = ScriptSchemas()
    names = {m[2].rsplit('/', 1)[-1]: m[1] for m in re.finditer(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', c.source('Tables'))}
    wanted = {'equip_cabinet_score', 'equip_cabinet_evaluate', 'arena_destiny',
              'destiny_score_award',
              'legion_map_skill', 'legion_map_skill_study_level'}
    tables, inputs = {}, {}
    for bundle in ['config_luban', 'config_luban_astrologicaldestiny']:
        for path, ptr in e.apk.container(bundle):
            name = path.rsplit('/', 1)[-1].removesuffix('.g.bytes')
            if name not in wanted:
                continue
            obj = ptr.deref()
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = c.table(raw, names[name])
            inputs[name] = dict(e.apk.provenance(obj), asset=path, bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest(), constructor=names[name],
                constructor_sha256=hashlib.sha256((SOURCE / (names[name] + '.cs')).read_bytes()).hexdigest())
    if set(tables) != wanted:
        raise ValueError('Missing pinned lifecycle tables: ' + str(wanted - set(tables)))
    print('Season lifecycle tables:', {key: len(value) for key, value in tables.items()}, flush=True)
    original = json.loads((ROOT / 'assets/official/ui.json').read_text('utf8'))
    keys = ['astrological', 'astrological_rule', 'astrological_props'] + ['astrological_season_' + str(i) for i in range(1, 7)]
    panels = {key: copy.deepcopy(original[key]) for key in keys}
    for key, name in [('season_rule_grade', 'AstrologicalDestinyRuleGradeItem'),
                      ('season_rule_grade_award', 'AstrologicalDestinyRuleGradeItemWithAward')]:
        panels[key] = e.ui('prefabs_ui_astrologicaldestiny', '/' + name + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    sources = {}
    read_fields = schemas.read

    def subtree(tree, name):
        at = next(i for i, n in enumerate(tree) if n['level'] == 1 and n['name'] == name)
        end = next((i for i in range(at + 1, len(tree)) if tree[i]['level'] == 1), len(tree))
        return copy.deepcopy(tree[at:end])

    schemas.tree('Assembly-CSharp', 'AstrologicalDestinyEntryPanel')
    entry = schemas.cache['Assembly-CSharp:AstrologicalDestinyEntryPanel']
    # TabChangeEvent lacks [Serializable], unlike TabItem.TabEvent. Its public
    # field is absent from the pinned object. Read the three serialized fields
    # in source order, retaining strict full-object checking.
    nodes = copy.deepcopy(entry[:9]); nodes[0]['type'] = 'UnityEngine.UI.Ext.TabGroup'
    nodes.append(dict(level=1, type='bool', name='m_AllowSwitchOff', flags=0x4000))
    nodes += [dict(level=1, type='vector', name='mTabItems', flags=0),
              dict(level=2, type='Array', name='Array', flags=0),
              dict(level=3, type='int', name='size', flags=0)]
    part = subtree(entry, 'mAnimation'); part[0]['name'] = 'data'
    part[0]['type'] = 'PPtr<$TabItem>'
    for n in part: n['level'] += 2
    nodes += part
    part = subtree(entry, 'mAnimation'); part[0].update(name='m_ResetTab', type='PPtr<$TabItem>')
    nodes += part
    schemas.cache['Assembly-CSharp:UnityEngine.UI.Ext.TabGroup'] = nodes
    sources['manual_schemas'] = {'UnityEngine.UI.Ext.TabGroup': dict(
        source_sha256=hashlib.sha256((ROOT / '.tools/source-packages/official-code/client-source/UnityEngine.UI.Ext/TabGroup.cs').read_bytes()).hexdigest(),
        operation='Three original serialized fields; non-Serializable TabChangeEvent excluded; exact PPtr layout and full bytes required')}
    name = 'RuleGradeScrollRectAwardInfo'
    source = (ROOT / '.tools/source-packages/official-code/client-source' / (name + '.cs')).read_text('utf-8-sig')
    nodes = copy.deepcopy(entry[:9]); nodes[0]['type'] = name
    for kind, field in re.findall(r'\[SerializeField\]\s*private (\w+) (\w+)(?:\s*=.*)?;', source):
        if kind in ['Align', 'float']:
            nodes.append(dict(level=1, type='int' if kind == 'Align' else 'float', name=field, flags=0))
        else:
            part = subtree(entry, 'mAnimation'); part[0].update(name=field, type='PPtr<$' + kind + '>')
            nodes += part
    schemas.cache['Assembly-CSharp:' + name] = nodes
    sources['manual_schemas'][name] = dict(source_sha256=hashlib.sha256(source.encode('utf8')).hexdigest(),
        operation='Original six serialized fields in source order; exact enum/float/PPtr schemas, full bytes required')

    def read_original(component):
        cls, raw, basic = e.mono(component)
        try:
            return read_fields(component)
        except Exception as error:
            raise ValueError(f'Season schema {cls} ({len(raw)} bytes) at '
                             f'{e.object_path(basic.m_GameObject)}') from error
    schemas.read = read_original
    attachment = reward_attachment(e, schemas, panels, original, sources)
    extract_bindings(e, schemas, panels, sources)
    cell = panels[attachment['prefab']]['root']['original_bindings']['ItemIconAttachmentCell']
    attachment['layer'] = cell['mLayer']
    config_object = e.apk.asset('scriptableconfig_modules_astrologicaldestiny', '/AstrologicalDestinyConfig.asset')
    config = schemas.read(config_object)
    art = {'gameplay': {}, 'attachments': {'2': attachment}}
    for pair in config['GameplayTagSettings']['mSerializeBuffer']:
        value = pair['Value']
        art['gameplay'][str(pair['Key'])] = dict(key=value['mKey'],
            sprite=e.guid_sprite(value['mBgImgSpriteProxy']['mAssetGuid']))
    for key in ['DemonBattle', 'SecretArea', 'DailyDungeon', 'NPCAdventure']:
        art[key + 'Locked'] = e.guid_sprite(config[key + 'LockedImgSpriteProxy']['mAssetGuid'])
    sources['config_input'] = dict(e.apk.provenance(config_object),
        sha256=hashlib.sha256(config_object.get_raw_data()).hexdigest())
    layout, canvases, canvas_receipts = {}, {}, {}
    for bundle in sorted({panel['source']['bundle'] for panel in panels.values()}):
        print('Season layout bundle:', bundle, flush=True)
        for obj in e.apk.objects(bundle):
            if obj.type.name == 'Canvas':
                raw = obj.read_typetree()
                path = e.object_path(obj.read().m_GameObject)
                canvases[path] = dict(override=bool(raw['m_OverrideSorting']),
                    order=int(raw['m_SortingOrder']), layer_id=int(raw['m_SortingLayerID']))
                canvas_receipts[path] = dict(e.apk.provenance(obj), object_path=path,
                    sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                    fields=canvases[path], operation='Original built-in Canvas full type-tree read')
                continue
            if obj.type.name != 'MonoBehaviour':
                continue
            cls, raw, basic = e.mono(obj)
            if cls != 'TextMeshProUGUI':
                continue
            path = e.object_path(basic.m_GameObject)
            length = struct.unpack_from('<i', raw, 88)[0]
            aligned = (92 + length + 3) & ~3
            shared = struct.unpack_from('<i', raw, aligned + 28)[0]
            materials = struct.unpack_from('<i', raw, aligned + 44 + 12 * shared)[0]
            style = aligned + 12 * (shared + materials)
            wrap = struct.unpack_from('<i', raw, style + 256)[0]
            overflow = struct.unpack_from('<i', raw, style + 264)[0]
            if wrap not in [0, 1] or overflow not in range(7):
                raise ValueError('Unexpected season TMP layout: ' + path)
            layout[path] = dict(word_wrap=bool(wrap), overflow=overflow,
                               margin=list(struct.unpack_from('<4f', raw, style + 352)))
            sources.setdefault('layout_inputs', []).append(dict(e.apk.provenance(obj),
                object_path=path, sha256=hashlib.sha256(raw).hexdigest()))

    selected_canvases = set()
    def enrich(node):
        if 'text' in node and node['path'] in layout:
            node['text'].update(layout[node['path']])
        if node['path'] in canvases:
            node['canvas_sort'] = canvases[node['path']]
            selected_canvases.add(node['path'])
        for child in node['children']:
            enrich(child)
    for panel in panels.values():
        enrich(panel['root'])
    sources['canvas_inputs'] = [canvas_receipts[path] for path in sorted(selected_canvases)]
    language, texts = {}, {}
    for name in ['wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile']:
        raw = (ROOT / '.tools/source-packages/WwiseBnk' / name).read_bytes()
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            key, offset = struct.unpack_from('<Qi', raw, 4 + i * 12)
            pos = start + offset
            length = struct.unpack_from('<H', raw, pos)[0]
            language[key] = raw[pos + 2:pos + 2 + length].decode('utf8')
        language_receipts = json.loads((ROOT / '.tools/source-packages/WwiseBnk/bundle-sources.json').read_text('utf8'))
        inputs[name] = dict(sha256=hashlib.sha256(raw).hexdigest(), source=language_receipts[name])
    keys_to_read = set()
    def collect_language_keys(value):
        if isinstance(value, dict):
            if 'localization_key' in value:
                keys_to_read.add(value['localization_key'])
            if 'mStateMultiLanguageTextData' in value:
                for pair in value['mStateMultiLanguageTextData']['Keys']['mSerializeBuffer']:
                    keys_to_read.add(pair['Value'])
            for entry in value.get('mStateMultiLanguageTextDatas', []):
                for pair in entry['Keys']['mSerializeBuffer']:
                    keys_to_read.add(pair['Value'])
            for nested in value.values():
                collect_language_keys(nested)
        elif isinstance(value, list):
            for nested in value:
                collect_language_keys(nested)
    collect_language_keys(panels)
    rank_source = (SOURCE / 'Rank.cs').read_bytes()
    rank_names = re.findall(r'^\s*(\w+)\s*,?\s*$', rank_source.decode('utf-8-sig'), re.M)
    keys_to_read.update('Rank.' + name for name in rank_names)
    keys_to_read.add('ui_common_level_style_14')
    for row in tables['equip_cabinet_evaluate']:
        keys_to_read.add('equip_cabinet_evaluate_' + rank_names[row['Rank']] + '_' + row['Evaluate'])
    inputs['rank_names'] = dict(source='common-source/Common/Rank.cs', sha256=hashlib.sha256(rank_source).hexdigest())
    # Original tag controllers form these language keys at runtime. They are
    # absent from serialized text bindings, so enumerate their official enums
    # and the already resolved official artwork keys explicitly.
    for enum, prefix in [('DailyDungeonDifficulty', 'ui_daily_dungeon_difficulty_text_'),
                         ('BattleResultGradeType', 'ui_daily_dungeon_grade_text_'),
                         ('SecretAreaStyle', 'secret_area_style_name_')]:
        raw = (SOURCE / (enum + '.cs')).read_bytes()
        body = re.search(r'public enum ' + enum + r'\s*\{([^}]+)\}', raw.decode('utf-8-sig'), re.S).group(1)
        names = [entry.strip().split('=')[0].strip() for entry in body.split(',') if entry.strip()]
        if not all(re.fullmatch(r'\w+', name) for name in names):
            raise ValueError('Unexpected original enum declaration: ' + enum)
        keys_to_read.update(prefix + name for name in names)
        inputs['enum_' + enum] = dict(source='common-source/Common/' + enum + '.cs', sha256=hashlib.sha256(raw).hexdigest())
    for entry in art['gameplay'].values():
        key = entry['key']
        if key.isdigit():
            keys_to_read.add(('ui_demon_battle_entry_item_title_' if int(key) < 10 else 'ui_daily_dungeon_item_title_without_difficulty_') + key)
        elif key.startswith('NPCAdventureGroup'):
            keys_to_read.add('ui_npc_travel_map_group_' + key.removeprefix('NPCAdventureGroup'))
    keys_to_read.add('ui_npc_adventure_progress_text')
    source_code = ROOT / '.tools/source-packages/official-code'
    source_paths = list((source_code / 'client-source').glob('AD*TagController.cs'))
    source_paths.append(source_code / 'client-source/ADGamePlayTagControllerUtils.cs')
    source_paths += list((source_code / 'client-source').glob('AstrologicalDestiny*.cs'))
    for path in source_paths:
        keys_to_read.update(re.findall(r'"([a-zA-Z][\w]+)"', path.read_text('utf-8-sig')))
    keys_to_read.update(entry['key'] for entry in art['gameplay'].values())
    keys_to_read.update('astrological_destiny_grade_' + str(i) for i in range(10))
    keys_to_read.update('Mainland_group_' + str(i) for i in range(10, 25))
    for type_name in ['All', 'Bless', 'Gameplay']:
        keys_to_read.add(f'destiny_score_{type_name}_RuleDesc')
    for type_name in ['DemonBattle', 'DailyDungeon', 'SecretArea', 'MapProgress', 'MapGroupProgress', 'MapGroupRegionProgress', 'NPCAdventureProgress']:
        keys_to_read.add(f'destiny_score_Gameplay_{type_name}_RuleDesc')
        keys_to_read.add(f'destiny_score_Gameplay_{type_name}_Extra_RuleDesc')
    for season in range(1, 8):
        keys_to_read.add(f'destiny_score_Gameplay_{season}_Map_Tip_completed_Extra_RuleDesc')
        keys_to_read.add(f'destiny_gameplay_map_not_tag_completed_tip_{season}')
    for key in sorted(keys_to_read):
        value = language.get(xxhash.xxh64_intdigest(key.encode()))
        if value is not None:
            texts[key] = value
    out = ROOT / 'assets/official'
    source_manifest = ROOT / 'assets/reference/season-lifecycle/source-manifest.json'
    if source_manifest.exists():
        previous = source_manifest.read_bytes()
        history = source_manifest.parent / 'history' / ('source-manifest-' + hashlib.sha256(previous).hexdigest() + '.json')
        history.parent.mkdir(parents=True, exist_ok=True)
        if not history.exists():
            history.write_bytes(previous)
    save(out / 'season-lifecycle.json', dict(tables=tables, art=art, texts=texts))
    save(out / 'season-lifecycle-ui.json', panels)
    source_code = ROOT / '.tools/source-packages/official-code'
    paths = ['common-source/Common/' + name + '.cs' for name in [
        'EquipCabinetEvaluateInfoParser', 'EquipCabinetScoreInfoParser', 'ArenaDestinyInfoParser',
        'PlayerEquipCabinetDataExt', 'PlayerSeasonRecordDataWrap', 'PlayerSeasonRecordDataWrapExtra',
        'DailyDungeonPassRecordData', 'DemonBalttePassRecordData', 'SecretAreaDamageRecordData',
        'PlayerSecretAreaSettleDamageRequest', 'GameResultSecretAreaInfo', 'LanguageUtils', 'SeasonRecordUtils']]
    paths.append('client-source/SecretAreaUIUtils.cs')
    paths.append('client-source/UIUtils.cs')
    paths += ['client-source/' + name + '.cs' for name in [
        'ItemIconAttachmentConfig', 'ItemIconAttachmentType', 'ItemIconAttachmentManager',
        'ItemIconAttachmentPool', 'ItemIconAttachment', 'ItemIcon']]
    paths += ['client-source/' + name + '.cs' for name in CLASSES if (source_code / 'client-source' / (name + '.cs')).exists()]
    from fetch_official_assets import SHA256
    sources.update(manifest='854_156383', apk_sha256=SHA256, tables=inputs,
        cached_geometry={key: dict(source=panels[key]['source'], sha256=hashlib.sha256(json.dumps(original[key], ensure_ascii=False, separators=(',', ':')).encode('utf8')).hexdigest()) for key in keys},
        code={path: hashlib.sha256((source_code / path).read_bytes()).hexdigest() for path in paths} | {path.relative_to(source_code).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in source_paths},
        outputs={name: hashlib.sha256((out / name).read_bytes()).hexdigest() for name in ['season-lifecycle.json', 'season-lifecycle-ui.json']},
        resources={path: bundle_receipt(e, record) for path, record in e.records.items()},
        operation='Extract fixed-version Legion map skill and equipment cabinet property tables; complete original Luban constructors, strict component reads, GotTip2 assets, and dynamic language')
    save(source_manifest, sources)
    print('Season lifecycle UI:', len(panels), 'prefabs,', len(sources['season_bindings']), 'binding receipts', flush=True)


if __name__ == '__main__':
    build()
