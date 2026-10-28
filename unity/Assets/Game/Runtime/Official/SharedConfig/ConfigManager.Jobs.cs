using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace GConfig
{
    public partial class ConfigManager
    {
        private static List<object> mConcurrentBag = new List<object>();
        public Func<string, string> PathTransformFunc { get; set; }
        private bool supportAsyncParsing;
        public bool UseJobSystemThread;
        private string rootDir = string.Empty;
        private IFileLoader fileLoader;
        private ConcurrentQueue<IConfigParseJob> loadQueue = new ConcurrentQueue<IConfigParseJob>();
        private Action<float> progressListener;
        private ConfigBatchRequest batchRequest;
        private ThreadPool threadPool;
        public string RootDir => rootDir;
        public IFileLoader FileLoader => fileLoader;
        public string TransformPath(string path) => PathTransformFunc != null ? PathTransformFunc(path) : path;
        public int BackgroundThreadCount => supportAsyncParsing ? threadPool.ThreadCount : 0;
        public void QueueUserWorkItem(Action<object> callback, object state)
        {
            if (supportAsyncParsing) threadPool.QueueUserWorkItem(callback, state);
            else callback(state);
        }

        public void AddJob(IConfigParseJob job)
        {
            job.Request.StatusChanged += OnStatusChanged;
            loadQueue.Enqueue(job);
        }
        private void OnStatusChanged(ConfigRequestBase request)
        {
            switch (request.Status)
            {
                case ParseStatus.Loaded:
                    if (!supportAsyncParsing) ParseJob(request.Job);
                    else if (UseJobSystemThread)
                    {
                        int index=mConcurrentBag.Count;
                        mConcurrentBag.Add(request.Job);
                        new ConfigParseJob { Index=index }.Schedule();
                    }
                    else threadPool.QueueUserWorkItem(job => ParseJob((IConfigParseJob)job), request.Job);
                    break;
                case ParseStatus.Parsed: PostParseJob(request.Job); break;
            }
        }
        private void LoadJob(IConfigParseJob job)
        {
            try { job.Request.StartLoading(); job.Load(this); }
            catch (Exception error)
            {
                Debug.LogError("加载配置文件失败: " + job.File + "\n" + error);
                job.Request.Finish(false);
            }
        }
        private static void ParseJob(IConfigParseJob job)
        {
            try
            {
                if (job.Request.LoadedData != null) { job.Request.StartParsing(); job.Parse(); }
                else
                {
                    Debug.LogError("加载配置文件失败: " + job.File);
                    job.Request.Finish(false);
                }
            }
            catch (Exception error)
            {
                Debug.LogError("解析配置文件失败: " + job.File + "\n" + error);
                job.Request.Finish(false);
            }
        }
        private void PostParseJob(IConfigParseJob job)
        {
            try { job.PostParse(); job.Request.Finish(true); }
            catch (Exception error)
            {
                Debug.LogError("解析配置文件失败: " + job.File + "\n" + error);
                job.Request.Finish(false);
            }
        }
        private void Update()
        {
            while (loadQueue.TryDequeue(out var job)) LoadJob(job);
            if (batchRequest != null && progressListener != null) InvokeProgressListener(batchRequest.Progress);
        }
        private void InvokeProgressListener(float progress)
        {
            try { progressListener(progress); }
            catch (Exception error) { Debug.LogError("调用进度监听器失败\n"+error); }
        }
        public void Wait(ConfigRequest[] requests)
        {
            if (requests == null || requests.Length == 0) return;
            for (int i=0; i<requests.Length; i++)
                if (requests[i].Status != ParseStatus.Finish) { Update(); i=-1; }
        }
        internal void Wait(ConfigBatchRequest request) { while (!request.Completed) Update(); }
        private void OnDestroy() { if (threadPool != null) { threadPool.Dispose(); threadPool=null; } }
        private struct ConfigParseJob : IJob
        {
            public int Index;
            public void Execute()
            {
                ParseJob((IConfigParseJob)mConcurrentBag[Index]);
                mConcurrentBag[Index] = null;
            }
        }
    }
}
