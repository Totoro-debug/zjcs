"""Inspect pinned native MessagePack ABI changes used by shared map data.

Only the explicit serializer types below may contribute method bodies. No
animation SDK implementation is selected or consumed.
"""
import argparse
import bisect
import contextlib
import hashlib
import io
import json
from pathlib import Path
from read_official_native import NativeMethods, ROOT

TYPES = {('MessagePack.Unity', name) for name in (
    'Vector2Formatter', 'Vector3Formatter', 'Vector4Formatter',
    'QuaternionFormatter', 'ColorFormatter', 'BoundsFormatter', 'RectFormatter')}
TYPES.update({('MessagePack.Decoders', 'Float32Single'),
              ('MessagePack.Decoders', 'Float64Double'),
              ('MessagePack', 'MessagePackBinary')})


def inspect(output):
    metadata = ROOT / '.tools/source-packages/current/871_156598/official-code/global-metadata.dat'
    image = ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin'
    native = NativeMethods(image, metadata)
    boundaries = sorted(native.by_address)
    records = {}
    for row in native.types:
        namespace, name = native.string(row[1]), native.string(row[0])
        if (namespace, name) not in TYPES:
            continue
        methods = []
        for index in range(row[9], row[9] + row[16]):
            method = native.methods[index]
            method_name = native.string(method[0])
            if name == 'MessagePackBinary' and method_name not in ('WriteSingle', 'ReadSingle'):
                continue
            address = native.modules['GFrameCore.dll'][2][(method[6] & 0xffffff) - 1]
            if not address:
                continue
            stop = boundaries[bisect.bisect_right(boundaries, address)]
            if stop - address > 100000:
                raise ValueError('Unbounded selected serializer method')
            stream = io.StringIO()
            with contextlib.redirect_stdout(stream):
                native.disassemble(address, stop - address)
            methods.append(dict(index=index, name=method_name, address=address,
                                length=stop-address,
                                sha256=hashlib.sha256(native.image[address:stop]).hexdigest(),
                                instructions=stream.getvalue().splitlines()))
        records[namespace + '.' + name] = methods
    if set(records) != {namespace + '.' + name for namespace, name in TYPES}:
        raise ValueError('The original serializer type set differs')
    output = output.resolve()
    validation = (ROOT / 'output/validation').resolve()
    if output.parent == validation or not output.is_relative_to(validation) or not output.parent.is_dir():
        raise ValueError('Output must be inside an existing owned validation session')
    result = dict(metadata_sha256=hashlib.sha256(native.metadata).hexdigest(),
                  image_sha256=hashlib.sha256(native.image).hexdigest(), types=records)
    output.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print('Read', len(records), 'explicit native serializer types')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    inspect(parser.parse_args().output)
