using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropertyInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using SlotRank=Zjcs.Official.TreasureSlots.SubRank;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureDetailServices:ITreasureDetailServices,ITreasureDescriptionServices
    {
        private readonly OfflineTreasureService cultivation;
        private readonly OfflineTreasureSlotService slots;
        private readonly OfflineTreasureCatalog rules;
        private readonly OfflineTreasurePropertyCatalog tables;
        private readonly OfflineTreasureDetailConfiguration configuration;
        private readonly Action<GameObject> close;
        private readonly Action<UIBase,IProfessionItemData,Action> promote;
        private readonly Action<int> error;
        private readonly Action<TreasureCultivationResult> failed;
        private readonly Func<int> season,rank;
        private readonly Func<int,string> seasonDescription;
        private readonly ITreasureSuitViewServices suits;
        public ITreasurePropertyTables Tables=>tables;
        public ITreasurePropertyPlayer Player { get; }
        public TreasurePropertyQueries Properties { get; }
        public TreasureDescriptionQuery Descriptions { get; }
        public ITreasureIconServices Icons { get; }
        public int SeasonId=>season();
        public int PlanesRank=>rank();
        public OfflineTreasureDetailServices(OfflineTreasurePropertyCatalog tables,ITreasurePropertyPlayer player,
            ITreasureIconServices icons,OfflineTreasureService cultivation,OfflineTreasureSlotService slots,
            OfflineTreasureCatalog rules,OfflineTreasureDetailConfiguration configuration,ITreasureSuitViewServices suits,
            Func<int> season,Func<int> rank,Func<int,string> seasonDescription,
            Action<GameObject> close,Action<UIBase,IProfessionItemData,Action> promote,
            Action<int> error,Action<TreasureCultivationResult> failed)
        {
            this.tables=tables??throw new ArgumentNullException(nameof(tables));Player=player??throw new ArgumentNullException(nameof(player));
            Icons=icons??throw new ArgumentNullException(nameof(icons));this.cultivation=cultivation??throw new ArgumentNullException(nameof(cultivation));
            this.slots=slots??throw new ArgumentNullException(nameof(slots));this.rules=rules??throw new ArgumentNullException(nameof(rules));
            this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));this.suits=suits??throw new ArgumentNullException(nameof(suits));
            this.season=season??throw new ArgumentNullException(nameof(season));this.rank=rank??throw new ArgumentNullException(nameof(rank));
            this.seasonDescription=seasonDescription??throw new ArgumentNullException(nameof(seasonDescription));
            this.close=close??throw new ArgumentNullException(nameof(close));this.promote=promote??throw new ArgumentNullException(nameof(promote));
            this.error=error??throw new ArgumentNullException(nameof(error));this.failed=failed??throw new ArgumentNullException(nameof(failed));
            Properties=new TreasurePropertyQueries(tables,season);Descriptions=new TreasureDescriptionQuery(this);
        }
        public IProfessionItemData GetItemById(int id)=>Icons.GetItemById(id);
        public TreasureState GetState(IProfessionItemData item)=>Icons.GetState(item);
        public int GetRank(int subRank)=>(int)Zjcs.Official.TreasureSlots.EnumExt.ToRank((SlotRank)subRank);
        public PropertyInfo GetTreasure(int id)=>tables.GetTreasure(id);
        public TreasureSuitInfo GetSuitByTreasure(int id)=>tables.Suits.FirstOrDefault(row=>row.Treasures.Contains(id));
        public Quality GetQuality(int id)=>(Quality)configuration.GetItem(id).Quality;
        public int GetElement(int id)=>configuration.GetItem(id).ElementType;
        public HashSet<int> GetNpcByTreasure(int id)=>configuration.GetNpcByTreasure(id);
        public long GetItemAmount(int id)=>cultivation.GetItemAmount(id);
        public int GetPieceIdByTreasureId(int id)=>cultivation.GetPieceIdByTreasureId(id);
        public int GetTreasureInSlotIndex(int id)=>slots.Snapshot().GetTreasureInSlotIndex(id);
        public bool IsTreasureMaxLevel(int id)
        {
            if(tables.GetTreasure(id)==null){Debug.LogError("TreasureInfoParser 配置找不到 treasureClassId : "+id);return false;}
            return Player.GetTreasureFinalLevel(id)>=rules.Settings.TreasureMaxLevel;
        }
        public IReadOnlyDictionary<int,long> ActivationMaterials(Quality quality,int star)
            =>rules.GetUpgradeRule((int)quality,star).Materials.ToDictionary(row=>row.ItemId,row=>row.Count);
        public int CanActivateTreasure(int id)=>cultivation.CanActivateTreasure(id);
        public bool Activate(IProfessionItemData item)
        {
            var result=cultivation.ActivateTreasure(item.Id,"treasure-detail-activate-"+Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if(result.IsApplied)return true;failed(result);return false;
        }
        public void Close(GameObject window)=>close(window);
        public void PopupPromote(UIBase owner,IProfessionItemData item,Action onComplete)=>promote(owner,item,onComplete);
        public void ShowError(int code)=>error(code);
        public string Text(string key)=>configuration.TextValue(key);
        public string TextFormatted(string key,params object[] args)=>string.Format(Text(key),args);
        public string GetSeasonLevelUnEffectDesc(int rank)=>seasonDescription(rank);
        public object Compact(long value)=>CultureNumberArg.Compact(value);
        public object PropertyValue(int type,long value,bool includeUnit,bool includePropDesc,long targetValue,bool needSpaceBeforeValue)
            =>new ProfessionPropertyLanguage.PropValue((ProfessionPropertyType)type,value,includeUnit,includePropDesc,targetValue).SetNeedSpaceBeforeValue(needSpaceBeforeValue);
        public bool HasItem(int id,long count)=>Icons.HasItem(id,count);
        public Transform InstantiateSuitMember(Transform prefab,Transform parent)=>suits.InstantiateSuitMember(prefab,parent);
        public void PopupTreasure(int id,bool canOperation)=>suits.PopupTreasure(id,canOperation);
        public void PopupPiece(int id,bool showButtons)=>suits.PopupPiece(id,showButtons);
    }
}
