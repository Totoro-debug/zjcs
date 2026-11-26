"""Bind current official shared Wwise routing without relabelling older media.

Run after the base Windows sound conversion. Every reused event bank and media
must remain identical; only the independently verified Minimum package changes.
"""
import argparse
import json
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


def prepare(work, event_names):
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
    chosen = [by_event[name] for name in sorted(set(event_names))]
    action_ids = {key for event in chosen for key in event['actions']}
    actions = [row for row in events['actions'] if row['id'] in action_ids]
    old_objects = {row['id']: row for row in dependencies['objects']}
    pending = [key for row in actions for key in values(row['fields'], 'idExt') if key]
    edges = {}
    for row in dependencies['edges']:
        edges.setdefault(row['source'], []).append(row['target'])
    packages, seen = {MINIMUM}, set()
    while pending:
        key = pending.pop()
        if key in seen:
            continue
        seen.add(key)
        pending.extend(edges.get(key, []))
        packages.update(row['package'] for row in old_objects[key]['rawObjects'])
    packages.update(row['package'] for action in actions for row in action['sources'])
    selected = OfficialSource()
    with AudioSource(expected_version=selected.version, work_directory=work) as audio:
        for event in chosen:
            identifier, banks = audio.event_banks(event['name'])
            if identifier != event['id'] or banks != event['banks']:
                raise ValueError('Migrate changed official event/bank mapping: ' + event['name'])
        for name in sorted(packages):
            audio.objects(name)
            if name != MINIMUM and audio.receipts()[name]['sha256'] != events['packages'][name]['sha256']:
                raise ValueError('Migrate changed official media bank: ' + name)
        for action in actions:
            if audio.by_id[action['id']][1] != [tuple(field) for field in action['fields']]:
                raise ValueError('Migrate changed official event action: ' + str(action['id']))
        objects, current_edges, parameters = closure(audio, actions)
        media_ids = {key for row in objects if row['type'] == 2 for key in values(row['fields'], 'sourceID')}
        # Media can live outside its action bank. Retain its independently
        # verified package even when that package contains no reachable HIRC.
        old_media = {row['id']: row for row in dependencies['media']}
        for key in media_ids:
            for row in old_media[key]['sources']:
                raw = audio.package(row['package'])
                if sha(raw) != events['packages'][row['package']]['sha256']:
                    raise ValueError('Migrate changed shared official media package')
        media = media_receipts(audio, media_ids)
        if any(row['sha256'] != old_media[row['id']]['sha256'] for row in media):
            raise ValueError('Migrate changed official skill media')
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
            before = old_objects[row['id']]
            if row['fields'] == [tuple(field) for field in before['fields']]:
                continue
            if row['type'] != 17 or values(row['fields'], 'fxID') != values(before['fields'], 'fxID'):
                raise ValueError('Inspect changed non-effect routing: ' + str(row['id']))
            changed.append(row['id'])
        converted = convert_android_package_to_windows(audio.package(MINIMUM))
        PACKAGE.write_bytes(converted)
        receipt = dict(manifest=selected.version, base_source=identity(BASE),
            event_source=identity(EVENTS), dependency_source=identity(DEPENDENCIES),
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
    options = parser.parse_args()
    prepare(options.workspace, options.events)
