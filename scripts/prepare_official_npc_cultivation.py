"""Export fixed-version NPC friendship/profile evidence without shared writes.

The integration owner merges npc-cultivation-ui.json and registers resources.
No central UI, language, source manifest or asset-pack lock is written here.
"""
import hashlib
import json
import re
import struct
import xxhash
from official_apk import ROOT
from official_tables import Constructors, SOURCE
from prepare_official_panels import PanelExporter, unique_sibling_paths
from prepare_fidelity_treasure import extract_treasure_bindings
from prepare_map_props import decode


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':'),
                               allow_nan=False), encoding='utf8')


def enrich_layout(exporter, panels, sources):
    """Retain TMP wrapping and ClampingScrollRect's serialized clamp ranges."""
    values, receipts = {}, []
    for obj in exporter.apk.objects(panels['partner_detail']['source']['bundle']):
        if obj.type.name != 'MonoBehaviour':
            continue
        cls, raw, basic = exporter.mono(obj)
        if cls not in ['TextMeshProUGUI', 'ClampingScrollRect']:
            continue
        path = exporter.object_path(basic.m_GameObject)
        if not path.startswith('/NpcDetailPanel/'):
            continue
        if cls == 'TextMeshProUGUI':
            length = struct.unpack_from('<i', raw, 88)[0]
            aligned = (92 + length + 3) & ~3
            shared = struct.unpack_from('<i', raw, aligned + 28)[0]
            materials = struct.unpack_from('<i', raw, aligned + 44 + 12 * shared)[0]
            style = aligned + 12 * (shared + materials)
            wrapping = struct.unpack_from('<i', raw, style + 256)[0]
            overflow = struct.unpack_from('<i', raw, style + 264)[0]
            if wrapping not in (0, 1) or overflow not in range(7):
                raise ValueError('Unexpected original NPC text layout: ' + path)
            values[path] = dict(text=dict(word_wrap=bool(wrapping), overflow=overflow,
                margin=list(struct.unpack_from('<4f', raw, style + 352))))
        else:
            # The subclass appends two aligned bools, two Vector2s and one
            # aligned bool after ScrollRect. No managed-reference fields.
            offset = len(raw) - 28
            width, height, wmin, wmax, hmin, hmax, move = struct.unpack_from('<ii4fi', raw, offset)
            if any(v not in (0, 1) for v in [width, height, move]) or wmin > wmax or hmin > hmax:
                raise ValueError('Unexpected original NPC clamping layout: ' + path)
            values[path] = dict(clamping_scroll=dict(width=bool(width), height=bool(height),
                width_range=[wmin, wmax], height_range=[hmin, hmax], clamp_move=bool(move)))
        receipts.append(dict(exporter.apk.provenance(obj), object_path=path, script=cls,
            sha256=hashlib.sha256(raw).hexdigest(), operation='Original NPC text layout or trailing ClampingScrollRect fields'))

    def enrich(node):
        for key, value in values.get(node['path'], {}).items():
            if key == 'text':
                node[key].update(value)
            else:
                node[key] = value
        for child in node['children']:
            enrich(child)
    enrich(panels['partner_detail']['root'])
    sources['layout_fields'] = receipts


def build():
    art = ROOT / 'assets/official'
    exporter = PanelExporter()
    rules = json.loads((art / 'rules.json').read_text('utf8'))
    constructors = Constructors()
    mapping = {m[2].rsplit('/', 1)[-1]: m[1] for m in re.finditer(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    tables, inputs = {}, {}
    for name in ['npc_profile', 'npc_talent_tree_map', 'npc_talent_tree_map_prop',
                 'npc_talent_tree_gift_transform']:
        source = ROOT / '.tools/source-packages/official-data' / (name + '.g.bytes')
        if not source.exists():
            continue
        raw = source.read_bytes()
        original = exporter.apk.asset('config_luban', '/' + name + '.g.bytes')
        if original.read().m_Script.encode('utf8', 'surrogateescape') != raw:
            raise ValueError('Cached NPC table differs from fixed source: ' + name)
        tables[name] = constructors.table(raw, mapping[name])
        inputs[name] = dict(sha256=hashlib.sha256(raw).hexdigest(),
                            source=exporter.apk.provenance(original),
                            constructor=mapping[name], consumed_bytes=len(raw),
                            constructor_sha256=hashlib.sha256(
                                (SOURCE / (mapping[name] + '.cs')).read_bytes()).hexdigest())
    if 'npc_profile' not in tables:
        raise ValueError('Pinned npc_profile is required')
    wanted = {int(row['LevelPropId']) for row in rules['npc'] if row['CanDevelop']}
    wanted.discard(0)
    binary = ROOT / '.tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    raw = binary.read_bytes()
    props = decode(raw, wanted)
    receipts = json.loads((binary.parent / 'bundle-sources.json').read_text('utf8'))
    inputs['level_prop'] = dict(source=receipts[binary.name],
                               sha256=hashlib.sha256(raw).hexdigest(), classes=sorted(wanted))
    language = {}
    for name in ['wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile']:
        raw = (binary.parent / name).read_bytes()
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            key, offset = struct.unpack_from('<Qi', raw, 4 + i * 12)
            pos = start + offset
            length = struct.unpack_from('<H', raw, pos)[0]
            language[key] = raw[pos + 2:pos + 2 + length].decode('utf8')
        inputs[name] = dict(sha256=hashlib.sha256(raw).hexdigest(), source=receipts[name])
    texts = {}
    for row in tables['npc_profile']:
        key = f"npc_profile_Content_{row['Id']}_{row['ProfileId']}"
        value = language.get(xxhash.xxh64_intdigest(key.encode()))
        if value is None:
            raise ValueError('Missing pinned profile text: ' + key)
        texts[key] = value
    panels = {'partner_detail': json.loads((art / 'ui.json').read_text('utf8'))['partner_detail']}
    inputs['partner_detail'] = dict(source=panels['partner_detail']['source'],
        cached_definition_sha256=hashlib.sha256(json.dumps(panels['partner_detail'], ensure_ascii=False,
            separators=(',', ':')).encode('utf8')).hexdigest())
    panels['partner_prop_group'] = exporter.ui('prefabs_ui_npc', '/NpcFriendshipPropItemGroup.prefab')
    unique_sibling_paths(panels['partner_prop_group']['root'])
    sources = {}
    extract_treasure_bindings(exporter, panels, sources, list(panels), {
        'NpcDetailPanel', 'NpcDetailPanelPropContent', 'NpcDetailPanelProfileContent',
        'NpcDetailPanelProfileContentTabItem', 'NpcFriendshipPropItem',
        'NpcFriendshipPropItemGroup', 'PlayerPropItem'})
    enrich_layout(exporter, panels, sources)
    for key in ['ui_npc_detail_tab_item_friend_text', 'ui_npc_detail_tab_item_gift_text',
                'ui_npc_detail_tab_item_profile_text', 'conditon_unlock_desc_NpcFriendshipLevel']:
        value = language.get(xxhash.xxh64_intdigest(key.encode()))
        if value is not None:
            texts[key] = value
    save(art / 'npc-cultivation.json', dict(tables=tables, props=props, texts=texts))
    save(art / 'npc-cultivation-ui.json', panels)
    code = ROOT / '.tools/source-packages/official-code'
    names = ['common-source/Common/NpcFriendshipLevelPropChunck.cs',
             'common-source/Common/NpcInfoParser.cs', 'common-source/Common/NpcProfileInfo.cs',
             'common-source/Common/BattleFormulaHandler.cs', 'common-source/PlayerServiceProxyImpl.cs',
             'client-source/NpcDetailPanelPropContent.cs', 'client-source/NpcDetailPanelProfileContent.cs',
             'client-source/NpcFriendshipPropItem.cs', 'framework-source/ClampingScrollRect.cs',
             'common-source/NpcComparer.cs', 'common-source/Common/NpcFriendshipLevelCondition.cs']
    sources.update(manifest='854_156383', tables=inputs,
        code={name: hashlib.sha256((code / name).read_bytes()).hexdigest() for name in names},
        resources=exporter.records,
        outputs={name: hashlib.sha256((art / name).read_bytes()).hexdigest()
                 for name in ['npc-cultivation.json', 'npc-cultivation-ui.json']},
        operation='Original level_prop rows, complete Luban profile constructors, language and original serialized UI fields; no central writes')
    save(ROOT / 'assets/reference/npc-cultivation/source-manifest.json', sources)
    print('NPC cultivation:', len(props), 'property classes,', len(tables['npc_profile']),
          'profiles,', len(sources['treasure_bindings']), 'UI bindings')


if __name__ == '__main__':
    build()
