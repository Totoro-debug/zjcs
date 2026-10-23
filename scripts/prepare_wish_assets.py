"""Recheck pinned wish tables/prefabs and restore Sprite atlas trim metadata.

Only the wish shell and its two statue sprites are updated. No painting and no
newer-version substitution; Apk verifies the original APK and cached bundles.
"""
import hashlib
import json
import re
import struct

from official_apk import ROOT
from official_tables import Constructors
from prepare_official_city import Exporter, ART, REFERENCE
from official_resource_manifest import write_resources
from UnityPy.classes.PPtr import PPtr


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    exporter = Exporter()
    screens = json.loads((ART / 'ui.json').read_text(encoding='utf-8'))
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf-8'))
    evidence = {'manifest': '854_156383', 'apk_sha256': sha(ROOT / '.tools/source-packages/zjcs_M509085.apk'),
                'sprites': [], 'tables': {}, 'client_code': {}}
    for key in ['gacha_shell', 'gacha_background', 'gacha_senior_background']:
        screen = screens[key]
        by_path = {}

        def index(node):
            by_path[node['path']] = node
            for child in node['children']:
                index(child)

        index(screen['root'])
        obj = exporter.apk.asset(screen['source']['bundle'], screen['source']['prefab'])

        def walk(go, path=''):
            data = go.read()
            path += '/' + data.m_Name
            for component in data.m_Component:
                reader = component.component.deref()
                if reader.type.name == 'MonoBehaviour' and path in by_path and 'image' in by_path[path]:
                    cls, raw, _ = exporter.mono(reader)
                    if cls in ['Image', 'PivotCenterImage', 'MirrorImage']:
                        file_id, path_id = struct.unpack_from('<iq', raw, 88)
                        ptr = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=reader.assets_file)
                        sprite = exporter.sprite(ptr)
                        if sprite:
                            by_path[path]['image']['sprite'] = sprite
                            if 'PivotCenter' in path or key.endswith('background'):
                                evidence['sprites'].append({'prefab': key, 'path': path, 'sprite': sprite,
                                    'source': exporter.apk.provenance(ptr.deref())})
                elif reader.type.name == 'RectTransform':
                    for child in reader.read().m_Children:
                        walk(child.read().m_GameObject, path)

        walk(obj)
    # Compare actual APK TextAssets with the runtime rules through the original
    # generated Luban constructors, including complete byte consumption.
    constructors = Constructors()
    mapping = {m[2]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    runtime = json.loads((ART / 'rules.json').read_text(encoding='utf-8'))
    for name in ['gacha', 'chest', 'reward_rule', 'random_reward', 'profession_base',
                 'skill', 'item', 'achievement', 'shop', 'shop_goods', 'shop_goods_group',
                 'game_settings', 'gacha_pity_probability']:
        source = exporter.apk.asset('config_luban', '/' + name + '.g.bytes').read()
        raw = source.m_Script.encode('utf-8', 'surrogateescape')
        decoded = constructors.table(raw, mapping[name])
        assert json.loads(json.dumps(decoded)) == runtime[name], f'Runtime table differs from pinned source: {name}'
        evidence['tables'][name] = {'sha256': hashlib.sha256(raw).hexdigest(), 'bytes': len(raw),
                                   'runtime_matches': True}
    icons = json.loads((ART / 'icons.json').read_text(encoding='utf-8'))
    evidence['reward_icons'] = {}
    needed = {int(item) for row in runtime['achievement'] if row['AchievementType'] in [7,20,60,61] for item in row['Reward']}
    for item_id in sorted(needed):
        if str(item_id) in icons and item_id != 6800:
            continue
        item = next(row for row in runtime['item'] if row['ClassId'] == item_id)
        icon = item['RawIcon']
        prefix = 'defaultpackage_assets_image_' + icon.rsplit('/',1)[0].replace('/','_').lower()
        searched, found = [], None
        for bundle in exporter.apk.index:
            if not bundle.startswith(prefix):
                continue
            searched.append(bundle)
            try:
                found = next((ptr for path,ptr in exporter.apk.container(bundle)
                    if path.rsplit('/',1)[-1].rsplit('.',1)[0].lower() == icon.rsplit('/',1)[-1].lower()
                    and ptr.type.name == 'Sprite'), None)
            except FileNotFoundError:
                continue
            if found:
                break
        if found:
            icons[str(item_id)] = exporter.sprite(found)
            evidence['reward_icons'][str(item_id)] = {'sprite': icons[str(item_id)], 'source': exporter.apk.provenance(found.deref())}
            if item_id == 6800:
                manifest['unsupported']['wish_first_score_reward_icon']['resolution'] = {
                    'date': '2026-10-13', 'status': 'Converted and used in score rewards and gift lists',
                    'file': icons[str(item_id)]['file'], 'source_audit': 'wish-source-audit.json',
                    'source': exporter.apk.provenance(found.deref())}
        else:
            evidence['reward_icons'][str(item_id)] = {'missing': icon, 'searched': searched}
    (ART / 'icons.json').write_text(json.dumps(icons, ensure_ascii=False, separators=(',',':')), encoding='utf-8')
    code = ROOT / '.tools/source-packages/official-code'
    for folder, names in {
        'client-source': ['SkillGachaPanel', 'SkillSeniorGachaPanel', 'GachaPanelBase', 'GachaProbabilityContent',
                          'GachaDetailInfoSkillGachaProvider', 'ScoreRewardUI', 'GachaHistoryContent'],
        'common-source/Common': ['ItemFilterForProfessionSkill', 'ItemFilterForProfessionUnlockedSkill',
                                'AchievementRawInfoParser', 'AchievementInfo', 'CycleTemplateInfo', 'SkillGachaSettings',
                                'ProfessionBaseInfoParser', 'PlayerGachaHistoryManager'],
        'framework-firstpass-source': ['PivotCenterImage'],
    }.items():
        for name in names:
            path = code / folder / (name + '.cs')
            evidence['client_code'][f'{folder}/{name}.cs'] = sha(path)
    (ART / 'ui.json').write_text(json.dumps(screens, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    manifest['resources'].update(exporter.records)
    manifest['data_sha256']['ui.json'] = sha(ART / 'ui.json')
    manifest['data_sha256']['icons.json'] = sha(ART / 'icons.json')
    (REFERENCE / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    (REFERENCE / 'wish-source-audit.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding='utf-8')
    write_resources(manifest)
    print(f'Wish source audit: {len(evidence["tables"])} original tables match; {len(evidence["sprites"])} sprite geometries restored.')


if __name__ == '__main__':
    build()
