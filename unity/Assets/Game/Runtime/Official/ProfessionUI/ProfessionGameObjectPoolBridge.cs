using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public static class ProfessionGameObjectPoolBridge
    {
        // Source scene-container instantiation applies injection before exposing
        // the borrowed object. The offline scope supplies those same bindings.
        public static Func<Transform, GameObject, Transform, bool, GameObject> Provider { get; set; }
        public static GameObject Instantiate(Transform scope, GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if (Provider == null) throw new InvalidOperationException("Original item pool scene injection is unbound.");
            return Provider(scope, prefab, parent, worldPositionStays);
        }
    }
}
