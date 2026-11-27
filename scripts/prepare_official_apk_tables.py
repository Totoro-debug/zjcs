"""Decode every packaged table bundle with the matching APK or online code."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT, UnityPy
from official_manifest import read_manifest
from official_source import OfficialSource
from official_bundle_reader import OfficialBundleReader, verify_payload
from official_tables import Constructors


def prepare(*, embedded=True):
    source = OfficialSource()
    code = source.prepared_path/('official-code/apk' if embedded else 'official-code')
    code_receipt = source.prepared_path/('apk-code-manifest.json' if embedded else 'code-manifest.json')
    archive = json.loads(code_receipt.read_bytes())
    if archive['manifest'] != source.version or archive['apk_sha256'] != source.apk_sha256:
        raise ValueError('Embedded APK code identity differs')
    target = source.prepared_path/('official-data/apk' if embedded else 'official-data')
    target.mkdir(exist_ok=True)
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('Embedded APK hash differs')
    inputs, bundles, extracted = [], [], {}
    with OfficialBundleReader(source) as reader:
        manifest = (read_manifest(reader.archive.read(f'assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_{source.apk_version}.v3.bytes'))
                    if embedded else reader.manifest('DefaultPackage'))
        for row in manifest['bundles']:
            if not row['name'].startswith('defaultpackage_assets_config_luban'):
                continue
            if embedded:
                entry = 'assets/yoo/DefaultPackage/'+row['file']
                raw = reader.archive.read(entry)
                verify_payload(raw, row, identity=entry)
                origin = dict(apk_entry=entry)
            else:
                verified = reader.read('DefaultPackage', row['name'])
                raw, origin = verified.data, dict(verified.provenance)
            bundles.append(dict(bundle=row['name'], sha256=hashlib.sha256(raw).hexdigest(), source=origin))
            env = UnityPy.load(Apk.decode(raw))
            represented = set()
            for asset_path, pointer in env.container.items():
                if not asset_path.startswith('Assets/Config/Luban/') or not asset_path.endswith('.g.bytes'):
                    raise ValueError('Unexpected table asset path: '+asset_path)
                relative = asset_path.removeprefix('Assets/Config/Luban/')
                path = target/relative
                if not path.resolve().is_relative_to(target.resolve()):
                    raise ValueError('Unsafe table path')
                obj = pointer.deref()
                if obj.type.name != 'TextAsset':
                    raise ValueError('Unexpected table object type')
                data = obj.read().m_Script.encode('utf8', 'surrogateescape')
                if path.exists() and path.read_bytes() != data:
                    raise ValueError('Existing table bytes changed: '+relative)
                if relative in extracted:
                    raise ValueError('Duplicate table asset path: '+relative)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                extracted[relative.removesuffix('.g.bytes')] = path
                represented.add(obj.path_id)
                inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(data).hexdigest(),
                                   bundle=row['name'], path_id=str(obj.path_id)))
            if represented != {obj.path_id for obj in env.objects if obj.type.name == 'TextAsset'}:
                raise ValueError('Uncatalogued table payload: '+row['name'])
    constructors = Constructors(code, source.prepared_path/'official-code/global-metadata.dat')
    tables_source = (code/'common-source/Common/Tables.cs').read_text('utf-8-sig')
    mapping = {match[2]:match[1] for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', tables_source)}
    tables, failures = {}, {}
    for name, path in extracted.items():
        try:
            tables[name] = constructors.table(path.read_bytes(), mapping[name])
        except Exception as error:
            failures[name] = str(error)
    payload = json.dumps(tables, ensure_ascii=False, separators=(',', ':')).encode('utf8')
    (target/'tables.json').write_bytes(payload)
    (target/'table-failures.json').write_text(json.dumps(failures, ensure_ascii=False, indent=2)+'\n', 'utf8')
    result = dict(manifest=source.version, source_manifest=source.apk_version if embedded else source.version, apk_sha256=source.apk_sha256,
                  generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                  constructors_sha256=hashlib.sha256((ROOT/'scripts/official_tables.py').read_bytes()).hexdigest(),
                  metadata_sha256=hashlib.sha256((source.prepared_path/'official-code/global-metadata.dat').read_bytes()).hexdigest(),
                  table_code_sha256=hashlib.sha256((code/'common-source/Common/Tables.cs').read_bytes()).hexdigest(),
                  code_manifest_sha256=hashlib.sha256(code_receipt.read_bytes()).hexdigest(),
                  bundles=bundles, inputs=inputs,
                  code_tables_without_payload=sorted(set(mapping)-set(extracted)),
                  tables=len(tables), unresolved=failures, table_sha256=hashlib.sha256(payload).hexdigest(),
                  completed=not failures)
    receipt_name = 'apk-tables-source-manifest.json' if embedded else 'all-tables-source-manifest.json'
    (source.prepared_path/receipt_name).write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('APK tables:' if embedded else 'Online tables:', len(tables), 'unresolved:', failures, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--online', action='store_true')
    args = parser.parse_args()
    prepare(embedded=not args.online)
