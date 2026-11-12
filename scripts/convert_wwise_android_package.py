"""Convert Wwise AKPK v1 language strings from Android to Windows.

The matching 2022.1.18 SDK samples/SoundEngine/Common/AkFilePackageLUT.{h,cpp}
map language strings as native AkOSChar. Android stores UTF-8; Windows reads
UTF-16LE. Bank/media/external payload bytes and language IDs remain unchanged.
Only the language map, header lengths and absolute start-block indexes change.
"""
from __future__ import annotations

import math
import struct


def convert_android_package_to_windows(raw: bytes) -> bytes:
    """Return a Windows AKPK package, rejecting unsupported or malformed input."""
    if len(raw) < 28 or raw[:4] != b'AKPK':
        raise ValueError('Expected an original Wwise AKPK package')
    chunk_size, version, language_size, bank_size, media_size, external_size = struct.unpack_from('<6I', raw, 4)
    header_end = chunk_size + 8
    tables_end = 28 + language_size + bank_size + media_size + external_size
    if version != 1 or not tables_end <= header_end <= len(raw) or language_size < 4:
        raise ValueError('Unsupported or truncated original AKPK header')

    languages = raw[28:28 + language_size]
    count, = struct.unpack_from('<I', languages)
    strings_start = 4 + count * 8
    if strings_start > len(languages):
        raise ValueError('Truncated original language index')
    names, identifiers = [], []
    for index in range(count):
        offset, identifier = struct.unpack_from('<2I', languages, 4 + index * 8)
        if not strings_start <= offset < len(languages):
            raise ValueError('Original language string is outside its map')
        end = languages.find(b'\0', offset)
        if end < 0:
            raise ValueError('Unterminated original language string')
        # The selected source language enum uses ASCII identifiers. Other names
        # need explicit Windows ordering verification before extending this tool.
        name = languages[offset:end].decode('ascii')
        if not name or name in names or identifier in identifiers:
            raise ValueError('Empty or duplicate original language identity')
        names.append(name)
        identifiers.append(identifier)
    if names != sorted(names):
        raise ValueError('Original language map is not sorted for binary lookup')

    windows_map = bytearray(struct.pack('<I', count) + bytes(count * 8))
    for index, (name, identifier) in enumerate(zip(names, identifiers)):
        struct.pack_into('<2I', windows_map, 4 + index * 8, len(windows_map), identifier)
        windows_map.extend(name.encode('utf-16-le') + b'\0\0')
    windows_map.extend(bytes(-len(windows_map) % 4))

    cursor, tables, records, alignment = 28 + language_size, [], [], 1
    for table_size, id_width in ((bank_size, 4), (media_size, 4), (external_size, 8)):
        if table_size < 4 or cursor + table_size > header_end:
            raise ValueError('Truncated original file lookup table')
        table = bytearray(raw[cursor:cursor + table_size])
        entries, = struct.unpack_from('<I', table)
        entry_size = id_width + 16
        if table_size != 4 + entries * entry_size:
            raise ValueError('Original file lookup table length mismatch')
        for index in range(entries):
            entry_offset = 4 + index * entry_size
            block, size, start, language = struct.unpack_from('<4I', table, entry_offset + id_width)
            offset = block * start
            if block == 0 or offset < header_end or offset + size > len(raw):
                raise ValueError('Original file payload is outside its package')
            if language and language not in identifiers:
                raise ValueError('Original file references an unknown language')
            alignment = math.lcm(alignment, block)
            if alignment > 65536:
                raise ValueError('Inspect unsupported original package alignment')
            records.append((table, entry_offset + id_width + 8, start, block))
        tables.append(table)
        cursor += table_size

    minimum_growth = max(0, len(windows_map) - language_size)
    growth = (minimum_growth + alignment - 1) // alignment * alignment
    windows_map.extend(bytes(language_size + growth - len(windows_map)))
    for table, start_offset, start, block in records:
        struct.pack_into('<I', table, start_offset, start + growth // block)
    header = bytearray(raw[:28])
    struct.pack_into('<I', header, 4, chunk_size + growth)
    struct.pack_into('<I', header, 12, len(windows_map))
    converted = bytes(header + windows_map + b''.join(tables)) + raw[tables_end:]
    if len(converted) != len(raw) + growth or converted[header_end + growth:] != raw[header_end:]:
        raise ValueError('Windows conversion changed original package payload bytes')
    return converted
