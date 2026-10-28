using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace GConfig
{
    // Original Config.dll pool, not System.Threading.ThreadPool. Native
    // 0x991c860, 0x991c52c, 0x991acd0, 0x991eb14 and 0x991ec04.
    public class ThreadPool : IDisposable
    {
        private const int MaxConcurrentWorkItems = 1000000;
        private ConcurrentQueue<WorkItem> queue;
        private Semaphore semaphore;
        private volatile bool exit;
        private Thread[] threads;
        private struct WorkItem { public Action<object> Callback; public object State; }
        public int ThreadCount => threads.Length;

        public ThreadPool(int threadCount)
        {
            queue = new ConcurrentQueue<WorkItem>();
            semaphore = new Semaphore(0, MaxConcurrentWorkItems);
            int available = Environment.ProcessorCount - 1;
            int count = Math.Max(1, threadCount > 0 ? Math.Min(threadCount, available) : available);
            threads = new Thread[count];
            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(ProcessInThread);
                thread.Start();
                threads[i] = thread;
            }
            Debug.LogFormat("GConfig创建了{0}个工作线程", count);
        }

        public void Dispose()
        {
            if (threads == null) return;
            exit = true;
            semaphore.Release(threads.Length);
            foreach (var thread in threads) thread.Join();
            semaphore.Dispose();
            semaphore = null;
            Debug.LogFormat("GConfig销毁了{0}个工作线程", threads.Length);
            threads = null;
        }

        public void QueueUserWorkItem(Action<object> callback, object state)
        {
            queue.Enqueue(new WorkItem { Callback = callback, State = state });
            semaphore.Release();
        }
        private void ProcessInThread()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            while (!exit)
            {
                if (queue.TryDequeue(out var item)) Process(item);
                else semaphore.WaitOne();
            }
        }
        private void Process(WorkItem item)
        {
            try { item.Callback(item.State); }
            catch (Exception error) { Debug.LogError("解析配置表的工作线程异常\n" + error); }
        }
    }
}
