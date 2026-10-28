"""Decode the original target-confirm event from the verified minimum Wwise package."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import wave

from official_apk import Apk, ROOT
from prepare_character_creation_opening_audio import parse_pck
from prepare_unity_map12_ui_audio import audio_meta, embedded_media

PROOF = ROOT / 'assets/reference/official-map/map12-team-target-audio-source-manifest.json'
DEST = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/Sounds'
NAME, EVENT = 'Main_Cursor_Success', 2934494047


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(write=False):
    apk = Apk.current()
    if apk.version != '871_156598':
        raise ValueError('Inspect original target Wwise selection')
    input_path = ROOT / 'assets/reference/official-map/map12-ui-audio-source-manifest.json'
    inputs = json.loads(input_path.read_bytes())
    package, = [row for row in inputs['packages'] if row['name'].endswith('_minimum_pck.rawfile')]
    raw = apk.archive.read(package['source']['apk_entry'])
    if sha(raw) != package['sha256'] or len(raw) != package['current_manifest']['size'] or hashlib.md5(raw).hexdigest() != package['current_manifest']['hash']:
        raise ValueError('Original target minimum Wwise bytes differ')
    validation = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if not validation.is_relative_to((ROOT / 'output/validation').resolve()):
        raise ValueError('Use an owned validation workspace')
    stage = validation / 'team-target-audio'
    banks, txtps = stage / 'banks', stage / 'txtp'
    banks.mkdir(parents=True, exist_ok=True); txtps.mkdir(exist_ok=True)
    rows, _ = parse_pck(raw)
    source_banks = {}
    for bank_id, _, size, offset, _ in rows:
        data = raw[offset:offset + size]
        path = banks / (str(bank_id) + '.bnk')
        path.write_bytes(data)
        source_banks[bank_id] = dict(id=bank_id, offset=offset, bytes=size, sha256=sha(data))
    wwiser, decoder = ROOT / '.tools/wwiser/wwiser.py', ROOT / '.tools/vgmstream/vgmstream-cli.exe'
    command = [sys.executable, str(wwiser), *map(str, banks.glob('*.bnk')), '-g', '-d', 'none',
               '-go', str(txtps), '-gw', '../banks/', '-gv', '1.0', '-gf', str(EVENT)]
    rendered = subprocess.run(command, check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (stage / 'wwiser.log').write_bytes(rendered.stdout)
    found = []
    for path in txtps.glob('*.txtp'):
        text = path.read_text(encoding='utf-8')
        if re.search(r'CAkEvent\[\d+\] ' + str(EVENT) + r'\b', text):
            found.append((path, text))
    if len(found) != 1:
        raise ValueError('Resolve exact target-confirm event graph')
    path, text = found[0]
    leaves = [line.strip() for line in text.splitlines() if line.strip() and not line.lstrip().startswith('#')]
    if len(leaves) != 1 or 'master volume: auto' in text:
        raise ValueError('Preserve original target-confirm event playback graph')
    match = re.fullmatch(r'\.\./banks/(\d+)\.bnk #s(\d+) #i(?:  #v (-?[\d.]+)dB)?  ##(\d+)\.wem', leaves[0])
    if match is None:
        raise ValueError('Inspect original target-confirm event leaf: ' + leaves[0])
    bank_id, subsong, gain, media_id = match.groups()
    media = embedded_media((banks / (bank_id + '.bnk')).read_bytes(), int(media_id))
    if media['subsong'] != int(subsong):
        raise ValueError('Original target-confirm subsong differs')
    decoded = subprocess.run([str(decoder), '-i', '-o', str(stage / (NAME + '.wav')), str(path)],
                             check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (stage / 'decode.log').write_bytes(decoded.stdout)
    pcm = (stage / (NAME + '.wav')).read_bytes()
    with wave.open(str(stage / (NAME + '.wav')), 'rb') as audio:
        if audio.getsampwidth() != 2:
            raise ValueError('Expected native PCM16 target-confirm audio')
        info = dict(channels=audio.getnchannels(), frames=audio.getnframes(), sample_rate=audio.getframerate())
    target = DEST / (NAME + '.wav')
    outputs = {target: pcm, target.with_suffix('.wav.meta'): audio_meta(NAME),
        DEST.with_name(DEST.name + '.meta'): ('fileFormatVersion: 2\nguid: ' + sha(b'zjcs:map12:team-target-sounds')[:32] + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()}
    value = dict(format=1, manifest=apk.version, package=package, bank=source_banks[int(bank_id)],
        media=media, name=NAME, id=EVENT, gain_db=float(gain or 0), loop=False, txtp=text, audio=info,
        input=dict(path=input_path.relative_to(ROOT).as_posix(), sha256=sha(input_path.read_bytes())),
        tools=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in (wwiser, decoder, Path(__file__).resolve())],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(data)) for p, data in outputs.items()],
        operation='Decode the exact original target-confirm event graph and event gain to PCM16 without normalization.')
    for path, data in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        elif not path.is_file() or path.read_bytes() != data:
            raise ValueError('Original target-confirm PCM drift')
    raw = (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
    if write:
        PROOF.write_bytes(raw)
    elif not PROOF.is_file() or PROOF.read_bytes() != raw:
        raise ValueError('Original target-confirm audio receipt drift')
    print('Original target-confirm Wwise graph/PCM verified.', flush=True)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
