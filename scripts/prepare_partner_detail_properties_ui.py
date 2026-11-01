"""Stage the selected detail-property controller behind explicit local ports."""
import hashlib
import json
from pathlib import Path
import re

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]
DEST = Path('unity/Assets/Game/Runtime/Official/PartnersUI/Detail')


def build(work):
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review the detail property source after changing versions')
    inputs, outputs = {}, {}
    folder = work / 'detail-property-code'
    folder.mkdir(exist_ok=True)

    def source(relative):
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected detail property source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def replace(text, old, new):
        if old not in text:
            raise ValueError('Original detail property expression changed: ' + old)
        return text.replace(old, new)

    def write(name, text, destination=DEST):
        path = folder / name
        path.write_text('\n'.join(line.rstrip() for line in text.split('\n')), 'utf8', newline='\n')
        identity = hashlib.md5(('zjcs-partner-ui:' + (destination / name).as_posix()).encode()).hexdigest()
        metadata = Path(str(path) + '.meta')
        importer = 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        metadata.write_text('fileFormatVersion: 2\nguid: ' + identity
            + '\n' + importer + ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
        for entry in (path, metadata):
            outputs[(destination / entry.name).as_posix()] = hashlib.sha256(entry.read_bytes()).hexdigest()

    text = source('client-source/NpcDetailPanelPropContent.cs')
    for relative in ('client-source/NpcUtils.cs', 'common-source/Common/BattleFormulaHandler.cs'):
        source(relative)
    source('common-source/Common/Formula.cs')
    source('client-source/UIUtils.cs')
    source('client-source/EUIInstantiateFlag.cs')
    text = re.sub(r'^using (Common|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    start = text.index('\t[Inject]')
    end = text.index('\n\tprotected override void OnDestroy()', start)
    text = text[:start] + '''    private IPartnerDetailPropertyServices services;
    public void Configure(IPartnerDetailPropertyServices value)
    { services = value ?? throw new ArgumentNullException(nameof(value)); }
''' + text[end:]
    text = replace(text, 'new NpcTargetData(mNpcData.NpcId, mNpcData.NpcClassId, mNpcData.PlayerId, PlanesInfoUtils.PlanesRank)',
        'services.CreateNpcTarget(mNpcData.NpcClassId)')
    start = text.index('\t\tType type = ', text.index('private void OnClickTipBtn'))
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''        services.PopupFriendship(this, mNpcInfo.IsStarGodNpc, dialog =>
        {
            if (dialog != null) dialog.Init(mNpcInfo.Friendship);
        });''' + text[end:]
    start = text.index('\t\tPlayerNpcCollectReq req = ', text.index('private void OnClickCollectionBtn()'))
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''        services.SetCollected(mNpcInfo.Id, !mNpcData.IsCollected, OnClickCollectionBtnCompleted);''' + text[end:]
    start = text.index('\t\tGLoggerErrorInterpolatedStringHandler message;', text.index('private void OnClickCollectionBtnCompleted'))
    end = text.index('\n\t\telse\n\t\t{\n\t\t\tbool isCollected', start)
    text = text[:start] + '''        if (errCode != 0)
        {
            Debug.LogError("OperPlayerCollectNpc failed, errCode:" + errCode);
        }
        else if (response == null)
        {
            Debug.LogError("mCollectionBtn OperPlayerCollectNpc response is null");
        }''' + text[end:]
    start = text.index('\t\tmPlayerLiteInfoManager.GetLiteInfo(', text.index('private void OnClickPropBtn()'))
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''        services.GetLiteInfo(mNpcTargetData, mLiteInfoCallback ?? (mLiteInfoCallback = new PartnerDetailLiteInfoCallback((PartnerDetailLiteInfo liteInfo) =>
        {
            mNpcTargetData.NpcLevel = liteInfo.Level;
            mNpcTargetData.NpcSubRank = liteInfo.SubRank;
            mNpcTargetData.CreatePlanesRank = liteInfo.TargetData.CreatePlanesRank;
            services.PopupNpcBrief(this, mNpcTargetData);
        })));''' + text[end:]
    start = text.index('\t\tNpcPositionNavigateParam param = ', text.index('private void OnClickJumpBtn()'))
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''        services.NavigateNpcPosition(mNpcInfo.Id, NavigateCallBack);''' + text[end:]
    text = replace(text, '\t\tPlayerItemDataWrap itemById = MeManager.MainPlayer.GetItemById(treasureClassId);\n\t\tif (itemById == null || !itemById.GetItemParamTreasure().Active)',
        '\t\tif (!services.IsTreasureActive(treasureClassId))')
    text = replace(text, '\t\tCommon.NpcAdventureSettings npcAdventureSettings = Parsers.GameSettings.NpcAdventureSettings;\n', '')
    original = 'UIUtils.Instantiate<NpcFriendshipPropItemGroup>(mPropItemGroupPrefab, mSlot).Init(addPropsList: GetNpcTreasurePropsList(mNpcInfo.TreasureClassId, item2.Value), propList: item2.Value, showBg: showBg);'
    text = replace(text, original, '''NpcFriendshipPropItemGroup group = PartnerDetailUiUtils.Instantiate<NpcFriendshipPropItemGroup>(mPropItemGroupPrefab, mSlot);
            group.Configure(services);
            group.Init(addPropsList: GetNpcTreasurePropsList(mNpcInfo.TreasureClassId, item2.Value), propList: item2.Value, showBg: showBg);''')
    for old, new in {
        'MeManager.MainPlayer.IsFunctionLimit(FunctionType.PinNpc)': 'services.IsPinFunctionLimited',
        'BattleFormula.Instance.CalcPlayerNpcProps(MeManager.MainPlayer, mNpcInfo.Id)': 'services.CalcPlayerNpcProps(mNpcInfo.Id)',
        'npcAdventureSettings.IsUnlockTraveNpcProp(MeManager.MainPlayer, item.Key)': 'services.IsUnlockTravelNpcProp(item.Key)',
        'NpcUtils.TryGetUnlockNpcNavigateInfo(mNpcInfo.Id, out var _)': 'services.CanNavigateNpc(mNpcInfo.Id)',
        'NpcUtils.GetTreasureToNpcFriendshipPercent': 'services.GetTreasureToNpcFriendshipPercent',
        'BroadcastManager.TrySystemMsg(LanguageManager.Instance.GetText(key))': 'services.SystemMessage(services.GetText(key))',
        'ret.TipMsg.IsTxtVaildForNavigate()': 'services.IsNavigateTipValid(ret.TipMsg)',
        'NavigateUtils.BroadcastTipMsg(ret.TipMsg)': 'services.BroadcastNavigateTip(ret.TipMsg)',
        'StateSwitcher<': 'WorldStateSwitcher<',
    }.items():
        text = replace(text, old, new)
    if any(value in text for value in ('Parsers.', 'MeManager.', '[Inject]', 'GLogger', 'ServiceProxyBase', 'WindowManager.', 'NpcUtils.')):
        raise ValueError('Unbound selected detail dependency')
    aliases = '''using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerDetailNpcInfo;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerDetailNpcData;
using NpcTargetData = Zjcs.Official.PartnersUI.PartnerDetailNpcTarget;
using LiteInfoCallback = Zjcs.Official.PartnersUI.PartnerDetailLiteInfoCallback;
using PlayerNpcCollectRep = Zjcs.Official.PartnersUI.PartnerDetailCollectedReply;
using NavigateResult = Zjcs.Official.PartnersUI.PartnerDetailNavigateResult;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
'''
    write('NpcDetailPanelPropContent.cs', '// Selected official detail-property controller; see partner-detail-properties-ui-source-manifest.json.\n'
        + 'namespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n')
    write('PartnerDetailUiUtils.cs', '''using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.PartnersUI
{
    internal static class PartnerDetailUiUtils
    {
        // Consumed UIUtils.Instantiate<T>(GameObject,Transform,All): injected
        // creation, position/rotation/scale reset, activation, GetComponent.
        public static T Instantiate<T>(GameObject prefab, Transform parent)
        {
            GameObject go = ProfessionGameObjectPoolBridge.Instantiate(parent, prefab, parent, false);
            Transform transform = go.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            go.SetActive(true);
            return go.GetComponent<T>();
        }
    }
}
''')
    template = ROOT / 'scripts/templates/partner_detail/PartnerDetailPropertyServices.cs'
    inputs[template.relative_to(ROOT).as_posix()] = hashlib.sha256(template.read_bytes()).hexdigest()
    write(template.name, template.read_text('utf8'))
    callback = source('client-source/LiteInfoCallbackBase.cs')
    source('client-source/LiteInfoCallback.cs')
    callback = re.sub(r'^using Framework[^\n]*\n', '', callback, flags=re.M)
    callback = callback.replace('public abstract class LiteInfoCallbackBase<T>', 'public sealed class PartnerDetailLiteInfoCallback')
    callback = callback.replace('public LiteInfoCallbackBase(', 'public PartnerDetailLiteInfoCallback(')
    callback = re.sub(r'\bT\b', 'PartnerDetailLiteInfo', callback)
    start = callback.index('\t\t\tGLoggerErrorInterpolatedStringHandler message;')
    end = callback.index('\n\t\t\tRelease();', start)
    callback = callback[:start] + '''            if (!ex.ToString().StartsWith("UnityEngine.MissingReferenceException: The object of type"))
            {
                UnityEngine.Debug.LogException(ex);
                return;
            }
            UnityEngine.Debug.LogError(mCallback.Method.Name + "未释放");''' + callback[end:]
    write('PartnerDetailLiteInfoCallback.cs', '// Selected LiteInfoCallbackBase; only logging and closed generic type adapted.\nnamespace Zjcs.Official.PartnersUI\n{\n' + callback + '\n}\n')
    formula = source('common-source/Common/BattleFormulaHandler.cs')
    methods = {}

    def extract(signature):
        start = formula.index(signature)
        opening = formula.index('{', start)
        depth, end = 1, opening + 1
        while depth:
            depth += (formula[end] == '{') - (formula[end] == '}')
            end += 1
        result = formula[start:end]
        methods[signature] = hashlib.sha256(result.encode()).hexdigest()
        return result

    chunks = []
    for signature in (
        'public IReadOnlyDictionary<PropType, long> CalcPlayerNpcProps(Player player, int npcClassId)',
        'public IReadOnlyDictionary<PropType, long> CalcNpcFriendshipProps(int npcClassId, int friendshipLevel)',
        'public IReadOnlyDictionary<PropType, long> CalcNpcTalentTreeProps(int npcClassId, IReadOnlyCollection<int> activeTalentPoint)',
        'public void MergePropsInPlace(Dictionary<PropType, long> dest, IReadOnlyDictionary<PropType, long> add)',
        'public long GetRawProp(Dictionary<PropType, long> props, PropType type)',
        'public void AddProp(Dictionary<PropType, long> props, PropType propType, long value)',
    ):
        chunk = extract(signature)
        chunk = chunk.replace('Player player', 'IPartnerNpcPropertyPlayer player')
        chunk = chunk.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(npcClassId)', 'PartnerNpcPropertyInfo npcInfo = source.GetNpc(npcClassId)')
        chunk = chunk.replace('NpcTalentTreeMapPropInfoParser npcTalentTreeMapPropInfoParser = Parsers.Instance.NpcTalentTreeMapPropInfoParser;', '')
        chunk = chunk.replace('LevelPropParser levelPropParser = Parsers.Instance.LevelPropParser;', '')
        chunk = chunk.replace('NpcTalentTreeMapPropInfo nodeMapPropInfo = npcTalentTreeMapPropInfoParser.GetNodeMapPropInfo', 'PartnerNpcTalentPropertyInfo nodeMapPropInfo = source.GetNodeMapPropInfo')
        chunk = chunk.replace('Parsers.Instance.LevelPropParser.GetProps', 'source.GetProps').replace('levelPropParser.GetProps', 'source.GetProps')
        chunk = chunk.replace('BattleFormula.Instance.', '')
        chunk = re.sub(r'(?ms)\t\t\tGLoggerErrorInterpolatedStringHandler message = .*?\n\t\t\tLog.Error\([^\n]*\);',
            '\t\t\tUnityEngine.Debug.LogError("Official partner property configuration or destination is absent.");', chunk)
        if any(value in chunk for value in ('Parsers.', 'GLogger', 'BattleFormula.')):
            raise ValueError('Unbound original NPC property formula dependency')
        chunks.append(chunk)
    write('PartnerNpcPropertyFormula.cs', '''using System;
using System.Collections.Generic;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
namespace Zjcs.Official.PartnersUI
{
    public sealed class PartnerNpcPropertyInfo { public int LevelPropId; }
    public sealed class PartnerNpcTalentPropertyInfo
    {
        public int PropId, PropLevel;
        public List<PropType> PropTypes;
    }
    public interface IPartnerNpcPropertyPlayer
    {
        IReadOnlyDictionary<int, int> GetNpcFriendshipLevelDict();
        IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int npcId);
    }
    public interface IPartnerNpcPropertySource
    {
        PartnerNpcPropertyInfo GetNpc(int npcId);
        PartnerNpcTalentPropertyInfo GetNodeMapPropInfo(int npcId, int nodeId);
        IReadOnlyDictionary<PropType, long> GetProps(int propId, int level);
    }
    // Selected BattleFormulaHandler methods; preserve the original branches,
    // merge order, zero entries and integer addition.
    public sealed class PartnerNpcPropertyFormula
    {
        private readonly IPartnerNpcPropertySource source;
        public PartnerNpcPropertyFormula(IPartnerNpcPropertySource value)
        { source = value ?? throw new ArgumentNullException(nameof(value)); }
''' + '\n\n'.join(chunks) + '\n    }\n}\n')
    template = ROOT / 'scripts/templates/partner_detail/OfflinePartnerDetailPropertySource.cs'
    inputs[template.relative_to(ROOT).as_posix()] = hashlib.sha256(template.read_bytes()).hexdigest()
    write(template.name, template.read_text('utf8'), Path('unity/Assets/Game/Runtime/Offline/PartnersUI'))
    template = ROOT / 'scripts/templates/partner_detail/OfficialPartnerDetailPropertyAssetCatalog.cs'
    inputs[template.relative_to(ROOT).as_posix()] = hashlib.sha256(template.read_bytes()).hexdigest()
    write(template.name, template.read_text('utf8'), Path('unity/Assets/Game/Runtime/Offline/PartnersUI'))
    template = ROOT / 'scripts/templates/partner_detail/OfflinePartnerDetailPropertyServices.cs'
    inputs[template.relative_to(ROOT).as_posix()] = hashlib.sha256(template.read_bytes()).hexdigest()
    write(template.name, template.read_text('utf8'), Path('unity/Assets/Game/Runtime/Offline/PartnersUI'))
    write('Zjcs.Official.PartnerDetailUI.asmdef', json.dumps(dict(name='Zjcs.Official.PartnerDetailUI',
        rootNamespace='Zjcs.Official.PartnersUI', references=['Unity.ugui', 'Zjcs.Official.Partners',
        'Zjcs.Official.PartnerGiftUI', 'Zjcs.Official.PartnerGiftControllerUI',
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI']), indent=2) + '\n')
    proof = dict(manifest=version, generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        inputs=inputs, outputs=outputs, formula_methods_sha256=methods, adaptations=[
            'Retain sorted property rows, original travel gating, pair grouping, alternating backgrounds and treasure truncation.',
            'Explicit services replace global player, configuration, network collect reply, lite-info, windows and navigation.',
            'Original lite-info callback stays reusable and is released on destruction; no replacement UI hierarchy.'],
        scope='Detail-property controller staged; local services, native binding and runtime validation are required before installation.')
    (work / 'partner-detail-properties-ui-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print(folder)


if __name__ == '__main__':
    build(validation_directory())
