"""Convert verified original Dragon Country performances and restore reference registries."""
import argparse
import json
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_story_playable_inputs import CACHE, OUTPUT, RECEIPT
from prepare_unity_map13_water_entity_export import build, convert

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true")
    if parser.parse_args().convert:
        convert(CACHE)
    receipt = json.loads(RECEIPT.read_bytes())
    if receipt["mapClassId"] != 14:
        raise ValueError("Dragon Country performance inputs must identify Map14")
    build(receipt_path=RECEIPT, component_path=OUTPUT, roots=receipt["roots"],
          cache_root=CACHE, proof_path=ROOT / "assets/reference/official-map/map14-native-story-playable-export-source-manifest.json",
          expected_counts=None, entry_path=Path(__file__).resolve(), map_class_id=14, subject="story performance")
