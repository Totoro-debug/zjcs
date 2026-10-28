using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    [Serializable]
    public sealed class WorldStateMultiLanguageData
    {
        public WorldMultiLanguageText Text;
        public WorldStateKVMatch<string> Keys;
    }

    // Framework.StateMultiLanguagesConfig. A missing key stops this array,
    // rather than continuing to later labels; preserve that original branch.
    [Serializable]
    public sealed class WorldStateMultiLanguagesConfig : IWorldStateConfig
    {
        [SerializeField] private WorldStateMultiLanguageData[] mStateMultiLanguageTextDatas;
        public static WorldStateMultiLanguagesConfig FromBindings(WorldStateMultiLanguageData[] data)
            => new WorldStateMultiLanguagesConfig { mStateMultiLanguageTextDatas = data };
        public void SetState(WorldStateConfigValue state)
        {
            if (mStateMultiLanguageTextDatas == null || mStateMultiLanguageTextDatas.Length == 0) return;
            foreach (var data in mStateMultiLanguageTextDatas)
            {
                if (!data.Keys.TryGetValue(state, out string key)) break;
                data.Text.SetKey(key);
            }
        }
        public void Clear() { }
    }
}
