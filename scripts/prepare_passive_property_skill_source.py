"""Recheck property-only profession skills against an explicit official source."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

import UnityPy
import xxhash

from official_apk import Apk
from official_bundle_reader import OfficialBundleReader
from official_source import OfficialSource, ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest())


def verify(item_id, manifest):
    selected = OfficialSource(manifest)
    code = selected.prepared_path / 'official-code'
    constructors = Constructors(code)
    runtime = ROOT / 'unity/Assets/Official/Combat'
    rules = json.loads((runtime / 'rules.json').read_bytes())
    skill_rules = json.loads((runtime / 'skill-rules.json').read_bytes())
    installed = next(row for row in rules['skill'] if row['ClassId'] == item_id)
    if installed['EcEntityId'] or installed['PassiveStatusIdList'] or not installed['PassivePropFactors']:
        raise ValueError('This verifier only accepts passive properties without a skill or status entity')
    table_inputs = []
    with OfficialBundleReader(selected) as reader:
        bundle = reader.read('DefaultPackage', 'defaultpackage_assets_config_luban.bundle')
        environment = UnityPy.load(Apk.decode(bundle.data))
        texts = {obj.read().m_Name: obj for obj in environment.objects if obj.type.name == 'TextAsset'}
        mappings = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}

        def table(name):
            obj = texts[name + '.g']
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            table_inputs.append(dict(name=name, path_id=str(obj.path_id),
                                     source=dict(bundle.provenance), payload_sha256=hashlib.sha256(raw).hexdigest()))
            return json.loads(json.dumps(constructors.table(raw, mappings[name])))

        current = next(row for row in table('skill') if row['ClassId'] == item_id)
        if current != installed:
            raise ValueError('Selected official passive skill differs from the installed item')
        item = next(row for row in table('item') if row['ClassId'] == item_id)
        previous_item = next(row for row in rules['item'] if row['ClassId'] == item_id)
        for field in ('RawIcon', 'ItemType', 'Rank', 'Quality', 'ElementType'):
            if item[field] != previous_item[field]:
                raise ValueError('Selected passive item presentation changed: ' + field)
        icon_receipt_path = ROOT / 'assets/reference/profession-ui/skill-icons-source-manifest.json'
        icon_receipt = json.loads(icon_receipt_path.read_bytes())
        binding = next(row for row in icon_receipt['bindings'] if row['item_id'] == item_id and row['kind'] == 'skill')
        sprite = next(row for row in icon_receipt['sprites'] if row['identity'] == binding['sprite_identity'])
        asset = next(row for row in read_asset_manifest(selected.manifest_bytes('DefaultPackage'))
                     if row['address'] == 'Assets/Image/' + item['RawIcon'])
        if asset != binding['original_asset']:
            raise ValueError('Selected passive icon asset binding changed')
        icon = reader.read('DefaultPackage', asset['bundle'])
        if hashlib.sha256(icon.data).hexdigest() != sprite['source']['bundle_sha256']:
            raise ValueError('Selected passive icon bundle differs from the converted original')
        if hashlib.sha256((ROOT / sprite['path']).read_bytes()).hexdigest() != sprite['sha256']:
            raise ValueError('Installed original passive icon changed')
        owners = [row for row in table('profession_base') if item_id in row['Skills'] + row.get('FightUsedSkills', [])]
        if not owners or any(row not in rules['profession_base'] for row in owners):
            raise ValueError('Selected official profession binding differs')
        groups = [row for row in table('entity_prop_group_level') if row['GroupId'] == current['PassiveGroupLevelPropId']]
        previous_groups = [row for row in skill_rules['tables']['entity_prop_group_level'] if row['GroupId'] == current['PassiveGroupLevelPropId']]
        prop_ids = {row['LevelPropId'] for row in groups} | {current['PassiveRankPropId'], current['PassiveRankFactorId']}
        prop_ids.discard(0)
        numeric = reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        props = decode_level_props(numeric.data, prop_ids, include_zero=True)
        defaults = [row for row in table('level_prop_default_value') if row['ClassId'] in prop_ids]
        # LevelPropParser.TryGetPropDict adds defaults only for absent keys.
        # Retain explicit zero entries so they never become a default value.
        for row in defaults:
            for values in props[str(row['ClassId'])]['rows'].values():
                for prop_id in row['PropTypes']:
                    values.setdefault(str(prop_id), row['DefaultValue'])
        # CalcSkillProps removes every property absent from PassivePropFactors.
        # Keep exactly the contributing columns in this item's local payload.
        wanted_properties = set(current['PassivePropFactors'])
        for values in props.values():
            values['rows'] = {level: {key: value for key, value in row.items() if key in wanted_properties}
                              for level, row in values['rows'].items()}
        language = reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile')
        raw = language.data
        count, = struct.unpack_from('<i', raw)
        start = 4 + count * 12
        keys = {xxhash.xxh64_intdigest(key.encode()): key for key in
                (f'item_{item_id}_name', f'item_{item_id}_func_desc')}
        wording = {}
        for index in range(count):
            text_id, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
            if text_id in keys:
                size, = struct.unpack_from('<H', raw, start + offset)
                wording[keys[text_id]] = raw[start + offset + 2:start + offset + 2 + size].decode('utf-8')
        fidelity = json.loads((ROOT / 'unity/Assets/Official/Skills/fidelity-texts.json').read_bytes())
        if len(wording) != len(keys) or any(fidelity[key] != value for key, value in wording.items()):
            raise ValueError('Selected official passive wording differs')
        names = ['BattleFormulaHandler', 'PropType', 'SkillInfo', 'SkillPropChunck', 'LevelPropParser']
        code_inputs = [identity(code / 'common-source/Common' / (name + '.cs')) for name in names]
        code_inputs += [identity(code / 'common-source' / ('Common' if (code / 'common-source/Common' / (name + '.cs')).is_file()
                                                        else 'Common.LubanGen') / (name + '.cs'))
                        for name in sorted(constructors.sources)]
        payload = dict(manifest=selected.version, item_id=item_id, skill=current, item=item,
                       tables=dict(entity_prop=[], entity_prop_group_level=groups), props=props)
        encoded = (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
        receipt = dict(manifest=selected.version, item_id=item_id, skill=current, item=item, professions=owners,
                    table_inputs=table_inputs, numeric_source=dict(numeric.provenance),
                    numeric_sha256=hashlib.sha256(numeric.data).hexdigest(),
                    numeric_rows_sha256=hashlib.sha256(json.dumps(props, sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
                    wording_source=dict(language.provenance), wording=wording,
                    icon=dict(source=dict(icon.provenance), asset=asset, sprite=identity(ROOT / sprite['path']),
                              conversion_receipt=identity(icon_receipt_path)),
                    previous_group_bindings=previous_groups,
                    current_group_bindings=groups, level_property_defaults=defaults,
                    output_sha256=hashlib.sha256(encoded).hexdigest(),
                    official_code=code_inputs, generator=identity(Path(__file__)),
                    level_property_decoder=identity(Path(decode_level_props.__code__.co_filename)))
        return receipt, encoded


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--item', type=int, required=True)
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    result, payload = verify(args.item, args.manifest)
    path = ROOT / f'assets/reference/battle-rules/passive-property-{args.item}-source-manifest.json'
    runtime = ROOT / f'unity/Assets/Game/Resources/Combat/SkillRules/{args.item}-passive.json'
    runtime_receipt = runtime.with_name(runtime.stem + '-source-manifest.json')
    binding = dict(manifest=args.manifest, output_sha256=result['output_sha256'],
                   source_manifest=path.relative_to(ROOT).as_posix())
    if args.write:
        path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        runtime.write_bytes(payload)
        runtime_receipt.write_text(json.dumps(binding, indent=2) + '\n', encoding='utf-8')
    elif json.loads(path.read_bytes()) != result or runtime.read_bytes() != payload or json.loads(runtime_receipt.read_bytes()) != binding:
        raise ValueError('Passive property source receipt or runtime payload differs')
    print('Official property-only skill verified:', args.item, args.manifest)


if __name__ == '__main__':
    main()
