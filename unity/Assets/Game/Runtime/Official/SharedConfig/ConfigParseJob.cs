namespace GConfig
{
    public delegate void LoadDelegate(ConfigRequestBase request, string file, bool loadAsync);
    public delegate void ParseDelegate(ConfigRequestBase request, object data);
    public delegate void PostParseDelegate(object data);
    public delegate void ParseDelegate<T>(ConfigRequestBase request, object data);
    public delegate void PostParseDelegate<T>(T data);

    public class ConfigParseJob : IConfigParseJob
    {
        private string file;
        private ConfigRequest request;
        private bool loadAsync;
        private LoadDelegate loadFunc;
        private ParseDelegate parseFunc;
        private PostParseDelegate postParseFunc;
        public string File => file;
        public ConfigRequestBase Request => request;
        public bool LoadAsync => loadAsync;
        public ConfigParseJob(ConfigRequest request, string file, bool loadAsync, LoadDelegate loadFunc, ParseDelegate parseFunc, PostParseDelegate postParseFunc)
        {
            this.request=request;this.file=file;this.loadAsync=loadAsync;
            this.loadFunc=loadFunc;this.parseFunc=parseFunc;this.postParseFunc=postParseFunc;
            request.Job=this;
        }
        public void Load(ConfigManager configManager) => loadFunc(request,configManager.TransformPath(file),loadAsync);
        public void Parse() => parseFunc(request,request.LoadedData);
        public void PostParse() { postParseFunc?.Invoke(request.Result); }
    }

    public class ConfigParseJob<T> : IConfigParseJob
    {
        private string file;
        private ConfigRequest<T> request;
        private bool loadAsync;
        private LoadDelegate loadFunc;
        private ParseDelegate<T> parseFunc;
        private PostParseDelegate<T> postParseFunc;
        public string File => file;
        public ConfigRequestBase Request => request;
        public bool LoadAsync => loadAsync;
        public ConfigParseJob(ConfigRequest<T> request, string file, bool loadAsync, LoadDelegate loadFunc, ParseDelegate<T> parseFunc, PostParseDelegate<T> postParseFunc)
        {
            this.request=request;this.file=file;this.loadAsync=loadAsync;
            this.loadFunc=loadFunc;this.parseFunc=parseFunc;this.postParseFunc=postParseFunc;
            request.Job=this;
        }
        public void Load(ConfigManager configManager) => loadFunc(request,configManager.TransformPath(file),loadAsync);
        public void Parse() => parseFunc(request,request.LoadedData);
        public void PostParse() { postParseFunc?.Invoke(request.Result); }
    }
    public static class ConfigParseJobFactory
    {
        public static IConfigParseJob Create(ConfigRequest request,string file,bool loadAsync,LoadDelegate loadFunc,ParseDelegate parseFunc,PostParseDelegate postParseFunc) =>
            new ConfigParseJob(request,file,loadAsync,loadFunc,parseFunc,postParseFunc);
        public static IConfigParseJob Create<T>(ConfigRequest<T> request,string file,bool loadAsync,LoadDelegate loadFunc,ParseDelegate<T> parseFunc,PostParseDelegate<T> postParseFunc) =>
            new ConfigParseJob<T>(request,file,loadAsync,loadFunc,parseFunc,postParseFunc);
    }
}
