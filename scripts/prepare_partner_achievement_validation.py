"""Reuse the owned imported original-window project for achievement regressions."""
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
    relatives = ['Assets/Game/Runtime/Official/Partners/PartnerFriendshipAchievementRules.cs',
        'Assets/Game/Runtime/Official/Partners/Zjcs.Official.Partners.asmdef',
        'Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs',
        'Assets/Game/Runtime/Offline/Progression/OfflinePartnerAchievementRecord.cs',
        'Assets/Game/Runtime/Offline/Progression/OfflinePartnerAchievementService.cs']
    for relative in relatives:
        source = ROOT / 'unity' / relative
        meta = metadata(source)
        if source.name in ('PartnerFriendshipAchievementRules.cs', 'OfflinePartnerAchievementRecord.cs', 'OfflinePartnerAchievementService.cs'):
            canonical = ('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n').encode()
            if meta.read_bytes() != canonical: meta.write_bytes(canonical)
        for suffix in ('', '.meta'): shutil.copy2(ROOT / 'unity' / (relative + suffix), project / (relative + suffix))
    shutil.copy2(ROOT / 'scripts/fixtures/partner_achievement_validation.cs', project / 'Assets/Editor/PartnerAchievementValidation.cs')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8'); addition = '        PartnerAchievementValidation.Check();'
    if addition not in text:
        anchor = '        PartnerProfileTransactionValidation.Check();'
        if text.count(anchor) != 1: raise ValueError('Expected original-window/profile transaction fixture')
        host.write_text(text.replace(anchor, anchor + '\n' + addition), 'utf8', newline='\n')
    print('Prepared actual original achievement rules, saved transactions and window regressions')


if __name__ == '__main__': main()
