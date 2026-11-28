using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropertyTreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;

namespace Zjcs.Official.TreasureDetailUI
{
    public interface ITreasurePropertyViewServices
    {
        ITreasurePropertyTables Tables { get; }
        ITreasurePropertyPlayer Player { get; }
        TreasurePropertyQueries Properties { get; }
        TreasureDescriptionQuery Descriptions { get; }
        IProfessionItemData GetItemById(int id);
        TreasureState GetState(IProfessionItemData data);
        int GetRank(int subRank);
        HashSet<int> GetNpcByTreasure(int treasureId);
    }
    [Serializable]
    public struct TreasureViewPrefabReference
    {
        public string mAssetGuid;
        public string AssetGuid=>mAssetGuid;
    }
    public sealed class TreasureNpcViewInfo
    {
        public int ClassId,TreasureClassId;
        public string Icon,PartnerAdditionBg,NameKey;
    }
    public interface ITreasureNpcEffectServices
    {
        IProfessionImageResourceLoader Resources { get; }
        LanguageType Language { get; }
        TreasureNpcViewInfo GetNpc(int id);
        double GetFriendshipPercent(int treasureId);
        GameObject Instantiate(GameObject prefab,Transform parent);
    }
    public interface ITreasureItemShareServices
    {
        bool CanShare { get; }
        bool IsLocalItem(IProfessionItemData data);
        void ShareItem(IProfessionItemData data,int snapshotType);
    }
    public interface ITreasureDetailServices:ITreasurePropertyViewServices,ITreasureSuitViewServices
    {
        ITreasureIconServices Icons { get; }
        PropertyTreasureInfo GetTreasure(int id);
        TreasureSuitInfo GetSuitByTreasure(int id);
        Quality GetQuality(int id);
        int GetElement(int id);
        int GetTreasureInSlotIndex(int id);
        long GetItemAmount(int id);
        bool IsTreasureMaxLevel(int id);
        IReadOnlyDictionary<int,long> ActivationMaterials(Quality quality,int star);
        int CanActivateTreasure(int id);
        bool Activate(IProfessionItemData data);
        void PopupPromote(UIBase owner,IProfessionItemData data,Action onComplete);
        void Close(GameObject window);
        void ShowError(int code);
        string Text(string key);
        string TextFormatted(string key,params object[] args);
        int PlanesRank { get; }
        string GetSeasonLevelUnEffectDesc(int rank);
    }
}
