"""Restore source-verified event trigger arrays outside managed-reference registries."""
import hashlib
import json
import re
import struct
from pathlib import Path
from official_apk import Apk, ROOT
from official_native_yaml_verification import documents
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_water_entity_export import original_identity
from prepare_unity_profession_native_inputs import CACHE, ROOT_BUNDLES
from read_official_native import NativeMethods


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def restore(proof_path):
    cache = CACHE / "typed"
    exported = cache / "export/ExportedProject/Assets"
    destination = cache / "restored"
    source = json.loads((cache / "inputs/source-manifest.json").read_bytes())
    proof = json.loads(proof_path.read_bytes())
    apk = Apk.current()
    _, missing = collect(apk, ROOT_BUNDLES)
    if missing:
        raise ValueError("Profession event source dependency closure is incomplete")
    objects = {(obj.assets_file.name, str(obj.path_id)): obj for bundle in apk.loaded.values()
        for file in serialized_files(bundle) for obj in file.objects.values()}
    originals = {}
    for row in source["components"]:
        if row["type"] != "AkEvent":
            continue
        obj = objects[row["cab"], row["path_id"]]
        if sha(obj.get_raw_data()) != row["payload_sha256"]:
            raise ValueError("Original profession event payload changed")
        identity = original_identity(obj)
        if identity in originals:
            raise ValueError("Profession event hierarchy identity is ambiguous")
        originals[identity] = row
    if len(originals) != 8:
        raise ValueError("Inspect original profession event coverage")
    guid_pattern = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")
    script_meta = exported / "Scripts/AK.Wwise.Unity.MonoBehaviour/AkEvent.cs.meta"
    script_guid = guid_pattern.search(script_meta.read_text(encoding="utf-8"))[1]
    records, checked = [], set()
    outputs = {row["path"]: row for row in proof["outputs"]}
    for path in sorted(exported.rglob("*.prefab")):
        raw = path.read_bytes()
        if script_guid.encode() not in raw:
            continue
        relative = path.relative_to(exported)
        target = destination / relative
        text = (target if target.exists() else path).read_text(encoding="utf-8")
        docs = documents(text)
        def go_identity(file_id):
            fields = docs[file_id][1]
            transform = next(pair["component"]["fileID"] for pair in fields["m_Component"]
                if docs[pair["component"]["fileID"]][0] in ("RectTransform", "Transform"))
            parent = docs[transform][1]["m_Father"]["fileID"]
            prefix = go_identity(docs[parent][1]["m_GameObject"]["fileID"]) + "/" if parent else ""
            return prefix + str(fields["m_Name"] or "")
        edits = []
        headers = list(re.finditer(r"(?m)^--- !u!\d+ &(-?\d+)\n", text))
        spans = {int(match[1]): (match.end(), headers[i + 1].start() if i + 1 < len(headers) else len(text))
            for i, match in enumerate(headers)}
        for file_id, (kind, fields) in docs.items():
            if kind != "MonoBehaviour" or fields.get("m_Script", {}).get("guid") != script_guid:
                continue
            identity = ("AkEvent", go_identity(fields["m_GameObject"]["fileID"]))
            if identity not in originals:
                # Export includes unrelated dependency prefabs outside the fully
                # typed profession/rule/item source scope. Do not change them.
                continue
            if identity in checked:
                raise ValueError("Exported profession event identity is duplicated: " + str(identity))
            row = originals[identity]
            values = row["fields"]["triggerList"]
            if not values or any(type(value) is not int for value in values):
                raise ValueError("Inspect original profession trigger list type")
            # Event callbacks/actions have not been adapted to a Windows host.
            # Record their actual source scope; never infer event timing from a name.
            packed = struct.pack("<" + "i" * len(values), *values).hex()
            start, end = spans[file_id]
            body, count = re.subn(r"(?m)^  triggerList:[^\n]*$", "  triggerList: " + packed, text[start:end])
            if count != 1:
                raise ValueError("Original profession trigger field was not exported once")
            edits.append((start, end, body))
            records.append(dict(cab=row["cab"], path_id=row["path_id"], payload_sha256=row["payload_sha256"],
                hierarchy=identity[1], native_relative_path=relative.as_posix(), file_id=file_id,
                original_values=values, exported_scalar=str(fields["triggerList"]), restored_hex=packed,
                use_other_object=row["fields"]["useOtherObject"], enable_action=row["fields"]["enableActionOnEvent"],
                use_callbacks=row["fields"]["useCallbacks"], callback_count=len(row["fields"]["Callbacks"])))
            checked.add(identity)
        if not edits:
            continue
        for start, end, body in sorted(edits, reverse=True):
            text = text[:start] + body + text[end:]
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")
        target.with_name(target.name + ".meta").write_bytes(path.with_name(path.name + ".meta").read_bytes())
        key = target.relative_to(ROOT).as_posix()
        output = outputs.get(key, dict(input=path.relative_to(ROOT).as_posix(), input_sha256=sha(raw), path=key, registries=0))
        output["sha256"] = sha(target.read_bytes())
        outputs[key] = output
        restored = documents(text)
        for record in [record for record in records if record["native_relative_path"] == relative.as_posix()]:
            actual = restored[record["file_id"]][1]["triggerList"]
            if list(struct.unpack("<" + "i" * len(record["original_values"]), bytes.fromhex(str(actual)))) != record["original_values"]:
                raise ValueError("Restored event trigger bytes differ from original source")
    if checked != set(originals):
        raise ValueError("Not every original profession event trigger was restored")
    native = NativeMethods(ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin",
        ROOT / ".tools/source-packages/official-code/global-metadata.dat")
    address, method_index = native.by_name["AkTriggerHandler.OnEnable"]
    # The native handler checks this exact List<int> value before HandleEvent(null).
    words = struct.unpack_from("<2I", native.image, 0x794a7c4)
    if address != 0x794a788 or words != (0x529b3e41, 0x72bd9c01):
        raise ValueError("Inspect the original enable-trigger comparison instructions")
    method_bytes = native.image[address:address + 0x7c]
    proof["event_trigger_restoration"] = dict(records=records,
        native_enable_handler=dict(method="AkTriggerHandler.OnEnable", metadata_index=method_index,
            address=hex(address), byte_count=len(method_bytes), sha256=sha(method_bytes), trigger_id=-320808462),
        generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
        scope="Restore only original int-list bytes on all eight source-identified AkEvent components; preserve all other prefab text and registry restoration. Windows playback and callback/action support are separate.")
    proof["counts"]["event_trigger_components"] = len(records)
    proof["counts"]["event_trigger_values"] = sum(len(record["original_values"]) for record in records)
    proof["outputs"] = sorted(outputs.values(), key=lambda row: row["path"])
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Restored original profession event trigger arrays:", len(records), "components")
