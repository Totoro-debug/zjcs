"""Decode Map12 UI events from the selected original Wwise banks without gain normalization."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT / '.tools/character-art-python'), str(ROOT / 'scripts')]
from official_apk import Apk
from official_source import OfficialSource
from official_unity_schema import ScriptSchemas
from prepare_character_creation_opening_audio import parse_pck
from prepare_dungeon_ui_bindings import CODE, VERSION, relative_transform_path

DEST = ROOT / 'unity/Assets/Official/World/Map/12/Resources/Map12/PopupSound'
RECEIPT = ROOT / 'assets/reference/official-map/map12-ui-audio-source-manifest.json'
CACHE = ROOT / '.tools/map12-ui-audio-871_156598'
EVENTS = {'Main_Tag': 680474747, 'Main_Cursor_Normal': 3772241779, 'Main_Cursor_Back': 3686182067,
          'Main_Cursor_Fail': 2458796094, 'TeamMatch_Waiting_1': 1386996041}
PACKAGES = ('minimum', 'common_002_ui', 'ui_sfx_01', 'ui_bgm_01', 'ui_bgm_02')

def sha(data): return hashlib.sha256(data).hexdigest()
def encoded(value): return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf8')
def write(path, data, update):
    old = path.read_bytes() if path.exists() else None
    if old is not None and old != data and not update:
        raise ValueError('Refusing to replace changed official UI audio output: ' + str(path))
    if old != data:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

def event_sources(apk):
    from UnityPy.classes.PPtr import PPtr
    schemas = ScriptSchemas(code_root=CODE)
    root = apk.asset('prefabs_ui_dailydungeon', '/DailyDungeonDialogPrivateMap.prefab').read()
    result = []
    for go, indices, names, _ in relative_transform_path(root):
        for pair in go.m_Component:
            obj = pair.component.deref()
            if obj.type.name != 'MonoBehaviour': continue
            cls = obj.read(check_read=False).m_Script.read().m_ClassName
            keys = {'UIClickSound': ('AkEvent',), 'ButtonSound': ('ClickEvent',),
                    'PopupWindowSound': ('ShowSound', 'HideSound'), 'AnimationSoundPlayer': ('mEvents',)}.get(cls)
            if keys is None: continue
            fields = schemas.read(obj)
            for key in keys:
                values = fields[key] if key == 'mEvents' else [fields[key]]
                for value in values:
                    if value['idInternal'] or value['valueGuidInternal']: raise ValueError('Unverified inline Wwise event identity')
                    ptr = value['WwiseObjectReference']
                    if not ptr['m_PathID']: continue
                    ref = PPtr(**ptr, assetsfile=obj.assets_file).deref()
                    data = schemas.read(ref)
                    if EVENTS.get(data['objectName']) != data['id']: raise ValueError('Original private UI event identity changed')
                    result.append(dict(name=data['objectName'], id=data['id'], field=key,
                        path='/'.join(map(str, indices)), objects=list(names), component=cls,
                        component_source=apk.provenance(obj), component_sha256=sha(obj.get_raw_data()),
                        reference_source=apk.provenance(ref), reference_sha256=sha(ref.get_raw_data())))
    if {row['name'] for row in result} != set(EVENTS): raise ValueError('Original UI event set is incomplete')
    return result

def embedded_media(raw, media_id):
    cursor = 0; index = None; data = None
    while cursor + 8 <= len(raw):
        kind = raw[cursor:cursor+4]; size = struct.unpack_from('<I', raw, cursor+4)[0]
        if cursor + 8 + size > len(raw): raise ValueError('Truncated original Wwise bank chunk')
        if kind == b'DIDX': index = raw[cursor+8:cursor+8+size]
        if kind == b'DATA': data = (cursor+8, size)
        cursor += 8 + size
    if index is None or data is None or len(index) % 12: raise ValueError('Original UI bank has no complete media index')
    for number in range(len(index)//12):
        ident, offset, size = struct.unpack_from('<III', index, number*12)
        if ident != media_id: continue
        if offset + size > data[1]: raise ValueError('Original embedded media range is incomplete')
        start = data[0] + offset
        return dict(id=ident, subsong=number+1, offset=start, bytes=size, sha256=sha(raw[start:start+size]))
    raise ValueError('Original embedded UI media was not found')

def audio_meta(name):
    seed = b'zjcs-map12-private-popup-back-audio' if name == 'Main_Cursor_Back' else ('zjcs-map12-private-ui-sound:' + name).encode()
    guid = sha(seed)[:32]
    return (f'fileFormatVersion: 2\nguid: {guid}\nAudioImporter:\n  externalObjects: {{}}\n  serializedVersion: 7\n'
        '  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n'
        '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 0\n'
        '  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n'
        '  3D: 1\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode('ascii')

def build(update=False, stage=False):
    apk = Apk.current()
    if apk.version != VERSION: raise ValueError('Unexpected selected Map12 UI audio version')
    witnesses = event_sources(apk)
    index = {row['name']: row for row in OfficialSource().manifest('WwiseBnk')['bundles']}
    bank_dir = ROOT / '.tools/source-packages/WwiseBnk'
    cached_sources = json.loads((bank_dir / 'bundle-sources.json').read_bytes())
    packages = []; banks = {}; outputs = []
    bank_cache = CACHE / 'banks'; bank_cache.mkdir(parents=True, exist_ok=True)
    for stem in PACKAGES:
        key = 'wwisebnk_assets_wwisedata_android_' + stem + '_pck.rawfile'
        current = index[key]; entry = 'assets/yoo/WwiseBnk/' + current['file']
        packaged = entry in apk.archive.namelist()
        raw = apk.archive.read(entry) if packaged else (bank_dir/key).read_bytes()
        if len(raw) != current['size'] or hashlib.md5(raw).hexdigest() != current['hash']:
            raise ValueError('Original selected UI audio package mismatch: ' + key)
        packages.append(dict(name=key, current_manifest=current, sha256=sha(raw),
            source=dict(origin='apk_zip', apk_entry=entry, apk_manifest='854_156383') if packaged else cached_sources[key]))
        rows, _ = parse_pck(raw)
        for bank_id, _, size, offset, _ in rows:
            data = raw[offset:offset+size]; path = bank_cache/(str(bank_id)+'.bnk')
            if bank_id in banks and banks[bank_id]['sha256'] != sha(data): raise ValueError('Original bank ID collision')
            write(path, data, update)
            banks[bank_id] = dict(id=bank_id, path=path.relative_to(ROOT).as_posix(), package=key,
                offset=offset, bytes=size, sha256=sha(data))
    wwiser = ROOT / '.tools/wwiser/wwiser.py'; decoder = ROOT / '.tools/vgmstream/vgmstream-cli.exe'
    txtps = CACHE / 'txtp'; pcm = CACHE / 'pcm'; txtps.mkdir(exist_ok=True); pcm.mkdir(exist_ok=True)
    # Linear master gain 1.0 is 0 dB; "*" would normalize away original event attenuation.
    command = [sys.executable, str(wwiser), *[str(ROOT/row['path']) for row in banks.values()],
        '-g', '-d', 'none', '-go', str(txtps), '-gw', '../banks/', '-gv', '1.0', '-gf', *map(str, EVENTS.values())]
    result = subprocess.run(command, check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (CACHE/'wwiser.log').write_bytes(result.stdout)
    rendered = {}
    for path in sorted(txtps.glob('*.txtp')):
        text = path.read_text(encoding='utf8')
        match = re.search(r'CAkEvent\[\d+\] (\d+)', text)
        if match is None: raise ValueError('Generated UI TXTP has no original event identity')
        event_id = int(match.group(1)); name = next((n for n, ident in EVENTS.items() if ident == event_id), None)
        if name is None or name in rendered or 'master volume: auto' in text: raise ValueError('Ambiguous or normalized UI event rendering')
        leaves = [line for line in text.splitlines() if line.strip() and not line.lstrip().startswith('#')]
        if len(leaves) != 1 or not re.search(r'#[i]\b', leaves[0]): raise ValueError('UI audio event needs additional playback migration')
        leaf = re.fullmatch(r'\.\./banks/(\d+)\.bnk #s(\d+) #i(?:  #v (-?[\d.]+)dB)?  ##(\d+)\.wem', leaves[0].strip())
        if leaf is None: raise ValueError('Unexpected original UI event playback graph: ' + leaves[0])
        bank_id, subsong, gain, media_id = leaf.groups(); bank = banks[int(bank_id)]
        media = embedded_media((ROOT/bank['path']).read_bytes(), int(media_id))
        if media['subsong'] != int(subsong): raise ValueError('Original source media/subsong mismatch')
        target = pcm/(name+'.wav')
        decoded = subprocess.run([str(decoder), '-i', '-o', str(target), str(path)], check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (CACHE/(name+'.decode.log')).write_bytes(decoded.stdout)
        payload = target.read_bytes()
        with wave.open(str(target), 'rb') as audio:
            if audio.getsampwidth() != 2: raise ValueError('Expected original event PCM16 decoding')
            audio_info = dict(frames=audio.getnframes(), channels=audio.getnchannels(), sample_rate=audio.getframerate(), sample_width=audio.getsampwidth())
        native = DEST/(name+'.wav'); metadata = native.with_name(native.name+'.meta')
        for output, data in ((native, payload), (metadata, audio_meta(name))):
            target_output = CACHE/'staged'/output.relative_to(ROOT) if stage else output
            write(target_output, data, update)
            outputs.append(dict(path=output.relative_to(ROOT).as_posix(), sha256=sha(data), bytes=len(data)))
        rendered[name] = dict(name=name, id=event_id, resource='Map12/PopupSound/'+name, path=native.relative_to(ROOT).as_posix(),
            sha256=sha(payload), loop=False, gain_db=float(gain or 0), audio=audio_info,
            action_id=int(re.search(r'CAkActionPlay\[\d+\] (\d+)', text).group(1)),
            sound_id=int(re.search(r'CAkSound\[\d+\] (\d+)', text).group(1)), bank=bank, media=media,
            txtp=text, txtp_sha256=sha(path.read_bytes()))
    if set(rendered) != set(EVENTS): raise ValueError('Original UI event render set is incomplete')
    folder_meta = DEST.with_name(DEST.name+'.meta')
    if not folder_meta.exists(): raise ValueError('Original popup sound resource folder metadata is missing')
    outputs.append(dict(path=folder_meta.relative_to(ROOT).as_posix(), sha256=sha(folder_meta.read_bytes()), bytes=folder_meta.stat().st_size))
    tool_paths = [wwiser, ROOT/'.tools/wwiser/wwiser/generator/wtxtp_cache.py',
        ROOT/'.tools/wwiser/wwiser/generator/txtp/wtxtp_simplifier.py', decoder]
    receipt = dict(manifest=VERSION, mapClassId=12, operation='Original Wwise Event/Play/Sound graph at fixed 0 dB master; preserve event gain, media, loop and PCM; no loudness normalization or re-encoding.',
        source_components=witnesses, packages=packages, events=[rendered[name] for name in sorted(rendered)],
        tools=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in tool_paths],
        outputs=sorted(outputs, key=lambda row: row['path']))
    write(CACHE/'staged-source-manifest.json' if stage else RECEIPT, encoded(receipt), update)
    print(json.dumps(dict(events={name: dict(gain_db=row['gain_db'],audio=row['audio']) for name,row in rendered.items()}, receipt=RECEIPT.relative_to(ROOT).as_posix()), ensure_ascii=False))

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--update', action='store_true');parser.add_argument('--stage', action='store_true')
    args = parser.parse_args(); build(args.update, args.stage)
