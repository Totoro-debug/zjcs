"""Record item-use component validation separately from native asset recovery."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_pet_item_detail_native import check as check_native

RECEIPT = ROOT / 'assets/reference/pet-item-detail-runtime-source-manifest.json'


def build():
    check_native()
    paths = [ROOT / path for path in ('assets/reference/pet-item-detail-native-source-manifest.json',
        'scripts/fixtures/item_use_native_validation.cs', 'scripts/test_item_use_native.ps1',
        'scripts/prepare_pet_item_detail_runtime.py')]
    return dict(inputs={path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths},
        scope='Original item-use and choice component fixture, process-local pointer input and isolated real save transactions. Source navigation, nested item details, award presentation and production entry remain separate integration work.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build()
    if args.check:
        if json.loads(RECEIPT.read_bytes()) != result:
            raise ValueError('Item-use validation sources differ')
        print('Original item-use validation source verified')
    else:
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
