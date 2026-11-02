"""Port the selected NpcPanel graph dependency nodes; retain original control flow."""
import re
from official_apk import ROOT
from prepare_partner_input_dialog import metadata

NODES = ('TipDotFurnitureCanRepairConditionNode', 'TipDotHasAnyNpcDialogueConditionNode',
         'TipDotHasFurnitureConditionNode', 'TipDotLinkNpcNode', 'TipDotLinkPetAbilityItemNode',
         'TipDotLinkPetSkillItemNode', 'TipDotPetCanEvolutionConditionNode',
         'TipDotPetCanLevelUpConditionNode', 'TipDotPetPotentialUpgradeConditionNode')


def prepare():
    target = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots'
    original = ROOT / '.tools/source-packages/official-code'
    selected = ROOT / '.tools/source-packages/current/884_156692/official-code'
    inputs, outputs = [], []
    names = [(name, 'client-source') for name in (*NODES, 'PetNodeInstanceInfo',
              'PetSkillNodeInstanceInfo', 'PetAbilityNodeInstanceInfo')]
    names.append(('PetEvolutionPhase', 'common-source/Common'))
    for name, folder in names:
        source = original / folder / (name + '.cs')
        current = selected / folder / source.name
        if source.read_bytes() != current.read_bytes():
            raise ValueError('Selected graph source differs: ' + name)
        body = source.read_text('utf8')
        body = re.sub(r'^using (Common|Framework|NaughtyAttributes|GFrameCore.Extensions.Cloneable);\n', '', body, flags=re.M)
        body = body.replace('namespace Common;\n', '')
        body = re.sub(r'^\s*\[(MenuPath\([^\n]*|SearchableEnum)\]\n', '\n', body, flags=re.M)
        body = body.replace(', IFillable<TipDotNodeBase>', '')
        if name in NODES:
            body = body.replace('public class ' + name + ' : ', 'public class ' + name + ' : ', 1)
            body = body.replace('\n{', ', IPartnerPanelTipDotNode\n{\n\tpublic IPartnerPanelTipDotServices Services { get; set; }', 1)
        elif name != 'PetEvolutionPhase':
            body = body.replace('\n{', '\n{\n\tpublic IPartnerPanelTipDotServices Services { get; set; }', 1)
        replacements = {
            'EFurniture': 'int', 'FurnitureInfo': 'PartnerPanelFurnitureInfo',
            'PlayerNpcDialogueDataWrap': 'object', 'PlayerPetDataWrap': 'PartnerPanelPetRecord',
            'PlayerItemDataWrap': 'PartnerTipDotItem', 'AwardSourceData': 'object',
            'PetInfo': 'PartnerPanelPetInfo', 'PetAbilityInfo': 'PartnerPanelPetAbilityInfo',
            'PetEvolutionInfo': 'PartnerPanelPetEvolutionInfo', 'ICondition': 'PartnerPanelCondition',
            'NpcInfo': 'PartnerPanelNpcInfo', 'Player mainPlayer': 'IPartnerPanelTipDotPlayer mainPlayer',
            'MeManager.MainPlayer': 'Services.Player',
            'Parsers.Instance.FurnitureInfoParser.Get': 'Services.GetFurniture',
            'Parsers.Instance.NpcInfoParser.GetNpcs(NpcType.MapExploreBoss)': 'Services.MapExploreNpcs()',
            'Parsers.Instance.PetInfoParser.Get': 'Services.GetPet',
            'Parsers.Instance.PetAbilityInfoParser.Get': 'Services.GetPetAbilities',
            'Parsers.Instance.PetAbilityPropInfoParser': 'Services.PetAbilityPropInfoParser',
            'Parsers.Instance.PetEvolutionInfoParser.Get': 'Services.GetPetEvolution',
            'Parsers.Instance.PetFoodInfoParser.SortedFoods': 'Services.SortedFoods',
            'Parsers.Instance.FunctionLimitInfoParser.Get(FunctionType.PetPotential).Condition': 'Services.GetPetPotentialCondition()',
            'PetUtils.TryCalcLevelUpExpRequire': 'Services.TryCalcLevelUpExpRequire',
            'ConditionHandler.Instance.Register': 'Services.RegisterCondition',
            'ConditionHandler.Instance.Deregister': 'Services.DeregisterCondition',
            'ConditionHandler.Instance != null': 'Services != null',
            'NodeManager.ConnectNodes': '((IPartnerDynamicTipDotNodeManager)NodeManager).ConnectNodes',
            'LanguageManager.Instance.GetText(LanguageUtils.GetItemNameKey(SkillId))': 'Services.ItemName(SkillId)',
            'CloneableHelper.ThrowException(source == null, GetType().Name);': 'throw new System.ArgumentException(GetType().Name, nameof(source));',
            'CloneableHelper.Fill<TipDotGraphNodeSelector, TipDotGraphNodeSelector>(ref mNodeSelector, obj.mNodeSelector);': 'mNodeSelector = obj.mNodeSelector?.Clone();',
            'ClonerResolver.Instance.GetClonerWithVerify<List<NpcRoleType>>().Fill(ref NpcRoleTypes, obj.NpcRoleTypes);': 'NpcRoleTypes = obj.NpcRoleTypes == null ? null : new List<NpcRoleType>(obj.NpcRoleTypes);',
        }
        # Type replacement must leave member names such as FurnitureInfo unchanged.
        for before, after in sorted(replacements.items(), key=lambda row: -len(row[0])):
            if before.isidentifier():
                if before == 'FurnitureInfo':
                    body = re.sub(r'\bFurnitureInfo(?=\s+(?:mFurnitureInfo|FurnitureInfo)\b)', after, body)
                else: body = re.sub(r'\b' + before + r'\b', after, body)
            else: body = body.replace(before, after)
        body = re.sub(r'(?<![.\w])Player\.', 'Services.Player.', body)
        # Preserve graph key identity while supplying the original diagnostic-name services.
        body = re.sub(r'new (Pet(?:Ability|Skill)?NodeInstanceInfo)\(([^();\n]*)\)',
                      r'new \1(\2) { Services = Services }', body)
        if any(token in body for token in ('MeManager.', 'Parsers.', 'CloneableHelper.', 'ConditionHandler.', 'PetUtils.')):
            raise ValueError('Unmapped original graph dependency: ' + name)
        body = '// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.\n' + \
            'namespace Zjcs.Official.PartnersUI\n{\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.Partners;\n' + body + '\n}\n'
        path = target / source.name
        path.write_text(body, 'utf8', newline='\n')
        inputs.extend((source, current)); outputs.extend((path, metadata(path)))
    for path in (target / 'PartnerPanelTipDotServices.cs', ROOT / 'scripts/prepare_partner_panel_tipdots.py'):
        inputs.append(path)
    outputs.append(metadata(target / 'PartnerPanelTipDotServices.cs'))
    return inputs, outputs


if __name__ == '__main__': prepare()
