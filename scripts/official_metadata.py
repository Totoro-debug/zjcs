"""Read configuration field definitions from this APK's IL2CPP metadata v31.

This does not claim to decompile executable methods or server logic. Schemas
are exported with each table and a table is accepted only after consuming its
entire payload, without guessed trailing fields.
"""
import bisect
import struct

from official_apk import LubanReader


class Metadata:
    def __init__(self, data):
        self.data = data
        self.header = struct.unpack_from('<64I', data)
        if self.header[:2] != (0xfab11baf, 31):
            raise ValueError('Expected the pinned APK metadata version 31')
        h = self.header
        self.types = []
        self.fields = []
        for offset in range(h[24], h[24] + h[25], 12):
            name, type_index, token = struct.unpack_from('<IiI', data, offset)
            self.fields.append({'name': self.string(name), 'type_index': type_index, 'token': token})
        self.defaults = {}
        for offset in range(h[16], h[16] + h[17], 12):
            field, kind, index = struct.unpack_from('<iii', data, offset)
            if index >= 0:
                self.defaults[field] = index
        for index, offset in enumerate(range(h[40], h[40] + h[41], 88)):
            v = struct.unpack_from('<16i8H2I', data, offset)
            fields = [dict(self.fields[j], index=j) for j in range(v[8], v[8] + v[18])]
            self.types.append({'index': index, 'name': self.string(v[0]), 'namespace': self.string(v[1]),
                               'byval': v[2], 'parent': v[4], 'flags': v[7], 'fields': fields,
                               'enum': bool(v[-2] & 2)})
        self.sorted = sorted(self.types, key=lambda t: t['byval'])
        self.type_indices = [t['byval'] for t in self.sorted]
        self.common = {t['name']: t for t in self.types if t['namespace'] == 'Common'}
        self.ids = {}
        for t in self.types:
            field = next((f for f in t['fields'] if f['name'] == '__ID__'), None)
            if field and field['index'] in self.defaults:
                self.ids[self.default_int(field['index'])] = t

    def string(self, index):
        start = self.header[6] + index
        return self.data[start:self.data.index(b'\0', start)].decode('utf-8')

    def default_int(self, field):
        start = self.header[18] + self.defaults[field]
        data = self.data
        n = data[start]
        if n < 0x80:
            value = n
        elif n < 0xc0:
            value = ((n & 0x7f) << 8) | data[start + 1]
        elif n < 0xe0:
            value = ((n & 0x3f) << 24) | int.from_bytes(data[start + 1:start + 4], 'big')
        elif n == 0xf0:
            value = int.from_bytes(data[start + 1:start + 5], 'little')
        elif n == 0xfe:
            value = 0xfffffffe
        elif n == 0xff:
            return -2147483648
        else:
            raise ValueError(f'Invalid metadata constant {n}')
        return -(value // 2 + 1) if value & 1 else value // 2

    def type_for(self, index):
        return self.sorted[bisect.bisect_right(self.type_indices, index) - 1]

    def schema(self, name):
        t = self.common[name] if isinstance(name, str) else name
        fields = t['fields']
        # Generated table data are readonly. Earlier fields belong to partial
        # runtime classes and are computed after deserialization.
        starts = {'ItemInfo': 'ClassId', 'SkillInfo': 'ClassId', 'PetInfo': 'ClassId',
                  'TreasureInfo': 'ClassId', 'FastHangUpInfo': 'FastHangUpId'}
        if t['name'] in starts:
            fields = fields[next(i for i, f in enumerate(fields) if f['name'] == starts[t['name']]):]
        return [f for f in fields if f['name'] != '__ID__'
                and not f['name'].startswith(('<', 'm'))]

    def read_value(self, reader, index):
        collections = {53207: ('int',), 53211: ('int',), 53222: ('int',), 52526: ('double',), 54636: ('string',),
                       24200: ('int', 'int'), 24191: ('int', 'int'), 23984: ('int', 'double'),
                       53281: ('int',), 52579: ('int',), 54164: ('int',), 52554: ('int',),
                       54463: ('int',), 53065: ('int',), 25697: ('int', 'double'),
                       22863: ('int', 'int')}
        if index in collections:
            return reader.collection(*collections[index])
        if index < 76000:
            raise ValueError(f'Unresolved generic field type {index} at byte {reader.pos}')
        t = self.type_for(index)
        if t['enum']:
            return reader.integer()
        primitives = {'Int32': 'int', 'Int64': 'int', 'Single': 'float', 'Double': 'double',
                      'String': 'string', 'Boolean': 'bool'}
        if t['name'] in primitives:
            return reader.scalar(primitives[t['name']])
        if t['flags'] & 0x80 or t['flags'] & 0x20:  # abstract/interface union
            ident = reader.integer()
            if ident == 0:
                return None
            if ident not in self.ids:
                raise ValueError(f'Unknown union type {ident} at byte {reader.pos}')
            subtype = self.ids[ident]
            return dict(self.read_bean(reader, subtype), _type=subtype['name'])
        return self.read_bean(reader, t)

    def read_bean(self, reader, name):
        result = {}
        t = self.common[name] if isinstance(name, str) else name
        parent = self.type_for(t['parent']) if t['parent'] >= 76000 else None
        if parent and parent['name'] not in ['ICondition', 'ItemPartInfo', 'BeanBase', 'Object']:
            result.update(self.read_bean(reader, parent))
        for f in self.schema(name):
            result[f['name']] = self.read_value(reader, f['type_index'])
        return result

    def table(self, data, name):
        r = LubanReader(data)
        count = r.uint()
        if count > 100000:
            raise ValueError('Invalid table row count')
        rows = [self.read_bean(r, name) for _ in range(count)]
        if r.pos != len(data):
            raise ValueError(f'{name}: decoded {r.pos} of {len(data)} bytes')
        return {'schema': [{'name': f['name'], 'type_index': f['type_index']} for f in self.schema(name)], 'rows': rows}
