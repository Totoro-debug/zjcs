"""Reuse the owned portrait-window project to exercise actual achievement clicks."""
import argparse
import json
import shutil
from pathlib import Path
from official_apk import ROOT
from prepare_partner_input_dialog import metadata
from validation_workspace import require_temporary_unity_project


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args()
    project = require_temporary_unity_project(args.project)
    proof = json.loads((ROOT / 'assets/reference/partner-achievement-ui-source-manifest.json').read_bytes())
    for relative in proof['outputs']:
        if not relative.startswith('unity/'): raise ValueError('Expected runtime achievement output')
        source = ROOT / relative
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_achievement_ui_validation.cs', project / 'Assets/Editor/PartnerAchievementUiValidation.cs')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8')
    anchor = '        var profileUi = PartnerProfileUiValidation.Check('
    lines = text.splitlines()
    targets = [i for i, line in enumerate(lines) if line.startswith(anchor)]
    if len(targets) != 1: raise ValueError('Expected actual native profile UI fixture')
    addition = lines[targets[0]].replace('profileUi', 'achievementUi').replace('PartnerProfileUiValidation', 'PartnerAchievementUiValidation')
    if addition not in lines:
        lines.insert(targets[0] + 2, addition)
        lines.insert(targets[0] + 3, '        while (achievementUi.MoveNext()) yield return achievementUi.Current;')
        host.write_text('\n'.join(lines) + '\n', 'utf8', newline='\n')
    print('Prepared native achievement hierarchy and actual pointer fixture')


if __name__ == '__main__': main()
