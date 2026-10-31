"""Prepare an isolated Play Mode fixture for the actual gift/property controllers."""
import json
import os
from pathlib import Path
import shutil

from prepare_partner_roster_ui_validation import prepare, ROOT


if __name__ == '__main__':
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if Path(os.environ['ZJCS_REPOSITORY_ROOT']).resolve() != ROOT or not work.is_relative_to(ROOT / 'output/validation'):
        raise ValueError('Owned validation workspace required')
    project = prepare(work)
    editor = project / 'Assets/Editor'
    (editor / 'PartnerRosterUiValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_ui_validation.cs', editor / 'PartnerGiftUiValidation.cs')
    asmdef = editor / 'Validation.asmdef'
    data = json.loads(asmdef.read_bytes())
    data['references'] += ['Zjcs.Official.PartnerGiftUI', 'Zjcs.Official.World.NativeLabelUI']
    asmdef.write_text(json.dumps(data), 'utf8')
    print(project)
