"""Port the selected original NPC profile region and chapter tabs."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_gift_configuration import method, without_logging
from validation_workspace import validation_directory


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review the selected profile source version')
    work = validation_directory()
    folder = work / 'profile-code'
    folder.mkdir(exist_ok=True)
    inputs, outputs, methods = {}, {}, {}
    destination = Path('unity/Assets/Game/Runtime/Official/PartnersUI/Roster/Profile')

    def sha(raw): return hashlib.sha256(raw).hexdigest()

    def source(relative):
        path = ROOT / '.tools/source-packages/current' / version / 'official-code' / relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def replace(text, old, new):
        if old not in text: raise ValueError('Selected profile expression changed: ' + old)
        return text.replace(old, new)

    def write(name, text, target=destination):
        path = folder / name
        path.write_text('\n'.join(line.expandtabs(4).rstrip() for line in text.split('\n')), 'utf8', newline='\n')
        Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: '
            + hashlib.md5(('zjcs-partner-profile:' + name).encode()).hexdigest()
            + '\nMonoImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for entry in (path, Path(str(path) + '.meta')):
            outputs[(target / entry.name).as_posix()] = sha(entry.read_bytes())

    text = source('client-source/NpcDetailPanelProfileContent.cs')
    signatures = [
        'private void Awake()', 'protected override void OnEnableAndInited()',
        'protected override void OnDisableAndInited()', 'public void Init(PlayerNpcDataWrap npcDataWrap)',
        'private void OnTabItemChanged(TabItem tabItem, bool value)', 'private void InitContent()',
        'private void Refresh()', 'private void RecycleItemIcon()',
        'private ProfileStatus GetProfileStatus(NpcProfileInfo info)', 'private void OnUnlockBtn()']
    methods['NpcDetailPanelProfileContent'] = {signature: sha(method(text, signature).encode()) for signature in signatures}
    # The decompiler's compiler-generated state machine is restored as the
    # equivalent awaited DelayFrame(1, PostLateUpdate), followed by ScrollToItem.
    start = text.index('\t[StructLayout(LayoutKind.Auto)]')
    end = text.index('\n\t[SerializeField]', start)
    text = text[:start] + text[end:]
    start = text.index('\t[AsyncStateMachine(')
    end = text.index('\n\tprivate void Refresh()', start)
    text = text[:start] + '''    private async UniTask SafeScrollToItem(RectTransform rectTransform)
    {
        await UniTask.DelayFrame(1, (PlayerLoopTiming)8, default(CancellationToken), false);
        ScrollRectUtil.ScrollToItem(mTabScrollRect, rectTransform);
    }
''' + text[end:]
    start = text.index('\t[Inject]')
    end = text.index('\n\tprivate void Awake()', start)
    text = text[:start] + '''    private IPartnerProfileUiServices services;
    public void Configure(IPartnerProfileUiServices value)
    {
        services = value ?? throw new ArgumentNullException(nameof(value));
        foreach (var item in mTabItems) item.Configure(value);
        foreach (var item in mShortTabItems) item.Configure(value);
    }
''' + text[end:]
    text = replace(text, 'Parsers.Instance.NpcProfileInfoParser.GetNpcProfileInfos(npcDataWrap.NpcClassId)', 'services.GetNpcProfiles(npcDataWrap.NpcClassId)')
    text = replace(text, 'mProfileInfo.UnlockCondition.GetUnLockDesc(MeManager.MainPlayer)', 'services.GetUnlockDescription(mProfileInfo)')
    text = replace(text, 'info.UnlockCondition.IsMet(MeManager.MainPlayer)', 'services.IsConditionMet(info)')
    text = replace(text, 'ItemIconManager', 'services')
    start = text.index('\t\tPlayerReceiveNpcProfileRewardRequest req = ', text.index('private void OnUnlockBtn()'))
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''        services.ReceiveReward(mProfileInfo.Id, mProfileInfo.ProfileId, () =>
        {
            Refresh();
        });''' + text[end:]
    text = replace(text, 'StateSwitcher<', 'WorldStateSwitcher<')
    text = text.replace('using UnityEngine.UI.Ext;\n', '')
    for pattern in (r'^using (?:Common|Framework\.DependencyInjection);\n', r'^using Cysharp\.Threading\.Tasks\.CompilerServices;\n',
                    r'^using System\.(?:Diagnostics|Runtime\.CompilerServices|Runtime\.InteropServices);\n', r'^\s*//IL_[^\n]*\n'):
        text = re.sub(pattern, '', text, flags=re.M)
    if any(word in text for word in ('Parsers.', 'MeManager.', 'ServiceProxyBase', '[Inject]', '_003C')):
        raise ValueError('An original profile dependency remains unbound')
    aliases = '''using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using Object = UnityEngine.Object;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerProfileNpcData;
using NpcProfileInfo = Zjcs.Official.Partners.PartnerProfileRules.ProfileInfo;
'''
    write('NpcDetailPanelProfileContent.cs', '// Selected original NPC profile controller; see partner-profile-ui-source-manifest.json.\nnamespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n')
    tab = source('client-source/NpcDetailPanelProfileContentTabItem.cs')
    tab = tab.replace('using Common;\n', '')
    tab = tab.replace('using UnityEngine.UI.Ext;\n', '')
    tab = replace(tab, 'public NpcProfileInfo Info { get; private set; }', '''private IPartnerProfileUiServices services;
    public void Configure(IPartnerProfileUiServices value) { services = value; services.ConfigureTip(mTipDotNodeLinker); }
    public NpcProfileInfo Info { get; private set; }''')
    write('NpcDetailPanelProfileContentTabItem.cs', '// Selected original NPC chapter tab.\nnamespace Zjcs.Official.PartnersUI\n{\n' + aliases + tab + '\n}\n')
    template = ROOT / 'scripts/templates/partner_profile/PartnerProfileUiServices.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    write(template.name, template.read_text('utf8'))
    scroll = source('client-source/ScrollRectUtil.cs')
    scroll = re.sub(r'^\s*//IL_[^\n]*\n', '', scroll, flags=re.M)
    scroll = replace(scroll, 'Vector2.op_Implicit(((Transform)content).InverseTransformPoint(((Transform)item).position))',
        '(Vector2)((Transform)content).InverseTransformPoint(((Transform)item).position)')
    write('ScrollRectUtil.cs', '// Selected original game-owned scrolling helper.\nnamespace Zjcs.Official.PartnersUI\n{\n' + scroll + '\n}\n')
    template = ROOT / 'scripts/templates/partner_profile/PartnerProfileRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    core = template.read_text('utf8')
    original = source('common-source/Common/NpcFriendshipLevelCondition.cs')
    bodies = []
    for signature, logging in [('public override bool IsMet(DataHub dataHub)', 0),
            ('public override (long current, long target) GetProgress(DataHub dataHub)', 0),
            ('private long GetValue(DataHub dataHub)', 1)]:
        body = method(original, signature)
        methods.setdefault('NpcFriendshipLevelCondition', {})[signature] = sha(body.encode())
        bodies.append((without_logging(body, logging) if logging else body).replace('public override ', 'public '))
    core = core.replace('/*CONDITION_METHODS*/', '\n'.join(bodies))
    body = method(original, 'public override string GetUnLockDesc(DataHub dataHub)')
    methods['NpcFriendshipLevelCondition']['GetUnLockDesc'] = sha(body.encode())
    body = body.replace('public override string GetUnLockDesc(DataHub dataHub)',
        'public string GetUnlockDescription(Func<string, bool> contains, Func<string, object, string> format)')
    body = body.replace('GetUnLockDescKey()', '"conditon_unlock_desc_NpcFriendshipLevel"').replace('LanguageManager.Instance.ContainsKey(', 'contains(').replace('LanguageManager.Instance.GetTextFormated(', 'format(')
    core = core.replace('/*DESCRIPTION*/', body)
    original = source('common-source/Common/NpcProfileInfoParser.cs')
    bodies = []
    for signature, logging in [('protected override void PostParse(Dictionary<int, Dictionary<int, NpcProfileInfo>> configs)', 0),
            ('public List<NpcProfileInfo> GetNpcProfileInfos(int npcClassId)', 1)]:
        body = method(original, signature)
        methods.setdefault('NpcProfileInfoParser', {})[signature] = sha(body.encode())
        body = (without_logging(body, logging) if logging else body).replace('protected override ', 'private ')
        body = body.replace('\tbase.PostParse(configs);\n', '').replace('NpcProfileInfo', 'ProfileInfo')
        # Keep the public parser API name and original grouping/order.
        body = body.replace('GetProfileInfos(', 'GetNpcProfileInfos(').replace('mProfileInfoDict', 'mNpcProfileInfoDict')
        bodies.append(body)
    core = core.replace('/*PARSER_METHODS*/', '\n'.join(bodies))
    source('common-source/Common/NpcProfileInfo.cs')
    source('common-source/Common/ICondition.cs')
    write('PartnerProfileRules.cs', core, Path('unity/Assets/Game/Runtime/Official/Partners'))
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=inputs,
        outputs=outputs, original_methods_sha256=methods, adaptations=[
            'Restore the compiler-generated one-frame asynchronous scroll as an awaited UniTask with the original timing.',
            'Bind the original parser, player condition, icon manager and request/response through explicit local services.',
            'Preserve short/long tabs, first-claimable selection, lock/content states, first configured reward and icon lifecycle.'])
    (work / 'partner-profile-ui-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original profile controller and tabs:', folder)


if __name__ == '__main__': main()
