using System;
using System.IO;

namespace Zjcs.Offline.State
{
    public static class UserDataStorage
    {
        private static string root;
        public static string Root
        {
            get
            {
                if (root != null) return root;
                string validation = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (!string.IsNullOrWhiteSpace(validation))
                    return root = Path.GetFullPath(Path.Combine(validation, "profiles"));
#if UNITY_EDITOR
                // Editor sessions keep their own local profiles.
                return root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../UserData"));
#else
                return root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
#endif
            }
        }
    }
}
