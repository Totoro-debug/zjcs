"""Prepare the actual original player widget with the committed local profile."""
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_main_avatar_validation import main as prepare_avatar
from validation_workspace import validation_directory


def main():
    prepare_avatar()
    editor = validation_directory() / 'unity/Assets/Editor'
    shutil.copy2(ROOT / 'scripts/fixtures/partner_local_player_widget_validation.cs',
                 editor / 'PartnerLocalPlayerWidgetValidation.cs')
    print('Prepared original PlayerIconMe, local profile, live health and current native avatar')


if __name__ == '__main__':
    main()
