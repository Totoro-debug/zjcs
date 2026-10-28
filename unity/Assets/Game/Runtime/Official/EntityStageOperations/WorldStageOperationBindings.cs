using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.EntityStageOperations
{
    public interface IWorldStageOperationState
    {
        ECViewEntityData Entity { get; }
        bool IsActive();
    }
    public interface IWorldStageInteractionStarter
    {
        void StartStage(ECWorldStageStateData state, int subIndex,
            Dictionary<StageComponentType,object> conditions, object stageData, Action<object,int> callback);
    }
    public interface IWorldStageWindowMapRoot
    {
        bool Initialized { get; }
        IDisposable SubscribeHookMapInput(EcEntityIdentifier id);
    }
    public sealed class StagePopupWindowOperation { public GameObject GameObject; }
    public interface IWorldStageWindowHost
    {
        UniTask<StagePopupWindowOperation> Popup(string name);
        UniTask<StagePopupWindowOperation> PopupAndReport(string name, SDKEventPanelId id, SDKEventPanelSource source);
        WorldStageUiContext GetUiContext(ECWorldStageStateData stage);
    }
    public static class WorldStageOperationServices
    {
        public static IServiceProvider ApplicationScope { get; set; }
        public static IWorldStageWindowHost WindowHost { get; set; }
        public static IWorldStageWindowHost RequireWindowHost => WindowHost ??
            throw new InvalidOperationException("Original stage window host is not bound.");
        public static IWorldStageOperationState State(ECWorldStageStateData stage) => stage as IWorldStageOperationState ??
            throw new InvalidOperationException("Original stage entity/activity state is not bound.");
    }
}
