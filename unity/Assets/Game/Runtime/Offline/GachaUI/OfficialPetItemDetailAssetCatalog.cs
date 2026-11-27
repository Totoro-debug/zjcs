using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.GachaUI
{
    public sealed class OfficialPetItemDetailAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class Resource { public string path, assetGuid; public UnityEngine.Object asset; }
        public GameObject UseDetail, Detail, Choose, ChooseItem, ResultIcon, ItemSourceItem, ItemIcon;
        public TextAsset Texts, Rules;
        public ItemIconConfig IconConfig;
        public ItemIconAttachmentConfig AttachmentConfig;
        public Material GrayMaterial, GrayFontMaterial;
        public Resource[] Images;
        public static OfficialPetItemDetailAssetCatalog LoadDefault()
            => UnityEngine.Resources.Load<OfficialPetItemDetailAssetCatalog>("PetGacha/OfficialPetItemDetailAssetCatalog")
                ?? throw new InvalidOperationException("Original pet score item-detail resources are unbound.");
    }
}
