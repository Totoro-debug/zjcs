using System.IO;
using UnityEngine;

namespace Zjcs.Offline.BattleNativePlayables
{
    // Raw AKPK files are opened by the native engine, outside Unity serialization.
    public static class OfflineBattleWwisePackages
    {
        public const string AssetDirectory = "Assets/Official/Combat/NativeSound/Packages";
        public const string StreamingDirectory = "Combat/Wwise";

        public static string DirectoryPath
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDirectory));
#else
                return Path.Combine(Application.streamingAssetsPath, StreamingDirectory);
#endif
            }
        }
    }
}
