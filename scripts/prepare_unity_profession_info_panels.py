"""Migrate original information panel and complete skill-icon presentation."""
import json
import re
from pathlib import Path

from prepare_unity_profession_character_resources import meta, record

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def replace(text, old, new):
    if old not in text:
        raise ValueError('Inspect original information dependency: ' + old)
    return text.replace(old, new)


def build():
    receipt = CODE.parent / 'code-manifest.json'
    checked = json.loads(receipt.read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if checked['manifest'] != '871_156598' or record(assembly)['sha256'] != checked['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Original profession information assembly differs')
    ports = []
    names = ('ProfessionInfoPanel', 'ProfessionEquipTypeIcon', 'ProfessionInfoPanelSkillItem',
             'ProfessionSkillSlot', 'ItemIconSkill', 'SkillStarInfo', 'StarMiniInfo', 'BlessLevelTextViewChanger')
    for name in names:
        src = CODE / 'client-source' / (name + '.cs')
        text = src.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Common|GMFunction|Framework(?:\.DependencyInjection)?|R3);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_.*\n', '', text, flags=re.M)
        text = re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n', '\n', text)
        text = re.sub(r'\bProfession\b|\bItemType\b|\bActorTargetType\b', 'int', text)
        text = text.replace('.int', '.ItemType')
        text = text.replace('ItemInfo', 'IProfessionItemInfo').replace('PlayerItemDataWrap', 'IProfessionItemData')
        text = text.replace('ProfessionBaseInfo', 'ProfessionDetailDefinition')
        text = text.replace('MultiLanguageText', 'WorldMultiLanguageText').replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('GameObjectPool<PlayerPropItem>', 'GameObjectPool<ProfessionPlayerPropItem>')
        text = re.sub(r'TryGetComponent<([^>]+)>\(ref ', r'TryGetComponent<\1>(out ', text)
        text = re.sub(r'\bDisposableBag\b', 'ProfessionResourceOperationBag', text)
        text = text.replace('mDisposableBag = default;', 'mDisposableBag = new ProfessionResourceOperationBag();')
        info = name in ('ProfessionInfoPanel', 'ProfessionEquipTypeIcon')
        binding = 'IProfessionInfoBindings' if info else 'IProfessionSkillIconBindings'
        holder = 'ProfessionInfoBindings' if info else 'ProfessionSkillIconBindings'
        if name != 'BlessLevelTextViewChanger':
            header = f'''
    private {binding} mBindings;
    private {binding} Bindings => mBindings ?? {holder}.Required;
    public void Configure({binding} bindings)
    {{ mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }}
'''
            # StarMiniInfo consumes only its serialized quality dictionary.
            if name != 'StarMiniInfo':
                at = text.index('\n{', text.index('public class ')) + 2
                text = text[:at] + header + text[at:]
        if name == 'ProfessionInfoPanel':
            text = replace(text, 'MonoBehaviour, LoopScrollDataSource', 'MonoBehaviour, LoopScrollDataSource, IProfessionDetailInfoPanel')
            text = text.replace('List<IProfessionItemInfo>', 'List<IProfessionInfoSkill>')
            text = replace(text, 'Parsers.Instance.ProfessionDetailDefinitionParser.Get(pro)', 'Bindings.GetProfession(pro)')
            text = replace(text, 'LanguageUtils.GetProfessionDescKey(mPro)', 'Bindings.GetProfessionDescKey(mPro)')
            text = replace(text, 'Radar.ShowProp(mPro);', 'Radar.ShowProp(Bindings.GetFeatureProps(mPro), Bindings.GetRadarColor(mPro), Bindings.InitPropItem);')
            text = replace(text, '\t\tEquipTypeInfoParser equipTypeInfoParser = Parsers.Instance.EquipTypeInfoParser;\n', '')
            text = replace(text, 'equipTypeInfoParser.GetSupportEquipTypeByProfession(EquipPos.Weapon, mPro)', 'Bindings.GetSupportWeaponTypes(mPro)')
            text = replace(text, 'equipTypeInfoParser.GetSupportEquipTypeByProfession(EquipPos.SecondWeapon, mPro)', 'Bindings.GetSupportSecondWeaponTypes(mPro)')
            text = replace(text, '\t\tIProfessionItemInfoParser itemInfoParser = Parsers.Instance.IProfessionItemInfoParser;\n', '')
            text = replace(text, 'IProfessionItemInfo itemInfo = itemInfoParser.Get(key)', 'IProfessionInfoSkill itemInfo = Bindings.GetItemInfo(key)')
            text = text.replace('int.ActiveSkill', '100').replace('int.PassiveSkill', '101')
            text = replace(text, 'SkillItemComparer sDefault = SkillItemComparer.sDefault;', 'ProfessionInfoSkillComparer sDefault = ProfessionInfoSkillComparer.sDefault;')
            text = text.replace('IProfessionItemInfo skillInfo = mAllSkills[idx]', 'IProfessionInfoSkill skillInfo = mAllSkills[idx]')
        elif name == 'ProfessionEquipTypeIcon':
            text = replace(text, '\t\tEquipTypeInfo equipTypeInfo = Parsers.Instance.EquipTypeInfoParser.Get(itemType);\n', '')
            text = replace(text, 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(equipTypeInfo.Icon), ref mDisposableBag)', 'mDisposableBag.Add(Bindings.ResourceLoader.LoadAsset<Sprite>(Bindings.GetEquipTypeIcon(itemType)))')
            text = replace(text, 'LanguageUtils.GetEnumKey(itemType)', 'Bindings.GetEquipTypeNameKey(itemType)')
        elif name == 'ProfessionInfoPanelSkillItem':
            text = replace(text, 'Player mainPlayer = MeManager.MainPlayer;', 'IProfessionSkillIconPlayer mainPlayer = Bindings.MainPlayer;')
        elif name == 'ProfessionSkillSlot':
            text = text.replace('ItemIconManager.GetIcon<ItemIconSkill>', 'Bindings.GetIcon').replace('ItemIconManager.RecycleIcon', 'Bindings.RecycleIcon').replace('ItemIconManager != null', '(mBindings ?? ProfessionSkillIconBindings.Current) != null')
        elif name == 'ItemIconSkill':
            text = text.replace('MeManager.MainPlayer', 'Bindings.MainPlayer')
            text = text.replace('DebugShowSkillRank.OnChanged', 'Bindings.DebugShowSkillRankChanged').replace('DebugShowSkillRank.IsEnabled', 'Bindings.DebugShowSkillRank')
            text = replace(text, 'LanguageUtils.TextKeyArgWrap text = new LanguageUtils.TextKeyArgWrap("ui_common_level_style_3", level);', 'ProfessionItemText text = new ProfessionItemText { Key = "ui_common_level_style_3", Args = new object[] { level } };')
            text = text.replace('PlayerItemParamSkillWrap', 'IProfessionSkillParameter')
            text = text.replace('myItemData?.GetItemParamSkill()', 'myItemData == null ? null : Bindings.GetItemParamSkill(myItemData)')
            text = text.replace('myItemData.GetItemParamSkill()', 'Bindings.GetItemParamSkill(myItemData)').replace('itemData.GetItemParamSkill()', 'Bindings.GetItemParamSkill(itemData)')
            text = text.replace('Parsers.Instance.SkillRankInfoParser.Get(rank)', 'Bindings.GetRank(rank)')
            text = replace(text, 'SkillInfo skillInfo = Parsers.Instance.SkillInfoParser.Get(myInfo.ClassId);\n\t\tif (skillInfo == null || skillInfo.ProfessionRank <= 0)', 'int professionRank = Bindings.GetDebugProfessionRank(myInfo.ClassId);\n\t\tif (professionRank <= 0)')
            text = text.replace('skillInfo.ProfessionRank.ToString()', 'professionRank.ToString()')
            text = replace(text, '\t\tProfessionDetailDefinitionParser professionBaseInfoParser = Parsers.Instance.ProfessionDetailDefinitionParser;\n', '')
            text = replace(text, 'targetType != int.Player || professionBaseInfoParser.IsSkillAvailableForProfession(myInfo.ClassId, profession)', '!Bindings.IsPlayerTarget(targetType) || Bindings.IsSkillAvailableForProfession(myInfo.ClassId, profession)')
            text = replace(text, 'PetUtils.IsPetSkill(myInfo.ItemType)', 'Bindings.IsPetSkill(myInfo)')
            text = replace(text, 'ServerInfoManager.Instance.CheckFunctionDisabled(Bindings.MainPlayer, ServerFunctionType.UseSkillItem, new CommonFunctionsDisabledKey(classId))', 'Bindings.IsSkillGMDisabled(classId)')
            text = replace(text, 'SkillDetailDialog.IPropDataProvider', 'object')
            a = text.index('\t\t\tWindowManager.Instance.Popup(')
            b = text.index('\n\t\t});', a)
            text = text[:a] + '''            if (itemIcon.myItemData != null)
                Bindings.PopupSkillDetail(itemIcon.myItemData, propDataProvider);
            else if (itemIcon.myInfo != null)
                Bindings.PopupSkillDetail(itemIcon.myInfo.ClassId, 0L);''' + text[b:]
            text = replace(text, 'Bindings.MainPlayer.GetItem(myItemData.ItemIndex) != null', 'Bindings.MainPlayer.ContainsItemAtIndex(myItemData)')
            text = replace(text, 'Parsers.Instance.SkillSkinInfoParser.Get(skillSkinId) != null', 'Bindings.HasSkillSkin(skillSkinId)')
            text = replace(text, 'Parsers.Instance.IProfessionItemInfoParser.Get(skillSkinId)', 'Bindings.GetItemInfo(skillSkinId)')
        elif name == 'SkillStarInfo':
            text = text.replace('SkillRankInfo skillRankInfo = Parsers.Instance.SkillRankInfoParser.Get(rank)', 'ProfessionSkillRankDefinition skillRankInfo = Bindings.GetRank(rank)')
            text = replace(text, 'Parsers.Instance.SkillRankInfoParser.GetMaxRankByQuality(skillRankInfo.Quality)', 'Bindings.GetMaxRankByQuality(skillRankInfo.Quality)')
        if any(t in text for t in ('[Inject]', 'Parsers.Instance', 'MeManager', 'ResourceLoader.LoadSprite', 'Disposable.AddTo', 'WindowManager.Instance')):
            raise ValueError('Unbound dependency in ' + name)
        text = 'namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.WorldUI;\n' + text + '\n}\n'
        path = DEST / (name + '.cs')
        path.write_text(text, encoding='utf-8', newline='\n'); meta(path)
        ports.append(dict(name=name, source=record(src), output=record(path), meta=record(path.with_name(path.name+'.meta'))))
    bindings = DEST / 'ProfessionInfoBindings.cs'; meta(bindings)
    # The InfoPanel calls only this original comparer chain: Rank descending,
    # signed long subtraction collapsed to -1/0/+1, with no secondary key.
    comparator = DEST / 'ProfessionInfoSkillComparer.cs'
    comparator.write_text('''namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionInfoSkillComparer
    {
        public static ProfessionInfoSkillComparer sDefault { get; } = new ProfessionInfoSkillComparer();
        public int DefaultComparer(IProfessionInfoSkill x, IProfessionInfoSkill y)
        {
            long longValue = (long)y.Rank - x.Rank;
            if (longValue == 0L) return 0;
            if (longValue <= 0) return -1;
            return 1;
        }
    }
}
''', encoding='utf-8', newline='\n'); meta(comparator)
    proof = dict(format=1, manifest=checked['manifest'], code_manifest=record(receipt), application_assembly=record(assembly),
        ports=ports, bindings=[record(bindings), record(bindings.with_name(bindings.name+'.meta'))],
        comparer=dict(sources=[record(CODE/'common-source'/n) for n in ('SkillItemComparer.cs', 'ItemComparerBase.cs')], output=record(comparator), meta=record(comparator.with_name(comparator.name+'.meta')),
            scope='Only InfoPanel default ItemInfo comparator chain; Rank descending, long subtraction and signed collapse, ties return zero with no extra key.'),
        generator=record(Path(__file__)),
        operation='Copy all methods of eight original information/skill-icon components. Preserve original order, owned/unowned branch, subscriptions, star threshold, quality lookup, blessing effects, skill-skin refresh and icon pool lifecycle. Adapt application parser/player/window/pool accesses to explicit consumed services; reuse original native LoopScroll, ItemIcon, UIBase, MonoDictionary and shared text/state ports.',
        validated_scope='Pending actual Unity Play Mode validation.',
        limitations=['Original information and skill prefab bindings, production services, complete upgrade panel and skill-detail popup remain incomplete.', 'Complete native animated profession pages, save/input and Windows release remain incomplete.'])
    (ROOT/'assets/reference/profession-ui/info-panels-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print('Ported', len(ports), 'original information and skill-icon components.')


if __name__ == '__main__':
    build()
