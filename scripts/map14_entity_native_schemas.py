"""Recover the original stripped CinemachineComposer serialization description.

No camera behavior is implemented here. Public/private flags, attributes, field
order, field types and inheritance are verified against the pinned APK metadata.
"""
import struct

from official_apk import ROOT
from official_metadata import Metadata
from read_official_native import NativeMethods
from map13_entity_native_schemas import install as install_shared
from map13_native_schemas import fields_tree


def install(schemas, code):
    proof = install_shared(schemas, code)
    native = NativeMethods(ROOT / proof["image"]["path"], ROOT / proof["metadata"]["path"])
    model = Metadata(native.metadata)
    values = struct.unpack_from("<16Q", native.image, proof["metadata_registration"])
    if values[7] != proof["type_table"] or values[10] != len(model.types):
        raise ValueError("Original camera metadata registration differs")
    images = [struct.unpack_from("<10I", native.metadata, at)
              for at in range(native.header[42], native.header[42] + native.header[43], 40)]
    fields = [("m_TrackedObjectOffset", "Vector3f")]
    fields += [(name, "bool" if name in ("m_LookaheadIgnoreY", "m_CenterOnActivate") else "float") for name in (
        "m_LookaheadTime", "m_LookaheadSmoothing", "m_LookaheadIgnoreY", "m_HorizontalDamping", "m_VerticalDamping",
        "m_ScreenX", "m_ScreenY", "m_DeadZoneWidth", "m_DeadZoneHeight", "m_SoftZoneWidth", "m_SoftZoneHeight",
        "m_BiasX", "m_BiasY", "m_CenterOnActivate")]
    composer, = [t for t in model.types if t["namespace"] == "Cinemachine" and t["name"] == "CinemachineComposer"]
    base, = [t for t in model.types if t["namespace"] == "Cinemachine" and t["name"] == "CinemachineComponentBase"]
    if composer["parent"] != base["byval"]:
        raise ValueError("Original camera base type differs")
    for typ, expected in [(base, []), (composer, [label for label, _ in fields])]:
        image, = [i for i in images if i[2] <= typ["index"] < i[2] + i[3]]
        attributes = dict(struct.unpack_from("<2I", native.metadata, native.header[52] + i * 8)
                          for i in range(image[8], image[8] + image[9]))
        records, serialized = [], []
        for field in typ["fields"]:
            pointer = struct.unpack_from("<Q", native.image, values[7] + field["type_index"] * 8)[0]
            data, bits = struct.unpack_from("<QI", native.image, pointer)
            record = dict(field, type_pointer=pointer, type_data=data, type_bits=bits)
            constructors = []
            if field["token"] in attributes:
                at = native.header[50] + attributes[field["token"]]
                count = native.metadata[at]
                if count >= 128: raise ValueError("Inspect extended original camera attribute count")
                for i in range(count):
                    method_index = struct.unpack_from("<I", native.metadata, at + 1 + i * 4)[0]
                    method = native.methods[method_index]; owner = native.types[method[1]]
                    constructors.append(dict(method_index=method_index,
                        name=native.string(owner[1]) + "." + native.string(owner[0]) + "." + native.string(method[0])))
            record["attribute_constructors"] = constructors
            flags = bits & 0xffff
            if not flags & (0x10 | 0x20 | 0x40 | 0x80) and ((flags & 7) == 6
                or any(c["name"] == "UnityEngine.SerializeField..ctor" for c in constructors)):
                serialized.append(field["name"])
                expected_kind = dict(fields)[field["name"]]
                if (bits >> 16) & 255 != {"Vector3f": 17, "float": 12, "bool": 2}[expected_kind]:
                    raise ValueError("Original camera field type differs: " + field["name"])
                if expected_kind == "Vector3f" and (model.types[data]["namespace"], model.types[data]["name"]) != ("UnityEngine", "Vector3"):
                    raise ValueError("Original camera offset is not Vector3")
            records.append(record)
        if serialized != expected: raise ValueError("Original camera serialized field order differs: " + str(serialized))
        proof["types"].append(dict(type=typ["name"], namespace=typ["namespace"], type_index=typ["index"], fields=records, serialized=serialized))
    previous = schemas.default.tree

    def tree(assembly, name):
        if name != "Cinemachine.CinemachineComposer": return previous(assembly, name)
        result = fields_tree(schemas.default, fields)
        described = [dict(level=child.m_Level, type=child.m_Type, name=child.m_Name, flags=child.m_MetaFlag or 0)
                     for child in result.traverse()]
        schemas.default.cache[assembly + ":" + name] = described
        schemas.default.records[assembly + ":" + name] = described
        return result

    schemas.default.tree = tree
    return proof
