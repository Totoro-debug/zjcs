"""Read every referenced GLES executable, including original GLES 3.1 cases."""
from UnityPy.helpers import CompressionHelper
from UnityPy.streams import EndianBinaryReader
from UnityPy.export.ShaderConverter import ShaderSubProgram


def programs(shader):
    indexes = set()
    for sub in shader.m_ParsedForm.m_SubShaders:
        for pass_ in sub.m_Passes:
            for name in ['progVertex', 'progFragment', 'progGeometry', 'progHull', 'progDomain', 'progRayTracing']:
                stage = getattr(pass_, name, None)
                if stage:
                    indexes.update(p.m_BlobIndex for p in stage.m_SubPrograms)
                    indexes.update(p.m_BlobIndex for group in (stage.m_PlayerSubPrograms or []) for p in group)
    if not indexes:
        return []
    if shader.platforms != [9]:
        raise ValueError('Select the original GLES table before decoding programs')
    chunks = [CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[offset:offset + length], size)
              for offset, length, size in zip(shader.offsets[0], shader.compressedLengths[0], shader.decompressedLengths[0])]
    table = EndianBinaryReader(chunks[0], endian='<')
    count = table.read_int()
    result = []
    for index in sorted(indexes):
        if index >= count:
            raise ValueError('Original executable index exceeds table')
        table.Position = 4 + index * 12
        offset, length, segment = table.read_int(), table.read_int(), table.read_int()
        if offset < 0 or length < 8 or offset + length > len(chunks[segment]):
            raise ValueError('Original executable segment is invalid')
        raw = chunks[segment][offset:offset + length]
        program = ShaderSubProgram(EndianBinaryReader(raw, endian='<'))
        if program.m_ProgramType.value not in (2, 3, 4):
            raise ValueError('Referenced GLES entry has an unexpected GPU type')
        result.append(dict(index=index, gpu_type=program.m_ProgramType.name, source=program.m_ProgramCode.decode('utf-8'),
                           keywords=sorted(set(program.m_Keywords + (program.m_LocalKeywords or [])))))
    keys = [(tuple(p['keywords']), p['source']) for p in result]
    if len(keys) != len(set(keys)):
        raise ValueError('Original GLES program identities need explicit variant selection')
    return result
