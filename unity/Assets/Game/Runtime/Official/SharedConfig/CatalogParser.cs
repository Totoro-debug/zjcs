using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace GConfig.Parser
{
    public abstract class CatalogParser<T, TCache> : ParserBase, ISupportParseSingle where TCache : CatalogCache<T>, new()
    {
        protected CatalogConfigAttribute configAttr;
        public override void Initialize(ConfigManager manager)
        {
            base.Initialize(manager);
            configAttr = GetType().GetCustomAttribute<CatalogConfigAttribute>();
            if (configAttr == null) throw new Exception("类型没有添加CatalogConfigAttribute: " + GetType());
        }
        public ConfigRequest ParseSingle(string file, bool loadAsync = true)
        {
            var request = new ConfigRequest();
            string path = ConfigManager.RootDir + configAttr.Path + file;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request, path, loadAsync, Load,
                (req, data) => ParseCatalogFile(req, path, data), null));
            return request;
        }
        public void ParseSingle(string file, Action<object> callback, bool loadAsync = true)
        {
            var request = new ConfigRequest();
            request.Finished += callback;
            string path = ConfigManager.RootDir + configAttr.Path + file;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request, path, loadAsync, Load,
                (req, data) => ParseCatalogFile(req, path, data), null));
        }
        public ConfigRequest ReloadChildFile(string file, bool loadAsync = true)
        {
            var request = new ConfigRequest();
            string path = ConfigManager.RootDir + configAttr.Path + file;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request, path, loadAsync,
                (req, unusedFile, unusedAsync) => req.EndLoading(string.Empty),
                (req, unusedData) => ParseSingleFile(req, path, file), null));
            return request;
        }
        public bool Exists(string file) => ConfigManager.FileLoader.Exists(ConfigManager.RootDir + configAttr.Path + file);
        protected override ConfigRequest ParseInternal(ParsePhase phase, Action<object> callback, bool loadAsync = true)
        {
            if ((configAttr.LoadPhases & phase) == 0) return null;
            var request = new ConfigRequest();
            if (callback != null) request.Finished += callback;
            string path = ConfigManager.RootDir + configAttr.Path;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request, path, loadAsync, Load,
                (req, data) => ParseCatalogFile(req, path, data), null));
            return request;
        }
        protected virtual void PreParse(TCache cache) { }
        protected abstract IEnumerator ParseChildFile(TCache cache, CatalogChildParseJob<T> job, int index);
        protected abstract object PostParse(TCache cache);
        private void Load(ConfigRequestBase request, string file, bool async) => ConfigManager.FileLoader.LoadFile(file, false, async, request);
        private void ParseCatalogFile(ConfigRequestBase request, string catalogFile, object data)
        {
            string[] files = ((string)data).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var cache = new TCache { CatalogFile = catalogFile, Files = files, Configs = new T[files.Length], ParsingCount = files.Length, Request = request };
            PreParse(cache);
            if (files.Length == 0) { EndParsing(cache); return; }
            bool loadAsync = request.Job.LoadAsync;
            for (int i = 0; i < files.Length; i++) ProcessChildFile(cache, i, loadAsync);
        }
        private void ProcessChildFile(TCache cache, int index, bool loadAsync)
        {
            var job = new CatalogChildParseJob<T>(value =>
            {
                cache.Configs[index] = value;
                if (Interlocked.Decrement(ref cache.ParsingCount) == 0) EndParsing(cache);
            }, loadAsync);
            job.Start(ParseChildFile(cache, job, index));
        }
        private void EndParsing(TCache cache)
        {
            try { cache.Request.EndParsing(PostParse(cache)); }
            catch (Exception error)
            {
                Debug.LogError("解析配置文件失败: " + GetType() + "\n" + error);
                cache.Request.Finish(false);
            }
        }
        private void ParseSingleFile(ConfigRequestBase request, string catalogFile, string file)
        {
            var cache = new TCache { CatalogFile = catalogFile, Files = new[] { file }, Configs = new T[1], ParsingCount = 1, Request = request };
            PreParse(cache);
            ProcessChildFile(cache, 0, request.Job.LoadAsync);
        }
    }
}
