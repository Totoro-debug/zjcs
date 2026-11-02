"""Bind the selected original whale-island ground through the common binder."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_map15_native_ground import GroundBinding, GroundSource, sha
from validation_workspace import validation_directory


if __name__ == '__main__':
    root = ROOT / 'unity/Assets/Official/World/Map/16'
    raw = (root / 'map16.json').read_bytes()
    receipt = json.loads((root / 'source-manifest.json').read_bytes())
    if receipt['manifest'] != '884_156692' or receipt['mapClassId'] != 16 or receipt['output']['sha256'] != sha(raw):
        raise ValueError('Verified selected Map16 definition is required')
    definition = json.loads(raw)
    source = GroundSource(16, definition['OriginalGroundPrefab'], 'defaultpackage_assets_prefabs_map_map_16.bundle')
    binder = GroundBinding(validation_directory(), source)
    binder.inputs[(root / 'source-manifest.json').relative_to(ROOT).as_posix()] = sha((root / 'source-manifest.json').read_bytes())
    binder.inputs[(root / 'map16.json').relative_to(ROOT).as_posix()] = sha(raw)
    tool = Path(__file__)
    binder.inputs[tool.relative_to(ROOT).as_posix()] = sha(tool.read_bytes())
    binder.build_ground()
