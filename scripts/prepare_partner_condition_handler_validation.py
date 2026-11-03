"""Add original subscription dispatch checks to the existing imported Play fixture."""
import argparse
import shutil
from pathlib import Path

from official_apk import ROOT
from validation_workspace import require_temporary_unity_project


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    args = parser.parse_args()
    project = require_temporary_unity_project(args.project)
    for relative in ('Assets/Game/Runtime/Official/Partners/PartnerConditionHandler.cs',
            'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotConditions.cs'):
        for suffix in ('', '.meta'):
            shutil.copy2(ROOT / 'unity' / (relative + suffix), project / (relative + suffix))
    shutil.copy2(ROOT / 'scripts/fixtures/partner_condition_handler_validation.cs',
        project / 'Assets/Editor/PartnerConditionHandlerValidation.cs')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8')
    if '        PartnerConditionHandlerValidation.Check();' not in text:
        anchor = '        PartnerTipDotPlayerValidation.Check();'
        if text.count(anchor) != 1: raise ValueError('Expected verified saved-player/original-window fixture')
        host.write_text(text.replace(anchor, anchor + '\n        PartnerConditionHandlerValidation.Check();'), 'utf8', newline='\n')
    print('Prepared original condition dispatch and saved-player/window regressions')


if __name__ == '__main__': main()
