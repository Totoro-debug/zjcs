"""Port the six selected original conversation playback controllers."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource

TARGET = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/DialoguePlayback'
NAMES = ('NpcDialoguePanel', 'NpcDialoguePanelItem', 'NpcDialoguePanelItemContent',
    'NpcDialoguePanelOption', 'NpcDialoguePanelOptionItem', 'NpcDialoguePanelRewardItemContent', 'NpcDialogueAwardHandler')
SUPPORT = ('StaticHyperLinkClickListener', 'HyperLinkInfoProvider', 'UITouchPass')


def sha(raw): return hashlib.sha256(raw).hexdigest()


def metadata(path):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        key = hashlib.md5(('zjcs:partner-dialogue-playback:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        meta.write_text('fileFormatVersion: 2\nguid: ' + key + '\n', 'utf8', newline='\n')
    return meta


def port(name, source):
    text = source.decode('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
    text = re.sub(r'^\[UIWindow\([^\n]*\)\]\n', '', text, flags=re.M)
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    # Keep all branches and return paths; adapt only unavailable log handlers.
    text, count = re.subn(r'(?m)^(\t+)(?:GLogger\w+InterpolatedStringHandler )?(message\d*) = new GLogger\w+InterpolatedStringHandler\([^\n]*\);\n.*?^\1Log\.(Error|Info)\([^\n]*\);',
        lambda m: m[1] + 'Debug.Log' + ('Error' if m[3] == 'Error' else '') + '("' + name + ': original ' + m[3] + ' branch.");', text, flags=re.S)
    text = re.sub(r'^\t+(?:GLogger\w+InterpolatedStringHandler message;|bool enabled;)\n', '', text, flags=re.M)
    for old, new in [('PlayerNpcDialogueDataWrap', 'PartnerDialogueData'),
        ('NpcDialogueInfo', 'PartnerConversationNode'), ('ITextProcessor', 'IWorldTextProcessor'),
        ('StateSwitcher<', 'WorldStateSwitcher<'), ('DialogueSpokesMan', 'PartnerDialogueSpokesMan'),
        ('MeManager.MainPlayer.OnNpcDialogueUpdate', 'services.DialogueUpdated'),
        ('MeManager.MainPlayer != null', 'services.MainPlayerAvailable'),
        ('Parsers.Instance.PartnerConversationNodeParser.Get', 'services.Configuration.Get'),
        ('PartnerConversationNodeParser.sDialogueStartNodeId', 'services.DialogueStartNodeId'),
        ('.DialogueNodes.Data', '.DialogueNodes'),
        ('LanguageUtils.GetNpcDialogueTextKey', 'PartnerDialogueListRules.GetNpcDialogueTextKey'),
        ('LanguageUtils.GetNpcDialogueOptionKey', 'PartnerDialoguePlaybackRules.GetNpcDialogueOptionKey'),
        ('new PlayerNameTextProcessor(MeManager.MainPlayer?.Name ?? "")', 'new PartnerDialoguePlayerNameProcessor(services.PlayerName ?? "")'),
        ('((Object)(object)', '((UnityEngine.Object)(object)'), ('(Object)null', '(UnityEngine.Object)null')]:
        text = text.replace(old, new)
    text = text.replace('npcDialogueInfo.SpokesMan', '((PartnerDialogueSpokesMan)npcDialogueInfo.SpokesMan)')
    text = text.replace('info.SpokesMan', '((PartnerDialogueSpokesMan)info.SpokesMan)')
    if name == 'NpcDialoguePanel':
        text = text.replace('RPCOperation<object> yco = ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerNpcDialogueAck(mData.DialogueId, dialogueNodeId, invokeByPlayer: false);',
            'PartnerDialogueAck yco = services.Acknowledge(mData.DialogueId, dialogueNodeId, false);')
        text = text.replace('npcDialogueInfo.UnlockCondition.IsMet(MeManager.MainPlayer)', 'services.IsNodeUnlocked(DialogueClassId, dialogueNodeId)')
        text = text.replace('MeManager.MainPlayer.NpcManager', 'services.Npcs')
        text = text.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(obj.NpcClassId);', 'string nameKey = services.GetNpcNameKey(obj.NpcClassId);')
        text = text.replace('mPanelTitle.SetKey(npcInfo.NameKey)', 'mPanelTitle.SetKey(nameKey)')
    elif name == 'NpcDialoguePanelItemContent':
        text = text.replace('private PlayerIcon mPlayerIcon;', 'private Component mPlayerIcon;')
        start = text.index('\t\tlong id = MeManager.MainPlayer.Id;')
        end = text.index('\n\t\t((Component)mPlayerIcon).gameObject.SetActive(true);', start)
        text = text[:start] + '\t\tservices.InitializeActorIcon(mPlayerIcon, mActorId, (PartnerDialogueSpokesMan)info.SpokesMan);' + text[end:]
    elif name == 'NpcDialoguePanelOptionItem':
        text = text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerNpcDialogueAck(mDialogueId, mDialogueNodeId);',
            'services.Acknowledge(mDialogueId, mDialogueNodeId, true);')
    elif name == 'NpcDialoguePanelRewardItemContent':
        text = text.replace('\t[Inject]\n\tprivate IItemIconManager ItemIconManager { get; set; }\n', '')
        text = text.replace('ItemInfo itemInfo = Parsers.Instance.ItemInfoParser.Get(mRewardItem.Key);', 'ItemIcon itemIcon = services.GetRewardIcon(mRewardItem.Key, mRewardSlot);')
        text = text.replace('if (itemInfo != null)', 'if (itemIcon != null)')
        text = text.replace('ItemIcon itemIcon = ItemIconManager.GetIcon(itemInfo, mRewardSlot).SetClickPopupInfoDialog()', 'itemIcon.SetClickPopupInfoDialog()')
        text = text.replace('ItemIconManager.RecycleIcon', 'services.RecycleRewardIcon')
    elif name == 'NpcDialogueAwardHandler':
        text = text.replace('PlayerAwardMsg', 'object')
        text = text.replace('AwardManager.Instance.', 'services.')
        text = text.replace('msg.SourceData.SourceOperate != SourceOperate.NpcDialogue', '!services.IsConversationAward(msg)')
        text = text.replace('WindowManager.Instance.Popup((AwardPanel go) =>\n\t\t{\n\t\t\tgo.Init(msg);\n\t\t});', 'services.PopupAward(msg);')
    elif name == 'StaticHyperLinkClickListener':
        text = text.replace('\t[Inject]\n\tprivate INavigateManager NavigateManager { get; set; }\n', '')
        text = text.replace('Object.op_Implicit((UnityEngine.Object)(object)mUiTouchPass)', 'mUiTouchPass != null')
        text = text.replace('TryGetComponent<IHyperLinkInfoProvider>(ref mProvider)', 'TryGetComponent<IHyperLinkInfoProvider>(out mProvider)')
        text = text.replace('HyperLinkClickHandlerFactory.Create', 'services.CreateHandler')
        text = text.replace('HyperLinkClickHandler hyperLinkClickHandler', 'IPartnerStaticHyperLinkHandler hyperLinkClickHandler')
        text = text.replace('hyperLinkClickHandler.Init(this, NavigateManager)', 'hyperLinkClickHandler.Init(this)')
        text = text.replace('mProvider = ((Component)this).gameObject.AddComponent<HyperLinkInfoProvider>();\n\t\t}',
            'mProvider = ((Component)this).gameObject.AddComponent<HyperLinkInfoProvider>();\n\t\t}\n        if (mProvider is HyperLinkInfoProvider provider) provider.Configure(services);')
    elif name == 'HyperLinkInfoProvider':
        text = text.replace('Parsers.Instance.HyperLinkInfoParser.Get', 'services.GetHyperLinkInfo')
    elif name == 'UITouchPass':
        text = text.replace('\t[Inject]\n\tprivate IEventSystemManager EventSystemManager { get; set; }\n', '')
        text = text.replace('EventSystemManager.ForeachRaycastResults', 'services.ForeachRaycastResults')
        text = text.replace('EventFunction<', 'ExecuteEvents.EventFunction<')
    if name in SUPPORT: text = re.sub(r'\bHyperLinkInfo\b', 'PartnerStaticHyperLinkInfo', text)
    service_type = ('IPartnerDialogueAwardServices' if name == 'NpcDialogueAwardHandler'
        else 'IPartnerStaticHyperLinkServices' if name in SUPPORT else 'IPartnerDialoguePlaybackServices')
    configure = '\n    private ' + service_type + ' services;\n    public void Configure(' + service_type + ' value) => services = value ?? throw new System.ArgumentNullException(nameof(value));\n'
    if name == 'StaticHyperLinkClickListener':
        text = text.replace('if (mProvider is HyperLinkInfoProvider provider) provider.Configure(services);',
            'if (services != null && mProvider is HyperLinkInfoProvider provider) provider.Configure(services);')
        configure = '\n    private IPartnerStaticHyperLinkServices services;\n    public void Configure(IPartnerStaticHyperLinkServices value)\n    {\n        services = value ?? throw new System.ArgumentNullException(nameof(value));\n        if (mProvider is HyperLinkInfoProvider provider) provider.Configure(services);\n    }\n'
    index = text.index('{', text.index('public class ' + name)) + 1
    text = text[:index] + configure + text[index:]
    for token in ('GLogger', 'ServiceProxy', 'MeManager', 'Parsers.', 'LanguageUtils.', 'NpcDialogueInfo', '[Inject]', 'IItemIconManager'):
        if token in text: raise ValueError(name + ': unadapted boundary ' + token)
    result = ('// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.\n'
        'namespace Zjcs.Official.PartnersUI\n{\nusing Zjcs.Official.Partners;\nusing Zjcs.Official.ProfessionUI;\n'
        'using Zjcs.Official.WorldUI;\nusing Zjcs.Official.StoryBubbles;\nusing Object = UnityEngine.Object;\n'
        'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n' + text + '\n}\n')
    return '\n'.join(line.rstrip() for line in result.splitlines()) + '\n', count


def main():
    version = OfficialSource().version
    TARGET.mkdir(parents=True, exist_ok=True)
    inputs, outputs, logs = {}, {}, {}
    for name in NAMES + SUPPORT:
        path = ROOT / '.tools/source-packages/official-code/client-source' / (name + '.cs')
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code/client-source' / path.name).read_bytes():
            raise ValueError('Playback controller differs from selected source: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        content, logs[name] = port(name, raw)
        target = TARGET / path.name
        target.write_text(content, 'utf8', newline='\n')
    enum_path = ROOT / '.tools/source-packages/official-code/common-source/Common/DialogueSpokesMan.cs'
    raw = enum_path.read_bytes()
    if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code/common-source/Common' / enum_path.name).read_bytes():
        raise ValueError('Original speaker enum differs')
    inputs[enum_path.relative_to(ROOT).as_posix()] = sha(raw)
    enum = raw.decode('utf-8-sig').replace('\r\n', '\n').replace('namespace Common;', 'namespace Zjcs.Official.PartnersUI\n{').replace('DialogueSpokesMan', 'PartnerDialogueSpokesMan') + '\n}\n'
    (TARGET / 'PartnerDialogueSpokesMan.cs').write_text(enum, 'utf8', newline='\n')
    language = ROOT / '.tools/source-packages/official-code/common-source/Common/LanguageUtils.cs'
    raw = language.read_bytes()
    if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code/common-source/Common/LanguageUtils.cs').read_bytes():
        raise ValueError('Original language helper differs')
    inputs[language.relative_to(ROOT).as_posix()] = sha(raw)
    for relative in ('client-source/IHyperLinkInfoProvider.cs', 'common-source/Common/HyperLinkInfo.cs',
        'client-source/TMP_TypeWriterEffect.cs'):
        path = ROOT / '.tools/source-packages/official-code' / relative
        if path.read_bytes() != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected playback dependency source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    from prepare_partner_gift_configuration import method
    original = method(raw.decode('utf-8-sig').replace('\r\n', '\n'),
        'public static string GetNpcDialogueOptionKey(int dialogueClassId, int dialogueNodeId)')
    (TARGET / 'PartnerDialoguePlaybackRules.cs').write_text(
        'namespace Zjcs.Official.PartnersUI\n{\npublic static class PartnerDialoguePlaybackRules\n{\n' + original + '\n}\n}\n',
        'utf8', newline='\n')
    if logs != dict(zip(NAMES + SUPPORT, (6, 1, 2, 0, 1, 0, 0, 1, 1, 0))): raise ValueError('Reinspect changed logging branches')
    definition = dict(name='Zjcs.Official.PartnerDialoguePlaybackUI', rootNamespace='Zjcs.Official.PartnersUI',
        references=['Zjcs.Official.Partners', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.PartnersUI',
            'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Official.StoryBubbles', 'Unity.ugui', 'Unity.TextMeshPro'])
    (TARGET / 'Zjcs.Official.PartnerDialoguePlaybackUI.asmdef').write_text(json.dumps(definition, indent=2) + '\n', 'utf8', newline='\n')
    for path in TARGET.iterdir():
        if path.suffix not in ('.cs', '.asmdef'): continue
        for result in (path, metadata(path)): outputs[result.relative_to(ROOT).as_posix()] = sha(result.read_bytes())
    folder_meta = metadata(TARGET)
    outputs[folder_meta.relative_to(ROOT).as_posix()] = sha(folder_meta.read_bytes())
    receipt = dict(manifest=version, source_sha256=inputs, installed_output_sha256=outputs,
        logging_adaptations=logs, generator_sha256=sha(Path(__file__).read_bytes()),
        adaptations=['Original playback/options/typewriter/reward controller branches and coroutine order retained.',
            'Saved stable dialogue view gains original player identity and node history fields.',
            'Configuration, actor icon, reward icon pool, NPC name and local acknowledgment use explicit ports.',
            'Original RPC completion is a local completed yield operation; notifications arrive only after disk commit.',
            'Native prefab bindings and concrete host presentation are separate work; no generated UI layout.'])
    (ROOT / 'assets/reference/partner-dialogue-playback-ui-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared selected original dialogue playback controllers and dependencies', logs, flush=True)


if __name__ == '__main__': main()
