using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.PartnersUI
{
    internal static class PartnerDetailUiUtils
    {
        // Consumed UIUtils.Instantiate<T>(GameObject,Transform,All): injected
        // creation, position/rotation/scale reset, activation, GetComponent.
        public static T Instantiate<T>(GameObject prefab, Transform parent)
        {
            GameObject go = ProfessionGameObjectPoolBridge.Instantiate(parent, prefab, parent, false);
            Transform transform = go.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            go.SetActive(true);
            return go.GetComponent<T>();
        }
    }
}
