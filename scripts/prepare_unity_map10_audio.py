"""Extract the official Map10 voice and playable sound events from pinned Wwise PCKs."""
from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

from prepare_character_creation_opening_audio import parse_pck, read_field, sha256


ROOT = Path(__file__).resolve().parents[1]
WWISE_ROOT = ROOT / ".tools/source-packages/WwiseBnk"
PACKAGE_INDEX = WWISE_ROOT / "bundle-index.json"
PACKAGE_SOURCES = WWISE_ROOT / "bundle-sources.json"
WWISER = ROOT / ".tools/wwiser"
VGMSTREAM = ROOT / ".tools/vgmstream/vgmstream-cli.exe"
STORY_DATA = ROOT / "unity/Assets/Official/World/Map/10/Resources/Map10/stories.json"
MAP_DATA = ROOT / "unity/Assets/Official/World/Map/10/map10.json"
OFFICIAL_CONFIG_DATA = ROOT / "assets/official/adventure.json"
EXISTING_AUDIO_MAPPING = ROOT / "assets/official/map-audio.json"
EXISTING_AUDIO_MANIFEST = ROOT / "assets/reference/official-map/audio-source-manifest.json"
OUTPUT_ROOT = ROOT / "unity/Assets/StreamingAssets/Map10Audio"
OUTPUT_MANIFEST = ROOT / "assets/reference/official-map/map10-audio-source-manifest.json"
OUTPUT_MAPPING = OUTPUT_ROOT / "map10-audio.json"
CROSS_PACKAGE_AUDIO_MANIFEST = ROOT / "assets/reference/official-map/tower-cannons-source-manifest.json"
MAP_SFX_PACKAGE = "wwisebnk_assets_wwisedata_android_map_sfx_01_pck.rawfile"
EMBEDDED_MAP10_MEDIA = {
    967493768: {"package": MAP_SFX_PACKAGE, "bank_id": 236876705, "offset": 4340412,
                "bytes": 27054, "sha256": "5104dd0fc2e8d0ed4f43c8573f9d643dbee568efc1677db785c761ef2747ae79"},
    346297511: {"package": MAP_SFX_PACKAGE, "bank_id": 236876705, "offset": 1043196,
                "bytes": 21602, "sha256": "86c33cac08c70f7bd37cc265cf0d1a1f590176c0cc07f1da76c7e46f3cfaaae7"},
    470549823: {"package": MAP_SFX_PACKAGE, "bank_id": 236876705, "offset": 1590636,
                "bytes": 18073, "sha256": "a272837d813c2d5c8d9b5c6d3b709ccf74df2ae13988e268f2fe5b2e01b5e204"},
}


def _pck_names():
    index = json.loads(PACKAGE_INDEX.read_text(encoding="utf-8"))
    return sorted(name for name in index if name.endswith("_pck.rawfile")
                  and (WWISE_ROOT / name).is_file())


def _package_receipt(name, raw, index, receipts):
    actual = hashlib.md5(raw).hexdigest()
    if index.get(name) != actual:
        raise ValueError("Pinned Wwise source MD5 mismatch: " + name)
    receipt = receipts.get(name)
    if not receipt or receipt.get("bundle_hash") != actual:
        raise ValueError("Wwise source receipt does not match its package index: " + name)
    return {"package": "WwiseBnk", "manifest": "854_156383", "asset": name,
            "md5": actual, "sha256": sha256(raw), "receipt": receipt}


def _story_voice_events():
    data = json.loads(STORY_DATA.read_text(encoding="utf-8"))
    result = {line["VoiceEventKey"] for story in data["stories"]
              for group in story.get("dialogueGroups", [])
              for line in group.get("Lines", []) if line.get("VoiceEventKey")}
    result.update(node["VoiceEventKey"] for dialog in data["nativeDialogs"]
                  for node in dialog.get("Nodes", []) if node.get("VoiceEventKey"))
    return sorted(result)


def _playable_sounds():
    playable_data = json.loads((ROOT / "assets/official/map-playables.json").read_text(encoding="utf-8"))
    expected = ("91ab22ab272abaf4eb63daaea8bb0d29",
                "5fd5bec6c4f91f54799a4b823888f49a",
                "9b248cde91299ee4287533b25b7dd468")
    registry = playable_data.get("registry", {})
    result = []
    for guid in expected:
        scene = playable_data["scenes"].get(guid)
        if scene is None:
            raise ValueError("Official Map10 playable source is missing: " + guid)
        sounds = []
        for track in scene.get("tracks", []):
            if track.get("type") != "MyPlayableSound":
                continue
            event = track.get("config", {}).get("SoundEvent", {}).get("WwiseObjectReference", {}).get("event")
            if event:
                sounds.append({"eventKey": event, "start": track["start"]})
        result.append({"assetGuid": guid,
                       "keys": sorted(key for key, value in registry.items() if value == guid),
                       "sounds": sounds})
    return result


def _map_bgm():
    map_data = json.loads(MAP_DATA.read_text(encoding="utf-8"))
    bgm_id = map_data.get("MapBgmId", 0)
    if bgm_id <= 0:
        raise ValueError("Official Map10 map data does not define a BGM id")
    config_data = json.loads(OFFICIAL_CONFIG_DATA.read_text(encoding="utf-8"))
    rows = [row for row in config_data["tables"]["bgm_event_key"]
            if row["BgmId"] == bgm_id and row["IsDefault"]
            and row["Region"] == 0 and row["ReleaseType"] == 2]
    if len(rows) != 1:
        raise ValueError("Official Map10 BGM event config is missing or ambiguous")
    row = rows[0]
    audio_index = json.loads(EXISTING_AUDIO_MAPPING.read_text(encoding="utf-8"))
    if audio_index["bgms"].get(str(bgm_id)) != row["BgmPlayEventKey"] \
        or row["BgmPlayEventKey"] not in audio_index["events"]:
        raise ValueError("Official Map10 BGM event does not match its MapAudio index")
    return {"bgmId": bgm_id, "playEventKey": row["BgmPlayEventKey"],
            "stopEventKey": row["BgmStopEventKey"], "pauseEventKey": row["BgmPauseEventKey"],
            "resumeEventKey": row["BgmResumeEventKey"], "loop": True}


def _find_events_in_bank(bank_path, target_ids):
    sys.path.insert(0, str(WWISER))
    from wwiser.parser import wparser

    parser = wparser.Parser()
    parser.parse_banks([str(bank_path)])
    matches = {}
    for bank in parser.get_banks():
        hirc = next((item for item in bank.get_children() if item.get_name() == "HircChunk"), None)
        if hirc is None:
            continue
        objects = [item for group in hirc.get_children() or [] if group.get_name() == "listLoadedItem"
                   for item in group.get_children() or []]
        by_id = {read_field(item, "ulID"): item for item in objects}
        for event_id in target_ids:
            event = by_id.get(event_id)
            if event is None:
                continue
            actions = next((child for child in event.get_children() or []
                            if child.get_name() == "EventInitialValues"), None)
            actions = next((child for child in actions.get_children() or []
                            if child.get_name() == "actions"), None) if actions else None
            action_ids = [read_field(action, "ulActionID") for action in actions.get_children() or []] if actions else []
            media_ids = []
            for action_id in action_ids:
                action = by_id.get(action_id)
                sound = by_id.get(read_field(action, "idExt")) if action is not None else None
                media_id = read_field(sound, "sourceID") if sound is not None else None
                if media_id is not None:
                    media_ids.append(media_id)
            if media_ids:
                matches[event_id] = {"bank_id": read_field(bank, "ulID"),
                                     "action_ids": action_ids, "media_ids": media_ids}
    return matches


def _resolve_media(events):
    sys.path.insert(0, str(WWISER))
    from wwiser import wfnv

    names = _pck_names()
    index = json.loads(PACKAGE_INDEX.read_text(encoding="utf-8"))
    receipts = json.loads(PACKAGE_SOURCES.read_text(encoding="utf-8"))
    cross_package_media = json.loads(CROSS_PACKAGE_AUDIO_MANIFEST.read_text(encoding="utf-8"))["audio"]["media"]
    ids = {event: wfnv.Fnv().get_hash(event) for event in events}
    pending = set(ids.values())
    resolved = {}
    package_sources = {}
    package_bytes = {}

    def search(package_names):
        for name in package_names:
            if not pending:
                return
            path = WWISE_ROOT / name
            raw = path.read_bytes()
            package_bytes[name] = raw
            receipt = _package_receipt(name, raw, index, receipts)
            banks, media = parse_pck(raw)
            media_by_id = {record[0]: record for record in media}
            candidates = {event_id for event_id in pending
                          if raw.find(event_id.to_bytes(4, "little")) >= 0}
            if not candidates:
                continue
            with tempfile.TemporaryDirectory(prefix="map10-audio-", dir=ROOT / ".tools/unity-temp") as temp:
                temp = Path(temp)
                for number, bank_record in enumerate(banks):
                    bank_id, _, size, offset, _ = bank_record
                    bank_path = temp / ("bank-%03d.bnk" % number)
                    bank_path.write_bytes(raw[offset:offset + size])
                    matches = _find_events_in_bank(bank_path, candidates)
                    for event_id, binding in matches.items():
                        records = []
                        for media_id in binding["media_ids"]:
                            if media_id in media_by_id:
                                media_record = media_by_id[media_id]
                                source_package = name
                                source_raw = raw
                                source_sha256 = None
                            else:
                                source_record = EMBEDDED_MAP10_MEDIA.get(media_id)
                                if source_record is None:
                                    source_record = cross_package_media.get(str(media_id))
                                if source_record is None:
                                    raise ValueError("Official Wwise media is missing for event %08x, media %d" %
                                                     (event_id, media_id))
                                source_package = source_record["package"]
                                if source_package not in package_bytes:
                                    source_raw = (WWISE_ROOT / source_package).read_bytes()
                                    package_bytes[source_package] = source_raw
                                    package_sources[source_package] = _package_receipt(
                                        source_package, source_raw, index, receipts)
                                else:
                                    source_raw = package_bytes[source_package]
                                media_record = (media_id, 0, source_record["bytes"],
                                                source_record["offset"], 0)
                                source_sha256 = source_record["sha256"]
                                if source_record.get("bank_id") is not None:
                                    source_banks = banks if source_package == name else parse_pck(source_raw)[0]
                                    source_bank = next((item for item in source_banks
                                                        if item[0] == source_record["bank_id"]), None)
                                    if source_bank is None \
                                        or source_record["offset"] < source_bank[3] \
                                        or source_record["offset"] + source_record["bytes"] > source_bank[3] + source_bank[2]:
                                        raise ValueError("Embedded Wwise media is outside its recorded bank: %d" % media_id)
                            _, _, media_size, media_offset, _ = media_record
                            media_bytes = source_raw[media_offset:media_offset + media_size]
                            if len(media_bytes) != media_size:
                                raise ValueError("Wwise media slice is truncated: %d" % media_id)
                            if source_sha256 and sha256(media_bytes) != source_sha256:
                                raise ValueError("Cross-package Wwise media SHA256 mismatch: %d" % media_id)
                            records.append({"id": media_id, "bytes": media_bytes,
                                            "package": source_package, "sha256": sha256(media_bytes),
                                            "bankId": source_record.get("bank_id") if media_id not in media_by_id else None,
                                            "offset": media_offset, "size": media_size})
                        resolved[event_id] = {"package": name, "source": receipt,
                                              "bank_id": binding["bank_id"], "bank_record_id": bank_id,
                                              "action_ids": binding["action_ids"],
                                              "media": records}
                        pending.remove(event_id)
            package_sources[name] = receipt

    search([name for name in names if "map_sfx" in name or "voice_mapchat" in name])
    if pending:
        search(names)
    if pending:
        missing = sorted("%s (%08x)" % (event, ids[event]) for event in events if ids[event] in pending)
        raise ValueError("Official Map10 Wwise events could not be resolved in pinned PCKs: " + ", ".join(missing))

    return ids, resolved, package_sources


def _write_identical(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if path.read_bytes() != data:
            raise FileExistsError("Refusing to replace a different Map10 audio output: " + str(path))
        return
    path.write_bytes(data)


def _write_mapping_upgrade(path, data, event_keys):
    if path.exists() and path.read_bytes() != data:
        previous = json.loads(path.read_text(encoding="utf-8"))
        old_keys = {item.get("eventKey") for item in previous.get("events", [])}
        if previous.get("manifest") != "854_156383" or previous.get("mapClassId") != 10 \
            or previous.get("mapBgm") or previous.get("playables") != _playable_sounds() \
            or not old_keys.issubset(event_keys):
            raise FileExistsError("Refusing to replace a different Map10 audio mapping: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def _write_manifest_upgrade(path, data):
    if path.exists() and path.read_bytes() != data:
        previous = json.loads(path.read_text(encoding="utf-8"))
        if previous.get("manifest") != "854_156383" or previous.get("mapClassId") != 10 \
            or previous.get("operation") not in (
                "Reuse verified official MapAudio Vorbis and extract uncovered pinned Map10 Wwise voices to PCM WAV",
                "Reuse verified official MapAudio Vorbis and extract pinned Map10 Wwise voice/playable sounds to PCM WAV"):
            raise FileExistsError("Refusing to replace a different Map10 audio source manifest: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def main():
    if not VGMSTREAM.is_file():
        raise FileNotFoundError("Pinned local vgmstream converter is missing")
    playables = _playable_sounds()
    map_bgm = _map_bgm()
    events = sorted(set(_story_voice_events()) | {map_bgm["playEventKey"]} | {
        sound["eventKey"] for playable in playables for sound in playable["sounds"]})
    source_mapping = json.loads(EXISTING_AUDIO_MAPPING.read_text(encoding="utf-8"))
    source_manifest = json.loads(EXISTING_AUDIO_MANIFEST.read_text(encoding="utf-8"))
    known_events = source_mapping["events"]
    extracted_events = [event for event in events if event not in known_events]
    if extracted_events:
        event_ids, bindings, package_sources = _resolve_media(extracted_events)
    else:
        event_ids, bindings, package_sources = {}, {}, {}
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    mapping = {"manifest": "854_156383", "mapClassId": 10, "events": [],
               "playables": playables, "mapBgm": map_bgm}
    event_records = []
    output_records = []

    with tempfile.TemporaryDirectory(prefix="map10-audio-convert-", dir=ROOT / ".tools/unity-temp") as temp:
        temp = Path(temp)
        for event in events:
            event_files = []
            if event in known_events:
                for index, source_path in enumerate(known_events[event]):
                    source = source_manifest["resources"].get(source_path)
                    if source is None or source.get("sha256") is None:
                        raise ValueError("Official Map10 voice source is missing: " + source_path)
                    source_file = ROOT / source_path.removeprefix("res://")
                    source_bytes = source_file.read_bytes()
                    if sha256(source_bytes) != source["sha256"]:
                        raise ValueError("Official Map10 source audio SHA256 mismatch: " + source_path)
                    output_name = "%s-%02d.ogg" % (event, index)
                    output_path = OUTPUT_ROOT / output_name
                    _write_identical(output_path, source_bytes)
                    relative = "Map10Audio/" + output_name
                    event_files.append({"path": relative, "audioType": "OGGVORBIS"})
                    output_records.append({"path": "unity/Assets/StreamingAssets/" + relative,
                                          "bytes": len(source_bytes), "sha256": sha256(source_bytes)})
                    event_records.append({"event": event, "sourcePath": source_path,
                                          "sourceSha256": source["sha256"], "outputPath": relative,
                                          "outputSha256": sha256(source_bytes)})
            else:
                binding = bindings[event_ids[event]]
                for index, record in enumerate(binding["media"]):
                    media_id = record["id"]
                    wem_bytes = record["bytes"]
                    wem_path = temp / ("%08x.wem" % media_id)
                    wav_path = temp / ("%s-%02d.wav" % (event, index))
                    wem_path.write_bytes(wem_bytes)
                    subprocess.run([str(VGMSTREAM), "-o", str(wav_path), str(wem_path)],
                                   check=True, capture_output=True, text=True)
                    with wave.open(str(wav_path), "rb") as decoded:
                        if decoded.getcomptype() != "NONE" or decoded.getsampwidth() not in (2, 3, 4):
                            raise ValueError("Map10 event did not decode to supported PCM WAV: " + event)
                        audio = {"channels": decoded.getnchannels(), "sample_rate": decoded.getframerate(),
                                 "sample_width": decoded.getsampwidth(), "frames": decoded.getnframes()}
                    output_name = "%s-%02d.wav" % (event, index)
                    output_path = OUTPUT_ROOT / output_name
                    data = wav_path.read_bytes()
                    _write_identical(output_path, data)
                    relative = "Map10Audio/" + output_name
                    event_files.append({"path": relative, "audioType": "WAV"})
                    output_records.append({"path": "unity/Assets/StreamingAssets/" + relative,
                                          "bytes": len(data), "sha256": sha256(data)})
                    event_records.append({"event": event, "eventId": event_ids[event],
                                          "package": binding["package"], "source": binding["source"],
                                          "bankId": binding["bank_id"], "bankRecordId": binding["bank_record_id"],
                                          "actionIds": binding["action_ids"],
                                          "mediaId": media_id, "mediaPackage": record["package"],
                                          "mediaBankId": record["bankId"], "mediaOffset": record["offset"],
                                          "mediaBytes": record["size"],
                                          "wemSha256": sha256(wem_bytes), "outputPath": relative,
                                          "outputSha256": sha256(data), "audio": audio})
            mapping["events"].append({"eventKey": event, "files": event_files})

    bgm_entry = next(entry for entry in mapping["events"]
                     if entry["eventKey"] == map_bgm["playEventKey"])
    map_bgm["file"] = bgm_entry["files"][0]

    mapping_bytes = (json.dumps(mapping, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    _write_mapping_upgrade(OUTPUT_MAPPING, mapping_bytes, {entry["eventKey"] for entry in mapping["events"]})
    output_records.append({"path": "unity/Assets/StreamingAssets/Map10Audio/map10-audio.json",
                          "bytes": len(mapping_bytes), "sha256": sha256(mapping_bytes)})
    manifest = {"manifest": "854_156383", "mapClassId": 10,
                "operation": "Reuse verified official MapAudio Vorbis and extract pinned Map10 Wwise voice/playable sounds to PCM WAV",
                "existingAudioManifest": {
                    "path": "assets/reference/official-map/audio-source-manifest.json",
                    "sha256": sha256(EXISTING_AUDIO_MANIFEST.read_bytes())},
                "storyDataSha256": sha256(STORY_DATA.read_bytes()),
                "mapDataSha256": sha256(MAP_DATA.read_bytes()),
                "officialBgmTableSha256": sha256(OFFICIAL_CONFIG_DATA.read_bytes()),
                "playableDataSha256": sha256((ROOT / "assets/official/map-playables.json").read_bytes()),
                "mapBgm": map_bgm,
                "packages": package_sources, "events": event_records,
                "outputs": sorted(output_records, key=lambda item: item["path"])}
    OUTPUT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    _write_manifest_upgrade(OUTPUT_MANIFEST,
                            (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print("Prepared Map10 audio:", len(event_records), "event media files",
          "including", len(_story_voice_events()), "dialogue voice events and",
          sum(len(item["sounds"]) for item in playables), "playable sound events")


if __name__ == "__main__":
    main()
