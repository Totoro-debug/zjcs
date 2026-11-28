"""Shared helpers for existing asset tools; no conversion command entry point."""

from __future__ import annotations

import hashlib

import struct

from prepare_character_creation_opening_audio import parse_pck

def sha(raw):
    return hashlib.sha256(raw).hexdigest()

def inspect(raw):
    banks, media = parse_pck(raw)
    files = {str(identity): {'bytes': size, 'sha256': sha(raw[offset:offset+size])}
             for identity, _, size, offset, _ in media}
    objects = {}
    for bank_id, _, size, offset, _ in banks:
        bank = raw[offset:offset+size]
        cursor = 0
        while cursor < len(bank):
            tag = bank[cursor:cursor+4]
            size = struct.unpack_from('<I', bank, cursor+4)[0]
            chunk = bank[cursor+8:cursor+8+size]
            if len(chunk) != size:
                raise ValueError('Truncated official Wwise chunk')
            cursor += 8 + size
            if tag != b'HIRC':
                continue
            count = struct.unpack_from('<I', chunk)[0]
            position = 4
            for _ in range(count):
                kind = chunk[position]
                size = struct.unpack_from('<I', chunk, position+1)[0]
                payload = chunk[position+5:position+5+size]
                if len(payload) != size:
                    raise ValueError('Truncated official Wwise object')
                identity = struct.unpack_from('<I', payload)[0]
                objects[str(identity)] = {'bank_id': bank_id, 'type': kind, 'sha256': sha(payload)}
                position += 5 + size
            if position != len(chunk):
                raise ValueError('Unconsumed official Wwise object bytes')
    return {'media': files, 'objects': objects}

def mapping(raw):
    if raw[0] != 2:
        raise ValueError('Unsupported Wwise event mapping header')
    count = struct.unpack_from('<I', raw, 1)[0]
    position = 5
    banks = []
    for _ in range(count):
        if raw[position] != 2:
            raise ValueError('Unsupported Wwise bank definition')
        identity, size = struct.unpack_from('<iI', raw, position+1)
        name = raw[position+9:position+9+size].decode('utf8')
        flags = raw[position+9+size]
        position += 10 + size
        banks.append({'id': identity, 'name': name, 'flags': flags})
    count = struct.unpack_from('<I', raw, position)[0]
    position += 4
    events = {}
    for _ in range(count):
        identity, size = struct.unpack_from('<II', raw, position)
        position += 8
        indices = list(struct.unpack_from('<' + 'H'*size, raw, position))
        position += size*2
        if identity in events or any(index >= len(banks) for index in indices):
            raise ValueError('Invalid Wwise event mapping')
        events[identity] = indices
    if position != len(raw):
        raise ValueError('Unconsumed Wwise event mapping bytes')
    return {'banks': banks, 'events': events}
