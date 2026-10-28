using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace GConfig.Parser
{
    public class CatalogChildParseJob<T>
    {
        public interface ILoadJob { }
        public interface IParseJob { }
        private class LoadJob : ILoadJob { }
        private class ParseJob : IParseJob { }
        private static readonly ILoadJob loadJob = new LoadJob();
        private static readonly IParseJob parseJob = new ParseJob();
        private readonly Action<T> finishCallback;
        private readonly bool loadAsync;
        private readonly List<ISupportParseSingle> parseParsers;
        private readonly List<ILoadSingle> loadParsers;
        private readonly List<string> files;
        private readonly List<object> intermediateResults;
        private IEnumerator ie;
        private int waitingCount;
        private T result;
        public List<object> IntermediateResults => intermediateResults;
        public T Result => result;
        internal CatalogChildParseJob(Action<T> finishCallback, bool loadAsync)
        {
            this.finishCallback = finishCallback;
            this.loadAsync = loadAsync;
            intermediateResults = new List<object>();
            parseParsers = new List<ISupportParseSingle>();
            loadParsers = new List<ILoadSingle>();
            files = new List<string>();
        }
        public void AddToParse(ISupportParseSingle parser, string file)
        {
            if (parser == null) throw new ArgumentNullException("parser");
            if (string.IsNullOrEmpty(file)) throw new ArgumentNullException("file");
            parseParsers.Add(parser);
            files.Add(file);
        }
        public void AddToLoad(ILoadSingle parser, string file)
        {
            if (parser == null) throw new ArgumentNullException("parser");
            if (string.IsNullOrEmpty(file)) throw new ArgumentNullException("file");
            loadParsers.Add(parser);
            files.Add(file);
        }
        public void ClearIntermediateResults() => intermediateResults.Clear();
        public void SetResult(T result) { this.result = result; }
        public IParseJob Parse() => parseJob;
        public ILoadJob Load() => loadJob;
        internal void Start(IEnumerator ie) { this.ie = ie; Step(); }
        private void ParseInternal()
        {
            string file = string.Empty;
            try
            {
                int count = parseParsers.Count;
                if (count == 0) throw new Exception("没有需要解析的文件");
                waitingCount = count;
                intermediateResults.Clear();
                for (int i = 0; i < count; i++) intermediateResults.Add(null);
                for (int i = 0; i < count; i++)
                {
                    int index = i;
                    file = files[i];
                    parseParsers[i].ParseSingle(files[i], value =>
                    {
                        try
                        {
                            intermediateResults[index] = value;
                            if (Interlocked.Decrement(ref waitingCount) == 0) Step();
                        }
                        catch (Exception error)
                        {
                            Debug.LogError(string.Format("解析文件失败：{0}", file) + "\n" + error);
                            finishCallback(default(T));
                        }
                    }, loadAsync);
                }
            }
            catch (Exception error)
            {
                Debug.LogError(string.Format("解析文件失败：{0}", file) + "\n" + error);
                finishCallback(default(T));
            }
        }
        private void LoadInternal()
        {
            string file = string.Empty;
            try
            {
                int count = loadParsers.Count;
                if (count == 0) throw new Exception("没有需要加载的文件");
                waitingCount = count;
                intermediateResults.Clear();
                for (int i = 0; i < count; i++) intermediateResults.Add(null);
                for (int i = 0; i < count; i++)
                {
                    int index = i;
                    file = files[i];
                    loadParsers[i].LoadSingle(files[i], value =>
                    {
                        try
                        {
                            intermediateResults[index] = value;
                            if (Interlocked.Decrement(ref waitingCount) == 0) Step();
                        }
                        catch (Exception error)
                        {
                            Debug.LogError(string.Format("加载文件失败：{0}", file) + "\n" + error);
                            finishCallback(default(T));
                        }
                    }, loadAsync);
                }
            }
            catch (Exception error)
            {
                Debug.LogError(string.Format("加载文件失败：{0}", file) + "\n" + error);
                finishCallback(default(T));
            }
        }
        private void Step()
        {
            parseParsers.Clear();
            files.Clear();
            while (ie.MoveNext())
            {
                object current = ie.Current;
                if (ReferenceEquals(current, parseJob))
                {
                    if (parseParsers.Count > 0) { ParseInternal(); return; }
                    intermediateResults.Clear();
                }
                else if (ReferenceEquals(current, loadJob))
                {
                    if (loadParsers.Count > 0) { LoadInternal(); return; }
                    intermediateResults.Clear();
                }
            }
            finishCallback(result);
        }
    }
}
