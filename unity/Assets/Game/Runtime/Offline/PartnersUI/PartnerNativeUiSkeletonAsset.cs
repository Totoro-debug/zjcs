using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerNativeUiSkeletonAsset : ScriptableObject
    {
        [SerializeField] private TextAsset payload;
        [SerializeField] private int npcId;
        [SerializeField] private string[] combineSkins;
        [SerializeField] private float defaultMix;
        private ProfessionNativeSkeletonData data;
        public ProfessionNativeSkeletonData Data => data ??= ProfessionNativeSkeletonData.LoadPartnerUi(
            payload ? payload.bytes : throw new InvalidOperationException("Original UI payload is unbound."), npcId);
        public string[] CombineSkins => combineSkins;
        public float DefaultMix => defaultMix;
    }
}
