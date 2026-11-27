"""Replay an existing acceleration binding against its verified retained raw inputs."""
import argparse
from pathlib import Path

from prepare_home_fast_hangup_native import HomeFastHangUpBinding, RECEIPT, install
from retained_native_binding import rebuild as rebuild_binding
from validation_workspace import validation_directory


def rebuild(work, install_result=False, record_unchanged=False):
    rebuild_binding(work, RECEIPT, HomeFastHangUpBinding, generator=Path(__file__),
        record_unchanged=record_unchanged, install=install if install_result else None)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--record-unchanged', action='store_true',
        help='Record rebuilt controller bindings only; require identical outputs and preserve all resource hashes')
    args = parser.parse_args()
    rebuild(validation_directory(), args.install, args.record_unchanged)
