using System;
using System.Collections.Generic;
using UnityEngine;

namespace GConfig
{
    // Original parser registry portion. Loading/job scheduling is migrated separately.
    public partial class ConfigManager : MonoBehaviour, IDisposable
    {
        private Dictionary<Type, IParser> parsers = new Dictionary<Type, IParser>();
        public void Dispose()
        {
            foreach (var parser in parsers.Values)
                if (parser is IDisposable disposable) disposable.Dispose();
        }
        public T GetParser<T>() where T : IParser
        {
            return parsers.TryGetValue(typeof(T), out var parser) ? (T)parser : default(T);
        }
        public IParser GetParser(Type type)
        {
            return parsers.TryGetValue(type, out var parser) ? parser : null;
        }
        public void RegisterTypes(Type[] types)
        {
            foreach (var type in types)
            {
                if (parsers.ContainsKey(type))
                {
                    Debug.LogWarningFormat("解析器重复注册: {0}", type);
                    continue;
                }
                try
                {
                    var parser = (IParser)Activator.CreateInstance(type);
                    parser.Initialize(this);
                    parsers.Add(type, parser);
                }
                catch (Exception error)
                {
                    Debug.LogErrorFormat("解析器注册失败: {0}\n{1}", type, error);
                }
            }
        }
        public void RegisterType<T>() where T : IParser, new()
        {
            if (parsers.ContainsKey(typeof(T)))
            {
                Debug.LogWarningFormat("解析器重复注册: {0}", typeof(T));
                return;
            }
            try
            {
                var parser = new T();
                parser.Initialize(this);
                parsers.Add(typeof(T), parser);
            }
            catch (Exception error)
            {
                Debug.LogErrorFormat("解析器注册失败: {0}\n{1}", typeof(T), error);
            }
        }
    }
}
