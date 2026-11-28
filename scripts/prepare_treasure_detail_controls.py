"""Migrate the complete original treasure detail controller and its property/NPC presenters."""
import argparse
import hashlib
import json
import re
import os
import uuid
from pathlib import Path
from official_source import ROOT,OfficialSource
from prepare_pet_score_chest_preview import original_method

DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasureDetailUI'
RECEIPT=ROOT/'assets/reference/treasure-detail-controls-source-manifest.json'
HEADER='''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.HomePlayerInfoUI;
using PlayerCostMoneyContent=Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent;
using IntroductionPopBtn=Zjcs.Official.HomePlayerInfoUI.HomeIntroductionPopBtn;
using TreasureState=Zjcs.Official.TreasureCultivation.TreasureState;
using PropType=System.Int32;
using SubRank=System.Int32;
using Rank=System.Int32;
using Object=UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {
'''


def sha(raw):return hashlib.sha256(raw).hexdigest()


def adapt(text):
    text=re.sub(r'^using [^\n]+\n','',text,flags=re.M)
    text=re.sub(r'^\s*//IL_[^\n]*\n','',text,flags=re.M)
    text=re.sub(r'^\s*\[(?:UIWindow|Inject)\([^\n]*\)\]\n','',text,flags=re.M)
    text=text.replace('internal class ','public class ')
    text=text.replace('MultiLanguageText','WorldMultiLanguageText').replace('StateSwitcher<','WorldStateSwitcher<')
    text=re.sub(r'\bPlayerPropItem\b','ProfessionPlayerPropItem',text)
    text=re.sub(r'\bBlessPropBtn\b','ProfessionBlessPropBtn',text)
    text=text.replace('PlayerItemDataWrap','IProfessionItemData').replace('PlayerItemParamTreasureWrap','TreasureState')
    return text


def prepare(check=False):
    source=OfficialSource();code=source.prepared_path/'official-code';consumed=[]
    def read(name,common=False):
        path=code/('common-source/Common' if common else 'client-source')/(name+'.cs');consumed.append(path)
        return path.read_text('utf-8-sig')
    element=adapt(read('ElementPropItem'))
    element=element.replace('PropCfgInfo propCfgInfo = Parsers.Instance.PropCfgInfoParser.Get(propType);','ProfessionPropertyDefinition propCfgInfo = Bindings.GetProperty(propType);')
    element=element.replace('Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(propCfgInfo.SmallIcon), ref mDisposableBag)',
        'mDisposableBag.Add(Bindings.Resources.LoadAsset<Sprite>("Assets/Image/"+propCfgInfo.SmallIcon))')
    # This override shares the existing typed property row and its original resource bag.
    element=element.replace('Init(PropType propType,','Init(ProfessionPropertyType propType,')
    props=adapt(read('TreasureProps'))
    props=props.replace('public class TreasureProps\n{','''public class TreasureProps
{
    private ITreasurePropertyViewServices Services;
    public void Configure(ITreasurePropertyViewServices services) => Services=services??throw new ArgumentNullException(nameof(services));
''')
    props=props.replace('new TreasureDummyPropProvider(classId)','new TreasureDummyPropProvider(Services.Tables,Services.Player,classId)')
    props=props.replace('Parsers.GameSettings.TreasureSlotPropAddRate','Services.Tables.TreasureSlotPropAddRate')
    props=props.replace('MeManager.MainPlayer.GetItemById(TreasureInfo.ClassId)','Services.GetItemById(TreasureInfo.ClassId)')
    props=props.replace('itemById.GetItemParamTreasure().Active','Services.GetState(itemById).Active')
    props=props.replace('subRank.ToRank()','Services.GetRank(subRank)')
    props=props.replace('TreasureCalcPropData.CreateCalcPropDataByBaseLevel(baseLevel)','TreasureCalcPropData.CreateCalcPropDataByBaseLevel(Services.Tables,baseLevel)')
    props=props.replace('TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(num, baseLevel)','TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(Services.Tables,num,baseLevel)')
    for method in ('GetElementPropInfo','GetEffectPropInfo'):
        props=props.replace('TreasureInfo.'+method+'(MeManager.MainPlayer,','Services.Properties.'+method+'(TreasureInfo,Services.Player,')
    props=props.replace('TreasureInfo.Effect.GenerateDesc(TreasureInfo,','Services.Descriptions.GenerateDesc(TreasureInfo.Effect,TreasureInfo,')
    props=props.replace('Parsers.Instance.NpcInfoParser.GetNpcByTreasure(TreasureInfo.ClassId)','Services.GetNpcByTreasure(TreasureInfo.ClassId)')
    props=props.replace('LanguageUtils.FormatPropString(','TreasureViewLanguage.FormatPropString(')
    props=props.replace('new LanguageUtils.PropValue(','new TreasureViewLanguage.PropValue(')
    props=props.replace('LanguageUtils.PropValue propValue','TreasureViewLanguage.PropValue propValue')
    props=props.replace('new LanguageUtils.ReplaceTextArg(','new TreasureViewLanguage.ReplaceTextArg(')
    props=props.replace('LanguageUtils.GetEnumKey(effectPropInfo.Item1)','TreasureViewLanguage.GetPropertyNameKey(effectPropInfo.Item1)')
    props=props.replace('.Init(elementPropInfo.Item1,','.Init((ProfessionPropertyType)elementPropInfo.Item1,')
    props=props.replace('.Init(rank, rank, elementPropInfo.Item1,','.Init(rank,rank,(ProfessionPropertyType)elementPropInfo.Item1,')
    props=props.replace('.Init(rank, rank, effectPropInfo.Item1,','.Init(rank,rank,(ProfessionPropertyType)effectPropInfo.Item1,')
    props=props.replace('.Init(rank, rank, effectPropInfo2.Item1,','.Init(rank,rank,(ProfessionPropertyType)effectPropInfo2.Item1,')

    npc=adapt(read('TreasureNpcEffectContent'))
    npc=npc.replace('AssetGuidProxy<GameObject>','TreasureViewPrefabReference')
    npc=re.sub(r'\bDisposableBag\b','ProfessionResourceOperationBag',npc)
    npc=npc.replace('[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }','''private ITreasureNpcEffectServices Services;
    public void Configure(ITreasureNpcEffectServices services) => Services=services??throw new ArgumentNullException(nameof(services));''')
    npc=npc.replace('mDisposableBag = default;','mDisposableBag = new ProfessionResourceOperationBag();')
    npc=npc.replace('mDisposableBag.Dispose();','mDisposableBag?.Dispose();')
    npc=npc.replace('Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(mNpcEffectItemPrefab.AssetGuid), ref mDisposableBag)',
        'mDisposableBag.Add(Services.Resources.LoadAssetByGuid<GameObject>(mNpcEffectItemPrefab.AssetGuid))')
    npc=npc.replace('UIUtils.Instantiate(result, (Transform)(object)mItemParent)','Services.Instantiate(result,mItemParent)')
    npc_item=adapt(read('TreasureNpcEffectItem'))
    npc_item=npc_item.replace('public class TreasureNpcEffectItem : UIBase\n{','''public class TreasureNpcEffectItem : UIBase
{
    private ITreasureNpcEffectServices Services;
    public void Configure(ITreasureNpcEffectServices services) => Services=services??throw new ArgumentNullException(nameof(services));
''')
    npc_item=npc_item.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(mNpcId);','TreasureNpcViewInfo npcInfo = Services.GetNpc(mNpcId);')
    npc_item=npc_item.replace('NpcUtils.GetTreasureToNpcFriendshipPercent(treasureClassId)','Services.GetFriendshipPercent(treasureClassId)')
    npc_item=npc_item.replace('Formula.Instance.ToStandardSignificantPercent(value, LanguageManager.Instance.CurrentLanguage)',
        'ProfessionPropertyFormula.ToStandardSignificantPercent(value,Services.Language)')
    original=read('TreasureDetailDialog');detail=adapt(original)
    detail=detail.replace('public class TreasureDetailDialog : UIBase, IItemDetailDialog\n{','''public class TreasureDetailDialog : UIBase, IItemDetailDialog
{
    private ITreasureDetailServices Services;
    [SerializeField] private TreasureSuitContent mSuit;
    public void Configure(ITreasureDetailServices services)
    {
        Services=services??throw new ArgumentNullException(nameof(services));
        mTreasureProps.Configure(services);
        mSuit.Configure(services);
    }
''')
    begin=detail.index('\tpublic GameObject mySuitRoot;');end=detail.index('\t[Header("操作")]')
    detail=detail[:begin]+detail[end:]
    for signature in ('private void SetSuitInfo(','private void DoRefreshSuitTreasureInfo(',
        'private void PopupSuitDetailPanel(','private void DoRefreshSuitProps('):
        method=original_method(detail,signature);detail=detail.replace(method,'')
    detail=detail.replace('private void RefreshSuitInfo()',
        'private void SetSuitInfo(TreasureSuitInfo info,Dictionary<int,int> stars)=>mSuit.Show(TreasureClassId,mCanOperation,info,stars);\n\n\tprivate void RefreshSuitInfo()')
    detail=detail.replace('mCanOperation = canOperation;','mCanOperation = canOperation;\n\t\t\tmSuit.SetCanOperation(canOperation);')
    detail=detail.replace('protected TreasureState ItemParam;',
        'protected TreasureState ItemParam => mSnapshotTreasure!=null?mSnapshotTreasure.TreasureParam:Services.GetState(mItemData);')
    detail=re.sub(r'^\s*ItemParam = [^\n]+\n','',detail,flags=re.M)
    detail=detail.replace('[Inject]\n\tprivate ITreasureReplaceEventLisener ReplaceListener { get; set; }',
        'private ITreasureReplaceEventLisener ReplaceListener => Services.Icons.ReplaceListener;')
    detail=detail.replace('MeManager.MainPlayer.OnTreasureRefresh','Services.Icons.OnTreasureRefresh')
    detail=detail.replace('MeManager.MainPlayer.OnItemCountUpdate','Services.Icons.OnItemCountUpdate')
    detail=detail.replace('MeManager.MainPlayer.GetTreasureInSlotIndex','Services.GetTreasureInSlotIndex')
    detail=detail.replace('MeManager.MainPlayer.GetItemAmount','Services.GetItemAmount')
    detail=detail.replace('MeManager.MainPlayer.GetItemById','Services.GetItemById')
    detail=detail.replace('MeManager.MainPlayer != null','Services.Icons != null')
    detail=detail.replace('AwardSourceData sourceData','object sourceData')
    detail=detail.replace('WindowManager.Instance.Close(','Services.Close(')
    detail=detail.replace('TreasureInfo.ItemInfo.ElementType','Services.GetElement(TreasureInfo.ClassId)')
    detail=detail.replace('TreasureInfo.ItemInfo.Quality','Services.GetQuality(TreasureInfo.ClassId)')
    detail=detail.replace('Formula.GetPieceIdByTreasureId(','Services.GetPieceIdByTreasureId(')
    detail=detail.replace('Parsers.Instance.TreasureInfoParser.Get(','Services.GetTreasure(')
    detail=detail.replace('new TreasureDummyPropProvider(TreasureClassId)','new TreasureDummyPropProvider(Services.Tables,Services.Player,TreasureClassId)')
    detail=detail.replace('new TreasureSnapshotPropProvider(mSnapshotTreasure)','new TreasureSnapshotPropProvider(Services.Tables,mSnapshotTreasure)')
    detail=re.sub(r'int suitClassIdByTreasure = Parsers.Instance.TreasureSuitInfoParser.GetSuitClassIdByTreasure\(TreasureClassId\);\s*TreasureSuitInfo treasureSuitInfo = Parsers.Instance.TreasureSuitInfoParser.Get\(suitClassIdByTreasure\);',
        'TreasureSuitInfo treasureSuitInfo = Services.GetSuitByTreasure(TreasureClassId);',detail)
    detail=detail.replace('treasureSuitInfo.GetSuitStartInfo(MeManager.MainPlayer, dictionary)','TreasureSuitQueries.GetSuitStartInfo(treasureSuitInfo,Services.Player,dictionary)')
    detail=detail.replace('LanguageUtils.GetItemNameKey(','TreasureSuitLanguage.GetItemNameKey(')
    detail=detail.replace('LanguageManager.Instance.GetTextFormated(','Services.TextFormatted(').replace('LanguageManager.Instance.GetText(','Services.Text(')
    detail=detail.replace('LanguageUtils.GetSeasonLevelUnEffectDesc(PlanesInfoUtils.PlanesRank)','Services.GetSeasonLevelUnEffectDesc(Services.PlanesRank)')
    detail=detail.replace('LanguageUtils.TextKeyArgWrap','HomeInfoTextKey')
    detail=detail.replace('TreasureHelper.IsTreasureMaxLevel(MeManager.MainPlayer, TreasureInfo.ClassId)','Services.IsTreasureMaxLevel(TreasureInfo.ClassId)')
    detail=detail.replace('Dictionary<int, long> materials = Parsers.Instance.TreasureUpgradeInfoParser.Get(mItemData.GetQuality(), ItemParam.Star).Materials;',
        'IReadOnlyDictionary<int,long> materials = Services.ActivationMaterials(mItemData.GetQuality(),ItemParam.Star);')
    detail=detail.replace('TreasureHelper.CanActivateTreasure(TreasureClassId, MeManager.MainPlayer)','Services.CanActivateTreasure(TreasureClassId)')
    detail=detail.replace('BroadcastManager.ShowErrText(num, null, false)','Services.ShowError(num)')
    request=original_method(detail,'ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerTreasureActivate(')
    detail=detail.replace(request+');\n\t\t\tServices.Close(((Component)this).gameObject);',
        'if(Services.Activate(mItemData))Services.Close(((Component)this).gameObject);')
    train=original_method(detail,'private void OnTrainBtnClick(')
    detail=detail.replace(train,'''private void OnTrainBtnClick()
    {
        Services.PopupPromote(this,mItemData,()=>Services.Close(gameObject));
    }''')
    share=adapt(read('ShareItemBtn'))
    share=share.replace('public class ShareItemBtn : MonoBehaviour\n{','''public class ShareItemBtn : MonoBehaviour
{
    private ITreasureItemShareServices Services;
    public void Configure(ITreasureItemShareServices services)=>Services=services??throw new ArgumentNullException(nameof(services));
''')
    share=share.replace('public SnapshotType SnapshotType;','public int SnapshotType;')
    share=share.replace('private IShareItemData mItemData;','private IProfessionItemData mItemData;')
    share=share.replace('itemData != null && itemData.PlayerId == MeManager.MainPlayer.Id',
        'itemData!=null&&Services.IsLocalItem(itemData)&&Services.CanShare')
    share=share.replace('((itemData != null) ? new SharePlayerItemData(itemData.ItemIndex, SnapshotType) : null)','itemData')
    click=original_method(share,'private void OnShareBtnClick(')
    share=share.replace(click,'private void OnShareBtnClick(){Services.ShareItem(mItemData,SnapshotType);}')
    language=read('LanguageUtils',True)
    replace_arg=original_method(language,'public class ReplaceTextArg')
    lang='''public static class TreasureViewLanguage
{
    public static string GetPropertyNameKey(int type)=>"PropType."+(ProfessionPropertyType)type;
    public static string FormatPropString(int type,long value,bool includeUnit=true,bool includePropDesc=true,long targetValue=-1L)
        =>ProfessionPropertyLanguage.FormatPropString((ProfessionPropertyType)type,value,includeUnit,includePropDesc,targetValue);
    public sealed class PropValue:ProfessionPropertyLanguage.PropValue
    {
        public PropValue(int type,long value,bool includeUnit=true,bool includePropDesc=true,long targetValue=-1L)
            :base((ProfessionPropertyType)type,value,includeUnit,includePropDesc,targetValue){}
    }
'''+replace_arg+'\n}\n'
    # Consume the original NPC percentage implementation even though the local table owner supplies it.
    original_method(read('NpcUtils'),'public static double GetTreasureToNpcFriendshipPercent(')
    original_method(read('PlayerPropItem'),'public virtual void Init(PropType propType, long value,')
    detail=re.sub(r'(?m)^[ \t]+$','',detail)
    for name in ('GlobalColors','TextColorConfig','TextColorList'):
        consumed.append(code/'common-source'/(name+'.cs'))
    read('ItemDetailDialog')
    outputs={DEST/(name+'.cs'):(HEADER+text+'\n}\n').encode() for name,text in (
        ('ElementPropItem',element),('TreasureProps',props),('TreasureNpcEffectContent',npc),
        ('TreasureNpcEffectItem',npc_item),('TreasureViewLanguage',lang),('TreasureDetailDialog',detail),('ShareItemBtn',share))}
    replacer_path=code/'common-source/TextReplacer.cs';consumed.append(replacer_path)
    replacer_source=replacer_path.read_text('utf-8-sig')
    replace_text=adapt(original_method(replacer_source,'public string ReplaceText('))
    replace_text=replace_text.replace('ReadOnlySpan<char> val = MemoryExtensions.AsSpan(text, i + 1, j - i - 1);','string val = text.Substring(i + 1, j - i - 1);')
    replace_text=replace_text.replace('((object)val/*cast due to constrained. prefix*/).ToString()','val')
    replacer='''using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Zjcs.Official.World.NativeLabelUI.Framework {
// Original TextReplacer core; dynamic gameplay methods are registered by their owners.
public sealed class WorldTextReplacer:ITextReplacer {
    private readonly Func<string,string> getText;
    private readonly string mReplacePattern="\\\\{(.*?)\\\\}";
    private readonly Dictionary<string,Func<object>> mDynamicMethodDict=new Dictionary<string,Func<object>>();
    public WorldTextReplacer(Func<string,string> getText){this.getText=getText??throw new ArgumentNullException(nameof(getText));}
    private string GetReplacementString(string key,params object[] paras){
        if(mDynamicMethodDict.TryGetValue(key,out var method)){
            object value=method();if(value!=null)return value.ToString();
            Debug.LogError("replace method return null!key:"+key);return null;
        }
        return getText(key);
    }
'''+replace_text+'\n'+'\n'.join(adapt(original_method(replacer_source,signature)) for signature in (
        'private bool ShouldIgnore(string key)','private bool IsAllDigits(string input)',
        'public bool TryMatchReplaceKey(','public void Register(','public void Unregister('))+'\n}}\n'
    outputs[ROOT/'unity/Assets/Game/Runtime/Official/World/NativeLabelUI/Framework/WorldTextReplacer.cs']=replacer.encode()
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-detail:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\nMonoImporter:\n  externalObjects: {}\n').encode()
    inputs=source.verify_code_files(consumed)
    for path in (Path(__file__),ROOT/'scripts/prepare_pet_score_chest_preview.py',
        ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionPlayerPropItem.cs'):
        inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
    runtime=ROOT/'unity/Assets/Game/Runtime'
    bindings=[DEST/'TreasureDetailServices.cs',runtime/'Official/WorldUI/WorldNativeGradientData.cs',runtime/'Official/ItemDetailUI/ItemDetailDialog.cs',
        runtime/'Official/ItemDetailUI/ItemDetailBindings.cs',runtime/'Offline/ProfessionUI/OfflineProfessionPropertyScope.cs',
        runtime/'Offline/HomeCurrencyUI/OfflineHomeCurrencyWindows.cs',
        runtime/'Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
        *sorted((runtime/'Offline/TreasureUI').glob('OfflineTreasureDetail*.cs')),
        runtime/'Offline/TreasureUI/OfflineTreasureItemDetailBindings.cs']
    for path in bindings:
        for consumed in (path,Path(str(path)+'.meta')):inputs[consumed.relative_to(ROOT).as_posix()]=sha(consumed.read_bytes())
    record=dict(manifest=source.version,inputs=inputs,outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        adaptations=['Preserve complete original property presenter, element row, NPC content and item lifecycle.',
            'Use existing property/description queries, typed property formatting, resource bags and explicit local table/window owners.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    write_work=None
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Original detail controls differ: '+str(path))
        else:
            if path!=RECEIPT and path.exists() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another detail-control edit: '+str(path))
            if not path.exists() or path.read_bytes()!=raw:
                if write_work is None:
                    write_work=ROOT/'output/validation'/('treasure-detail-write-'+uuid.uuid4().hex);write_work.mkdir(parents=True)
                    (write_work/'.zjcs-validation.json').write_text(json.dumps(dict(format=1,repository=str(ROOT)))+'\n','utf8')
                replacement=write_work/(sha(path.relative_to(ROOT).as_posix().encode())+'.bin');replacement.write_bytes(raw)
                path.parent.mkdir(parents=True,exist_ok=True);os.replace(replacement,path)
    print('Prepared original detail property and NPC presenters',flush=True)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');prepare(p.parse_args().check)
