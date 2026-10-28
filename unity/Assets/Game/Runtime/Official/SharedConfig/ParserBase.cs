using System;
using System.Reflection;

namespace GConfig
{
    public interface IParser
    {
        void Initialize(ConfigManager manager);
        ConfigRequest Parse(ParsePhase phase, bool loadAsync = true);
        bool Parse(ParsePhase phase, Action<object> callback, bool loadAsync = true);
        void Validate();
        void PostParse();
        int GetProcessWeight();
    }

    // Complete original base method bodies, including the original no-op hooks.
    public abstract class ParserBase : IParser
    {
        private ConfigManager configManager;
        public ConfigManager ConfigManager => configManager;
        public virtual void Initialize(ConfigManager manager) { configManager = manager; }
        public ConfigRequest Parse(ParsePhase phase, bool loadAsync = true) => ParseInternal(phase, null, loadAsync);
        public bool Parse(ParsePhase phase, Action<object> callback, bool loadAsync = true) => ParseInternal(phase, callback, loadAsync) != null;
        public virtual void Validate() { }
        public virtual void PostParse() { }
        public virtual int GetProcessWeight() => 1;
        protected abstract ConfigRequest ParseInternal(ParsePhase phase, Action<object> callback, bool loadAsync = true);
    }

    public abstract class ParserBase<T> : ParserBase where T : IConfig
    {
        protected ConfigAttribute configAttr;
        // Original shared generic body 0xe330a44; query T, not the parser type.
        public override void Initialize(ConfigManager manager)
        {
            base.Initialize(manager);
            configAttr = typeof(T).GetCustomAttribute<ConfigAttribute>();
            if (configAttr == null) throw new Exception("类型没有添加ConfigAttribute: " + typeof(T));
        }
    }

    public interface ISupportParseSingle
    {
        ConfigRequest ParseSingle(string file, bool loadAsync = true);
        void ParseSingle(string file, Action<object> callback, bool loadAsync = true);
    }
    public interface ISupportParseSingle<T>
    {
        ConfigRequest<T> ParseSingleGeneric(string file, bool loadAsync = true);
        void ParseSingleGeneric(string file, Action<T> callback, bool loadAsync = true);
    }
    public interface ILoadSingle
    {
        ConfigRequest LoadSingle(string file, bool loadAsync = true);
        void LoadSingle(string file, Action<object> callback, bool loadAsync = true);
    }
    public interface ILoadSingle<T>
    {
        ConfigRequest<T> LoadSingleGeneric(string file, bool loadAsync = true);
        void LoadSingleGeneric(string file, Action<T> callback, bool loadAsync = true);
    }
}
