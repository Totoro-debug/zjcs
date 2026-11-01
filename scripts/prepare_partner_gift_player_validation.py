"""Prepare real local-save gift commands and the production UI player adapter."""
from pathlib import Path
import shutil

from prepare_partner_gift_configuration_validation import main as prepare_configuration
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_configuration()
    work = validation_directory()
    editor = work / 'unity/Assets/Editor'
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_player_validation.cs', editor / 'PartnerGiftPlayerValidation.cs')
    print(work / 'unity')


if __name__ == '__main__':
    main()
