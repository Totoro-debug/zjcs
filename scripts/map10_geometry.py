"""Decode the fixed-version Map10 terrain geometry export."""
from __future__ import annotations

import json
import struct


MAGIC = b"ZJMAP001"
VERSION = 5


class Map10GeometryError(ValueError):
    pass


def _metadata(source: str, data: bytes, offset: int):
    decoder = json.JSONDecoder()
    try:
        value, end = decoder.raw_decode(source, offset)
        encoded = source[offset:end].encode("latin1").decode("utf-8")
        value = json.loads(encoded)
    except (json.JSONDecodeError, UnicodeError) as error:
        raise Map10GeometryError(f"Invalid geometry metadata at byte {offset}") from error
    return value, end


def decode(data: bytes) -> list[dict]:
    if len(data) < 16 or data[:8] != MAGIC or data[8:9] != b"|":
        raise Map10GeometryError("Map10 geometry header is invalid")
    version = int.from_bytes(data[9:12], "little")
    if version != VERSION:
        raise Map10GeometryError(f"Unsupported Map10 geometry version: {version}")

    first_metadata_length = struct.unpack_from("<I", data, 12)[0]
    source = data.decode("latin1")
    offset = 16
    batches = []
    while offset < len(data):
        metadata, metadata_end = _metadata(source, data, offset)
        if not isinstance(metadata, dict):
            raise Map10GeometryError(f"Geometry metadata at byte {offset} is not an object")
        if not batches and metadata_end - offset != first_metadata_length:
            raise Map10GeometryError("Map10 geometry metadata length differs from its header")
        offset = metadata_end

        if offset + 4 > len(data):
            raise Map10GeometryError("Map10 geometry is missing a vertex count")
        vertex_count = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        if vertex_count <= 0:
            raise Map10GeometryError("Map10 geometry batch is empty")

        position_bytes = vertex_count * 2 * 4
        uv_bytes = vertex_count * 2 * 4
        color_bytes = vertex_count * 4 * 4
        if offset + position_bytes + uv_bytes + color_bytes + 4 > len(data):
            raise Map10GeometryError("Map10 geometry vertex payload is truncated")
        positions = struct.unpack_from(f"<{vertex_count * 2}f", data, offset)
        offset += position_bytes
        uvs = struct.unpack_from(f"<{vertex_count * 2}f", data, offset)
        offset += uv_bytes
        colors = struct.unpack_from(f"<{vertex_count * 4}f", data, offset)
        offset += color_bytes

        index_count = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        if index_count == 0 or index_count % 3:
            raise Map10GeometryError("Map10 geometry index count is not triangular")
        index_bytes = index_count * 4
        if offset + index_bytes > len(data):
            raise Map10GeometryError("Map10 geometry index payload is truncated")
        indices = struct.unpack_from(f"<{index_count}I", data, offset)
        offset += index_bytes
        if max(indices) >= vertex_count:
            raise Map10GeometryError("Map10 geometry index exceeds its vertex count")

        identifier = None
        if offset < len(data):
            if offset + 5 > len(data) or data[offset + 4:offset + 5] != b"{":
                raise Map10GeometryError("Map10 geometry batch separator is invalid")
            identifier = struct.unpack_from("<I", data, offset)[0]
            offset += 4

        texture = metadata.get("texture")
        bounds = metadata.get("bounds")
        if not isinstance(texture, str) or not texture.startswith("res://assets/official/sprites/"):
            raise Map10GeometryError("Map10 geometry batch has no official texture URI")
        if not isinstance(bounds, list) or len(bounds) != 4:
            raise Map10GeometryError("Map10 geometry batch bounds are invalid")
        batches.append({
            "metadata": metadata,
            "positions": positions,
            "uvs": uvs,
            "colors": colors,
            "indices": indices,
            "identifier": identifier,
        })

    return batches
