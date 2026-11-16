"""Recover selected home furniture story voices from the pinned Wwise package."""
import argparse
import hashlib
import json
import subprocess
import tempfile
import wave
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_character_creation_opening_audio import parse_pck
from prepare_unity_map11_dialogue_audio import event_actions
from prepare_unity_map13_sources import encode, meta
from validation_workspace import validation_directory


VERSION = '884_156692'
PACKAGE = 'wwisebnk_assets_wwisedata_android_zh_cn_voice_chapter_02_pck.rawfile'
STORY_SOURCE = ROOT / 'unity/Assets/Official/Home/Source/furniture-unlock-stories.json'
STORY_RECEIPT = ROOT / 'assets/reference/home-furniture-unlock-story-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Home/FurnitureStoryAudio/Resources/Home/FurnitureStoryAudio'
RECEIPT = ROOT / 'assets/reference/home-furniture-unlock-story-audio-source-manifest.json'
VGMSTREAM = ROOT / '.tools/vgmstream/vgmstream-cli.exe'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    story_raw = STORY_SOURCE.read_bytes()
    story_proof_raw = STORY_RECEIPT.read_bytes()
    story_proof = json.loads(story_proof_raw)
    if story_proof['manifest'] != VERSION or sha(story_raw) != story_proof['output']['sha256']:
        raise ValueError('Selected furniture story source does not match its manifest')
    story = json.loads(story_raw)
    rows = [row for groups in story['dialogues'].values() for group in groups.values() for row in group]
    if any(not row['VoiceEventKey'] for row in rows if row['DialogType'] == 1):
        raise ValueError('An original Voice dialogue row has no event key')
    keys = sorted({row['VoiceEventKey'] for row in rows if row['DialogType'] == 1})
    if len(keys) != 13:
        raise ValueError('Selected furniture story voice closure changed')
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError('Voice package differs from the selected source version')
    verified = apk.reader.read('WwiseBnk', PACKAGE)
    package = verified.data
    banks, files = parse_pck(package)
    by_id = {row[0]: row for row in files}
    if len(by_id) != len(files):
        raise ValueError('Original voice package contains duplicate media IDs')
    previous_temp = tempfile.tempdir
    tempfile.tempdir = str(work)
    try:
        events = event_actions(package, keys)
    finally:
        tempfile.tempdir = previous_temp
    target = work / 'home-furniture-story-voices'
    target.mkdir(exist_ok=True)
    entries = []
    for key in keys:
        actions = events[key]['actions']
        if len(actions) != 1 or actions[0]['actionType'] != 1027 or actions[0]['mediaId'] not in by_id:
            raise ValueError('Original dialogue event is not a unique playable voice: ' + key)
        media_id = actions[0]['mediaId']
        _, _, size, offset, _ = by_id[media_id]
        wem = package[offset:offset + size]
        wem_path = work / ('voice-' + str(media_id) + '.wem')
        wav_path = target / (str(media_id) + '.wav')
        wem_path.write_bytes(wem)
        subprocess.run([str(VGMSTREAM), '-o', str(wav_path), str(wem_path)],
            check=True, capture_output=True, text=True,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        with wave.open(str(wav_path), 'rb') as audio:
            if audio.getcomptype() != 'NONE' or audio.getsampwidth() not in (2, 3, 4):
                raise ValueError('Converted original voice is not supported PCM: ' + key)
            format_info = dict(channels=audio.getnchannels(), sample_rate=audio.getframerate(),
                               sample_width=audio.getsampwidth(), frames=audio.getnframes())
        wav = wav_path.read_bytes()
        entries.append(dict(eventKey=key, clip='Home/FurnitureStoryAudio/' + str(media_id),
            media_id=media_id, offset=offset, bytes=size, wem_sha256=sha(wem),
            wav_sha256=sha(wav), wav_bytes=len(wav), format=format_info, event=events[key]))
        wem_path.unlink()
    catalog = encode(dict(manifest=VERSION, events=[dict(eventKey=row['eventKey'], clip=row['clip'],
                                                        sha256=row['wav_sha256']) for row in entries]))
    (target / 'voices.json').write_bytes(catalog)
    source_files = [Path(__file__), ROOT / 'scripts/prepare_character_creation_opening_audio.py',
        ROOT / 'scripts/prepare_unity_map11_dialogue_audio.py', VGMSTREAM, STORY_SOURCE, STORY_RECEIPT]
    proof = dict(manifest=VERSION, package=dict(name=PACKAGE, source=dict(verified.provenance),
        sha256=sha(package), bytes=len(package), bank_count=len(banks), media_count=len(files)),
        inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in source_files},
        entries=entries, catalog_sha256=sha(catalog),
        conversion='Original Wwise media decoded to PCM WAV by the recorded vgmstream executable.',
        scope='Native local voices only; original dialogue UI and audio lifecycle are not yet connected.')
    (work / 'home-furniture-story-voices-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered selected furniture story voices:', len(entries), flush=True)


def audio_meta(path):
    identity = path.relative_to(ROOT / 'unity').as_posix()
    guid = hashlib.md5(('zjcs-official:' + identity).encode()).hexdigest()
    raw = ('fileFormatVersion: 2\nguid: ' + guid + '\nAudioImporter:\n'
        '  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n'
        '    serializedVersion: 2\n    loadType: 2\n    sampleRateSetting: 0\n'
        '    sampleRateOverride: 44100\n    compressionFormat: 1\n    quality: 1\n'
        '    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 0\n'
        '  normalize: 0\n  preloadAudioData: 0\n  loadInBackground: 1\n'
        '  ambisonic: 0\n  3D: 0\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    target = Path(str(path) + '.meta')
    if target.exists() and target.read_bytes() != raw:
        raise ValueError('Refusing to replace unrelated voice metadata: ' + str(target))
    target.write_bytes(raw)
    return target


def install(work):
    proof = json.loads((work / 'home-furniture-story-voices-manifest.json').read_bytes())
    staged = work / 'home-furniture-story-voices'
    for relative, expected in proof['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Voice source changed: ' + relative)
    DESTINATION.mkdir(parents=True, exist_ok=True)
    folders = (DESTINATION.parents[2], DESTINATION.parents[1], DESTINATION.parent, DESTINATION)
    for folder in folders:
        meta(folder, folder=True)
    outputs = []
    for row in proof['entries']:
        source = staged / (str(row['media_id']) + '.wav')
        raw = source.read_bytes()
        if sha(raw) != row['wav_sha256']:
            raise ValueError('Staged voice changed: ' + row['eventKey'])
        target = DESTINATION / source.name
        if target.exists() and sha(target.read_bytes()) != row['wav_sha256']:
            raise ValueError('Refusing to replace unrelated original voice: ' + str(target))
        target.write_bytes(raw)
        meta_path = audio_meta(target)
        outputs.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(raw),
                            meta_sha256=sha(meta_path.read_bytes())))
    source = staged / 'voices.json'
    raw = source.read_bytes()
    if sha(raw) != proof['catalog_sha256']:
        raise ValueError('Staged voice catalog changed')
    target = DESTINATION / source.name
    if target.exists() and sha(target.read_bytes()) != proof['catalog_sha256']:
        raise ValueError('Refusing to replace an unrelated voice catalog')
    target.write_bytes(raw)
    meta(target)
    outputs.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(raw),
                        meta_sha256=sha(Path(str(target) + '.meta').read_bytes())))
    proof['outputs'] = outputs
    proof['folder_meta_sha256'] = {Path(str(folder) + '.meta').relative_to(ROOT).as_posix():
        sha(Path(str(folder) + '.meta').read_bytes()) for folder in folders}
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    (install if args.install else build)(validation_directory())
