"""Port the selected official star-god list, grouped cards and image locator."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_partner_dialogue_list_ui import meta
from prepare_partner_roster_ui import method

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster'
RECEIPT = ROOT / 'assets/reference/partner-star-god-ui-source-manifest.json'


def main():
    version = OfficialSource().version
    inputs, outputs = {}, {}

    def source(name, folder='client-source'):
        relative = folder + '/' + name + '.cs'
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected official source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        path = DEST / (name + '.cs')
        path.write_text('\n'.join(line.rstrip() for line in text.splitlines()) + '\n', 'utf8', newline='\n')

    def port(name, text):
        text = re.sub(r'^using (?:Common|AK.Wwise|R3|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<').replace('PlayerNpcDataWrap', 'NpcRosterItem')
        text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
        text = text.replace('MeManager.MainPlayer.OnNpcCollectChanged', 'services.NpcCollectedChanged')
        text = text.replace('MeManager.MainPlayer.OnAddNewNpc', 'services.NpcAdded')
        text = text.replace('MeManager.MainPlayer.OnNpcFriendshipLevelUpdate', 'services.NpcFriendshipLevelUpdated')
        text = text.replace('MeManager.MainPlayer.NpcManager.GetByNpcClassId', 'services.GetNpc')
        text = text.replace('MeManager.MainPlayer.NpcManager.Values', 'services.OwnedNpcs')
        text = text.replace('NpcHelper.GetNpcMeetStatus(MeManager.MainPlayer, ', 'services.MeetStatusOf(')
        text = text.replace('mNpcData.FriendShipLevel', 'mNpcData.FriendshipLevel')
        if name not in ('ImageLocator', 'StarGodNpcItemContent'):
            assert 'MeManager' not in text and 'Parsers.' not in text and 'GLogger' not in text
        aliases = '''using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
'''
        write(name, '// Ported selected official client; see partner-star-god-ui-source-manifest.json.\n'
            + 'namespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n')

    text = source('NpcPanelStarGodContent')
    declaration = 'public class NpcPanelStarGodContent : MonoBehaviour, LoopScrollDataSource\n{'
    text = text.replace(declaration, declaration + '''
    private IPartnerStarGodUiServices services;
    public void Configure(IPartnerStarGodUiServices value)
    {
        if (services != null) throw new System.InvalidOperationException("Star-god list is already configured.");
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        if (isActiveAndEnabled) OnEnable();
    }
''')
    text = text.replace('MeManager.MainPlayer.OnNpcCollectChanged +=', 'if (services == null) return;\n\t\tMeManager.MainPlayer.OnNpcCollectChanged +=')
    text = text.replace('MeManager.MainPlayer != null', 'services != null && services.MainPlayerAvailable')
    text = text.replace('Parsers.Instance.NpcInfoParser.GetNpcs(NpcType.MapExploreBoss)', 'services.Definitions')
    text = text.replace('foreach (NpcInfo npc', 'foreach (PartnerStarGodNpcDefinition npc')
    text = text.replace('if (!npc.IsStarGodNpc)', 'if (npc.IsMapExploreBoss || !npc.IsStarGodNpc)')
    text = text.replace('PlayerNpcDataWrap.CreateNpcDataByInfo(MeManager.MainPlayer.Id, npc, 1)', 'services.CreatePreviewNpc(npc, 1)')
    text = text.replace('NpcComparer.sDefault.SortFriendshipPriority(mStarGodNpcs, isReverse: false, MeManager.MainPlayer);',
        'mStarGodNpcs.Sort((x, y) => PartnerStarGodListRules.Compare(x, y, services.CanPinNpc));')
    port('NpcPanelStarGodContent', text)
    port('StarGodNpcItemGroupItem', source('StarGodNpcItemGroupItem'))
    port('StarGodNpcItemContent', source('StarGodNpcItemContent'))
    source('ImageLocator')  # Reuse the existing original ProfessionUI locator.

    text = source('StarGodNpcItem')
    text = text.replace('public class StarGodNpcItem : UIBase\n{', '''public class StarGodNpcItem : UIBase
{
    private IPartnerStarGodUiServices services;
    private CancellationTokenSource mMeetLifetime;
    public void Configure(IPartnerStarGodUiServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));
    private void CancelMeetPresentation()
    {
        if (mMeetLifetime != null) { mMeetLifetime.Cancel(); mMeetLifetime.Dispose(); mMeetLifetime = null; }
    }
''')
    start = text.index('\t[Inject]')
    end = text.index('\n\tpublic void Init(', start)
    text = text[:start] + text[end:]
    start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
    end = text.index('\n\t\t}', start)
    text = text[:start] + '\t\t\tDebug.LogError("StarGodNpcItem init npcData is null!");' + text[end:]
    text = text.replace('mNpcTipDotLinker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(mNpcData.NpcClassId));',
        'services.BindNpcTipDot(mNpcTipDotLinker, mNpcData.NpcClassId);')
    text = text.replace('public Event MeetSound;', 'public AudioClip MeetSound;')
    text = text.replace('private DisposableBag mNpcImageDisposableBag;', 'private readonly List<System.IDisposable> mNpcImageDisposableBag = new List<System.IDisposable>();')
    text = text.replace('mNpcImageDisposableBag = default;', '')
    text = text.replace('mNpcImageDisposableBag.Dispose();', 'CancelMeetPresentation();\n\t\tforeach (var lease in mNpcImageDisposableBag) lease.Dispose();\n\t\tmNpcImageDisposableBag.Clear();')
    text = text.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(mNpcData.NpcClassId);',
        'PartnerStarGodNpcDefinition npcInfo = services.GetDefinition(mNpcData.NpcClassId);')
    text = text.replace('NpcStarGodInfo npcStarGodInfo = Parsers.Instance.NpcStarGodInfoParser.Get(mNpcData.NpcClassId);',
        'string locationKey = npcInfo.LocationDescKey;')
    text = text.replace('if (npcStarGodInfo != null)', 'if (locationKey != null)')
    text = text.replace('npcStarGodInfo.GetLocationDescKey()', 'locationKey')
    text = text.replace('npcInfo.TryGetFullBodyImagePath(ENpcFullBodyImageType.Static, out var path)', '!string.IsNullOrEmpty(npcInfo.StaticFullBodyImagePath)')
    text = text.replace('AssetOperationHandle<Sprite> assetOperationHandle = Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(path), ref mNpcImageDisposableBag);',
        'IProfessionAssetOperation<Sprite> assetOperationHandle = services.LoadSprite(npcInfo.StaticFullBodyImagePath);\n\t\t\t\tmNpcImageDisposableBag.Add(assetOperationHandle);')
    text = text.replace('!MeManager.MainPlayer.IsFunctionLimit(FunctionType.PinNpc)', 'services.CanPinNpc')
    text = text.replace('ServerInfoManager.Instance.CheckFunctionDisabled(MeManager.MainPlayer, ServerFunctionType.StarGodNpcAll)', 'services.IsStarGodNpcDisabled')
    text = text.replace('BroadcastManager.ShowErrText(53897, null, false)', 'services.ShowError(53897)')
    text = text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerMeetNpc(mNpcData.NpcClassId)', 'services.RequestMeetNpc(mNpcData.NpcClassId)')
    text = text.replace('Parsers.Instance.NpcInfoParser.Get(npc.NpcClassId)?.IsStarGodNpc', 'services.GetDefinition(npc.NpcClassId)?.IsStarGodNpc')
    start = text.index('\t\t\t\tWindowManager.Instance.Popup(')
    end = text.index('\n\t\t\t}', start)
    text = text[:start] + '\t\t\t\tservices.ShowStarGodDetails(hadMeetStarGodNpcList, index);' + text[end:]
    text = text.replace('NpcHelper.GetMeetNpcItems(', 'services.GetMeetNpcItems(')
    text = text.replace('MeManager.MainPlayer.GetItemAmount(', 'services.GetItemAmount(')
    text = text.replace('list[i].Init(num2);', 'services.PrepareItemIcon(list[i]);\n\t\t\t\tlist[i].Init(num2);')
    text = method(text, 'private async void OnAddNewNpc(', '''private async void OnAddNewNpc(int npcId, NpcMeetType type)
    {
        if (mNpcData == null || mNpcData.NpcClassId != npcId) return;
        CancelMeetPresentation();
        mMeetLifetime = new CancellationTokenSource();
        CancellationToken token = mMeetLifetime.Token;
        mNpcData.MeetStatus = NpcMeetStatus.HadMeet;
        if (MeetSound != null) services.PostSound(MeetSound, gameObject);
        mIsMeetingStateSwitcher.SetState(true);
        mAnimation.Play(mUnlockAnimation.name);
        try { await UniTask.WaitForSeconds(mUnlockAnimation.length * mPopMeetDelayFactor, false, (PlayerLoopTiming)8, token, false); }
        catch (System.OperationCanceledException) { return; }
        if (token.IsCancellationRequested) return;
        NpcRosterItem byNpcClassId = services.GetNpc(npcId);
        if (mNpcData != byNpcClassId) mNpcData = byNpcClassId;
        services.ShowFriendshipLevelUp(byNpcClassId, 0, 1);
        mIsMeetingStateSwitcher.SetState(false);
    }''')
    port('StarGodNpcItem', text)
    for name, folder in [('NpcComparer', 'common-source'), ('ItemComparerBase', 'common-source'),
                         ('NpcInfoParser', 'common-source/Common'), ('NpcInfo', 'common-source/Common'),
                         ('NpcStarGodInfo', 'common-source/Common'), ('NpcHelper', 'common-source'),
                         ('PlayerNpcDataWrap', 'common-source/Common')]:
        source(name, folder)
    for name in ('NpcPanelStarGodContent', 'StarGodNpcItem', 'StarGodNpcItemGroupItem', 'StarGodNpcItemContent',
                 'PartnerStarGodUiServices', 'PartnerStarGodListRules'):
        path = DEST / (name + '.cs')
        for output in (path, meta(path)):
            outputs[output.relative_to(ROOT).as_posix()] = hashlib.sha256(output.read_bytes()).hexdigest()
    RECEIPT.write_text(json.dumps(dict(manifest=version, inputs=inputs, outputs=outputs,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        adaptation='Original list/cards/group/animation event/image locator; explicit player/config/icon/resource/window services. Cancel obsolete pooled-card animation waits.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    main()
