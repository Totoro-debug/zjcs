"""Convert the pinned official guild prefabs, sprites and fully consumed rules."""
import hashlib
import json
import re
import struct
import subprocess
import xxhash

from prepare_official_panels import PanelExporter, unique_sibling_paths
from prepare_official_city import ART, REFERENCE, ROOT
from official_resource_manifest import write_resources
from official_tables import Constructors, SOURCE
from guild_asset_helpers import skeleton_job
from prepare_map_regions import Regions, save
from UnityPy.classes.PPtr import PPtr


class GuildExporter(PanelExporter):
    texture = Regions.texture
    def __init__(self):
        super().__init__()
        self.texture_cache = {}
    def ui_node(self, obj, path=''):
        node = super().ui_node(obj, path)
        if node is None:
            go = obj.read()
            if go.m_Name == 'ItemContent' and '/LegionFightBattlePanelPlayerGroupItem/RootForUI' in path:
                t = next(c.component.read() for c in go.m_Component if c.component.type.name == 'Transform')
                node = dict(name=go.m_Name, path=path+'/'+go.m_Name, active=bool(go.m_IsActive),
                    anchor_min=[0.5,0.5], anchor_max=[0.5,0.5], anchored_position=[t.m_LocalPosition.x,t.m_LocalPosition.y],
                    size_delta=[0,0], pivot=[0.5,0.5], scale=[t.m_LocalScale.x,t.m_LocalScale.y,t.m_LocalScale.z],
                    rotation=0, scripts=[], original_type='Transform', children=[])
                node['children'] = [self.ui_node(c.read().m_GameObject.deref(), node['path']) for c in t.m_Children]
                node['children'] = [c for c in node['children'] if c is not None]
                return node
            return None
        for component in obj.read().m_Component:
            c = component.component.deref()
            if c.type.name != 'MonoBehaviour':
                continue
            cls, raw, _ = self.mono(c)
            if cls != 'SkeletonGraphic':
                continue
            f, ident = struct.unpack_from('<iq', raw, 88)
            if not ident:
                continue
            length = struct.unpack_from('<i', raw, 152)[0]
            skin = raw[156:156+length].decode() if 0 <= length < len(raw)-156 else ''
            asset = PPtr(m_FileID=f, m_PathID=ident, assetsfile=c.assets_file).deref()
            node['guild_skeleton'] = skeleton_job(self, asset, skin)
        return node

PANELS = {
    'entry': 'Legion/NoLegionPanel', 'join': 'Legion/LegionJoinCreatePanel',
    'home': 'LegionMap/LegionMapHallPanel', 'map': 'LegionMap/LegionMapPanel',
    'info': 'Legion/LegionInfoDialog', 'members': 'Legion/LegionMemberPanel',
    'member_row': 'Legion/LegionMemberPanelItem', 'requests': 'Legion/LegionJoinRequestPanel',
    'settings': 'Legion/LegionSettingPanel', 'icons': 'Legion/LegionIconSelectDialog',
    'donate': 'Legion/LegionDonateDialog', 'affairs': 'Legion/LegionAffairDialog',
    'build': 'LegionMap/LegionMapBuildingBuildPanel',
    'upgrade': 'LegionMap/LegionMapBuildingUpgradePanel',
    'gather': 'LegionMap/LegionMapGatherPanel', 'storage': 'LegionMap/LegionMapStoragePanel',
    'library': 'LegionMap/Library/LegionMapLibraryPanel',
    'skill': 'LegionMap/Library/LegionMapSkillPromotePanel',
    'life': 'LegionMap/LegionMapLifeHangUpBuildingPanel',
    'tools': 'LegionMap/LegionMapLifeHangUpToolDialog',
    'shop': 'LegionMap/LegionShopPanel', 'boss': 'Legion/LegionBoss/LegionBossPanel',
    'boss_info': 'Legion/LegionBoss/LegionBossInfoDialog',
    'embassy': 'Legion/LegionEmbassy/LegionSearchPanel',
    'visit': 'Legion/LegionEmbassy/LegionVisitMapPanel',
    'plunder': 'LegionMap/LegionMapLifeHangUpPlunderDialog',
    'resist': 'LegionMap/LegionMapLifeHangUpPlunderResistDialog',
    'war': 'Legion/LegionFight/LegionFightMainPanel',
    'war_battle': 'Legion/LegionFight/LegionFightBattlePanel',
    'war_defense': 'Legion/LegionFight/LegionFightStrongholdPanel',
    'war_history': 'Legion/LegionFight/LegionFightHistoryDialog',
    'war_rules': 'Legion/LegionFight/LegionFightRuleDialog',
    'activities': 'Legion/LegionActivity/LegionActivityPanel',
}


def build():
    exporter = GuildExporter()
    panels = {}
    for key, path in PANELS.items():
        bundle = 'prefabs_ui_legionmap' if path.startswith('LegionMap/') else 'prefabs_ui_legion'
        panels[key] = exporter.ui(bundle, '/' + path.split('/', 1)[1] + '.prefab')
        unique_sibling_paths(panels[key]['root'])
        print('Prefab', key, flush=True)
    # Runtime pools are not serialized as children of their host panels. Keep
    # their real prefabs too: replacing these with generic cards loses the UI.
    known = set(PANELS.values())
    for bundle in ['prefabs_ui_legion', 'prefabs_ui_legionmap']:
        paths = [p for p, obj in exporter.apk.container(bundle) if obj.type.name == 'GameObject']
        for path in paths:
            relative = path.removeprefix('Assets/Prefabs/UI/').removesuffix('.prefab')
            if relative in known or '/World/' in path or '/Eff_' in path or path.endswith('/SheepFarmingManager.prefab'):
                continue
            panel = exporter.ui(bundle, '/' + path.split('/', 4)[-1])
            if panel['root'] is None:
                continue
            unique_sibling_paths(panel['root'])
            panels[path.rsplit('/', 1)[-1].removesuffix('.prefab')] = panel
    # Achievement lists and confirmation dialogs are embedded original prefabs.
    # Export their subtrees without inventing another layout.
    def subtree(node, name):
        if node['name'] == name:
            return node
        return next((found for child in node['children'] if (found := subtree(child, name))), None)
    for key, host, name in [('achievement', 'members', 'AchievementPanelWithTypes'),
                            ('achievement_row', 'members', 'AchievementPanelItem'),
                            ('rank_row', 'members', 'TopRankPersonalItem')]:
        root = subtree(panels[host]['root'], name)
        assert root, name
        panels[key] = {'root': root, 'source': dict(panels[host]['source'], subtree=root['path'])}
    for bundle, suffix in [('confirmdialog', 'ConfirmDialog'), ('inputdialog', 'InputDialog'),
                           ('chat', 'ChatPanel'), ('chat', 'MsgItem/NormalMsg'), ('chat', 'ContentItem/ContentDialog'), ('chat', 'MsgItem/MsgItem'),
                           ('fight_gameendpanellegionboss', 'GameEndPanelLegionBoss'),
                           ('fight_gameendpanellegionfight', 'GameEndPanelLegionFight'),
                           ('fight_gameendpanellegionplunder', 'GameEndPanelLegionPlunder')]:
        key = suffix.rsplit('/', 1)[-1]
        panels[key] = exporter.ui('prefabs_ui_' + bundle, '/' + suffix + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    # These original production backgrounds are screen-matched world canvases.
    # Retain their uGUI hierarchy; their parent Transform is not a UI node.
    for path, obj in exporter.apk.container('prefabs_ui_legionmap'):
        if '/World/' not in path or not any(s in path for s in ['AffairContentUI', 'LegionMapLifeHangUpItem']) or obj.type.name != 'GameObject':
            continue
        obj = obj.deref()
        transform = next(c.component.read() for c in obj.read().m_Component if c.component.type.name == 'Transform')
        canvas = next(c.read().m_GameObject.deref() for c in transform.m_Children if c.read().m_GameObject.read().m_Name == 'WorldCanvas')
        key = path.rsplit('/', 1)[-1].removesuffix('.prefab')
        if 'LegionMapLifeHangUpItem' in path:
            t = next(c.component.read() for c in canvas.read().m_Component if c.component.type.name == 'RectTransform')
            canvas = t.m_Children[0].read().m_GameObject.deref()
        panels[key] = dict(root=exporter.ui_node(canvas), source=dict(exporter.apk.provenance(obj), subtree='/WorldCanvas'))
        unique_sibling_paths(panels[key]['root'])
    exporter.save('guild-ui.json', panels)
    cache = ROOT / '.tools/source-packages/guild-skeletons'
    cache.mkdir(exist_ok=True)
    (cache / 'ui-jobs.json').write_text(json.dumps(exporter.skeleton_jobs), encoding='utf8')
    subprocess.run(['node', str(ROOT / 'scripts/extract_home_meshes.mjs'), str(cache / 'ui-jobs.json'), str(cache / 'ui-meshes.json')], check=True)
    skeletons = json.loads((cache / 'ui-meshes.json').read_text())
    for ident, data in skeletons.items():
        data['textures'] = exporter.skeleton_jobs[ident]['textures']
        data['scale'] = exporter.skeleton_jobs[ident]['scale']
    exporter.save('guild-ui-spine.json', skeletons)
    c = Constructors()
    mapping = {m[2]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', c.source('Tables'))}
    tables, records = {}, {}
    for name, constructor in mapping.items():
        if not name.startswith(('legion_', 'stage_legion')) and name != 'achievement':
            continue
        path = ROOT / '.tools/source-packages/official-data' / (name + '.g.bytes')
        raw = path.read_bytes()
        tables[name] = c.table(raw, constructor)
        records[name] = {'text_asset': name + '.g', 'bytes': len(raw), 'consumed_bytes': len(raw),
                         'sha256': hashlib.sha256(raw).hexdigest(),
                         'constructor': 'Console.dll/Common/' + constructor + '.cs',
                         'constructor_sha256': hashlib.sha256((SOURCE / (constructor + '.cs')).read_bytes()).hexdigest()}
    rules = json.loads((ART / 'rules.json').read_text(encoding='utf8'))
    requests = {}
    for table, field, id_field in [('legion_map_building', 'Icon', 'Id'), ('legion_icon', 'Icon', 'Id'),
                                 ('legion_map_skill', 'Icon', 'SkillClassId')]:
        for row in tables[table]:
            requests[table + ':' + str(row[id_field])] = row[field]
    for row in tables['legion_map_skill']:
        requests['legion_map_skill_bg:' + str(row['SkillClassId'])] = row['BgIcon']
    item_ids = {5, 400, 401, 402, 405}
    for row in tables['achievement']:
        if row['AchievementType'] in [28, 54, 55]:
            item_ids.update(row['Reward'])
    for row in tables['legion_map_hangup_material']:
        item_ids.add(row['Id'])
        item_ids.update(row['PetFoodReward'])
        item_ids.update(row['PetAbilityFoodReward'])
    item_ids.update(row['ToolId'] for row in tables['legion_map_hangup_tool'])
    for ident in item_ids:
        item = next((x for x in rules['item'] if x['ClassId'] == int(ident)), None)
        if item:
            requests['item:' + str(ident)] = item['RawIcon']
    icons, missing, bundles = {}, [], {}
    for path, pointer in exporter.apk.container('image_ui_store'):
        if pointer.type.name == 'Sprite' and path.rsplit('/', 1)[-1].startswith('ImgStore_'):
            icons['store:' + path.rsplit('/', 1)[-1].removesuffix('.png')] = exporter.sprite(pointer)
    for ident, icon in requests.items():
        if not icon:
            continue
        asset_key = 'defaultpackage_assets_image_' + icon.replace('/', '_').lower()
        found = None
        for bundle in sorted(exporter.apk.index, key=len, reverse=True):
            if not (asset_key.startswith(bundle.removesuffix('.bundle') + '_') or
                    ident.startswith('item:') and any(x in bundle for x in ['image_itemicon_legion_', 'image_itemicon_box', 'image_itemicon_other_'])):
                continue
            if bundle not in bundles:
                try:
                    bundles[bundle] = {p.rsplit('/', 1)[-1].rsplit('.', 1)[0].lower(): ptr
                                       for p, ptr in exporter.apk.container(bundle) if ptr.type.name == 'Sprite'}
                except FileNotFoundError:
                    bundles[bundle] = {}
            found = bundles[bundle].get(icon.rsplit('/', 1)[-1].lower())
            if found:
                break
        if found:
            icons[ident] = exporter.sprite(found)
        else:
            missing.append({'id': ident, 'icon': icon})
    # Chinese localization keys embedded in prefabs, plus the official building labels.
    texts, language, text_sources = {}, {}, []
    for name in ['wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile']:
        raw = (ROOT / '.tools/source-packages/WwiseBnk' / name).read_bytes()
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            key, offset = struct.unpack_from('<Qi', raw, 4 + 12 * i)
            pos = start + offset
            length = struct.unpack_from('<H', raw, pos)[0]
            assert pos >= start and pos + 2 + length <= len(raw)
            language[key] = raw[pos + 2:pos + 2 + length].decode('utf8')
        text_sources.append({'name': name, 'sha256': hashlib.sha256(raw).hexdigest(), 'entries': count})
    def localize(node):
        key = node.get('localization_key')
        if key:
            value = language.get(xxhash.xxh64_intdigest(key.encode('utf8')), '')
            if value:
                texts[key] = value
                node['text']['value'] = value
        for child in node['children']:
            localize(child)
    for panel in panels.values():
        localize(panel['root'])
    keys = [f'item_{int(row["ClassId"])}_name' for row in rules['item']]
    keys += [f'ui_legion_map_building_{kind}_{row["Id"]}' for row in tables['legion_map_building'] for kind in ['name', 'desc']]
    keys += [f'ui_legion_map_skill_{kind}_{row["SkillClassId"]}' for row in tables['legion_map_skill'] for kind in ['name', 'desc']]
    for key in keys:
        value = language.get(xxhash.xxh64_intdigest(key.encode('utf8')), '')
        if value:
            texts[key] = value
    exporter.save('guild-ui.json', panels)
    exporter.save('guild.json', {'tables': tables, 'icons': icons, 'texts': texts})
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf8'))
    manifest['resources'].update(exporter.records)
    for name in ['guild-ui.json', 'guild.json', 'guild-ui-spine.json']:
        manifest['data_sha256'][name] = hashlib.sha256((ART / name).read_bytes()).hexdigest()
    save(REFERENCE / 'source-manifest.json', manifest)
    reference = ROOT / 'assets/reference/guild-ui'
    reference.mkdir(parents=True, exist_ok=True)
    source = {'version': '854_156383', 'prefabs': {k: v['source'] for k, v in panels.items()},
              'resources': exporter.records, 'tables': records, 'missing_icons': missing, 'localization': text_sources,
              'adaptation': 'Offline NPC guilds and members; original construction, production, study, donation, life, shop, boss and war rules. Local combat and turn-based war scheduling are documented separately.'}
    save(reference / 'source-manifest.json', source)
    write_resources(manifest)
    print('Guild:', len(panels), 'prefabs,', len(tables), 'tables,', len(icons), 'icons; missing', missing, flush=True)


if __name__ == '__main__':
    build()
