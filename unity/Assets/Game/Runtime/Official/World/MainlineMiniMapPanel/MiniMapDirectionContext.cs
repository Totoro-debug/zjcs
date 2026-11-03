using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapDirectionGround { Vector3 GridToPos(Pos2d position); }
    public abstract class MiniMapDirectionContext
    {
        public abstract MiniMapFullScreenContext FullScreen { get; }
        public abstract bool HasMapRoot { get; }
        public abstract bool HasMainPlayer { get; }
        public abstract IMiniMapCharacterManager Characters { get; }
        public abstract IMiniMapDirectionGround Ground { get; }
        public abstract event Action OnInitialized;
        public abstract event Action<MiniMapDirectionPlayerItemData,long,object> OnItemCountUpdate;
        public abstract Vector3 GetWorldPosition(IMiniMapCharacterController controller);
        public abstract bool TryGetItemGuiderData(out MiniMapDirectionData data);
        public abstract MiniMapDirectionItemInfo GetItemInfo(int item);
        public abstract MiniMapRegionInfo GetRegionInfoByDropItemId(int item);
        public abstract void DestroyChildren(Transform slot);
        public abstract void CreateItemIconWithoutText(int item, Transform slot);
    }
    public class MiniMapDirectionData { public int MapClassId, Index; public List<int> AreaList; }
    public sealed class MiniMapDirectionItemData : MiniMapDirectionData { public int ItemId; public long RequireCount; }
    public sealed class MiniMapDirectionItemInfo { public int ClassId, AgioItemId; public MiniMapDirectionItemType ItemType; public string Icon; }
    public sealed class MiniMapDirectionPlayerItemData { public MiniMapDirectionItemInfo Info; }
}
