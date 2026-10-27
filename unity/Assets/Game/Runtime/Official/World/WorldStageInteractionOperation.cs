using System;

namespace Zjcs.Official.World
{
    // Serialized from ECViewDriver, not a table of hand-selected map interactions.
    [Serializable]
    public sealed class WorldStageInteractionOperation
    {
        public string Type, WindowName;
        public long Id, MeetId, NoMeetId, EndOperationId;
        public int[] ConditionIds;
        public bool NeedAllMeet, IsReport;
        public int Scenario, PanelId;
        public long[] OperationIds;
        public WorldStageWindowClick[] Clicks;
        [NonSerialized] private bool storyPlaying;

        // Mirrors Condition/Sequence/Click/OnlyStory/Window.DoOper. The context
        // replaces ECS service lookup and server/window transport only.
        public bool DoOper(IWorldStageOperationContext context)
        {
            if (context == null) return false;
            switch (Type)
            {
                case "ECWorldStageInteractionOperationCondition":
                    if (ConditionIds == null || ConditionIds.Length == 0) return false;
                    bool met = NeedAllMeet;
                    foreach (int id in ConditionIds)
                    {
                        bool? value = context.Condition(id);
                        if (NeedAllMeet && value == false) { met = false; break; }
                        if (!NeedAllMeet && value == true) { met = true; break; }
                    }
                    return context.ResolveOperation(met ? MeetId : NoMeetId)?.DoOper(context) ?? false;
                case "ECWorldStageInteractionOperationSequence":
                    if (OperationIds == null || OperationIds.Length == 0) return false;
                    bool success = true;
                    foreach (long operation in OperationIds)
                    {
                        bool result = context.ResolveOperation(operation).DoOper(context); // Does not await a story.
                        if (!result) context.ReportError("ECWorldStageInteractionOperationSequence child failed: " + operation);
                        success &= result;
                    }
                    return success;
                case "ECWorldStageInteractionOperationClick":
                    if (context.IsStageActive) context.StartStage(0);
                    return true;
                case "ECWorldStageInteractionOperationOnlyStory":
                    if (storyPlaying) return true;
                    storyPlaying = true;
                    context.PlayStory(Scenario, () =>
                    {
                        storyPlaying = false;
                        context.ResolveOperation(EndOperationId)?.DoOper(context);
                    });
                    return true;
                case "ECWorldStageInteractionOperationWindow":
                    if (string.IsNullOrEmpty(WindowName)) return false;
                    context.OpenWindow(WindowName, IsReport, PanelId, type => OnClick(context, type));
                    return true;
                default: throw new InvalidOperationException("Unconverted official stage operation: " + Type);
            }
        }

        public void OnClick(IWorldStageOperationContext context, int type)
        {
            if (Clicks == null || Clicks.Length == 0) return;
            foreach (WorldStageWindowClick click in Clicks)
                if (click.Type == type) { context.ResolveOperation(click.OperationId)?.DoOper(context); return; }
            context.ReportError("Original stage window has no matching click: " + type);
        }
    }

    [Serializable] public sealed class WorldStageWindowClick
    {
        public int Type;
        public long OperationId;
    }

    public interface IWorldStageOperationContext
    {
        bool IsStageActive { get; }
        void ReportError(string message);
        bool? Condition(int id);
        WorldStageInteractionOperation ResolveOperation(long id);
        void StartStage(int subState);
        void PlayStory(int scenario, Action completed);
        void OpenWindow(string name, bool report, int panelId, Action<int> click);
    }
}
