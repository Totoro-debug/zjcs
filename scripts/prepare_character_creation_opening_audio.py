"""Extract and convert the official opening dialogue voices from the pinned Wwise PCK."""
import hashlib
import json
import struct
import subprocess
import tempfile
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_NAME = "wwisebnk_assets_wwisedata_android_zh_cn_voice_prologue_pck.rawfile"
SOURCE = ROOT / ".tools/source-packages/WwiseBnk" / SOURCE_NAME
PACKAGE_INDEX = ROOT / ".tools/source-packages/WwiseBnk/bundle-index.json"
PACKAGE_SOURCES = ROOT / ".tools/source-packages/WwiseBnk/bundle-sources.json"
PAYLOAD = ROOT / "unity/Assets/StreamingAssets/Opening"
REFERENCE = ROOT / "assets/reference/character-creation/opening-source-manifest.json"
WWISER = ROOT / ".tools/wwiser"
VGMSTREAM = ROOT / ".tools/vgmstream/vgmstream-cli.exe"
EVENTS = (
    "prologue_main_1_01",
    "prologue_main_1_02",
    "prologue_main_2_01",
    "prologue_main_2_02",
    "prologue_main_3_01",
    "prologue_main_3_02",
)


def sha256(value):
    return hashlib.sha256(value).hexdigest()


def read_field(node, name):
    for child in node.get_children() or []:
        attrs = child.get_attrs()
        if attrs.get("name") == name:
            return attrs.get("value")
        value = read_field(child, name)
        if value is not None:
            return value
    return None


def parse_pck(raw):
    if raw[:4] != b"AKPK" or struct.unpack_from("<I", raw, 8)[0] != 1:
        raise ValueError("The opening Wwise package header is unsupported")
    start = 28 + struct.unpack_from("<I", raw, 12)[0]
    bank_bytes = struct.unpack_from("<I", raw, 16)[0]
    file_bytes = struct.unpack_from("<I", raw, 20)[0]
    bank_count = struct.unpack_from("<I", raw, start)[0]
    cursor = start + 4
    banks = []
    for _ in range(bank_count):
        banks.append(struct.unpack_from("<5I", raw, cursor))
        cursor += 20
    if cursor != start + bank_bytes:
        raise ValueError("Wwise bank index length does not match its header")
    file_count = struct.unpack_from("<I", raw, cursor)[0]
    cursor += 4
    files = []
    for _ in range(file_count):
        files.append(struct.unpack_from("<5I", raw, cursor))
        cursor += 20
    if cursor != start + bank_bytes + file_bytes:
        raise ValueError("Wwise media index length does not match its header")
    for media_id, _, size, offset, _ in files:
        if offset + size > len(raw) or raw[offset:offset + 4] != b"RIFF":
            raise ValueError("Wwise media record is incomplete: %08x" % media_id)
    return banks, files


def source_receipt(raw):
    expected_md5 = json.loads(PACKAGE_INDEX.read_text(encoding="utf-8"))[SOURCE_NAME]
    actual_md5 = hashlib.md5(raw).hexdigest()
    if actual_md5 != expected_md5:
        raise ValueError("Pinned Wwise source MD5 mismatch: " + actual_md5)
    receipts = json.loads(PACKAGE_SOURCES.read_text(encoding="utf-8"))[SOURCE_NAME]
    if receipts.get("bundle_hash") != expected_md5:
        raise ValueError("Wwise source receipt does not match its package index")
    return {"package": "WwiseBnk", "manifest": "854_156383", "asset": SOURCE_NAME,
            "md5": actual_md5, "sha256": sha256(raw), "receipt": receipts}


def resolve_media(bank_path, banks, files):
    import sys
    sys.path.insert(0, str(WWISER))
    from wwiser import wfnv
    from wwiser.parser import wparser

    bank_id = wfnv.Fnv().get_hash("voice_prologue")
    if not any(entry[0] == bank_id for entry in banks):
        raise ValueError("The original voice_prologue bank is missing from the package")
    parser = wparser.Parser()
    parser.parse_banks([str(bank_path)])
    parsed_banks = parser.get_banks()
    hirc = next((item for item in parsed_banks[0].get_children()
                 if item.get_name() == "HircChunk"), None)
    if hirc is None:
        raise ValueError("The original prologue bank has no HIRC objects")
    objects = [item for group in hirc.get_children() or [] if group.get_name() == "listLoadedItem"
               for item in group.get_children() or []]
    objects_by_id = {read_field(item, "ulID"): item for item in objects}

    result = {}
    for event in EVENTS:
        event_id = wfnv.Fnv().get_hash(event)
        event_object = objects_by_id.get(event_id)
        if event_object is None:
            raise ValueError("Official opening voice event is missing: " + event)
        actions = next((child for child in event_object.get_children() or []
                        if child.get_name() == "EventInitialValues"), None)
        actions = next((child for child in actions.get_children() or []
                        if child.get_name() == "actions"), None) if actions else None
        action_ids = [read_field(action, "ulActionID") for action in actions.get_children() or []] if actions else []
        if len(action_ids) != 1 or action_ids[0] not in objects_by_id:
            raise ValueError("Opening event does not resolve to one original action: " + event)
        action = objects_by_id[action_ids[0]]
        sound_id = read_field(action, "idExt")
        sound = objects_by_id.get(sound_id)
        media_id = read_field(sound, "sourceID") if sound is not None else None
        if sound is None or media_id is None:
            raise ValueError("Opening event has an unresolved Sound/Media reference: " + event)
        media = next((entry for entry in files if entry[0] == media_id), None)
        if media is None:
            raise ValueError("Opening event media is absent from the original package: " + event)
        result[event] = {"event_id": event_id, "action_id": action_ids[0],
                         "sound_id": sound_id, "media_id": media_id, "media": media}
    return result


def main():
    if not SOURCE.is_file() or not VGMSTREAM.is_file():
        raise FileNotFoundError("Official Wwise source or vgmstream converter is missing")
    raw = SOURCE.read_bytes()
    source = source_receipt(raw)
    banks, files = parse_pck(raw)
    PAYLOAD.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="opening-audio-", dir=ROOT / ".tools/unity-temp") as temp:
        temp = Path(temp)
        bank_id = next((entry[0] for entry in banks if entry[0] == 0x5F314EDB), None)
        if bank_id is None:
            raise ValueError("The official voice_prologue bank has an unexpected ID")
        bank_record = next(entry for entry in banks if entry[0] == bank_id)
        _, _, size, offset, _ = bank_record
        bank_path = temp / "voice_prologue.bnk"
        bank_path.write_bytes(raw[offset:offset + size])
        resolved = resolve_media(bank_path, banks, files)
        voice_dir = PAYLOAD / "voices"
        voice_dir.mkdir(parents=True, exist_ok=True)
        mapping = {"manifest": "854_156383", "voices": []}
        output_records = []
        for event, binding in resolved.items():
            media_id, _, size, offset, _ = binding["media"]
            wem_path = temp / ("%08x.wem" % media_id)
            wem = raw[offset:offset + size]
            wem_path.write_bytes(wem)
            wav_path = voice_dir / (event + ".wav")
            subprocess.run([str(VGMSTREAM), "-o", str(wav_path), str(wem_path)],
                           check=True, capture_output=True, text=True)
            with wave.open(str(wav_path), "rb") as decoded:
                if decoded.getcomptype() != "NONE" or decoded.getsampwidth() not in (2, 3, 4):
                    raise ValueError("Decoded opening voice is not supported PCM: " + event)
                audio = {"channels": decoded.getnchannels(), "sample_rate": decoded.getframerate(),
                         "sample_width": decoded.getsampwidth(), "frames": decoded.getnframes()}
            output_path = wav_path.relative_to(PAYLOAD).as_posix()
            data = wav_path.read_bytes()
            mapping["voices"].append({"event": event, "file": output_path})
            output_records.append({"path": "unity/Assets/StreamingAssets/Opening/" + output_path,
                                   "bytes": len(data), "sha256": sha256(data)})
            binding.update({"wem_bytes": size, "wem_sha256": sha256(wem), "audio": audio,
                            "output_sha256": sha256(data)})

    mapping_path = PAYLOAD / "opening-voices.json"
    mapping_bytes = (json.dumps(mapping, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    mapping_path.write_bytes(mapping_bytes)
    output_records.append({"path": "unity/Assets/StreamingAssets/Opening/opening-voices.json",
                           "bytes": len(mapping_bytes), "sha256": sha256(mapping_bytes)})
    manifest = json.loads(REFERENCE.read_text(encoding="utf-8"))
    previous = {item["path"] for item in output_records}
    manifest["outputs"] = [item for item in manifest.get("outputs", []) if item["path"] not in previous]
    manifest["outputs"].extend(output_records)
    manifest["outputs"].sort(key=lambda item: item["path"])
    manifest["voices"] = {"source": source, "bank_id": bank_id, "events": resolved,
                          "conversion": "Wwise Vorbis WEM to PCM WAV via pinned local vgmstream",
                          "outputs": [item["path"] for item in output_records]}
    REFERENCE.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"source": source["asset"], "events": len(resolved),
                      "outputs": output_records}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
