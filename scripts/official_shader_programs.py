"""Decode original Unity shader program segments without running Unity."""
import struct
from UnityPy.helpers import CompressionHelper
from UnityPy.streams import EndianBinaryReader
from UnityPy.export.ShaderConverter import ShaderSubProgram


def gles_programs(shader, with_keywords=False):
    programs=[]
    # Newer Unity tables also contain parameter blobs with identical GPU headers.
    # The serialized pass references identify executable entries explicitly.
    indexes=set()
    for subshader in shader.m_ParsedForm.m_SubShaders:
        for pass_ in subshader.m_Passes:
            for name in ['progVertex','progFragment','progGeometry','progHull','progDomain','progRayTracing']:
                stage=getattr(pass_,name,None)
                if stage is None:continue
                for program in stage.m_SubPrograms:indexes.add(program.m_BlobIndex)
                for platform in getattr(stage,'m_PlayerSubPrograms',[]) or []:
                    for program in platform:indexes.add(program.m_BlobIndex)
    for offsets,lengths,sizes in zip(shader.offsets,shader.compressedLengths,shader.decompressedLengths):
        chunks=[CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[offset:offset+length],size)
                for offset,length,size in zip(offsets,lengths,sizes)]
        table=EndianBinaryReader(chunks[0],endian='<')
        count=table.read_int()
        for index in range(count):
            if indexes and index not in indexes:continue
            table.Position=4+index*12
            offset,length,segment=table.read_int(),table.read_int(),table.read_int()
            if offset<0 or length<8 or offset+length>len(chunks[segment]):raise ValueError('Invalid original shader program segment')
            if struct.unpack_from('<i',chunks[segment],offset+4)[0] in (2,3):continue
            reader=EndianBinaryReader(chunks[segment][offset:offset+length],endian='<')
            program=ShaderSubProgram(reader)
            if 'GLES' in program.m_ProgramType.name:
                result={'type':program.m_ProgramType.name,'source':program.m_ProgramCode.decode('utf8')}
                if with_keywords:
                    result.update(index=index, keywords=sorted(set(program.m_Keywords + (program.m_LocalKeywords or []))))
                programs.append(result)
    if not programs:raise ValueError('Original shader has no GLES program')
    return programs
