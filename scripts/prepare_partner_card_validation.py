"""Import the source-backed partner card in an isolated Unity project."""
import json
import os
from pathlib import Path
import shutil

from prepare_partner_roster_ui_validation import prepare
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    work = validation_directory()
    if Path(os.environ['ZJCS_REPOSITORY_ROOT']).resolve() != ROOT or work == ROOT / 'output/validation':
        raise ValueError('An owned validation session is required')
    project = work / 'unity'
    if not project.is_dir():
        prepare(work)
    for relative in ('Official/PartnersUI/Roster/TipDots', 'Official/World/NativeText',
                     'Presentation/Opening/OfficialCoffeeUIParticle'):
        shutil.copytree(ROOT / 'unity/Assets/Game/Runtime' / relative,
                        project / 'Assets/Game/Runtime' / relative, dirs_exist_ok=True)
    shutil.copytree(work / 'card-assets', project / 'Assets/PartnerCard', dirs_exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_card_binding_validation.cs', project / 'Assets/Editor/PartnerCardBindingValidation.cs')
    shutil.copy2(ROOT / 'scripts/fixtures/partner_roster_ui_validation.cs', project / 'Assets/Editor/PartnerRosterUiValidation.cs')
    assembly = project / 'Assets/Editor/Validation.asmdef'
    data = json.loads(assembly.read_bytes())
    data['references'] = sorted(set(data['references']) | {'Zjcs.Official.World.NativeText', 'Coffee.UIParticle'})
    assembly.write_text(json.dumps(data, indent=2), 'utf8')
    print(project)


if __name__ == '__main__':
    main()
