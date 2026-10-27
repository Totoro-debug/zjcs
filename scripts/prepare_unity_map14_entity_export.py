"""Convert verified Map14 entity inputs and restore original reference records.

Outputs remain in the cache until original runtime and explicit native bindings
are ready. This command does not import runnable entities or complete the map.
"""
import argparse
import json
from pathlib import Path

from prepare_unity_map14_entity_inputs import CACHE, OUTPUT, RECEIPT
from prepare_unity_map14_sources import encode, write
from official_apk import ROOT
from prepare_unity_map13_water_entity_export import build, convert


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true")
    args = parser.parse_args()
    receipt = json.loads(RECEIPT.read_bytes())
    if receipt["mapClassId"] != 14 or receipt["selection"]["mapClassId"] != 14:
        raise ValueError("Map14 entity conversion must use Map14 inputs")
    if json.loads(OUTPUT.read_bytes())["mapClassId"] != 14:
        raise ValueError("Map14 component records must identify Map14")
    if args.convert:
        convert(CACHE)
    proof = ROOT / "assets/reference/official-map/map14-native-entity-export-source-manifest.json"
    build(receipt_path=RECEIPT, component_path=OUTPUT,
          roots=receipt["selection"]["roots"] + receipt["selection"]["dynamic_textures"]["roots"],
          cache_root=CACHE, proof_path=proof,
          expected_counts=None, entry_path=Path(__file__).resolve())
    # The reused verifier writes its historical Map13 label. The checked input
    # receipt and this entry point own the map identity; no payload is changed.
    result = json.loads(proof.read_bytes())
    result["mapClassId"] = receipt["mapClassId"]
    result["operation"] += " Map identity is supplied by the verified Map14 input receipt and entry point."
    write(proof, encode(result))
