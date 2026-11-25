// The original NewFunctionManager collection and notifications; lifecycle owners supply triggers.
using System;
using System.Collections.Generic;

namespace Zjcs.Official.HomeTipDots
{
    public sealed class HomeNewFunctionState
    {
        private readonly Dictionary<int, HashSet<int>> mNewFunctions = new Dictionary<int, HashSet<int>>();

        public event Action<int, int> OnNewFunction;
        public event Action<int, int> OnRemoveNewFunction;

        public void Reset() => mNewFunctions.Clear();

        public void AddNewFunction(int type, int arg1 = 0)
        {
            if (!mNewFunctions.TryGetValue(type, out HashSet<int> values))
            {
                values = new HashSet<int>();
                mNewFunctions.Add(type, values);
            }
            values.Add(arg1);
            OnNewFunction?.Invoke(type, arg1);
        }

        public bool IsNew(int type, int arg1 = 0)
        {
            if (!mNewFunctions.TryGetValue(type, out HashSet<int> values)) return false;
            if (values.Count == 0) return false;
            if (arg1 == 0) return true;
            return values.Contains(arg1);
        }

        public void RemoveNewFunction(int type, int arg1 = 0)
        {
            if (mNewFunctions.TryGetValue(type, out HashSet<int> values))
            {
                values.Remove(arg1);
                if (values.Count == 0) mNewFunctions.Remove(type);
                OnRemoveNewFunction?.Invoke(type, arg1);
            }
        }

        public HashSet<int> GetNewFunctionArgsByType(int type)
        {
            if (!mNewFunctions.TryGetValue(type, out HashSet<int> values)) return null;
            return values;
        }
    }
}
