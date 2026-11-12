// Selected original input dispatcher and global-pointer operation.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.HomeMainUiControls
{
    // OS input is read only by the desktop owner. Isolated validation supplies
    // process-local samples through this same boundary, without system input.
    public interface IHomePointerSnapshot
    {
        bool LeftDown { get; }
        Vector2 Position { get; }
        int Width { get; }
        int Height { get; }
    }
public interface IInputOperationResult
{
}
public interface IInputOperation
{
    InputOperationLayer Layer { get; }

    Type ResultType { get; }

    int LayerHashCode { get; }

    int ResultTypeHashCode { get; }

    bool UpdateResult();

    void Call(Delegate callback);
}
public interface IInputOperationModule
{
    bool Enable { get; set; }

    void HookOnInput<T>(Action<T> callback) where T : IInputOperationResult;

    void UnhookOnInput<T>(Action<T> callback) where T : IInputOperationResult;

    long Block<T>() where T : IInputOperationResult;

    long Block(InputOperationLayer layer);

    void Unblock(long signature);
}
public enum InputOperationLayer
{
    GlobalInput,
    GameObjectInput,
    IgnoreUIInput
}
public struct InputOperationCallbackInfo
{
    public Delegate Callback { get; private set; }

    public string TraceInfo { get; private set; }

    public InputOperationCallbackInfo(Delegate callback, string traceInfo)
    {
        Callback = callback;
        TraceInfo = traceInfo;
    }

    public InputOperationCallbackInfo(Delegate callback)
    {
        Callback = callback;
        TraceInfo = null;
    }
}
public abstract class InputOperationBase<T> : IInputOperation where T : IInputOperationResult
{
    public abstract InputOperationLayer Layer { get; }

    public Type ResultType => typeof(T);

    public int LayerHashCode { get; private set; }

    public int ResultTypeHashCode { get; private set; }

    public T Result { get; protected set; }

    public abstract bool UpdateResult();

    protected readonly IHomePointerSnapshot pointer;
    private readonly Action<Exception> reportException;
    public InputOperationBase(IHomePointerSnapshot pointer, Action<Exception> reportException)
    {
        this.pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
        this.reportException = reportException ?? throw new ArgumentNullException(nameof(reportException));
        LayerHashCode = Layer.GetHashCode();
        ResultTypeHashCode = ResultType.GetHashCode();
    }

    public void Call(Delegate callback)
    {
        try
        {
            ((Action<T>)callback)(Result);
        }
        catch (Exception exception)
        {
            reportException(exception);
        }
    }

    protected Vector2 GetLimitInScreenPos(Vector2 pos)
    {
        return new Vector2(Mathf.Clamp(pos.x, 0f, (float)pointer.Width), Mathf.Clamp(pos.y, 0f, (float)pointer.Height));
    }

    protected bool CheckInScreen(Vector2 pos)
    {
        if (pos.x >= 0f && pos.y >= 0f && pos.x <= (float)pointer.Width)
        {
            return pos.y <= (float)pointer.Height;
        }
        return false;
    }
}
public class GlobalPointerDownOperation : InputOperationBase<OnGlobalPointerDownResult>
{
    public GlobalPointerDownOperation(IHomePointerSnapshot pointer, Action<Exception> reportException)
        : base(pointer, reportException) { }
    public override InputOperationLayer Layer => InputOperationLayer.GlobalInput;

    public override bool UpdateResult()
    {
        if (!pointer.LeftDown)
        {
            return false;
        }
        Vector2 pos = pointer.Position;
        if (!CheckInScreen(pos))
        {
            return false;
        }
        Result = GetLimitInScreenPos(pos);
        return true;
    }
}
public struct OnGlobalPointerDownResult : IInputOperationResult
{
    public Vector2 ScreenPos { get; private set; }

    public OnGlobalPointerDownResult(Vector2 screenPos)
    {
        ScreenPos = screenPos;
    }

    public static implicit operator OnGlobalPointerDownResult(Vector2 screenPos)
    {
        return new OnGlobalPointerDownResult(screenPos);
    }

    public static implicit operator OnGlobalPointerDownResult(Vector3 screenPos)
    {
        return new OnGlobalPointerDownResult((Vector2)screenPos);
    }

    public static implicit operator Vector2(OnGlobalPointerDownResult result)
    {
        return result.ScreenPos;
    }

    public static implicit operator Vector3(OnGlobalPointerDownResult result)
    {
        return (Vector3)result.ScreenPos;
    }
}
public class InputOperationModule : IInputOperationModule
{
    private readonly Action<string> reportDuplicate;

    private List<IInputOperation> mInputOperations;

    private Dictionary<Type, List<long>> mCallbackExecutionBuffer;

    private Dictionary<long, InputOperationCallbackInfo> mCallbacksMatcher;

    private Dictionary<Delegate, long> mSignaturesMatcher;

    private Dictionary<int, HashSet<long>> mBlockSignatures;

    private uint mBlockSignatureSeed;

    private uint mCallbackSignatureSeed;

    public bool Enable { get; set; }

    public InputOperationModule(IEnumerable<IInputOperation> operations, Action<string> reportDuplicate)
    {
        this.reportDuplicate = reportDuplicate ?? throw new ArgumentNullException(nameof(reportDuplicate));
        mInputOperations = new List<IInputOperation>();
        mCallbackExecutionBuffer = new Dictionary<Type, List<long>>();
        mCallbacksMatcher = new Dictionary<long, InputOperationCallbackInfo>();
        mSignaturesMatcher = new Dictionary<Delegate, long>();
        mBlockSignatures = new Dictionary<int, HashSet<long>>();
        foreach (InputOperationLayer value in Enum.GetValues(typeof(InputOperationLayer)))
        {
            mBlockSignatures.Add(value.GetHashCode(), new HashSet<long>());
        }
        LoadOperations(operations);
    }

private void LoadOperations(IEnumerable<IInputOperation> operations)
    {
        if (operations == null) throw new ArgumentNullException(nameof(operations));
        foreach (IInputOperation inputOperation in operations)
        {
            mInputOperations.Add(inputOperation);
            mCallbackExecutionBuffer.Add(inputOperation.ResultType, new List<long>());
            mBlockSignatures.Add(inputOperation.ResultType.GetHashCode(), new HashSet<long>());
        }
    }

    public void Update()
    {
        if (!Enable)
        {
            return;
        }
        for (int num = mInputOperations.Count - 1; num >= 0; num--)
        {
            IInputOperation inputOperation = mInputOperations[num];
            if (inputOperation.UpdateResult() && mBlockSignatures[inputOperation.LayerHashCode].Count <= 0 && mBlockSignatures[inputOperation.ResultTypeHashCode].Count <= 0 && mCallbackExecutionBuffer.TryGetValue(inputOperation.ResultType, out var value))
            {
                for (int num2 = value.Count - 1; num2 >= 0; num2--)
                {
                    if (!mCallbacksMatcher.TryGetValue(value[num2], out var value2))
                    {
                        value.RemoveAt(num2);
                    }
                    else
                    {
                        inputOperation.Call(value2.Callback);
                    }
                }
            }
        }
    }

    void IInputOperationModule.HookOnInput<T>(Action<T> callback)
    {
        List<long> value;
        if (mSignaturesMatcher.ContainsKey(callback))
        {
            reportDuplicate(callback + " 被重复Hook!");
        }
        else if (mCallbackExecutionBuffer.TryGetValue(typeof(T), out value))
        {
            long num = AcquireCallbackSigniture(callback.GetHashCode());
            value.Add(num);
            mCallbacksMatcher.Add(num, new InputOperationCallbackInfo(callback));
            mSignaturesMatcher.Add(callback, num);
        }
    }

    void IInputOperationModule.UnhookOnInput<T>(Action<T> callback)
    {
        long key = default;
        if (mSignaturesMatcher.Remove((Delegate)callback, out key))
        {
            mCallbacksMatcher.Remove(key);
        }
    }

    long IInputOperationModule.Block<T>()
    {
        return Block(typeof(T).GetHashCode());
    }

    long IInputOperationModule.Block(InputOperationLayer layer)
    {
        return Block(layer.GetHashCode());
    }

    void IInputOperationModule.Unblock(long signature)
    {
        int key = (int)(signature >> 32);
        if (mBlockSignatures.TryGetValue(key, out var value))
        {
            value.Remove(signature);
        }
    }

    private long Block(int source)
    {
        if (!mBlockSignatures.TryGetValue(source, out var value))
        {
            return 0L;
        }
        long num;
        do
        {
            mBlockSignatureSeed = ((mBlockSignatureSeed < uint.MaxValue) ? (mBlockSignatureSeed + 1) : 0u);
            num = ((long)source << 32) | mBlockSignatureSeed;
        }
        while (!value.Add(num));
        return num;
    }

    private long AcquireCallbackSigniture(int source)
    {
        long num;
        do
        {
            mCallbackSignatureSeed = ((mCallbackSignatureSeed < uint.MaxValue) ? (mCallbackSignatureSeed + 1) : 0u);
            num = ((long)source << 32) | mCallbackSignatureSeed;
        }
        while (mCallbacksMatcher.ContainsKey(num));
        return num;
    }
}
}
