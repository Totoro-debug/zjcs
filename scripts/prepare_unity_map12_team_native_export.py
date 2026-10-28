"""Export prepared original team resources in a private, hidden converter."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_sources import BUNDLE
from prepare_unity_map13_water_entity_export import build, convert
from validation_workspace import validation_directory


class ConversionPath(type(Path())):
    """Use Win32 long paths for IO while retaining normal source receipt paths."""
    def __fspath__(self):
        value = super().__fspath__()
        if os.name == 'nt' and self.is_absolute() and not value.startswith('\\\\?\\'):
            return '\\\\?\\UNC\\' + value[2:] if value.startswith('\\\\') else '\\\\?\\' + value
        return value


def run(stage, convert_first=False):
    stage = stage.resolve()
    workspace = validation_directory()
    if workspace == ROOT / 'output/validation' or not stage.is_relative_to(workspace):
        raise ValueError('Use prepared team inputs in the calling owned validation workspace')
    stage = ConversionPath(stage)
    original_receipt = stage / 'inputs/source-manifest.json'
    source = json.loads(original_receipt.read_bytes())
    if source['manifest'] != '871_156598' or source['mapClassId'] != 12 or source['roots'] != [BUNDLE]:
        raise ValueError('Original team converter input selection differs')
    fields = stage / source['field_record']['file']
    if sha(fields.read_bytes()) != source['field_record']['sha256']:
        raise ValueError('Original complete team field records changed')
    # The shared verifier consumes explicit output hashes. Keep the original
    # preparation receipt intact and add its complete field file to this input.
    source['outputs'] = [dict(path=source_path(fields), sha256=sha(fields.read_bytes()))]
    source['team_preparation_receipt'] = dict(path=source_path(original_receipt), sha256=sha(original_receipt.read_bytes()))
    verifier_receipt = stage / 'native-export-input.json'
    verifier_receipt.write_bytes(encode(source))
    if convert_first:
        convert(stage)
    build(receipt_path=verifier_receipt, component_path=fields, roots=[BUNDLE], cache_root=stage,
          proof_path=stage / 'native-export-proof.json', expected_counts=None,
          entry_path=Path(__file__), map_class_id=12, subject='original stele team UI')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage-dir', type=Path, required=True)
    parser.add_argument('--convert', action='store_true', help='Run a private hidden AssetRipper before verification')
    args = parser.parse_args()
    run(args.stage_dir, args.convert)
