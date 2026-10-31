"""Stage the selected full gift controller and gift blue-dot component."""
import hashlib
import json
from pathlib import Path
import re

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]
DEST = Path('unity/Assets/Game/Runtime/Official/PartnersUI/GiftController')


def build(work):
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, outputs = {}, {}
    folder = work / 'gift-controller-code'
    folder.mkdir(exist_ok=True)

    def source(relative):
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected gift source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        path = folder / name
        path.write_text(text, 'utf8', newline='\n')
        guid = hashlib.md5(('zjcs-partner-ui:' + (DEST / name).as_posix()).encode()).hexdigest()
        importer = 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter'
        metadata = Path(str(path) + '.meta')
        metadata.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + importer
                            + ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
        for entry in (path, metadata):
            outputs[(DEST / entry.name).as_posix()] = hashlib.sha256(entry.read_bytes()).hexdigest()

    def replace(text, old, new):
        if old not in text:
            raise ValueError('Original gift expression changed: ' + old)
        return text.replace(old, new)

    def callback(text, call, replacement):
        start = text.index(call)
        opening = text.index('{', start)
        depth, end = 1, opening + 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        if text[end] != ';':
            raise ValueError('Original service callback terminator changed')
        return text[:start] + replacement + text[start + len(call):end] + ');' + text[end + 1:]

    aliases = '''using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerGiftNpcInfo;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerGiftNpcData;
using PlayerItemDataWrap = Zjcs.Official.ProfessionUI.IProfessionItemData;
using NpcFriendshipLevelInfo = Zjcs.Official.PartnersUI.PartnerGiftLevelInfo;
using NpcFriendshipLevelInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftLevels;
using NpcGiftInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftExperience;
using NpcFriendshipTagInfo = Zjcs.Official.PartnersUI.PartnerGiftTagInfo;
using NpcGiftInfo = Zjcs.Official.PartnersUI.PartnerGiftInfo;
using Player = Zjcs.Official.PartnersUI.IPartnerGiftPlayer;
using NpcMeetType = System.Int32;
using AwardSourceData = System.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
'''

    def port(name, text):
        text = re.sub(r'^using (Common|R3|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        if any(value in text for value in ('Parsers.', 'MeManager.', '[Inject]', 'GLogger', 'ServiceProxyBase', 'WindowManager.')):
            raise ValueError('Unbound official dependency: ' + name)
        write(name + '.cs', '// Selected official controller; see partner-gift-controller-ui-source-manifest.json.\n'
              + 'namespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n')

    text = source('client-source/NpcDetailPanelGiftContent.cs')
    text = replace(text, '''\t[Inject]
\tprivate IResourceLoader ResourceLoader { get; set; }

\t[Inject]
\tprivate IServiceProvider ServiceProvider { get; set; }''', '''    private IPartnerGiftUiServices services;
    public void Configure(IPartnerGiftUiServices value)
    {
        services = value ?? throw new ArgumentNullException(nameof(value));
        mNpcGiftBlueDotItem.Configure(value);
    }''')
    text = replace(text, '\tprivate FontCharacterValidChecker mCharacterValidChecker;\n', '')
    text = replace(text, '\t\tmCharacterValidChecker = ServiceProvider.CreateInstance<FontCharacterValidChecker>(Array.Empty<object>());\n', '')
    text = replace(text, 'private DisposableBag mDisposableBag;', 'private PartnerGiftResourceBag mDisposableBag;')
    text = replace(text, 'mDisposableBag = default;', 'mDisposableBag = new PartnerGiftResourceBag();')
    text = replace(text, 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(tagInfoByRoleTypeAndLevel.TagBg), ref mDisposableBag)', 'mDisposableBag.Add(services.LoadSprite(tagInfoByRoleTypeAndLevel.TagBg))')
    text = replace(text, '\t\tGLoggerErrorInterpolatedStringHandler message;\n\t\tbool enabled;\n', '')
    text, diagnostics = re.subn(r'(?ms)^(?P<indent>\t+)(?:GLoggerErrorInterpolatedStringHandler )?message = new GLoggerErrorInterpolatedStringHandler\([^\n]+\);\n(?P=indent)if \(enabled\)\n(?P=indent)\{\n(?P=indent)\tmessage.AppendLiteral\("(?P<message>[^"\n]+)"\);\n(?P=indent)\}\n(?P=indent)Log.Error\([^\n]+\);',
        lambda match: match['indent'] + 'UnityEngine.Debug.LogError("' + match['message'] + '");', text)
    if diagnostics != 3:
        raise ValueError('Original gift diagnostic count changed')
    text = callback(text, 'ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerNpcSetCustomFriendShipTag(mNpcInfo.Id, input).OnSuccess +=', 'services.SetCustomTag(mNpcInfo.Id, input,')
    text = callback(text, 'ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerGiveNpcGift(mNpcData.NpcClassId, mSelectedGiftDict).OnSuccess += (PlayerRemoveItemsResponse rep) =>', 'services.GiveGift(mNpcData.NpcClassId, mSelectedGiftDict, () =>')
    for old, new in {
        'StateSwitcher<': 'WorldStateSwitcher<',
        'NpcGiftItemCompare': 'PartnerGiftItemCompare',
        'new PartnerGiftItemCompare(npcInfo)': 'new PartnerGiftItemCompare(npcInfo, services)',
        'Parsers.Instance.NpcGiftInfoParser': 'services.Gifts',
        'Parsers.Instance.NpcFriendshipLevelInfoParser': 'services.Levels',
        'MeManager.MainPlayer': 'services.Player',
        'Parsers.Instance.NpcFriendshipTagInfoParser.GetTagInfoByRoleTypeAndLevel': 'services.GetTagInfoByRoleTypeAndLevel',
        'Parsers.GameSettings.NpcFriendshipTagSettings.CanCustomTagFriendShipLevel': 'services.CanCustomTagFriendShipLevel',
        'LanguageUtils.GetNpcFriendshipTagNameKey': 'services.GetNpcFriendshipTagNameKey',
        'WindowManager.Instance.Popup((InputDialog go)': 'services.PopupInput((IPartnerGiftInputDialog go)',
        'WindowManager.Instance.Popup((ConfirmDialog go)': 'services.PopupConfirm((IPartnerGiftConfirmDialog go)',
        'LanguageUtils.TextKeyArgWrap title = new LanguageUtils.TextKeyArgWrap("ui_npc_change_tag");': 'string title = "ui_npc_change_tag";',
        'LanguageUtils.TextKeyArgWrap placeholder = new LanguageUtils.TextKeyArgWrap("ui_npc_input_tag");': 'string placeholder = "ui_npc_input_tag";',
        'mCharacterValidChecker.IsAllCharacterValid': 'services.IsAllCharacterValid',
        'BroadcastManager.ShowErrText': 'services.ShowError',
        'Formula.Instance.CheckNpcFriendshipTagLegal': 'services.CheckNpcFriendshipTagLegal',
        'BroadcastManager.Instance.SystemMsg': 'services.SystemMessage',
        'LanguageManager.Instance.GetText': 'services.GetText',
        'WindowManager.Instance.Close(((Component)go).gameObject)': 'services.Close(go.GameObject)',
        'go.SetContentModerationClickMask(ContentCustomType.NpcFriendShipTag)': 'go.SetNpcFriendshipTagModerationClickMask()',
        'Dictionary<PropType, long> friendshipLevelPropDiff = Parsers.Instance.NpcInfoParser.GetFriendshipLevelPropDiff(mNpcInfo,': 'IReadOnlyDictionary<PropType, long> friendshipLevelPropDiff = services.GetFriendshipLevelPropDiff(mNpcInfo.Properties,',
        '\t\tCommon.NpcAdventureSettings npcAdventureSettings = Parsers.GameSettings.NpcAdventureSettings;\n': '',
        'npcAdventureSettings.IsUnlockTraveNpcProp(services.Player, item.Key)': 'services.IsUnlockTravelNpcProp(item.Key)',
        '\t\t\tnpcFriendshipPropItemGroup.Init(item2.Value, showBg);': '\t\t\tnpcFriendshipPropItemGroup.Configure(services);\n\t\t\tnpcFriendshipPropItemGroup.Init(item2.Value, showBg);',
        'mNpcGiftSelect.Info.PartInfo as NpcGiftInfo': 'services.GetGiftInfo(mNpcGiftSelect.Info)',
        '\t\tcomponent.Init(playerItemDataWrap);': '\t\tservices.ConfigureGiftIcon(component);\n\t\tcomponent.Init(playerItemDataWrap);',
        'Parsers.GameSettings.NpcGiftDefaultPreferenceFactor': 'services.NpcGiftDefaultPreferenceFactor',
    }.items():
        text = replace(text, old, new)
    port('NpcDetailPanelGiftContent', text)

    text = source('client-source/NpcGiftBlueDotItem.cs')
    text = replace(text, 'public class NpcGiftBlueDotItem : RedDotItemBase\n{', '''public class NpcGiftBlueDotItem : ProfessionRedDotItemBase
{
    private IPartnerGiftUiServices services;
    public void Configure(IPartnerGiftUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        base.Configure(value.PlayerBindings);
    }
    private IPartnerGiftUiServices Services => services ?? PartnerGiftUiServices.Required;''')
    for old, new in {
        '\t\tNpcInfoParser npcInfoParser = Parsers.Instance.NpcInfoParser;\n': '',
        'npcInfoParser.Get': 'Services.GetNpcInfo',
        'MeManager.MainPlayer': 'Services.Player',
        'Parsers.Instance.NpcInfoParser.Get': 'Services.GetNpcInfo',
        'Parsers.Instance.NpcGiftInfoParser': 'Services.Gifts',
        'Parsers.Instance.NpcFriendshipLevelInfoParser': 'Services.Levels',
        'itemData.Info.ItemType == ItemType.NpcGift': 'Services.IsNpcGift(itemData.Info)',
        'mainPlayer.IsFunctionLimit(FunctionType.NPCAddressList)': 'mainPlayer.IsAddressListFunctionLimited',
        'mainPlayer.NpcManager.GetByNpcClassId': 'mainPlayer.GetNpcByClassId',
        'Services.Player.NpcManager': 'Services.Player.Npcs',
    }.items():
        text = replace(text, old, new)
    port('NpcGiftBlueDotItem', text)
    text = source('client-source/NpcFriendshipLevelContent.cs')
    text = replace(text, 'public class NpcFriendshipLevelContent : UIBase\n{', '''public class NpcFriendshipLevelContent : UIBase
{
    private IPartnerGiftUiServices services;
    public void Configure(IPartnerGiftUiServices value)
    { services = value ?? throw new ArgumentNullException(nameof(value)); }''')
    start = text.index('\t\tType type = ', text.index('private void OnClickTipBtn'))
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''        services.PopupFriendship(this, mNpcInfo.IsStarGodNpc, dialog =>
        {
            if (dialog != null) dialog.Init(mNpcInfo);
        });''' + text[end:]
    for old, new in {
        'StateSwitcher<': 'WorldStateSwitcher<',
        'MeManager.MainPlayer.NpcManager.GetByNpcClassId': 'services.Player.GetNpcByClassId',
        'MeManager.MainPlayer': 'services.Player',
        'Parsers.Instance.NpcFriendshipLevelInfoParser': 'services.Levels',
        'Parsers.Instance.NpcGiftInfoParser': 'services.Gifts',
        'CultureNumberArg.Compact': 'services.Compact',
    }.items():
        text = replace(text, old, new)
    port('NpcFriendshipLevelContent', text)
    for relative in ('client-source/FontCharacterValidChecker.cs', 'client-source/RedDotItemBase.cs',
                     'common-source/NpcGiftItemCompare.cs', 'common-source/ItemComparerBase.cs',
                     'common-source/Common/NpcGiftInfoParser.cs', 'common-source/Common/NpcFriendshipLevelInfoParser.cs',
                     'common-source/Common/NpcFriendshipTagInfoParser.cs'):
        source(relative)
    for name in ('PartnerGiftUiServices.cs', 'PartnerGiftItemCompare.cs', 'PartnerGiftResourceBag.cs'):
        write(name, (ROOT / 'scripts/templates/partner_gift_controller' / name).read_text('utf8'))
    write('Zjcs.Official.PartnerGiftControllerUI.asmdef', json.dumps(dict(
        name='Zjcs.Official.PartnerGiftControllerUI', rootNamespace='Zjcs.Official.PartnersUI',
        references=['Unity.ugui', 'Zjcs.Official.PartnerGiftUI', 'Zjcs.Official.WorldUI',
                    'Zjcs.Official.ProfessionUI']), indent=2) + '\n')
    receipt = dict(official_source=version, source_sha256=inputs, output_sha256=outputs,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        adaptations=['Explicit local configuration/player/resource, input/confirm-window and asynchronous transaction ports.',
            'Original gift selection, sorting, preview, rank/overflow checks, confirmation and callback order.',
            'Existing migrated original UIBase, ItemIcon, property group pool, loop scroll, state switcher and red-dot base.',
            'Unity diagnostics replace decompiled logging handlers; configure pooled property rows before their original Init.'],
        scope='Complete gift controller and blue dot staged; production native panel host/provider remain pending.')
    (work / 'partner-gift-controller-ui-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged complete official gift controller:', len(outputs), 'outputs')


if __name__ == '__main__':
    build(validation_directory())
