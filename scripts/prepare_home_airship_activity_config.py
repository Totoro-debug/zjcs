"""Read the current official airship banner and function-limit tables into an owned validation workspace."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from validation_workspace import validation_directory


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, help='Output JSON file within the owned validation workspace')
    args = parser.parse_args()
    work = validation_directory()
    if work == ROOT / 'output/validation':
        raise ValueError('Use an owned validation workspace')
    output = Path(args.output).resolve()
    if not output.is_relative_to(work) or output == work:
        raise ValueError('Output must be inside the owned validation workspace')

    apk = Apk.current()
    constructors = Constructors(ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
    tables_source = constructors.source('Tables')
    mapping = dict((name, kind) for kind, name in
                   re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', tables_source))
    names = ('activity_airship_banner', 'function_limit')
    objects = {}
    for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
        if obj.type.name != 'TextAsset':
            continue
        name = obj.read().m_Name.rsplit('/', 1)[-1]
        if name not in {value + '.g' for value in names}:
            continue
        if name in objects:
            raise ValueError('Duplicate selected table: ' + name)
        objects[name] = obj
    data, sources = {}, {}
    for name in names:
        obj = objects[name + '.g']
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = constructors.table(raw, mapping[name])
        if not isinstance(rows, list):
            raise ValueError('Expected a table of records: ' + name)
        data[name] = rows
        sources[name] = {'object': apk.provenance(obj), 'object_sha256': digest(obj.get_raw_data()),
                         'payload_sha256': digest(raw), 'constructor': mapping[name], 'rows': len(rows)}
    enum_source = constructors.source('FunctionType')
    trade = re.findall(r'\bTrade\s*=\s*(\d+)', enum_source)
    if len(trade) != 1:
        raise ValueError('Cannot identify the official Trade function type')
    selected = [row for row in data['function_limit']
                if (row['Type'], row['Key0'], row['Key1']) == (int(trade[0]), 0, 0)]
    if len(selected) != 1:
        raise ValueError('Expected exactly one official Trade function condition')
    data['function_limit'] = selected
    source_names = ('Tables', 'TbActivityAirshipBannerInfo', 'ActivityAirshipBannerInfo',
                    'ActivityAirshipBannerInfoParser', 'TbFunctionLimitInfo', 'FunctionLimitInfo',
                    'TipDotFunctionUnlockConditionNode', 'AirShipBanner', 'FunctionType')
    provenance = {name: digest(constructors.source(name).encode('utf8')) for name in source_names}
    result = {'official_manifest': apk.version, 'sources': sources,
              'source_code_sha256': provenance, 'tables': data}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Decoded current airship tables to', output)


if __name__ == '__main__':
    main()
