using System;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class PetGachaNativeModel
    {
        public int PetClassId;
        public int UIModel;
        public GameObject Prefab;
    }

    public sealed class OfficialPetGachaAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "PetGacha/official-pet-gacha-assets";
        public string Manifest;
        public PetGachaNativeModel[] NativeModels;

        public static OfficialPetGachaAssetCatalog LoadDefault()
        {
            OfficialPetGachaAssetCatalog catalog = Resources.Load<OfficialPetGachaAssetCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official pet gacha native models are not installed.");
            return catalog;
        }

        public GameObject NativeModel(int petClassId, int uiModel)
        {
            PetGachaNativeModel entry = (NativeModels ?? Array.Empty<PetGachaNativeModel>())
                .FirstOrDefault(value => value != null && value.PetClassId == petClassId && value.UIModel == uiModel);
            return entry == null ? null : entry.Prefab;
        }

        public void Validate(PetGachaRuntimeCatalog rules)
        {
            if (Manifest != "854_156383" || rules == null || NativeModels == null)
                throw new FormatException("The official pet gacha native model catalog is missing or unsupported.");
            foreach (PetGachaPetProfile profile in rules.PetProfiles)
                if (NativeModel(profile.ClassId, profile.UIModel) == null)
                    throw new FormatException("The official native pet model is not installed: " + profile.ClassId);
        }
    }
}
