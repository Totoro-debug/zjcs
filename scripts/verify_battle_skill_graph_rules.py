"""Compare migrated skill graph components with the selected official EC bytes."""
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/python-packages'))
from official_apk import Apk
from official_ec_codec import Formats
from official_map_codec import unpack


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def verify_selected_components(definitions, skill_items=None):
    apk = Apk.current()
    assert apk.version == '884_156692', 'Review the selected EC version.'
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    formats = Formats(code)
    manifests = [ROOT / 'unity/Assets/Official/Combat' / name
                 for name in ('skill-rules-source-manifest.json', 'battle-rules-source-manifest.json')]
    paths = {}
    for path in manifests:
        doc = json.loads(path.read_text(encoding='utf-8-sig'))
        for key, record in doc.get('selected', doc.get('entities', {}).get('selected', {})).items():
            paths.setdefault(key, record['path'])
    wanted = set(definitions)
    groups = {}
    for key in wanted:
        groups.setdefault(paths[key], set()).add(key)
    records = []
    item_binding = None
    try:
        for name, keys in sorted(groups.items()):
            obj = apk.asset('config_ec', '/' + Path(name).name)
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows = unpack(raw)[0]
                prefix = False
            except Exception:
                rows = unpack(raw[2:])[0]
                prefix = True
            matched = {}
            for parent, entity_id, components in rows:
                key = str(entity_id)
                if key not in keys:
                    continue
                assert key not in matched, 'Ambiguous selected EC entity: ' + key
                original = {}
                for component, payload in components:
                    short = component.removeprefix('Common.')
                    if short in definitions[key]:
                        original[short] = formats.component(component, payload[2:] if prefix and payload else payload)
                original = json.loads(json.dumps(original, ensure_ascii=False))
                assert original == definitions[key], ('Selected EC differs from migrated configuration', key,
                                                     original, definitions[key])
                matched[key] = dict(parent=parent, components=original)
            assert set(matched) == keys, ('Selected EC fragment no longer contains these entities', name, keys - set(matched))
            records.append(dict(path=name, source=apk.provenance(obj),
                object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                payload_sha256=hashlib.sha256(raw).hexdigest(), legacy_prefix=prefix,
                entities=matched))
        if skill_items is not None:
            from official_tables import Constructors
            parser = Constructors(code)
            mapping = {name: cls for cls, name in re.findall(
                r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
            obj = apk.asset('config_luban', '/skill.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            wanted_items = {row['ClassId']: row['EcEntityId'] for row in skill_items}
            rows = [dict(ClassId=row['ClassId'], EcEntityId=row['EcEntityId'])
                    for row in parser.table(raw, mapping['skill']) if row['ClassId'] in wanted_items]
            assert {row['ClassId']: row['EcEntityId'] for row in rows} == wanted_items
            constructors = []
            for name in sorted(parser.sources):
                path = code / 'common-source/Common' / (name + '.cs')
                if not path.exists():
                    path = code / 'common-source/Common.LubanGen' / (name + '.cs')
                assert path.is_file(), name
                constructors.append(identity(path))
            item_binding = dict(source=apk.provenance(obj),
                object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                payload_sha256=hashlib.sha256(raw).hexdigest(), rows=rows, constructors=constructors)
    finally:
        apk.archive.close()
    return dict(manifest=apk.version, fragments=records, skill_item_bindings=item_binding,
        runtime_source_manifests=[identity(path) for path in manifests],
        formatters=[identity(path) for path in sorted(set(formats.sources.values()))],
        verifier=identity(Path(__file__)),
        operation='Decode the selected APK-first EC fragments with their own generated formatters. '
            'Compare every migrated component field for the requested entities; retain historical '
            'runtime source manifests separately. No configuration or expected hash is replaced.')
