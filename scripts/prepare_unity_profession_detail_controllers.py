"""Copy original detail, rank-badge and tutor controllers with explicit services."""
import hashlib
import json
import re
from pathlib import Path

from prepare_unity_profession_character_resources import meta, record

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def build():
    receipt = CODE.parent / 'code-manifest.json'
    checked = json.loads(receipt.read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if checked['manifest'] != '871_156598' or record(assembly)['sha256'] != checked['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Original profession controller assembly differs')
    ports = []
    for name in ('ProfessionPanel', 'ProfessionSkillBadgeRewarder', 'ProfessionChangeJumpBtn'):
        src = CODE / 'client-source' / (name + '.cs')
        text = src.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?|R3|UnityEngine\.UI\.Ext);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_.*\n', '', text, flags=re.M)
        text = re.sub(r'^\[UIWindow\([^\n]+\n', '', text, flags=re.M)
        text = text.replace('using UnityEngine.Events;\n', '')
        text = re.sub(r'\bProfession\b', 'int', text)
        text = text.replace('int.None', '0')
        text = text.replace('ProfessionBaseInfo', 'ProfessionDetailDefinition')
        text = text.replace('PlayerItemDataWrap', 'IProfessionDetailSkill')
        text = text.replace('private UIBase mContainer;', 'private UIBase mContainer;')
        text = re.sub(r'\bDisposableBag\b', 'ProfessionResourceOperationBag', text)
        text = text.replace('mDisposableBag = default;', 'mDisposableBag = new ProfessionResourceOperationBag();')
        text = text.replace('MeManager.MainPlayer', 'Bindings.MainPlayer')
        text = text.replace('GameObjectPool<PlayerPropItem>', 'GameObjectPool<ProfessionPlayerPropItem>')
        text = text.replace('MultiLanguageText', 'WorldMultiLanguageText')
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('TabGroup', 'WorldTabGroup').replace('TabItem', 'WorldTabItem')
        # Keep the original method names, which contain the source type name.
        text = text.replace('OnWorldTabItemChanged', 'OnTabItemChanged').replace('ResetWorldTabItem', 'ResetTabItem')
        text = text.replace('((UnityEvent<WorldTabItem, bool>)ToggleGroup.OnTabItemChanged).AddListener((UnityAction<WorldTabItem, bool>)OnSwitch);', 'ToggleGroup.OnTabItemChanged.AddListener(OnSwitch);')
        text = text.replace('((UnityEvent)mJumpBtn.onClick).AddListener((UnityAction)Jump);', 'mJumpBtn.onClick.AddListener(Jump);')
        text = text.replace('ResourceLoader.LoadSprite(', 'Bindings.ResourceLoader.LoadAsset<Sprite>(')
        text = text.replace('ResourceLoader.LoadAssetByGuid<Sprite>(', 'Bindings.ResourceLoader.LoadAssetByGuid<Sprite>(')
        text = re.sub(r'Disposable.AddTo<AssetOperationHandle<Sprite>>\((.*?), ref mDisposableBag\)', r'mDisposableBag.Add(\1)', text)
        text = re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n', '\n', text)
        header = '''
    private IProfessionDetailBindings mBindings;
    private IProfessionDetailBindings Bindings => mBindings ?? ProfessionDetailBindings.Required;
    public void Configure(IProfessionDetailBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }
'''
        at = text.index('\n{', text.index('public class ')) + 2
        text = text[:at] + header + text[at:]
        if name == 'ProfessionPanel':
            text = text.replace('public ProfessionUpgradePanel UpgradePanel;', 'public Component UpgradePanel;').replace('public ProfessionInfoPanel InfoPanel;', 'public Component InfoPanel;')
            text = text.replace('SkillBadgeRewarder.Init(mCurProfession, this);', 'SkillBadgeRewarder.Configure(Bindings);\n\t\tSkillBadgeRewarder.Init(mCurProfession, this);')
            text = text.replace('mJumpBtn.SetProfession(mCurProfession);', 'mJumpBtn.Configure(Bindings);\n\t\tmJumpBtn.SetProfession(mCurProfession);')
            text = text.replace('\t\tProfessionDetailDefinitionParser professionBaseInfoParser = Parsers.Instance.ProfessionDetailDefinitionParser;\n', '')
            text = text.replace('professionBaseInfoParser.GetSecondIconPath(pro)', 'Bindings.GetSecondIconPath(pro)')
            text = text.replace('ProfessionSettings.Value.GetFullBodyImg(pro, Sex.Male).AssetGuid', 'Bindings.GetFullBodyGuid(pro, Sex.Male)').replace('ProfessionSettings.Value.GetFullBodyImg(pro, Sex.Female).AssetGuid', 'Bindings.GetFullBodyGuid(pro, Sex.Female)')
            text = text.replace('LanguageUtils.GetProfessionNameKey(pro)', 'Bindings.GetProfessionNameKey(pro)')
            text = text.replace('ProfessionSettings.Value.GetSetting(pro).ProfessionPanelFadeColor', 'Bindings.GetPanelFadeColor(pro)').replace('ProfessionSettings.Value.GetSetting(pro).ProfessionPanelLineColor', 'Bindings.GetPanelLineColor(pro)')
            text = text.replace('new ProfessionNodeInstanceInfo(mCurProfession)', 'new ProfessionNodeInstanceInfo(mCurProfession)')
            text = text.replace('InfoPanel.Init(mCurProfession, mPool);', '((IProfessionDetailInfoPanel)InfoPanel).Init(mCurProfession, mPool);').replace('UpgradePanel.Init(mCurProfession);', '((IProfessionDetailUpgradePanel)UpgradePanel).Init(mCurProfession);')
            text = text.replace('\t\tProfessionLevelCostInfo info;\n\t\treturn !Parsers.Instance.ProfessionLevelCostInfoParser.TryGetLevelInfo(mCurProfession, professionLevel + 1, out info);', '\t\treturn !Bindings.HasLevelCost(mCurProfession, professionLevel + 1);')
        elif name == 'ProfessionSkillBadgeRewarder':
            a = text.index('\t\tProfessionDetailDefinitionParser professionBaseInfoParser')
            b = text.index('\n\t\tint amount = 0;', a)
            text = text[:a] + '''        IProfessionDetailPlayer player = Bindings.MainPlayer;
        IReadOnlyCollection<IProfessionDetailSkill> readOnlyCollection = player.FindSkillItems();
        ProfessionDetailDefinition professionBaseInfo = Bindings.GetProfession(mProfession);''' + text[b:]
            text = text.replace('skillRankInfoParser.Get(rank).Badge', 'Bindings.GetRankBadge(rank)').replace('skillRankInfoParser.Get(lastRewardedSkillRank).Badge', 'Bindings.GetRankBadge(lastRewardedSkillRank)')
            a = text.index('\t\tServiceProxyBase<PlayerServiceProxyImpl>')
            b = text.index('\n\t}\n', a)
            text = text[:a] + '''        Bindings.ClaimSkillRankReward(mSkillIds, ret =>
        {
            Bindings.PopupSkillBadge(mContainer, mItemWrapInfos, badgeId, amount);
        });''' + text[b:]
        else:
            text = text.replace('Parsers.Instance.ProfessionDetailDefinitionParser.HighestRank', 'Bindings.HighestRank')
            text = text.replace('\t\tProfessionDetailDefinitionParser professionBaseInfoParser = Parsers.Instance.ProfessionDetailDefinitionParser;\n', '')
            text = text.replace('professionBaseInfoParser.GetSuccessor(', 'Bindings.GetSuccessor(').replace('professionBaseInfoParser.Get(', 'Bindings.GetProfession(')
            text = text.replace('ProfessionUtils.IsProfessionTutorUnlock(', 'Bindings.IsProfessionTutorUnlock(')
            text = text.replace('ProfessionSettings.Value.GetTutorHeadImg(professionBaseInfo.int).AssetGuid', 'Bindings.GetTutorHeadGuid(professionBaseInfo.Profession)')
            text = text.replace('Parsers.Instance.ProfessionDetailDefinitionParser.Get(', 'Bindings.GetProfession(')
            text = text.replace('TaskJumpableUtils.TryGotoMapAreaByUniqueEntitySource(MapRootManagerProxy, MapMyCharacterManager, DirectionGuiderDataHandler, MapInput, professionBaseInfo.TutorEntityTag)', 'Bindings.TryGotoTutor(professionBaseInfo.TutorEntityTag)')
            text = text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("ui_profession_tutor_not_found"));', 'Bindings.ShowSystemMessage("ui_profession_tutor_not_found");')
        # Regex type migration must not change the original record property name.
        text = text.replace('.int', '.Profession')
        if any(t in text for t in ('[Inject]', 'Parsers.Instance', 'ServiceProxyBase', 'MeManager', 'Disposable.AddTo', 'UnityAction', 'ProfessionSettings.Value')):
            raise ValueError('Unbound original controller dependency: ' + name + ': ' + '\n'.join(line for line in text.splitlines() if any(t in line for t in ('[Inject]', 'Parsers.Instance', 'ServiceProxyBase', 'MeManager', 'Disposable.AddTo', 'UnityAction', 'ProfessionSettings.Value'))))
        text = 'namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.WorldUI;\n' + text + '\n}\n'
        path = DEST / (name + '.cs')
        path.write_text(text, encoding='utf-8', newline='\n'); meta(path)
        ports.append(dict(name=name, source=record(src), output=record(path), meta=record(path.with_name(path.name+'.meta'))))
    bindings = DEST / 'ProfessionDetailBindings.cs'; meta(bindings)
    proof = dict(format=1, manifest=checked['manifest'], code_manifest=record(receipt), application_assembly=record(assembly),
        ports=ports, bindings=[record(bindings), record(bindings.with_name(bindings.name+'.meta'))], generator=record(Path(__file__)),
        operation='Copy all original detail-window, skill-rank badge rewarder and tutor-jump controller methods. Keep UIBase initialization, original state/tab/reset order, resource request lifetimes, rank difference and sort, success-only popup, successor-first/backtracking tutor traversal and failure message. Only original service/parser accesses and child-panel calls use explicit injected contracts; enums consumed as profession keys use their original integers.',
        validated_scope='Pending actual Unity Play Mode controller and simulated input validation.',
        limitations=['Info/upgrade child panels, rank reward dialog, actual detail prefab/resource/window/player/tutor service integration remain unbound.', 'Complete native animated profession pages, save/input and Windows release remain incomplete.'])
    path = ROOT / 'assets/reference/profession-ui/detail-controllers-source-manifest.json'
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print('Ported', len(ports), 'complete original profession detail controllers.')


if __name__ == '__main__':
    build()
