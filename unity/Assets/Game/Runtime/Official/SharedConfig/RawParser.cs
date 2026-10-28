using System;

namespace GConfig.Parser
{
    public abstract class RawParser : ParserBase, ILoadSingle, ISupportParseSingle
    {
        protected abstract ParsePhase ParsePhase { get; }
        protected abstract string Path { get; }
        protected abstract bool IsBinary { get; }
        public ConfigRequest LoadSingle(string file,bool loadAsync=true) => LoadSingleInternal(file,null,loadAsync);
        public void LoadSingle(string file,Action<object> callback,bool loadAsync=true) { LoadSingleInternal(file,callback,loadAsync); }
        public ConfigRequest ParseSingle(string file,bool loadAsync=true) => ParseSingleInternal(file,null,loadAsync);
        public void ParseSingle(string file,Action<object> callback,bool loadAsync=true) { ParseSingleInternal(file,callback,loadAsync); }
        protected override ConfigRequest ParseInternal(ParsePhase phase,Action<object> callback,bool loadAsync=true)
        {
            if ((ParsePhase & phase)==0) return null;
            var request=new ConfigRequest();
            if (callback!=null) request.Finished+=callback;
            string file=ConfigManager.RootDir+Path;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request,file,loadAsync,Load,ParseFile,PostParse));
            return request;
        }
        private ConfigRequest LoadSingleInternal(string file,Action<object> callback,bool loadAsync)
        {
            var request=new ConfigRequest();
            request.Finished+=callback;
            string path=ConfigManager.RootDir+Path+file;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request,path,loadAsync,Load,(req,data)=>req.EndParsing(data),null));
            return request;
        }
        private ConfigRequest ParseSingleInternal(string file,Action<object> callback,bool loadAsync)
        {
            var request=new ConfigRequest();
            if (callback!=null) request.Finished+=callback;
            string path=ConfigManager.RootDir+Path+file;
            ConfigManager.AddJob(ConfigParseJobFactory.Create(request,path,loadAsync,Load,ParseFile,PostParse));
            return request;
        }
        private void Load(ConfigRequestBase request,string path,bool async) => ConfigManager.FileLoader.LoadFile(path,IsBinary,async,request);
        protected virtual void PostParse(object config) { }
        protected abstract void ParseFile(ConfigRequestBase request,object data);
    }
}
