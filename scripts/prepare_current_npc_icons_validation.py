"""Validate every current native NPC image plus the actual round-card flow."""
import argparse
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_current_round_ui_validation import stage


def prepare(work):
    project = stage(work,npc_source=work/'current-npc-icons-source-manifest.json')
    editor = project/'Assets/Editor'
    shutil.copyfile(ROOT/'scripts/fixtures/current_npc_icons_validation.cs',editor/'CurrentNpcIconsValidation.cs')
    fixture = editor/'CurrentRoundUiValidation.cs'
    text = fixture.read_text('utf-8-sig')
    anchor = 'var assets = Resources.Load<BattleRoundPortraitAssets>("Combat/RoundUI/PortraitAssets");'
    if text.count(anchor) != 1: raise ValueError('Review the original round resource fixture entry')
    fixture.write_text(text.replace(anchor,'CurrentNpcIconsValidation.Check();\n        '+anchor),encoding='utf-8')
    print('Prepared current NPC image GPU checks and real scene/UGUI card flow:',project,flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    prepare(parser.parse_args().work.resolve())
