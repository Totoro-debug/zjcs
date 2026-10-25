"""Decode the client's MessagePack map records, including its raw Vector3 codec.

Field order is evidenced by Common/*Formatter.cs in the pinned Console.dll.
No map coordinates, walkability flags or entity placements are synthesized.
"""
import struct
from official_apk import ROOT
import msgpack


def unpack(data):
    return msgpack.unpackb(data, raw=False, strict_map_key=False)


def ground(data):
    if data[0] != 0x98:
        raise ValueError('MapRootEntityLiteSingleInfo must contain eight fields')
    reader = msgpack.Unpacker(raw=False, strict_map_key=False)
    reader.feed(data[1:])
    identity, character, member, size = [next(reader) for _ in range(4)]
    offset = 1 + reader.tell()
    # The client's Unity Vector3 formatter stores three untagged BE float32s.
    origin = list(struct.unpack_from('>3f', data, offset))
    tail = msgpack.Unpacker(raw=False, strict_map_key=False)
    tail.feed(data[offset + 12:])
    cells, areas, max_steps = [next(tail) for _ in range(3)]
    if offset + 12 + tail.tell() != len(data):
        raise ValueError('Unconsumed ground bytes')
    if len(cells) != size[1] or any(len(row) != size[0] for row in cells):
        raise ValueError('Ground dimensions differ from cell data')
    return dict(id=identity, character=character, member=member, size=size,
                origin=origin, cells=cells, areas=areas, max_steps=max_steps)


def entities(data):
    identity, regions, group, tag, progress = unpack(data)
    result = []
    for region, rows in regions.items():
        for row in rows:
            lo, refresh, public, private, position, spawn, stages = row
            result.append(dict(id=lo, region=region, refresh=refresh,
                public=public, private=private, grid=position[0], direction=position[1],
                unique=position[2], area=position[3], entity=spawn[0], type=spawn[1],
                stages=stages or {}))
    return dict(id=identity, group=group, tag=tag, progress=progress, entities=result)
