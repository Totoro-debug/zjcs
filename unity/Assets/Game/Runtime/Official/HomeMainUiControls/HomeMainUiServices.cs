using System;
using UnityEngine;
using Zjcs.Official.HomeHudUI;

namespace Zjcs.Official.HomeMainUiControls
{
    // Dependency boundaries for the original parser, player, input and window
    // calls. Implementations bind the current local player before activation.
    public interface IHomeMainUiConditions
    {
        int TaskFirstTraceConditionId { get; }
        IHomeHudCondition GetCondition(int id);
        IHomeHudCondition GetFunctionCondition(FunctionType type);
        IHomeHudConditionHandler ConditionHandler { get; }
        bool IsMet(IHomeHudCondition condition);
        bool HasMainMap { get; }
        bool OpenMapChannel { get; }
    }
    public struct HomeUiGlobalPointerDownResult { public Vector2 ScreenPos; }
    public interface IHomeMainUiInput
    {
        void HookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> listener);
        void UnhookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> listener);
    }
    public interface IHomeMainFunctionEntryDialog { void Init(); }
    public interface IHomeMainUiWindows
    {
        bool HasPanel { get; }
        bool HasDialog { get; }
        void ReportFunctionEntryClick();
        void PopupFunctionEntryDialog(Action<IHomeMainFunctionEntryDialog> initialize);
    }
}
