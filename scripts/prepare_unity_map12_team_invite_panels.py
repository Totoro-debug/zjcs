"""Port the original five-mode invite window and selection-panel lifecycle."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/871_156598/official-code/client-source'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-invite-panels-source-manifest.json'


def build(write):
    inputs, outputs = [], []
    if json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '871_156598':
        raise ValueError('Original invite panel source version changed')

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    def change(text, old, new):
        if old not in text:
            raise ValueError('Original invite panel changed: ' + old)
        return text.replace(old, new)

    for name in ('TeamInvitePanel', 'RoomInvitePlayerPanel', 'CompanionInvitePlayerPanel',
                 'ChallengeInvitePlayerPanel', 'TeamInviteRobotComparer', 'TeamInviteRobotPanel',
                 'TeamInviteRobotItem', 'PlayerCompanionTag', 'HonorTitleItemProvider', 'HonorTitleItem', 'SortingOrderSetter'):
        source = SOURCE / (name + '.cs')
        inputs.append(record(source))
        text = source.read_text(encoding='utf-8-sig')
        text = re.sub(r'^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?|R3|GFrameCore.Extensions.ZLogger|Microsoft.Extensions.Logging|ZLogger);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        text = text.replace('\t[Inject]\n', '')
        text = re.sub(r'^using UnityEngine.UI.Ext;\n|^\[UIWindow\([^\n]+\)\]\n', '', text, flags=re.M)
        text = text.replace('ITeamTarget', 'TeamTargetData').replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('TabGroup ', 'WorldTabGroup ').replace('TabItem ', 'WorldTabItem ')
        if name == 'TeamInvitePanel':
            start = text.index('\tprivate IMapRootManagerProxy')
            end = text.index('\n\tpublic void Init(', start)
            text = text[:start] + '''\tprivate ITeamInvitePanelServices services;
\tpublic void Configure(ITeamInvitePanelServices value)
\t{
\t\tservices = value;
\t\tChallengeInvitePanel.Configure(value); PlayerPanel.Configure(value);
\t\tRoomPanel.Configure(value); RobotPanel.Configure(value); CompanionPanel?.Configure(value);
\t}
\tprotected void Awake()
\t{
\t\tmInviteFriend = services.CreateFriendsProvider();
\t\tmInviteNpc = services.CreateNpcProvider();
\t\tmRecentBuddies = services.CreateRecentBuddiesProvider();
\t}
''' + text[end:]
            start = text.index('\t\tGLoggerDebugInterpolatedStringHandler')
            end = text.index('\t\tInitWrapper(', start)
            text = text[:start] + text[end:]
            text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
            text = text.replace('MeManager.MainPlayer.OnLegionUpdate', 'services.LegionUpdated')
            text = text.replace('Player mainPlayer = MeManager.MainPlayer;', 'bool mainPlayer = services.MainPlayerAvailable;')
            text = text.replace('mainPlayer == null', '!mainPlayer')
            text = text.replace('mainPlayer.LegionId', 'services.LegionId')
            text = text.replace('IsNpcFunctionLimited(mainPlayer)', 'IsNpcFunctionLimited()')
            text = text.replace('IsTeamLeaderOrSolo(mainPlayer)', 'IsTeamLeaderOrSolo()')
            text = text.replace('IsNpcFunctionLimited(Player player)', 'IsNpcFunctionLimited()')
            text = text.replace('Parsers.Instance.FunctionLimitInfoParser.IsLimit(player, FunctionType.NpcFightAssist)', 'services.NpcFightAssistLimited')
            text = text.replace('IsTeamLeaderOrSolo(Player player)', 'IsTeamLeaderOrSolo()')
            text = text.replace('MeManager.MainTeam != null', 'services.TeamLeaderId.HasValue')
            text = text.replace('MeManager.MainTeam.TeamLeaderId == player.Id', 'services.TeamLeaderId.Value == services.MainPlayerId')
            text = text.replace('(Object)(object)MapRootManagerProxy.MapRoot == (Object)null', '!services.MapRootAvailable')
            text = text.replace('MyContainer myContainer = MapRootManagerProxy.MyContainer;', 'int? myContainer = services.MapClassId;')
            text = text.replace('(Object)(object)myContainer != (Object)null', 'myContainer.HasValue')
            text = text.replace('myContainer.ClassId', 'myContainer.Value')
            text = text.replace('ServiceProvider.CreateInstance<TeamInviteLegionMember>(Array.Empty<object>())', 'services.CreateLegionProvider()')
        elif name == 'ChallengeInvitePlayerPanel':
            # Only diagnostic logger construction is removed; keep each early-return test in order.
            text = '''public class ChallengeInvitePlayerPanel : TeamInviteSubPanel
{
    protected override bool PlayerFilter(TeamInvitePlayerInfoData infoData)
    {
        if (!services.MainPlayerAvailable) return true;
        if (!infoData.ChallengeUnlocked) return true;
        if (infoData.ActorType != TeamInviteActorType.RealPlayer) return true;
        if (infoData.ServerId != ((ITeamInvitePanelServices)services).MainPlayerServerId) return true;
        return base.PlayerFilter(infoData);
    }
}
'''
        elif name == 'TeamInviteRobotComparer':
            text = text.replace('bool isJoinStrikeTarget, PropType propType', 'bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType')
            text = text.replace('base(queryFunc, isJoinStrikeTarget, propType)', 'base(queryFunc, isJoinStrikeTarget, services, propType)')
        elif name == 'TeamInviteRobotPanel':
            text = re.sub(r'\tprivate IScriptableObjectConfig<GlobalUIConfig> GlobalUIConfig \{ get; set; \}\n', '', text)
            text = text.replace('PlayerRobotInvite ', 'TeamInviteRobotRule ').replace('RobotInvitePlayType type', 'int type')
            text = text.replace('CultureTextArg mPropTypeArg', 'object mPropTypeArg')
            text = text.replace('new CultureTextArg($"{typeof(PropType).Name}.{mPropType.Value}")', 'new TeamInviteCultureTextArg(services.Localize, $"{typeof(PropType).Name}.{mPropType.Value}")')
            text = text.replace('LanguageManager.Instance.GetText', 'services.Localize').replace('BroadcastManager.Instance.SystemMsg', 'services.SystemMessage')
            text = change(text, 'WindowManager.Instance.Close(((Component)((Component)this).GetComponentInParent<PopupWindow>()).gameObject)', '((ITeamInvitePanelServices)services).CloseInviteWindow(this)')
            text = text.replace('Parsers.Instance.PlayerRobotInviteParser.Get(type)', '((ITeamInvitePanelServices)services).GetRobotInvite(type)')
            text = text.replace('MeManager.MainPlayer.GetAllFriendAssistCount(type)', '((ITeamInvitePanelServices)services).GetAllFriendAssistCount(type)')
            text = text.replace('MeManager.MainTeam != null && MeManager.MainTeam.GetPlayer(infoData.PlayerId) != null', 'services.TeamHasPlayer(infoData.PlayerId)')
            text = text.replace('UIUtils.SetUIGray(GlobalUIConfig.Value, ((Component)ConfirmBtn).gameObject, !flag)', '((ITeamInvitePanelServices)services).SetUiGray(((Component)ConfirmBtn).gameObject, !flag)')
            text = text.replace('ActivityData activityData = ActivityFestivalBossUtils.GetActivityData();', 'int? activityData = services.FestivalActivityId;')
            text = text.replace('activityData.Id', 'activityData.Value')
            text = text.replace('isJoinStrikeTarget, propType)', 'isJoinStrikeTarget, services, propType)').replace('isJoinStrikeTarget)', 'isJoinStrikeTarget, services)')
        elif name == 'TeamInviteRobotItem':
            text = text.replace('PlayerRobotInvite ', 'TeamInviteRobotRule ')
            text = text.replace('mTargetData.IsVaild', '(mTargetData.TargetId > 0)')
            text = text.replace('BroadcastManager.ShowErrText(num, null, false)', '((ITeamInvitePanelServices)services).ShowError(num, false)')
            text = text.replace('mTargetData.GetActorAssistCount(mRobotInviteCfg.PlayType)', '((ITeamInvitePanelServices)services).GetActorAssistCount(mTargetData, mRobotInviteCfg.PlayType)')
            text = text.replace('mTargetData.CheckPlayTypeCanAssist(mRobotInviteCfg.PlayType)', '((ITeamInvitePanelServices)services).CheckPlayTypeCanAssist(mTargetData, mRobotInviteCfg.PlayType)')
            text = text.replace('NpcTargetData', 'IProfessionInfoNpcTarget').replace('PlayerNpcDataWrap', 'TeamInviteNpcRecord')
            start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
            end = text.index('\n\t\t\treturn;', start)
            text = text[:start] + '\t\t\tservices.Error("TeamInviteRobotItem requires original NPC target data.");' + text[end:]
            text = text.replace('MeManager.MainPlayer.NpcManager.GetByNpcClassId', 'services.GetNpc')
            text = text.replace('NpcUtils.IsNpcAssistLimit', 'services.IsNpcAssistLimit')
            text = re.sub(r'NpcFriendshipLevelCondition npcFriendshipLevelCondition = [^\n]+;', 'int requiredLevel = services.RequiredNpcAssistFriendshipLevel(npcTargetData.NpcClassId);', text)
            text = text.replace('npcFriendshipLevelCondition.FriendshipLevel', 'requiredLevel')
            text = text.replace('JointStrikeUIUtils.IsInvitedAvailable', 'services.IsJointStrikeInvitedAvailable')
            at = text.index('\n\tpublic virtual void Init(')
            text = text[:at] + '\n\tpublic override void Configure(ITeamInviteUiServices value) { base.Configure(value); mNpcFriendshipLevelShow.Configure(value); }\n' + text[at:]
        elif name == 'PlayerCompanionTag':
            text = text.replace('internal class', 'public class')
            text = text.replace('private DisposableBag mDisposableBag;', 'private TeamInviteResourceOperationBag mDisposableBag = new TeamInviteResourceOperationBag();')
            text = text.replace('private IActivityCompanionManager CompanionManager { get; set; }', 'private ITeamInvitePanelServices services;\n\tpublic void Configure(ITeamInvitePanelServices value) { services = value; }')
            text = text.replace('mDisposableBag = default;', 'mDisposableBag = new TeamInviteResourceOperationBag();')
            text = change(text, 'Disposable.AddTo<IDisposable>(CompanionManager.OnBound.Subscribe(OnBound), ref mDisposableBag);', 'services.CompanionBound += OnBound;\n\t\tmDisposableBag.Add(new TeamInviteListenerRemoval(() => services.CompanionBound -= OnBound));')
            text = change(text, 'Disposable.AddTo<IDisposable>(CompanionManager.OnDataSync.Subscribe(OnDataChanged), ref mDisposableBag);', 'services.CompanionDataSynced += OnDataChanged;\n\t\tmDisposableBag.Add(new TeamInviteListenerRemoval(() => services.CompanionDataSynced -= OnDataChanged));')
            text = text.replace('OnBound(PlayerActivityCompanionBoundMsg msg)', 'OnBound(long companionId)').replace('msg.CompanionId', 'companionId')
            text = text.replace('CompanionManager.CompanionId', 'services.CompanionId')
        elif name == 'HonorTitleItemProvider':
            text = text.replace('HonorTitleItemProvider : MonoBehaviour', 'HonorTitleItemProvider : MonoBehaviour, IProfessionPlayerInfoTitle')
            text = text.replace('private DisposableBag mDisposableBag;', 'private TeamInviteResourceOperationBag mDisposableBag = new TeamInviteResourceOperationBag();')
            text = text.replace('private IResourceLoader ResourceLoader { get; set; }', 'private ITeamHonorTitleServices services;\n\tpublic void Configure(ITeamHonorTitleServices value) { services = value; }\n\tpublic void Init(object appearance) { Init(appearance as AppearanceData); }')
            text = text.replace('Init(Player player,', 'Init(IProfessionInfoPlayer player,').replace('Init(player.AppearanceData, initAction)', 'Init(player.AppearanceData as AppearanceData, initAction)')
            text = text.replace('mDisposableBag = default;', 'mDisposableBag = new TeamInviteResourceOperationBag();')
            text = text.replace('mSlot.DestroyChildren();', 'for (int i = mSlot.childCount - 1; i >= 0; i--) Object.Destroy(mSlot.GetChild(i).gameObject);')
            text = change(text, 'Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadPrefab(itemPath), ref mDisposableBag).Result', 'mDisposableBag.Add(services.Resources.LoadAsset<GameObject>(itemPath)).Result')
            text = text.replace('UIUtils.Instantiate(result, mSlot)', 'services.InstantiateKeepScale(result, mSlot)')
            text = text.replace('\t\t\t\tInitTitleItem(component);', '\t\t\t\tcomponent.Configure(services);\n\t\t\t\tInitTitleItem(component);')
        elif name == 'HonorTitleItem':
            text = text.replace('private MyPlayable mStatePlayable;', 'private Component mStatePlayable;')
            start = text.index('\tprivate IResourceLoader ResourceLoader')
            end = text.index('\n\tprivate void OnDisable()', start)
            text = text[:start] + '\tprivate ITeamHonorTitleServices services;\n\tpublic void Configure(ITeamHonorTitleServices value) { services = value; }\n' + text[end:]
            text = text.replace('HonorTitlePlayableConfig.Value.HonorTitleUIPlayables.TryGetValue(mItemId, out var value)', 'services.TryGetPlayableGuid(mItemId, out var value)')
            text = text.replace('mMyPlayableManager.Play', 'services.Play')
            text = text.replace('ResourceLoader.LoadAssetByGuid<GameObject>(value.AssetGuid)', 'services.Resources.LoadAssetByGuid<GameObject>(value)')
            text = text.replace('AssetOperationHandle<GameObject> obj', 'IProfessionAssetOperation<GameObject> obj')
            text = text.replace('mStatePlayable.Stop();', 'services.Stop(mStatePlayable);')
        elif name == 'SortingOrderSetter':
            text = text.replace('PopupWindow', 'WorldPopupWindow')
        if re.search(r'MeManager|Parsers\.|GLogger|\bDisposableBag\b|ServiceProvider|BroadcastManager|WindowManager|Common;', text):
            raise ValueError('Unmigrated dependency: ' + name)
        text = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)
        text = '\n'.join(line.rstrip() for line in text.split('\n'))
        text = ('namespace Zjcs.Official.TeamUI\n{\nusing Zjcs.Official.ProfessionUI;\n'
                'using Zjcs.Official.WorldUI;\nusing Object = UnityEngine.Object;\n'
                'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n' + text + '\n}\n')
        path = DEST / (name + '.cs')
        raw = text.encode()
        if write:
            path.write_bytes(raw)
        elif path.read_bytes() != raw:
            raise ValueError('Invite panel source drift: ' + name)
        outputs.append(record(path))
    for name in ('TeamInvitePanelServices', 'TeamInviteListenerRemoval', 'TeamInvitePanel',
                 'RoomInvitePlayerPanel', 'CompanionInvitePlayerPanel', 'ChallengeInvitePlayerPanel',
                 'TeamInviteRobotComparer', 'TeamInviteRobotPanel', 'TeamInviteRobotItem', 'PlayerCompanionTag',
                 'HonorTitleItemProvider', 'HonorTitleItem', 'SortingOrderSetter'):
        path = DEST / (name + '.cs')
        meta = path.with_name(path.name + '.meta')
        raw = ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/map12/team-invite/' + path.name).hex + '\n').encode()
        if write:
            meta.write_bytes(raw)
        elif meta.read_bytes() != raw:
            raise ValueError('Invite panel metadata drift: ' + name)
        if name in ('TeamInvitePanelServices', 'TeamInviteListenerRemoval'):
            outputs.append(record(path))
        outputs.append(record(meta))
    inputs.extend(record(SOURCE.parent / 'common-source/Common' / (name + '.cs')) for name in ('PlayerRobotInvite', 'RobotInvitePlayType'))
    inputs.append(record(SOURCE.parent / 'framework-firstpass-source/Framework/CultureTextArg.cs'))
    code_proof_path = SOURCE.parent.parent / 'code-manifest.json'
    code_proof = json.loads(code_proof_path.read_bytes())
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        assembly = record(SOURCE.parent / 'managed/decoded' / name)
        if assembly['sha256'] != code_proof['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original invite panel assembly changed: ' + name)
        inputs.append(assembly)
    inputs.append(record(code_proof_path))
    inputs.append(record(SOURCE.parent / 'framework-firstpass-source/PopupWindow.cs'))
    outputs.append(record(DEST.parent / 'WorldUI/WorldPopupWindow.cs'))
    action_source = SOURCE.parent / 'framework-firstpass-source/Framework/GAction.cs'
    inputs.append(record(action_source))
    action_text = action_source.read_text(encoding='utf-8-sig').split('public sealed class GAction<T>')[0]
    action_text = re.sub(r'^using Framework[^\n]*\n', '', action_text, flags=re.M)
    action_text = re.sub(r'\t\t\t\t\tGLoggerErrorInterpolatedStringHandler message =.*?\n\t\t\t\t\tLog.Error\([^\n]*\);',
                         '\t\t\t\t\tUnityEngine.Debug.LogException(exception);', action_text, flags=re.S)
    action_text = action_text.replace('GAction?', 'GAction').replace('ObserverNode?', 'ObserverNode')
    action_text = action_text.replace('namespace Framework;', 'namespace Zjcs.Official.WorldUI\n{').replace('GAction', 'WorldGAction') + '\n}\n'
    action_path = DEST.parent / 'WorldUI/WorldGAction.cs'
    action_meta = action_path.with_name(action_path.name + '.meta')
    for path, raw in ((action_path, action_text.encode()), (action_meta,
        ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/map12/team-invite/world-gaction').hex + '\n').encode())):
        if write:
            path.write_bytes(raw)
        elif path.read_bytes() != raw:
            raise ValueError('Original popup sorting action drift')
        outputs.append(record(path))
    result = dict(format=1, manifest='871_156598', mapClassId=12, inputs=inputs, outputs=outputs,
                  script=record(Path(__file__)), adaptation='Original five-mode root, tab/cancellation order, '
                  'panel filters, robot selection and companion-tag subscriptions; external managers use explicit services.',
                  scope='Source ports only. Native prefab bindings, live profile data and robot-navigation validation remain pending.')
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    if write:
        RECEIPT.write_bytes(raw)
    elif RECEIPT.read_bytes() != raw:
        raise ValueError('Invite panel receipt drift')
    print('Original five-mode invitation panels verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
