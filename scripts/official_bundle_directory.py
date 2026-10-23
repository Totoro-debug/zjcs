"""Read UnityFS CAB names without decompressing or loading its asset payload."""
def cab_names(stream):
    from UnityPy.helpers import CompressionHelper
    from UnityPy.streams import EndianBinaryReader

    header = stream.read(512)
    encrypted = bytes(c ^ 0x40 for c in header[:7]) == b'UnityFS'

    def decode(data):
        return bytes(c ^ 0x40 for c in data) if encrypted else data

    reader = EndianBinaryReader(decode(header))
    if reader.read_string_to_null() != 'UnityFS':
        raise ValueError('Expected UnityFS bundle')
    version = reader.read_u_int()
    reader.read_string_to_null()
    reader.read_string_to_null()
    size = reader.read_long()
    compressed, uncompressed, flags = reader.read_u_int(), reader.read_u_int(), reader.read_u_int()
    if version < 7 or compressed > size or uncompressed > 16 * 1024 * 1024:
        raise ValueError('Unsupported UnityFS directory layout')
    start = (reader.Position + 15) & ~15
    stream.seek(size - compressed if flags & 0x80 else start)
    data = decode(stream.read(compressed))
    compression = flags & 0x3f
    if compression in (2, 3):
        data = CompressionHelper.decompress_lz4(data, uncompressed)
    elif compression == 1:
        data = CompressionHelper.decompress_lzma(data)
    elif compression != 0:
        raise ValueError('Unsupported UnityFS directory compression')
    if len(data) != uncompressed:
        raise ValueError('Truncated UnityFS directory')
    reader = EndianBinaryReader(data)
    reader.Position = 16
    blocks = reader.read_int()
    if blocks < 0 or blocks * 10 > len(data):
        raise ValueError('Invalid UnityFS block count')
    reader.Position += blocks * 10
    nodes = reader.read_int()
    if nodes < 0 or nodes * 21 > len(data):
        raise ValueError('Invalid UnityFS node count')
    names = []
    for _ in range(nodes):
        reader.read_long()
        reader.read_long()
        reader.read_u_int()
        name = reader.read_string_to_null()
        if name.lower().startswith('cab-'):
            names.append(name)
    if reader.Position != len(data):
        raise ValueError('Unexpected trailing UnityFS directory data')
    return names
