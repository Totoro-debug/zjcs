"""Add the actual profile region and transactions to an owned imported project."""
import argparse
import json
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
    editor_asmdef, = (project / 'Assets/Editor').glob('*.asmdef')
    editor = json.loads(editor_asmdef.read_bytes())
    if 'Zjcs.Official.World.NativeLabelUI' not in editor['references']:
        editor['references'].append('Zjcs.Official.World.NativeLabelUI')
        editor_asmdef.write_text(json.dumps(editor, indent=2) + '\n', 'utf8', newline='\n')
    proof = json.loads((ROOT / 'assets/reference/partner-profile-native-source-manifest.json').read_bytes())
    paths = set(proof['installed_output_sha256'])
    asset_proof = json.loads((ROOT / 'assets/reference/partner-profile-assets-source-manifest.json').read_bytes())
    paths.update(asset_proof['outputs'])
    reward_proof = json.loads((ROOT / 'assets/reference/partner-gift-controller-source-manifest.json').read_bytes())
    paths.update(reward_proof['installed_output_sha256'])
    paths.update(('unity/Assets/Game/Runtime/Offline/Progression/OfflinePartnerProfileService.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerProfileUiServices.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftRuntimeBindings.cs',
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerProfileAssetCatalog.cs'))
    for relative in list(paths):
        source = ROOT / relative
        if source.suffix == '.cs' and not Path(str(source) + '.meta').exists(): metadata(source)
        if source.suffix == '.cs': paths.add(relative + '.meta')
    paths.add('unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs')
    for relative in paths:
        target = project / Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / relative, target)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_profile_transaction_validation.cs', project / 'Assets/Editor/PartnerProfileTransactionValidation.cs')
    shutil.copy2(ROOT / 'scripts/fixtures/partner_profile_ui_validation.cs', project / 'Assets/Editor/PartnerProfileUiValidation.cs')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8')
    if '        PartnerProfileTransactionValidation.Check();' not in text:
        anchor = '        PartnerTalentPlayerValidation.Check();'
        if text.count(anchor) != 1: raise ValueError('Expected previously verified actual save/window fixture')
        text = text.replace(anchor, anchor + '\n        PartnerProfileTransactionValidation.Check();')
        host.write_text(text, 'utf8', newline='\n')
    if 'PartnerProfileUiValidation.Check' not in text:
        anchor = '        PartnerNpcIconValidation.CheckOpeningLifecycle();'
        if text.count(anchor) != 1: raise ValueError('Expected original portrait camera fixture')
        text = text.replace(anchor, '''        var profileUi = PartnerProfileUiValidation.Check(canvas.transform, cameraUi, events, font);
        while (profileUi.MoveNext()) yield return profileUi.Current;
''' + anchor)
        host.write_text(text, 'utf8', newline='\n')
    if 'PartnerProfileUiValidation.Check(canvas.transform, cameraUi, events);' in text:
        text = text.replace('PartnerProfileUiValidation.Check(canvas.transform, cameraUi, events);', 'PartnerProfileUiValidation.Check(canvas.transform, cameraUi, events, font);')
        host.write_text(text, 'utf8', newline='\n')
    print('Prepared selected original profile UI and actual save transactions in', project)


if __name__ == '__main__': main()
