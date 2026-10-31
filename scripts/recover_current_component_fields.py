"""Recover exact current component fields alongside converter-native assets.

Uses original hot-update assemblies and recovered original AOT descriptors.
Raw bytes and CAB/PathID identities are retained even if a schema cannot decode.
"""
import argparse
import gc
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT, UnityPy
from official_source import OfficialSource
from current_component_schemas import CurrentSchemas
from prepare_unity_bundle_inputs import serialized_files


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


class BundleReader:
    def __init__(self, cache, receipt):
        self.cache = cache
        rows = {r['package']+'/'+r['file']: r for r in receipt['inputs'] if 'package' in r}
        index = json.loads((OfficialSource().cloud_path/'verified-cab-files.json').read_bytes())
        self.owners = {cab.casefold(): rows[paths[0]] for cab, paths in index.items()}
        self.builtins = [r for r in receipt['inputs'] if r['format'] == 'builtin']
        self.reset()

    def reset(self):
        self.env = UnityPy.Environment()
        self.loaded = {}
        fallback = self.env.find_file
        def resolve(name, *args, **kwargs):
            key = name.rsplit('/', 1)[-1].casefold()
            if self.env.get_cab(key) is None:
                if key in self.owners:
                    self.load(self.owners[key])
                elif key in ('unity_builtin_extra', 'unity default resources', 'globalgamemanagers', 'globalgamemanagers.assets', 'resources.assets', 'sharedassets0.assets', 'level0'):
                    for row in self.builtins:
                        self.load(row)
            found = self.env.get_cab(key)
            if found is not None:
                return found
            if key.startswith('cab-'):
                raise FileNotFoundError('Verified CAB dependency is absent: '+key)
            return fallback(name, *args, **kwargs)
        self.env.find_file = resolve

    def load(self, row):
        key = row['output']
        if key not in self.loaded:
            raw = (self.cache/key).read_bytes()
            if sha(raw) != row['decoded_sha256']:
                raise ValueError('Verified component input changed: '+key)
            loaded = self.env.load_file(raw, name=key)
            self.loaded[key] = loaded
            for file in serialized_files(loaded):
                self.env.register_cab(file.name, file)
            if Path(key).name == 'unity default resources':
                values = list(serialized_files(loaded))
                if len(values) == 1:
                    self.env.register_cab('unity default resources', values[0])
        return self.loaded[key]


def recover(bundle=None, only_class=None, limit=None, unsupported_only=False, all_inputs=False):
    source = OfficialSource()
    cache = ROOT/'.tools/source-packages/current'/source.version/'native-conversion'
    selection_path = cache/('source-manifest.json' if all_inputs else 'changed-inputs-source-manifest.json')
    selection = json.loads(selection_path.read_bytes())
    schemas = CurrentSchemas()
    reader = BundleReader(cache, json.loads((cache/'source-manifest.json').read_bytes()))
    rows = [row for row in selection['inputs'] if row['format'] == 'UnityFS' or (all_inputs and row['format'] == 'builtin')]
    required_types = None
    if unsupported_only:
        export = json.loads((cache/('native-export-all-source-manifest.json' if all_inputs else 'native-export-complete-source-manifest.json')).read_bytes())
        required_types = {match[1] for issue in export['conversion_issues']
                          if (match := re.search(r'Could not read MonoBehaviour structure for `([^`]+)`', issue))}
        print('Supplementing unsupported component types:', len(required_types), flush=True)
    if bundle:
        rows = [row for row in rows if row['name'] == bundle]
        if not rows:
            raise ValueError('Probe bundle is not in the verified selection')
    output = cache/('component-fields-probe' if bundle or only_class or limit else 'component-fields-all' if all_inputs else 'component-fields')
    output.mkdir(exist_ok=True)
    directories = {output}
    def ensure(path):
        if path not in directories:
            path.mkdir(parents=True, exist_ok=True)
            directories.add(path)
    records, failures = [], []
    for index, row in enumerate(rows, 1):
        objects = [obj for file in serialized_files(reader.load(row)) for obj in file.objects.values()]
        for obj in objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            base = obj.read(check_read=False)
            if not base.m_Script.m_PathID:
                continue
            script = base.m_Script.read()
            name = (script.m_Namespace+'.' if script.m_Namespace else '')+script.m_ClassName
            if required_types is not None and name not in required_types and script.m_ClassName not in required_types:
                continue
            if only_class and name != only_class:
                continue
            raw = obj.get_raw_data()
            digest = sha(raw)
            raw_path = output/'Raw'/(digest+'.bytes')
            ensure(raw_path.parent)
            if raw_path.exists() and raw_path.read_bytes() != raw:
                raise ValueError('Content-addressed original component changed')
            if not raw_path.exists():
                raw_path.write_bytes(raw)
            record = dict(bundle=row.get('name', Path(row['output']).name), cab=obj.assets_file.name, path_id=str(obj.path_id),
                          assembly=script.m_AssemblyName, type=name, raw_sha256=digest,
                          raw=raw_path.relative_to(output).as_posix())
            try:
                fields = schemas.read(obj)
                payload = json.dumps(fields, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8')
                relative = 'Fields/'+obj.assets_file.name+'/'+str(obj.path_id)+'.json'
                target = output/relative
                ensure(target.parent)
                if target.exists() and target.read_bytes() != payload:
                    raise ValueError('Previously decoded original fields changed')
                target.write_bytes(payload)
                record.update(fields=relative, sha256=sha(payload), managed_references=len(fields.get('references', {}).get('RefIds', [])))
            except Exception as error:
                record['decode_error'] = str(error)
                failures.append(record)
            records.append(record)
            if limit and len(records) >= limit:
                break
        if limit and len(records) >= limit:
            break
        # Keep shared source indexes, not UnityPy's accumulated object graphs.
        reader.reset()
        if index % 50 == 0:
            gc.collect()
            print('Component bundles:', index, '/', len(rows), 'objects:', len(records), 'unresolved:', len(failures), flush=True)
    result = dict(manifest=source.version, selection_sha256=sha(selection_path.read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), operation=__doc__,
                  supplemental_schema_sha256=sha((ROOT/'scripts/current_component_schemas.py').read_bytes()),
                  scope='Exporter-unsupported component types' if unsupported_only else 'Selected component types',
                  required_types=sorted(required_types) if required_types is not None else None,
                  objects=records, unresolved=failures, completed=not failures,
                  limitations=['Field sidecars retain source CAB/PathID PPtrs. Binding them into native Unity objects still requires exact module object mapping.'])
    (output/'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Decoded components:', len(records)-len(failures), 'unresolved:', len(failures), 'managed references:', sum(r.get('managed_references', 0) for r in records))
    if failures:
        print('First unresolved:', failures[0])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle')
    parser.add_argument('--only-class')
    parser.add_argument('--limit', type=int)
    parser.add_argument('--unsupported-only', action='store_true')
    parser.add_argument('--all-inputs', action='store_true')
    args = parser.parse_args()
    recover(args.bundle, args.only_class, args.limit, args.unsupported_only, args.all_inputs)
