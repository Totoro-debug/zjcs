"""Read native YAML scalars and compare packed arrays using original type trees."""
import re
import struct

import yaml


class OriginalYamlInteger(int):
    def __new__(cls, value, scalar):
        result = super().__new__(cls, value)
        result.scalar = scalar
        return result

    def __str__(self):
        return self.scalar


class NativeLoader(getattr(yaml, "CSafeLoader", yaml.SafeLoader)):
    pass


class OriginalYamlFloat(float):
    def __new__(cls, value, scalar):
        result = super().__new__(cls, value)
        result.scalar = scalar
        return result

    def __str__(self):
        return self.scalar


def _integer(loader, node):
    return OriginalYamlInteger(yaml.SafeLoader.construct_yaml_int(loader, node), node.value)


def _float(loader, node):
    return OriginalYamlFloat(yaml.SafeLoader.construct_yaml_float(loader, node), node.value)


NativeLoader.add_constructor("tag:yaml.org,2002:int", _integer)
NativeLoader.add_constructor("tag:yaml.org,2002:float", _float)
HEADER = re.compile(r"(?m)^--- !u!\d+ &(-?\d+)\n")


def documents(text):
    parts = HEADER.split(text)
    return {int(parts[index]): next(iter(yaml.load(parts[index + 1], Loader=NativeLoader).items()))
            for index in range(1, len(parts), 2)}


def reference_shapes(unions):
    result = {}
    for union in unions:
        stack, root = [], None
        for field in union["schema"]:
            level = field["m_Level"]
            value = dict(type=field["m_Type"], name=field["m_Name"], children={})
            while len(stack) > level:
                stack.pop()
            if len(stack) != level:
                raise ValueError("Original native schema skips a level")
            if stack:
                stack[-1]["children"][value["name"]] = value
            else:
                root = value
            stack.append(value)
        registry = root["children"]["references"]
        records = {}
        for index, identity in enumerate(union["types"]):
            records[tuple(identity)] = element(registry["children"]["Map13Type" + str(index)])
        registry["children"] = dict(version=registry["children"]["version"],
                                   RefIds=dict(type="vector", name="RefIds", record_types=records))
        result[(union["cab"], union["script_type_index"])] = root
    return result


def element(shape, expected=None):
    if "record_types" in shape:
        typ = expected["type"]
        return shape["record_types"][(typ["asm"], typ["ns"], typ["class"])]
    return shape["children"]["Array"]["children"]["data"]


def unpack_array(value, shape, count):
    formats = {"SInt8": "b", "UInt8": "B", "char": "B", "SInt16": "h", "UInt16": "H",
               "int": "i", "SInt32": "i", "UInt32": "I", "SInt64": "q", "UInt64": "Q",
               "float": "f", "double": "d", "bool": "?"}
    kind = element(shape)["type"]
    if kind not in formats:
        raise ValueError("Native packed array element is not a primitive: " + kind)
    if value is None and count == 0:
        # AssetRipper writes zero packed bytes as an empty scalar after the key.
        return []
    scalar = value.scalar if isinstance(value, OriginalYamlInteger) else value
    if not isinstance(scalar, str) or not re.fullmatch(r"[0-9a-fA-F]*", scalar):
        raise ValueError("Native packed array is not a hexadecimal scalar")
    fmt = formats[kind]
    if len(scalar) != count * struct.calcsize("<" + fmt) * 2:
        raise ValueError("Native packed array byte width differs from the original schema")
    return list(struct.unpack("<" + str(count) + fmt, bytes.fromhex(scalar)))


def float_bits(value, shape):
    if shape["type"] not in ("float", "double"):
        raise ValueError("Floating-point value has a different original schema type")
    return struct.pack("<f" if shape["type"] == "float" else "<d", value)
