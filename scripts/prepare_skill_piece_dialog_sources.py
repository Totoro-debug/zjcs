"""Recover omitted serialized fields in the two existing official skill dialogs."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk
from prepare_character_prop_bindings import BindingReader, node_path
from official_unity_schema import ScriptSchemas

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / "unity/Assets/Official/Skills/skill-ui.json"
SOURCE = ROOT / "unity/Assets/Official/Skills/source-manifest.json"
RECEIPT = ROOT / "assets/reference/skill-piece-dialogs-source-manifest.json"
BASE_SHA = "365560c582ccfc8e68f195a7bb2faa82d6f8566fd5a8dc79d35fa74efb7a767c"
CLASSES = {"ItemSourceGo", "MultiLanguageText", "ItemProgress", "ItemDetailDialogAddition"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)


def resolve_states(value, owner):
    registry = {row["rid"]: row for row in owner.get("references", {}).get("RefIds", [])}
    def resolve(item):
        if isinstance(item, list):
            return [resolve(child) for child in item]
        if not isinstance(item, dict):
            return item
        if set(item) == {"rid"} and item["rid"] in registry:
            row = registry[item["rid"]]
            return {"type": row["type"], "data": resolve(row["data"])}
        return {key: resolve(child) for key, child in item.items() if key != "references"}
    return resolve(value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true")
    args = parser.parse_args()
    original = UI.read_bytes()
    data = json.loads(original)
    reader = BindingReader.__new__(BindingReader)
    reader.apk = Apk.current()
    if reader.apk.version != "884_156692":
        raise ValueError("Skill dialog supplements require the pinned 884_156692 source.")
    reader.schemas = ScriptSchemas()
    reader.inputs = {}
    inputs, additions = [], []
    previous = json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {}
    existing_additions = {(row["template"], row["path"], row["class"]) for row in previous.get("added_bindings", [])}
    for key in ("skill_piece_detail", "skill_merge_confirm"):
        source = data[key]["source"]
        prefab = reader.apk.asset(source["bundle"], source["prefab"])
        nodes = {node["path"]: node for node in walk(data[key]["root"])}
        for game_object in reader.walk(prefab.read()):
            path = node_path(game_object)
            # New-version descendants outside the retained historical template
            # are not part of this field-only conversion.
            if path not in nodes:
                continue
            for part in game_object.m_Component:
                obj = part.component.deref()
                if obj.type.name != "MonoBehaviour":
                    continue
                name = obj.read(check_read=False).m_Script.read().m_ClassName
                if name not in CLASSES and name not in ("SkillPieceDetailDialog", "SkillMergeConfirmDialog"):
                    continue
                fields = reader.schemas.read(obj)
                fields = {field: reader.convert(value, obj) for field, value in fields.items()
                          if field not in ("m_GameObject", "m_Enabled", "m_Script", "m_Name")}
                binding = nodes[path].setdefault("original_bindings", {})
                identity = (key, path, name)
                if name not in CLASSES:
                    # The existing prefab field graph must match the selected
                    # official version before adding any missing bindings.
                    old = binding[name]
                    for field, value in old.items():
                        if field == "references":
                            continue
                        if resolve_states(fields.get(field), fields) != resolve_states(value, old):
                            raise ValueError("Current dialog field differs from legacy layout: " + str(identity) + ":" + field)
                    continue
                if name in binding and resolve_states(binding[name], binding[name]) != resolve_states(fields, fields):
                    raise ValueError("Previously decoded official fields differ: " + str(identity))
                if name not in binding or identity in existing_additions:
                    additions.append({"template": key, "path": path, "class": name})
                if name not in binding:
                    binding[name] = fields
                inputs.append({**reader.apk.provenance(obj), "template": key, "path": path,
                               "class": name, "raw_sha256": sha(obj.get_raw_data())})
    output = json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    # Prove that conversion only adds the recorded original component fields.
    baseline = json.loads(output)
    for row in additions:
        node = next(node for node in walk(baseline[row["template"]]["root"]) if node["path"] == row["path"])
        del node["original_bindings"][row["class"]]
        if not node["original_bindings"]:
            del node["original_bindings"]
    if sha(json.dumps(baseline, ensure_ascii=False, separators=(",", ":")).encode("utf-8")) != BASE_SHA:
        raise ValueError("The dialog conversion changes data outside the verified field additions.")
    code = []
    for name in ("SkillPieceDetailDialog", "SkillMergeConfirmDialog", "ItemDetailDialog", "ItemIcon", "ItemIconSkillPiece", "ItemSourceGo", "ItemDetailDialogAddition"):
        path = ROOT / ".tools/source-packages/current/884_156692/official-code/client-source" / (name + ".cs")
        code.append({"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())})
    receipt = {"schema": 1, "official_code_version": "884_156692", "supplement_version": reader.apk.version,
               "legacy_ui_provenance": "unity/Assets/Official/Skills/source-manifest.json",
               "operation": "Recover omitted original component bindings; preserve existing layout, art and historical UI identity.",
               "code": code, "inputs": inputs, "added_bindings": additions,
               "input_sha256": BASE_SHA, "output_sha256": sha(output),
               "output": UI.relative_to(ROOT).as_posix(),
               "runtime_entry": "CharacterSkillPanel.Pieces.cs; existing serialized icon nodes and original state/format fields"}
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    record = next(row for row in source["files"] if row["path"] == "Assets/Official/Skills/skill-ui.json")
    if record["sha256"] != sha(original) or (sha(original) != BASE_SHA and previous.get("output_sha256") != sha(original)):
        raise ValueError("Refusing to replace an unrelated existing UI payload hash.")
    record.update(sha256=sha(output), bytes=len(output))
    source["piece_dialog_binding_conversion"] = {
        "source": RECEIPT.relative_to(ROOT).as_posix(), "input_sha256": BASE_SHA,
        "output_sha256": sha(output), "operation": "Add verified original serialized fields only; no art/layout changes."}
    if args.record:
        UI.write_bytes(output)
        SOURCE.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    elif original != output or previous != receipt or json.loads(SOURCE.read_text(encoding="utf-8")) != source:
        raise ValueError("Original dialog fields or their source receipt need regeneration.")
    print(f"Official skill dialog bindings verified: {len(inputs)} source components, {len(additions)} restored component bindings.")


if __name__ == "__main__":
    main()
