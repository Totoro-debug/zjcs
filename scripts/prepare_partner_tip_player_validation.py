"""Exercise the saved tip player in the existing original-window Play fixture."""
import argparse
import json
import shutil
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata
from prepare_partner_profile_validation import main as prepare_profiles
from validation_workspace import require_temporary_unity_project


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    args = parser.parse_args()
    project = require_temporary_unity_project(args.project)
    prepare_profiles()
    assembly = 'Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef'
    shutil.copy2(ROOT / 'unity' / assembly, project / assembly)
    gift_player = 'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftPlayer.cs'
    shutil.copy2(ROOT / 'unity' / gift_player, project / gift_player)
    editor_asmdef, = (project / 'Assets/Editor').glob('*.asmdef')
    definition = json.loads(editor_asmdef.read_bytes())
    if 'Zjcs.Official.PartnersUI' not in definition['references']:
        definition['references'].append('Zjcs.Official.PartnersUI')
        editor_asmdef.write_text(json.dumps(definition, indent=2) + '\n', 'utf8', newline='\n')
    relative = 'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotPlayer.cs'
    source = ROOT / 'unity' / relative
    metadata(source)
    for suffix in ('', '.meta'):
        shutil.copy2(Path(str(source) + suffix), project / (relative + suffix))
    shutil.copy2(ROOT / 'scripts/fixtures/partner_tipdot_player_validation.cs', project / 'Assets/Editor/PartnerTipDotPlayerValidation.cs')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8')
    if '        PartnerTipDotPlayerValidation.Check();' not in text:
        anchor = '        PartnerProfileTransactionValidation.Check();'
        if text.count(anchor) != 1: raise ValueError('Expected verified profile/window fixture')
        host.write_text(text.replace(anchor, anchor + '\n        PartnerTipDotPlayerValidation.Check();'), 'utf8', newline='\n')
    print('Prepared actual saved tip player and original window regressions')


if __name__ == '__main__': main()
