"""Migrate the selected official activity invitation button and its local team seam."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def main():
    source = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/ActivityDomainBossTeamButton.cs'
    text = source.read_text('utf8')
    replacements = [
        ('using Common;\n', ''), ('using Cysharp.Threading.Tasks;\n', ''),
        ('public class ActivityDomainBossTeamButton : UIBase', 'public class ActivityDomainBossTeamButton : WorldInitializedUI'),
        ('ITeamTarget mTeamTarget', 'IActivityBattleTeam mTeamTarget'),
        ('Init(ITeamTarget teamTarget)', 'Init(IActivityBattleTeam teamTarget)'),
        ('mTeamTarget = teamTarget;', 'mTeamTarget = teamTarget ?? throw new ArgumentNullException(nameof(teamTarget));'),
        ('MeManager instance = MeManager.Instance;\n\t\tinstance.OnTeamPlayerAdd += new Action<TeamPlayerInfo>(Team_OnPlayerUpdate);\n\t\tinstance.OnTeamPlayerRemove += new Action<TeamPlayerInfo>(Team_OnPlayerUpdate);\n\t\tinstance.OnTeamUpdate += new Action(Team_OnTeamUpdate);',
         'mTeamTarget.Changed += Team_OnTeamUpdate;'),
        ('MeManager instance = MeManager.Instance;\n\t\tif (instance != null)\n\t\t{\n\t\t\tinstance.OnTeamPlayerAdd -= new Action<TeamPlayerInfo>(Team_OnPlayerUpdate);\n\t\t\tinstance.OnTeamPlayerRemove -= new Action<TeamPlayerInfo>(Team_OnPlayerUpdate);\n\t\t\tinstance.OnTeamUpdate -= new Action(Team_OnTeamUpdate);\n\t\t}',
         'mTeamTarget.Changed -= Team_OnTeamUpdate;'),
        ('\n\tprivate void Team_OnPlayerUpdate(TeamPlayerInfo playerInfo)\n\t{\n\t\tTeam_OnTeamUpdate();\n\t}\n', ''),
        ('mTeamTarget.GetTeamMaxPlayer()', 'mTeamTarget.MaximumMemberCount'),
        ('((MeManager.MainTeam == null) ? 1 : MeManager.MainTeam.Players.Count)', 'mTeamTarget.MemberCount'),
        ('TeamUpdateTargetAndQueryTeamBriefInfosRequest req = new TeamUpdateTargetAndQueryTeamBriefInfosRequest\n\t\t{\n\t\t\tActorTargetDatas = MeManager.MainPlayer.GetTeamAllMemberTargetDatas(),\n\t\t\tTeamTarget = mTeamTarget\n\t\t};\n\t\tUniTaskVoid val = TeamPanelHelper.TryPopupTeamPanel(null, req);\n\t\tval.Forget();',
         'mTeamTarget.OpenInvitation();'),
    ]
    for old, new in replacements:
        if text.count(old) != 1:
            raise ValueError('Reinspect changed official activity team method: ' + old[:90])
        text = text.replace(old, new)
    text = '\n'.join(line for line in text.splitlines() if '//IL_' not in line) + '\n'
    output = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI/ActivityDomainBossTeamButton.cs'
    text = ('// Selected official ActivityDomainBossTeamButton; online team operations use the local team adapter.\n'
            'using Zjcs.Official.WorldUI;\n'
            'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
            'namespace Zjcs.Official.ActivityBattleUI\n{\n' + text + '}\n')
    output.write_text(text, 'utf8', newline='\n')
    meta = metadata(output)
    seam = output.with_name('IActivityBattleTeam.cs')
    if not seam.is_file(): raise ValueError('Local activity team interface missing')
    seam_meta = metadata(seam)
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    receipt = dict(manifest='884_156692', source=source.relative_to(ROOT).as_posix(), source_sha256=digest(source),
        generator_sha256=digest(Path(__file__)), outputs={p.relative_to(ROOT).as_posix(): digest(p)
            for p in (output, meta, seam, seam_meta)},
        adaptation='UIBase lifecycle uses migrated WorldInitializedUI. Three online team events share Changed; '
            'team count/maximum and original invitation request pass through IActivityBattleTeam. '
            'Serialized fields, text formatting, button callback and enable/disable order retained.')
    (ROOT / 'assets/reference/daily-challenges/activity-team-runtime-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__': main()
