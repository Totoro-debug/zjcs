using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasurePromoteUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using S=Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.TreasureUI
{
    // Original player/parser queries over the same durable role used by the main city.
    public sealed class OfflineTreasurePromoteServices:ITreasurePromoteServices,IDisposable
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureTipService tips;
        private readonly OfflineTreasureSlotService slots;
        private readonly OfflineTreasureCatalog rules;
        private readonly OfflineTreasureCostRules costs;
        private readonly OfflineTreasureDetailConfiguration configuration;
        private readonly OfflineHomeCurrencyWindows windows;
        private readonly Func<int> season;
        private readonly Action<int> error;
        private readonly Action<ProgressionResult> settingFailed;
        private readonly Action<TreasureCultivationResult> failed;
        private readonly Action<Action<TreasureAwakeMaterialPanel>> popup;
        private readonly Func<GameObject,Transform,ProfessionUseItemSliderGroup> row;
        private readonly Action<string> message;
        private readonly Action<Action> dispatchResponse;
        private bool disposed;
        private readonly OfflineTreasureIconServices icons;
        private readonly HashSet<int> knownItems;
        public event Action<IProfessionItemData> OnTreasureAdd;
        public ITreasureIconServices Icons=>icons;
        public ITreasurePropertyTables Tables { get; }
        public ITreasurePropertyPlayer Player { get; }
        public TreasurePropertyQueries Properties { get; }
        public TreasurePropertyCalculator Calculator { get; }
        public IProfessionImageResourceLoader Images { get; }
        public IProfessionMaterialServices Materials { get; }
        public IProfessionUpgradeBindings UseItemBindings { get; }
        public int SubRank=>Player.SubRank;
        public int SeasonId=>season();
        public int MaxBaseLevel=>Tables.GetMaxTreasureBaseLevel(SubRank);
        public int MaxBlessLevel { get {var p=LevelPlayer;return p.Tables.Formula.GetMaxTreasureBlessLevel(p.SubRank,p.MyPlanesRank);} }
        public int MoneyId=>UseItemBindings.MoneyId;
        public int CommonPieceClassId=>-2147483555;
        public bool AutoMergeWhenLevelUp=>tips.AutoMergeWhenLevelUp;
        public bool AwakeAutoMergeEnable=>session.Save.Player.CharacterProperties.TreasureSlots.AwakeAutoMergeEnable;
        public bool UpgradeUseCommonMaterailEnable=>session.Save.Player.CharacterProperties.TreasureSlots.UpgradeUseCommonMaterailEnable;
        private S.Player LevelPlayer=>slots.CurrentLevelSnapshot(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public event Action<GameObject> OnWindowClosing { add=>windows.WindowClosing+=value;remove=>windows.WindowClosing-=value; }
        public OfflineTreasurePromoteServices(OfflineMainlineProgressSession session,OfflineTreasureTipService tips,
            OfflineTreasureSlotService slots,OfflineTreasureCatalog rules,OfflineTreasureDetailConfiguration configuration,
            ITreasurePropertyTables tables,ITreasurePropertyPlayer player,OfflineTreasureIconServices icons,
            IProfessionImageResourceLoader images,IProfessionMaterialServices materials,IProfessionUpgradeBindings bindings,
            OfflineHomeCurrencyWindows windows,Func<int> season,Action<int> error,Action<ProgressionResult> settingFailed,
            Action<TreasureCultivationResult> failed,Action<Action<TreasureAwakeMaterialPanel>> popup,
            Func<GameObject,Transform,ProfessionUseItemSliderGroup> row,Action<string> message,Action<Action> dispatchResponse)
        {
            this.session=session??throw new ArgumentNullException(nameof(session));this.tips=tips??throw new ArgumentNullException(nameof(tips));
            this.slots=slots??throw new ArgumentNullException(nameof(slots));this.rules=rules??throw new ArgumentNullException(nameof(rules));
            this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));Tables=tables??throw new ArgumentNullException(nameof(tables));
            Player=player??throw new ArgumentNullException(nameof(player));this.icons=icons??throw new ArgumentNullException(nameof(icons));
            Images=images??throw new ArgumentNullException(nameof(images));Materials=materials??throw new ArgumentNullException(nameof(materials));
            UseItemBindings=bindings??throw new ArgumentNullException(nameof(bindings));this.windows=windows??throw new ArgumentNullException(nameof(windows));
            this.season=season??throw new ArgumentNullException(nameof(season));this.error=error??throw new ArgumentNullException(nameof(error));
            this.settingFailed=settingFailed??throw new ArgumentNullException(nameof(settingFailed));this.failed=failed??throw new ArgumentNullException(nameof(failed));
            this.popup=popup??throw new ArgumentNullException(nameof(popup));this.row=row??throw new ArgumentNullException(nameof(row));
            this.message=message??throw new ArgumentNullException(nameof(message));
            this.dispatchResponse=dispatchResponse??throw new ArgumentNullException(nameof(dispatchResponse));
            Properties=new TreasurePropertyQueries(tables,season);Calculator=new TreasurePropertyCalculator(tables);
            costs=new OfflineTreasureCostRules(rules,id=>configuration.GetItem(id).Quality);
            knownItems=new HashSet<int>(session.Save.Player.Inventory.Select(item=>item.ItemId));
            icons.OnItemCountUpdate+=ObserveItem;
        }
        private void ObserveItem(IProfessionItemData item,long previous,object source)
        {
            bool present=session.Save.Player.Inventory.Any(row=>row.ItemId==item.Id);
            bool added=present&&!knownItems.Contains(item.Id);
            if(present)knownItems.Add(item.Id);else knownItems.Remove(item.Id);
            if(added&&configuration.GetItem(item.Id)?.ItemType==305)OnTreasureAdd?.Invoke(item);
        }
        public void Dispose(){disposed=true;icons.OnItemCountUpdate-=ObserveItem;OnTreasureAdd=null;}
        public int GetItemType(int id)=>configuration.GetItem(id).ItemType;
        public int GetTreasureIdByPieceId(int id)=>tips.Cultivation.GetTreasureIdByPieceId(id);
        public int CanMergeTreasure(int id)=>tips.Cultivation.CanMergeTreasure(id);
        public Dictionary<int,long> GetMergeMaterials(int id)=>costs.GetMergeMaterials(id);
        public string QualityBackground(Quality quality)=>"Assets/Image/UI/Common/ImgTreasureQuality_"+quality;
        public bool LimitCommonMaterial(int id)=>tips.Cultivation.GetTreasure(id).LimitUseCommonMaterial;
        public bool CanUseCommonMaterial(int id)=>!LimitCommonMaterial(id)&&UpgradeUseCommonMaterailEnable;
        public long GetItemAmount(int id)=>tips.Cultivation.GetItemAmount(id);
        public int GetTreasureFinalLevel(int id)=>Player.GetTreasureFinalLevel(id);
        public int GetTreasureBaseLevel(int id)=>LevelPlayer.GetTreasureBaseLevel(id);
        public int GetTreasureInSlotIndex(int id)=>LevelPlayer.GetTreasureInSlotIndex(id);
        public bool IsTreasureMaxLevel(int id)
        {
            if(Tables.GetTreasure(id)==null){Debug.LogError("TreasureInfoParser 配置找不到 treasureClassId : "+id);return false;}
            return GetTreasureFinalLevel(id)>=rules.Settings.TreasureMaxLevel;
        }
        public int GetHitCount(TreasureInfo info,int level)
        {
            int count=Player.GetTreasureState(info.ClassId).HitCount;
            int max=info.Effect.GetLevelMaxHitCount(level);return count>max?max:count;
        }
        public bool HasBless(int finalLevel)=>MaxBlessLevel>0&&finalLevel>MaxBaseLevel;
        public Dictionary<int,long> GetLevelUpMaterials(int level)
        {
            var p=LevelPlayer;
            return !p.CanTresureSlotBlessLevelUp()?p.Tables.GetLevelCost(level)?.Materials:
                p.Tables.GetBlessCost(p.Rank,level-p.Tables.Limits.GetTreasureBaseLevelLimit(p.SubRank))?.LevelCostItems;
        }
        public S.TreasureLevelInfo GetLevelInfo(int level)=>LevelPlayer.Tables.GetLevelCost(level);
        public S.TreasureBlessInfo GetBlessInfo(int level){var p=LevelPlayer;return p.Tables.GetBlessCost(p.Rank,level);}
        public bool IsItemSourceFromMerge(int id)=>LevelPlayer.IsItemSourceFromMerge(id);
        public long CalcCanMergeAmount(int id,bool recursion){var p=LevelPlayer;return p.Tables.CalcCanMergeAmount(p,id,recursion);}
        public (PlayerUseItemContent.UseItemState,string) GetLevelUseItemState(IProfessionItemData item,bool autoMerge)
        {
            var p=LevelPlayer;var slot=p.GetTreasureSlotDataAndIndex(item.Id,out int index);
            string desc;
            if(slot==null)desc="ui_treasure_share_level_up_limit2";
            else switch(S.TreasureHelper.CanTreasureSlotLevelUp(p,(S.ElementType)icons.GetElement(item.Id),index,autoMerge))
            {
                case 53302:desc=new CultureTextArg("function_limit_subrank",new CultureTextArg("SubRank."+(p.SubRank+1))).ToString();break;
                case 53312:desc="ui_treasure_share_level_up_limit1";break;
                case 53313:desc=new CultureTextArg("function_limit_season",SeasonName(p,p.MyPlanesRank+1)).ToString();break;
                case 53314:
                    int code=p.Tables.Limits.GetMinSystemLimitInfo(slot.BaseLevel+slot.BlessLevel+1,p.SubRank,p.Level,p.MyPlanesRank,
                        info=>info.TreasureLevel+info.TreasureBlessLevel,out var selected);
                    desc=selected==null?new CultureTextArg("function_limit_season",SeasonName(p,p.MyPlanesRank+1)).ToString():
                        code==61804?new CultureTextArg("function_limit_season",SeasonName(p,S.EnumExt.ToRank(selected.SubRank))).ToString():
                        code==61802?new CultureTextArg("function_limit_subrank",new CultureTextArg("SubRank."+selected.SubRank)).ToString():
                        new CultureTextArg("treasure_limit_player_level",selected.PlayerLevel+selected.PlayerBlessLevel).ToString();break;
                case 53318:
                    p.Tables.Gaps.TryGetSystemLevelGapInfo(S.DevelopType.Treasure,slot.BaseLevel+slot.BlessLevel+1,p.ServerRecordSeason,out int limit);
                    desc=new CultureTextArg("ui_treasure_limit_gap_level",limit).ToString();break;
                default:return (PlayerUseItemContent.UseItemState.Normal,string.Empty);
            }
            return (PlayerUseItemContent.UseItemState.Limit,desc);
        }
        private static string SeasonName(S.Player p,S.Rank rank)=>LanguageManager.Instance.GetText("astrological_season_"+p.Tables.GetSeasonByRank(rank)+"_name");
        public int CanSlotLevelUp(S.ElementType element,int index,bool autoMerge)=>S.TreasureHelper.CanTreasureSlotLevelUp(LevelPlayer,element,index,autoMerge);
        public void LevelUp(IProfessionItemData item,bool autoMerge,Action completed)
        {
            var result=slots.UpgradeTreasureSlot(icons.GetElement(item.Id),GetTreasureInSlotIndex(item.Id),autoMerge,
                "treasure-promote-level-"+Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if(result.IsApplied)completed();else failed(result);
        }
        public int GetUpgradeMaterials(int id,bool hasEmpty,bool useCommon,out Dictionary<int,long> materials)
            =>tips.Cultivation.GetTreasureNextLevelUpgradeMaterial(id,hasEmpty,useCommon,out materials);
        public int CanUpgrade(int id,Dictionary<int,long> materials,bool autoMerge)=>tips.Cultivation.CanTreasureUpgrade(id,materials,autoMerge);
        public void Upgrade(int id,bool useCommon,bool autoMerge,Action completed)
        {
            var result=tips.Cultivation.UpgradeTreasure(id,useCommon,autoMerge,"treasure-promote-star-"+Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if(result.IsApplied)completed();else failed(result);
        }
        public IEnumerable<(IProfessionItemData It,long Count)> GetCommonTreasurePiece(IProfessionItemData item)
        {
            var quality=item.GetQuality();int star=icons.GetState(item).Star;
            costs.GetUpgradeMaterials(item.Id,star,star+1); // Original discarded GetStarUpPiece parser query.
            return session.Save.Player.Inventory.Where(stack=>configuration.GetItem(stack.ItemId)?.ItemType==305)
                .Select(stack=>icons.GetItemById(stack.ItemId)).Where(it=>{
                    if(it.GetQuality()!=quality)return false;
                    _=Tables.GetTreasure(it.Id).StarMax;
                    var piece=icons.GetItemById(tips.Cultivation.GetPieceIdByTreasureId(it.Id));
                    return piece!=null&&piece.Count>ReservedPieces(piece.Id,icons.GetState(it).Star);
                }).Select(it=>{
                    var piece=icons.GetItemById(tips.Cultivation.GetPieceIdByTreasureId(it.Id));
                    return (piece,piece.Count-ReservedPieces(piece.Id,icons.GetState(it).Star));
                });
        }
        private long ReservedPieces(int pieceId,int star)
        {
            int id=tips.Cultivation.GetTreasureIdByPieceId(pieceId);
            return costs.GetUpgradeMaterials(id,star,Tables.GetTreasure(id).StarMax).TryGetValue(pieceId,out long count)?count:0;
        }
        public long GetStarUpCommonPiece(int id,int star)=>costs.GetUpgradeMaterials(id,star,star+1).TryGetValue(CommonPieceClassId,out long count)?count:0;
        public long GetUpgradePieceCount(int pieceId)
        {
            int id=tips.Cultivation.GetTreasureIdByPieceId(pieceId);
            int star=tips.Cultivation.GetItemById(id)?.State.Star??-1;
            return costs.GetUpgradeMaterials(id,star,star+1).TryGetValue(pieceId,out long count)?count:0;
        }
        public bool PieceCanMerge(int pieceId)=>tips.Cultivation.PieceCanMerge(pieceId);
        public void SetAutoMergeWhenLevelUp(bool value)
        {
            var result=tips.SetAutoMergeWhenLevelUp(value,"treasure-promote-auto-"+Guid.NewGuid().ToString("N"));
            if(result.IsApplied||result.Status==ProgressionResultStatus.Unchanged)CompleteSetting(null);else settingFailed(result);
        }
        public void SetAwakeAutoMerge(bool value)=>SaveSetting(next=>next.CharacterProperties.TreasureSlots.AwakeAutoMergeEnable=value,null);
        public void SetUseCommonMaterial(bool value,Action completed)=>SaveSetting(next=>next.CharacterProperties.TreasureSlots.UpgradeUseCommonMaterailEnable=value,completed);
        private void SaveSetting(Action<OfflinePlayerProgress> change,Action completed)
        {
            ProgressionResult result;
            try{session.Reload();result=session.ApplyInventoryTransaction("treasure-promote-setting-"+Guid.NewGuid().ToString("N"),next=>change(next.Player));}
            catch(IOException ex){settingFailed(new ProgressionResult{Status=ProgressionResultStatus.InvalidState,Message=ex.Message});return;}
            catch(UnauthorizedAccessException ex){settingFailed(new ProgressionResult{Status=ProgressionResultStatus.InvalidState,Message=ex.Message});return;}
            if(!result.IsApplied&&result.Status!=ProgressionResultStatus.Unchanged){settingFailed(result);return;}
            CompleteSetting(completed);
        }
        private void CompleteSetting(Action completed)=>dispatchResponse(()=>{if(disposed)return;completed?.Invoke();icons.PublishSettings();});
        public void PopupAwakeMaterials(Action<TreasureAwakeMaterialPanel> initialize)=>popup(initialize);
        public ProfessionUseItemSliderGroup InstantiateMaterialRow(GameObject prefab,Transform parent)=>row(prefab,parent);
        public void SystemMessage(string text)=>message(text);
        public void ShowError(int code)=>error(code);
    }
}
