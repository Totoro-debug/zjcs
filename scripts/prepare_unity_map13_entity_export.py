"""Convert verified Map13 entity inputs and restore original reference records.

Outputs remain in the cache until original runtime and explicit native bindings
are ready. This command does not import runnable entities or complete the map.
"""
import argparse
import json
from pathlib import Path

from prepare_unity_map13_entity_inputs import CACHE, OUTPUT, RECEIPT
from official_apk import ROOT
from prepare_unity_map13_water_entity_export import build, convert


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true")
    if parser.parse_args().convert:
        convert(CACHE)
    receipt = json.loads(RECEIPT.read_bytes())
    build(receipt_path=RECEIPT, component_path=OUTPUT,
          roots=receipt["selection"]["roots"] + receipt["selection"]["dynamic_textures"]["roots"],
          cache_root=CACHE, proof_path=ROOT / "assets/reference/official-map/map13-native-entity-export-source-manifest.json",
          expected_counts=None, entry_path=Path(__file__).resolve())
