// Selected original SafeAction<T1..T4>; explicit local exception reporter.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
public sealed class HomeTaskPlayerEvent<T1>
{
private readonly HashSet<Action<T1>> mActionSet = new HashSet<Action<T1>>();
private readonly Stack<List<Action<T1>>> mStack = new Stack<List<Action<T1>>>();
readonly Action<Exception> reportException;
public HomeTaskPlayerEvent(Action<Exception> reportException) { this.reportException=reportException??throw new ArgumentNullException(nameof(reportException)); }
    private List<Action<T1>> PopList()
    {
        if (mStack.Count > 0)
        {
            return mStack.Pop();
        }
        return new List<Action<T1>>();
    }

    private void PushList(List<Action<T1>> list)
    {
        mStack.Push(list);
    }

    public void AddListener(Action<T1> action)
    {
        mActionSet.Add(action);
    }

    public void RemoveListener(Action<T1> action)
    {
        mActionSet.Remove(action);
    }

    public void ClearListener()
    {
        mActionSet.Clear();
    }

    public void Invoke(T1 param1)
    {
        if (mActionSet.Count == 0)
        {
            return;
        }
        List<Action<T1>> list = PopList();
        list.Clear();
        list.AddRange(mActionSet);
        foreach (Action<T1> item in list)
        {
            try
            {
                item(param1);
            }
            catch (Exception exception) { reportException(exception); }
        }
        list.Clear();
        PushList(list);
    }

}
public sealed class HomeTaskPlayerEvent<T1, T2>
{
private readonly HashSet<Action<T1, T2>> mActionSet = new HashSet<Action<T1, T2>>();
private readonly Stack<List<Action<T1, T2>>> mStack = new Stack<List<Action<T1, T2>>>();
readonly Action<Exception> reportException;
public HomeTaskPlayerEvent(Action<Exception> reportException) { this.reportException=reportException??throw new ArgumentNullException(nameof(reportException)); }
    private List<Action<T1, T2>> PopList()
    {
        if (mStack.Count > 0)
        {
            return mStack.Pop();
        }
        return new List<Action<T1, T2>>();
    }

    private void PushList(List<Action<T1, T2>> list)
    {
        mStack.Push(list);
    }

    public void AddListener(Action<T1, T2> action)
    {
        mActionSet.Add(action);
    }

    public void RemoveListener(Action<T1, T2> action)
    {
        mActionSet.Remove(action);
    }

    public void ClearListener()
    {
        mActionSet.Clear();
    }

    public void Invoke(T1 param1, T2 param2)
    {
        if (mActionSet.Count == 0)
        {
            return;
        }
        List<Action<T1, T2>> list = PopList();
        list.Clear();
        list.AddRange(mActionSet);
        foreach (Action<T1, T2> item in list)
        {
            try
            {
                item(param1, param2);
            }
            catch (Exception exception) { reportException(exception); }
        }
        list.Clear();
        PushList(list);
    }

}
public sealed class HomeTaskPlayerEvent<T1, T2, T3>
{
private readonly HashSet<Action<T1, T2, T3>> mActionSet = new HashSet<Action<T1, T2, T3>>();
private readonly Stack<List<Action<T1, T2, T3>>> mStack = new Stack<List<Action<T1, T2, T3>>>();
readonly Action<Exception> reportException;
public HomeTaskPlayerEvent(Action<Exception> reportException) { this.reportException=reportException??throw new ArgumentNullException(nameof(reportException)); }
    private List<Action<T1, T2, T3>> PopList()
    {
        if (mStack.Count > 0)
        {
            return mStack.Pop();
        }
        return new List<Action<T1, T2, T3>>();
    }

    private void PushList(List<Action<T1, T2, T3>> list)
    {
        mStack.Push(list);
    }

    public void AddListener(Action<T1, T2, T3> action)
    {
        mActionSet.Add(action);
    }

    public void RemoveListener(Action<T1, T2, T3> action)
    {
        mActionSet.Remove(action);
    }

    public void ClearListener()
    {
        mActionSet.Clear();
    }

    public void Invoke(T1 param1, T2 param2, T3 param3)
    {
        if (mActionSet.Count == 0)
        {
            return;
        }
        List<Action<T1, T2, T3>> list = PopList();
        list.Clear();
        list.AddRange(mActionSet);
        foreach (Action<T1, T2, T3> item in list)
        {
            try
            {
                item(param1, param2, param3);
            }
            catch (Exception exception) { reportException(exception); }
        }
        list.Clear();
        PushList(list);
    }

}
public sealed class HomeTaskPlayerEvent<T1, T2, T3, T4>
{
private readonly HashSet<Action<T1, T2, T3, T4>> mActionSet = new HashSet<Action<T1, T2, T3, T4>>();
private readonly Stack<List<Action<T1, T2, T3, T4>>> mStack = new Stack<List<Action<T1, T2, T3, T4>>>();
readonly Action<Exception> reportException;
public HomeTaskPlayerEvent(Action<Exception> reportException) { this.reportException=reportException??throw new ArgumentNullException(nameof(reportException)); }
    private List<Action<T1, T2, T3, T4>> PopList()
    {
        if (mStack.Count > 0)
        {
            return mStack.Pop();
        }
        return new List<Action<T1, T2, T3, T4>>();
    }

    private void PushList(List<Action<T1, T2, T3, T4>> list)
    {
        mStack.Push(list);
    }

    public void AddListener(Action<T1, T2, T3, T4> action)
    {
        mActionSet.Add(action);
    }

    public void RemoveListener(Action<T1, T2, T3, T4> action)
    {
        mActionSet.Remove(action);
    }

    public void ClearListener()
    {
        mActionSet.Clear();
    }

    public void Invoke(T1 param1, T2 param2, T3 param3, T4 param4)
    {
        if (mActionSet.Count == 0)
        {
            return;
        }
        List<Action<T1, T2, T3, T4>> list = PopList();
        list.Clear();
        list.AddRange(mActionSet);
        foreach (Action<T1, T2, T3, T4> item in list)
        {
            try
            {
                item(param1, param2, param3, param4);
            }
            catch (Exception exception) { reportException(exception); }
        }
        list.Clear();
        PushList(list);
    }

}
}
