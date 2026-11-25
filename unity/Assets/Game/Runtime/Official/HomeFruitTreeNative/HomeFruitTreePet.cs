// Original pet-tree function branch; the host supplies local window ownership.
using Zjcs.Official.HomeUI;

namespace Zjcs.Official.HomeFruitTreeNative
{
    public interface IHomePetTreeFeatureServices
    {
        bool IsPetGachaDisabled();
        void PopupPetGacha();
    }

    public class HomeFruitTreePet : HomeFruitTreeSubFurniture
    {
        public IHomePetTreeFeatureServices PetTreeFeatureServices { get; set; }

        public override bool TryCallFunction()
        {
            if (!Services.PlayerData.HasFurniture(EFurniture.FruitTree2)) return false;
            if (PetTreeFeatureServices.IsPetGachaDisabled())
            {
                Services.ShowError(53814);
                return false;
            }
            if (Services.CheckFunctionDisabled(EFurniture.FruitTree2))
            {
                Services.ShowError(53851);
                return false;
            }
            PetTreeFeatureServices.PopupPetGacha();
            return true;
        }
    }
}
