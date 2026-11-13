using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
// Original SafeAction without arguments; only the log sink is injected.
public sealed class HomeTaskTraceEvent
{
    readonly HashSet<Action> mActionSet=new HashSet<Action>();
    readonly Stack<List<Action>> mStack=new Stack<List<Action>>();
    readonly Action<Exception> reportException;
    public HomeTaskTraceEvent(Action<Exception> reportException){this.reportException=reportException??throw new ArgumentNullException(nameof(reportException));}
private List<Action> PopList()
    {
        if (mStack.Count > 0)
        {
            return mStack.Pop();
        }
        return new List<Action>();
    }
private void PushList(List<Action> list)
    {
        mStack.Push(list);
    }
public void AddListener(Action action)
    {
        mActionSet.Add(action);
    }
public void RemoveListener(Action action)
    {
        mActionSet.Remove(action);
    }
public void ClearListener()
    {
        mActionSet.Clear();
    }
public void Invoke()
    {
        if (mActionSet.Count == 0)
        {
            return;
        }
        List<Action> list = PopList();
        list.Clear();
        list.AddRange(mActionSet);
        foreach (Action item in list)
        {
            try
            {
                item();
            }
            catch (Exception exception) { reportException(exception); }
        }
        list.Clear();
        PushList(list);
    }
}
}
