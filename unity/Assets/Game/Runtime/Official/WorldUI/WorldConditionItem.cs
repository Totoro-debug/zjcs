// Migrated ConditionItem; only the existing Windows text/state type names differ.
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    public sealed class WorldConditionItem : MonoBehaviour
    {
        [SerializeField] public WorldMultiLanguageText mDesc;
        [SerializeField] public WorldStateSwitcher<bool> mStateSwitcher;

        public WorldConditionItem SetDesc(string key, params object[] desc)
        {
            mDesc.SetKey(key, desc);
            return this;
        }

        public WorldConditionItem SetMet(bool isMet)
        {
            mStateSwitcher.SetState(isMet);
            return this;
        }
    }
}
