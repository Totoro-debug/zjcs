using System;
using System.Collections.Generic;
using UnityEngine;

namespace GConfig
{
    public class ResourceManager : MonoBehaviour
    {
        private const int WorkerCount = 8;
        private readonly Queue<LoadItem> queue = new Queue<LoadItem>();
        private LoadWorker[] workers = new LoadWorker[8];
        private bool isLoading;
        private class LoadItem
        {
            public string Asset;
            public Type Type;
            public Action<object> Callback;
        }
        private class LoadWorker
        {
            public LoadItem Item;
            public ResourceRequest Request;
            public bool IsIdle => Item == null;
            public void Clear() { Item = null; Request = null; }
        }
        private void Awake() { for (int i = 0; i < workers.Length; i++) workers[i] = new LoadWorker(); }
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            int extension = path.LastIndexOf('.');
            if (extension >= 0) path = path.Substring(0, extension);
            T asset = Resources.Load<T>(path);
            if (asset == null) Debug.LogErrorFormat("资源不存在或者不是期望的类型: {0} {1}", path, typeof(T));
            return asset;
        }
        public void LoadAsync<T>(string asset, Action<object> callback) where T : UnityEngine.Object
        {
            queue.Enqueue(new LoadItem { Asset = asset, Type = typeof(T), Callback = callback });
        }
        public bool Exists(string file) => true;
        private void Update()
        {
            if (!isLoading && queue.Count == 0) return;
            int idleCount = 0;
            foreach (LoadWorker worker in workers)
            {
                if (worker.IsIdle) { idleCount++; continue; }
                ResourceRequest request = worker.Request;
                if (!request.isDone) continue;
                LoadItem item = worker.Item;
                worker.Clear();
                idleCount++;
                if (request.asset == null) Debug.LogErrorFormat("资源不存在或者不是期望的类型: {0} {1}", item.Asset, item.Type);
                try { item.Callback?.Invoke(request.asset); }
                catch (Exception error) { Debug.LogError("Failed to call callback\n" + error); }
            }
            if (idleCount > 0 && queue.Count > 0)
            {
                foreach (LoadWorker worker in workers)
                {
                    if (!worker.IsIdle) continue;
                    LoadItem item = queue.Dequeue();
                    worker.Item = item;
                    string path = item.Asset;
                    int extension = path.LastIndexOf('.');
                    if (extension >= 0) path = path.Substring(0, extension);
                    worker.Request = item.Type != typeof(UnityEngine.Object) ? Resources.LoadAsync(path, item.Type) : Resources.LoadAsync(path);
                    idleCount--;
                    if (queue.Count == 0) break;
                }
            }
            isLoading = idleCount < workers.Length;
        }
    }
}
