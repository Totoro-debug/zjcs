"""Losslessly expose heterogeneous Unity managed references to AssetRipper.

The converter cannot export Unity's managed-reference registry. Group its
records into ordinary typed vectors, retaining original order and every RID,
type descriptor and data field. Inverse conversion restores the original
registry after AssetRipper has remapped its native object pointers.
"""
from collections import defaultdict
import re

from UnityPy.helpers.TypeTreeNode import TypeTreeNode


def clone(tree):
    # to_dict_list retains m_Children references; from_list would append into
    # those original lists. Build independent nodes instead.
    fields = {key: value for key, value in tree.to_dict().items() if key != "m_Children"}
    result = TypeTreeNode(**fields)
    result.m_Children = [clone(child) for child in tree.m_Children]
    return result


def identity(record):
    typ = record["type"]
    return typ["asm"], typ["ns"], typ["class"]


def normalize(tree):
    sizes = {"bool": 1, "UInt8": 1, "SInt8": 1, "char": 1, "SInt16": 2, "UInt16": 2,
             "SInt32": 4, "UInt32": 4, "int": 4, "float": 4, "SInt64": 8, "UInt64": 8, "double": 8}
    for index, node in enumerate(tree.traverse()):
        node.m_Index = index
        node.m_Version = max(node.m_Version, 1)
        node.m_TypeFlags = node.m_TypeFlags or 0
        node.m_RefTypeHash = node.m_RefTypeHash or 0
        node.m_MetaFlag = node.m_MetaFlag or 0
        if node.m_ByteSize == 0:
            node.m_ByteSize = sizes.get(node.m_Type, -1)
        if node.m_Type == "Array":
            node.m_TypeFlags = 1
        if node.m_Type == "managedReference":
            node.m_Type = "Map13ReferenceId"
    return tree


def scalar(kind, name, level):
    return TypeTreeNode(level, kind, name, 4, 1)


def vector(name, element, level):
    node = TypeTreeNode(level, "vector", name, -1, 1)
    array = TypeTreeNode(level + 1, "Array", "Array", -1, 1, m_TypeFlags=1, m_MetaFlag=0x4000)
    element = clone(element)
    delta = level + 2 - element.m_Level
    for child in element.traverse():
        child.m_Level += delta
    element.m_Name = "data"
    array.m_Children = [scalar("int", "size", level + 2), element]
    node.m_Children = [array]
    return node


def schema(tree, types, schemas):
    result = clone(tree)
    registry, = [node for node in result.m_Children if node.m_Name == "references"]
    registry.m_Type = "Map13ReferenceRegistry"
    original, = [node for node in registry.m_Children if node.m_Name == "RefIds"]
    entry = original.m_Children[0].m_Children[1]
    rid, = [node for node in entry.m_Children if node.m_Name == "rid"]
    typ, = [node for node in entry.m_Children if node.m_Name == "type"]
    # AssetRipper 1.3.14 corrupts negative values in packed Int64/UInt64
    # YAML vectors. A record vector emits ordinary signed scalar RIDs.
    order = clone(entry)
    order.m_Type = "Map13ReferenceOrder"
    order.m_Children = [clone(rid)]
    children = [clone(node) for node in registry.m_Children if node.m_Name != "RefIds"]
    children.append(vector("Map13OriginalOrder", order, registry.m_Level + 1))
    for index, (assembly, namespace, name) in enumerate(types):
        record = clone(entry)
        record.m_Type = "Map13ReferenceRecord" + str(index)
        record.m_MetaFlag = 0
        type_node = clone(typ)
        type_node.m_Type = "Map13ReferenceType"
        record.m_Children = [clone(rid), type_node]
        if name:
            name_full = (namespace + "." if namespace else "") + name
            payload = clone(schemas.tree(assembly + ".dll", name_full))
            payload.m_Children = [node for node in payload.m_Children if node.m_Name not in
                                 ("m_GameObject", "m_Enabled", "m_Script", "m_Name")]
            payload.m_Type = "Map13ReferenceData" + str(index)
            payload.m_Name = "data"
            delta = record.m_Level + 1 - payload.m_Level
            for child in payload.traverse():
                child.m_Level += delta
            record.m_Children.append(payload)
        children.append(vector("Map13Type" + str(index), record, registry.m_Level + 1))
    registry.m_Children = children
    return normalize(result)


def forward(fields, types):
    original = fields["references"]
    if original["version"] != 2:
        raise ValueError("Unsupported original managed-reference version")
    records = original["RefIds"]
    if len({record["rid"] for record in records}) != len(records):
        raise ValueError("Duplicate original managed-reference RID")
    grouped = defaultdict(list)
    for record in records:
        grouped[identity(record)].append(record)
    if not set(grouped).issubset(types):
        raise ValueError("Managed-reference union omits an original type")
    registry = dict(version=original["version"], Map13OriginalOrder=[dict(rid=record["rid"]) for record in records])
    registry.update({"Map13Type" + str(index): grouped[typ] for index, typ in enumerate(types)})
    return dict(fields, references=registry)


def inverse(fields):
    registry = fields.get("references")
    if not registry or "Map13OriginalOrder" not in registry:
        return fields
    entries = [record for key, records in registry.items() if key.startswith("Map13Type") for record in records]
    if len({record["rid"] for record in entries}) != len(entries):
        raise ValueError("Duplicate converted managed-reference RID")
    records = {record["rid"]: record for record in entries}
    order = [entry["rid"] for entry in registry["Map13OriginalOrder"]]
    if set(records) != set(order) or len(records) != len(order):
        raise ValueError("Converted managed-reference records/order differ")
    return dict(fields, references=dict(version=registry["version"], RefIds=[records[rid] for rid in order]))


def restore_exported_yaml(text):
    """Restore registries without reserializing native YAML floats or pointers.

    Keep every exported record verbatim; only replace the temporary grouping/order
    fields with Unity's original ordered RefIds list. Assembly redirects belong
    to the later, explicit runtime binding step.
    """
    restored = []

    def replace(match):
        body = match[1]
        order_match = re.search(r"(?m)^    Map13OriginalOrder:([^\n]*)\n", body)
        if order_match is None:
            return match[0]
        groups = list(re.finditer(r"(?m)^    Map13Type\d+:([^\n]*)\n", body))
        if not groups or order_match[1].strip() not in ("", "[]"):
            raise ValueError("Unexpected exported reference order vector")
        order_block = body[order_match.end():groups[0].start()]
        order_entries = list(re.finditer(r"(?m)^    - rid: (-?\d+)\n", order_block))
        if "".join(entry[0] for entry in order_entries) != order_block:
            raise ValueError("Unexpected exported reference order fields")
        order = [int(entry[1]) for entry in order_entries]
        records = {}
        for index, group in enumerate(groups):
            end = groups[index + 1].start() if index + 1 < len(groups) else len(body)
            block = body[group.end():end]
            entries = list(re.finditer(r"(?m)^    - rid: (-?\d+)\n", block))
            if group[1].strip() not in ("", "[]") or (group[1].strip() == "[]" and entries):
                raise ValueError("Unexpected exported managed-reference group")
            for item, entry in enumerate(entries):
                rid = int(entry[1])
                if rid in records:
                    raise ValueError("Duplicate exported managed-reference RID")
                limit = entries[item + 1].start() if item + 1 < len(entries) else len(block)
                records[rid] = block[entry.start():limit]
        if len(order) != len(records) or set(order) != set(records):
            raise ValueError("Exported managed-reference records/order differ")
        prefix = body[:order_match.start()]
        if prefix != "    version: 2\n":
            raise ValueError("Unexpected exported managed-reference registry header")
        restored.append(order)
        return "  references:\n" + prefix + ("    RefIds:\n" + "".join(records[rid] for rid in order)
                                                if order else "    RefIds: []\n")

    text = re.sub(r"(?m)^  references:\n((?:[ \t]+[^\n]*\n|\n)*)", replace, text)
    if "Map13OriginalOrder:" in text or re.search(r"(?m)^    Map13Type\d+:", text):
        raise ValueError("An exported temporary reference registry was not restored")
    return text, restored
