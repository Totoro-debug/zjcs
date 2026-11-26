"""Bind current official fruit-tree story voices to the shared native dialogue UI."""
import argparse
import json
import subprocess
import tempfile
import wave
from pathlib import Path

import yaml

from official_apk import Apk, ROOT
from prepare_character_creation_opening_audio import parse_pck
from prepare_current_home_furniture_story_audio import PACKAGE, VGMSTREAM, audio_meta, sha
from prepare_unity_map11_dialogue_audio import event_actions
from prepare_unity_map13_sources import meta
from validation_workspace import validation_directory


STORY = ROOT / 'unity/Assets/Official/Home/Source/fruit-tree-unlock-stories.json'
STORY_RECEIPT = ROOT / 'assets/reference/home-fruit-tree-unlock-story-source-manifest.json'
DEST = ROOT / 'unity/Assets/Official/Home/FruitTreeStoryAudio/Resources/Home/FruitTreeStoryAudio'
ASSET = ROOT / 'unity/Assets/Game/Resources/Home/HomeFruitTreeStoryAssets.asset'
RECEIPT = ROOT / 'assets/reference/home-fruit-tree-unlock-story-audio-source-manifest.json'
BASE_ASSET = ROOT / 'unity/Assets/Game/Resources/Home/HomeStoryDialogAssets.asset'


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    if sha(STORY.read_bytes()) != json.loads(STORY_RECEIPT.read_bytes())['output']['sha256']:
        raise ValueError('Original fruit-tree story source differs from its receipt')
    story = json.loads(STORY.read_bytes())
    rows = [row for groups in story['dialogues'].values() for group in groups.values() for row in group]
    keys = sorted({row['VoiceEventKey'] for row in rows if row['DialogType'] == 1})
    if not keys or any(not key for key in keys):
        raise ValueError('Original voice dialogue has missing event keys')
    apk = Apk.current()
    try:
        package = apk.reader.read('WwiseBnk', PACKAGE)
        version = apk.version
    finally:
        apk.reader.close()
    banks, files = parse_pck(package.data)
    media = {row[0]: row for row in files}
    if len(media) != len(files):
        raise ValueError('Duplicate original Wwise media IDs')
    previous_temp = tempfile.tempdir
    tempfile.tempdir = str(work)
    try:
        events = event_actions(package.data, keys)
    finally:
        tempfile.tempdir = previous_temp
    entries = []
    for key in keys:
        actions = events[key]['actions']
        if len(actions) != 1 or actions[0]['actionType'] != 1027 or actions[0]['mediaId'] not in media:
            raise ValueError('Original event is not a unique playable voice: ' + key)
        media_id = actions[0]['mediaId']
        _, _, size, offset, _ = media[media_id]
        raw = package.data[offset:offset + size]
        wem = work / (str(media_id) + '.wem')
        wav = work / (str(media_id) + '.wav')
        wem.write_bytes(raw)
        subprocess.run([str(VGMSTREAM), '-o', str(wav), str(wem)], check=True, capture_output=True,
                       creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        with wave.open(str(wav), 'rb') as audio:
            if audio.getcomptype() != 'NONE' or audio.getsampwidth() not in (2, 3, 4) or audio.getnframes() == 0:
                raise ValueError('Decoded original voice is not non-empty PCM: ' + key)
            info = dict(channels=audio.getnchannels(), sample_rate=audio.getframerate(),
                        sample_width=audio.getsampwidth(), frames=audio.getnframes())
        entries.append(dict(eventKey=key, media_id=media_id, event=events[key], offset=offset,
            bytes=size, wem_sha256=sha(raw), wav_sha256=sha(wav.read_bytes()), format=info))
    inputs = [Path(__file__), ROOT / 'scripts/prepare_current_home_furniture_story_audio.py',
              ROOT / 'scripts/prepare_character_creation_opening_audio.py',
              ROOT / 'scripts/prepare_unity_map11_dialogue_audio.py', VGMSTREAM, STORY, STORY_RECEIPT,
              BASE_ASSET, Path(str(STORY) + '.meta'),
              ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs',
              ROOT / '.tools/source-packages/current' / version / 'official-code/framework-firstpass-source/PopupWindow.cs']
    proof = dict(manifest=version, package=dict(name=PACKAGE, source=dict(package.provenance),
        sha256=sha(package.data), bytes=len(package.data)), entries=entries,
        inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in inputs},
        scope='Native dialogue source and voices only; full city unlock-story execution requires separate integration.')
    (work / RECEIPT.name).write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered original fruit-tree dialogue voices:', len(entries))


def install(work):
    proof = json.loads((work / RECEIPT.name).read_bytes())
    for name, digest in proof['inputs'].items():
        if sha((ROOT / name).read_bytes()) != digest:
            raise ValueError('Original voice binding input changed: ' + name)
    if ASSET.exists() or RECEIPT.exists():
        if not RECEIPT.exists():
            raise ValueError('Existing fruit-tree dialogue asset has no receipt')
        for name, digest in json.loads(RECEIPT.read_bytes())['outputs'].items():
            if sha((ROOT / name).read_bytes()) != digest:
                raise ValueError('Existing fruit-tree dialogue assets differ: ' + name)
    DEST.mkdir(parents=True, exist_ok=True)
    outputs = {}
    for folder in (DEST.parents[2], DEST.parents[1], DEST.parent, DEST):
        meta(folder, folder=True)
        path = Path(str(folder) + '.meta')
        outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    voices = []
    for row in proof['entries']:
        source = work / (str(row['media_id']) + '.wav')
        if sha(source.read_bytes()) != row['wav_sha256']:
            raise ValueError('Decoded original voice changed: ' + row['eventKey'])
        target = DEST / source.name
        target.write_bytes(source.read_bytes())
        meta_path = audio_meta(target)
        for path in (target, meta_path):
            outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        voices.append(dict(EventKey=row['eventKey'], Clip=dict(fileID=8300000,
            guid=yaml.safe_load(meta_path.read_bytes())['guid'], type=3)))
    base = BASE_ASSET.read_text('utf8')
    prefix, body = base.split('--- !u!114 &11400000\n', 1)
    data = yaml.safe_load(body)
    data['MonoBehaviour']['m_Name'] = 'HomeFruitTreeStoryAssets'
    data['MonoBehaviour']['StorySource'] = dict(fileID=4900000,
        guid=yaml.safe_load(Path(str(STORY) + '.meta').read_bytes())['guid'], type=3)
    data['MonoBehaviour']['Voices'] = voices
    ASSET.write_text(prefix + '--- !u!114 &11400000\n' + yaml.safe_dump(data, allow_unicode=True, sort_keys=False),
                     'utf8', newline='\n')
    asset_meta = Path(str(ASSET) + '.meta')
    if not asset_meta.exists():
        meta(ASSET)
    identity = yaml.safe_load(asset_meta.read_bytes())['guid']
    asset_meta.write_text('fileFormatVersion: 2\nguid: ' + identity + '\nNativeFormatImporter:\n'
        '  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n'
        '  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
    for path in (ASSET, Path(str(ASSET) + '.meta')):
        outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof['outputs'] = outputs
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    (install if args.install else build)(validation_directory())
