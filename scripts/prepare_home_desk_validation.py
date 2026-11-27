"""Reuse the native building input/render harness for the Desk flow."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_roster_ui import method
from validation_workspace import require_temporary_unity_project


def prepare(project, work):
    project = require_temporary_unity_project(project)
    work = work.resolve()
    if not work.is_relative_to(ROOT / 'output/validation'):
        raise ValueError('Use an isolated validation output')
    base = ROOT / 'scripts/fixtures/home_diningtable_runtime_validation.cs'
    checks = ROOT / 'scripts/fixtures/home_desk_runtime_validation.cs'
    text = method(base.read_text('utf8'), 'private static IEnumerator RunChecks()',
        'private static IEnumerator RunChecks() { return RunDeskChecks(); }')
    text = text.replace('public static class HomeDiningTableRuntimeValidation', 'public static partial class HomeDeskRuntimeValidation')
    text = text.replace('HomeDiningTableRuntimeValidation', 'HomeDeskRuntimeValidation')
    text = text.replace('ZJCS_DINING_PROJECT', 'ZJCS_DESK_PROJECT').replace('HOME_DININGTABLE_', 'HOME_DESK_')
    # Unity already imports changed inputs before executeMethod. ForceUpdate here
    # invalidates the entire independent project's otherwise reusable cache.
    text = text.replace('AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);',
        'AssetDatabase.Refresh();')
    text = text.replace('states, notice, UnlockCommunication story, mask, repair effect, persistence, NPC bubble tip, dialogue acknowledge, TipDots, FunctionLimit 57, and all 6 skins verified.',
        'native Desk unlock branches, repair notice, free repair, cold ownership restoration, shared graph contract and six skins verified; cultivation panel remains separate work.')
    fixture = project / 'Assets/Editor/HomeDeskValidation'
    fixture.mkdir(parents=True, exist_ok=True)
    (fixture / 'HomeDeskRuntimeValidation.cs').write_text(text, 'utf8', newline='\n')
    (fixture / 'HomeDeskRuntimeChecks.cs').write_bytes(checks.read_bytes())
    tips = ROOT / 'scripts/fixtures/treasure_tips_validation.cs'
    (fixture / 'TreasureTipsValidation.cs').write_bytes(tips.read_bytes())
    from prepare_treasure_tip_rules import prepare as prepare_tip_rules
    prepare_tip_rules(oracle=fixture/'OriginalTreasureTips.cs')
    references = ['Launch', 'Zjcs.Editor', 'Zjcs.Presentation.Home', 'Zjcs.Presentation.World',
        'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Official.TreasureCultivation',
        'Zjcs.Offline.Home', 'Zjcs.Offline.HomeSceneInput', 'Zjcs.Offline.PartnersUI', 'Zjcs.Offline.HomeTipDots',
        'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput',
        'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.HomeStoryDialog', 'Zjcs.Official.HomeTipDots',
        'Zjcs.Official.BattleSkillPlayables', 'Zjcs.Official.Partners',
        'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.PartnerDialoguePlaybackUI',
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.StoryMasks', 'Zjcs.Official.WorldUI',
        'spine-unity', 'Unity.TextMeshPro', 'Unity.ugui']
    (fixture / 'HomeDeskValidation.asmdef').write_text(json.dumps(dict(name='HomeDeskValidation',
        includePlatforms=['Editor'], references=references), indent=2) + '\n', 'utf8', newline='\n')
    paths = [base, checks, tips, Path(__file__)] + list(fixture.glob('*'))
    (work / 'source-fixture.json').write_text(json.dumps({str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in paths}, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared native Desk validation:', fixture)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.project, args.work)
