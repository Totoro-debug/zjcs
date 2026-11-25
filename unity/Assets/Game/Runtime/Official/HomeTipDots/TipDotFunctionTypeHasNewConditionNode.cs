// Selected official new-function tip node and map instance; function identifiers retain their integer values.
using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeTipDots
{
    public class MapNodeInstanceInfo : BaseNodeInstanceInfo
    {
        public int MapId;
        public MapNodeInstanceInfo(int mapId) { MapId = mapId; }
        public override string GetInstanceDesc() => $"Mainland_{MapId}";
        public override bool Equals(object obj) => obj is MapNodeInstanceInfo other && MapId == other.MapId;
        public override int GetHashCode() => HashCode.Combine(MapId);
    }

    [Serializable]
    public class TipDotFunctionTypeHasNewConditionNode : TipDotConditionNodeBase
    {
        public List<int> Types;
        public List<int> Arg1List;
        public HomeNewFunctionState Services { get; set; }

        public override void Init()
        {
            base.Init();
            MapNodeInstanceInfo instanceInfo = GetInstanceInfo<MapNodeInstanceInfo>();
            if (instanceInfo != null) Arg1List = new List<int> { instanceInfo.MapId };
            Services.OnNewFunction += OnNewFunction;
            Services.OnRemoveNewFunction += OnRemoveNewFunction;
        }

        public override void Release()
        {
            Services.OnNewFunction -= OnNewFunction;
            Services.OnRemoveNewFunction -= OnRemoveNewFunction;
            base.Release();
        }

        public override bool CheckShowTip()
        {
            bool result = false;
            for (int i = 0; i < Types.Count; i++)
            {
                int arg = Arg1List.Count > i ? Arg1List[i] : 0;
                result = Services.IsNew(Types[i], arg);
                if (result) break;
            }
            return result;
        }

        private void OnNewFunction(int type, int arg1)
        {
            if (NeedRefresh(type, arg1)) SetDirty();
        }

        private void OnRemoveNewFunction(int type, int arg1)
        {
            if (NeedRefresh(type, arg1)) SetDirty();
        }

        private bool NeedRefresh(int type, int arg1)
        {
            for (int i = 0; i < Types.Count; i++)
            {
                if (type == Types[i])
                {
                    int configured = Arg1List.Count > i ? Arg1List[i] : 0;
                    if (configured == 0 || arg1 == configured) return true;
                }
            }
            return false;
        }

        public override TipDotNodeBase Clone()
        {
            var node = new TipDotFunctionTypeHasNewConditionNode();
            node.Fill(this);
            return node;
        }

        public void Fill(TipDotFunctionTypeHasNewConditionNode source)
        {
            Types = source.Types == null ? null : new List<int>(source.Types);
            Arg1List = source.Arg1List == null ? null : new List<int>(source.Arg1List);
        }
    }
}
