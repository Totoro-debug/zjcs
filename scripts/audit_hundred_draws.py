"""Verify hundred-draw thresholds against the pinned APK and original prefab.

Reads verified local sources only. Writes an evidence report, no runtime assets.
"""
import hashlib
import json
import re
import struct

from official_apk import Apk, ROOT
from official_tables import Constructors


def digest(data):
    return hashlib.sha256(data).hexdigest()


def audit():
    apk = Apk()  # Checks the pinned APK and CDN receipts before reading.
    constructors = Constructors()
    mapping = {m[2]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    runtime = json.loads((ROOT / 'assets/official/rules.json').read_text(encoding='utf8'))
    result = {'manifest': '854_156383',
              'apk_sha256': 'ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9',
              'tables': {}, 'client_code': {}, 'buttons': [], 'pools': []}
    tables = {}
    for name in ['gacha', 'chest', 'game_settings']:
        obj = apk.asset('config_luban', '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = json.loads(json.dumps(constructors.table(raw, mapping[name])))
        assert rows == runtime[name], 'Runtime differs from pinned table: ' + name
        tables[name] = rows
        result['tables'][name] = {'source': apk.provenance(obj), 'sha256': digest(raw),
                                  'bytes': len(raw), 'runtime_matches': True}
    settings = tables['game_settings']
    result['PreciousChestMaxOpenCount'] = settings['PreciousChestMaxOpenCount']
    assert result['PreciousChestMaxOpenCount'] == 100
    for cfg in tables['gacha']:
        type_ = int(cfg['GachaType'])
        if type_ not in [0, 2, 3, *range(4, 13)]:
            continue
        chest = next(r for r in tables['chest'] if r['ClassId'] == cfg['ChestId'])
        item = str(cfg['CostItemId'])
        assert 100 in chest['OpenCounts']
        result['pools'].append({'type': type_, 'chest': cfg['ChestId'], 'cost_item': int(item),
                                'cost_per_draw': chest['CostItems'][item],
                                'hundred_cost': chest['CostItems'][item] * 100,
                                'OpenCounts': chest['OpenCounts']})
    # Serialized ChestGachaDrawButton controls whether the hundred group is
    # active. Its min/max/dynamic fields are not retained by the UI image export.
    panel = apk.asset('prefabs_ui_chestgacha', '/ChestGachaPanel.prefab')
    result['panel_source'] = apk.provenance(panel)

    def walk(go, path=''):
        definition = go.read()
        path += '/' + definition.m_Name
        for entry in definition.m_Component:
            obj = entry.component.deref()
            if obj.type.name == 'RectTransform':
                for child in obj.read().m_Children:
                    walk(child.read().m_GameObject, path)
            elif obj.type.name == 'MonoBehaviour':
                mono = obj.read(check_read=False)
                if not mono.m_Script.path_id or mono.m_Script.read().m_ClassName != 'ChestGachaDrawButton':
                    continue
                raw = obj.get_raw_data()
                offset = (32 + len(mono.m_Name.encode('utf8')) + 3) & ~3
                count = struct.unpack_from('<i', raw, offset)[0]
                offset += 4 + count * 12 + 24  # Red-dot list; button and text PPtrs.
                minimum, maximum, fixed = struct.unpack_from('<3i', raw, offset)
                dynamic = bool(raw[offset + 12])
                assert offset + 16 == len(raw), 'Unconsumed button serialization: ' + path
                button = {'path': path, 'mMinOpenCount': minimum, 'mMaxOpenCount': maximum,
                          'mOpenCount': fixed, 'mIsDynamicCount': dynamic,
                          'sha256': digest(raw), 'source': apk.provenance(obj)}
                if '/HundredOpenBtn' in path:
                    assert dynamic and minimum == 100 and maximum == 100
                result['buttons'].append(button)

    walk(panel)
    assert len([b for b in result['buttons'] if '/HundredOpenBtn' in b['path']]) == 2
    for name, needles in {
        'GachaPanelBase': ['private void Refresh()', 'if ((num >= 100) & flag)'],
        'GachaResultPanel': ['RefreshMultiGacha()', '(num >= 100) & flag'],
        'PetGachaSubPanel': ['class PetGachaSubPanel : GachaPanelBase'],
        'PetGachaResultPanel': ['(num >= 100) & flag'],
        'ChestGachaDrawButton': ['public void SetButtonByChest(', 'if (num < mMinOpenCount)'],
        'GachaChestBase': ['public virtual int GetMaxOpenCount(', 'public void RefreshOpenCount()'],
        'PreciousChestPanel': ['private void RefreshGachaChest(', 'mGachaHundredBtn.SetButtonByChest('],
        'ElementChestPanel': ['HundredCostMoneyContent.SetButtonByChest('],
        'ChestGachaAwardPanel': ['HundredMode'],
        'GachaPanelUtils': ['public static void HundredGachePreRemind('],
    }.items():
        path = ROOT / '.tools/source-packages/official-code/client-source' / (name + '.cs')
        raw = path.read_bytes()
        lines = raw.decode('utf-8-sig').splitlines()
        matches = {needle: [i for i, line in enumerate(lines, 1) if needle in line] for needle in needles}
        assert all(matches.values()), 'Missing original code evidence: ' + name
        result['client_code'][name] = {'sha256': digest(raw), 'lines': matches}
    result['conclusion'] = {
        'condition': 'The selected pool supports 100 and its matching material affords 100 individual draws.',
        'mode': 'Hundred replaces ten; single stays visible; results recalculate against the current balance.',
        'persistent_unlock': False,
        'level_or_lifetime_draw_gate': False,
        'daily_limit': 'Checked when submitting the batch, separately from hundred-button visibility.',
        'confirmation': 'Original client asks for hundred confirmation, with a per-pool ignore-today setting; this is not an unlock gate.',
        'scope': 'Confirms the inventory threshold. Online material purchase and server lottery internals are outside the offline port.'}
    target = ROOT / 'assets/reference/hundred-draws/source-audit.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print(f'Hundred source audit: {len(result["pools"])} pools, {len(result["buttons"])} original buttons, three APK tables match.')


if __name__ == '__main__':
    audit()
