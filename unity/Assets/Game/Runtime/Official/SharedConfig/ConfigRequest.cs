using System;
using System.Threading;
using UnityEngine;

namespace GConfig
{
    public enum ParseStatus { NotStarted, Loading, Loaded, Parsing, Parsed, Finish }
    public delegate void StatusChanged(ConfigRequestBase request);
    public interface IConfigParseJob
    {
        string File { get; }
        ConfigRequestBase Request { get; }
        bool LoadAsync { get; }
        void Load(ConfigManager configManager);
        void Parse();
        void PostParse();
    }

    // Original atomic transitions and publication order from 0x991b8d4..0x991e6f8.
    public abstract class ConfigRequestBase : CustomYieldInstruction
    {
        public object LoadedData { get; private set; }
        public object Result { get; private set; }
        public bool Success { get; private set; }
        public IConfigParseJob Job { get; internal set; }
        public event StatusChanged StatusChanged;
        private int status;
        public ParseStatus Status { get { Thread.MemoryBarrier(); return (ParseStatus)status; } }
        public override bool keepWaiting => Status != ParseStatus.Finish;

        public void StartLoading()
        {
            int previous = Interlocked.CompareExchange(ref status, 1, 0);
            if (previous != 0) throw StateError(previous, 0);
            StatusChanged?.Invoke(this);
        }
        public void EndLoading(object data)
        {
            int previous = Interlocked.CompareExchange(ref status, 2, 1);
            if (previous != 1) throw StateError(previous, 1);
            LoadedData = data;
            StatusChanged?.Invoke(this);
        }
        public void StartParsing()
        {
            int previous = Interlocked.CompareExchange(ref status, 3, 2);
            if (previous != 2) throw StateError(previous, 2);
            StatusChanged?.Invoke(this);
        }
        public void EndParsing(object result)
        {
            int previous = Interlocked.CompareExchange(ref status, 4, 3);
            if (previous != 3) throw StateError(previous, 3);
            Result = result;
            StatusChanged?.Invoke(this);
        }
        public void Finish(bool success)
        {
            int previous = Interlocked.Exchange(ref status, 5);
            if (previous == 5) throw new Exception("ConfigRequest状态错误: 重复完成");
            Success = success;
            StatusChanged?.Invoke(this);
            OnFinish();
        }
        private static Exception StateError(int actual, int expected) =>
            new Exception(string.Format("ConfigRequest状态错误: {0}，应该是 {1}", (ParseStatus)actual, (ParseStatus)expected));
        protected abstract void OnFinish();
    }

    public class ConfigRequest : ConfigRequestBase
    {
        public event Action<object> Finished;
        protected override void OnFinish()
        {
            try { Finished?.Invoke(Result); }
            catch (Exception error) { Debug.LogError("Failed to call callback\n" + error); }
        }
    }
    public class ConfigRequest<T> : ConfigRequestBase
    {
        public new T Result => (T)base.Result;
        public event Action<T> Finished;
        protected override void OnFinish()
        {
            try { Finished?.Invoke(Result); }
            catch (Exception error) { Debug.LogError("Failed to call callback\n" + error); }
        }
    }
}
