"""Copy original profession upgrade panel, red-dot and effect lifetimes."""
import json
import re
from pathlib import Path

from prepare_unity_profession_character_resources import meta, record

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def replace(text, old, new):
    if old not in text:
        raise ValueError('Inspect original upgrade dependency: ' + old)
    return text.replace(old, new)


def build():
    receipt = CODE.parent / 'code-manifest.json'
    checked = json.loads(receipt.read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if checked['manifest'] != '871_156598' or record(assembly)['sha256'] != checked['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Original profession upgrade assembly differs')
    ports = []
    for name in ('PlayerUIBase', 'RedDotItemBase', 'ProfessionUpgradeRedDotItem',
                 'APropEffectPlayAgent', 'PropUpgradeEffectAgent', 'ProfessionUpgradePanel', 'PlayerUseItemContent'):
        src = CODE / 'client-source' / (name + '.cs')
        text = src.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_.*\n', '', text, flags=re.M)
        text = re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n', '\n', text)
        text = re.sub(r'\bProfession\b|\bPropType\b', 'int', text)
        text = text.replace('int.None', '0')
        text = text.replace('.int', '.Profession')
        text = text.replace('ProfessionBaseInfo', 'ProfessionDetailDefinition')
        text = text.replace('ProfessionLevelCostInfo', 'ProfessionUpgradeCostDefinition')
        text = text.replace('PlayerItemDataWrap', 'IProfessionItemData').replace('AwardSourceData', 'object')
        text = re.sub(r'\bPlayer\b', 'IProfessionUpgradePlayer', text)
        text = text.replace('PlayerPropItem', 'ProfessionPlayerPropItem')
        text = re.sub(r'\bPlayerUIBase\b', 'ProfessionPlayerUIBase', text)
        text = re.sub(r'\bRedDotItemBase\b', 'ProfessionRedDotItemBase', text)
        text = text.replace('MultiLanguageText', 'WorldMultiLanguageText')
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('using UnityEngine.Events;\n', '')
        text = text.replace('((UnityEvent)BtnUpgrade.onClick).AddListener((UnityAction)OnUpgrade);', 'BtnUpgrade.onClick.AddListener(OnUpgrade);')
        text = text.replace('MeManager.MainPlayer', 'Bindings.MainPlayer')
        if name in ('PlayerUIBase', 'ProfessionUpgradePanel', 'PlayerUseItemContent'):
            header = '''
    private IProfessionUpgradeBindings mBindings;
    protected IProfessionUpgradeBindings Bindings => mBindings ?? ProfessionUpgradeBindings.Required;
    public void Configure(IProfessionUpgradeBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }
'''
            at = text.index('\n{', text.index('public class ')) + 2
            text = text[:at] + header + text[at:]
        if name == 'PlayerUIBase':
            text = text.replace('MeManager.Instance != null', 'Bindings.ManagerAvailable')
            text = text.replace('MeManager.Instance.OnPlayerUpdate', 'Bindings.OnPlayerUpdate')
        elif name == 'RedDotItemBase':
            text = replace(text, 'TipDotSystem tipDotSystem = TipDotSystem;', 'IProfessionTipDotSystem tipDotSystem = Bindings.TipDotSystem;')
            text = text.replace('TipDotSystem.IsShowTipDot', 'Bindings.TipDotSystem.IsShowTipDot')
        elif name == 'ProfessionUpgradeRedDotItem':
            text = text.replace('.IsFunctionLimit(FunctionType.Profession)', '.IsProfessionFunctionLimit')
            text = replace(text, 'itemData.Info.ItemType', 'Bindings.GetItemType(itemData.Info)')
            text = text.replace('ItemType.Badge', '316').replace('ItemType.ActiveSkill', '100').replace('ItemType.PassiveSkill', '101')
            text = replace(text, '\t\tProfessionUpgradeCostDefinitionParser professionLevelCostInfoParser = Parsers.Instance.ProfessionUpgradeCostDefinitionParser;\n', '')
            text = text.replace('Parsers.Instance.ProfessionDetailDefinitionParser.Get(pro)', 'Bindings.GetProfession(pro)')
            text = text.replace('professionLevelCostInfoParser.TryGetLevelInfo(', 'Bindings.TryGetLevelInfo(')
            text = text.replace('ProfessionUtils.CalcAwardBadge(', 'Bindings.CalcAwardBadge(')
        elif name == 'ProfessionUpgradePanel':
            text = replace(text, 'MonoBehaviour\n', 'MonoBehaviour, IProfessionDetailUpgradePanel\n')
            text = replace(text, '\t\tProfessionDetailDefinitionParser professionBaseInfoParser = Parsers.Instance.ProfessionDetailDefinitionParser;\n', '')
            text = text.replace('professionBaseInfoParser.Get(mPro)', 'Bindings.GetProfession(mPro)')
            text = text.replace('ProfessionRedDot.SetProfessions(pro);', 'ProfessionRedDot.Configure(Bindings);\n\t\tProfessionRedDot.SetProfessions(pro);')
            text = text.replace('Parsers.Instance.ProfessionUpgradeCostDefinitionParser.TryGetLevelInfo(', 'Bindings.TryGetLevelInfo(')
            text = text.replace('mInfo.LevelPropId', 'Bindings.GetLevelPropId(mPro)')
            text = replace(text, '\t\tLevelPropParser levelPropParser = Parsers.Instance.LevelPropParser;\n', '')
            text = text.replace('levelPropParser.GetProps(', 'Bindings.GetProps(')
            text = replace(text, 'newPropItem.Init(item.Key, item.Value, -1L);', 'Bindings.InitPropItem(newPropItem, item.Key, item.Value, -1L);')
            text = text.replace('Formula.Instance.GetProfessionLevelPropDiff(', 'Bindings.GetProfessionLevelPropDiff(')
            text = replace(text, 'LanguageManager.Instance.GetText($"{typeof(int).Name}.{item.Key}")', 'Bindings.GetPropNameText(item.Key)')
            text = replace(text, 'new LanguageUtils.PropValue(item.Key, item.Value, includeUnit: false, includePropDesc: false, -1L)', 'Bindings.FormatPropValue(item.Key, item.Value)')
            text = text.replace('ItemIconManager.GetIcon(', 'Bindings.GetIcon(').replace('ItemIconManager.RecycleIcon(', 'Bindings.RecycleIcon(')
            text = text.replace('ItemIconManager == null', '(mBindings ?? ProfessionUpgradeBindings.Current) == null')
            text = replace(text, 'ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerProfessionUpgrade(mPro).OnSuccess += (PlayerProfessionUpgradeResponse rep) =>', 'Bindings.Upgrade(mPro, () =>')
            text = replace(text, '\t\t\tUpAnimation.Play();\n\t\t};', '\t\t\tUpAnimation.Play();\n\t\t});')
        elif name == 'PlayerUseItemContent':
            text = replace(text, '((UnityEvent)UseItemBtn.onClick).AddListener((UnityAction)OnClickUseItemButton);', 'UseItemBtn.onClick.AddListener(OnClickUseItemButton);')
            text = text.replace('ItemIconManager.GetIcon(', 'Bindings.GetIcon(').replace('ItemIconManager.RecycleIcon(', 'Bindings.RecycleIcon(')
            text = text.replace('Parsers.GameSettings.MoneyId', 'Bindings.MoneyId')
            text = text.replace('MoneyUtils.ShowLackOfItem(', 'Bindings.ShowLackOfItem(')
            text = replace(text, '\t\tItemMergeInfoParser itemMergeInfoParser = Parsers.Instance.ItemMergeInfoParser;\n', '')
            text = text.replace('ItemSourceDataManager.IsItemSourceFromMerge(', 'Bindings.IsItemSourceFromMerge(')
            text = text.replace('itemMergeInfoParser.CalcCanMergeAmout(', 'Bindings.CalcCanMergeAmount(')
            text = text.replace('LanguageUtils.TextKeyArgWrap', 'ProfessionItemText')
            text = text.replace('.Deconstruct(ref num, ref num2)', '.Deconstruct(out num, out num2)')
        forbidden = ('[Inject]', 'Parsers.Instance', 'ServiceProxyBase', 'MeManager', 'LanguageManager.Instance', 'ItemIconManager', 'UnityAction', 'Formula.Instance', 'FunctionType.')
        if any(t in text for t in forbidden):
            raise ValueError(name + ': unbound original calls: ' + '\n'.join(l for l in text.splitlines() if any(t in l for t in forbidden)))
        text = 'namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.WorldUI;\n' + text + '\n}\n'
        target_name = {'PlayerUIBase':'ProfessionPlayerUIBase', 'RedDotItemBase':'ProfessionRedDotItemBase'}.get(name,name)
        path = DEST / (target_name + '.cs')
        path.write_text(text, encoding='utf-8', newline='\n'); meta(path)
        ports.append(dict(name=name, source=record(src), output=record(path), meta=record(path.with_name(path.name+'.meta'))))
    bindings = DEST / 'ProfessionUpgradeBindings.cs'; meta(bindings)
    extension = DEST / 'ProfessionUseItemTransformExtensions.cs'; meta(extension)
    proof = dict(format=1, manifest=checked['manifest'], code_manifest=record(receipt), application_assembly=record(assembly),
        ports=ports, bindings=[record(bindings), record(bindings.with_name(bindings.name+'.meta'))], generator=record(Path(__file__)),
        transform_extensions=dict(source=record(CODE/'framework-source/Framework/TransformExtensions.cs'), output=record(extension), meta=record(extension.with_name(extension.name+'.meta')), scope='Original DestroyChildren and DestroyActivedChildren only.'),
        consumed_enum=record(CODE/'common-source/Common/ItemType.cs'),
        operation='Copy all methods of seven original upgrade/player-UI/red-dot/effect/use-item components. Preserve update order, next-level cost lookup, pending badge calculation branch, five player subscriptions, dirty red-dot update, property pool/background/effect order, success-only animation, use-item lifecycle, money-only shortage and merge-tip conditions. Adapt application parser/player/formula/typed-property/transaction calls to explicit consumed services; reuse original UIBase, ItemIcon, GameObjectPool and existing property presentation port.',
        validated_scope='Pending actual Unity import and Play Mode validation.',
        limitations=['Production player/formula/transaction/property-format services and full original upgrade prefab bindings remain incomplete.', 'Existing property-row port is still scoped to string presentation; full typed/colorful property and blessing popup paths remain required.', 'No complete native profession page, save verification or Windows release is claimed.'])
    (ROOT/'assets/reference/profession-ui/upgrade-panel-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported',len(ports),'original upgrade, red-dot and effect components.')


if __name__ == '__main__':
    build()
