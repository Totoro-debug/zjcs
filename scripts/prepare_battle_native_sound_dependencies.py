"""Verify the transitive source dependencies of the original native battle sound events.

This retains original HIRC payloads, media identities and global defaults. It does
not decode replacement PCM or claim that the Android banks run on Windows.
"""
import base64
from collections import Counter, defaultdict
import json
import os
from pathlib import Path
import struct

from map14_audio_source import AudioSource, sha
from prepare_battle_native_sound_sources import identity, values
from prepare_battle_scene_appearance_runtime import write_changed
from prepare_character_creation_opening_audio import parse_pck
from prepare_unity_map14_audio import fields

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / 'assets/reference/battle-animation/native-sound-sources-source-manifest.json'
OUTPUT = ROOT / 'assets/reference/battle-animation/native-sound-dependencies-source-manifest.json'
MINIMUM = 'wwisebnk_assets_wwisedata_android_minimum_pck.rawfile'


def closure(audio, actions):
    pending = [value for action in actions for value in values(action['fields'], 'idExt') if value]
    selected, edges, parameters = {}, [], set()
    while pending:
        identifier = pending.pop()
        if identifier in selected:
            continue
        if identifier not in audio.by_id:
            raise ValueError('Unresolved original sound dependency: ' + str(identifier))
        _, data = audio.by_id[identifier]
        kinds = values(data, 'eHircType')
        if len(kinds) != 1 or kinds[0] not in (2, 5, 7, 8, 16, 17, 21):
            raise ValueError('Inspect an unsupported original sound dependency type: ' + str(identifier))
        kind = kinds[0]
        if any(values(data, 'ulNumStateGroups')):
            raise ValueError('Inspect original state dependencies before extending this closure')
        selected[identifier] = dict(id=identifier, type=kind, fields=data,
                                   rawObjects=audio.object_payloads[identifier])
        for name, value, _ in data:
            if name in ('RTPCID', 'NonRTPC.uGameParamID') and value:
                parameters.add(value)
            follows = name in ('DirectParentID', 'OverrideBusId', 'reflectionsAuxBus',
                               'BusID', 'idDeviceShareset', 'fxID_0')
            follows |= name in ('ulChildID', 'ulPlayID') and kind == 5
            # In effects and devices fxID is a plug-in ID, not a HIRC identity.
            follows |= name == 'fxID' and kind in (2, 5, 7, 8)
            if follows and value:
                edges.append(dict(source=identifier, field=name, target=value))
                pending.append(value)
    return [selected[k] for k in sorted(selected)], edges, sorted(parameters)


def media_receipts(audio, media_ids):
    found = defaultdict(list)
    for name, (raw, _) in audio.packages.items():
        if not name.endswith('_pck.rawfile'):
            continue
        banks, files = parse_pck(raw)
        for identifier, _, size, offset, _ in files:
            if offset + size > len(raw):
                raise ValueError('Truncated original packaged media')
            if identifier in media_ids:
                found[identifier].append((raw[offset:offset + size],
                    dict(package=name, offset=offset, bytes=size)))
        for bank_id, _, bank_size, bank_offset, _ in banks:
            cursor, chunks = bank_offset, {}
            while cursor < bank_offset + bank_size:
                tag = raw[cursor:cursor + 4]
                length = struct.unpack_from('<I', raw, cursor + 4)[0]
                if cursor + 8 + length > bank_offset + bank_size:
                    raise ValueError('Truncated original bank chunk')
                if tag in chunks:
                    raise ValueError('Inspect duplicate original bank chunk: ' + repr(tag))
                chunks[tag] = (cursor + 8, length)
                cursor += 8 + length
            if b'DIDX' not in chunks:
                continue
            if b'DATA' not in chunks:
                raise ValueError('Original media index has no DATA chunk')
            index_offset, index_size = chunks[b'DIDX']
            data_offset, data_size = chunks[b'DATA']
            for identifier, relative, size in struct.iter_unpack('<III', raw[index_offset:index_offset + index_size]):
                if relative + size > data_size:
                    raise ValueError('Truncated original indexed media')
                if identifier in media_ids:
                    absolute = data_offset + relative
                    found[identifier].append((raw[absolute:absolute + size],
                        dict(package=name, bankId=bank_id, offset=absolute, bytes=size)))
    missing = sorted(media_ids - found.keys())
    if missing:
        raise ValueError('Search additional original packages for media: ' + str(missing))
    result = []
    for identifier, versions in sorted(found.items()):
        complete = max((raw for raw, _ in versions), key=len)
        if any(not complete.startswith(raw) for raw, _ in versions):
            raise ValueError('Conflicting original media identity: ' + str(identifier))
        result.append(dict(id=identifier, bytes=len(complete), sha256=sha(complete),
            sources=[dict(record, sha256=sha(raw), prefetchFragment=len(raw) < len(complete))
                     for raw, record in versions]))
    return result


def global_settings(audio):
    from wwiser.parser import wparser
    raw = audio.package(MINIMUM)
    chunks, defaults = [], {}
    for bank_id, _, size, offset, _ in parse_pck(raw)[0]:
        parser = wparser.Parser()
        parser.parse_banks([str(Path(audio.temporary.name) / (str(bank_id) + '.bnk'))])
        bank = parser.get_banks()[0]
        global_nodes = [n for n in bank.get_children() if n.get_name() == 'GlobalSettingsChunk']
        if len(global_nodes) > 1:
            raise ValueError('Duplicate global settings chunk')
        if global_nodes:
            data = fields(global_nodes[0])
            current, before = None, len(defaults)
            for name, value, _ in data:
                if name == 'RTPC_ID':
                    current = dict(id=value)
                    if value in defaults:
                        raise ValueError('Duplicate original global RTPC')
                    defaults[value] = current
                elif current is not None and name in ('fValue', 'rampType', 'fRampUp', 'fRampDown', 'eBindToBuiltInParam'):
                    current[name] = value
            if len(defaults) - before != values(data, 'ulNumParams')[0]:
                raise ValueError('Incomplete original global parameter defaults')
        cursor = offset
        while cursor < offset + size:
            tag = raw[cursor:cursor + 4]
            length = struct.unpack_from('<I', raw, cursor + 4)[0]
            if cursor + 8 + length > offset + size:
                raise ValueError('Truncated original global bank chunk')
            if tag in (b'INIT', b'STMG', b'ENVS', b'PLAT'):
                payload = raw[cursor:cursor + 8 + length]
                chunks.append(dict(tag=tag.decode('ascii'), package=MINIMUM, bankId=bank_id,
                    packageOffset=cursor, bytes=len(payload), sha256=sha(payload),
                    base64=base64.b64encode(payload).decode('ascii')))
            cursor += 8 + length
    if not defaults or sum(chunk['tag'] == 'STMG' for chunk in chunks) != 1:
        raise ValueError('Original initialization settings are unresolved')
    return chunks, defaults


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if work.parent != (ROOT / 'output/validation').resolve() or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace')
    source = json.loads(INPUT.read_bytes())
    if source['manifest'] != '884_156692':
        raise ValueError('Inspect changed sound event source version')
    with AudioSource(expected_version='884_156692', work_directory=work) as audio:
        if source['native_library']['source']['apkSha256'] != audio.selected.apk_sha256:
            raise ValueError('Changed original native library APK source')
        for name in sorted(source['packages']):
            if name.endswith('_pck.rawfile'):
                audio.objects(name)
        for name, record in source['packages'].items():
            if audio.receipts()[name]['sha256'] != record['sha256']:
                raise ValueError('Changed original sound package: ' + name)
        for action in source['actions']:
            if audio.by_id[action['id']][1] != [tuple(field) for field in action['fields']]:
                raise ValueError('Changed original ordered event action: ' + str(action['id']))
        objects, edges, parameters = closure(audio, source['actions'])
        media_ids = {value for obj in objects if obj['type'] == 2
                     for value in values(obj['fields'], 'sourceID')}
        for obj in objects:
            if obj['type'] == 2 and values(obj['fields'], 'ulPluginID') != [262145]:
                raise ValueError('Inspect an original source codec before resolving media')
        media = media_receipts(audio, media_ids)
        globals_raw, defaults = global_settings(audio)
        if set(parameters) - defaults.keys():
            raise ValueError('Unresolved original global parameter default')
        counts = Counter(obj['type'] for obj in objects)
        receipt = dict(manifest=audio.selected.version, event_input=identity(INPUT),
            native_library=source['native_library'], objects=objects, edges=edges, media=media,
            global_chunks=globals_raw, referenced_parameters=parameters,
            global_parameter_defaults=[defaults[k] for k in sorted(defaults)],
            packages=audio.receipts(), tools=[identity(Path(__file__)), identity(ROOT / 'scripts/map14_audio_source.py')],
            counts=dict(objects=len(objects), object_types=dict(sorted(counts.items())), edges=len(edges),
                        media=len(media), media_bytes=sum(row['bytes'] for row in media)),
            scope='Transitive original HIRC targets, random playlists, inheritance, buses, ducking, effects, output device, media and global RTPC defaults of the mapped native graph events.',
            limitations=['No Windows Wwise runtime has been installed or validated.',
                'Original Android bank/platform/plugin payloads are retained; Windows compatibility and native DSP output remain unverified.',
                'Selected HIRC objects have no state groups or switch containers; unused global state settings are retained as original STMG bytes.',
                'Actor-mixer sibling lists do not activate unrelated sounds; all duplicate raw actor-mixer payloads remain in the proof.',
                'This source tool does not change production playback, timing, resources or replacement PCM.'])
        write_changed(OUTPUT, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps(receipt['counts']))


if __name__ == '__main__':
    main()
