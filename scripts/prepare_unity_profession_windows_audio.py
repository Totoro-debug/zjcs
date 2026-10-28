"""Resolve the native profession event references through pinned Wwise banks to PCM clips."""
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
import wave
import zipfile
from official_apk import ROOT
from official_cloud_cache import CloudCache
from official_source import OfficialSource
from official_native_yaml_verification import documents
from prepare_character_creation_opening_audio import parse_pck, read_field
from prepare_unity_profession_native_inputs import CACHE
from verify_current_audio_sources import mapping

WORK = ROOT / ".tools/profession-native-ui/audio"
OUTPUT = ROOT / "unity/Assets/Official/ProfessionAudio"
PROOF = ROOT / "assets/reference/profession-ui/windows-audio-source-manifest.json"
WWISER = ROOT / ".tools/wwiser/wwiser.py"
VGMSTREAM = ROOT / ".tools/vgmstream/vgmstream-cli.exe"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def source_file(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def guid(value):
    return hashlib.md5(("zjcs-profession-audio:" + value).encode()).hexdigest()


def all_fields(node, name):
    result = [node.get_attrs()["value"]] if node.get_attrs().get("name") == name else []
    return result + [value for child in node.get_children() or [] for value in all_fields(child, name)]


def media_entries(raw):
    cursor = 0
    chunks = {}
    while cursor < len(raw):
        name = raw[cursor:cursor+4]
        length = struct.unpack_from("<I", raw, cursor+4)[0]
        chunks[name] = raw[cursor+8:cursor+8+length]
        cursor += 8 + length
    if cursor != len(raw):
        raise ValueError("Original bank chunks are incomplete")
    index = chunks[b"DIDX"]
    return [(id, chunks[b"DATA"][offset:offset+size]) for id,offset,size in struct.iter_unpack("<III", index)]


def build():
    WORK.mkdir(parents=True, exist_ok=True)
    selected = OfficialSource()
    if selected.version != "871_156598":
        raise ValueError("Inspect profession Wwise version")
    plan_path = CACHE / "typed/native-bindings-main-dynamic.json"
    plan = json.loads(plan_path.read_bytes())
    events = []
    for relative, asset in plan["assets"].items():
        if not relative.startswith("Wwise/ScriptableObjects/Event/"):
            continue
        path = ROOT / asset["path"]
        if sha(path.read_bytes()) != asset["sha256"]:
            raise ValueError("Native SDK event source changed: " + str(path))
        fields = next(value for kind,value in documents(path.read_text(encoding="utf-8")).values() if kind == "MonoBehaviour")
        events.append(dict(name=fields["objectName"], event_id=fields["id"], native_reference=source_file(path)))
    if len(events) != 5 or len({event["event_id"] for event in events}) != 5:
        raise ValueError("Inspect the complete original profession event reference scope")
    events.sort(key=lambda row: row["event_id"])
    with zipfile.ZipFile(selected.apk) as archive:
        cloud = CloudCache(selected.cloud_path, selected.version)
        manifest = selected.manifest("WwiseBnk", archive)
        def payload(key):
            bundle = next(row for row in manifest["bundles"] if key in row["name"])
            entry = "assets/yoo/WwiseBnk/" + bundle["file"]
            if entry in archive.namelist():
                raw = archive.read(entry)
                origin = dict(origin="apk", entry=entry, manifest=selected.apk_version)
            else:
                found = cloud.read("WwiseBnk", bundle)
                if found is None:
                    raise FileNotFoundError("Original Wwise package not cached: " + bundle["name"])
                raw, origin = found
            if len(raw) != bundle["size"] or hashlib.md5(raw).hexdigest() != bundle["hash"]:
                raise ValueError("Wwise source package differs from selected manifest")
            return raw, dict(bundle=bundle, source=origin, sha256=sha(raw))
        map_raw, map_source = payload("eventbankmapping_bytes")
        bank_map = mapping(map_raw)
        package_sources = [map_source]
        banks_raw = {}
        for key in ("minimum_pck", "ui_sfx_01_pck"):
            raw, source = payload(key)
            package_sources.append(source)
            banks, _ = parse_pck(raw)
            for bank_id, _, size, offset, _ in banks:
                bank = raw[offset:offset+size]
                if bank_id in banks_raw and banks_raw[bank_id] != bank:
                    raise ValueError("Conflicting original Wwise bank identity")
                banks_raw[bank_id] = bank
                (WORK / (str(bank_id) + ".bnk")).write_bytes(bank)
    sys.path.insert(0, str(WWISER.parent))
    from wwiser import wfnv
    from wwiser.parser import wparser
    parser = wparser.Parser()
    bank_paths = [WORK / (str(id) + ".bnk") for id in sorted(banks_raw)]
    parser.parse_banks([str(path) for path in bank_paths])
    bank_objects = {}
    for bank in parser.get_banks():
        bank_id = int(Path(bank.get_attrs()["filename"]).stem)
        hirc = next((child for child in bank.get_children() or [] if child.get_name() == "HircChunk"), None)
        bank_objects[bank_id] = {} if hirc is None else {read_field(item, "ulID"): item
            for group in hirc.get_children() or [] if group.get_name() == "listLoadedItem"
            for item in group.get_children() or []}
    names = WORK / "profession-event-names.txt"
    names.write_text("\n".join(row["name"] for row in events) + "\n", encoding="utf-8")
    txtp_dir = WORK / "profession-source-txtp"
    # Explicit zero master gain disables wwiser's automatic normalization.
    # wwiser logs a misleading warning for exactly 0 dB, but retains zero gain.
    args = [sys.executable, str(WWISER), "-d", "none", "-g", "-go", str(txtp_dir), "-gw", str(WORK),
        "-gv", "0dB", "-nl", str(names), "-gf", *[str(row["event_id"]) for row in events], "--", *map(str, bank_paths)]
    subprocess.run(args, check=True, capture_output=True)
    old = json.loads(PROOF.read_bytes()) if PROOF.exists() else {}
    previous = {row["path"]: row["sha256"] for row in old.get("outputs", [])}
    outputs = []
    def write(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError("Refuse to overwrite unowned/modified audio output: " + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs.append(dict(path=relative, sha256=sha(raw), bytes=len(raw)))
    write(OUTPUT.with_name(OUTPUT.name + ".meta"), ("fileFormatVersion: 2\nguid: " + guid("folder") + "\nfolderAsset: yes\n").encode())
    catalog_entries = []
    for event in events:
        event_id = event["event_id"]
        if wfnv.Fnv().get_hash(event["name"]) != event_id:
            raise ValueError("SDK event ID/name differs from original Wwise FNV identity")
        indices = bank_map["events"][event_id]
        if len(indices) != 1:
            raise ValueError("Inspect multiple-bank profession event")
        bank_name = bank_map["banks"][indices[0]]["name"]
        bank_id = wfnv.Fnv().get_hash(bank_name)
        by_id = bank_objects[bank_id]
        action_ids = all_fields(by_id[event_id], "ulActionID")
        if len(action_ids) != 1:
            raise ValueError("Inspect original multi-action profession sound")
        action = by_id[action_ids[0]]
        if read_field(action, "ulActionType") != 1027 or any(all_fields(action, "cProps")):
            raise ValueError("Inspect original event action/transition parameters")
        sound_id = read_field(action, "idExt")
        sound = by_id[sound_id]
        media_id = read_field(sound, "sourceID")
        if (read_field(sound, "ulPluginID") != 0x40001 or read_field(sound, "StreamType") != 0
                or any(all_fields(sound, "uNumFx"))):
            raise ValueError("Inspect original codec/stream/effect scope")
        txtp = txtp_dir / (event["name"] + ".txtp")
        text = txtp.read_text(encoding="utf-8")
        lines = [line for line in text.splitlines() if line.strip() and not line.lstrip().startswith("#")]
        if len(lines) != 1:
            raise ValueError("Inspect non-one-shot profession event graph")
        first = lines[0]
        match = re.fullmatch(r"(.+?)/(\d+)\.bnk #s(\d+) #i  #v (-?[\d.]+)dB  ##(\d+)\.wem", first)
        if (not match or "master volume: auto" in text or int(match[2]) != bank_id
                or int(match[5]) != media_id):
            raise ValueError("Inspect source TXTP graph/gain: " + event["name"])
        entries = media_entries(banks_raw[bank_id])
        extracted_id, wem = entries[int(match[3])-1]
        if extracted_id != media_id:
            raise ValueError("Source bank subsong differs from actual event media")
        wav = WORK / (event["name"] + "-source.wav")
        subprocess.run([str(VGMSTREAM), "-o", str(wav), str(txtp)], check=True, capture_output=True)
        with wave.open(str(wav), "rb") as audio:
            if audio.getcomptype() != "NONE" or audio.getsampwidth() != 2:
                raise ValueError("Inspect original event PCM format")
            pcm = audio.readframes(audio.getnframes())
            event["pcm"] = dict(channels=audio.getnchannels(), rate=audio.getframerate(), frames=audio.getnframes(), sample_width=2, sha256=sha(pcm))
        clip_guid = guid(event["name"])
        target = OUTPUT / (event["name"] + ".wav")
        write(target, wav.read_bytes())
        meta = "fileFormatVersion: 2\nguid: " + clip_guid + "\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        meta = "\n".join(line.rstrip() for line in meta.splitlines()) + "\n"
        write(target.with_name(target.name + ".meta"), meta.encode())
        event.update(bank_id=bank_id, bank_name=bank_name, action_id=action_ids[0], sound_id=sound_id,
            media_id=media_id, media_sha256=sha(wem), bank_sha256=sha(banks_raw[bank_id]),
            txtp=source_file(txtp), gain_db=float(match[4]), clip=target.relative_to(ROOT).as_posix(), clip_guid=clip_guid)
        catalog_entries.append("  - EventId: " + str(event_id) + "\n    Clip: {fileID: 8300000, guid: " + clip_guid + ", type: 3}\n")
    script = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAudioCatalog.cs"
    script_guid = re.search(r"(?m)^guid: (\w+)$", script.with_name(script.name + ".meta").read_text())[1]
    catalog = OUTPUT / "ProfessionAudio.asset"
    text = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + script_guid + ", type: 3}\n  m_Name: ProfessionAudio\n  m_EditorClassIdentifier: \n  Events:\n" + "".join(catalog_entries)
    text = "\n".join(line.rstrip() for line in text.splitlines()) + "\n"
    write(catalog, text.encode())
    write(catalog.with_name(catalog.name + ".meta"), ("fileFormatVersion: 2\nguid: " + guid("catalog") + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n").encode())
    result = dict(format=1, manifest=selected.version, apk_sha256=selected.apk_sha256,
        package_manifest_sha256=sha(selected.manifest_bytes("WwiseBnk")), packages=package_sources,
        events=events, outputs=outputs, generator=source_file(Path(__file__)),
        tools=[source_file(WWISER), source_file(VGMSTREAM)] + [source_file(ROOT / "scripts" / name) for name in
            ("official_source.py", "official_cloud_cache.py", "verify_current_audio_sources.py", "prepare_character_creation_opening_audio.py")],
        runtime=[source_file(path) for path in sorted((ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI").iterdir()) if path.is_file()],
        validation=source_file(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionAudioPlaybackValidation.cs"),
        operation="Resolve all five native SDK references through current EventBankMapping and actual APK banks. Preserve event/action/sound/media identity; wwiser zero master gain, original bus/voice gain baked via vgmstream to uncompressed PCM; native Unity importer preserves channels/rate with no normalization or second lossy compression.",
        limitations=["Profession UI window installation and local settings mixer are not yet connected.",
            "Only resolved simple UI one-shot events are supported; original callbacks/actions/AkGameObj remain separate migration work."])
    PROOF.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Prepared original profession Windows PCM:", len(events), "events;", len(outputs), "native outputs")
    return result


if __name__ == "__main__":
    build()
