"""Read TreasurePanel's serialized filter values, without inferring button order."""
import hashlib
import struct
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr


def extract_treasure_bindings(exporter, panels, sources, keys=None, classes=None):
    schemas = ScriptSchemas()
    classes = classes or {'TreasureDetailDialog', 'TreasureSuitPropItem', 'TreasureDetailSuitItem',
                         'TreasureRow', 'TreasurePieceTabItem'}
    keys = keys or ['treasure_detail', 'relic_suit_member', 'relic_piece_row', 'relic_suit']

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
                    if sibling.path_id == transform.object_reader.path_id: break
                    if sibling.read().m_GameObject.read().m_Name == name: occurrence += 1
                if occurrence: name += f' ({occurrence})'
            names.insert(0, name)
            if not transform.m_Father.path_id: break
            transform = transform.m_Father.read()
            obj = transform.m_GameObject.read()
        return '/' + '/'.join(names)

    def convert(value, component):
        if isinstance(value, dict):
            if 'm_PathID' in value:
                if not value['m_PathID']: return None
                ptr = PPtr(m_FileID=value['m_FileID'], m_PathID=value['m_PathID'],
                           assetsfile=component.assets_file)
                return unique_path(ptr)
            return {key: str(entry) if key == 'rid' else convert(entry, component)
                    for key, entry in value.items()}
        if isinstance(value, list): return [convert(entry, component) for entry in value]
        return value

    bindings = {}
    receipts = []
    for bundle in {panels[key]['source']['bundle'] for key in keys}:
        for obj in exporter.apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour': continue
            cls, raw, basic = exporter.mono(obj)
            if cls not in classes: continue
            fields = schemas.read(obj)
            path = unique_path(basic.m_GameObject)
            bindings.setdefault(path, {})[cls] = {key: convert(value, obj) for key, value in fields.items()
                if key not in ['m_GameObject', 'm_Script', 'm_Name', 'm_Enabled']}
            receipts.append(dict(exporter.apk.provenance(obj), object_path=path, script=cls,
                sha256=hashlib.sha256(raw).hexdigest(), operation='Complete original schema read; original pointer paths and colors'))

    def enrich(node):
        if node['path'] in bindings: node['original_bindings'] = bindings[node['path']]
        for child in node['children']: enrich(child)
    for key in keys:
        enrich(panels[key]['root'])
    sources['treasure_bindings'] = receipts


def extract_treasure_filters(exporter, panel, sources):
    values = {}
    receipts = []
    for obj in exporter.apk.objects(panel['source']['bundle']):
        if obj.type.name != 'MonoBehaviour':
            continue
        cls, raw, basic = exporter.mono(obj)
        if cls not in ['FilterGroupItemForRank', 'FilterGroupItemForProperty', 'TreasureSortGroupItem']:
            continue
        offset = (32 + len(basic.m_Name.encode()) + 3) & ~3
        if cls != 'TreasureSortGroupItem' and len(raw) != offset + 4:
            raise ValueError('Unexpected original filter fields: ' + cls)
        fields = {'kind': cls, 'value': struct.unpack_from('<i', raw, offset)[0]}
        if cls == 'TreasureSortGroupItem':
            fields['reverse'] = bool(raw[offset + 4])
        path = exporter.object_path(basic.m_GameObject)
        values[path] = fields
        receipts.append(dict(exporter.apk.provenance(obj), object_path=path, script=cls,
                             sha256=hashlib.sha256(raw).hexdigest(), field_offset=offset,
                             operation='Original FilterType / SortType enum and IsReverse fields'))

    def enrich(node):
        if node['path'] in values:
            node['treasure_filter'] = values[node['path']]
        for child in node['children']:
            enrich(child)

    enrich(panel['root'])
    sources['treasure_filter_fields'] = receipts
