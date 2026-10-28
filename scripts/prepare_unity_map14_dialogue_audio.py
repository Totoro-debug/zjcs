"""Bind every original Map14 dialogue voice to a local native AudioClip."""
from __future__ import annotations

import json
import subprocess
import wave
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
OUTPUT = ROOT / "unity/Assets/Official/World/Map/14/Resources/Map14/Audio"
MANIFEST = ROOT / "assets/reference/official-map/map14-dialogue-audio-source-manifest.json"
STOP_PACKAGE = "wwisebnk_assets_wwisedata_android_zh_cn_voice_prologue_pck.rawfile"


def event_actions(raw, keys):
    sys.path.insert(0, str(ROOT / ".tools/wwiser"))
    from wwiser import wfnv
    from wwiser.parser import wparser
    target_ids = {wfnv.Fnv().get_hash(key): key for key in keys}
    result = {}
    with tempfile.TemporaryDirectory(prefix="zjcs-map14-dialogue-bank-") as temporary:
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
                    props = fields(sound) if sound is not None else []
                    property_ids = [v for n, v, _ in props if n == "pID"]
                    values = [v for n, v, _ in props if n == "pValue"]
                    if property_ids not in ([], [6]) or len(values) != len(property_ids):
                        raise ValueError("Migrate original dialogue sound properties: " + key + ":" + repr((property_ids, values)))
                    gain_db = values[0] if property_ids else 0.0
                    actions.append({"id": action_id, "actionType": read_field(action, "ulActionType"),
                                    "target": read_field(action, "idExt"),
                                    "mediaId": read_field(sound, "sourceID") if sound else None,
                                    "transitionMs": 0, "fadeCurve": read_field(action, "eFadeCurve"), "gainDb": gain_db})
                if key in result:
                    raise ValueError("Ambiguous original dialogue event: " + key)
                result[key] = {"eventId": event_id, "actions": actions}
    if set(result) != set(keys):
        raise ValueError("Missing original dialogue voice events: " + str(sorted(set(keys) - result.keys())))
    return result


def build():
    stories_path = ROOT / "unity/Assets/Game/Resources/Map14/stories.json"
    stories_raw = stories_path.read_bytes()
    stories = json.loads(stories_raw)
    story_manifest_path = ROOT / "assets/reference/official-map/map14-story-runtime-source-manifest.json"
    story_manifest = json.loads(story_manifest_path.read_bytes())
    selected_story_path = ROOT / "assets/reference/official-map/map14-source-manifest.json"
    selected_story = json.loads(selected_story_path.read_bytes())
    expected = next(row for row in story_manifest["outputs"] if row["path"] == stories_path.relative_to(ROOT).as_posix())
    if stories["manifest"] != "871_156598" or stories["mapClassId"] != 14 or expected["sha256"] != digest(stories_raw):
        raise ValueError("Dialogue audio requires the verified current Map14 story closure")
    all_keys = sorted({line["VoiceEventKey"] for story in stories["stories"] + stories["subgraphs"]
                   for group in story["dialogueGroups"] for line in group["Lines"]
                   if line["DialogType"] == "Voice" and line.get("VoiceEventKey")})
    if len(all_keys) != 401: raise ValueError("Pinned Map14 dialogue voice closure changed")
    from map14_audio_source import AudioSource
    source = AudioSource()
    from prepare_unity_map14_playable_audio import load_verified
    timeline_path=ROOT/"unity/Assets/Official/World/Map/14/Source/entity-timelines.json"
    timeline_proof=ROOT/"assets/reference/official-map/map14-entity-timelines-source-manifest.json"
    timeline=load_verified(timeline_path,timeline_proof)
    routed=timeline["timelineRoutedEvents"]
    non_direct={key for key in all_keys if not source.event_banks(key)[1]}
    if non_direct!={row["eventKey"] for row in routed}:
        raise ValueError("Inspect original non-direct dialogue voice routing")
    keys = [key for key in all_keys if key not in non_direct]
    index_path = ROOT / "assets/official/map-audio.json"
    source_path = ROOT / "assets/reference/official-map/audio-source-manifest.json"
    audio = json.loads(index_path.read_bytes())
    sources = json.loads(source_path.read_bytes())
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    apk=Apk.current()
    schemas=ScriptSchemas(code_root=ROOT/".tools/source-packages/current"/apk.version/"official-code")
    candidates=[]
    for obj in apk.objects("defaultpackage_assets_scriptableconfig_modules_story.bundle"):
        if obj.type.name=="MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName=="StorySettings":candidates.append(obj)
    if len(candidates)!=1:raise ValueError("Ambiguous current original StorySettings")
    settings_obj=candidates[0];settings_fields=schemas.read(settings_obj)
    settings=dict(source=apk.provenance(settings_obj),objectSha256=digest(settings_obj.get_raw_data()),fields=settings_fields)
    stop_key=settings_fields["mDialogVoiceStopEventKey"]
    if stop_key!="Voice_StopAll":raise ValueError("Original dialogue voice stop event changed")
    packages, resources, bindings = {}, {}, []
    for key in keys:
        files = audio["events"][key]
        if len(files) != 1:
            raise ValueError("A dialogue event does not resolve to one original voice: " + key)
        uri = files[0]
        resources[uri] = sources["resources"][uri]
    selected = OfficialSource()
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
    routing = {}
    for name, package in packages.items():
        local_keys = [key for key in keys if resources[audio["events"][key][0]]["bank"] == name]
        if name == STOP_PACKAGE:
            local_keys.append(stop_key)
        event_sources.update(event_actions(package["raw"], local_keys))
        source.objects(name)
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
    work = ROOT / ".tools/map14-audio-conversion/voices"
    work.mkdir(parents=True,exist_ok=True)
    pcm_sources = {}
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
        routing.update(source.routing(event["actions"][0]["target"]))
        wem_path = work / (str(receipt["media_id"]) + ".wem")
        wav_path = work / (str(receipt["media_id"]) + ".wav")
        wem_path.write_bytes(media)
        converter=ROOT/".tools/vgmstream/vgmstream-cli.exe"
        subprocess.run([str(converter),"-o",str(wav_path),str(wem_path)],check=True,capture_output=True)
        with wave.open(str(wav_path),"rb") as decoded:
            if decoded.getcomptype()!="NONE" or decoded.getsampwidth()!=2:raise ValueError("Unexpected original dialogue PCM")
            pcm_sources[key]=dict(channels=decoded.getnchannels(),sampleRate=decoded.getframerate(),frames=decoded.getnframes(),sampleWidth=2,pcmSha256=digest(decoded.readframes(decoded.getnframes())))
        raw=wav_path.read_bytes()
        target = OUTPUT / "VoicesPcm" / (str(receipt["media_id"]) + ".wav")
        write_generated(target, raw, previous)
        write_meta(target, "AudioImporter")
        outputs.append({"path": target.relative_to(ROOT).as_posix(), "sha256": digest(raw)})
        bindings.append({"eventKey": key, "clip": "Map14/Audio/VoicesPcm/" + str(receipt["media_id"]),
                         "sha256": digest(raw), "gainDb": event["actions"][0]["gainDb"], "pcm": pcm_sources[key]})
    mapping = OUTPUT / "voices.json"
    mapping_raw = json_bytes({"manifest": selected.version, "mapClassId": 14,
                              "stopEventKey": stop_key, "events": bindings, "timelineRoutedEvents": routed})
    write_generated(mapping, mapping_raw, previous)
    write_meta(mapping, "TextScriptImporter")
    outputs.append({"path": mapping.relative_to(ROOT).as_posix(), "sha256": digest(mapping_raw)})
    voice_directory = OUTPUT / "VoicesPcm"
    write_meta(voice_directory, "DefaultImporter")
    for path in [voice_directory, mapping] + [ROOT / value["path"] for value in outputs[:-1]]:
        meta = path.with_name(path.name + ".meta")
        if path.suffix == ".wav":
            guid = next(line for line in meta.read_text(encoding="utf-8").splitlines() if line.startswith("guid:"))
            meta.write_text("fileFormatVersion: 2\n" + guid + "\nAudioImporter:\n"
                "  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n"
                "    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n"
                "    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n"
                "    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 0\n"
                "  normalize: 0\n  preloadAudioData: 0\n  loadInBackground: 1\n"
                "  ambisonic: 0\n  3D: 0\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
        else:
            meta.write_text("\n".join(line.rstrip() for line in meta.read_text(encoding="utf-8").splitlines())
                            + "\n", encoding="utf-8")
    outputs.extend({"path": str(value["path"]) + ".meta", "sha256": digest((ROOT / (str(value["path"]) + ".meta")).read_bytes())} for value in list(outputs))
    code_paths = [ROOT / ".tools/source-packages/current" / selected.version / "official-code" / path for path in
                  ("client-source/TalkDialog.cs", "client-source/OptionTalkDialogPanel.cs",
                   "common-source/DialogInfoUtils.cs", "common-source/Common/DialogType.cs")]
    manifest = {"manifest": selected.version, "mapClassId": 14,
                "inputs": {"stories": {"path": stories_path.relative_to(ROOT).as_posix(), "sha256": digest(stories_raw)},
                           "selectedStoryProof": {"path": selected_story_path.relative_to(ROOT).as_posix(), "sha256": digest(selected_story_path.read_bytes())},
                           "audioIndexSha256": digest(index_path.read_bytes()),
                           "audioManifestSha256": digest(source_path.read_bytes()),
                           "storySettings": settings,
                           "currentBankMappingAndRoutingPackages": source.receipts(),
                           "routing": routing, "timelineRoutedEvents": routed, "pcm": pcm_sources,
                           "entityTimeline": {"path": timeline_path.relative_to(ROOT).as_posix(), "sha256": digest(timeline_path.read_bytes())},
                           "entityTimelineProof": {"path": timeline_proof.relative_to(ROOT).as_posix(), "sha256": digest(timeline_proof.read_bytes())},
                           "packages": {name: {key: value for key, value in package.items() if key != "raw"}
                                        for name, package in packages.items()},
                           "resources": resources, "events": event_sources,
                           "code": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())}
                                    for path in code_paths]},
                "operation": "Decode current-package byte-verified original WEM directly to uncompressed PCM WAV with no normalization or second lossy encoding; retain Voice-only selection, immediate Voice_StopAll order and original sound MakeUpGain in dB.",
                "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(), "sha256": digest(Path(__file__).read_bytes())},
                "tools": [{"path": p.relative_to(ROOT).as_posix(), "sha256": digest(p.read_bytes())} for p in (ROOT/"scripts/map14_audio_source.py",converter)],
                "outputs": outputs}
    manifest["validationScript"] = {"path": "unity/Assets/Game/Editor/Map14/StoryPlayables/OfficialMap14AudioAssetAcceptance.cs", "sha256": digest((ROOT/"unity/Assets/Game/Editor/Map14/StoryPlayables/OfficialMap14AudioAssetAcceptance.cs").read_bytes())}
    manifest["limitations"] = ["chapter_5_main_36_17 is performed by the original entity Timeline with two split events; it must not be replaced by a direct dialog clip.", "Static PCM/media and original routing recovered; live bus DSP, RTPC and gain verification remain pending."]
    write_generated(MANIFEST, json_bytes(manifest), previous)
    source.close()
    print("Map14 dialogue audio: %d original events, %d local clips, immediate original stop event." % (len(keys), len(resources)))


if __name__ == "__main__":
    build()
