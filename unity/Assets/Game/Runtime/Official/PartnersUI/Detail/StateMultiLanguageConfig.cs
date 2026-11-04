using System;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.PartnersUI
{
    // Selected Framework.StateMultiLanguageConfig; serialized field and branch
    // retain the single-label form used by the original detail window.
    [Serializable]
    public sealed class StateMultiLanguageConfig : IWorldStateConfig
    {
        [SerializeField] private WorldStateMultiLanguageData mStateMultiLanguageTextData;
        public void SetState(WorldStateConfigValue state)
        {
            if (mStateMultiLanguageTextData.Keys.TryGetValue(state, out var value))
                mStateMultiLanguageTextData.Text.SetKey(value);
        }
        public void Clear() { }
    }
}
