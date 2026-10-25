"""Interpret the generated Luban constructors extracted from Console.dll.

Only the small, deterministic constructor language is accepted. No client
assembly is executed, and complete payload consumption is mandatory.
"""
import json
from pathlib import Path
import re
from datetime import datetime, timezone

from official_apk import ROOT, LubanReader
from official_metadata import Metadata

SOURCE = ROOT / '.tools/source-packages/official-code/common-source/Common'


class Record(dict):
    def __getattr__(self, key):
        return self[key]


def split_args(value):
    depth, start, result = 0, 0, []
    for i, ch in enumerate(value):
        if ch in '([<':
            depth += 1
        elif ch in ')]>':
            depth -= 1
        elif ch == ',' and depth == 0:
            result.append(value[start:i].strip())
            start = i+1
    result.append(value[start:].strip())
    return result


class Constructors:
    def __init__(self, code_root=None):
        code_root = Path(code_root) if code_root is not None else ROOT / '.tools/source-packages/official-code'
        self.source_root = code_root / 'common-source/Common'
        self.metadata = Metadata((code_root / 'global-metadata.dat').read_bytes())
        self.cache = {}
        self.sources = {}

    def source(self, name):
        if name not in self.sources:
            path = self.source_root / (name+'.cs')
            if not path.exists() or name in ['Pos2d', 'Vector2', 'Vector3']:
                path = self.source_root.parent / 'Common.LubanGen' / (name+'.cs')
            self.sources[name] = path.read_text(encoding='utf-8-sig') if path.exists() else ''
        return self.sources[name]

    def expression(self, expression):
        expression = expression.strip().replace('null', 'None').replace('true', 'True').replace('false', 'False')
        expression = re.sub(r'DateTimeOffset\.FromUnixTimeSeconds\((.*?)\)\.UtcDateTime',
                            r'utc_time(\1)', expression)
        expression = re.sub(r'\(\((\w+) != None && \1\.Count > 0\) \? \1 : None\)',
                            r'nonempty(\1)', expression)
        if expression == 'PropDictionaryUtils.Create()':
            # Native helper creates an empty property dictionary; its memory
            # optimization does not change the serialized key/value pairs.
            return '{}'
        external = re.fullmatch(r'LubanExternalTypes\.New\w+\((.*)\)', expression)
        if external:
            return self.expression(external[1])
        expression = expression.replace('Common.LubanGen.', '')
        expression = expression.replace('Common.', '')
        expression = re.sub(r'\([A-Za-z_][\w.]*\)(?=\w)', '', expression)
        if expression.startswith('new '):
            match = re.fullmatch(r'new [\w.<> ,]+\[([^\[\]]+)\](?:\[\])*', expression)
            if match:
                return '[None] * (' + self.expression(match[1]) + ')'
            if expression.startswith('new HashSet<'):
                return '[]'
            if re.fullmatch(r'new (Dictionary|List)<.+>\[.+\]', expression):
                size = re.search(r'\[([^\[\]]+)\]$', expression)[1]
                return '[None] * (' + self.expression(size) + ')'
            if expression.startswith(('new Dictionary<', 'new SortedDictionary<')):
                return '{}'
            if expression.startswith('new List<'):
                return '[]'
            match = re.fullmatch(r'new (\w+)\((.*)\)', expression)
            if match:
                return f'decode({match[1]!r}, {self.expression(match[2])})'
        match = re.fullmatch(r'(\w+)\.Deserialize\w*\((.*)\)', expression)
        if match:
            return f'decode({match[1]!r}, {self.expression(match[2])})'
        mapping = {'ReadInt': 'integer', 'ReadSize': 'uint', 'ReadLong': 'long', 'ReadString': 'string',
                   'ReadNonNullString': 'string', 'ReadByte': 'byte',
                   'ReadBool': 'boolean', 'ReadFloat': 'float32', 'ReadDouble': 'float64'}
        for original, replacement in mapping.items():
            expression = expression.replace('_buf.'+original+'()', '_buf.'+replacement+'()')
        if re.search(r'[^\w\s.,\[\]()<>+*/=!-]', expression) or 'new ' in expression or 'Deserialize' in expression:
            raise ValueError('Unsupported constructor expression: '+expression)
        # Every remaining call must be one of the buffer methods above.
        for call in re.findall(r'([\w.]+)\(', expression):
            if call not in ['_buf.'+m for m in mapping.values()] + ['bean', 'nonempty', 'utc_time']:
                raise ValueError('Unsupported constructor call: '+call)
        return expression.replace(' / ', ' // ')

    def statements(self, body):
        # Preserve semicolons inside for headers and every brace as a token.
        result, start, depth = [], 0, 0
        for i, ch in enumerate(body):
            if ch == '(':
                depth += 1
            if ch == ')':
                depth -= 1
            if (ch in '{};' and depth == 0):
                part = body[start:i].strip()
                if part:
                    result.append(part)
                if ch != ';':
                    result.append(ch)
                start = i+1
        return result

    def compile(self, name, argc):
        key = (name, argc)
        if key in self.cache:
            return self.cache[key]
        source = self.source(name)
        path = SOURCE / (name+'.cs')
        if not source:
            return None
        matches = list(re.finditer(r'public '+re.escape(name)+r'\((ByteBuf _buf[^)]*)\)\s*(?:: base\(_buf\))?\s*\{', source))
        match = next((m for m in matches if len(split_args(m[1])) == argc), None)
        if match is None:
            primary = re.search(r'public struct '+re.escape(name)+r'\(ByteBuf _buf\)\s*\{(.*?)public static', source, re.S)
            if primary and argc == 1:
                lines = []
                for field, expr in re.findall(r'public readonly \S+ (\w+) = ([^;]+);', primary[1]):
                    lines.append(field+' = '+self.expression(expr))
                self.cache[key] = (['_buf'], compile('\n'.join(lines), str(path), 'exec'))
                return self.cache[key]
            return None
        params = [s.split()[-1] for s in split_args(match[1])]
        pos, depth = match.end(), 1
        while depth:
            depth += (source[pos] == '{') - (source[pos] == '}')
            pos += 1
        body = source[match.end():pos-1]
        # These are serialized record initializers emitted by the generated
        # constructor. Keep every field expression and its original read order.
        body = re.sub(r'new \w+\s*\{([^{}]*)\}', lambda m: 'bean(' + m[1].strip().rstrip(',') + ')', body)
        tokens = self.statements(body)
        lines, indent, stack, pending = [], 0, [], None
        def add(line):
            lines.append('    '*indent+line)
        if ': base(_buf)' in match[0]:
            base = re.search(r'public (?:abstract )?class '+re.escape(name)+r'\s*:\s*(\w+)', source)
            if base:
                add('inherited = decode('+repr('__base__'+base[1])+', _buf)')
        def statement(value):
            if re.fullmatch(r'\w+\.TrimExcess\(\)', value):
                return 'pass'
            if re.fullmatch(r'_buf\.Read\w+\(\)', value):
                # Some constructors consume obsolete fields without storing them.
                return self.expression(value)
            if value.startswith('throw '):
                return 'raise ValueError("Original parser rejected the payload")'
            declaration = re.fullmatch(r'[\w.<> ,]+\s+(\w+)', value)
            if declaration:
                return declaration[1] + ' = None'
            m = re.fullmatch(r'([\w]+)\.Add\((.*)\)', value)
            if m:
                return f'add({m[1]}, {self.expression(m[2])})'
            if value.endswith('++') or value.endswith('--'):
                return value[:-2] + (' += 1' if value.endswith('++') else ' -= 1')
            if '=' in value:
                left, right = value.split('=', 1)
                left = left.strip().split()[-1]
                return left + ' = ' + self.expression(right)
            raise ValueError('Unsupported constructor statement: '+value)
        for token in tokens:
            if token.startswith('foreach ') and name.startswith('Tb'):
                # The generated trailing lookup-index construction performs
                # no reads. DataList already contains the complete table.
                break
            if token == '{':
                stack.append(pending)
                pending = None
                indent += 1
            elif token == '}':
                loop_end = stack.pop()
                if loop_end:
                    add(statement(loop_end))
                indent -= 1
            elif token.startswith('if '):
                add('if '+self.expression(token[token.index('(')+1:-1])+':')
            elif token == 'else':
                add('else:')
            elif token.startswith('for '):
                initial, condition, final = token[token.index('(')+1:-1].split(';')
                add(statement(initial.strip()))
                add('while '+self.expression(condition)+':')
                pending = final.strip()
            else:
                add(statement(token))
        code = '\n'.join(lines)
        self.cache[key] = (params, compile(code, str(path), 'exec'))
        return self.cache[key]

    @staticmethod
    def add(collection, *values):
        if isinstance(collection, dict):
            key, value = values
            if key in collection:
                raise ValueError('Duplicate original dictionary key')
            collection[key] = value
        else:
            collection.append(values[0])

    def decode(self, name, reader, *args):
        base_constructor = name.startswith('__base__')
        name = name.removeprefix('__base__')
        if name == 'ConditionFactory':
            name = 'ICondition'
        source = self.source(name)
        if not base_constructor:
            # Luban emits a single concrete subtype as an if rather than a
            # switch. It still carries the same leading discriminator.
            match=re.search(r'if \(_buf\.ReadInt\(\) == (-?\d+)\)\s*\{\s*return new (\w+)\(_buf\);',source)
            if match:
                ident=reader.integer()
                if ident!=int(match[1]):raise ValueError('Unexpected subtype for '+name+': '+str(ident))
                target=match[2]
                return Record(self.decode(target,reader),_type=target)
        if not base_constructor and 'return _buf.ReadInt() switch' in source:
            ident = reader.integer()
            cases = dict((int(i), n) for i,n in re.findall(r'(-?\d+) => (?:\([\w.]+\))?new (\w+)\(_buf\)', source))
            target = cases.get(ident) or self.metadata.ids[ident]['name']
            return Record(self.decode(target, reader), _type=target)
        parser = self.compile(name, 1+len(args))
        if parser is None:
            if name in ['ConditionFactory', 'ICondition']:
                ident = reader.integer()
                if ident == 0:
                    return None
                t = self.metadata.ids[ident]
                # Native Config.dll conditions inherit a description key.
                result = Record(_type=t['name'], mDescKey=reader.string())
                result.update(self.metadata.read_bean(reader, t))
                return result
            if name in self.metadata.common:
                return Record(self.metadata.read_bean(reader, name))
            raise ValueError('Constructor unavailable: '+name)
        params, code = parser
        namespace = dict(zip(params, [reader, *args]))
        namespace.update(decode=self.decode, add=self.add, ValueError=ValueError,
                         bean=lambda **fields: Record(fields),
                         nonempty=lambda value: value if value else None,
                         utc_time=lambda seconds: datetime.fromtimestamp(seconds, timezone.utc).isoformat().replace('+00:00', 'Z'))
        exec(code, {'__builtins__': {}}, namespace)
        result = Record(namespace.get('inherited', {}))
        result.update({k: v for k, v in namespace.items() if k not in params and k not in ['decode', 'add', 'ValueError', 'bean', 'nonempty', 'utc_time']
                       and (k[0].isupper() or k.startswith('_data'))})
        return result

    def table(self, data, table_type):
        reader = Reader(data)
        result = self.decode(table_type, reader)
        if reader.pos != len(data):
            raise ValueError(f'{table_type}: consumed {reader.pos} of {len(data)} bytes')
        return result.get('_dataList', result.get('_data', result))


class Reader(LubanReader):
    def byte(self):
        return self.take(1)[0]

    def boolean(self):
        return self.scalar('bool')

    def float32(self):
        return self.scalar('float')

    def float64(self):
        return self.scalar('double')

    def long(self):
        n = self.take(1)[0]
        if n < 128:
            return n
        for boundary, mask, length in [(192,63,1),(224,31,2),(240,15,3),(248,7,4),(252,3,5),(254,1,6)]:
            if n < boundary:
                return ((n & mask) << (8*length)) | int.from_bytes(self.take(length), 'big')
        if n == 254:
            return int.from_bytes(self.take(7), 'big')
        value = int.from_bytes(self.take(8), 'big')
        return value if value < 2**63 else value-2**64


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT / '.tools/source-packages',
                        help='Directory containing official-code and official-data')
    args = parser.parse_args()
    SOURCE = args.source_root / 'official-code/common-source/Common'
    constructors = Constructors(args.source_root / 'official-code')
    tables_source = (SOURCE/'Tables.cs').read_text(encoding='utf-8-sig')
    mapping = {m[2]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', tables_source)}
    root = args.source_root / 'official-data'
    output, failures = {}, {}
    for name, table in mapping.items():
        path = root / (name+'.g.bytes')
        if not path.exists():
            continue
        try:
            output[name] = constructors.table(path.read_bytes(), table)
        except Exception as exc:
            failures[name] = str(exc)
    (root/'tables.json').write_text(json.dumps(output, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    (root/'table-failures.json').write_text(json.dumps(failures, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Fully decoded tables:', len(output), 'unresolved:', len(failures))
    print('Unresolved:', {k:v for k,v in failures.items() if any(s in k for s in ['hang_up','furniture','gacha','skill','pet','treasure','item','home'])})
