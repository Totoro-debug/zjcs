"""Convert the pinned client's CollectBook prefabs and catalog metadata offline."""
import copy
import hashlib
import json
from pathlib import Path

from prepare_profession_ui import ProfessionExporter
from prepare_official_panels import unique_sibling_paths
from prepare_official_city import ART, REFERENCE, ROOT
from official_tables import Constructors
from official_resource_manifest import write_resources
from official_map_codec import unpack
from prepare_map_props import decode as decode_props
from UnityPy.classes.PPtr import PPtr


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class CodexExporter(ProfessionExporter):
    def ui_node(self, obj, path=''):
        node = super().ui_node(obj, path)
        if node is None:
            return None
        for part in obj.read().m_Component:
            component = part.component.deref()
            if component.type.name != 'MonoBehaviour':
                continue
            cls, _, _ = self.mono(component)
            if not (cls.startswith('CollectBook') or cls.startswith('FilterGroupItem') or
                    cls in ['CollectSortGroupItem', 'BaseCollectBookItem']):
                continue
            fields = self.fields(component)
            for name, value in fields.items():
                if name in ['m_GameObject', 'm_Script']:
                    continue
                if isinstance(value, dict) and 'm_PathID' in value:
                    target = self.target(component, value)
                    if target:
                        node.setdefault('bindings', {})[name] = target
            for key in ['SortType', 'FilterType', 'Profession']:
                if key in fields:
                    node.setdefault('codex_values', {})[key] = fields[key]
        return node


def build():
    e = CodexExporter()
    panels = {}
    for path, ptr in e.apk.container('prefabs_ui_collectbook'):
        if ptr.type.name != 'GameObject':
            continue
        key = path.rsplit('/', 1)[-1].removesuffix('.prefab')
        panels[key] = e.ui('prefabs_ui_collectbook', '/' + key + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    # Runtime constructs only the active state. Preserve the source shell and
    # separate prefabs, rather than instantiating both entire hidden catalogs.
    shell = copy.deepcopy(panels['CollectBookPanel'])
    def prune(node):
        if node['name'] == 'MiddleGo':
            node['children'] = []
        for child in node['children']:
            prune(child)
    prune(shell['root'])
    panels['shell'] = shell
    sprites = {}
    for path, ptr in e.apk.container('image_ui_collectbook'):
        if ptr.type.name == 'Sprite':
            sprites[path.rsplit('/', 1)[-1].rsplit('.', 1)[0]] = e.sprite(ptr)
    def sprite(path):
        key = path.rsplit('/', 1)[-1]
        if key not in sprites:
            raise ValueError('Original CollectBook sprite missing: ' + path)
        return sprites[key]
    rules = json.loads((ART / 'rules.json').read_text(encoding='utf8'))
    items = {x['ClassId']: x for x in rules['item']}
    texts = {}
    icons = {}
    constructors = Constructors()
    suit_obj = e.apk.asset('config_luban', '/treasure_suit.g.bytes')
    suit_raw = suit_obj.read().m_Script.encode('utf8', 'surrogateescape')
    suits = constructors.table(suit_raw, 'TbTreasureSuitInfo')
    suit_by_item = {ident: suit['ClassId'] for suit in suits for ident in suit['Treasures']}
    for ident in suit_by_item.values():
        texts[f'item_{ident}_name'] = e.text(f'item_{ident}_name')
    profession_by_skill = {ident: pro['Profession'] for pro in rules['profession_base'] for ident in pro['Skills']}
    skill_configs = {x['ClassId']: x for x in rules['skill']}
    asset_index = json.loads((ROOT / '.tools/source-packages/manifest-assets.json').read_text(encoding='utf8'))
    address_index = {x['address']: x for x in asset_index if x['address']}
    lite_obj = e.apk.asset('config_ec', '/SkillLiteInfoPack.g.bytes')
    lite_raw = lite_obj.read().m_Script.encode('utf8', 'surrogateescape')
    lite = unpack(lite_raw)[0]
    skills = []
    for ident, pro in profession_by_skill.items():
        cfg = skill_configs[ident]
        if cfg['UserType'] != 1:
            continue
        skills.append({'id': ident, 'profession': pro, 'element': lite.get(cfg['EcEntityId'], [0])[0]})
    bundles = {}
    needed = set(x['id'] for x in skills) | set(x['ClassId'] for x in rules['treasure'])
    for ident in needed:
        info = items[ident]
        icon_path = info['RawIcon']
        prefix = 'defaultpackage_assets_image_' + icon_path.replace('/', '_').lower()
        candidates = sorted((b for b in e.apk.index if prefix.startswith(b.removesuffix('.bundle') + '_')), key=len, reverse=True)
        if 'Assets/Image/' + icon_path in address_index:
            full_path = address_index['Assets/Image/' + icon_path]['path']
            resolved_prefix = 'defaultpackage_' + full_path.rsplit('.', 1)[0].replace('/', '_').lower()
            candidates = sorted((b for b in e.apk.index if resolved_prefix.startswith(b.removesuffix('.bundle') + '_')), key=len, reverse=True) + candidates
        found = None
        for bundle in candidates:
            if bundle not in bundles:
                bundles[bundle] = {p.rsplit('/', 1)[-1].rsplit('.', 1)[0].lower(): ptr
                                   for p, ptr in e.apk.container(bundle) if ptr.type.name == 'Sprite'}
            found = bundles[bundle].get(icon_path.rsplit('/', 1)[-1].lower())
            if found:
                break
        if found is None:
            raise ValueError('Original item icon missing: ' + icon_path + ' searched: ' + str(candidates))
        icons[str(ident)] = e.sprite(found)
        for suffix in ['name', 'desc', 'func_desc']:
            key = f'item_{ident}_{suffix}'
            value = e.text(key)
            if value != key:
                texts[key] = value
    cells = []
    profession_backgrounds = {}
    quality_backgrounds = {}
    for obj in e.apk.objects('prefabs_ui_collectbook'):
        if obj.type.name != 'MonoBehaviour':
            continue
        cls, _, _ = e.mono(obj)
        if cls not in ['CollectBookPanel', 'CollectBookSkillContent', 'SkillCollectSet', 'TreasureCollectSet', 'CollectBookTreasureContentItem']:
            continue
        fields = e.fields(obj)
        if cls == 'CollectBookPanel':
            for cell in fields['mMainCellInfo']:
                cells.append({'type': cell['CollectionType'], 'background': sprite(cell['BgSpritePath']),
                              'figure': sprite(cell['GodnessSpritePath']) if cell['GodnessSpritePath'] else None,
                              'title': e.text(cell['TitleTxt'])})
        if cls == 'CollectBookSkillContent':
            profession_backgrounds = {str(x['Key']): sprite(x['Value']) for x in fields['mProfessionSerial2SpritePath']['mSerializeBuffer']}
        if cls in ['SkillCollectSet', 'TreasureCollectSet'] and not quality_backgrounds:
            for entry in fields['QualityBgItems']:
                ptr = PPtr(m_FileID=entry['Sprite']['m_FileID'], m_PathID=entry['Sprite']['m_PathID'], assetsfile=obj.assets_file)
                quality_backgrounds[str(entry['Quality'])] = e.sprite(ptr)
        if cls == 'CollectBookTreasureContentItem':
            for key in ['mLockBgImage', 'mOtherBgImage']:
                sprites[key] = e.guid_sprite(fields[key]['mAssetGuid'])
    groups = []
    for cfg in rules['treasure_gacha']:
        ident = cfg['ChestGachaId']
        groups.append(dict(cfg, title=e.text(f'treasure_gacha_main_title_{ident}'),
                           subtitle=e.text(f'treasure_gacha_subtitle_{ident}') if cfg['Subtitle'] else '',
                           background=sprite(cfg['BgSpritePath'])))
    for key in ['CollectBookPanel.Others', 'CollectBookPanel.CollectBook', 'ui_skill_collect_title',
                'ui_treasure_collect_title', 'ui_collect_treasure_lock_grid', 'ui_collect_lock_type_cell_title']:
        texts[key] = e.text(key)
    regions = json.loads((ART / 'adventure.json').read_text(encoding='utf8'))['tables']['Map/map_region']
    region_by_item = {ident: region for region in regions for ident in region['DropItems']}
    treasures = []
    for cfg in rules['treasure']:
        region = region_by_item.get(cfg['ClassId'], {})
        map_id, region_id = region.get('ClassId', 0), region.get('Region', 0)
        name_key = f'region_{map_id}_{region_id}'
        region_name = e.text(name_key)
        treasures.append(dict(cfg, suit=suit_by_item.get(cfg['ClassId'], 0), map=map_id, region=region_id,
                              region_name=region_name if region_name != name_key else '其他'))
    prop_input = ROOT / '.tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    property_ids = {int(value) for cfg in treasures for value in cfg['LevelPropId'].values()}
    props = decode_props(prop_input.read_bytes(), property_ids)
    e.save('codex-ui.json', panels)
    e.save('codex.json', {'skills': skills, 'treasures': treasures, 'groups': groups, 'suits': suits,
                          'cells': cells, 'icons': icons, 'sprites': sprites, 'texts': texts,
                          'profession_backgrounds': profession_backgrounds, 'quality_backgrounds': quality_backgrounds})
    e.save('codex-props.json', props)
    reference = ROOT / 'assets/reference/codex'
    reference.mkdir(parents=True, exist_ok=True)
    source = {'manifest': '854_156383', 'prefabs': {k: p['source'] for k, p in panels.items()},
              'resources': e.records, 'localization': e.language_sources,
              'skill_elements': dict(e.apk.provenance(lite_obj), sha256=hashlib.sha256(lite_raw).hexdigest()),
              'tables': {'treasure_suit': dict(e.apk.provenance(suit_obj), sha256=hashlib.sha256(suit_raw).hexdigest(), bytes=len(suit_raw), consumed_bytes=len(suit_raw))},
              'level_props': {'sha256': sha(prop_input), 'path': prop_input.relative_to(ROOT).as_posix()},
              'behavior': ['CollectBookPanel.cs', 'CollectBookSkillContent.cs', 'CollectBookSkillDetaiContent.cs',
                           'CollectBookTreasureContent.cs', 'CollectBookTreasureDetaiContent.cs', 'TreasureGroupData.cs',
                           'ProfessionBaseInfoParser.cs', 'SkillCollectSet.cs', 'TreasureCollectSet.cs',
                           'SkillItemComparer.cs', 'TreasureComparer.cs', 'TreasureCollectInfo.cs'],
              'outputs': {name: sha(ART / name) for name in ['codex-ui.json', 'codex.json', 'codex-props.json']}}
    behavior = {}
    code = ROOT / '.tools/source-packages/official-code'
    for key in source['behavior']:
        path = next((code / folder / key for folder in ['client-source', 'common-source/Common', 'common-source'] if (code / folder / key).is_file()), None)
        if path is None:
            raise ValueError('Missing original behavior source: ' + key)
        behavior[key] = {'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(path)}
    source['behavior'] = behavior
    (reference / 'source-manifest.json').write_text(json.dumps(source, ensure_ascii=False, indent=2), encoding='utf8')
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf8'))
    manifest['resources'].update(e.records)
    manifest['data_sha256'].update(source['outputs'])
    (REFERENCE / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf8')
    write_resources(manifest)
    print('Codex:', len(skills), 'skills,', len(treasures), 'treasures,', len(panels), 'prefabs', flush=True)


if __name__ == '__main__':
    build()
