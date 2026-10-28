"""Read the pinned client's TMP_InputField fork without discarding trailing bytes."""
import hashlib
from functools import lru_cache

from UnityPy.helpers import TypeTreeHelper

from official_apk import ROOT
from official_metadata import Metadata


@lru_cache(maxsize=1)
def encoding_evidence():
    path = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    raw = path.read_bytes()
    metadata = Metadata(raw)
    owner = next(row for row in metadata.types
                 if row["namespace"] == "TMPro" and row["name"] == "TMP_InputField")
    field = next(row for row in owner["fields"] if row["name"] == "m_EncodingType")
    enum = metadata.type_for(field["type_index"])
    assert enum["enum"] and enum["name"] == "EncodingType", enum
    underlying = next(row for row in enum["fields"] if row["name"] == "value__")
    assert metadata.type_for(underlying["type_index"])["name"] == "Int32"
    return {"source": path.relative_to(ROOT).as_posix(), "sha256": hashlib.sha256(raw).hexdigest(),
            "class": "TMPro.TMP_InputField", "field": "m_EncodingType", "type": "EncodingType:Int32"}


def read_input_field(schemas, obj):
    """Use the full local TMP schema plus the verified extra serialized enum."""
    encoding_evidence()
    name = "TMPro.TMP_InputField"
    schemas.tree("Unity.TextMeshPro.dll", name)
    fields = schemas.cache["Unity.TextMeshPro.dll:" + name]
    if not any(row["level"] == 1 and row["name"] == "m_EncodingType" for row in fields):
        at = next(i for i, row in enumerate(fields)
                  if row["level"] == 1 and row["name"] == "m_ContentType")
        fields.insert(at + 1, {"level": 1, "type": "int", "name": "m_EncodingType", "flags": 0})
    # MonoScript assembly names in this bundle omit the .dll suffix.
    schemas.cache["Unity.TextMeshPro:" + name] = fields
    boost = TypeTreeHelper.read_typetree_boost
    try:
        # The accelerated reader misaligns TMP's UTF-16 asterisk character.
        # The regular reader checks that the complete original object is consumed.
        TypeTreeHelper.read_typetree_boost = None
        return schemas.read(obj)
    finally:
        TypeTreeHelper.read_typetree_boost = boost
