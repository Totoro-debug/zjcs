"""Project a selected map's original interaction tables through the shared converter."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from prepare_mainline_map_definition import MainlineMapSource
from prepare_unity_map13_sources import ROOT, encode, meta, source_path, write
from prepare_unity_map14_interactions import interaction_payload


def record(path):
    return dict(path=source_path(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build(map_id):
    source = MainlineMapSource(map_id)
    try:
        root = ROOT / f'unity/Assets/Official/World/Map/{map_id}/Source'
        definition_path = ROOT / f'assets/reference/official-map/map{map_id}-definition-source-manifest.json'
        definition = json.loads(definition_path.read_bytes())
        if definition['mapClassId'] != map_id or definition['manifest'] != source.apk.version:
            raise ValueError('The map definition must use the selected original source')
        expected = {row['path']: row['sha256'] for row in definition['outputs']}
        paths = [root / name for name in ('templates.json', 'tables.json')]
        for path in paths:
            if record(path)['sha256'] != expected[source_path(path)]:
                raise ValueError('Preserve modified map definition input: ' + str(path))
        templates, original = (json.loads(path.read_bytes()) for path in paths)
        tables = {name: original[kind] for name, kind in (
            ('Map/map_region', 'TbMapRegionInfo'), ('Map/map_area', 'TbMapAreaInfo'),
            ('Map/entity_extend_parameter', 'TbEntityExtendParameterInfo'))}
        mapping = {name: kind for kind, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', source.constructors.source('Tables'))}
        for name in ('status_effect', 'Map/map_periodicity_random_award_interaction'):
            bundle = 'config_luban_' + name.rsplit('/', 1)[0].lower() if '/' in name else 'config_luban'
            tables[name], _ = source.table(bundle, name, mapping[name])
        regions = tables['Map/map_region']
        if any(row['ClassId'] != map_id for row in regions):
            raise ValueError('The selected definition includes another map region')
        hero_ids = [int(key) for key, components in templates.items()
                    if 'StageResultPeriodicityRandomAwardComponent' in components]
        payload = interaction_payload(templates, tables, map_id, len(regions), source.apk.version, hero_ids)
        targets = [(root / 'interaction-tables.json', tables),
            (ROOT / f'unity/Assets/Game/Resources/Map{map_id}/interactions.json', payload)]
        receipt_path = ROOT / f'assets/reference/official-map/map{map_id}-interactions-source-manifest.json'
        previous = json.loads(receipt_path.read_bytes()) if receipt_path.is_file() else None
        old_outputs = {row['path']: row['sha256'] for row in previous['outputs']} if previous else {}
        outputs = []
        for path, data in targets:
            raw = encode(data)
            if path.is_file() and path.read_bytes() != raw:
                if record(path)['sha256'] != old_outputs.get(source_path(path)):
                    raise ValueError('Preserve an existing unrelated interaction payload: ' + str(path))
            write(path, raw)
            meta(path)
            outputs += [record(path), record(Path(str(path) + '.meta'))]
        proof = dict(format=1, manifest=source.apk.version, mapClassId=map_id,
            definition_receipt=record(definition_path), definition_inputs=[record(path) for path in paths],
            inputs=source.inputs, official_code=source.checked_code(),
            templates_receipt_scope='The definition receipt retains the original template, component decoder and map table provenance; only the two additional tables are decoded here.',
            operation='Use the shared original hero result-index converter. Preserve original template component/index identities, ordered weights, status IDs, costs, source region order, empty DropItems and each area refresh group/interval. Do not create runtime readiness or replace a historical source version.',
            outputs=outputs, counts={key: len(payload[key]) for key in ('heroes', 'parameters', 'regions', 'areas')},
            tools=[record(path) for path in (Path(__file__), ROOT / 'scripts/prepare_mainline_map_definition.py',
                ROOT / 'scripts/prepare_unity_map14_interactions.py')],
            runtime_bindings=[dict(path=relative, hashLineEndings='LF',
                sha256=hashlib.sha256((ROOT / relative).read_bytes().replace(b'\r\n', b'\n')).hexdigest())
                for relative in ('unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs',
                    'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineStatuesModule.HeroStatues.cs')],
            remaining=['formal mainline profile and host bindings', 'statue UI and visual effects',
                'actual map pointer interaction, progression and save restore', 'Windows Player release'])
        write(receipt_path, encode(proof))
        print('Projected original interactions:', map_id, proof['counts'])
        return proof
    finally:
        source.apk.archive.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--map-id', type=int, required=True)
    build(parser.parse_args().map_id)
