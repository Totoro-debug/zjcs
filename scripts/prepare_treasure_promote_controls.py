"""Migrate complete original treasure promotion controllers and their common material/effect controls."""
import argparse
import hashlib
import json
import os
import re
import uuid
from pathlib import Path
from official_source import ROOT, OfficialSource
from prepare_pet_score_chest_preview import original_method

DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasurePromoteUI'
RECEIPT=ROOT/'assets/reference/treasure-promote-controls-source-manifest.json'
NAMES=('TreasurePromotePanel','TreasurePromoteTab','TreasureLevelupPanel','TreasureUpgradePanel',
       'TreasureUpgradePanelStarInfo','TreasureAwakeMaterialPanel','ItemIconTreasurePiece','TreasureCanMergeRedDotItem')
COMMON={'APropEffectPlayAgent':'ProfessionPropEffectPlayAgentBase','PropEffectPlayAgent':'ProfessionPropEffectPlayAgent',
        'UseItemSliderGroup':'ProfessionUseItemSliderGroup','CommonMaterial':'ProfessionCommonMaterial',
        'RedDotMultiItem':'ProfessionRedDotMultiItem','DebugUIHandler':'ProfessionDebugUIHandler'}

def sha(raw):return hashlib.sha256(raw).hexdigest()
def clean(text):
    text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?|MessagePipe|R3|Cysharp.Threading.Tasks|UnityEngine.UI.Ext);\s*$', '',text,flags=re.M)
    text=re.sub(r'^\s*//IL_.*$', '',text,flags=re.M)
    return re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','\n',text)

def common(name,text):
    text=clean(text)
    for old,new in COMMON.items():text=re.sub(r'\b'+old+r'\b',new,text)
    text=re.sub(r'\bPlayerPropItem\b','ProfessionPlayerPropItem',text)
    text=text.replace('MultiLanguageText','WorldMultiLanguageText').replace('StateSwitcher<','WorldStateSwitcher<')
    text=text.replace('RedDotItemBase','ProfessionRedDotItemBase')
    if name in ('UseItemSliderGroup','CommonMaterial'):
        text=text.replace('internal class','public class')
        at=text.index('\n{',text.index('class '))+2
        text=text[:at]+'''
    private IProfessionMaterialServices services;
    private IProfessionMaterialServices Services => services??throw new InvalidOperationException("Bind original material controls before activation.");
    public void Configure(IProfessionMaterialServices value)=>services=value??throw new ArgumentNullException(nameof(value));
'''+text[at:]
        text=text.replace('ItemIconManager != null','services != null').replace('ItemIconManager.', 'Services.')
        text=text.replace('LanguageUtils.GetItemNameKey(', 'Services.GetItemNameKey(')
        text=text.replace('ItemIconConfig.Value.', 'Services.IconConfig.')
        text=text.replace('new MultiTextDecorator(CultureNumberArg.Compact(current), new ColorWrapTextProcessor(GlobalColors.Value, Color32.op_Implicit(GlobalColors.Value.LightRed)))',
                          'Services.DecorateLack(CultureNumberArg.Compact(current))')
    if name=='DebugUIHandler':
        at=text.index('\n{',text.index('class '))+2
        text=text[:at]+'''
    private IProfessionDebugUIServices services;
    private IProfessionDebugUIServices Services=>services??throw new InvalidOperationException("Bind the original debug UI actor before activation.");
    public void Configure(IProfessionDebugUIServices value)=>services=value??throw new ArgumentNullException(nameof(value));
'''+text[at:]
        text=text.replace('AssetGuidProxy<GameObject>','ProfessionPrefabGuidProxy')
        text=text.replace('MeManager.MainPlayer != null','Services.PlayerAvailable').replace('MeManager.MainPlayer.Gm','Services.IsGameMaster')
        text=text.replace('private DisposableBag mDisposableBag;','private ProfessionResourceOperationBag mDisposableBag=new ProfessionResourceOperationBag();')
        text=text.replace('mDisposableBag = default;', 'mDisposableBag = new ProfessionResourceOperationBag();')
        text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerDebugCheckIsGM(invokeByPlayer: false).OnSuccess += DebugUIHandler_OnComplete;',
                          'Services.CheckGameMaster(false,DebugUIHandler_OnComplete);')
        text=text.replace('BoolData isGm','bool isGm')
        text=text.replace('UIUtils.Instantiate(Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(mAssetGuidAndSlotProxy.AssetGuidProxy.AssetGuid), ref mDisposableBag).Result, mAssetGuidAndSlotProxy.Slot);',
                          'Services.Instantiate(mDisposableBag.Add(Services.Resources.LoadAssetByGuid<GameObject>(mAssetGuidAndSlotProxy.AssetGuidProxy.AssetGuid)).Result,mAssetGuidAndSlotProxy.Slot);')
    if any(key in text for key in ('[Inject]','ItemIconManager.','GlobalColors.','LanguageUtils.')):raise ValueError('Unbound original material dependency: '+name)
    return ('// Complete original common promotion control; explicit local material services.\nusing System;\n'
            'using Zjcs.Official.WorldUI;\nusing Zjcs.Official.World.NativeLabelUI.Framework;\n'
            'namespace Zjcs.Official.ProfessionUI {\nusing Object=UnityEngine.Object;\n'+text+'\n}\n').encode()

def adapt(name,text):
    text=clean(text).replace('internal class','public class')
    text=re.sub(r'^\[UIWindow\([^\n]+\)\]\n','',text,flags=re.M)
    text=text.replace('MultiLanguageText','WorldMultiLanguageText').replace('StateSwitcher<','WorldStateSwitcher<')
    text=text.replace('PlayerItemDataWrap','IProfessionItemData').replace('PlayerItemParamTreasureWrap','TreasureState')
    text=text.replace('TabGroup','WorldTabGroup').replace('TabItem','WorldTabItem')
    # Serialized field names and original tab-group API names retain their source spelling.
    text=text.replace('mWorldTabGroupGo','mTabGroupGo').replace('OnWorldTabItemChanged','OnTabItemChanged')
    text=text.replace('GetWorldTabItem','GetTabItem').replace('ResetWorldTabItem','ResetTabItem')
    for old,new in COMMON.items():text=re.sub(r'\b'+old+r'\b',new,text)
    text=re.sub(r'\bPlayerPropItem\b','ProfessionPlayerPropItem',text)
    at=text.index('\n{',text.index('public class'))+2
    if name!='TreasurePromoteTab':
        text=text[:at]+'''
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));
'''+text[at:]
    text=text.replace('mItemData.GetItemParamTreasure()', 'Services.Icons.GetState(mItemData)')
    text=text.replace('itemData.Info.ElementType','(ElementType)Services.Icons.GetElement(itemData.Info.ClassId)')
    text=text.replace('mItemData.Info.ElementType','(ElementType)Services.Icons.GetElement(mItemData.Info.ClassId)')
    text=text.replace('mItemData.Info.PartInfo as TreasureInfo','Services.Tables.GetTreasure(mItemData.Info.ClassId)')
    text=text.replace('TreasureInfo treasureInfo = Services.Tables.GetTreasure(mItemData.Info.ClassId);','TreasureInfo treasureInfo = Services.Tables.GetTreasure(mItemData.Info.ClassId);')
    text=text.replace('(Services.Tables.GetTreasure(mItemData.Info.ClassId)).LimitUseCommonMaterial', 'Services.LimitCommonMaterial(mItemData.Info.ClassId)')
    text=text.replace('(Services.Tables.GetTreasure(mItemData.Info.ClassId)).StarMax','Services.Icons.GetStarMax(mItemData)')
    text=text.replace('mItemData.Info.Quality', 'mItemData.GetQuality()').replace('ItemData.Info.Quality','ItemData.GetQuality()')
    text=text.replace('Player mainPlayer = MeManager.MainPlayer;', 'ITreasurePromoteServices mainPlayer = Services;')
    text=text.replace('TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(', 'TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(Services.Tables,')
    text=text.replace('BattleFormula.Instance.CalcTreasureProps(', 'Services.Calculator.CalcTreasureProps(')
    text=text.replace('treasureInfo.GetStarProps(MeManager.MainPlayer, ', 'Services.Properties.GetStarProps(treasureInfo,Services.Player, ')
    text=text.replace('treasureInfo.GetEffectPropInfo(MeManager.MainPlayer, ', 'Services.Properties.GetEffectPropInfo(treasureInfo,Services.Player, ')
    text=text.replace('itemParamTreasure.GetHitCount(treasureInfo, treasureFinalLevel)', 'Services.GetHitCount(treasureInfo,treasureFinalLevel)')
    text=text.replace('new TreasureItemPropProvider(mItemData)', 'new TreasureDummyPropProvider(Services.Tables,Services.Player,mItemData.Id)')
    text=text.replace('PropType.None','0').replace('LanguageManager.Instance.GetText($"{typeof(PropType).Name}.{item.Key}")','LanguageManager.Instance.GetText(TreasureViewLanguage.GetPropertyNameKey(item.Key))')
    text=text.replace('LanguageManager.Instance.GetText($"{typeof(PropType).Name}.{mTmpCurStarProp.Key}")','LanguageManager.Instance.GetText(TreasureViewLanguage.GetPropertyNameKey(mTmpCurStarProp.Key))')
    text=re.sub(r'\.Init\((item|mTmpCurStarProp)\.Key,',r'.Init((ProfessionPropertyType)\1.Key,',text)
    text=text.replace('MeManager.MainPlayer.GetTreasureFinalLevel(', 'Services.GetTreasureFinalLevel(')
    text=text.replace('MeManager.MainPlayer.GetTreasureBaseLevel(', 'Services.GetTreasureBaseLevel(')
    text=text.replace('MeManager.MainPlayer.GetTreasureInSlotIndex(', 'Services.GetTreasureInSlotIndex(')
    text=text.replace('MeManager.MainPlayer.IsTreasureInSlot(', 'Services.Player.IsTreasureInSlot(')
    text=text.replace('MeManager.MainPlayer.SubRank', 'Services.SubRank')
    text=text.replace('PlanesInfoUtils.SeasonId', 'Services.SeasonId')
    text=text.replace('TreasureHelper.IsTreasureMaxLevel(MeManager.MainPlayer, ', 'Services.IsTreasureMaxLevel(')
    text=text.replace('Formula.GetMaxTreasureBaseLevel(mainPlayer.SubRank)', 'Services.MaxBaseLevel')
    text=text.replace('Formula.GetMaxTreasureBaseLevel(Services.SubRank)', 'Services.MaxBaseLevel')
    text=text.replace('Formula.Instance.GetMaxTreasureBlessLevel(Services.SubRank, PlanesInfoUtils.PlanesRank)', 'Services.MaxBlessLevel')
    text=text.replace('Formula.Instance.CheckTreasureSlotLevelHasBless(mainPlayer.SubRank, PlanesInfoUtils.PlanesRank, num, mainPlayer.BaseLevel, mainPlayer.GetCurBlessPlayerLevel())', 'Services.HasBless(num)')
    text=text.replace('Formula.Instance.CheckTreasureSlotLevelHasBless(mainPlayer.SubRank, PlanesInfoUtils.PlanesRank, treasureFinalLevel, mainPlayer.BaseLevel, mainPlayer.GetCurBlessPlayerLevel())', 'Services.HasBless(treasureFinalLevel)')
    text=text.replace('ItemIconManager.', 'Services.Materials.')
    text=text.replace('new DisposableBag()', 'new ProfessionResourceOperationBag()').replace('DisposableBag mDisposableBag','ProfessionResourceOperationBag mDisposableBag')
    text=text.replace('mDisposableBag = default;', 'mDisposableBag = new ProfessionResourceOperationBag();')
    if name=='TreasurePromotePanel':
        text=text.replace('_ = mItemData.Info.PartInfo;', '_ = Services.Tables.GetTreasure(mItemData.Info.ClassId);')
        text=text.replace('Init(ulong index)','Init(int index)').replace('MeManager.MainPlayer.GetItem(index)','Services.Icons.GetItemById(index)')
        text=text.replace('MeManager.MainPlayer.GetItemById(id)','Services.Icons.GetItemById(id)').replace('itemById.ItemIndex','itemById.Id')
        text=text.replace('Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(mItemData.Info.Icon), ref mDisposableBag).Result',
                          'mDisposableBag.Add(Services.Images.LoadAsset<Sprite>("Assets/Image/"+mItemData.Info.Icon)).Result')
        text=text.replace('Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(LanguageUtils.GetTreasureQualityBgPath(mItemData.GetQuality())), ref mDisposableBag).Result',
                          'mDisposableBag.Add(Services.Images.LoadAsset<Sprite>(Services.QualityBackground(mItemData.GetQuality()))).Result')
        text=text.replace('LanguageUtils.GetItemNameKey(mItemData.Id)', 'Services.Materials.GetItemNameKey(mItemData.Id)')
        start=text.index('\n\t\tdefault:');end=text.index('\n\t\t}\n\t\tRefreshReddots',start)
        text=text[:start]+'\n\t\tdefault: Debug.LogWarning("未处理的 TreasureTab 类型！"+tabType);break;'+text[end:]
    elif name=='TreasureLevelupPanel':
        text=text.replace('PersistentState.State.AutoMergeWhenLevelUp', 'Services.AutoMergeWhenLevelUp')
        old=original_method(text,'private void OnAutoMergeToggleChange(')
        text=text.replace(old,'private void OnAutoMergeToggleChange(bool isOn) { Services.SetAutoMergeWhenLevelUp(isOn); }')
        text=text.replace('MeManager.MainPlayer.OnItemCountUpdate', 'Services.Icons.OnItemCountUpdate').replace('MeManager.MainPlayer != null','services != null')
        text=text.replace('AwardSourceData arg3','object arg3')
        text=text.replace('Parsers.Instance.TreasureLevelInfoParser.GetLevelUpMaterials(MeManager.MainPlayer, treasureFinalLevel + 1)', 'Services.GetLevelUpMaterials(treasureFinalLevel+1)')
        text=text.replace('TreasureBlessInfo treasureBlessInfo = Parsers.Instance.TreasureBlessInfoParser.Get(mainPlayer.Rank, num - maxTreasureBaseLevel);', 'var treasureBlessInfo = Services.GetBlessInfo(num-maxTreasureBaseLevel);')
        text=text.replace('TreasureLevelInfo treasureLevelInfo = Parsers.Instance.TreasureLevelInfoParser.Get(num);', 'var treasureLevelInfo = Services.GetLevelInfo(num);')
        text=text.replace('ItemMergeInfoParser itemMergeInfoParser = Parsers.Instance.ItemMergeInfoParser;', '')
        text=text.replace('ItemSourceDataManager.IsItemSourceFromMerge(mainPlayer, ', 'Services.IsItemSourceFromMerge(')
        text=text.replace('itemMergeInfoParser.CalcCanMergeAmout(mainPlayer, ', 'Services.CalcCanMergeAmount(')
        text=text.replace('item.Deconstruct(ref num2, ref num3);','item.Deconstruct(out num2, out num3);')
        old=original_method(text,'private (PlayerUseItemContent.UseItemState, string) GetUseItemState(')
        text=text.replace(old,'private (PlayerUseItemContent.UseItemState, string) GetUseItemState() => Services.GetLevelUseItemState(mItemData,mAutoMergeToggle.isOn);')
        start=text.index('\n\t\telse\n\t\t{\n\t\t\tGLoggerError');end=text.index('\n\t\tbool show',start)
        text=text[:start]+'\n\t\telse { Debug.LogError("古遗物找不到升级材料："+mItemData.Info.ClassId+"/"+treasureFinalLevel); }'+text[end:]
        old=original_method(text,'private void OnLevelBtnClick(')
        replacement='''private void OnLevelBtnClick()
    {
        bool isOn=mAutoMergeToggle.isOn;
        int index=Services.GetTreasureInSlotIndex(mItemData.Info.ClassId);
        if(index<0){Debug.LogError("未装备在槽位的古遗物无法强化！treasureClassId = "+mItemData.Info.ClassId);return;}
        int code=Services.CanSlotLevelUp((ElementType)Services.Icons.GetElement(mItemData.Info.ClassId),index,isOn);
        if(code!=0){Services.ShowError(code);return;}
        Services.LevelUp(mItemData,isOn,()=>{mEffectAgent.StartPlayEffect();Refresh();});
    }'''
        text=text.replace(old,replacement)
    elif name=='TreasureUpgradePanel':
        text=text.replace('MeManager.MainPlayer.TreasureSlotData.AwakeAutoMergeEnable','Services.AwakeAutoMergeEnable')
        text=text.replace('MeManager.MainPlayer.TreasureSlotData.UpgradeUseCommonMaterailEnable','Services.UpgradeUseCommonMaterailEnable')
        text=text.replace('WindowManager.Instance.OnWindowClosing','Services.OnWindowClosing')
        for method,body in {
            'OnAutoMergeToggleChange':'Services.SetAwakeAutoMerge(isOn);',
            'OnUseCommonMaterialToggleChange':'Services.SetUseCommonMaterial(isOn,()=>{if(mItemData!=null)TryRefreshMaterials();});',
            'OnAwakeMaterialBtnClick':'Services.PopupAwakeMaterials(panel=>{panel.ItemData=mItemData;panel.SelectItems=mSelectedPieces;});',
        }.items():
            old=original_method(text,'private void '+method+'(')
            signature='bool isOn' if method!='OnAwakeMaterialBtnClick' else ''
            text=text.replace(old,'private void '+method+'('+signature+') { '+body+' }')
        text=text.replace('TreasureHelper.GetTreasureNextLevelUpgradeMaterial(mItemData.Id, mainPlayer, ', 'Services.GetUpgradeMaterials(mItemData.Id, ')
        text=text.replace('TreasureHelper.IsTreasureCanUpgradeUseCommonMaterial(mainPlayer, ', 'Services.CanUseCommonMaterial(')
        text=text.replace('TreasureUpgradeInfoParser.sCommonPieceClassId','Services.CommonPieceClassId').replace('Parsers.GameSettings.MoneyId','Services.MoneyId')
        text=text.replace('MeManager.MainPlayer.GetCommonTreasurePiece(mItemData)', 'Services.GetCommonTreasurePiece(mItemData)')
        text=text.replace('Parsers.Instance.TreasureUpgradeInfoParser.GetStarUpCommonPiece(', 'Services.GetStarUpCommonPiece(')
        for method,use_common,auto in [('OnUpgradeBtnClick','Services.CanUseCommonMaterial(mItemData.Id)','true'),('OnAwakeBtnClick','true','Services.AwakeAutoMergeEnable')]:
            old=original_method(text,'private void '+method+'(')
            text=text.replace(old,'''private void '''+method+'''()
    {
        Services.GetUpgradeMaterials(mItemData.Id,true,'''+use_common+''',out var materials);
        int code=Services.CanUpgrade(mItemData.Id,materials,'''+auto+''');
        if(code!=0){Services.ShowError(code);return;}
        Services.Upgrade(mItemData.Id,'''+use_common+','+auto+''',()=>{mEffectAgent.StartPlayEffect();Refresh();});
    }''')
    elif name=='TreasureAwakeMaterialPanel':
        text=text.replace('ItemData.GetItemParamTreasure()','Services.Icons.GetState(ItemData)')
        text=text.replace('Parsers.Instance.TreasureUpgradeInfoParser.GetStarUpCommonPiece(', 'Services.GetStarUpCommonPiece(')
        text=text.replace('MeManager.MainPlayer.GetCommonTreasurePiece(ItemData)', 'Services.GetCommonTreasurePiece(ItemData)')
        text=text.replace('UIUtils.Instantiate<ProfessionUseItemSliderGroup>(mItemTemplate, mItemSlot)', 'Services.InstantiateMaterialRow(mItemTemplate,mItemSlot)')
        text=text.replace('BroadcastManager.TrySystemMsg(LanguageManager.Instance.GetText(', 'Services.SystemMessage(LanguageManager.Instance.GetText(')
    elif name=='ItemIconTreasurePiece':
        text=text.replace('AwardSourceData sourceData','object sourceData')
        old=original_method(text,'private long GetUpgradePieceCount(')
        text=text.replace(old,'private long GetUpgradePieceCount(int pieceClassId)=>Services.GetUpgradePieceCount(pieceClassId);')
        text=text.replace('TreasureHelper.PieceCanMerge(myInfo.ClassId, MeManager.MainPlayer)','Services.PieceCanMerge(myInfo.ClassId)')
    elif name=='TreasureCanMergeRedDotItem':
        text=text.replace(': RedDotItemBase',': ProfessionRedDotItemBase')
        text=text.replace('MeManager.MainPlayer.OnItemCountUpdate','Services.Icons.OnItemCountUpdate').replace('MeManager.MainPlayer.OnTreasureAdd','Services.OnTreasureAdd')
        text=text.replace('MeManager.MainPlayer != null','services != null').replace('AwardSourceData sourceData','object sourceData')
        text=text.replace('Formula.GetTreasureIdByPieceId(', 'Services.GetTreasureIdByPieceId(')
        text=text.replace('TreasureHelper.CanMergeTreasure(mTreasureClassId, MeManager.MainPlayer)','Services.CanMergeTreasure(mTreasureClassId)')
        old=original_method(text,'public void Init(')
        text=text.replace(old,'''public void Init(int itemId) {
            mItemId=itemId;
            if(Services.GetItemType(itemId)!=306)UnityEngine.Debug.LogError("TreasureCanMergeRedDotItem 物品："+itemId+" 不是 TreasurePiece");
            else {mTreasureClassId=Services.GetTreasureIdByPieceId(itemId);mMergeMaterialDict=Services.GetMergeMaterials(mTreasureClassId);}
        }''')
    text=text.replace('mItemData.GetItemParamTreasure()', 'Services.Icons.GetState(mItemData)')
    if any(s in text for s in ('MeManager','Parsers.','Formula.','ServiceProxy','WindowManager','[Inject]','GLogger','Log.Warn','Log.Error','PersistentState','Publisher','ResourceLoader.','LanguageUtils.','BattleFormula','BroadcastManager','Disposable.')):
        raise ValueError('Bind complete original promotion dependency: '+name+'\n'+text)
    header='''using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
'''
    return ('// Complete original promotion controller; explicit saved-player, parser and window ports.\n'+header+
        'namespace Zjcs.Official.TreasurePromoteUI {\nusing Object=UnityEngine.Object;\n'+text+'\n}\n').encode()

def prepare(check=False):
    source=OfficialSource();code=source.prepared_path/'official-code';proof=source.prepared_path/'code-manifest.json'
    inputs=[Path(__file__),proof];outputs={}
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=code/'managed/decoded'/name
        if sha(path.read_bytes())!=json.loads(proof.read_bytes())['assemblies'][name]['decoded_sha256']:raise ValueError('Original assembly differs')
        inputs.append(path)
    for name in NAMES:
        path=code/'client-source'/(name+'.cs');inputs.append(path);outputs[DEST/(name+'.cs')]=adapt(name,path.read_text('utf-8-sig'))
    for name,target in COMMON.items():
        path=code/'client-source'/(name+'.cs');inputs.append(path)
        outputs[DEST.parent/'ProfessionUI'/(target+'.cs')]=common(name,path.read_text('utf-8-sig'))
    assembly=DEST/'Zjcs.Official.TreasurePromoteUI.asmdef'
    outputs[assembly]=(json.dumps(dict(name='Zjcs.Official.TreasurePromoteUI',references=['Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI',
        'Zjcs.Official.World.NativeLabelUI','Zjcs.Official.TreasureCultivation','Zjcs.Official.TreasureUI','Zjcs.Official.TreasureIconUI',
        'Zjcs.Official.TreasureSlotsUI','Zjcs.Official.TreasureDetailUI','Unity.ugui']),indent=2)+'\n').encode()
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-promote:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer='AssemblyDefinitionImporter' if path.suffix=='.asmdef' else 'MonoImporter'
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+importer+':\n  externalObjects: {}\n').encode()
    outputs[Path(str(DEST)+'.meta')]=('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:treasure-promote:folder').hexdigest()+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    for relative in ('Official/TreasurePromoteUI/ITreasurePromoteServices.cs','Official/ProfessionUI/IProfessionMaterialServices.cs',
        'Offline/TreasureUI/OfflineTreasurePromoteServices.cs','Offline/TreasureUI/OfflineTreasurePromoteWindowView.cs',
        'Offline/TreasureUI/OfflineTreasureMaterialServices.cs','Offline/TreasureUI/OfflineTreasureUseItemBindings.cs',
        'Official/HomeHudUI/OfficialHomeTopBarAssetCatalog.cs',
        'Official/HomeCurrencyActionsUI/HomeCurrencyActionFactory.cs','Offline/HomeUI/OfflineHomeCurrencyPlayer.cs',
        'Offline/TreasureUI/OfflineTreasureIconServices.cs','Offline/HomeCurrencyUI/OfflineHomeCurrencyWindows.cs'):
        path=ROOT/'unity/Assets/Game/Runtime'/relative;inputs.extend((path,Path(str(path)+'.meta')))
    contracts={}
    for relative,signatures in (
        ('common-source/TreasureHelper.cs',('public static bool IsTreasureMaxLevel(','public static bool IsTreasureCanUpgradeUseCommonMaterial(')),
        ('common-source/Common/Player.cs',('public IEnumerable<(PlayerItemDataWrap It, long Count)> GetCommonTreasurePiece(',)),
        ('common-source/Common/PlayerItemParamTreasureWrap.cs',('public int GetHitCount(',)),
        ('common-source/Common/FormulaHandler.cs',('public bool CheckTreasureSlotLevelHasBless(',)),
        ('common-source/Common/TreasureUpgradeInfoParser.cs',('public long GetStarUpCommonPiece(','public long GetNeedPieceAmountFromTreasurePiece(')),
        ('common-source/Common/SystemLevelLimitInfoParser.cs',('public CultureTextArg GetSystemLimitDesc(',)),
        ('common-source/Common/TreasureLevelInfoParser.cs',('public Dictionary<int, long> GetLevelUpMaterials(',)),
        ('common-source/Common/LanguageUtils.cs',('public static string GetTreasureQualityBgPath(','public static string GetSeasonNameByRank(','public static string AstrologicalSeasonNameKey(')),
        ('common-source/GlobalColors.cs',('public string WrapColor(',)),
        ('client-source/ItemIconManager.cs',('private ItemIcon GetIconGameObject(','public void ResetIconGo(',)),
        ('client-source/PlayerItemUIHandlerFactory.cs',('public static IPlayerItemUIHandler Create(',)),
        ('client-source/IconConfigPool.cs',('public static GameObject GetGoPrefab(','public string GetPoolKey(','public void RecycleIcon(')),
    ):
        path=code/relative;inputs.append(path);text=path.read_text('utf-8-sig')
        for signature in signatures:contracts[relative+':'+signature]=sha(original_method(text,signature).encode())
    outputs={path:b'\n'.join(line.rstrip() for line in raw.split(b'\n')) for path,raw in outputs.items()}
    record=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},contracts=contracts,
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        adaptations=['Keep complete original promotion tabs, item/page binding, preview rows, costs, common-material selector, toggles, effect ordering and callbacks.',
                     'Use the existing item/star/slot services and explicit local transactions, material pools, parser and window owners.',
                     'Fresh window instances may restore a toggle before the hidden promotion page has item data; its success callback skips only that uninitialized visual refresh, and original Show rebuilds the page.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {};outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    work=None
    for path,raw in outputs.items():
        if path.exists() and path.read_bytes()==raw:continue
        if check:raise ValueError('Original promotion output differs: '+str(path))
        if path!=RECEIPT and path.exists() and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):raise ValueError('Preserve another promotion edit: '+str(path))
        if work is None:
            work=ROOT/'output/validation'/('treasure-promote-write-'+uuid.uuid4().hex);work.mkdir(parents=True)
            (work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
        path.parent.mkdir(parents=True,exist_ok=True);temp=work/'replacement';temp.write_bytes(raw);os.replace(temp,path)
    print('Verified complete original treasure promotion controls',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');prepare(parser.parse_args().check)
