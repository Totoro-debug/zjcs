using System;
using System.Reflection;
using GConfig;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Local binding of the unchanged shared GameSettings parser and Parsers singleton.
    public sealed class NativeMap14SharedConfiguration : IDisposable
    {
        public const string SourcePath = "Config/Luban/game_settings.g.bytes";
        public const string ResourcePath = "Map14/SharedConfig/game_settings.g.bytes";
        public const string SettingsSha256 = "07b8e9400e89722fa35ee8d6ac9ff693f00b24cdfd8da2ad91589673989c347e";
        private GameObject owner;
        private FieldInfo instanceField;
        private object previousInstance;
        private object instance;
        public ConfigManager Manager { get; private set; }
        public object Settings { get; private set; }
        public int RootEntityId { get; private set; }

        public NativeMap14SharedConfiguration(NativeMap14SharedAssemblies shared)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            try
            {
                var asset = Resources.Load<TextAsset>(ResourcePath.Substring(0, ResourcePath.LastIndexOf('.')));
                if (!asset) throw new InvalidOperationException("Original shared settings payload is not bound.");
                NativeMap14SharedAssemblies.Verify(asset.bytes, SettingsSha256);
                owner = new GameObject("Map14 original shared configuration");
                Manager = owner.AddComponent<ConfigManager>();
                // Each offline scope owns its resource loader. Restore the
                // original factory binding immediately after initialization.
                IFileLoader previousLoader = GConfig.FileLoader.sFileLoader;
                try
                {
                    GConfig.FileLoader.SetupFileLoader(new DefaultFileLoader());
                    Manager.Initialize("", false, 0);
                }
                finally { GConfig.FileLoader.SetupFileLoader(previousLoader); }
                Manager.PathTransformFunc = path => path == SourcePath ? ResourcePath : throw new InvalidOperationException("Original config path is not bound: " + path);
                Type parserType = shared.CommonType("GameSettingsParser");
                Manager.RegisterTypes(new[] { parserType });
                IParser parser = Manager.GetParser(parserType);
                if (parser == null) throw new InvalidOperationException("Original settings parser registration failed.");
                var attribute = shared.CommonType("GameSettings").GetCustomAttribute<ConfigAttribute>();
                if (attribute == null) throw new InvalidOperationException("Original settings load phases are absent.");
                ConfigRequest request = parser.Parse(attribute.LoadPhases, false);
                if (request == null) throw new InvalidOperationException("Original settings phase was rejected.");
                Manager.Wait(new[] { request });
                if (!request.Success) throw new InvalidOperationException("Original shared settings parsing failed.");
                Settings = parserType.GetMethod("Get").Invoke(parser, null);
                RootEntityId = (int)Settings.GetType().GetField("RootEntityId").GetValue(Settings);
                Type parsersType = shared.Shared.GetType("Parsers", true);
                instanceField = parsersType.GetField("Instance");
                previousInstance = instanceField.GetValue(null);
                instance = Activator.CreateInstance(parsersType, new object[] { Manager });
                instanceField.SetValue(null, instance);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (instanceField != null && instance != null && ReferenceEquals(instanceField.GetValue(null), instance))
                instanceField.SetValue(null, previousInstance);
            instanceField = null;
            instance = null;
            previousInstance = null;
            Settings = null;
            Manager = null;
            if (owner)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(owner);
                else UnityEngine.Object.DestroyImmediate(owner);
                owner = null;
            }
        }
    }
}
