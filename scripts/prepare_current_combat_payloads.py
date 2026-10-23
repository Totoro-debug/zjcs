"""Stage changed combat components using each version's generated formatter."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def normalized(value):
    return json.loads(json.dumps(value, ensure_ascii=False))


def build():
    apks = [Apk(), Apk.current()]
    from current_conversion_state import activated_receipt
    prior = activated_receipt(ROOT / 'assets/reference/official-map/current-combat-source-manifest.json', apks[1].version)
    if prior is not None:
        return prior
    integrated = {}
    for relative in ('assets/official/skill-rules.json', 'assets/official/battle-rules.json',
                     'assets/official/combat-entities-full.json',
                     'unity/Assets/Official/Combat/skill-rules.json',
                     'unity/Assets/Official/Skills/skill-rules.json',
                     'unity/Assets/Official/Combat/battle-rules.json'):
        path = ROOT / relative
        if path.exists():
            for identity, entity in json.loads(path.read_bytes())['entities'].items():
                integrated.setdefault(identity, set()).update(entity)
    formats = [Formats(), Formats(ROOT / '.tools/source-packages/current/871_156598/official-code')]
    changed = json.loads((ROOT / '.tools/source-packages/latest-discovery/changed-referenced-objects.json').read_bytes())
    ids = {int(row['record']['path_id']) for row in changed
           if row['record']['bundle'] == 'defaultpackage_assets_config_ec.bundle'
           and row['delta']['new']['type'] == 'TextAsset'}
    # Container names, rather than manifest dependency offsets, identify the
    # authoritative fragment recorded by the original extraction.
    containers = [{name: ptr.deref() for name, ptr in apk.container('config_ec')
                   if ptr.path_id in ids and name.endswith('.g.bytes')}
                  for apk in apks]
    records = []
    deltas = {}
    for name, obj in containers[0].items():
        objects = [obj, containers[1][name]]
        decoded = []
        source = []
        for apk, reader, obj in zip(apks, formats, objects):
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows = unpack(raw)[0]
                prefix = False
            except Exception:
                try:
                    rows = unpack(raw[2:])[0]
                except Exception as error:
                    raise ValueError(f'EC fragment envelope {apk.version} {name} '
                                     f'bytes={len(raw)} head={raw[:20].hex()}: {error}') from error
                prefix = True
            entities = {}
            for parent, identity, components in rows:
                entity = {}
                for typ, data in components:
                    payload = data[2:] if prefix and data else data
                    entity[typ.removeprefix('Common.')] = payload
                entities[str(identity)] = {'parent': parent, 'components': entity}
            decoded.append(entities)
            source.append({'source': apk.provenance(obj), 'sha256': sha(raw), 'bytes': len(raw)})
        for identity in decoded[0].keys() | decoded[1].keys():
            if identity not in integrated:
                continue
            a = decoded[0].get(identity)
            b = decoded[1].get(identity)
            if a != b:
                if a is None or b is None or a['parent'] != b['parent']:
                    raise ValueError('Changed combat entity membership or parent needs expansion: '
                                     + name + ':' + identity + ':'
                                     + str((a and a['parent'], b and b['parent'], a is None, b is None)))
                component_versions = [{}, {}]
                for component in a['components'].keys() | b['components'].keys():
                    if component not in integrated.get(identity, set()):
                        continue
                    if a['components'].get(component) == b['components'].get(component):
                        continue
                    for version, entity in enumerate((a, b)):
                        if component not in entity['components']:
                            continue
                        payload = entity['components'][component]
                        try:
                            component_versions[version][component] = normalized(
                                formats[version].component('Common.' + component, payload))
                        except Exception as error:
                            raise ValueError(f'{apks[version].version} {name} entity={identity} {component} raw={payload.hex()}: {error}') from error
                deltas[(name, identity)] = component_versions
        records.append({'path': name, 'inputs': source})
    selected_paths = {}
    for kind in ('skill', 'battle'):
        manifest = json.loads((ROOT / f'assets/reference/{kind}-rules/source-manifest.json').read_bytes())
        selected_paths[kind] = manifest['selected'] if kind == 'skill' else manifest['entities']['selected']
    output = ROOT / '.tools/source-packages/current/871_156598/combat-payloads'
    output.mkdir(parents=True, exist_ok=True)
    converted = []
    for relative, kind in (
        ('assets/official/skill-rules.json', 'skill'), ('assets/official/battle-rules.json', 'battle'),
        ('assets/official/combat-entities-full.json', 'skill'),
        ('unity/Assets/Official/Combat/skill-rules.json', 'skill'),
        ('unity/Assets/Official/Skills/skill-rules.json', 'skill'),
        ('unity/Assets/Official/Combat/battle-rules.json', 'battle')):
        path = ROOT / relative
        if not path.exists():
            continue
        raw = path.read_bytes()
        document = json.loads(raw)
        changes = []
        for (name, identity), (before, after) in deltas.items():
            if identity not in document['entities'] or selected_paths[kind].get(identity, {}).get('path') != name:
                continue
            target = document['entities'][identity]
            for component in before.keys() | after.keys():
                if before.get(component) == after.get(component) or component not in target:
                    continue
                if target[component] != before.get(component):
                    raise ValueError('Combat component differs from its recorded source: ' + relative + ':' + identity + ':' + component)
                if component not in after:
                    del target[component]
                else:
                    target[component] = after[component]
                changes.append({'entity': identity, 'component': component, 'source': name})
        if not changes:
            continue
        payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        digest = sha(payload)
        destination = output / (digest + '.json')
        destination.write_bytes(payload)
        converted.append({'path': relative, 'previous_sha256': sha(raw), 'sha256': digest,
                          'staged': destination.relative_to(ROOT).as_posix(), 'changes': changes})
        print(relative, len(changes), flush=True)
    receipt = {'manifest': apks[1].version, 'base_manifest': apks[0].version,
               'operation': __doc__, 'inputs': records, 'outputs': converted,
               'formatters': [{name: {'path': path.relative_to(ROOT).as_posix(),
                                      'sha256': sha(path.read_bytes())}
                               for name, path in reader.sources.items()} for reader in formats],
               'generator_sha256': sha(Path(__file__).read_bytes())}
    (output / 'source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


if __name__ == '__main__':
    import argparse
    from refresh_current_character_meshes import preserve
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    options = parser.parse_args()
    result = build()
    target = ROOT / 'assets/reference/official-map/current-combat-source-manifest.json'
    if target.exists() and json.loads(target.read_bytes()) == result:
        print('Verified converted combat payloads:', len(result['outputs']))
        raise SystemExit(0)
    if options.activate:
        for record in result['outputs']:
            if sha((ROOT / record['path']).read_bytes()) != record['previous_sha256']:
                raise ValueError('Combat payload changed after staging: ' + record['path'])
            if sha((ROOT / record['staged']).read_bytes()) != record['sha256']:
                raise ValueError('Staged combat payload changed: ' + record['path'])
        for record in result['outputs']:
            target = ROOT / record['path']
            record['previous'] = preserve(target)
            temporary = target.with_name(target.name + '.current.tmp')
            temporary.write_bytes((ROOT / record['staged']).read_bytes())
            temporary.replace(target)
        target = ROOT / 'assets/reference/official-map/current-combat-source-manifest.json'
        if target.exists():
            preserve(target)
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Converted combat payloads:', len(result['outputs']))
