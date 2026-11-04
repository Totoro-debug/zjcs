using UnityEngine;

namespace Zjcs.Official.PartnersUI
{
    public sealed class SkeletonGraphicProxy : MonoBehaviour
    {
        [SerializeField] private Component mSkeletonGraphic;
        public Component SkeletonGraphic => mSkeletonGraphic;
    }
}
