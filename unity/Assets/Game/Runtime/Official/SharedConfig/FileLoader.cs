using UnityEngine;

namespace GConfig
{
    public class FileLoader
    {
        public static IFileLoader sFileLoader = new DefaultFileLoader();
        public static void SetupFileLoader(IFileLoader fileLoader) { sFileLoader = fileLoader; }
    }
    public class DefaultFileLoader : IFileLoader
    {
        private ResourceManager resourceManager;
        public void Initialize(ConfigManager manager)
        {
            resourceManager = manager.gameObject.GetComponent<ResourceManager>();
            if (resourceManager == null) resourceManager = manager.gameObject.AddComponent<ResourceManager>();
        }
        public void LoadFile(string file, bool isBinary, bool async, ConfigRequestBase request)
        {
            if (isBinary) LoadBinaryFile(file, async, request);
            else LoadTextFile(file, async, request);
        }
        public bool Exists(string file) => resourceManager.Exists(file);
        private void LoadTextFile(string file, bool async, ConfigRequestBase request)
        {
            if (async) resourceManager.LoadAsync<TextAsset>(file, asset =>
            {
                var textAsset = asset as TextAsset;
                request.EndLoading(textAsset != null ? textAsset.text : null);
            });
            else
            {
                TextAsset asset = resourceManager.Load<TextAsset>(file);
                request.EndLoading(asset != null ? asset.text : null);
            }
        }
        private void LoadBinaryFile(string file, bool async, ConfigRequestBase request)
        {
            if (async) resourceManager.LoadAsync<TextAsset>(file, asset =>
            {
                var textAsset = asset as TextAsset;
                request.EndLoading(textAsset != null ? textAsset.bytes : null);
            });
            else
            {
                TextAsset asset = resourceManager.Load<TextAsset>(file);
                request.EndLoading(asset != null ? asset.bytes : null);
            }
        }
    }
}
