"""Record current resource conversion outputs and their exact source identities."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT


def record(path):
    data = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(data), sha256=hashlib.sha256(data).hexdigest())


def build(cache):
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
                  preservation='Existing native exports, original raw objects, source CAB/PathID identities, unmatched historical inputs and previous converted payloads remain on disk.',
                  limitations=['Conversion cache assets have not replaced existing game runtime assets.',
                               'Component fields include exact source PPtrs; native object rebinding remains a module integration task.',
                               'Native texture GUIDs and original CAB/PathID pairs are explicit in the texture receipt.',
                               'This receipt does not declare an external backup or a completed game feature.'])
    output = ROOT / 'assets/reference/official-map/current-resource-windows-source-manifest.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(output, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    build(parser.parse_args().cache.resolve())
