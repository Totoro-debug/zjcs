"""Exercise the real profile graph with saved partner configuration services."""
import argparse
import shutil
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata
from validation_workspace import require_temporary_unity_project


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    args = parser.parse_args()
    project = require_temporary_unity_project(args.project)
    relative = 'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotServices.cs'
    target = ROOT / 'unity' / relative
    meta = metadata(target)
    meta.write_text('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    for suffix in ('', '.meta'):
        shutil.copy2(ROOT / 'unity' / (relative + suffix), project / (relative + suffix))
    shutil.copy2(ROOT / 'scripts/fixtures/partner_tipdot_services_validation.cs', project / 'Assets/Editor/PartnerTipDotServicesValidation.cs')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8')
    addition = '        yield return PartnerTipDotServicesValidation.Check();'
    if addition not in text:
        anchor = '        yield return PartnerTipDotRuntimeValidation.Check();'
        if text.count(anchor) != 1: raise ValueError('Expected original graph runtime regression fixture')
        host.write_text(text.replace(anchor, anchor + '\n' + addition), 'utf8', newline='\n')
    print('Prepared saved services, actual profile graph and original-window regressions')


if __name__ == '__main__': main()
