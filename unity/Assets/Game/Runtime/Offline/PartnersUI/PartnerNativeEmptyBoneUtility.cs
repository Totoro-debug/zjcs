using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    // Retains the original empty utility binding without introducing a second
    // bone hierarchy. All motion remains in the independent pose producer.
    public sealed class PartnerNativeEmptyBoneUtility : MonoBehaviour
    {
        [SerializeField] private Transform boneRoot;
        [SerializeField] private bool flipBy180DegreeRotation;
        [SerializeField] private Component skeletonRenderer, skeletonGraphic;
        private void Awake()
        {
            if (boneRoot || flipBy180DegreeRotation || skeletonRenderer || !skeletonGraphic)
                throw new NotSupportedException("Bind a populated original bone utility explicitly before using it.");
        }
    }
}
