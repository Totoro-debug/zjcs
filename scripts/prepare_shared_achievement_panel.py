"""Record the shared regular achievement controllers and independent regression inputs."""
import argparse
import hashlib
import json
from pathlib import Path
from official_source import OfficialSource

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/shared-achievement-panel-source-manifest.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    # Reuse the already migrated profession rules; verify their recovered rows.
    table = ROOT / '.tools/source-packages/current/884_156692/official-data/tables.json'
    rules = ROOT / 'unity/Assets/Official/Combat/rules.json'
    select = lambda data: [row for row in data['achievement'] if row['Group'] == 10 and row['AchievementType'] == 56]
    rows = select(json.loads(rules.read_bytes()))
    if rows != select(json.loads(table.read_bytes())):
        raise ValueError('Existing profession achievement rows differ from their recovered source')
    paths = [Path(__file__), table, rules,
        ROOT / 'scripts/fixtures/achievement_panel_shared_validation.cs',
        ROOT / 'scripts/test_shared_achievement_panel.ps1',
        ROOT / 'scripts/test_profession_achievements.py',
        ROOT / 'scripts/fixtures/profession_achievements_harness.cs']
    paths += [code / path for path in (
        'client-source/AchievementPanel.cs', 'client-source/AchievementPanelBase.cs',
        'client-source/AchievementPanelItem.cs', 'client-source/AchievementContentHandler.cs',
        'framework-firstpass-source/UIBase.cs', 'common-source/Common/AchievementUtils.cs',
        'common-source/Common/PlayerAchievementRecordDataWrap.cs')]
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    paths += [runtime / 'Official/ProfessionUI' / (name + suffix)
        for name in ('AchievementPanel', 'AchievementPanelBase', 'AchievementPanelItem',
            'AchievementPanelData', 'ProfessionAchievementPanelSource', 'UIBase')
        for suffix in ('.cs', '.cs.meta')]
    paths += [runtime / 'Official/ProfessionUI/Zjcs.Official.ProfessionUI.asmdef',
              runtime / 'Offline/Progression/OfficialProfessionAchievementRules.cs',
              runtime / 'Offline/Progression/OfficialPropFormat.cs']
    proof = dict(manifest=source.version, inputs={p.relative_to(ROOT).as_posix(): sha(p) for p in paths},
        profession_table_rows=rows,
        scope='Original regular achievement search/filter/sort/row lifecycle and group claim callback. Existing profession type 56/group 10 adapter retained; data providers resolve other regular keys. Ranking and production score-detail presentation are outside this component proof.')
    if check:
        if json.loads(RECEIPT.read_bytes()) != proof:
            raise ValueError('Shared achievement controller or regression inputs differ')
        print('Shared achievement controller source and regression inputs verified')
    else:
        RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
