"""Bind every original Map11 dialogue voice to a local native AudioClip."""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import zipfile

from official_cloud_cache import CloudCache
from official_source import OfficialSource
from prepare_character_creation_opening_audio import parse_pck, read_field
from prepare_unity_map10_entities import json_bytes, write_generated, write_meta
from prepare_unity_map11_audio import digest, fields

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11/Audio"
MANIFEST = ROOT / "assets/reference/official-map/map11-dialogue-audio-source-manifest.json"
STOP_PACKAGE = "wwisebnk_assets_wwisedata_android_zh_cn_voice_prologue_pck.rawfile"


def event_actions(raw, keys):
    sys.path.insert(0, str(ROOT / ".tools/wwiser"))
    from wwiser import wfnv
    from wwiser.parser import wparser
    target_ids = {wfnv.Fnv().get_hash(key): key for key in keys}
    result = {}
    with tempfile.TemporaryDirectory(prefix="zjcs-map11-dialogue-bank-") as temporary:
        for index, (_, _, size, offset, _) in enumerate(parse_pck(raw)[0]):
            bank = raw[offset:offset + size]
            if not any(value.to_bytes(4, "little") in bank for value in target_ids):
                continue
            path = Path(temporary) / (str(index) + ".bnk")
            path.write_bytes(bank)
            parser = wparser.Parser()
            parser.parse_banks([str(path)])
            hirc = next(value for value in parser.get_banks()[0].get_children() if value.get_name() == "HircChunk")
            objects = [value for group in hirc.get_children() if group.get_name() == "listLoadedItem"
                       for value in group.get_children()]
            by_id = {read_field(value, "ulID"): value for value in objects}
            for event_id, key in target_ids.items():
                if event_id not in by_id:
                    continue
                actions = []
                for name, action_id, _ in fields(by_id[event_id]):
                    if name != "ulActionID":
                        continue
                    action = by_id[action_id]
                    if any(name in ("pID", "pValue") for name, _, _ in fields(action)):
                        raise ValueError("Dialogue voice action has nonzero timing or random parameters: " + key)
                    sound = by_id.get(read_field(action, "idExt"))
                    if sound is not None and any(name == "pID" for name, _, _ in fields(sound)):
                        raise ValueError("Dialogue voice has additional sound properties: " + key)
                    actions.append({"id": action_id, "actionType": read_field(action, "ulActionType"),
                                    "target": read_field(action, "idExt"),
                                    "mediaId": read_field(sound, "sourceID") if sound else None,
                                    "transitionMs": 0, "fadeCurve": read_field(action, "eFadeCurve")})
                if key in result:
                    raise ValueError("Ambiguous original dialogue event: " + key)
                result[key] = {"eventId": event_id, "actions": actions}
    if set(result) != set(keys):
        raise ValueError("Missing original dialogue voice events: " + str(sorted(set(keys) - result.keys())))
    return result


def build():
    stories_path = OUTPUT.parent / "stories.json"
    stories_raw = stories_path.read_bytes()
    stories = json.loads(stories_raw)
    story_manifest_path = ROOT / "assets/reference/official-map/map11-runtime-source-manifest.json"
    story_manifest = json.loads(story_manifest_path.read_bytes())
    if stories["manifest"] != "854_156383" or stories["mapClassId"] != 11 \
            or story_manifest["outputs"]["stories.json"]["sha256"] != digest(stories_raw):
        raise ValueError("Dialogue audio requires the verified Map11 story closure")
    keys = sorted({line["VoiceEventKey"] for story in stories["stories"] + stories["subgraphs"]
                   for group in story["dialogueGroups"] for line in group["Lines"]
                   if line["DialogType"] == "Voice" and line.get("VoiceEventKey")})
    if len(keys) != 207:
        raise ValueError("Pinned Map11 dialogue voice closure changed")
    index_path = ROOT / "assets/official/map-audio.json"
    source_path = ROOT / "assets/reference/official-map/audio-source-manifest.json"
    audio = json.loads(index_path.read_bytes())
    sources = json.loads(source_path.read_bytes())
    settings_path = ROOT / "assets/reference/official-map/map11-dialogue-source-manifest.json"
    stop_key = json.loads(settings_path.read_bytes())["inputs"]["storySettings"]["fields"]["mDialogVoiceStopEventKey"]
    if stop_key != "Voice_StopAll":
        raise ValueError("Original dialogue voice stop event changed")
    packages, resources, bindings = {}, {}, []
    for key in keys:
        files = audio["events"][key]
        if len(files) != 1:
            raise ValueError("A dialogue event does not resolve to one original voice: " + key)
        uri = files[0]
        resources[uri] = sources["resources"][uri]
    selected = OfficialSource("854_156383")
    with selected.apk.open("rb") as stream:
        import hashlib
        if hashlib.file_digest(stream, "sha256").hexdigest() != selected.apk_sha256:
            raise ValueError("Pinned APK SHA256 mismatch")
    with zipfile.ZipFile(selected.apk) as archive:
        inventory = {value["name"]: value for value in selected.manifest("WwiseBnk", archive)["bundles"]}
        cloud = CloudCache(selected.cloud_path, selected.version)
        package_names = sorted({value["bank"] for value in resources.values()} | {STOP_PACKAGE})
        for name in package_names:
            bundle = inventory[name]
            entry = "assets/yoo/WwiseBnk/" + bundle["file"]
            packaged = entry in archive.namelist()
            if packaged:
                raw = archive.read(entry)
                origin = {"origin": "apk_zip", "entry": entry, "apkSha256": selected.apk_sha256}
            else:
                cached = cloud.read("WwiseBnk", bundle)
                if cached is None:
                    raise FileNotFoundError("Original voice package is not downloaded: " + name)
                raw, origin = cached
            if len(raw) != bundle["size"] or hashlib.md5(raw).hexdigest() != bundle["hash"]:
                raise ValueError("Original voice package size/MD5 mismatch: " + name)
            packages[name] = {"raw": raw, "sha256": digest(raw), "source": origin,
                              "apkCheckedFirst": {"entry": entry, "packaged": packaged}}
    event_sources = {}
    for name, package in packages.items():
        local_keys = [key for key in keys if resources[audio["events"][key][0]]["bank"] == name]
        if name == STOP_PACKAGE:
            local_keys.append(stop_key)
        event_sources.update(event_actions(package["raw"], local_keys))
    stop_actions = event_sources[stop_key]["actions"]
    if len(stop_actions) != 2 or any(value["actionType"] != 258 or value["transitionMs"] != 0
                                     for value in stop_actions):
        raise ValueError("The original dialogue stop event must immediately stop its two voice buses")
    previous = {}
    if MANIFEST.exists():
        previous[MANIFEST.resolve()] = digest(MANIFEST.read_bytes())
        for value in json.loads(MANIFEST.read_bytes())["outputs"]:
            path = ROOT / value["path"]
            if digest(path.read_bytes()) != value["sha256"]:
                raise ValueError("Changed dialogue audio output: " + str(path))
            previous[path.resolve()] = value["sha256"]
    outputs = []
    for key in keys:
        uri = audio["events"][key][0]
        receipt = resources[uri]
        package = packages[receipt["bank"]]
        media = package["raw"][receipt["offset"]:receipt["offset"] + receipt["bytes"]]
        raw = (ROOT / uri.removeprefix("res://")).read_bytes()
        if package["sha256"] != receipt["source"]["sha256"] or digest(media) != receipt["input_sha256"] \
                or digest(raw) != receipt["sha256"]:
            raise ValueError("Original dialogue media SHA256 mismatch: " + key)
        event = event_sources[key]
        if len(event["actions"]) != 1 or event["actions"][0]["actionType"] != 1027 \
                or event["actions"][0]["mediaId"] != receipt["media_id"]:
            raise ValueError("Original dialogue action does not match the extracted voice: " + key)
        target = OUTPUT / "Voices" / (str(receipt["media_id"]) + ".ogg")
        write_generated(target, raw, previous)
        write_meta(target, "AudioImporter")
        outputs.append({"path": target.relative_to(ROOT).as_posix(), "sha256": digest(raw)})
        bindings.append({"eventKey": key, "clip": "Map11/Audio/Voices/" + str(receipt["media_id"]),
                         "sha256": digest(raw)})
    mapping = OUTPUT / "voices.json"
    mapping_raw = json_bytes({"manifest": selected.version, "mapClassId": 11,
                              "stopEventKey": stop_key, "events": bindings})
    write_generated(mapping, mapping_raw, previous)
    write_meta(mapping, "TextScriptImporter")
    outputs.append({"path": mapping.relative_to(ROOT).as_posix(), "sha256": digest(mapping_raw)})
    voice_directory = OUTPUT / "Voices"
    write_meta(voice_directory, "DefaultImporter")
    for path in [voice_directory, mapping] + [ROOT / value["path"] for value in outputs[:-1]]:
        meta = path.with_name(path.name + ".meta")
        if path.suffix == ".ogg":
            guid = next(line for line in meta.read_text(encoding="utf-8").splitlines() if line.startswith("guid:"))
            meta.write_text("fileFormatVersion: 2\n" + guid + "\nAudioImporter:\n"
                "  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n"
                "    serializedVersion: 2\n    loadType: 2\n    sampleRateSetting: 0\n"
                "    sampleRateOverride: 44100\n    compressionFormat: 1\n    quality: 1\n"
                "    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 0\n"
                "  normalize: 0\n  preloadAudioData: 0\n  loadInBackground: 1\n"
                "  ambisonic: 0\n  3D: 0\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
        else:
            meta.write_text("\n".join(line.rstrip() for line in meta.read_text(encoding="utf-8").splitlines())
                            + "\n", encoding="utf-8")
    code_paths = [ROOT / ".tools/source-packages/official-code" / path for path in
                  ("client-source/TalkDialog.cs", "client-source/OptionTalkDialogPanel.cs",
                   "common-source/DialogInfoUtils.cs", "common-source/Common/DialogType.cs")]
    manifest = {"manifest": selected.version, "mapClassId": 11,
                "inputs": {"stories": {"path": stories_path.relative_to(ROOT).as_posix(), "sha256": digest(stories_raw)},
                           "audioIndexSha256": digest(index_path.read_bytes()),
                           "audioManifestSha256": digest(source_path.read_bytes()),
                           "storySettingsManifestSha256": digest(settings_path.read_bytes()),
                           "packages": {name: {key: value for key, value in package.items() if key != "raw"}
                                        for name, package in packages.items()},
                           "resources": resources, "events": event_sources,
                           "code": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())}
                                    for path in code_paths]},
                "operation": "Reuse verified original voice Vorbis media as native AudioClips; retain Voice-only selection and immediate Voice_StopAll order.",
                "outputs": outputs}
    write_generated(MANIFEST, json_bytes(manifest), previous)
    print("Map11 dialogue audio: %d original events, %d local clips, immediate original stop event." % (len(keys), len(resources)))


if __name__ == "__main__":
    build()
