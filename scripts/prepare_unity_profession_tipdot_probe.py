"""Prepare a source-backed native graph import fixture in the isolated Unity project."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import uuid
import yaml

from official_apk import ROOT
from prepare_unity_profession_native_inputs import CACHE
from prepare_unity_profession_native_bindings import GUID, META_GUID, MANAGED, sha, guid
from validation_workspace import require_temporary_unity_project


def build(project):
    plan_path = CACHE / "typed/native-bindings-main.json"
    plan = json.loads(plan_path.read_bytes())
    prefix = "ScriptableConfig/TipDotGraphAssets/Character/"
    rows = {rel: row for rel, row in plan["assets"].items() if rel.startswith(prefix)}
    if len(rows) != 18:
        raise ValueError("Original profession graph asset count differs")
    root = project / "Assets/ProfessionTipDotGraphProbe"
    root.mkdir(parents=True, exist_ok=True)
    aliases, targets = {}, {}
    for rel, row in rows.items():
        source = ROOT / row["path"]
        if sha(source) != row["sha256"]:
            raise ValueError("Original graph export changed")
        target = root / Path(rel).name
        identity = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-profession-graph-probe/871_156598/" + rel).hex
        aliases[guid(source)] = identity
        targets[guid(source)] = target.relative_to(project).as_posix()
    for rel, binding in plan["script_bindings"].items():
        if binding is not None:
            original = CACHE / "typed/export/ExportedProject/Assets" / rel
            aliases[guid(original)] = binding["guid"]
    def target(ref):
        if "guid" not in ref:
            return targets[guid(path)] if ref["fileID"] else ""
        return targets[ref["guid"]]
    def native(text):
        text = re.sub(r"^%[^\n]*\n", "", text, flags=re.M)
        text = re.sub(r"^--- !u!\d+ &-?\d+\s*\n", "", text, flags=re.M)
        return yaml.safe_load(text)["MonoBehaviour"]
    fixture = dict(format=1, manifest=plan["manifest"], graphs=[], nodes=[])
    for rel, row in rows.items():
        path = ROOT / row["path"]
        text = path.read_text(encoding="utf-8")
        data = native(text)
        if "nodes" in data:
            fixture["graphs"].append(dict(path=targets[guid(path)], name=data["m_Name"], type=data["Type"], scope=data["Scope"], nodes=[target(ref) for ref in data["nodes"]]))
        else:
            refs = data["references"]["RefIds"]
            runtime = next(ref for ref in refs if ref["rid"] == data["TipDotNode"]["rid"])
            runtime["data"]["IsShowTip"] = bool(runtime["data"]["IsShowTip"])
            if "IsAll" in runtime["data"]: runtime["data"]["IsAll"] = bool(runtime["data"]["IsAll"])
            if "Professions" in runtime["data"]:
                packed = runtime["data"]["Professions"]
                if isinstance(packed, str):
                    raw = bytes.fromhex(packed)
                    runtime["data"]["Professions"] = list(struct.unpack("<" + "i" * (len(raw) // 4), raw))
                elif packed is None:
                    runtime["data"]["Professions"] = []
            for key in ("NeedAllShow", "IsRoot", "IsReverse"): data["PresetData"][key] = bool(data["PresetData"][key])
            ports = []
            for key, port in zip(data["ports"]["keys"], data["ports"]["values"]):
                ports.append(dict(key=key, field=port["_fieldName"], owner=target(port["_node"]),
                    type=port["_typeQualifiedName"], direction=port["_direction"], connectionType=port["_connectionType"],
                    typeConstraint=port["_typeConstraint"], flowType=port["_flowConnectionType"], dynamic=bool(port["_dynamic"]),
                    connections=[dict(field=ref["fieldName"], node=target(ref["node"])) for ref in port["connections"]]))
            fixture["nodes"].append(dict(path=targets[guid(path)], graph=target(data["graph"]), name=data["m_Name"],
                nodeId=data["NodeId"], mono=data["Mono"] or "", monoShow=bool(data["IsMonoShow"]),
                id=data["TipDotNodeId"], ban=bool(data["IsBan"]), desc=data["NodeDesc"] or "", nodeName=data["NodeName"] or "", selectedType=data["SelectedType"],
                position=data["position"], preset=data["PresetData"], ports=ports,
                managedType=runtime["type"]["class"], managedData=runtime["data"]))
        for original in set(GUID.findall(text)):
            if original not in aliases and original != "00000000000000000000000000000000":
                raise ValueError("Graph fixture has an unbound reference: " + original)
        text = GUID.sub(lambda match: "guid: " + aliases.get(match[1], match[1]), text)
        for (assembly, namespace, name), binding in MANAGED.items():
            old = "        class: " + name + "\n        ns:" + (" " + namespace if namespace else "") + "\n        asm: " + assembly
            new = "        class: " + binding[2] + "\n        ns: " + binding[1] + "\n        asm: " + binding[0]
            text = text.replace(old, new)
        dest = project / targets[guid(path)]
        dest.write_text(text, encoding="utf-8")
        meta = path.with_name(path.name + ".meta").read_text(encoding="utf-8")
        meta = META_GUID.sub("guid: " + aliases[guid(path)], meta)
        dest.with_name(dest.name + ".meta").write_text(meta, encoding="utf-8")
    fixture_path = CACHE / "typed/tipdot-native-fixture.json"
    fixture_path.write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Prepared original native graph fixture:", len(fixture["graphs"]), "graphs,", len(fixture["nodes"]), "nodes")
    return fixture_path


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    args = parser.parse_args()
    project = args.project.resolve()
    project = require_temporary_unity_project(project)
    build(project)
