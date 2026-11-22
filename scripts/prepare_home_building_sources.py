"""Recover original furniture descriptions and condition dependencies for building pages.

This reference payload never replaces an installed runtime rule or marks a
building as playable. The selected APK/CDN reader verifies every input.
"""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_map16_story_source import decode_localization


DEST = ROOT / 'assets/reference/home-buildings'
TABLES = {
    'furniture': 'TbFurnitureInfo',
    'conditions': 'TbConditionInfo',
    'hang_up': 'TbHangUpInfo',
}
CONTROLLERS = (
    'FurnitureBase', 'BedFurniture', 'FurnitureBedDialog', 'DeskFurniture',
    'CartFurniture', 'FurnitureCartDialog', 'StoreBoxFurniture',
    'BagualuFurniture', 'AirshipFurniture', 'ChestGachaFurniture',
    'GachaFurniture', 'ShopFurniture', 'AstrologicalDestinyWindowFurniture',
    'HomeFruitTreeProduceFruit', 'HomeFruitTreePet', 'OutputAdditionDetailButton',
    'PopTipOutputAddition', 'OutputAdditionItem', 'OutputAdditionPropItem',
)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode('utf8')


def recover():
    apk = Apk.current()
    try:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        code_manifest = code.parent / 'code-manifest.json'
        if json.loads(code_manifest.read_bytes())['manifest'] != apk.version:
            raise ValueError('Selected building code and resource sources differ')
        constructors = Constructors(code)
        tables, inputs = {}, []
        for name, constructor in TABLES.items():
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = constructors.table(raw, constructor)
            inputs.append(dict(table=name, constructor=constructor,
                source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw)))

        enum = code / 'common-source/Common/EFurniture.cs'
        enum_names = {int(value): name for name, value in re.findall(
            r'^\s*(\w+) = (\d+),?$', enum.read_text('utf-8-sig'), re.M)}
        furniture = tables['furniture']
        if (len({row['ClassId'] for row in furniture}) != len(furniture)
                or len({row['FurnitureType'] for row in furniture}) != len(furniture)):
            raise ValueError('Ambiguous original furniture mapping')
        if any(row['FurnitureType'] not in enum_names for row in furniture):
            raise ValueError('Original furniture enum is incomplete')
        condition_rows = {row['ClassId']: row for row in tables['conditions']}
        selected_conditions = {}

        def visit(node):
            if not isinstance(node, dict):
                return
            if node.get('_type') in ('CompositeOrCondition', 'CompositeAndCondition'):
                for condition_id in node['Ids']:
                    if condition_id in selected_conditions:
                        continue
                    row = condition_rows[condition_id]
                    selected_conditions[condition_id] = row
                    visit(row['Condition'])
            for value in node.values():
                if isinstance(value, dict):
                    visit(value)

        keys = set()
        for row in furniture:
            keys.update(f"item_{row['ClassId']}_{suffix}" for suffix in ('name', 'desc'))
            for field in ('UnlockCondition', 'RepairNoticeCondition', 'AutoRepairCondition'):
                visit(row[field])
        texts, language_inputs = decode_localization(apk, keys,
            {f"item_{row['ClassId']}_name" for row in furniture})
        data = dict(buildings=[dict(enum=enum_names[row['FurnitureType']],
            name=texts[f"item_{row['ClassId']}_name"],
            description=texts.get(f"item_{row['ClassId']}_desc"),
            description_key=f"item_{row['ClassId']}_desc", furniture=row,
            hang_ups=[item for item in tables['hang_up'] if item['BindFurniture'] == row['FurnitureType']])
            for row in sorted(furniture, key=lambda row: row['FurnitureType'])],
            enum_without_furniture=[dict(type=key, enum=value) for key, value in sorted(enum_names.items())
                if value != 'None' and key not in {row['FurnitureType'] for row in furniture}],
            conditions=[selected_conditions[key] for key in sorted(selected_conditions)])
        sources = {}
        paths = [enum, code_manifest, Path(__file__), ROOT / 'official-source.json',
            ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_tables.py',
            ROOT / 'scripts/prepare_map16_story_source.py']
        paths.extend(code / 'client-source' / (name + '.cs') for name in CONTROLLERS)
        for name, source in constructors.sources.items():
            if not source:
                continue
            path = code / 'common-source/Common' / (name + '.cs')
            if not path.is_file():
                path = code / 'common-source/Common.LubanGen' / (name + '.cs')
            paths.append(path)
        for path in paths:
            sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        payload = encode(data)
        proof = dict(manifest=apk.version, sources=sources, tables=inputs,
            language=language_inputs,
            output=dict(path=(DEST / 'original-functions.json').relative_to(ROOT).as_posix(),
                bytes=len(payload), sha256=sha(payload)),
            scope='Reference descriptions, original furniture rows and condition closure only; no runtime migration or completion assertion.')
        DEST.mkdir(parents=True, exist_ok=True)
        (DEST / 'original-functions.json').write_bytes(payload)
        (DEST / 'source-manifest.json').write_bytes(encode(proof))
        print('Recovered original building descriptions and condition dependencies:', len(furniture))
        return data
    finally:
        apk.reader.close()


if __name__ == '__main__':
    recover()
