"""Port the selected original invitation enums, base and comparer priorities."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI'
SOURCE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-invite-rules-source-manifest.json'


def build(write):
    if json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '871_156598':
        raise ValueError('Recheck selected invitation source version')
    inputs, outputs = [], []

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    def source(name):
        path = SOURCE / name
        inputs.append(record(path))
        return path.read_text(encoding='utf-8-sig')

    def emit(name, text):
        path = DEST / name
        raw = text.replace('\r\n', '\n').encode()
        if write:
            path.write_bytes(raw)
        elif path.read_bytes() != raw:
            raise ValueError('Original invitation port drift: ' + name)
        outputs.append(record(path))
        meta = path.with_name(path.name + '.meta')
        if not meta.exists() and write:
            guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/map12/team-invite/' + name).hex
            meta.write_bytes(('fileFormatVersion: 2\nguid: ' + guid + '\n').encode())
        outputs.append(record(meta))

    def wrap(text):
        text = text.replace('using Common;\n', '')
        return 'namespace Zjcs.Official.TeamUI\n{\nusing Zjcs.Official.ProfessionUI;\n' + text + '\n}\n'

    for name, original in (
        ('PropType.cs', 'common-source/Common/PropType.cs'),
        ('ComparerOrderType.cs', 'common-source/ComparerOrderType.cs'),
        ('TeamInviteComparerType.cs', 'client-source/TeamInviteComparerType.cs'),
        ('TeamInviteSubPanelBase.cs', 'client-source/TeamInviteSubPanelBase.cs')):
        text = source(original).replace('namespace Common;\n', '')
        text = text.replace('ITeamTarget', 'TeamTargetData')
        emit(name, wrap(text))

    text = source('client-source/TeamInviteComparer.cs')
    text = text.replace('using Framework.Logging;\n', '').replace('using Framework.Logging.InterpolatedStringHandler;\n', '')
    text = text.replace('private PropType mPropType;', 'private PropType mPropType;\n\tprivate readonly ITeamInviteComparerServices services;')
    text = text.replace('\tpublic TeamInviteComparer()\n\t{\n\t}\n\n', '')
    text = text.replace('bool isJoinStrikeTarget, PropType propType', 'bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType')
    text = text.replace('mQueryFunc = queryFunc;', 'this.services = services;\n\t\tmQueryFunc = queryFunc;')
    # Original logging-only default; preserve the zero return.
    start = text.index('\t\tdefault:\n\t\t{')
    end = text.index('\n\t\t}\n\t\t}', start)
    text = text[:start] + '\t\tdefault:\n\t\t{\n\t\t\tUnityEngine.Debug.LogError("unsupport comparerOrderType : " + type);\n\t\t\treturn 0;' + text[end:]
    text = text.replace('ConvertLong2IntForSort(x - y)', 'ConvertLong2IntForSort(unchecked(x - y))')
    text = text.replace('ConvertLong2IntForSort(y - x)', 'ConvertLong2IntForSort(unchecked(y - x))')
    text = text.replace('JointStrikeUtils.CanJointTogether(player.Profession, MeManager.MainPlayer.Profession)', 'services.CanJointTogether(player.Profession, services.MainPlayerProfession)')
    text = text.replace('NpcTargetData', 'IProfessionInfoNpcTarget')
    text = text.replace('MeManager.MainPlayer.IsFunctionLimit(FunctionType.PinNpc)', 'services.IsNpcPinLimited')
    text = text.replace('PlayerNpcDataWrap', 'TeamInviteNpcRecord')
    text = text.replace('MeManager.MainPlayer.NpcManager.GetByNpcClassId', 'services.GetNpc')
    text = text.replace('MeManager.MainPlayer?.NpcManager?.GetByNpcClassId', 'services.GetNpc')
    text = text.replace('NpcUtils.IsNpcAssistLimit', 'services.IsNpcAssistLimit')
    text = text.replace('IFriendshipData friendshipData = player.TargetData.ToFriendshipData();', 'var friendshipData = services.ToFriendshipData(player.TargetData);')
    text = text.replace('friendshipData?.FriendshipToTarget', 'friendshipData?.ToTarget').replace('friendshipData?.FriendshipToPlayer', 'friendshipData?.ToPlayer')
    text = text.replace('return (friendshipData?.ToTarget ?? 0) + (friendshipData?.ToPlayer ?? 0);', 'return unchecked((friendshipData?.ToTarget ?? 0) + (friendshipData?.ToPlayer ?? 0));')
    text = text.replace('ActivityFestivalBossUtils.GetActivityData();', 'services.RefreshFestivalActivityData();')
    text = text.replace('Parsers.Instance.ActivityFestivalBossNpcInfoParser.Get(key1: (player.TargetData as IProfessionInfoNpcTarget).NpcClassId, key0: mAcitivityId) != null', 'services.HasFestivalBossNpc(mAcitivityId, (player.TargetData as IProfessionInfoNpcTarget).NpcClassId)')
    if re.search(r'MeManager|Parsers\.|GLogger|Log\.Error|NpcUtils', text):
        raise ValueError('Unmigrated comparer dependency')
    emit('TeamInviteComparer.cs', wrap(text))
    for name in ('TeamInviteComparerBase', 'TeamInvitePlayerComparer'):
        text = source('client-source/' + name + '.cs')
        text = text.replace('bool isJoinStrikeTarget, PropType propType', 'bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType')
        text = text.replace('base(queryFunc, isJoinStrikeTarget, propType)', 'base(queryFunc, isJoinStrikeTarget, services, propType)')
        emit(name + '.cs', wrap(text))

    text = source('client-source/TeamInvitePlayerPanel.cs')
    text = text.replace('new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, propType)',
                        'new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services, propType)')
    text = text.replace('new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget)',
                        'new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services)')
    text = text.replace('ActivityData activityData = ActivityFestivalBossUtils.GetActivityData();',
                        'int? activityData = services.FestivalActivityId;').replace('activityData.Id', 'activityData.Value')
    emit('TeamInvitePlayerPanel.cs', wrap(text))

    for name in ('TeamInviteNpc.cs', 'TeamInviteServices.cs'):
        path = DEST / name
        emit(name, path.read_text(encoding='utf-8-sig'))
    for name in ('client-source/TeamInviteNpc.cs', 'client-source/NpcUtils.cs',
                 'client-source/ITeamInvitePlayer.cs', 'client-source/TeamInviteSortTypeUtils.cs',
                 'common-source/TeamInvitePlayerInfoData.cs', 'common-source/Common/NpcTargetData.cs',
                 'common-source/Common/ActorTargetData.cs', 'common-source/Common/PlayerNpcDataWrap.cs'):
        source(name)
    for name in ('scripts/fixtures/map12_team_invite_rules_harness.cs',
                 'scripts/test_map12_team_invite_rules.py', 'scripts/test_map12_team_invite_rules.ps1'):
        outputs.append(record(ROOT / name))
    code_proof_path = SOURCE.parent / 'code-manifest.json'
    code_proof = json.loads(code_proof_path.read_bytes())
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        if record(SOURCE / 'managed/decoded' / name)['sha256'] != code_proof['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original invitation assembly changed: ' + name)
    inputs.append(record(code_proof_path))
    result = dict(format=1, manifest='871_156598', mapClassId=12, inputs=inputs,
        assemblies=[record(SOURCE / 'managed/decoded' / name) for name in ('Assembly-CSharp.dll', 'Console.dll')],
        adaptation='Original NPC provider async body, sort enum, comparer scores and unchanged priority lists. '
                   'Manager/parser/RPC calls are injected; Task replaces UniTask. No server quotas are introduced.',
        scope='Rule ports only. Native invitation prefab and actual profile/team bindings remain pending.',
        outputs=outputs, script=record(Path(__file__)))
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    if write:
        RECEIPT.write_bytes(raw)
    elif RECEIPT.read_bytes() != raw:
        raise ValueError('Invitation rule receipt drift')
    print('Original invitation provider and exact comparer priorities verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
