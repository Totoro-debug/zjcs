using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Exact original fields consumed by the detail presentation. Rule data and
    // resource selection are supplied from the selected official configuration.
    public sealed class PartnerDetailPresentationInfo
    {
        public int Id, MapGroupId, TravelProfession, PreferenceGiftType;
        public bool IsSpecial, IsShadow;
        public string NameKey, BigBgImage, FigureModelUI;
        public Vector3 LiveOffset, LiveScale;
        public PartnerDetailNpcInfo Properties;
    }
    public sealed class PartnerDetailAnimationInfo
    {
        public string Animation;
        public List<int> TalkIdList;
    }
    public enum PartnerFullBodyImageType { None, Static, Live }
    public interface IPartnerDetailPresentationServices
    {
        PartnerDetailAnimationInfo GetFriendshipAnimation(int npcId, int level);
        PartnerFullBodyImageType GetFullBodyImagePath(int npcId, out string path);
        string GetNpcStartingAnimName(int npcId);
        IProfessionAssetOperation<Sprite> LoadSprite(string path);
        IProfessionAssetOperation<GameObject> LoadPrefab(string path);
        GameObject InstantiateKeepScale(GameObject prefab, Transform parent);
        Component GetSkeletonGraphic(GameObject model);
        void PlayAnimation(Component graphic, string animation, bool loop);
        void DestroyChildren(Transform parent);
        int RandomRange(int minimum, int maximum);
        IProfessionAssetOperation<Sprite> LoadAdventureIcon(int category, int value);
        void PopupAdventureTip(Vector3 position, int mapGroup, int profession, int preference);
    }
    public interface IPartnerTreasureIconServices
    {
        IProfessionItemInfo GetItemInfo(int itemId);
        bool IsTreasureActive(int itemId);
        void SetUIGray(GameObject target, bool gray);
        // Original TreasureDetailDialog.Init(id,0), operations/replacement off,
        // NPC treasure information and activation state visible.
        void PopupTreasureDetail(int itemId);
    }
    public interface IPartnerDetailLiveControllerServices
    {
        int MemorySizeLevel { get; }
        IProfessionAssetOperation<GameObject> LoadPrefab(string path);
        NpcDetailLiveItem Instantiate(GameObject prefab, Transform parent);
    }
    public interface IPartnerDetailLiveItemServices
    {
        PartnerDetailPresentationInfo GetNpcInfo(int npcId);
        IPartnerSequenceGraphic Resolve(Component graphic);
        bool HasSkeletonData(Component graphic);
        void Initialize(Component graphic, bool overwrite);
        bool TryGetLiveImagePath(int npcId, out string path);
        IProfessionAssetOperation<ScriptableObject> LoadSkeleton(string path);
        void SetSkeletonData(Component graphic, ScriptableObject asset);
        void PlayAnimation(Component graphic, string name, bool loop, float mixDuration, bool endResumeLast);
    }
}
