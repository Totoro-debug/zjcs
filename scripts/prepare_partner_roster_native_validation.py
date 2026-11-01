"""Prepare original roster hierarchy, original cards and actual local roster services."""
import json
from pathlib import Path
import re
import shutil

from prepare_partner_gift_native_validation import main as prepare_native
from prepare_partner_roster_ui_validation import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    prepare_native()
    project = work / 'unity'
    editor = project / 'Assets/Editor'
    for name in ('PartnerGiftNativeValidation.cs', 'FZY4K.ttf', 'FZY4K.ttf.meta'):
        (editor / name).unlink(missing_ok=True)
    for metadata in (project / 'Assets/Game/Runtime').rglob('*.meta'):
        if not Path(str(metadata)[:-5]).exists():
            metadata.unlink()
    existing = {}
    for metadata in (project / 'Assets').rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', metadata.read_bytes())
            if match:
                if match[1] in existing:
                    raise ValueError('Duplicate validation GUID before adding cards')
                existing[match[1]] = asset
    card = ROOT / 'unity/Assets/Official/Partners/UI/Card'
    for metadata in card.rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if not asset.is_file():
            continue
        key = re.search(rb'(?m)^guid: ([0-9a-f]{32})', metadata.read_bytes())[1]
        if key in existing:
            if asset.read_bytes() != existing[key].read_bytes():
                raise ValueError('Shared original asset has different bytes: ' + str(asset))
            continue
        target = project / 'Assets/PartnerCard' / asset.relative_to(card)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(asset, target)
        shutil.copy2(metadata, Path(str(target) + '.meta'))
        existing[key] = target
    shutil.copy2(ROOT / 'scripts/fixtures/partner_roster_native_validation.cs', editor)
    print(project)


if __name__ == '__main__':
    main()
