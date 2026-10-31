"""Port the active official roster/card/friendship view code with local service seams."""
import hashlib
import json
from pathlib import Path
import re
from prepare_partner_ui_helpers import meta

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster'
NAMESPACE = 'Zjcs.Official.PartnersUI'


def method(text, signature, replacement):
    start = text.index(signature)
    brace = text.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[:start] + replacement.rstrip() + text[end:]


def main():
    selection = json.loads((ROOT / 'official-source.json').read_bytes())
    version = selection['active']
    inputs, outputs = {}, {}
    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST, folder=True)

    def source(name):
        path = ROOT / '.tools/source-packages/official-code' / name
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / name).read_bytes():
            raise ValueError('Official source differs from selected version: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        path = DEST / name
        path.write_text(text, 'utf8')
        meta(path)
        outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()

    def port(name, text):
        text = re.sub(r'^using (Common|AK.Wwise|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<').replace('PlayerNpcDataWrap', 'NpcRosterItem')
        aliases = '''using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerRosterLanguage;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.SkillsUI;
'''
        write(name + '.cs', '// Ported from active official client; see partner-roster-ui-source-manifest.json.\n'
            + 'namespace ' + NAMESPACE + '\n{\n' + aliases + text + '\n}\n')

    text = source('client-source/NpcFriendshipLevelShow.cs')
    text = text.replace('public class NpcFriendshipLevelShow : UIBase\n{', '''public class NpcFriendshipLevelShow : UIBase
{
    private IPartnerRosterUiServices services;
    public void Configure(IPartnerRosterUiServices value) { services = value; }
''')
    text = text.replace('MeManager.MainPlayer.OnNpcFriendshipUpdate', 'services.NpcFriendshipUpdated')
    text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
    text = text.replace('MeManager.MainPlayer.NpcManager.GetByNpcClassId', 'services.GetNpc')
    text = text.replace('.FriendShipLevel', '.FriendshipLevel').replace('.FriendShip', '.FriendshipExp')
    text = text.replace('\t\tNpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = Parsers.Instance.NpcFriendshipLevelInfoParser;\n', '')
    text = text.replace('npcFriendshipLevelInfoParser.GetMaxLevel', 'services.FriendshipMaxLevel')
    text = text.replace('NpcFriendshipLevelInfo npcFriendshipLevelInfo = npcFriendshipLevelInfoParser.Get(mClassId, mFriendshipLevel);',
        'long currentThreshold = services.FriendshipExp(mClassId, mFriendshipLevel);')
    text = text.replace('NpcFriendshipLevelInfo npcFriendshipLevelInfo2 = npcFriendshipLevelInfoParser.Get(mClassId, mFriendshipLevel + 1);',
        'long nextThreshold = services.FriendshipExp(mClassId, mFriendshipLevel + 1);')
    text = text.replace('npcFriendshipLevelInfo2.FriendshipExp', 'nextThreshold').replace('npcFriendshipLevelInfo.FriendshipExp', 'currentThreshold')
    port('NpcFriendshipLevelShow', text)

    text = source('client-source/NpcPanelNpcItem.cs')
    text = text.replace('public class NpcPanelNpcItem : UIBase, IShowSequenceItem\n{', '''public class NpcPanelNpcItem : UIBase, IShowSequenceItem
{
    private IPartnerRosterUiServices services;
    private CancellationTokenSource mMeetLifetime;
    public void Configure(IPartnerRosterUiServices value)
    {
        services = value;
        mFriendshipLevel.Configure(value);
    }
    private void CancelMeetPresentation()
    {
        if (mMeetLifetime != null) { mMeetLifetime.Cancel(); mMeetLifetime.Dispose(); mMeetLifetime = null; }
    }
''')
    text = re.sub(r'\n\tprivate List<AchievementDataKey> mDataKeys = new List<AchievementDataKey>\(\);\n', '\n', text)
    text = re.sub(r'\n\t\[Inject\]\n\tprivate (IResourceLoader ResourceLoader|INpcFriendshipLevelHandler LevelHandler) \{ get; set; \}\n', '\n', text)
    text = text.replace('AssetOperationHandle<Sprite>', 'IProfessionAssetOperation<Sprite>')
    text = text.replace('public Event MeetSound;', 'public AudioClip MeetSound;')
    text = text.replace('MeManager.MainPlayer.OnAddNewNpc', 'services.NpcAdded')
    text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
    text = text.replace('MeManager.MainPlayer.NpcManager.GetByNpcClassId', 'services.GetNpc')
    text = text.replace('LevelHandler.PopupLevelUpDialog', 'services.ShowFriendshipLevelUp')
    text = text.replace('.Release(tryUnloadAsset: true)', '.Dispose()')
    text = text.replace('!MeManager.MainPlayer.IsFunctionLimit(FunctionType.PinNpc)', 'services.CanPinNpc')
    text = text.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(mNpcDataWrap.NpcClassId);',
        'PartnerNpcDefinition npcInfo = services.GetDefinition(mNpcDataWrap.NpcClassId);')
    text = text.replace('ResourceLoader.LoadSprite', 'services.LoadSprite')
    text = text.replace('NpcHelper.GetNpcMeetStatus(MeManager.MainPlayer, npcInfo.Id)', 'services.MeetStatusOf(npcInfo.Id)')
    text = text.replace('mTipDotNodeLinker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(npcInfo.Id))', 'services.BindNpcTipDot(mTipDotNodeLinker, npcInfo.Id)')
    start = text.index('\t\t\tHasItemCondition hasItemCanMeetCondition')
    end = text.index('\n\t\t}', start)
    text = text[:start] + '''\t\t\tPartnerMeetProgress? progress = services.MeetProgress(npcInfo.Id);
            if (!progress.HasValue)
            {
                mNpcMeetProgressItem.gameObject.SetActive(false);
                return;
            }
            mNpcMeetProgressItem.SetProgress(progress.Value.Current, progress.Value.Target);
            mNpcMeetProgressItem.gameObject.SetActive(true);''' + text[end:]
    text = method(text, 'private async void OnAddNewNpc(', '''private async void OnAddNewNpc(int npcId, NpcMeetType type)
    {
        if (mNpcDataWrap == null || mNpcDataWrap.NpcClassId != npcId) return;
        CancelMeetPresentation();
        mMeetLifetime = new CancellationTokenSource();
        CancellationToken token = mMeetLifetime.Token;
        mLockEffectSwitcher.SetState(LockStatus.Locking);
        mNpcDataWrap.MeetStatus = NpcMeetStatus.HadMeet;
        if (MeetSound != null) services.PostSound(MeetSound, gameObject);
        try { await UniTask.Delay(700, false, (PlayerLoopTiming)8, token, false); }
        catch (System.OperationCanceledException) { return; }
        if (token.IsCancellationRequested) return;
        mIsMeeting = false;
        Refresh();
        services.ShowFriendshipLevelUp(services.GetNpc(npcId), 0, 1);
    }''')
    text = method(text, 'public void Init(', '''public void Init(List<NpcRosterItem> dataList, int index)
    {
        InitWrapper(() =>
        {
            CancelMeetPresentation();
            mIsMeeting = false;
            mIndex = index;
            mNpcDataWrapList = dataList;
            mNpcDataWrap = dataList[index];
            mContentRoot.SetActive(false);
        });
    }''')
    text = text.replace('mNpcDataWrap = null;\n\t\tbase.ResetInitState();', 'CancelMeetPresentation();\n\t\tmNpcDataWrap = null;\n\t\tbase.ResetInitState();')
    text = method(text, 'private void OnMeetClick()', '''private async void OnMeetClick()
    {
        if (services.IsNormalNpcDisabled) { services.ShowError(53900); return; }
        if (mIsMeeting || mMeetStatus != NpcMeetStatus.CanMeet || mNpcDataWrap == null) return;
        mIsMeeting = true;
        int npcId = mNpcDataWrap.NpcClassId;
        bool applied = await services.MeetNpc(npcId);
        if (!applied && mNpcDataWrap != null && mNpcDataWrap.NpcClassId == npcId)
        {
            mIsMeeting = false;
            Refresh();
        }
    }''')
    text = method(text, 'private void OnDetailBtnClicked()', '''private void OnDetailBtnClicked()
    {
        if (services.IsNormalNpcDisabled) { services.ShowError(53900); return; }
        if (mMeetStatus == NpcMeetStatus.WaitMeet || mMeetStatus == NpcMeetStatus.UnMeet)
        {
            PartnerNpcDefinition npcInfo = services.GetDefinition(mNpcDataWrap.NpcClassId);
            if (services.HasItemDefinition(npcInfo.ExclusiveGiftId))
                services.ShowExclusiveGift(npcInfo.ExclusiveGiftId, this);
        }
        else
        {
            if (services.MetNpcCount == 0) return;
            List<NpcRosterItem> met = mNpcDataWrapList.Where(npc => npc.MeetStatus == NpcMeetStatus.HadMeet).ToList();
            int index = met.IndexOf(mNpcDataWrap);
            if (index != -1) services.ShowNpcDetails(met, index);
        }
    }''')
    port('NpcPanelNpcItem', text)

    text = source('client-source/NpcPanelNpcList.cs')
    text = text.replace('public class NpcPanelNpcList : MonoBehaviour, LoopScrollDataSource\n{', '''public class NpcPanelNpcList : MonoBehaviour, LoopScrollDataSource
{
    private IPartnerRosterUiServices services;
    public void Configure(IPartnerRosterUiServices value) { services = value; }
''')
    text = text.replace('MeManager.MainPlayer.OnNpcCollectChanged', 'services.NpcCollectedChanged')
    text = text.replace('MeManager.MainPlayer.OnRemoveNpc', 'services.NpcRemoved')
    text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
    text = method(text, 'private void InitNpcList()', '''private void InitNpcList()
    {
        mNpcs.Clear();
        mNpcs.AddRange(services.NpcRoster(mShowAllMeet));
    }''')
    text = method(text, 'private void RefreshLoop()', '''private void RefreshLoop()
    {
        switch (mSortType)
        {
            case CollectSortType.Friendship:
                mNpcs.Sort((x, y) => NpcRosterRules.Compare(x, y, NpcRosterSort.Friendship, mIsReverse, services.CanPinNpc));
                break;
            case CollectSortType.ActivateTime:
                mNpcs.Sort((x, y) => NpcRosterRules.Compare(x, y, NpcRosterSort.ActivateTime, mIsReverse, services.CanPinNpc));
                break;
            default: Debug.LogError("NpcPanelNpcList unsupported sort: " + mSortType); break;
        }
        mLoop.dataSource = this;
        mLoop.totalCount = mNpcs.Count;
        mLoop.RefillCells();
    }''')
    port('NpcPanelNpcList', text)
    scroll = source('client-source/LoopScrollRectUtil.cs')
    scroll = scroll.replace('MathUtils.Clamp(position, 0f, 1f)', 'System.Math.Max(0f, System.Math.Min(1f, position))')
    port('LoopScrollRectUtil', scroll)
    source('common-source/Common/MathUtils.cs')
    for name in ('NpcRoleType', 'NpcMeetType'):
        port(name, source('common-source/Common/' + name + '.cs').replace('namespace Common;\n', ''))
    language = source('common-source/Common/LanguageUtils.cs')
    enum_key, = re.findall(r'\tpublic static string GetEnumKey\(object obj\)\n\t\{\n.*?\n\t\}', language, re.S)
    write('PartnerRosterLanguage.cs', 'namespace ' + NAMESPACE + '\n{\ninternal static class PartnerRosterLanguage\n{\n' + enum_key + '\n}\n}\n')
    write('Zjcs.Official.PartnerRosterUI.asmdef', json.dumps(dict(name='Zjcs.Official.PartnerRosterUI', rootNamespace=NAMESPACE,
        references=['Unity.ugui', 'Zjcs.Official.PartnersUI', 'Zjcs.Official.Partners', 'Zjcs.Official.WorldUI',
                    'Zjcs.Official.ProfessionUI', 'Zjcs.Official.SkillsUI', 'UniTask']), indent=2) + '\n')
    for name in ('common-source/Common/NpcInfo.cs', 'common-source/Common/CompositeCondition.cs',
                 'common-source/Common/HasItemCondition.cs', 'common-source/Common/NpcFriendshipLevelInfoParser.cs'):
        source(name)
    receipt = ROOT / 'assets/reference/partner-roster-ui-source-manifest.json'
    adapters = [DEST / 'PartnerRosterUiServices.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerRosterUiServices.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef']
    receipt.write_text(json.dumps(dict(format=1, official_source=version, source_sha256=inputs, output_sha256=outputs,
        local_adapter_sha256={path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in adapters},
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        adaptations=['Local player/parser/resource/window boundaries; same serialized field names and official branches.',
            'MeetSound resolves the original Wwise event to its converted AudioClip, as existing shared sound components.',
            'Meet request waits one Unity continuation before committing locally; reject restores click availability.',
            'Cancel a 700 ms presentation when a pooled card is reinitialized/disabled to avoid using a recycled row.'],
        scope='Original roster/card/friendship controllers; native prefab/host installation is still required.'), ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Migrated official roster UI controllers:', len(outputs), 'files.')


if __name__ == '__main__':
    main()
