"""Bind newly referenced gift icons after decoding their current official Sprite."""
import hashlib
import io
import json
from pathlib import Path

from official_apk import Apk, ROOT
from refresh_current_character_meshes import preserve


def build():
    table_path = ROOT / '.tools/source-packages/current/871_156598/official-data/tables.json'
    tables = json.loads(table_path.read_bytes())
    icon_path = ROOT / 'assets/official/icons.json'
    icons = json.loads(icon_path.read_bytes())
    items = {row['ClassId']: row for row in tables['item']}
    needed = {int(key) for row in tables['weekly_pay_gift'] for key in row['Award']}
    missing = sorted(item for item in needed if str(item) not in icons)
    if not missing:
        return []
    city_path = ROOT / 'assets/reference/official-city/source-manifest.json'
    city = json.loads(city_path.read_bytes())
    apk = Apk.current()
    records = []
    for item_id in missing:
        item = items[item_id]
        candidates = [icons[str(other_id)] for other_id, other in items.items()
                      if other['RawIcon'] == item['RawIcon'] and str(other_id) in icons]
        if not candidates or any(icon != candidates[0] for icon in candidates):
            raise ValueError('Missing or ambiguous shared icon: ' + str(item_id))
        icon = candidates[0]
        source = city['resources'][icon['file']]
        obj = next(obj for obj in apk.objects(source['bundle']) if str(obj.path_id) == source['path_id'])
        if obj.type.name != 'Sprite' or obj.read().m_Name != icon['name']:
            raise ValueError('Current gift icon is not the expected Sprite')
        buffer = io.BytesIO()
        obj.read().image.save(buffer, format='PNG')
        original = ROOT / icon['file'].removeprefix('res://')
        if buffer.getvalue() != original.read_bytes():
            raise ValueError('Current gift Sprite differs from the existing decoded icon')
        icons[str(item_id)] = icon
        records.append({'item_id': item_id, 'raw_icon': item['RawIcon'], 'output': icon['file'],
                        'sha256': hashlib.sha256(buffer.getvalue()).hexdigest(),
                        'source': apk.provenance(obj)})
    prior = preserve(icon_path)
    icon_path.write_text(json.dumps(icons, ensure_ascii=False, separators=(',', ':')), 'utf8')
    city.setdefault('data_sha256', {})['icons.json'] = hashlib.sha256(icon_path.read_bytes()).hexdigest()
    preserve(city_path)
    city_path.write_text(json.dumps(city, ensure_ascii=False, indent=2) + '\n', 'utf8')
    receipt = {'manifest': apk.version, 'operation': __doc__, 'previous': prior,
               'table_sha256': hashlib.sha256(table_path.read_bytes()).hexdigest(),
               'generator_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
               'bindings': records, 'icons_sha256': hashlib.sha256(icon_path.read_bytes()).hexdigest()}
    output = ROOT / 'assets/reference/commerce-components/current-gift-icons-source-manifest.json'
    output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return records


if __name__ == '__main__':
    print('New current gift icon bindings:', len(build()))
