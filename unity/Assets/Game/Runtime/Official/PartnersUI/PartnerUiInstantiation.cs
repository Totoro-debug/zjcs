using UnityEngine;

namespace Zjcs.Official.PartnersUI
{
    internal static class PartnerUiInstantiation
    {
        // UIUtils.Instantiate(GameObject, parent, All): the partner filter clones
        // have no injected services. Keep all four original ResetUI operations.
        internal static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);
            return instance;
        }
    }
}
