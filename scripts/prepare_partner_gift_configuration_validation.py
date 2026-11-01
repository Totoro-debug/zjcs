"""Prepare the production gift configuration assembly closure and selected assets."""
from pathlib import Path
import json
import shutil

from prepare_partner_roster_ui_validation import prepare
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    work = validation_directory()
    project = prepare(work)
    assets = project / 'Assets'
    # The roster fixture is not an entry point of this data-only validation.
    (assets / 'Editor/PartnerRosterUiValidation.cs').unlink()
    shutil.copytree(ROOT / 'unity/Assets/Game/Resources/Partners', assets / 'Resources/Partners')
    target = assets / 'Official/Partners'
    target.mkdir(parents=True)
    for name in ('partner-rules.json', 'partner-properties.json', 'partner-custom-characters.txt'):
        for suffix in ('', '.meta'):
            shutil.copy2(ROOT / 'unity/Assets/Official/Partners' / (name + suffix), target / (name + suffix))
    editor = assets / 'Editor'
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_configuration_validation.cs', editor / 'PartnerGiftConfigurationValidation.cs')
    definition = editor / 'Validation.asmdef'
    data = json.loads(definition.read_bytes())
    data['references'] += ['Zjcs.Official.PartnerGiftControllerUI', 'Zjcs.Official.PartnerGiftUI']
    definition.write_text(json.dumps(data, indent=2), 'utf8', newline='\n')
    print(project)


if __name__ == '__main__':
    main()
