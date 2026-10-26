"""Convert pinned swamp terrain materials in an isolated output directory."""
import argparse
from copy import deepcopy
import json
from pathlib import Path

from official_apk import ROOT
import prepare_official_city as city
import prepare_map_regions as regions
import prepare_map11_rendering as rendering
from prepare_map12_rendering import Map12Regions
from prepare_map_regions import save
from prepare_map11_dialogs import digest
from prepare_map13_dialogs import build as dialogs

GROUND = 'Assets/Partition/MapGround_13/MapGround_13_bare.prefab'


def build(destination):
    destination = destination.resolve()
    art = destination/'assets/official'
    reference = destination/'assets/reference/official-map'
    (art/'regions').mkdir(parents=True, exist_ok=True)
    (art/'map-water-masks').mkdir(parents=True, exist_ok=True)
    # The APK reader keeps its original source root. Only exporter destinations
    # change, so independent chats can inspect candidates without a shared write.
    city.ROOT, city.ART, city.REFERENCE = destination, art, destination/'assets/reference/official-city'
    regions.ROOT, regions.ART = destination, art
    rendering.ROOT, rendering.ART = destination, art
    exporter = Map12Regions()
    exporter.exclude_skeleton_meshes = True
    previous = {f'assets/official/regions/13.{suffix}': digest(ROOT/f'assets/official/regions/13.{suffix}') for suffix in ['json', 'geom']}
    result = exporter.region(13, {'ground_path': GROUND}, art/'regions')
    old = json.loads((ROOT/'assets/official/regions/13.json').read_text(encoding='utf8'))
    serialized = json.loads((art/'regions/13.json').read_text(encoding='utf8'))
    if serialized['navigation'] != old['navigation'] or serialized['placement'] != old['placement']:
        raise ValueError('Pinned swamp navigation/placement differs from the existing conversion')
    if serialized['skeleton_instances'] != old['skeleton_instances']:
        raise ValueError('Swamp skeleton instances changed; reconvert animation data before installation')
    skeletons_path = ROOT/'assets/official/regions/skeletons.json'
    skeletons = json.loads(skeletons_path.read_text(encoding='utf8'))
    for key, job in exporter.skeleton_jobs.items():
        if key not in skeletons or skeletons[key]['textures'] != job['textures'] or skeletons[key]['scale'] != job['scale']:
            raise ValueError('Original swamp skeleton binding differs: '+key)
    original_renderers = rendering.terrain_renderers(exporter, GROUND)
    water = deepcopy(json.loads((ROOT/'assets/official/map-water.json').read_text(encoding='utf8'))['13'])
    water_renderers = [r for r in original_renderers if r['active'] and any(m['shader'] == 'Universal Render Pipeline/2D/Sprite-Lit-CustomWater2' for m in r['materials'])]
    if len(water_renderers) != len(water['renderers']):
        raise ValueError('Original swamp water shader/renderer count differs')
    water['native_materials'] = {'shader': 'Universal Render Pipeline/2D/Sprite-Lit-CustomWater2', 'mask': rendering.bake_mask(exporter, 13, GROUND)}
    view = rendering.model_view(exporter)
    save(art/'map13-rendering.json', {'manifest': '854_156383', 'map': 13, 'water': water, 'model_views': {view['model']: view}})
    outputs = ['assets/official/regions/13.json', 'assets/official/regions/13.geom', 'assets/official/map13-rendering.json', 'assets/official/map-water-masks/13.png']
    receipt = ROOT/'assets/reference/official-map/materials-13-source-manifest.json'
    if receipt.exists():
        previous = json.loads(receipt.read_text(encoding='utf8'))['previous_outputs']
    save(reference/'materials-13-source-manifest.json', {
        'manifest': '854_156383', 'map': 13,
        'scope': 'Original swamp tile renderer materials, Water2 meshes and WaterMask draws; remove duplicate serialized skeleton meshes',
        'terrain_source': exporter.apk.provenance(exporter.locate(GROUND)[1]),
        'original_renderers': original_renderers, 'inputs': exporter.source_records,
        'resources': exporter.records, 'schemas': exporter.schemas.records,
        'lava_shaders': exporter.lava_programs,
        'previous_outputs': previous, 'outputs': {p: digest(destination/p) for p in outputs},
        'skeleton_catalog': {'path': 'assets/official/regions/skeletons.json', 'sha256': digest(skeletons_path), 'instances': len(result['skeleton_instances'])},
        'counts': result['counts'], 'geometry': result['geometry'],
        'limitations': ['Original WaterMask maximum blend rasterized at 64 samples/world unit; Unity framebuffer pixel identity is not claimed.', 'Water2 uses the existing original GLES unlit adaptation; shared Unity URP lighting limitations remain.'],
        'history_preserved': ['regions-source-manifest.json', 'water-source-manifest.json']})
    dialogs(destination)
    print('Converted pinned swamp materials, skeleton bindings and eight battle-stele dialogs', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, required=True)
    build(parser.parse_args().output_dir)
