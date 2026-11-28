"""Shared helpers for existing asset tools; no conversion command entry point."""

from __future__ import annotations

import re

import struct

GUID_RE = re.compile(r"^[0-9a-fA-F]{32}$")

def read_aligned_string(data: bytes, offset: int) -> tuple[str, int]:
    if offset + 4 > len(data):
        raise ValueError("Split animation object ended inside a string length")
    length = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if length < 0 or offset + length > len(data):
        raise ValueError("Split animation object contains an invalid string length")
    value = data[offset:offset + length].decode("utf-8")
    offset += length
    offset = (offset + 3) & ~3
    return value, offset

def parse_split_binary_object(data: bytes) -> dict:
    offset = 28
    object_name, offset = read_aligned_string(data, offset)
    if offset + 4 > len(data):
        raise ValueError("Split animation object has no shared string count")
    string_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if string_count < 0 or string_count > 100000:
        raise ValueError("Split animation shared string count is invalid")
    strings = []
    for _ in range(string_count):
        value, offset = read_aligned_string(data, offset)
        strings.append(value)
    if offset + 4 > len(data):
        raise ValueError("Split animation object has no animation count")
    animation_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if animation_count < 0 or animation_count > 10000:
        raise ValueError("Split animation count is invalid")
    animations = []
    names = set()
    for _ in range(animation_count):
        content_name, offset = read_aligned_string(data, offset)
        asset_guid, offset = read_aligned_string(data, offset)
        if not content_name or not GUID_RE.fullmatch(asset_guid) or content_name in names:
            raise ValueError("Split animation record has an invalid name or asset GUID")
        names.add(content_name)
        animations.append({"name": content_name, "asset_guid": asset_guid.lower()})
    if offset + 4 > len(data):
        raise ValueError("Split animation object has no no-split count")
    no_split_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if no_split_count < 0 or no_split_count > 10000:
        raise ValueError("Split animation no-split count is invalid")
    no_split = []
    for _ in range(no_split_count):
        value, offset = read_aligned_string(data, offset)
        no_split.append(value)
    if offset != len(data):
        raise ValueError("Split animation object has unparsed trailing bytes")
    return {"name": object_name, "strings": strings, "animations": animations,
            "no_split_animations": no_split}
