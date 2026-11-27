"""Rebuild the existing ordinary-item detail closure without upgrading its official source."""
import argparse
from pathlib import Path

from official_apk import ROOT
from prepare_demon_result_native import DemonResultBinding, NAME, install
from retained_native_binding import rebuild
from validation_workspace import validation_directory


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--record-unchanged', action='store_true')
    args = parser.parse_args()
    rebuild(validation_directory(), ROOT / 'assets/reference' / NAME, DemonResultBinding,
        generator=Path(__file__), record_unchanged=args.record_unchanged, install=install if args.install else None)
