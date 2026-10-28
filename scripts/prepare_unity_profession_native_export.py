"""Restore and verify profession prefab managed-reference registries in the conversion cache.

This source conversion does not install unmigrated runtime scripts into Unity.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/unitypy-work'))
from prepare_unity_profession_native_inputs import CACHE, BUNDLE
from prepare_unity_map13_water_entity_export import build as verify_export, convert


def build(run_converter=False):
    cache = CACHE / 'typed'
    receipt = cache / 'inputs/source-manifest.json'
    source = json.loads(receipt.read_bytes())
    if (source['root'] != BUNDLE or source['restored_component_counts'].get(BUNDLE) != 8357
            or source['restored_component_counts'].get('defaultpackage_assets_prefabs_ui_rule.bundle') != 241):
        raise ValueError('Profession native input coverage changed; inspect the original prefab inventory.')
    if run_converter:
        convert(cache)
    proof = ROOT / 'assets/reference/profession-ui/native-export-source-manifest.json'
    verify_export(receipt_path=receipt, component_path=receipt,
                  roots=source.get("roots", [BUNDLE]), cache_root=cache, proof_path=proof,
                  expected_counts=None, entry_path=Path(__file__),
                  map_class_id=None, subject='profession UI')
    compact = {key: value for key, value in source.items()
               if key not in ('components', 'managed_reference_unions')}
    compact['component_count'] = len(source['components'])
    compact['managed_reference_schema_count'] = len(source['managed_reference_unions'])
    compact['component_cache'] = dict(path=receipt.relative_to(ROOT).as_posix(),
                                    sha256=hashlib.sha256(receipt.read_bytes()).hexdigest())
    target = proof.with_name('native-input-source-manifest.json')
    target.write_text(json.dumps(compact, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--convert', action='store_true')
    build(parser.parse_args().convert)
