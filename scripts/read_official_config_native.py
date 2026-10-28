"""Read pinned APK GConfig application methods, including shared generics.

Only GConfig method bodies may be selected. Other assemblies contribute method
identities for branch annotations, never implementation bodies.
"""
import argparse
import contextlib
import hashlib
import io
import json
import struct
from pathlib import Path

from read_official_native import NativeMethods, ROOT


def inspect(type_names, output):
    metadata = ROOT / '.tools/source-packages/current/871_156598/official-code/global-metadata.dat'
    image = ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin'
    native = NativeMethods(image, metadata)  # Verifies both complete input hashes.
    if not type_names or any(not name.startswith('GConfig.') for name in type_names):
        raise ValueError('Select explicit GConfig application type names')
    # Registration addresses belong to the hash-verified virtual image above.
    registration = 0x11b6a188
    generic_pointers = 0x118666c0
    generic_count = 0x60759
    values = struct.unpack_from('<16Q', native.image, registration)
    if values[10] != len(native.types) or values[12] != len(native.types):
        raise ValueError('Original metadata registration differs')
    pointers = struct.unpack_from('<' + 'Q' * generic_count, native.image, generic_pointers)
    types = {}
    selected = {}
    config_range = None
    for at in range(native.header[42], native.header[42] + native.header[43], 40):
        image_info = struct.unpack_from('<10I', native.metadata, at)
        if native.string(image_info[0]) == 'Config.dll':
            config_range = range(image_info[2], image_info[2] + image_info[3])
    if config_range is None:
        raise ValueError('Original Config.dll metadata image is absent')
    byval = {row[2]: i for i, row in enumerate(native.types)}
    def qualified_name(ti):
        row = native.types[ti]
        name = native.string(row[0]); namespace = native.string(row[1])
        if row[3] >= 0:
            return qualified_name(byval[row[3]]) + '+' + name
        return namespace + '.' + name if namespace else name
    def pointer_type_name(pointer):
        data, bits = struct.unpack_from('<QI', native.image, pointer)
        kind = (bits >> 16) & 255
        if kind in (0x11, 0x12):
            return qualified_name(data)
        if kind in (0x13, 0x1e):
            return ('!' if kind == 0x13 else '!!') + str(data)
        if kind == 0x15:
            definition, instance, method_instance = struct.unpack_from('<QQQ', native.image, data)
            count, arguments = struct.unpack_from('<QQ', native.image, instance)
            return pointer_type_name(definition) + '<' + ','.join(pointer_type_name(struct.unpack_from('<Q', native.image, arguments + j * 8)[0]) for j in range(count)) + '>'
        if kind == 0x1d:
            return pointer_type_name(data) + '[]'
        return {1:'void', 2:'bool', 3:'char', 4:'sbyte', 5:'byte', 6:'short', 7:'ushort', 8:'int', 9:'uint', 10:'long', 11:'ulong', 12:'float', 13:'double', 14:'string', 28:'object', 24:'IntPtr'}.get(kind, 'Il2CppType:' + hex(kind) + ':' + hex(data))
    def signature_name(signature):
        pointer = struct.unpack_from('<Q', native.image, values[7] + signature * 8)[0]
        return pointer_type_name(pointer)
    for ti, row in enumerate(native.types):
        qualified = qualified_name(ti)
        if qualified not in type_names:
            continue
        if ti not in config_range:
            raise ValueError('Selected type is not in the original AOT Config.dll: ' + qualified)
        fields = []
        for fi in range(row[8], row[8] + row[18]):
            field_name, signature, token = struct.unpack_from('<IiI', native.metadata, native.header[24] + fi * 12)
            field_type_pointer = struct.unpack_from('<Q', native.image, values[7] + signature * 8)[0]
            field_flags = struct.unpack_from('<I', native.image, field_type_pointer + 8)[0] & 0xffff
            fields.append(dict(name=native.string(field_name), signature=signature, type=signature_name(signature), flags=field_flags, token=token))
        generic_parameters = []
        if row[6] >= 0:
            container = struct.unpack_from('<4i', native.metadata, native.header[30] + row[6] * 16)
            for gi in range(container[3], container[3] + container[1]):
                owner, name, start, count, number, flags = struct.unpack_from('<iiHHHH', native.metadata, native.header[26] + gi * 16)
                constraints = [signature_name(struct.unpack_from('<i', native.metadata, native.header[28] + ci * 4)[0]) for ci in range(start, start + count)]
                generic_parameters.append(dict(name=native.string(name), number=number, flags=flags, constraints=constraints))
        types[qualified] = dict(index=ti, flags=row[7], parent_signature=row[4], parent_type=signature_name(row[4]) if row[4] >= 0 else None,
                                generic_parameters=generic_parameters, fields=fields, methods=[])
        for mi in range(row[9], row[9] + row[16]):
            method = native.methods[mi]
            selected[mi] = (qualified, native.string(method[0]))
    if set(types) != set(type_names):
        raise ValueError('Unknown original GConfig type: ' + str(set(type_names) - set(types)))
    generic_specs = {}
    for si in range(values[8]):
        definition, class_inst, method_inst = struct.unpack_from('<iii', native.image, values[9] + si * 12)
        if definition in selected:
            generic_specs[si] = (definition, class_inst, method_inst)
    implementations = {}
    for i in range(values[4]):
        spec, method_pointer, invoker, adjustor = struct.unpack_from('<iiii', native.image, values[5] + i * 16)
        if spec in generic_specs:
            mi, class_inst, method_inst = generic_specs[spec]
            address = pointers[method_pointer]
            implementations.setdefault(mi, []).append(dict(address=address, spec=spec, class_inst=class_inst, method_inst=method_inst))
            native.by_address.setdefault(address, []).append('.'.join(selected[mi]) + '[shared]')
    boundaries = sorted(set(native.by_address) | {p for p in pointers if p})
    import bisect
    for mi, (qualified, name) in selected.items():
        method = native.methods[mi]
        # Use the token's module pointer, rather than the name lookup, so
        # overloaded non-generic methods retain their individual bodies.
        module = native.modules['Config.dll']
        address = module[2][(method[6] & 0xffffff) - 1]
        bodies = implementations.get(mi, [])
        if address:
            bodies = [dict(address=address)] + bodies
        entry = dict(index=mi, name=name, flags=method[7], return_signature=method[2], return_type=signature_name(method[2]), parameters=[], bodies=[])
        for pi in range(method[4], method[4] + method[-1]):
            pn, token, signature = struct.unpack_from('<IIi', native.metadata, native.header[22] + pi * 12)
            entry['parameters'].append(dict(name=native.string(pn), signature=signature, type=signature_name(signature), token=token))
        for body in bodies:
            address = body['address']
            if not address:
                continue
            stop = boundaries[bisect.bisect_right(boundaries, address)]
            length = stop - address
            if length <= 0 or length > 100000:
                raise ValueError('Unbounded original GConfig method body')
            stream = io.StringIO()
            with contextlib.redirect_stdout(stream):
                native.disassemble(address, length)
            entry['bodies'].append(dict(**body, length=length, sha256=hashlib.sha256(native.image[address:stop]).hexdigest(), instructions=stream.getvalue().splitlines()))
        types[qualified]['methods'].append(entry)
    result = dict(metadata_sha256=hashlib.sha256(native.metadata).hexdigest(), image_sha256=hashlib.sha256(native.image).hexdigest(), types=types)
    output = output.resolve()
    validation = (ROOT / 'output/validation').resolve()
    if output.parent == validation or not output.is_relative_to(validation) or not output.parent.is_dir():
        raise ValueError('Output must be inside an existing owned validation session')
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Read', len(types), 'GConfig types,', len(selected), 'method definitions')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--type', action='append', required=True, dest='types')
    parser.add_argument('--output', type=Path, required=True)
    arguments = parser.parse_args()
    inspect(arguments.types, arguments.output)
