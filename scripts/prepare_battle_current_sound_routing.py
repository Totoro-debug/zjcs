"""Bind current official shared Wwise routing without relabelling older media.

Run after the base Windows sound conversion. Every reused event bank and media
must remain identical; only the independently verified Minimum package changes.
"""
import argparse
import json
import re
from pathlib import Path

from convert_wwise_android_package import convert_android_package_to_windows
from map14_audio_source import AudioSource, sha
from official_source import OfficialSource
from prepare_battle_native_sound_dependencies import closure, global_settings, media_receipts, MINIMUM
from prepare_battle_native_sound_sources import identity, values

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'assets/reference/battle-animation/windows-sound-runtime-source-manifest.json'
EVENTS = ROOT / 'assets/reference/battle-animation/native-sound-sources-source-manifest.json'
DEPENDENCIES = ROOT / 'assets/reference/battle-animation/native-sound-dependencies-source-manifest.json'
OUTPUT = ROOT / 'assets/reference/battle-animation/windows-sound-routing-source-manifest.json'
PACKAGE = ROOT / 'unity/Assets/Official/Combat/NativeSound/Packages/Minimum.pck'


def prepare(work, event_names, native_sources=()):
    work = work.resolve()
    if work.parent != (ROOT / 'output/validation').resolve() or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace')
    base = json.loads(BASE.read_bytes())
    events = json.loads(EVENTS.read_bytes())
    dependencies = json.loads(DEPENDENCIES.read_bytes())
    previous_output = next(row for row in base['outputs'] if row['path'] == PACKAGE.relative_to(ROOT).as_posix())
    previous = json.loads(OUTPUT.read_bytes()) if OUTPUT.exists() else None
    allowed = [previous_output]
    if previous:
        if previous['base_source'] != identity(BASE):
            raise ValueError('Inspect changed base sound conversion before replacing its routing')
        allowed.extend(row['output'] for row in previous['overrides'])
    current = identity(PACKAGE)
    if current not in allowed:
        raise ValueError('Preserve unrecognized current Minimum package')
    by_event = {row['name']: row for row in events['events']}
    native_receipts = []
    for path in native_sources:
        path = path.resolve()
        proof = json.loads(path.read_bytes())
        if proof['manifest'] != OfficialSource().version:
            raise ValueError('Inspect changed native event source version')
        native_receipts.append(identity(path))
        for row in proof['outputs']:
            if '/Wwise/ScriptableObjects/Event/' not in row['path'] or not row['path'].endswith('.asset'):
                continue
            asset = ROOT / row['path']
            asset_identity = identity(asset)
            if any(asset_identity[key] != value for key, value in row.items()):
                raise ValueError('Changed current native event asset: ' + row['path'])
            fields = {}
            for key in ('objectName', 'id', 'guid'):
                matches = re.findall(r'^  ' + key + r': (.+)$', asset.read_text('utf-8'), re.M)
                if len(matches) != 1:
                    raise ValueError('Inspect current native event fields')
                fields[key] = matches[0].strip()
            if fields['guid'].upper() != asset.stem.upper():
                raise ValueError('Native event GUID mismatch')
            by_event[fields['objectName']] = dict(id=int(fields['id']), name=fields['objectName'],
                guid=fields['guid'], source=asset_identity)
    chosen = [by_event[name] for name in sorted(set(event_names))]
    old_objects = {row['id']: row for row in dependencies['objects']}
    selected = OfficialSource()
    with AudioSource(expected_version=selected.version, work_directory=work) as audio:
        actions_by_id = {}
        old_actions = {row['id']: row for row in events['actions']}
        for event in chosen:
            identifier, banks = audio.load_event(event['name'])
            if identifier != event['id'] or not banks or 'banks' in event and banks != event['banks']:
                raise ValueError('Migrate changed official event/bank mapping: ' + event['name'])
            event['banks'] = banks
            fields = audio.by_id[identifier][1]
            if values(fields, 'eHircType') != [4]:
                raise ValueError('Native sound reference is not an event')
            ordered = values(fields, 'ulActionID')
            if 'actions' in event and event['actions'] != ordered:
                raise ValueError('Migrate changed official event action order')
            event['actions'] = ordered
            for key in ordered:
                fields = audio.by_id[key][1]
                if values(fields, 'eHircType') != [3]:
                    raise ValueError('Native event action type mismatch')
                if key in old_actions and fields != [tuple(field) for field in old_actions[key]['fields']]:
                    raise ValueError('Migrate changed official event action: ' + str(key))
                actions_by_id[key] = dict(id=key, fields=fields,
                    sources=[{k: v for k, v in row.items() if k != 'base64'}
                             for row in audio.object_payloads[key]])
        actions = [actions_by_id[key] for key in sorted(actions_by_id)]
        audio.objects(MINIMUM)
        objects, current_edges, parameters = closure(audio, actions)
        media_ids = {key for row in objects if row['type'] == 2 for key in values(row['fields'], 'sourceID')}
        # Media can live outside its action bank. Retain its independently
        # verified package even when that package contains no reachable HIRC.
        old_media = {row['id']: row for row in dependencies['media']}
        for key in media_ids:
            for row in old_media.get(key, {}).get('sources', []):
                raw = audio.package(row['package'])
                if sha(raw) != events['packages'][row['package']]['sha256']:
                    raise ValueError('Migrate changed shared official media package')
        media = media_receipts(audio, media_ids)
        if any(row['id'] in old_media and row['sha256'] != old_media[row['id']]['sha256'] for row in media):
            raise ValueError('Migrate changed official skill media')
        # Whole-bank equality also proves actions/media newly referenced by a
        # current status graph, without rewriting the historical event receipt.
        for name, row in audio.receipts().items():
            if name.endswith('_pck.rawfile') and name != MINIMUM and (
                    name not in events['packages'] or row['sha256'] != events['packages'][name]['sha256']):
                raise ValueError('Migrate changed or undelivered official media package: ' + name)
        chunks, defaults = global_settings(audio)
        old_chunks = {(row['bankId'], row['tag']): row['sha256'] for row in dependencies['global_chunks']}
        if {(row['bankId'], row['tag']): row['sha256'] for row in chunks} != old_chunks:
            raise ValueError('Inspect changed original global initialization settings')
        if set(parameters) - defaults.keys():
            raise ValueError('Unresolved original global parameter')
        library = audio.archive.read(events['native_library']['entry'])
        if sha(library) != events['native_library']['sha256']:
            raise ValueError('Inspect changed official Wwise engine before reusing its Windows SDK')
        changed = []
        for row in objects:
            before = old_objects.get(row['id'])
            if before is None:
                if any(source['package'] == MINIMUM for source in row['rawObjects']):
                    raise ValueError('Inspect newly referenced shared routing object: ' + str(row['id']))
                continue
            if row['fields'] == [tuple(field) for field in before['fields']]:
                continue
            if row['type'] != 17 or values(row['fields'], 'fxID') != values(before['fields'], 'fxID'):
                raise ValueError('Inspect changed non-effect routing: ' + str(row['id']))
            changed.append(row['id'])
        converted = convert_android_package_to_windows(audio.package(MINIMUM))
        PACKAGE.write_bytes(converted)
        receipt = dict(manifest=selected.version, base_source=identity(BASE),
            event_source=identity(EVENTS), dependency_source=identity(DEPENDENCIES),
            native_event_sources=native_receipts,
            events=chosen, actions=actions, objects=objects, edges=current_edges, media=media,
            global_chunks=chunks, referenced_parameters=parameters,
            global_parameter_defaults=[defaults[key] for key in sorted(defaults)],
            changed_effects=changed, packages=audio.receipts(),
            native_library=dict(entry=events['native_library']['entry'], sha256=sha(library),
                apkSha256=selected.apk_sha256),
            overrides=[dict(previous_output=previous_output, output=identity(PACKAGE), source_package=MINIMUM)],
            tools=[identity(Path(__file__)), identity(ROOT / 'scripts/convert_wwise_android_package.py')])
        OUTPUT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        print(json.dumps(dict(manifest=selected.version, events=[row['name'] for row in chosen],
            changed_effects=changed, package_sha256=sha(converted))))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', type=Path, required=True)
    parser.add_argument('--events', nargs='+', required=True)
    parser.add_argument('--native-sources', type=Path, nargs='*', default=[])
    options = parser.parse_args()
    prepare(options.workspace, options.events, options.native_sources)
