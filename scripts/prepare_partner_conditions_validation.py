"""Add current partner conditions and transaction checks to an owned native UI project."""
import hashlib
import json
from pathlib import Path
import shutil
from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / 'unity').is_dir():
        proof = json.loads((ROOT / 'assets/reference/partner-roster-native-source-manifest.json').read_bytes())
        if proof['manifest'] != '884_156692':
            raise ValueError('Selected native roster version required')
        for relative, expected in proof['installed_outputs'].items():
            if hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() != expected:
                raise ValueError('Edited native roster input: ' + relative)
        stage = work / 'gift-assets'
        if stage.exists():
            raise ValueError('Preserve an existing staged validation resource directory')
        shutil.copytree(ROOT / proof['installed_root'], stage)
        for row in proof['reused_assets']:
            source = ROOT / row['installed']
            metadata = Path(str(source) + '.meta')
            if hashlib.sha256(source.read_bytes()).hexdigest() != row['sha256'] or hashlib.sha256(metadata.read_bytes()).hexdigest() != row['metadata_sha256']:
                raise ValueError('Edited shared native roster input: ' + row['installed'])
            target = stage / row['staged']
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            shutil.copy2(metadata, Path(str(target) + '.meta'))
        from prepare_partner_roster_native_validation import main as prepare_native
        prepare_native()
    editor = work / 'unity/Assets/Editor'
    if not (editor / 'Validation.asmdef').is_file():
        raise ValueError('Prepare the owned native roster project first')
    for name in ('partner_condition_validation.cs', 'partner_roster_transaction_validation.cs', 'partner_card_binding_validation.cs'):
        shutil.copy2(ROOT / 'scripts/fixtures' / name, editor / name)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_roster_ui_validation.cs', editor / 'PartnerRosterUiValidation.cs')


if __name__ == '__main__':
    main()
