"""Stage a current Home export with stable Unity GUIDs and preserved importers."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import uuid
import argparse

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_home_assets import collect_assets, verify_builtin_font_texture, guid_from_meta


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build():
    profile = OfficialSource()
    stage = ROOT / 'output/current-871'
    export = stage / 'home-ripper/ExportedProject/Assets'
    inputs = stage / 'home-inputs'
    old_path = ROOT / 'assets/reference/home-unity/source-manifest.json'
    old = json.loads(old_path.read_text('utf8'))
    input_manifest = json.loads((inputs / 'source-manifest.json').read_text('utf8'))
    if input_manifest['manifest'] != profile.version:
        raise ValueError('Unexpected Home input profile')
    for record in input_manifest['inputs']:
        path = inputs / record['output']
        if sha(path.read_bytes()) != record['decoded_sha256']:
            raise ValueError('Home converter input hash differs: ' + str(path))
    by_guid, selected, unresolved = collect_assets(export)
    font = verify_builtin_font_texture(inputs)
    target_root = ROOT / 'unity/Assets/Official/Home'
    previous = {x['path']: x for x in old['outputs']}
    shared = {x['path']: x for x in old.get('shared_bindings', [])}
    targets = {}
    remaps = {}
    for path in sorted(selected):
        relative = path.relative_to(export).as_posix()
        if relative in shared:
            target = ROOT / shared[relative]['shared_asset']
        else:
            target = target_root / relative
        meta = Path(str(target) + '.meta')
        existing = guid_from_meta(meta) if meta.exists() else None
        source_guid = guid_from_meta(Path(str(path) + '.meta'))
        if not source_guid:
            raise ValueError('Export has no asset GUID: ' + relative)
        remaps[source_guid] = existing or uuid.uuid5(uuid.NAMESPACE_URL,
                                                   'zjcs/current-home/' + relative).hex
        targets[path] = target
    for guid, owners in unresolved.items():
        if any('MPLUSRounded1c-Medium' not in str(owner) or owner.suffix != '.mat' for owner in owners):
            raise ValueError('Unresolved Home dependency: ' + guid)
    output = ROOT / '.tools/source-packages/current' / profile.version / 'home-assets'
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for source in sorted(selected):
        relative = source.relative_to(export).as_posix()
        target = targets[source]
        raw = source.read_bytes()
        content = raw
        if raw.startswith(b'%YAML'):
            text = raw.decode('utf8')
            for missing, owners in unresolved.items():
                if source in owners:
                    old_ref = '{fileID: 2800000, guid: ' + missing + ', type: 3}'
                    new_ref = '{fileID: 10103, guid: ' + font['unity_guid'] + ', type: 0}'
                    if text.count(old_ref) != 1:
                        raise ValueError('Unexpected built-in font texture reference')
                    text = text.replace(old_ref, new_ref)
            text = re.sub(r'guid: ([0-9a-f]{32})',
                          lambda match: 'guid: ' + remaps.get(match[1], match[1]), text)
            content = text.encode('utf8')
        meta = Path(str(target) + '.meta')
        if meta.exists():
            meta_data = meta.read_bytes()
        else:
            source_meta = Path(str(source) + '.meta').read_text('utf8')
            meta_data = re.sub(r'(?m)^guid: ([0-9a-f]{32})$',
                               lambda match: 'guid: ' + remaps[match[1]], source_meta).encode('utf8')
        before = sha(target.read_bytes()) if target.exists() else None
        record = {'path': target.relative_to(ROOT).as_posix(), 'export_path': relative,
                  'source_sha256': sha(raw), 'sha256': sha(content), 'bytes': len(content),
                  'previous_sha256': before, 'meta_sha256': sha(meta_data),
                  'guid': remaps[guid_from_meta(Path(str(source) + '.meta'))]}
        if before != record['sha256']:
            prior = previous.get(relative) or shared.get(relative)
            allowed = {prior.get('sha256'), prior.get('unity_import_sha256')} if prior else set()
            if before is not None and before not in allowed:
                raise ValueError('Changed Home asset has unregistered local edits: ' + str(target))
            (output / (record['sha256'] + '.bin')).write_bytes(content)
            (output / (record['meta_sha256'] + '.meta')).write_bytes(meta_data)
            record['staged'] = (output / (record['sha256'] + '.bin')).relative_to(ROOT).as_posix()
        records.append(record)
    result = {'manifest': profile.version, 'base_manifest': old['source_manifest'],
              'previous_manifest_sha256': sha(old_path.read_bytes()),
              'inputs': input_manifest, 'font_texture': font,
              'generator_sha256': sha(Path(__file__).read_bytes()), 'outputs': records}
    (output / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Home assets:', len(records), 'changed:', sum('staged' in r for r in records), flush=True)
    return result


def activate():
    profile = OfficialSource()
    source = ROOT / '.tools/source-packages/current' / profile.version / 'home-assets/source-manifest.json'
    receipt = json.loads(source.read_bytes())
    manifest_path = ROOT / 'assets/reference/home-unity/source-manifest.json'
    if sha(manifest_path.read_bytes()) != receipt['previous_manifest_sha256']:
        current = json.loads(manifest_path.read_bytes())
        if current.get('current_conversion', {}).get('receipt_sha256') == sha(source.read_bytes()):
            for record in receipt['outputs']:
                if sha((ROOT / record['path']).read_bytes()) != record['sha256']:
                    raise ValueError('Activated Home asset changed: ' + record['path'])
            return current
        raise ValueError('Home source manifest changed since staging')
    # Validate all outputs before replacing any of them.
    for record in receipt['outputs']:
        path = ROOT / record['path']
        before = sha(path.read_bytes()) if path.exists() else None
        if before != record['previous_sha256']:
            raise ValueError('Home output changed since staging: ' + str(path))
        if 'staged' in record and sha((ROOT / record['staged']).read_bytes()) != record['sha256']:
            raise ValueError('Staged Home output hash differs')
    history = ROOT / '.tools/source-packages/history/converted'
    history.mkdir(parents=True, exist_ok=True)

    def preserve(path):
        if path.exists():
            raw = path.read_bytes()
            backup = history / (sha(raw) + '.bin')
            if not backup.exists():
                backup.write_bytes(raw)

    for record in receipt['outputs']:
        if 'staged' not in record:
            continue
        path = ROOT / record['path']
        preserve(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name + '.current.tmp')
        temporary.write_bytes((ROOT / record['staged']).read_bytes())
        temporary.replace(path)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            meta.write_bytes((source.parent / (record['meta_sha256'] + '.meta')).read_bytes())
    previous = json.loads(manifest_path.read_bytes())
    previous.update(source_manifest=profile.version, base_manifest=receipt['base_manifest'],
                    inputs=receipt['inputs']['inputs'], builtin_inputs=receipt['inputs']['builtin_inputs'])
    previous['outputs'] = [
        {'path': record['export_path'], 'bytes': record['bytes'], 'sha256': record['sha256'], 'guid': record['guid']}
        for record in receipt['outputs'] if record['path'].startswith('unity/Assets/Official/Home/')]
    previous['shared_bindings'] = [
        {'path': record['export_path'], 'shared_asset': record['path'], 'sha256': record['sha256'], 'guid': record['guid']}
        for record in receipt['outputs'] if not record['path'].startswith('unity/Assets/Official/Home/')]
    previous['current_conversion'] = {'receipt': source.relative_to(ROOT).as_posix(),
                                      'receipt_sha256': sha(source.read_bytes())}
    preserve(manifest_path)
    manifest_path.write_text(json.dumps(previous, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return previous


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    args = parser.parse_args()
    print('Activated Home assets:', len(activate()['outputs'])) if args.activate else build()
