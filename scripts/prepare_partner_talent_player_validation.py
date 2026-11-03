"""Validate current-save talent projections alongside the original full window."""
import json
import shutil

from official_apk import ROOT
from prepare_partner_talent_validation import main as prepare_rules
from prepare_partner_conversation_window_validation import replace_once
from validation_workspace import validation_directory


def main():
    prepare_rules()
    project = validation_directory() / 'star-god-unity'
    definition = project / 'Assets/Editor/Validation.asmdef'
    assembly = json.loads(definition.read_bytes())
    assembly['references'] = list(dict.fromkeys(assembly['references'] + ['Zjcs.Official.PartnerGiftControllerUI']))
    definition.write_text(json.dumps(assembly, indent=2) + '\n', 'utf8', newline='\n')
    relative = 'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTalentPlayer.cs'
    for suffix in ('', '.meta'):
        shutil.copy2(ROOT / 'unity' / (relative + suffix), project / (relative + suffix))
    shutil.copy2(ROOT / 'scripts/fixtures/partner_talent_player_validation.cs', project / 'Assets/Editor/PartnerTalentPlayerValidation.cs')
    path = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = path.read_text('utf8')
    if '        PartnerTalentPlayerValidation.Check();' not in text:
        text = replace_once(text, '        PartnerTalentValidation.Check();',
            '        PartnerTalentValidation.Check();\n        PartnerTalentPlayerValidation.Check();')
        path.write_text(text, 'utf8', newline='\n')
    print('Prepared actual local profile and original talent predicate integration')


if __name__ == '__main__': main()
