"""Describe the original TMP input-field addition from pinned native metadata."""
import struct
from official_apk import ROOT
from official_metadata import Metadata
from read_official_native import NativeMethods
from prepare_unity_map13_sources import sha, source_path


def install_input_field(schemas):
    image = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    native = NativeMethods(image, metadata)
    model = Metadata(native.metadata)
    registration = struct.unpack_from("<16Q", native.image, 0x11B6A188)
    if registration[10] != len(model.types) or registration[12] != len(model.types):
        raise ValueError("Original native type registration differs")
    typ, = [t for t in model.types if t["namespace"] == "TMPro" and t["name"] == "TMP_InputField"]
    images = [struct.unpack_from("<10I", native.metadata, at)
        for at in range(native.header[42], native.header[42] + native.header[43], 40)]
    img, = [i for i in images if i[2] <= typ["index"] < i[2] + i[3]]
    attributes = dict(struct.unpack_from("<2I", native.metadata, native.header[52] + i * 8)
        for i in range(img[8], img[8] + img[9]))
    serialized, proof = [], []
    for field in typ["fields"]:
        pointer = struct.unpack_from("<Q", native.image, registration[7] + field["type_index"] * 8)[0]
        data, bits = struct.unpack_from("<QI", native.image, pointer)
        flags = bits & 0xffff
        constructors = []
        if field["token"] in attributes:
            at = native.header[50] + attributes[field["token"]]
            count = native.metadata[at]
            if count >= 0x80:
                raise ValueError("Inspect extended original field attributes")
            for index in range(count):
                method = native.methods[struct.unpack_from("<I", native.metadata, at + 1 + index * 4)[0]]
                owner = native.types[method[1]]
                constructors.append(native.string(owner[1]) + "." + native.string(owner[0]) + "." + native.string(method[0]))
        selected = not flags & (0x10 | 0x20 | 0x40 | 0x80) and ((flags & 7) == 6 or "UnityEngine.SerializeField..ctor" in constructors)
        # The official OnValidateInput is a delegate; Unity does not serialize it.
        if field["name"] == "m_OnValidateInput":
            delegate = model.types[data]
            if delegate["name"] != "OnValidateInput":
                raise ValueError("Original input validation delegate type differs")
            selected = False
        if selected:
            serialized.append(field["name"])
        proof.append(dict(field, native_type_data=data, native_type_bits=bits,
            attribute_constructors=constructors, serialized=bool(selected)))
    key = "Unity.TextMeshPro:TMPro.TMP_InputField"
    schemas.tree("Unity.TextMeshPro", "TMPro.TMP_InputField")
    fields = schemas.cache[key]
    original = []
    for field in fields:
        if field["level"] == 1 and field["name"] == "m_InputType":
            original.append(dict(level=1, type="int", name="m_EncodingType", flags=0))
        original.append(field)
    names = [f["name"] for f in original if f["level"] == 1]
    if names[names.index("m_TextViewport"):] != serialized:
        raise ValueError("Original TMP input-field serialization differs: " + str(serialized))
    encoding = next(f for f in proof if f["name"] == "m_EncodingType")
    enum = model.types[encoding["native_type_data"]]
    if not enum["enum"] or enum["name"] != "EncodingType":
        raise ValueError("Original TMP encoding enum differs")
    schemas.cache[key] = original
    return dict(type=typ["name"], namespace=typ["namespace"], type_index=typ["index"], fields=proof,
        serialized=serialized, operation="Insert the original EncodingType Int32 before InputType; exclude the original nonserializable validation delegate; verify every derived serialized field name and order against original metadata; require full-byte consumption.",
        inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in (image, metadata)])
