// Original selected ECWorldStageView/provider fields and state-event order.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.SteleEntities
{
    public interface IECWorldStageStateCfgBaseProvider
    {
        WorldStageStateCfg GetStageStateCfg(int state);
        void Release();
    }
    [Serializable] public struct ECWorldStageStateCfgBaseProviderItemData
    { [SerializeReference] public IECWorldStageStateCfgBaseProvider ECWorldStageCfgProvider; }
    public interface IECWorldStageInteractionOperation
    {
        bool DoOper(IWorldStageOperationContext context);
        void Release();
    }
    [Serializable] public struct ECWorldStageInteractionOperationItemData
    { [SerializeReference] public IECWorldStageInteractionOperation InteractionOperation; }
    [Serializable] public sealed class WorldStageStateCfg
    {
        public string myUIName;
        public GameObject myStartPlayable, myLoopPlayable;
        public ECWorldStageInteractionOperationItemData InteractionOperation;
    }
    [Serializable] public sealed class ECWorldStageCfgNormalProvider : IECWorldStageStateCfgBaseProvider
    {
        [SerializeField] private List<WorldStageStateCfg> mStateCfgs;
        public WorldStageStateCfg GetStageStateCfg(int state) => state >= mStateCfgs.Count || state < 0 ? null : mStateCfgs[state];
        public void Release() { foreach (WorldStageStateCfg cfg in mStateCfgs) cfg.InteractionOperation.InteractionOperation?.Release(); }
    }
    [Serializable] public struct WorldStageInteractionOperationWindowClickData
    { public int Type; public ECWorldStageInteractionOperationItemData OperationData; }
    [Serializable] public sealed class ECWorldStageInteractionOperationWindow : IECWorldStageInteractionOperation
    {
        [SerializeField] private bool mIsReport;
        [SerializeField] private int mSDKEventPanelId;
        [SerializeField] private string mWindowName;
        [SerializeField] private List<WorldStageInteractionOperationWindowClickData> mClickDatas;
        [NonSerialized] private IWorldStageOperationContext mContext;
        public bool DoOper(IWorldStageOperationContext context)
        {
            if (context == null || string.IsNullOrEmpty(mWindowName)) return false;
            mContext = context;
            context.OpenWindow(mWindowName, mIsReport, mSDKEventPanelId, OnClick);
            return true;
        }
        private void OnClick(int type)
        {
            if (mClickDatas.Count <= 0) return;
            foreach (WorldStageInteractionOperationWindowClickData click in mClickDatas)
                if (click.Type == type) { click.OperationData.InteractionOperation?.DoOper(mContext); return; }
            mContext.ReportError("Original stele window has no matching click: " + type);
        }
        public void Release() { if (mClickDatas != null) foreach (var click in mClickDatas) click.OperationData.InteractionOperation?.Release(); }
    }
    [Serializable] public sealed class ECWorldStageView : ECViewBase
    {
        public SpriteRenderer EntityIcon;
        [SerializeField] private bool mCanInteractAfterMove = true;
        public WorldStateSwitcher<int> StageSwitcher;
        [SerializeField] private ECWorldStageStateCfgBaseProviderItemData mWorldStageCfgProviderData;
        public int State => mEntity.WorldStageState;
        public event Action<int> OnStateUpdate;
        protected override void Load()
        {
            base.Load();
            mEntity.EntityReplaced += InitStateEffect;
            InitStateEffect();
            mEntity.WorldStageChanged += WorldStageUpdate;
        }
        protected override void Unload()
        {
            mEntity.WorldStageChanged -= WorldStageUpdate;
            mEntity.EntityReplaced -= InitStateEffect;
            mWorldStageCfgProviderData.ECWorldStageCfgProvider.Release();
            base.Unload();
        }
        private void InitStateEffect() => UpdateStageSwitcher();
        private void UpdateStageSwitcher() { if (StageSwitcher != null) StageSwitcher.SetState(State); }
        private void WorldStageUpdate(int previous) { UpdateStageSwitcher(); OnStateUpdate?.Invoke(previous); }
        public WorldStageStateCfg GetCurStateCfg() => mWorldStageCfgProviderData.ECWorldStageCfgProvider.GetStageStateCfg(State);
        public int GetStateComponentIndex(int componentType, int subStateIndex = 0) => mEntity == null ? -1 : mEntity.GetStateComponentIndex(componentType, subStateIndex);
        public bool CanInteractAfterMove() => mCanInteractAfterMove;
        public bool DoInteraction() => GetCurStateCfg()?.InteractionOperation.InteractionOperation?.DoOper(mEntity.OperationContext) ?? false;
    }
}
