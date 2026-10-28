"""Convert verified original Map13 story performance inputs and restore reference registries."""
import argparse
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_story_playable_inputs import CACHE, OUTPUT, RECEIPT
from prepare_unity_map13_water_entity_export import build, convert
import json
import os


if __name__ == "__main__":
    if not os.environ.get("ZJCS_VALIDATION_DIR"):
        raise ValueError("Set ZJCS_VALIDATION_DIR to an owned GUID validation workspace before exporting story assets")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true")
    if parser.parse_args().convert:
        convert(CACHE)
    receipt = json.loads(RECEIPT.read_bytes())
    build(receipt_path=RECEIPT, component_path=OUTPUT, roots=receipt["roots"],
          cache_root=CACHE, proof_path=ROOT / "assets/reference/official-map/map13-native-story-playable-export-source-manifest.json",
          expected_counts=None, entry_path=Path(__file__).resolve())
