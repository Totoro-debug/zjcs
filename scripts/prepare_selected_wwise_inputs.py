"""Extract selected verified Wwise packages for their original event consumers."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_bundle_reader import OfficialBundleReader
from official_source import OfficialSource
from prepare_character_creation_opening_audio import parse_pck
from official_wwise_reader import inspect
from convert_current_raw_resources import media_entries


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare(destination, names):
    source = OfficialSource()
    destination = destination.resolve()
    destination.relative_to(ROOT / '.tools/source-packages/current' / source.version)
    names = sorted(set(names))
    if not names:
        raise ValueError('Select explicit official Wwise packages')
    manifest = source.manifest('WwiseBnk')
    records = {row['name']: row for row in manifest['bundles']}
    receipt = destination / 'raw-resources-source-manifest.json'
    if receipt.exists():
        prior = json.loads(receipt.read_bytes())
        if prior['manifest'] != source.version or prior['selection'] != names or not prior['completed']:
            raise ValueError('Preserve a different selected Wwise cache')
        for row in prior['packages']:
            if row['manifest_record'] != records[row['name']]:
                raise ValueError('Selected Wwise package identity differs')
        for row in prior['outputs']:
            if sha((destination / row['path']).read_bytes()) != row['sha256']:
                raise ValueError('Selected Wwise cache payload differs: ' + row['path'])
        return destination
    destination.mkdir(parents=True, exist_ok=True)
    outputs, packages, media = [], [], {}

    def write(relative, raw):
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.read_bytes() != raw:
            raise ValueError('Preserve an existing Wwise cache payload: ' + str(path))
        if not path.exists():
            path.write_bytes(raw)
        outputs.append(dict(path=relative, sha256=sha(raw), bytes=len(raw)))

    def wem(payload, identity):
        digest = sha(payload)
        if digest not in media:
            relative = 'Audio/WEM/' + digest + '.wem'
            write('raw-resources/' + relative, payload)
            media[digest] = dict(original=relative, identities=[])
        media[digest]['identities'].append(identity)

    with OfficialBundleReader(source) as reader:
        for name in names:
            record = records[name]
            verified = reader.read('WwiseBnk', name)
            raw = verified.data
            if raw[:4] != b'AKPK':
                raise ValueError('Selected original Wwise input is not a package')
            relative = 'inputs/WwiseBnk/' + name
            write(relative, raw)
            banks, entries = parse_pck(raw)
            package = dict(name=name, source=dict(verified.provenance), sha256=sha(raw),
                manifest_record=record, input=relative, kind='wwise-package', banks=[], media=[])
            for bank_id, block, length, offset, language in banks:
                bank = raw[offset:offset + length]
                if len(bank) != length or bank[:4] != b'BKHD':
                    raise ValueError('Selected original Wwise bank bounds differ')
                target = 'Audio/Banks/' + name + '/' + str(bank_id) + '-' + str(language) + '.bnk'
                write('raw-resources/' + target, bank)
                package['banks'].append(dict(id=bank_id, language=language, path=target, sha256=sha(bank)))
                cursor, has_media = 0, False
                while cursor < len(bank):
                    length = int.from_bytes(bank[cursor + 4:cursor + 8], 'little')
                    has_media |= bank[cursor:cursor + 4] == b'DIDX'
                    cursor += 8 + length
                if cursor != len(bank):
                    raise ValueError('Original Wwise bank chunks do not close')
                if has_media:
                    for media_id, payload in media_entries(bank):
                        wem(payload, dict(package=name, bank_id=bank_id, media_id=media_id, language=language))
            for media_id, block, length, offset, language in entries:
                payload = raw[offset:offset + length]
                if len(payload) != length:
                    raise ValueError('Selected original Wwise media bounds differ')
                wem(payload, dict(package=name, media_id=media_id, language=language))
                package['media'].append(dict(id=media_id, language=language, sha256=sha(payload)))
            package['control_objects'] = inspect(raw)['objects']
            packages.append(package)
    tools = [Path(__file__), ROOT / 'scripts/prepare_character_creation_opening_audio.py',
        ROOT / 'scripts/official_wwise_reader.py', ROOT / 'scripts/convert_current_raw_resources.py']
    result = dict(manifest=source.version, selection=names, packages=packages, audio=media,
        outputs=outputs, completed=True, tools={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in tools},
        operation=__doc__, scope='Selected package/bank/WEM extraction only; event graphs, gain and PCM decoding belong to their original event consumer.')
    staged = destination / 'raw-resources-source-manifest.pending.json'
    staged.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    staged.replace(receipt)
    return destination
