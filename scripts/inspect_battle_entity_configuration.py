"""Compare installed combat entities with the selected official EC fragments.

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

import re
import sqlite3
from validation_workspace import temporary_validation_directory


def file_digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def entity_items(path):
    # Keep only the UTF-8 text and a single decoded entity, never the full object tree.
    text = path.read_text(encoding='utf-8')
    decoder = json.JSONDecoder()
    space = re.compile(r'\s*')
    tokens = re.compile(r'"(?:[^"\\]|\\.)*"|[{}\[\]]')
    def ws(pos):
        return space.match(text, pos).end()
    def skip(pos):
        pos = ws(pos)
        if text[pos] not in '{[':
            return decoder.raw_decode(text, pos)[1]
        depth = 0
        for token in tokens.finditer(text, pos):
            mark = token.group()
            if mark[0] == '"':
                continue
            depth += 1 if mark in ('{', '[') else -1
            if depth == 0:
                return token.end()
        raise ValueError('Unterminated JSON value')
    p = ws(0)
    assert text[p] == '{'
    p += 1
    while text[ws(p)] != '}':
        key, p = decoder.raw_decode(text, ws(p))
        p = ws(p)
        assert text[p] == ':'
        p = ws(p + 1)
        if key == 'entities':
            assert text[p] == '{'
            p += 1
            while text[ws(p)] != '}':
                identity, p = decoder.raw_decode(text, ws(p))
                p = ws(p)
                assert text[p] == ':'
                components, p = decoder.raw_decode(text, ws(p + 1))
                yield identity, components
                p = ws(p)
                if text[p] == ',':
                    p += 1
                elif text[p] != '}':
                    raise ValueError('Invalid entity separator')
            p += 1
        else:
            p = skip(p)
        p = ws(p)
        if text[p] == ',':
            p += 1
        elif text[p] != '}':
            raise ValueError('Invalid root separator')
    if ws(p + 1) != len(text):
        raise ValueError('Trailing JSON')


class ExpectedEntities:
    def __init__(self, path):
        self.path = path
        self.db = sqlite3.connect(self.path)
        self.db.execute('PRAGMA cache_size=-8192')
        self.db.execute('CREATE TABLE entity (identity TEXT PRIMARY KEY, components TEXT NOT NULL)')
    def put(self, identity, components):
        row = self.db.execute('SELECT components FROM entity WHERE identity=?', (identity,)).fetchone()
        if row:
            merged = json.loads(row[0])
            merged.update(components)
            components = merged
        self.db.execute('INSERT OR REPLACE INTO entity VALUES (?,?)',
            (identity, json.dumps(components, ensure_ascii=False, separators=(',', ':'))))
    def __getitem__(self, identity):
        row = self.db.execute('SELECT components FROM entity WHERE identity=?', (identity,)).fetchone()
        if row is None:
            raise KeyError(identity)
        return json.loads(row[0])
    def __iter__(self):
        return (row[0] for row in self.db.execute('SELECT identity FROM entity'))


KINDS = {'FightSkillComponent', 'FightHitFixedComponent', 'FightHitChainedComponent',
         'FightHitSummonComponent', 'FightHitCustomTargetComponent',
         'FightHitRandomTargetComponent', 'FightHitTriggerSkillComponent'}
FRAGMENT_MOVES = {'884_156692': {
    13985: ('Assets/Config/EC/FightSkill_part197.g.bytes',
            'Assets/Config/EC/FightSkill_part200.g.bytes'),
    13871: ('Assets/Config/EC/FightSkill_part191.g.bytes',
            'Assets/Config/EC/FightSkill_part194.g.bytes'),
    13941: ('Assets/Config/EC/FightSkill_part194.g.bytes',
            'Assets/Config/EC/FightSkill_part197.g.bytes')}}


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=file_digest(path))


def canonical_digest(value):
    return digest(json.dumps(value, ensure_ascii=False, sort_keys=True,
                             separators=(',', ':')).encode('utf-8'))


def _inspect(entities, all_components):
    selected, inputs, payloads, provenance = {}, [], {}, {}
    for kind in ('skill', 'battle'):
        path = ROOT / f'unity/Assets/Official/Combat/{kind}-rules.json'
        sidecar = path.with_name(f'{kind}-rules-source-manifest.json')
        metadata = json.loads(sidecar.read_bytes())
        installed_hash = file_digest(path)
        if installed_hash != metadata['output_sha256']:
            raise ValueError('Installed configuration differs from its source receipt: ' + str(path))
        count = 0
        for identity, components in entity_items(path):
            entities.put(identity, components)
            count += 1
        entities.db.commit()
        selected.update(metadata['selected'] if kind == 'skill' else metadata['entities']['selected'])
        inputs.extend((record(path), record(sidecar)))
        payloads[path] = installed_hash
        provenance[kind] = dict(manifest=metadata['manifest'],
            conversion=metadata.get('current_combat_payload_conversion') if kind == 'skill'
                       else metadata.get('current_table_conversion'))
        del metadata
        print(f'Staged {kind}: {count} entities into isolated SQLite cache', file=sys.stderr, flush=True)
    installed_ids = set(map(int, entities))
    wanted = {int(identity) for identity in entities
              if all_components or KINDS.intersection(entities[identity])}
    if any(str(identity) not in selected for identity in wanted):
        raise ValueError('An installed skill/hit entity lacks its original fragment selection.')
    results, alternates, errors, moves = [], [], [], []
    remaining = set(wanted)
    referenced_damage_ids, new_damage_definitions = set(), {}

    def compare(origin, decoded):
        before = entities[str(origin['entity_id'])]
        changed = {key: dict(installed=before.get(key), selected=decoded.get(key))
                   for key in sorted(before.keys() | decoded.keys())
                   if before.get(key) != decoded.get(key)}
        results.append(dict(origin, changed=changed,
            installed_components_sha256=canonical_digest(before),
            selected_components_sha256=canonical_digest(decoded)))
        remaining.remove(origin['entity_id'])
        damage_id = decoded.get('FightHitFixedComponent', {}).get('DamageId', 0)
        if damage_id > 0 and damage_id not in installed_ids:
            referenced_damage_ids.add(damage_id)
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
            if (not name.endswith('.g.bytes') or '/config/ec/' not in name.casefold()
                    or (name.casefold() not in preferred and '/fightskill_' not in name.casefold())):
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            try:
                rows, prefix = unpack(raw)[0], False
            except Exception:
                try:
                    rows, prefix = unpack(raw[2:])[0], True
                except Exception as error:
                    errors.append(dict(path=name, component='<container>', error=str(error),
                                       raw_sha256=digest(raw), source=apk.provenance(obj)))
                    continue
            for parent, identity, components in rows:
                if identity not in installed_ids and any(
                        typ == 'Common.FightDamageComponent' for typ, data in components):
                    new_damage_definitions.setdefault(identity, []).append((
                        dict(entity_id=identity, path=name, parent=parent,
                             source=apk.provenance(obj), raw_sha256=digest(raw)),
                        components, prefix))
                if identity not in remaining:
                    continue
                decoded = {}
                error_count = len(errors)
                for typ, data in components:
                    try:
                        decoded[typ.removeprefix('Common.')] = formats.component(
                            typ, data[2:] if prefix and data else data)
                    except Exception as error:
                        errors.append(dict(entity_id=identity, path=name, component=typ,
                                           error=str(error), raw=data.hex() if data else None))
                if len(errors) != error_count:
                    continue
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

        additional_damage, unresolved_damage = [], []
        for identity in sorted(referenced_damage_ids):
            definitions = new_damage_definitions.get(identity, [])
            if len(definitions) != 1:
                unresolved_damage.append(dict(entity_id=identity, definitions=len(definitions)))
                continue
            origin, components, prefix = definitions[0]
            decoded = {}
            error_count = len(errors)
            for typ, data in components:
                try:
                    decoded[typ.removeprefix('Common.')] = formats.component(
                        typ, data[2:] if prefix and data else data)
                except Exception as error:
                    errors.append(dict(entity_id=identity, path=origin['path'],
                                       component=typ, error=str(error), raw=data.hex() if data else None))
            if len(errors) == error_count:
                additional_damage.append(dict(origin, components=decoded,
                    raw_components={typ: data.hex() if data else None for typ, data in components}))
            else:
                unresolved_damage.append(dict(entity_id=identity, definitions=1))

    if any(file_digest(path) != expected for path, expected in payloads.items()):
        raise ValueError('Installed configuration changed during source comparison.')
    inputs.extend(record(ROOT / 'scripts' / name) for name in
                  ('inspect_battle_entity_configuration.py', 'official_ec_codec.py', 'official_map_codec.py',
                   'official_apk.py', 'validation_workspace.py'))
    return dict(manifest=version, operation=__doc__, inputs=inputs,
        scope='all-components' if all_components else 'skill-and-hit',
        installed_provenance=provenance,
        entity_counts=dict(total=len(wanted), decoded=len(results),
                           changed=sum(bool(row['changed']) for row in results)),
        rows=sorted(results, key=lambda row: row['entity_id']),
        fragment_moves=moves, alternate_sources=alternates,
        additional_referenced_damage_entities=additional_damage,
        unresolved_damage_dependencies=unresolved_damage,
        unresolved_entities=sorted(remaining), decode_errors=errors,
        formatters={name: record(path) for name, path in sorted(formats.sources.items())},
        limitations=[('All installed combat entities' if all_components else 'Installed skill/hit entities')
                     + ' are compared; other tables, '
                     'properties, new entity membership and runtime behavior are outside this comparison.',
                     'Unaudited or ambiguous alternative fragments remain unresolved. '
                     'Comparison does not activate configuration changes.'])


def inspect(all_components=False):
    with temporary_validation_directory('battle-configuration-') as temporary:
        entities = ExpectedEntities(Path(temporary) / 'expected.sqlite')
        try:
            return _inspect(entities, all_components)
        finally:
            entities.db.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--all-components', action='store_true',
                        help='Include installed status, damage and other combat entities.')
    options = parser.parse_args()
    result = inspect(options.all_components)
    if options.output:
        options.output.parent.mkdir(parents=True, exist_ok=True)
        with options.output.open('w', encoding='utf-8') as stream:
            json.dump(result, stream, ensure_ascii=False, indent=2)
            stream.write('\n')
    else:
        json.dump(result, sys.stdout, ensure_ascii=False, indent=2)
        print()
    if (result['decode_errors'] or result['unresolved_entities']
            or result['unresolved_damage_dependencies']):
        raise SystemExit(1)
