"""Record the original UI event code and prepare an isolated native-field probe."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_profession_native_inputs import CACHE
from prepare_unity_profession_native_bindings import guid
from read_official_native import NativeMethods
from validation_workspace import require_temporary_unity_project


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path))


def build(project=None):
    cache = CACHE / "typed"
    exported = cache / "export/ExportedProject/Assets"
    proof_path = ROOT / "assets/reference/profession-ui/native-export-source-manifest.json"
    proof = json.loads(proof_path.read_bytes())
    events = proof["event_trigger_restoration"]["records"]
    if len(events) != 8 or any(row["enable_action"] or row["use_callbacks"] or row["callback_count"] for row in events):
        raise ValueError("Inspect original profession event callback/action scope")
    native = NativeMethods(ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin",
        ROOT / ".tools/source-packages/official-code/global-metadata.dat")
    methods = []
    for name in ("AK.Wwise.BaseType.get_Id", "AK.Wwise.BaseType.IsValid", "AkEvent.HandleEvent",
            "AkEvent.Start", "AkTriggerHandler.Start", "AkTriggerHandler.OnEnable"):
        address, index = native.by_name[name]
        end = min(value for value in native.by_address if value > address)
        methods.append(dict(method=name, metadata_index=index, address=hex(address), byte_count=end-address,
            sha256=hashlib.sha256(native.image[address:end]).hexdigest()))
    if "AK.Wwise.Event.Post" not in native.by_address[0x7926b20]:
        raise ValueError("Original simple event Post overload changed")
    methods.append(dict(method="AK.Wwise.Event.Post(GameObject)", address="0x7926b20", byte_count=0x94,
        sha256=hashlib.sha256(native.image[0x7926b20:0x7926bb4]).hexdigest()))
    folder = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    runtime = [dict(record(folder / (name + ".cs")), meta_sha256=sha(folder / (name + ".cs.meta")))
        for name in ("WwiseEventReference", "OfficialWwiseEvent", "AkEvent", "ButtonSound", "UIClickSound", "PopupWindowSound")]
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    result = dict(format=1, manifest=proof["manifest"], native_export=record(proof_path),
        native_inputs=[record(ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"),
            record(ROOT / ".tools/source-packages/official-code/global-metadata.dat")], methods=methods,
        managed_sources=[record(code / "framework-firstpass-source" / (name + ".cs"))
            for name in ("ButtonSound", "UIClickSound", "PopupWindowSound")], runtime=runtime,
        generator=record(Path(__file__)), validation=record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionEventFieldValidation.cs"),
        scope="Original serialized event references and source UI click/popup dispatch; source AkEvent enable/start trigger, emitter selection and simple Post path. All eight typed source components have actions/callbacks disabled. Unsupported triggers/actions/callbacks fail explicitly. SDK simple Post validates the native object reference, posts its actual ID and preserves the returned playing ID.",
        limitations=["The Windows sound-bank playback host, mixer/media bindings and AkGameObj adapter remain unconnected.",
            "Native event-field import and direct Post contract validation are separate from profession Play Mode UI/mouse/rendering acceptance."])
    target = ROOT / "assets/reference/profession-ui/audio-source-manifest.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if project is not None:
        prepare_probe(project.resolve(), events, exported)
    print("Recorded six original profession event ports and eight source trigger scopes")


def prepare_probe(project, events, exported):
    project = require_temporary_unity_project(project)
    dest = project / "Assets/ProfessionEventFieldProbe"
    if dest.exists():
        raise ValueError("An existing native event probe is not overwritten")
    folder = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    guid_pattern = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")
    index = {}
    for meta in exported.rglob("*.meta"):
        match = guid_pattern.search(meta.read_text(encoding="utf-8"))
        if match:
            index[match[1]] = meta.with_suffix("")
    aliases, event_assets, items = {}, {}, []
    for event in events:
        path = CACHE / "typed/restored" / event["native_relative_path"]
        text = path.read_text(encoding="utf-8")
        docs = documents(text)
        mono = docs[event["file_id"]][1]
        aliases[str(mono["m_Script"]["guid"])] = guid(folder / "AkEvent.cs")
        ref_guid = str(mono["data"]["WwiseObjectReference"]["guid"])
        if ref_guid not in event_assets:
            ref_path = index[ref_guid]
            ref_text = ref_path.read_text(encoding="utf-8")
            ref_fields = documents(ref_text)[11400000][1]
            ref_text = ref_text.replace(str(ref_fields["m_Script"]["guid"]), guid(folder / "WwiseEventReference.cs"))
            probe_guid = hashlib.md5(("profession-event-field-probe:" + ref_guid).encode()).hexdigest()
            aliases[ref_guid] = probe_guid
            event_assets[ref_guid] = (ref_fields, ref_text, probe_guid)
    dest.mkdir(parents=True)
    for key, (fields, text, probe_guid) in event_assets.items():
        path = dest / (key + ".asset")
        path.write_text(text, encoding="utf-8")
        path.with_name(path.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + probe_guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n", encoding="utf-8")
    for i, event in enumerate(events):
        source = CACHE / "typed/restored" / event["native_relative_path"]
        text = source.read_text(encoding="utf-8")
        docs = documents(text)
        mono = docs[event["file_id"]][1]
        go_id = mono["m_GameObject"]["fileID"]
        transform_id = next(pair["component"]["fileID"] for pair in docs[go_id][1]["m_Component"]
            if docs[pair["component"]["fileID"]][0] in ("Transform", "RectTransform"))
        if mono["soundEmitterObject"]["fileID"] != go_id:
            raise ValueError("Probe requires the original self emitter binding")
        headers = list(re.finditer(r"(?m)^--- !u!\d+ &(-?\d+)\n", text))
        spans = {int(match[1]): (match.start(), headers[n+1].start() if n+1 < len(headers) else len(text)) for n,match in enumerate(headers)}
        blocks = {}
        for file_id in (go_id, transform_id, event["file_id"]):
            a,b = spans[file_id]
            blocks[file_id] = text[a:b]
        # A test carrier contains the original GameObject/transform/AkEvent docs.
        # Exclude other components and parent hierarchy only in this probe; the
        # migrated game prefabs retain their complete original hierarchy.
        blocks[go_id] = re.sub(r"(?ms)^  m_Component:.*?(?=  m_Layer:)",
            "  m_Component:\n  - component: {fileID: " + str(transform_id) + "}\n  - component: {fileID: " + str(event["file_id"]) + "}\n", blocks[go_id])
        blocks[transform_id] = re.sub(r"(?m)^  m_Father:.*$", "  m_Father: {fileID: 0}", blocks[transform_id])
        if docs[transform_id][1]["m_Children"]:
            raise ValueError("Inspect source event children before preparing a carrier")
        output = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "".join(blocks[file_id] for file_id in (go_id,transform_id,event["file_id"]))
        output = re.sub(r"guid: ([0-9a-f]{32})", lambda match: "guid: " + aliases.get(match[1], match[1]), output)
        path = dest / (str(i) + ".prefab")
        path.write_text(output, encoding="utf-8")
        path.with_name(path.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(("profession-event-field-probe:" + event["native_relative_path"]).encode()).hexdigest() + "\nPrefabImporter:\n  externalObjects: {}\n", encoding="utf-8")
        ref = event_assets[str(mono["data"]["WwiseObjectReference"]["guid"])][0]
        items.append(dict(path=path.relative_to(project).as_posix(), triggers=event["original_values"],
            event_id=ref["id"], event_name=ref["objectName"], event_guid=ref["guid"], playing_id=mono["playingId"]))
    fixture = CACHE / "typed/event-native-fixture.json"
    fixture.write_text(json.dumps(dict(items=items), indent=2) + "\n", encoding="utf-8")
    print("Prepared original event-field carriers:", len(items), "; original SDK event assets:", len(event_assets))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path)
    build(parser.parse_args().project)
