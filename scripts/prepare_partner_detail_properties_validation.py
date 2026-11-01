"""Stage actual native detail-property controls and local services in an owned imported cache."""
import argparse
import json
from pathlib import Path
import shutil

from validation_workspace import require_temporary_unity_project, validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    args = parser.parse_args()
    project = require_temporary_unity_project(args.project)
    for name in ('partner-detail-properties-native', 'partner-detail-properties-assets'):
        receipt = json.loads((ROOT/'assets/reference'/f'{name}-source-manifest.json').read_bytes())
        for relative in receipt.get('installed_output_sha256', receipt.get('outputs', {})):
            target = project/Path(relative).relative_to('unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT/relative, target)
        for relative in receipt.get('installed_folder_sha256', {}):
            target = project/Path(relative).relative_to('unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT/relative, target)
    for relative in ('Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef', 'Assets/Official/Partners/partner-properties.json'):
        shutil.copy2(ROOT/'unity'/relative, project/relative)
    editor = project/'Assets/PartnerCurrentValidation/Editor'
    if not (editor/'PartnerFriendshipDialogBase.cs').is_file():
        raise ValueError('Reuse the current friendship validation cache for its explicit external-window fixture')
    assembly = editor/'PartnerCurrentValidation.asmdef'
    data = json.loads(assembly.read_bytes())
    for reference in ('Zjcs.Official.PartnerDetailUI', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.Partners'):
        if reference not in data['references']:
            data['references'].append(reference)
    assembly.write_text(json.dumps(data, indent=2)+'\n', 'utf8', newline='\n')
    shutil.copy2(ROOT/'scripts/fixtures/partner_detail_properties_validation.cs', editor/'PartnerDetailPropertiesValidation.cs')
    print('Prepared actual native detail-property region, current formula and local save ports in',project)


if __name__=='__main__':
    main()
