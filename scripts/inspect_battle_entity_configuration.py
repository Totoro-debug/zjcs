"""Compare installed skill/hit entities with the selected official EC fragments.

This reads sources and emits evidence without changing game configuration.
Only the explicitly audited fragment moves below can replace a historical
selection, and only after checking every current skill fragment for uniqueness.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack

KINDS = {'FightSkillComponent', 'FightHitFixedComponent', 'FightHitChainedComponent',
         'FightHitSummonComponent', 'FightHitCustomTargetComponent',
         'FightHitRandomTargetComponent', 'FightHitTriggerSkillComponent'}
FRAGMENT_MOVES = {'884_156692': {
    13871: ('Assets/Config/EC/FightSkill_part191.g.bytes',
            'Assets/Config/EC/FightSkill_part194.g.bytes'),
    13941: ('Assets/Config/EC/FightSkill_part194.g.bytes',
            'Assets/Config/EC/FightSkill_part197.g.bytes')}}


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path.read_bytes()))


def canonical_digest(value):
    return digest(json.dumps(value, ensure_ascii=False, sort_keys=True,
                             separators=(',', ':')).encode('utf-8'))


def inspect():
    entities, selected, inputs, payloads, provenance = {}, {}, [], {}, {}
    for kind in ('skill', 'battle'):
        path = ROOT / f'unity/Assets/Official/Combat/{kind}-rules.json'
        sidecar = path.with_name(f'{kind}-rules-source-manifest.json')
        raw = path.read_bytes()
        metadata = json.loads(sidecar.read_bytes())
        if digest(raw) != metadata['output_sha256']:
            raise ValueError('Installed configuration differs from its source receipt: ' + str(path))
        for identity, components in json.loads(raw)['entities'].items():
            entities.setdefault(identity, {}).update(components)
        selected.update(metadata['selected'] if kind == 'skill' else metadata['entities']['selected'])
        inputs.extend((record(path), record(sidecar)))
        payloads[path] = raw
        provenance[kind] = dict(manifest=metadata['manifest'],
            conversion=metadata.get('current_combat_payload_conversion') if kind == 'skill'
                       else metadata.get('current_table_conversion'))
    wanted = {int(identity) for identity, value in entities.items() if KINDS.intersection(value)}
    if any(str(identity) not in selected for identity in wanted):
        raise ValueError('An installed skill/hit entity lacks its original fragment selection.')
    results, alternates, errors, moves = [], [], [], []
    remaining = set(wanted)

    def compare(origin, decoded):
        before = entities[str(origin['entity_id'])]
        changed = {key: dict(installed=before.get(key), selected=decoded.get(key))
                   for key in sorted(before.keys() | decoded.keys())
                   if before.get(key) != decoded.get(key)}
        results.append(dict(origin, changed=changed,
            installed_components_sha256=canonical_digest(before),
            selected_components_sha256=canonical_digest(decoded)))
        remaining.remove(origin['entity_id'])
        if changed or len(results) % 100 == 0:
            print(f'Original configuration: {len(results)}/{len(wanted)}; '
                  f'entity {origin["entity_id"]}; changed {list(changed)}', file=sys.stderr, flush=True)

    with Apk.current() as apk:
        version = apk.version
        code = ROOT / '.tools/source-packages/current' / version
        code_manifest = json.loads((code / 'code-manifest.json').read_bytes())
        assembly = code / 'official-code/managed/decoded/Console.dll'
        if (code_manifest['manifest'] != version or digest(assembly.read_bytes())
                != code_manifest['assemblies']['Console.dll']['decoded_sha256']):
            raise ValueError('Selected original shared assembly differs from its source receipt.')
        inputs.extend((record(code / 'code-manifest.json'), record(assembly)))
        formats = Formats(code / 'official-code')
        preferred = {selected[str(identity)]['path'].casefold() for identity in wanted}
        entries = sorted(apk.container('config_ec'),
                         key=lambda pair: (pair[0].casefold() not in preferred, pair[0]))
        for name, pointer in entries:
            if not remaining:
                break
            if not name.endswith('.g.bytes') or '/fightskill_' not in name.casefold():
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            try:
                rows, prefix = unpack(raw)[0], False
            except Exception:
                rows, prefix = unpack(raw[2:])[0], True
            for parent, identity, components in rows:
                if identity not in remaining:
                    continue
                decoded = {}
                for typ, data in components:
                    try:
                        decoded[typ.removeprefix('Common.')] = formats.component(
                            typ, data[2:] if prefix and data else data)
                    except Exception as error:
                        errors.append(dict(entity_id=identity, path=name, component=typ,
                                           error=str(error), raw=data.hex() if data else None))
                origin = dict(entity_id=identity, path=name, parent=parent,
                              source=apk.provenance(obj), raw_sha256=digest(raw))
                if name.casefold() != selected[str(identity)]['path'].casefold():
                    alternates.append(dict(origin, components=decoded))
                    continue
                compare(origin, decoded)

        for identity in sorted(remaining):
            candidates = [row for row in alternates if row['entity_id'] == identity]
            move = FRAGMENT_MOVES.get(version, {}).get(identity)
            if (move is None or len(candidates) != 1 or selected[str(identity)]['legacy']
                    or (selected[str(identity)]['path'], candidates[0]['path']) != move):
                continue
            candidate = dict(candidates[0])
            decoded = candidate.pop('components')
            moves.append(dict(entity_id=identity, recorded_fragment=move[0],
                              selected_fragment=move[1], current_definitions=1))
            compare(candidate, decoded)

    if any(path.read_bytes() != raw for path, raw in payloads.items()):
        raise ValueError('Installed configuration changed during source comparison.')
    inputs.extend(record(ROOT / 'scripts' / name) for name in
                  (Path(__file__).name, 'official_ec_codec.py', 'official_map_codec.py', 'official_apk.py'))
    return dict(manifest=version, operation=__doc__, inputs=inputs,
        installed_provenance=provenance,
        entity_counts=dict(total=len(wanted), decoded=len(results),
                           changed=sum(bool(row['changed']) for row in results)),
        rows=sorted(results, key=lambda row: row['entity_id']),
        fragment_moves=moves, alternate_sources=alternates,
        unresolved_entities=sorted(remaining), decode_errors=errors,
        formatters={name: record(path) for name, path in sorted(formats.sources.items())},
        limitations=['Only installed skill/hit entity components are compared; other tables, '
                     'properties, new entity membership and runtime behavior are outside this comparison.',
                     'Unaudited or ambiguous alternative fragments remain unresolved. '
                     'Comparison does not activate configuration changes.'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    options = parser.parse_args()
    result = inspect()
    text = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
    if options.output:
        options.output.parent.mkdir(parents=True, exist_ok=True)
        options.output.write_text(text, encoding='utf-8')
    else:
        print(text, end='')
    if result['decode_errors']:
        raise SystemExit(1)
