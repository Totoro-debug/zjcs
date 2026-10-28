using System;
using GConfig.Helper;
using Newtonsoft.Json;

namespace GConfig
{
    public interface IParseSingleFromMemory
    {
        object ParseSingleFromMemory(object data);
    }
}

namespace GConfig.Parser
{
    public class JsonParser<T> : ParserBase<T>, ISupportParseSingle, ISupportParseSingle<T>, IParseSingleFromMemory where T : IConfig
    {
        protected T config;
        protected JsonSerializerSettings settings;
        public ConfigRequest ParseSingle(string file, bool loadAsync = true) => ParseSingleInternal(file, null, loadAsync);
        public void ParseSingle(string file, Action<object> callback, bool loadAsync = true) { ParseSingleInternal(file, callback, loadAsync); }
        public ConfigRequest<T> ParseSingleGeneric(string file, bool loadAsync = true) => ParseSingleGenericInternal(file, null, loadAsync);
        public void ParseSingleGeneric(string file, Action<T> callback, bool loadAsync = true) { ParseSingleGenericInternal(file, callback, loadAsync); }
        public object ParseSingleFromMemory(object data)
        {
            T result = JsonHelper.FromJson<T>((string)data, settings);
            PostParse(result);
            return result;
        }
        protected override ConfigRequest ParseInternal(ParsePhase phase, Action<object> callback, bool loadAsync = true)
        {
            if ((configAttr.LoadPhases & phase) == 0) return null;
            var request = new ConfigRequest();
            if (callback != null) request.Finished += callback;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request, ConfigManager.RootDir + configAttr.Path,
                loadAsync, Load, ParseFile, value => { config = (T)value; PostParse(config); }));
            return request;
        }
        private ConfigRequest ParseSingleInternal(string file, Action<object> callback, bool loadAsync)
        {
            var request = new ConfigRequest();
            request.Finished += callback;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request, ConfigManager.RootDir + configAttr.Path + file,
                loadAsync, Load, ParseFile, value => PostParse((T)value)));
            return request;
        }
        private ConfigRequest<T> ParseSingleGenericInternal(string file, Action<T> callback, bool loadAsync)
        {
            var request = new ConfigRequest<T>();
            request.Finished += callback;
            ConfigManager.AddJob(ConfigParseJobFactory.Create<T>(request, ConfigManager.RootDir + configAttr.Path + file,
                loadAsync, Load, ParseFile, PostParse));
            return request;
        }
        public T Get() => config;
        protected virtual void PostParse(T value) { }
        private void Load(ConfigRequestBase request, string file, bool async) => ConfigManager.FileLoader.LoadFile(file, false, async, request);
        private void ParseFile(ConfigRequestBase request, object data) => request.EndParsing(JsonHelper.FromJson<T>((string)data, settings));
    }
}
