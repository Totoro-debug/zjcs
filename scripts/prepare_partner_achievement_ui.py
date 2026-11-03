"""Restore the original friendship achievement panel controllers, with local ports."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_gift_configuration import method
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review friendship achievement UI source')
    inputs, outputs = {}, {}
    destination = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/Achievements'

    def read(relative):
        path = ROOT / '.tools/source-packages/current' / version / 'official-code' / relative
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def change(text, old, new):
        if old not in text: raise ValueError('Original achievement expression changed: ' + old)
        return text.replace(old, new)

    aliases = '''using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using AchievementRules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;
using AchievementType = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementType;
using AchievementState = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementState;
using AchievementInfo = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementInfo;
using AchievementDataKey = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementDataKey;
using AchievementDataBase = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementData;
using Player = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.Player;
using PlayerAchievementRecordDataWrap = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.PlayerAchievementRecordDataWrap;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Object = UnityEngine.Object;
'''

    def write(name, text):
        for original in ('AchievementPanelBase', 'AchievementPanelItem', 'AchievementPanel'):
            text = re.sub(r'\b' + original + r'\b', 'Partner' + original, text)
        text = text.replace('public PartnerAchievementPanelBase PartnerAchievementPanelBase;',
            'public PartnerAchievementPanelBase AchievementPanelBase;')
        text = text.replace('PartnerAchievementPanelBase.Configure(', 'AchievementPanelBase.Configure(')
        text = text.replace('PartnerAchievementPanelBase.InitDataKey(', 'AchievementPanelBase.InitDataKey(')
        text = text.replace('"PartnerAchievementPanel.', '"AchievementPanel.')
        text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?|NaughtyAttributes);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('CultureNumberArg.Compact(', 'services.Compact(')
        text = '// Selected original friendship achievement UI; see partner-achievement-ui-source-manifest.json.\nnamespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n'
        text = '\n'.join(line.expandtabs(4).rstrip() for line in text.splitlines()) + '\n'
        path = destination / ('Partner' + name + '.cs')
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.is_file() or path.read_text('utf8') != text: path.write_text(text, 'utf8', newline='\n')
        meta = metadata(path)
        for p in (path, meta): outputs[p.relative_to(ROOT).as_posix()] = hashlib.sha256(p.read_bytes()).hexdigest()

    text = read('client-source/AchievementPanel.cs')
    text = text.replace('\t[SearchableEnum]\n', '')
    text = change(text, 'private void Awake()\n\t{\n\t\tInitDataKey();\n\t}', '''private IPartnerAchievementUiServices services;
    public void Configure(IPartnerAchievementUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        AchievementPanelBase.Configure(value);
        InitDataKey();
    }''')
    text = change(text, 'AchievementDataKey.Create(AchievementType, JsonParam)', 'services.Rules.CreateKey(AchievementType, JsonParam)')
    write('AchievementPanel', text)

    text = read('client-source/AchievementPanelBase.cs')
    text = text[:text.index('\n\tprivate void InitTopFirstPlayerScore()')] + '\n}\n'
    text = change(text, '\tprivate AchievementParamInfo mAchievementParamInfo;\n', '')
    start = text.index('\t[Inject]'); end = text.index('\n\tprivate void Awake()', start)
    text = text[:start] + '''    private IPartnerAchievementUiServices services;
    private int AchievementPanelShowCount => services.AchievementPanelShowCount;
    public void Configure(IPartnerAchievementUiServices value)
    {
        services = value ?? throw new ArgumentNullException(nameof(value));
        foreach (var item in mPanelItemList) item.Configure(value);
    }
''' + text[end:]
    text = change(text, 'AchievementRawInfoParser achievementRawInfoParser = Parsers.Instance.AchievementRawInfoParser;', 'IPartnerAchievementUiServices achievementRawInfoParser = services;')
    text = change(text, 'Parsers.GameSettings.AchievementTypeListForShowAllInfosInSameType.Contains(', 'services.ShowAllInfosInSameType(')
    text = change(text, 'MeManager.MainPlayer', 'services.Player')
    text = change(text, 'AchievementUtils.GetAchievementStateSearchRange(', 'GetAchievementStateSearchRange(')
    text = text.replace('''		if (Parsers.GameSettings.AchievementTopTypeList.Contains(mCurrentType))
		{
			RefreshTopData();
		}
''', '')
    start = text.index('\n\tprivate void RefreshTopData()'); end = text.index('\n\tprivate void InitPanelItems()', start)
    text = text[:start] + text[end:]
    start = text.index('\n\tprivate void InitPanelItemsTopData()'); end = text.index('\n\tprivate void HidePanelItems()', start)
    text = text[:start] + text[end:]
    start = text.index('\t\t\tAchievementDataBase data = ', text.index('private void SetAchievementPanelTitle'))
    end = text.index('\n\t\t}', start)
    text = text[:start] + '''            if ((Object)(object)mPanelTitle != (Object)null)
                mPanelTitle.SetKey("ui_achievement_title_" + info.AchievementType);''' + text[end:]
    original = read('common-source/Common/AchievementUtils.cs')
    search = method(original, 'public static (int startIndex, int endIndex) GetAchievementStateSearchRange(Player player, IReadOnlyList<AchievementInfo> achievementInfos, int showCount)')
    text = text[:text.rfind('}')] + search + '\n}\n'
    write('AchievementPanelBase', text)

    text = read('client-source/AchievementPanelItem.cs')
    text = change(text, ' : UIBase, IGameObjectPoolItem', ' : UIBase')
    text = change(text, '\tprivate AchievementTask mAchievementTask;\n', '')
    start = text.index('\t[Inject]'); end = text.index('\n\tprivate void Awake()', start)
    text = text[:start] + '''    private IPartnerAchievementUiServices services;
    public void Configure(IPartnerAchievementUiServices value) => services = value ?? throw new ArgumentNullException(nameof(value));
''' + text[end:]
    text = text.replace('\t\tMeManager.MainPlayer.OnUpdateBlackboardData += MainPlayer_OnUpdateBlackboardData;\n', '')
    start = text.index('\t\tif (MeManager.MainPlayer != null)'); end = text.index('\n\t\tRecycleItemIcons();', start)
    text = text[:start] + text[end:]
    start = text.index('\n\tprivate void MainPlayer_OnUpdateBlackboardData('); end = text.index('\n\tpublic void Init(', start)
    text = text[:start] + text[end:]
    text = change(text, 'Parsers.Instance.FunctionLimitInfoParser.IsLimit(MeManager.MainPlayer, FunctionType.AchievementGroup)', 'services.AchievementGroupLimited')
    text = change(text, 'ItemIconManager', 'services')
    text = change(text, 'MeManager.MainPlayer', 'services.Player')
    for key in ('CanNotGetReward', 'AlreadyGotReward'):
        text = change(text, 'BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("AchievementPanel.' + key + '"));', 'services.ShowSystemMessage("AchievementPanel.' + key + '");')
    start = text.index('\t\t\tServiceProxyBase<'); end = text.index('\n\t\t}', start)
    text = text[:start] + '''            services.Claim(info.Group, -1, mReceiveSameType, () =>
            {
                mGetRewardCallBack?.Invoke(info.Group, -1);
            });''' + text[end:]
    start = text.index('\n\tprivate void RefreshProgress()'); end = text.index('\n\tprivate void SetProgressValue(', start)
    text = text[:start] + '''
    private void RefreshProgress()
    {
        var progress = services.GetProgress(mAchievementInfoAndState.info);
        SetProgressValue(progress.maxValue, progress.currentValue);
    }
''' + text[end:]
    start = text.index('\t\t\tAchievementDataBase data = ', text.index('private void SetAchievementItemTitle'))
    end = text.index('\n\t\t}', start)
    text = text[:start] + '''            if (info.AchievementType == AchievementType.NpcFriendshipLevel)
                TitleText.SetKey("ui_achievement_content_NpcFriendshipLevel", services.GetNpcName(info.AchievementData.NpcClassId));
            else TitleText.SetKey("ui_achievement_content_" + info.AchievementType);''' + text[end:]
    write('AchievementPanelItem', text)
    for name in ('AchievementContentHandler', 'AchievementContentNpcFriendshipLevel', 'AchievementContentBase'):
        read('client-source/' + name + '.cs')
    for name in ('AchievementTask', 'AchievementTaskNpcFriendshipLevel', 'AchievementTaskStarGodNpcFriendshipLevel'):
        read('common-source/Common/' + name + '.cs')
    proof = dict(manifest=version, generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), inputs=inputs, outputs=outputs,
        adaptations=['Original panel key/list/range/filter/sort/callback and item state/progress/reward bindings; only the two selected friendship types.',
            'Replace parser/player/content/icon/RPC endpoints with explicit local services; omit unrelated ranking and blackboard paths (both friendship tasks have no progress blackboard key).',
            'Keep profession-only achievement port unchanged; native original script references explicitly bind to these partner controllers.'])
    (work / 'partner-achievement-ui-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original friendship achievement controllers')


if __name__ == '__main__': main()
