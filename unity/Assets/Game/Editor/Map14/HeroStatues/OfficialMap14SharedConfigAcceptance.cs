using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using GConfig;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14SharedConfigAcceptance
    {
        public static IEnumerator Verify(NativeMap14SharedConfiguration configuration, NativeMap14SharedAssemblies shared)
        {
            var trace = new List<ParseStatus>();
            var request = new ConfigRequest();
            request.StatusChanged += value => trace.Add(value.Status);
            object finished = null;
            request.Finished += value => finished = value;
            request.StartLoading();
            request.EndLoading("loaded");
            request.StartParsing();
            request.EndParsing("parsed");
            request.Finish(true);
            Require(string.Join(",", trace) == "Loading,Loaded,Parsing,Parsed,Finish" && request.Success && !request.keepWaiting && Equals(finished, "parsed"), "request publication/finish order differs");
            var rejected = new ConfigRequest();
            bool threw = false;
            try { rejected.EndLoading("invalid"); } catch (Exception) { threw = true; }
            Require(threw && rejected.Status == ParseStatus.NotStarted && rejected.LoadedData == null, "invalid transition changed original state");
            Type parserType = shared.CommonType("GameSettingsParser");
            IParser parser = configuration.Manager.GetParser(parserType);
            var attribute = shared.CommonType("GameSettings").GetCustomAttribute<ConfigAttribute>();
            ConfigRequest asynchronous = parser.Parse(attribute.LoadPhases, true);
            float deadline = Time.realtimeSinceStartup + 30;
            while (asynchronous.keepWaiting)
            {
                Require(Time.realtimeSinceStartup < deadline, "original async resource request timed out");
                yield return null;
            }
            Require(asynchronous.Success && asynchronous.LoadedData is byte[] bytes && bytes.Length == 7881 && asynchronous.Result != null, "original async settings parsing failed");
            NativeMap14SharedAssemblies.Verify((byte[])asynchronous.LoadedData, NativeMap14SharedConfiguration.SettingsSha256);
            object settings = parserType.GetMethod("Get").Invoke(parser, null);
            int rootId = (int)settings.GetType().GetField("RootEntityId").GetValue(settings);
            Require(rootId == configuration.RootEntityId, "sync/async root setting differs");
            object container = Activator.CreateInstance(shared.CommonType("EcContainer"));
            int maximum = (int)settings.GetType().GetField("PreConfigEntityIdMax").GetValue(settings);
            MethodInfo allocate = container.GetType().GetMethod("NewEntityIndex");
            Require((int)allocate.Invoke(container, null) == maximum + 1 && (int)allocate.Invoke(container, null) == maximum + 2, "unchanged container allocation did not consume original settings");
            var singletonField = shared.Shared.GetType("Parsers", true).GetField("Instance");
            object originalSingleton = singletonField.GetValue(null);
            IFileLoader originalLoader = GConfig.FileLoader.sFileLoader;
            using (var nested = new NativeMap14SharedConfiguration(shared))
            {
                Require(!ReferenceEquals(originalSingleton, singletonField.GetValue(null)) && ReferenceEquals(originalLoader, GConfig.FileLoader.sFileLoader), "scoped parser/loader ownership differs");
            }
            Require(ReferenceEquals(originalSingleton, singletonField.GetValue(null)), "previous shared parser singleton was not restored");
            ConfigRequest afterNested = parser.Parse(attribute.LoadPhases, false);
            configuration.Manager.Wait(new[] { afterNested });
            Require(afterNested.Success, "nested disposal broke the outer resource loader");
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int workerThread = mainThread;
            object state = new object();
            object observed = null;
            using (var completed = new ManualResetEvent(false))
            {
                configuration.Manager.ResetThreadPool(1);
                try
                {
                    Require(configuration.Manager.BackgroundThreadCount == 1, "original pool size differs");
                    configuration.Manager.QueueUserWorkItem(value => { observed = value; workerThread = Thread.CurrentThread.ManagedThreadId; completed.Set(); }, state);
                    deadline = Time.realtimeSinceStartup + 30;
                    while (!completed.WaitOne(0))
                    {
                        Require(Time.realtimeSinceStartup < deadline, "original worker callback timed out");
                        yield return null;
                    }
                    Require(ReferenceEquals(observed, state) && workerThread != mainThread, "original queue did not execute on its worker");
                }
                finally { configuration.Manager.ResetThreadPool(0); }
            }
            Require(configuration.Manager.BackgroundThreadCount == 0, "original worker pool did not stop");
            Debug.Log("Map14 original shared configuration passed: exact Luban payload, original Console parser, synchronous/asynchronous Resources loading, atomic request states and unchanged container allocation.");
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidDataException("Original shared config: " + message);
        }
    }
}
