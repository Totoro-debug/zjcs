using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // The two original Framework.TransformExtensions destruction methods.
    public static class ProfessionUseItemTransformExtensions
    {
        public static void DestroyChildren(this Transform transform, bool includeInactive = true, bool detachChildren = false)
        {
            for (int num = transform.childCount - 1; num >= 0; num--)
            {
                Transform child = transform.GetChild(num);
                if (includeInactive || child.gameObject.activeSelf)
                    Object.Destroy(child.gameObject);
                if (detachChildren)
                    child.SetParent(null);
            }
        }

        public static void DestroyActivedChildren(this Transform transform, bool detachChildren = false)
        {
            transform.DestroyChildren(includeInactive: false, detachChildren);
        }
    }
}
