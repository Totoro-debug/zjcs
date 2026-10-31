"""Convert verified non-bundle inputs to native media and lossless catalogs.

Keeps original Wwise banks, media IDs, language IDs, payloads and event routing.
PCM assets describe media only; Wwise event graphs still need module binding.
"""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
from concurrent.futures import ThreadPoolExecutor

from official_apk import ROOT
from official_source import OfficialSource
from prepare_character_creation_opening_audio import parse_pck
from verify_current_audio_sources import mapping, inspect
from validation_workspace import temporary_validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def media_entries(raw):
    # Same DIDX/DATA layout used by prepare_unity_profession_windows_audio;
    # keep this parser independent of that module's historical Unity schemas.
    chunks, cursor = {}, 0
    while cursor < len(raw):
        length = struct.unpack_from('<I', raw, cursor+4)[0]
        payload = raw[cursor+8:cursor+8+length]
        if len(payload) != length:
            raise ValueError('Truncated original bank chunk')
        chunks[raw[cursor:cursor+4]] = payload
        cursor += 8+length
    if cursor != len(raw) or len(chunks[b'DIDX']) % 12:
        raise ValueError('Unconsumed original embedded media index')
    for identity, offset, length in struct.iter_unpack('<III', chunks[b'DIDX']):
        payload = chunks[b'DATA'][offset:offset+length]
        if len(payload) != length or payload[:4] != b'RIFF':
            raise ValueError('Truncated original embedded WEM')
        yield identity, payload


def convert():
    source = OfficialSource()
    cache = ROOT/'.tools/source-packages/current'/source.version/'native-conversion'
    receipt = json.loads((cache/'source-manifest.json').read_bytes())
    output = cache/'raw-resources'
    if (cache/'raw-resources-source-manifest.json').exists():
        raise FileExistsError('Retain the completed raw resource conversion')
    output.mkdir(exist_ok=True)
    decoder = ROOT/'.tools/vgmstream/vgmstream-cli.exe'
    decoder_source = ROOT/'.tools/vgmstream/source.json'
    tools = json.loads(decoder_source.read_bytes())
    if sha(decoder.read_bytes()) != tools['files']['.tools/vgmstream/vgmstream-cli.exe']:
        raise ValueError('Audio decoder differs from its verified release receipt')
    files, packages, media = {}, [], {}

    def write(relative, raw):
        path = output/relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.read_bytes() != raw:
            raise ValueError('Ambiguous native media identity: '+relative)
        if not path.exists():
            path.write_bytes(raw)
        files[relative] = dict(path=relative, bytes=len(raw), sha256=sha(raw))
        return relative

    def wem(raw, identity):
        digest = sha(raw)
        if digest not in media:
            original = write('Audio/WEM/'+digest+'.wem', raw)
            media[digest] = dict(original=original, identities=[])
        media[digest]['identities'].append(identity)

    for row in receipt['inputs']:
        if row['format'] != 'original':
            continue
        raw = (cache/row['output']).read_bytes()
        if sha(raw) != row['decoded_sha256']:
            raise ValueError('Verified raw source changed: '+row['name'])
        record = dict(name=row['name'], source=row['source'], sha256=sha(raw))
        if raw[:4] == b'AKPK':
            banks, entries = parse_pck(raw)
            record.update(kind='wwise-package', banks=[], media=[])
            for bank_id, block, length, offset, language in banks:
                bank = raw[offset:offset+length]
                if len(bank) != length or bank[:4] != b'BKHD':
                    raise ValueError('Original bank bounds/header differ')
                path = write('Audio/Banks/'+row['name']+'/'+str(bank_id)+'-'+str(language)+'.bnk', bank)
                record['banks'].append(dict(id=bank_id, language=language, path=path, sha256=sha(bank)))
                cursor, has_media = 0, False
                while cursor < len(bank):
                    size = struct.unpack_from('<I', bank, cursor+4)[0]
                    has_media |= bank[cursor:cursor+4] == b'DIDX'
                    cursor += 8+size
                if cursor != len(bank):
                    raise ValueError('Unconsumed original bank bytes')
                if has_media:
                    for media_id, payload in media_entries(bank):
                        wem(payload, dict(package=row['name'], bank_id=bank_id, media_id=media_id, language=language))
            for media_id, block, length, offset, language in entries:
                payload = raw[offset:offset+length]
                wem(payload, dict(package=row['name'], media_id=media_id, language=language))
                record['media'].append(dict(id=media_id, language=language, sha256=sha(payload)))
            record['control_objects'] = inspect(raw)['objects']
        elif 'eventbankmapping_bytes' in row['name']:
            record.update(kind='wwise-event-routing', decoded=mapping(raw))
        elif b'ftyp' in raw[:16]:
            record.update(kind='mp4', path=write('Video/'+row['name']+'.mp4', raw))
        else:
            record.update(kind='original-text-asset', path=write('Data/'+row['name']+'.bytes', raw))
        packages.append(record)
    print('Raw packages:', len(packages), 'unique audio payloads:', len(media), flush=True)
    with temporary_validation_directory('raw-audio-') as temporary:
        work = Path(temporary)
        def decode(entry):
            digest, item = entry
            original = output/item['original']
            info = subprocess.run([str(decoder), '-m', '-I', str(original)], check=True, capture_output=True, text=True)
            metadata = json.loads(info.stdout)
            wav = work/(digest+'.wav')
            subprocess.run([str(decoder), '-i', '-L', '-W', '4', '-o', str(wav), str(original)], check=True,
                           capture_output=True, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            raw = wav.read_bytes()
            if raw[:4] != b'RIFF' or raw[8:12] != b'WAVE' or len(raw) < 44:
                raise ValueError('Decoder output is not a complete WAVE file')
            chunks, cursor = {}, 12
            while cursor < len(raw):
                length = struct.unpack_from('<I', raw, cursor+4)[0]
                chunk = raw[cursor+8:cursor+8+length]
                if len(chunk) != length:
                    raise ValueError('Truncated decoded audio chunk')
                chunks[raw[cursor:cursor+4]] = chunk
                cursor += 8+length+(length & 1)
            fmt, channels, rate, byte_rate, block, bits = struct.unpack_from('<HHIIHH', chunks[b'fmt '])
            if cursor != len(raw) or fmt != 3 or bits != 32 or not channels or not rate or len(chunks[b'data']) % block:
                raise ValueError('Decoded audio is not complete float32 PCM')
            item.update(path=write('Audio/PCM/'+digest+'.wav', raw), metadata=metadata,
                        channels=channels, sample_rate=rate, frames=len(chunks[b'data'])//block, sample_format='IEEE float32')
            wav.unlink()
        with ThreadPoolExecutor(max_workers=4) as workers:
            for index, _ in enumerate(workers.map(decode, sorted(media.items())), 1):
                if index % 100 == 0:
                    print('Decoded media:', index, '/', len(media), flush=True)
    result = dict(manifest=source.version, input_manifest_sha256=sha((cache/'source-manifest.json').read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), decoder=dict(receipt=tools, executable_sha256=sha(decoder.read_bytes())),
                  operation=__doc__, packages=packages, audio=media, outputs=list(files.values()), completed=True,
                  limitations=['Media conversion only. Original Wwise banks/event graphs retained; gain, routing, looping and module runtime binding still require their original consumers.',
                               'Existing game media and unmatched older assets retained. No Unity import or Player audio validation for this cache.'])
    (cache/'raw-resources-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Converted raw resources:', len(files), 'files', flush=True)


if __name__ == '__main__':
    convert()
