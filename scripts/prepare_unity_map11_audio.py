"""Bind Map11's original Wwise BGM media and event parameters to native Windows audio."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys
import tempfile
import zipfile

from official_cloud_cache import CloudCache
from official_source import OfficialSource
from prepare_character_creation_opening_audio import parse_pck, read_field
from prepare_unity_map10_entities import json_bytes, write_generated, write_meta

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11/Audio"
MANIFEST = ROOT / "assets/reference/official-map/map11-audio-source-manifest.json"
PACKAGE = "wwisebnk_assets_wwisedata_android_map_bgm_01_pck.rawfile"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def fields(node):
    result = []
    for child in node.get_children() or []:
        attrs = child.get_attrs()
        if "value" in attrs:
            result.append((attrs.get("name"), attrs["value"], attrs.get("valuefmt")))
        result.extend(fields(child))
    return result


def parameters(raw, event_keys):
    sys.path.insert(0, str(ROOT / ".tools/wwiser"))
    from wwiser import wfnv
    from wwiser.parser import wparser

    pending = {wfnv.Fnv().get_hash(key): key for key in event_keys}
    result, sounds = {}, {}
    with tempfile.TemporaryDirectory(prefix="zjcs-map11-audio-bank-") as temporary:
        for index, (_, _, size, offset, _) in enumerate(parse_pck(raw)[0]):
            bank_raw = raw[offset:offset + size]
            if not any(key.to_bytes(4, "little") in bank_raw for key in pending):
                continue
            bank_path = Path(temporary) / ("bank-%d.bnk" % index)
            bank_path.write_bytes(bank_raw)
            parser = wparser.Parser()
            parser.parse_banks([str(bank_path)])
            bank = parser.get_banks()[0]
            hirc = next(item for item in bank.get_children() if item.get_name() == "HircChunk")
            objects = [item for group in hirc.get_children() if group.get_name() == "listLoadedItem"
                       for item in group.get_children()]
            by_id = {read_field(item, "ulID"): item for item in objects}
            for event_id, key in pending.items():
                if event_id not in by_id:
                    continue
                action_ids = [value for name, value, _ in fields(by_id[event_id]) if name == "ulActionID"]
                if len(action_ids) != 1:
                    raise ValueError("Map11 BGM event must resolve to one action: " + key)
                action = by_id[action_ids[0]]
                props = fields(action)
                property_ids = [value for name, value, _ in props if name == "pID"]
                values = [value for name, value, _ in props if name == "pValue"]
                if property_ids != [16] or len(values) != 1 or read_field(action, "eFadeCurve") != 1:
                    raise ValueError("Map11 BGM fade layout changed: " + key)
                sound_id = read_field(action, "idExt")
                sound = by_id[sound_id]
                sound_props = fields(sound)
                if [value for name, value, _ in sound_props if name == "pID"] != [58] \
                        or [value for name, value, _ in sound_props if name == "pValue"] != [0]:
                    raise ValueError("Map11 BGM sound must retain its original infinite loop property")
                result[key] = {"eventKey": key, "eventId": event_id, "actionId": action_ids[0],
                               "actionType": read_field(action, "ulActionType"), "soundId": sound_id,
                               "transitionMs": values[0], "fadeCurve": 1}
                sounds[sound_id] = {"mediaId": read_field(sound, "sourceID"), "loopCount": 0}
    if set(result) != set(event_keys) or len(sounds) != 1:
        raise ValueError("The four Map11 BGM events must reference the same original sound")
    return result, next(iter(sounds.values()))


def build():
    selected = OfficialSource("854_156383")
    with selected.apk.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != selected.apk_sha256:
            raise ValueError("Pinned APK SHA256 mismatch")
    with zipfile.ZipFile(selected.apk) as archive:
        bundle = next(value for value in selected.manifest("WwiseBnk", archive)["bundles"]
                      if value["name"] == PACKAGE)
        entry = "assets/yoo/WwiseBnk/" + bundle["file"]
        packaged = entry in archive.namelist()
        if packaged:
            raw = archive.read(entry)
            source = {"origin": "apk_zip", "apkEntry": entry, "apkSha256": selected.apk_sha256}
        else:
            cached = CloudCache(selected.cloud_path, selected.version).read("WwiseBnk", bundle)
            if cached is None:
                raise FileNotFoundError("Pinned Map11 BGM package has not been downloaded: " + PACKAGE)
            raw, source = cached
    if len(raw) != bundle["size"] or hashlib.md5(raw).hexdigest() != bundle["hash"]:
        raise ValueError("Pinned Map11 BGM package size/MD5 mismatch")

    config_path = ROOT / "assets/official/adventure.json"
    config_raw = config_path.read_bytes()
    tables = json.loads(config_raw)["tables"]
    configs = [row for row in tables["bgm_event_key"] if row["BgmId"] == 11
               and row["IsDefault"] and row["Region"] == 0 and row["ReleaseType"] == 2]
    if len(configs) != 1:
        raise ValueError("Ambiguous Map11 BGM configuration")
    config = configs[0]
    for table in ("Map/map_area_bgm", "Map/map_region_bgm"):
        if any(row.get("MapClassId", row.get("MapClass")) == 11 for row in tables[table]):
            raise ValueError("Map11 BGM overrides require explicit bindings: " + table)
    event_keys = [config["Bgm" + action + "EventKey"] for action in ("Play", "Stop", "Pause", "Resume")]
    actions, sound = parameters(raw, event_keys)
    index_path = ROOT / "assets/official/map-audio.json"
    audio_index = json.loads(index_path.read_bytes())
    legacy_path = ROOT / "assets/reference/official-map/audio-source-manifest.json"
    legacy = json.loads(legacy_path.read_bytes())
    media_uri = "res://assets/official/map-audio/%d.ogg" % sound["mediaId"]
    if audio_index["events"][event_keys[0]] != [media_uri]:
        raise ValueError("Map11 BGM event media differs from the extracted audio index")
    media_source = legacy["resources"][media_uri]
    media_raw = (ROOT / media_uri.removeprefix("res://")).read_bytes()
    if digest(media_raw) != media_source["sha256"]:
        raise ValueError("Map11 BGM converted media SHA256 mismatch")
    wem = raw[media_source["offset"]:media_source["offset"] + media_source["bytes"]]
    if digest(wem) != media_source["input_sha256"] or digest(raw) != media_source["source"]["sha256"]:
        raise ValueError("Map11 BGM original Wwise media/package SHA256 mismatch")

    previous = {}
    if MANIFEST.exists():
        old = json.loads(MANIFEST.read_bytes())
        previous[MANIFEST.resolve()] = digest(MANIFEST.read_bytes())
        for value in old["outputs"]:
            path = ROOT / value["path"]
            if digest(path.read_bytes()) != value["sha256"]:
                raise ValueError("Changed Map11 audio output: " + str(path))
            previous[path.resolve()] = value["sha256"]
    clip_path = OUTPUT / "Map11Bgm.ogg"
    write_generated(clip_path, media_raw, previous)
    write_meta(clip_path, "AudioImporter")
    mapping = {"manifest": selected.version, "mapClassId": 11, "bgmId": 11,
               "clip": "Map11/Audio/Map11Bgm", "sha256": digest(media_raw), "loop": True,
               "play": actions[event_keys[0]], "stop": actions[event_keys[1]],
               "pause": actions[event_keys[2]], "resume": actions[event_keys[3]]}
    mapping_path = OUTPUT / "bgm.json"
    mapping_raw = json_bytes(mapping)
    write_generated(mapping_path, mapping_raw, previous)
    write_meta(mapping_path, "TextScriptImporter")
    code = [ROOT / ".tools/source-packages/official-code/client-source" / (name + ".cs")
            for name in ("MapBgmHandler", "BgmManager")]
    manifest = {"manifest": selected.version, "mapClassId": 11,
                "inputs": {"apkCheckedFirst": {"entry": entry, "packaged": packaged},
                           "package": {"name": PACKAGE, "sha256": digest(raw), "source": source},
                           "media": media_source, "sound": sound,
                           "config": {"path": config_path.relative_to(ROOT).as_posix(), "sha256": digest(config_raw)},
                           "audioIndex": {"path": index_path.relative_to(ROOT).as_posix(), "sha256": digest(index_path.read_bytes())},
                           "audioManifest": {"path": legacy_path.relative_to(ROOT).as_posix(), "sha256": digest(legacy_path.read_bytes())},
                           "code": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())} for path in code]},
                "operation": "Reuse verified original Wwise Vorbis conversion; Unity native AudioClip with original loop and Sine event fades.",
                "outputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())}
                            for path in (clip_path, mapping_path)]}
    write_generated(MANIFEST, json_bytes(manifest), previous)
    write_meta(OUTPUT, "DefaultImporter")
    for path in (OUTPUT, clip_path, mapping_path):
        meta = path.with_name(path.name + ".meta")
        meta.write_text("\n".join(line.rstrip() for line in meta.read_text(encoding="utf-8").splitlines())
                        + "\n", encoding="utf-8")
    print("Map11 BGM: original media %d, four events with Sine fades %s ms." %
          (sound["mediaId"], [actions[key]["transitionMs"] for key in event_keys]))


if __name__ == "__main__":
    build()
