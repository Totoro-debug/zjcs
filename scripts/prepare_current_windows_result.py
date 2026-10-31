"""Record current resource conversion outputs and their exact source identities."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT


def record(path):
    data = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(data), sha256=hashlib.sha256(data).hexdigest())


def build(cache, validation_dir=None):
    native_path = cache / 'native-export-all-source-manifest.json'
    texture_path = cache / 'all-assets/windows-textures/source-manifest.json'
    fields_path = cache / 'component-fields-all/source-manifest.json'
    native = json.loads(native_path.read_bytes())
    texture = json.loads(texture_path.read_bytes())
    fields = json.loads(fields_path.read_bytes())
    if not all(r['completed'] for r in [native, texture, fields]):
        raise ValueError('A required conversion is incomplete')
    result = dict(format=1, manifest=native['manifest'],
                  inputs=[record(cache / 'source-manifest.json'), record(cache / 'all-assets/inventory.json')],
                  native=dict(receipt=record(native_path), files=len(native['outputs']),
                              directory=(cache / native['export_directory']).relative_to(ROOT).as_posix()
                              if not Path(native['export_directory']).is_absolute() else Path(native['export_directory']).relative_to(ROOT).as_posix()),
                  textures=dict(receipt=record(texture_path), **texture['counts'], unresolved=len(texture['unresolved'])),
                  components=dict(receipt=record(fields_path), types=len(fields['required_types']), objects=len(fields['objects']),
                                  managed_references=sum(r['managed_references'] for r in fields['objects']), unresolved=len(fields['unresolved'])),
                  generators=[record(ROOT / 'scripts' / name) for name in ['prepare_current_asset_inventory.py',
                              'convert_current_texture_mips.py', 'recover_current_component_fields.py', 'prepare_current_windows_result.py']],
                  preservation='Existing native exports, original current raw objects, source CAB/PathID identities, existing unmatched legacy files and previous converted payloads remain on disk.',
                  limitations=['Conversion cache assets have not replaced existing game runtime assets.',
                               'Component fields include exact source PPtrs; native object rebinding remains a module integration task.',
                               'Native texture GUIDs and original CAB/PathID pairs are explicit in the texture receipt.',
                               'This receipt does not declare an external backup or a completed game feature.'])
    shader_path = cache / 'all-assets/windows-shaders/source-manifest.json'
    if shader_path.is_file():
        shader = json.loads(shader_path.read_bytes())
        result['shaders'] = dict(receipt=record(shader_path), source_files=len(shader['shaders']),
                                 retained_program_cases=sum(len(r['programs']) for r in shader['shaders']),
                                 stripped_program_objects=sum(not r['programs'] for r in shader['shaders']),
                                 native_engine_bindings=len(shader['native_engine_bindings']), unresolved=len(shader['unresolved']),
                                 completed=shader['completed'], engine_receipt=record(cache / 'windows-engine/source-manifest.json'))
        result['generators'] += [record(ROOT / 'scripts' / name) for name in ['convert_current_windows_shaders.py', 'current_shader_programs.py', 'prepare_current_windows_engine.py',
                                   'current_vulkan_shader.py',
                                   'prepare_current_windows_validation.py', 'current_resource_windows_validation.cs',
                                   'current_resource_texture_validation.cs', 'current_resource_engine_probe.cs',
                                   'current_resource_shader_render_validation.cs', 'current_resource_particle_render_validation.cs']]
        result['shaders']['vulkan_only_cases'] = sum(p['gpu_type'] == 'kShaderGpuProgramSPIRV'
                for r in shader['shaders'] for p in r['programs'])
        result['shaders']['cross_compiler_tools'] = record(ROOT / '.tools/spirv-cross/source-manifest.json')
        result['shaders']['program_coverage'] = record(cache / 'all-assets/vulkan-program-coverage.json')
        if not shader['completed'] or shader['unresolved']:
            raise ValueError('Windows shader conversion is incomplete')
    if validation_dir:
        shader_results = json.loads((validation_dir / 'shader-results.json').read_bytes())
        catalog = {r['path']:r for r in shader['shaders'] if r['programs']}
        checked = {r['path']:r for r in shader_results['shaders']}
        if checked.keys() != catalog.keys() or any(not checked[p]['success'] or checked[p]['sha256'] != r['sha256']
                or checked[p]['compiled_stages'] != sum(3 if case.get('geometry') else 2 for case in r['programs']) for p, r in catalog.items()):
            raise ValueError('Windows shader compilation is incomplete')
        texture_results = json.loads((validation_dir / 'texture-results.json').read_bytes())
        texture_catalog = json.loads((validation_dir / 'texture-catalog.json').read_bytes())['textures']
        current_textures = {r['outputs'][0]['path']:r for r in texture['textures']}
        if not all(r['success'] for r in texture_results['textures']) or len(texture_results['textures']) != len(texture_catalog):
            raise ValueError('Native texture GPU checks are incomplete')
        for item in texture_catalog:
            if item['sha256'] != current_textures[item['path']]['outputs'][0]['sha256']:
                raise ValueError('Validated texture source differs')
        engine_results = json.loads((validation_dir / 'engine-results.json').read_bytes())
        render_result = json.loads((validation_dir / 'render-result.json').read_bytes())
        if not render_result['success'] or (engine_results['shaders'], engine_results['computes']) != (4, 6):
            raise ValueError('Native engine references or shader rendering are incomplete')
        render_path = (validation_dir / 'render-shader-path.txt').read_text().strip()
        rendered = catalog[render_path]
        if record(validation_dir / 'texture-unity' / render_path)['sha256'] != rendered['sha256']:
            raise ValueError('Rendered shader snapshot differs')
        particle_result = json.loads((validation_dir / 'fixes/particle-render-result.json').read_bytes())
        particle = next(r for r in shader['shaders'] if any(p['gpu_type'] == 'kShaderGpuProgramSPIRV' for p in r['programs']))
        if not particle_result['success'] or particle_result['sha256'] != particle['sha256']:
            raise ValueError('Vulkan-only particle rendering is incomplete')
        result['validation_result'] = dict(graphics_device=shader_results['graphics_device'], shader_objects=len(checked),
                compiled_stages=sum(r['compiled_stages'] for r in checked.values()), texture_sample_objects=len(texture_catalog),
                texture_mip_faces=sum(r['checked_mips'] for r in texture_results['textures']), native_engine=engine_results,
                rendered_shader=dict(path=render_path, sha256=rendered['sha256'], **render_result),
                rendered_vulkan_only_pass=particle_result,
                scope='D3D compilation covers every retained ported program case. Native texture GPU samples cover original formats, mip counts, color spaces and texture types. Engine checks cover builtin identities and available D3D kernel metadata; game integration and compute dispatch are outside this result.')
    output = ROOT / 'assets/reference/official-map/current-resource-windows-source-manifest.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(output, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--validation-dir', type=Path)
    args = parser.parse_args()
    build(args.cache.resolve(), args.validation_dir.resolve() if args.validation_dir else None)
